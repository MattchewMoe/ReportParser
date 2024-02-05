// Function1.cs in your BlobMonitor project
using System;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Storage;

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReportPaser.Data;
using System.Threading.Tasks;
using ReportParser.Entities;
using System.IO;
using DocumentFormat.OpenXml.InkML;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http;

namespace BlobMonitor
{
    public class CompSaleExtractor
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger _logger;
        private readonly ScanDocxForComparableSales _scanDocxService;
        private readonly AppDbContext _dbContext;
        private readonly ComparableSaleMapper _comparableSaleMapper;
        private readonly ComparableSaleMapperFactory _comparableSaleMapperFactory;
        // Functions.cs
        private readonly string _blobStorageBaseUrl = "https://appraisalapistorage.blob.core.windows.net/";
        private readonly string BingMapsApiKey;


        public CompSaleExtractor(
        ILoggerFactory loggerFactory,
        ScanDocxForComparableSales scanDocxService,
        AppDbContext dbContext,
        BlobServiceClient blobServiceClient,
        ComparableSaleMapperFactory mapperFactory,
        IConfiguration configuration)  // Inject IConfiguration here
        {
            _logger = loggerFactory.CreateLogger<CompSaleExtractor>();
            _scanDocxService = scanDocxService;
            _dbContext = dbContext;
            _blobServiceClient = blobServiceClient;
            _comparableSaleMapperFactory = mapperFactory;
            BingMapsApiKey = "AjpnWMgE0iRBJHlY0jtVgEWVfLcrvY-YQh1RZi-kfPpVJBnEpacjIEEaVY037UY5";// Now you can access BingMapsApiKey from configuration
        }
        [Function("CompSaleExtractor")]
        public async Task Run([BlobTrigger("reports/{name}", Connection = "AzureStorage")] byte[] myBlob, string name, FunctionContext context)
        {
            _logger.LogInformation($"C# Blob trigger function processed blob\n Name: {name} \n Size: {myBlob.Length} Bytes");
            var comparableSaleMapper = _comparableSaleMapperFactory.Create(_logger);

            try
            {
                _logger.LogInformation("Starting to process blob...");
                using (MemoryStream memStream = new MemoryStream(myBlob))
                {
                    _logger.LogInformation("Opening Word document...");
                    using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(memStream, false))
                    {
                        _logger.LogInformation("Extracting Sales Data and Images...");
                        var salesData = await _scanDocxService.ExtractSalesDataAndImages(wordDocument);

                        _logger.LogInformation("Looping through salesData...");
                        foreach (var saleKvp in salesData)
                        {
                            List<string> imageUrls = null;

                            _logger.LogInformation("Checking if Images key exists...");
                            if (saleKvp.Value.Count >= 5)
                            {
                                if (saleKvp.Value.ContainsKey("Images") && saleKvp.Value["Images"] is List<ExtractedImage> extractedImages)
                                {
                                    _logger.LogInformation("Uploading images to blob storage...");
                                    imageUrls = await UploadImagesToBlobStorage(extractedImages);
                                    saleKvp.Value["Images"] = imageUrls;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("The dictionary contains fewer than 5 keys. Skipping image upload.");
                            }

                            var saleDict = saleKvp.Value
                                                .Where(kvp => kvp.Key != "Images" && kvp.Value is string)
                                                .ToDictionary(k => k.Key, k => k.Value.ToString());

                            _logger.LogInformation("Mapping sale data to ComparableSale object...");
                            var rawComparableSale = comparableSaleMapper.MapDictionaryToObject<ComparableSale>(saleDict);
                            ComparableSale compSale = comparableSaleMapper.GenerateComparableSale(rawComparableSale, _logger);

                            if (compSale != null)
                            {
                                _logger.LogInformation("Processed ComparableSale object successfully.");

                                // Set the blob URL for the OriginatingReport
                                compSale.OriginatingReportUrl = $"{_blobStorageBaseUrl}reports/{name}";
                                compSale.ImageUrls = imageUrls;

                                // Geocoding logic
                                if (!string.IsNullOrEmpty(compSale.Location))
                                {
                                    try
                                    {
                                        var (latitude, longitude) = await GeocodeAddressAsync(compSale.Location);
                                        var formattedAddress = await ReverseGeocodeAsync(latitude, longitude);

                                        compSale.City = formattedAddress.City;
                                        compSale.County = formattedAddress.County;
                                        compSale.StreetAddress = formattedAddress.StreetAddress;
                                        compSale.State = formattedAddress.State;
                                        compSale.PostalCode = formattedAddress.PostalCode;
                                        compSale.Latitude = latitude;
                                        compSale.Longitude = longitude;

                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError($"Geocoding failed for location '{compSale.Location}': {ex.Message}");
                                    }
                                }
                                compSale.IsVerified = true;
                                compSale.FromInternalReport = true;
                                if (compSale.Zoning.Contains(";"))
                                {
                                    //Take the values before the semicolon
                                    compSale.Zoning = compSale.Zoning.Split(";")[0];
                                    //Take the values after the semicolon and set those to the UseCode
                                    compSale.UseCode = compSale.Zoning.Split(";")[1];
                                }
                                // Add the processed ComparableSale object to the dbContext
                                _logger.LogInformation("Adding ComparableSale object to dbContext...");
                                _dbContext.ComparableSales.Add(compSale);

                                // Save changes to the database
                                await _dbContext.SaveChangesAsync();
                            }
                            else
                            {
                                _logger.LogWarning("compSale object is null, skipping database save.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }
        }

        private async Task<(double latitude, double longitude)> GeocodeAddressAsync(string address)
        {
            using var client = new HttpClient();
            var response = await client.GetStringAsync($"http://dev.virtualearth.net/REST/v1/Locations?q={Uri.EscapeDataString(address)}&key={BingMapsApiKey}");
            var json = JObject.Parse(response);
            var location = json["resourceSets"][0]["resources"][0]["point"]["coordinates"];

            double latitude = location[0].Value<double>();
            double longitude = location[1].Value<double>();

            return (latitude, longitude);
        }
        private async Task<FormattedAddress> ReverseGeocodeAsync(double latitude, double longitude)
        {
            using var client = new HttpClient();
            var response = await client.GetStringAsync($"http://dev.virtualearth.net/REST/v1/Locations/{latitude},{longitude}?o=json&key={BingMapsApiKey}");
            var json = JObject.Parse(response);
            var address = json["resourceSets"][0]["resources"][0]["address"];

            return new FormattedAddress
            {
                StreetAddress = address["addressLine"]?.ToString(),
                City = address["locality"]?.ToString(),
                County = address["adminDistrict2"]?.ToString(),
                State = address["adminDistrict"]?.ToString(),
                PostalCode = address["postalCode"]?.ToString(),
                Country = address["countryRegion"]?.ToString()
            };
        }

        public class FormattedAddress
        {
            public string StreetAddress { get; set; }
            public string City { get; set; }
            public string County { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public string Country { get; set; }
        }



        private async Task<List<string>> UploadImagesToBlobStorage(List<ExtractedImage> images)
        {
            List<string> imageUrls = new List<string>();
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient("container-comp-images");

            // Check if the container exists, create if not
            if (!await containerClient.ExistsAsync())
            {
                await containerClient.CreateIfNotExistsAsync();
            }

            foreach (var image in images)
            {
                try
                {
                    // Generate a unique name for the blob
                    string blobName = Guid.NewGuid().ToString() + "." + image.ImageFormat.Split('/')[1]; // Extracts 'png' from 'image/png'

                    BlobClient blobClient = containerClient.GetBlobClient(blobName);

                    // Set the content type based on your ImageFormat
                    BlobUploadOptions uploadOptions = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = image.ImageFormat
                        }
                    };

                    using (var stream = new MemoryStream(image.ImageData))
                    {
                        await blobClient.UploadAsync(stream, uploadOptions, CancellationToken.None);

                        // Set metadata for the blob (like caption)
                        IDictionary<string, string> metadata = new Dictionary<string, string>
                {
                    { "Caption", image.Caption }
                };
                        await blobClient.SetMetadataAsync(metadata);

                        imageUrls.Add(blobClient.Uri.ToString());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to upload image: {ex.Message}");
                }
            }

            _logger.LogInformation($"Uploaded {imageUrls.Count} out of {images.Count} images to blob storage");
            _logger.LogInformation($"Image URLs: {string.Join(", ", imageUrls)}");

            return imageUrls;
        }

    }

}

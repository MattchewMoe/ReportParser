using DocumentFormat.OpenXml.Packaging;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Drawing;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;
using Newtonsoft.Json;
using System.Text;
using Azure.Storage.Blobs;
using ReportParser.Entities;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace BlobMonitor
{
    public class ScanDocxForComparableSales
    {
        private readonly ILogger<ScanDocxForComparableSales> _logger;


        public ScanDocxForComparableSales(ILogger<ScanDocxForComparableSales> logger)
        {
            _logger = logger;
        }





        public async Task<Dictionary<string, Dictionary<string, object>>> ExtractSalesDataAndImages(WordprocessingDocument wordDocument)
        {
            var salesData = new Dictionary<string, Dictionary<string, object>>();

            StringBuilder currentSaleContent = new StringBuilder();
            List<ExtractedImage> currentSaleImages = new List<ExtractedImage>();
            string currentSaleKey = null;
            bool endOfLastSaleReached = false;
            bool hasStartedCollectingSales = false;

            int saleCounter = 0;
            var paragraphs = wordDocument.MainDocumentPart.Document.Body.Elements<Paragraph>();
            var saleStartPattern = new Regex(@"\b(\S+) Sale No\. (\d+)", RegexOptions.IgnoreCase);
            string lastSaleNumber = null;

            foreach (var paragraph in paragraphs)
            {
                string content = paragraph.InnerText;
                var match = saleStartPattern.Match(content);

                if (match.Success)
                {
                    hasStartedCollectingSales = true;
                    string saleType = match.Groups[1].Value;
                    string saleNumber = match.Groups[2].Value;

                    // Check if we're still on the same sale
                    if (lastSaleNumber != saleNumber)
                    {
                        // Save the previous sale's data and images
                        if (currentSaleContent.Length > 0)
                        {
                            var extractedDataString = ExtractDataFromContent(currentSaleContent.ToString());
                            var extractedDataObject = new Dictionary<string, object>();
                            foreach (var kvp in extractedDataString)
                            {
                                extractedDataObject[kvp.Key] = kvp.Value;
                            }
                            if (currentSaleKey != null)
                            {
                                extractedDataObject["Images"] = currentSaleImages;
                                salesData[currentSaleKey] = extractedDataObject;
                            }
                        }

                        // Clear previous sale's data
                        currentSaleContent.Clear();
                        currentSaleImages = new List<ExtractedImage>();  // Creating a new list instead of clearing

                        // Prepare for the new sale
                        saleCounter++;
                        currentSaleKey = $"{saleType}_Sale_{saleNumber}_{saleCounter}";

                        // Update lastSaleNumber for the next iteration
                        lastSaleNumber = saleNumber;
                        endOfLastSaleReached = false;
                    }
                }


                if (hasStartedCollectingSales && content.Contains("Analysis", StringComparison.OrdinalIgnoreCase))
                {
                    endOfLastSaleReached = true;
                }

                if (!endOfLastSaleReached)
                {
                    currentSaleContent.AppendLine(content);

                    var drawings = paragraph.Descendants<Drawing>().ToList();
                    foreach (var drawing in drawings)
                    {
                        // Get the relationship Id of the image
                        string imageId = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault()?.Embed.Value;

                        if (string.IsNullOrEmpty(imageId))
                            continue;

                        // Get the ImagePart using the relationship Id
                        var imagePart = (ImagePart)wordDocument.MainDocumentPart.GetPartById(imageId);

                        // Read the bytes from the ImagePart
                        byte[] bytes;
                        using (var stream = imagePart.GetStream())
                        {
                            bytes = new byte[stream.Length];
                            await stream.ReadAsync(bytes, 0, (int)stream.Length);
                        }

                        // Get the image format (e.g., "image/png")
                        string imageFormat = imagePart.ContentType;

                        // Add the extracted image to currentSaleImages
                        var extractedImage = new ExtractedImage
                        {
                            ImageData = bytes,
                            ImageFormat = imageFormat
                        };

                        // Try to find the caption
                        var nextSibling = paragraph.NextSibling();
                        if (nextSibling is Paragraph nextParagraph)
                        {
                            var caption = nextParagraph.InnerText;
                            if (!string.IsNullOrEmpty(caption))
                            {
                                extractedImage.Caption = caption;
                            }
                        }

                        currentSaleImages.Add(extractedImage);
                    }
                }
            }

            if (currentSaleContent.Length > 0)
            {
                var extractedDataString = ExtractDataFromContent(currentSaleContent.ToString());
                var extractedDataObject = new Dictionary<string, object>();

                foreach (var kvp in extractedDataString)
                {
                    extractedDataObject[kvp.Key] = kvp.Value;
                }

                if (extractedDataObject.Count > 0 && currentSaleKey != null)
                {
                    extractedDataObject["Images"] = currentSaleImages;  // This should work now
                    salesData[currentSaleKey] = extractedDataObject;  // This should work now
                }
                currentSaleContent.Clear();
                currentSaleImages = new List<ExtractedImage>();
            }
            var keysToRemove = salesData
                    .Where(pair => pair.Value == null || !pair.Value.Any())
                    .Select(pair => pair.Key)
                    .ToList();

            foreach (var key in keysToRemove)
            {
                salesData.Remove(key);
            }

            return salesData;
        }





        public Dictionary<string, string> ExtractDataFromContent(string saleContent)
        {
            var result = new Dictionary<string, string>();
            string currentKey = null;
            StringBuilder currentValue = new StringBuilder();

            // Split the content by lines
            var lines = saleContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                // Try to match a key-value pair
                var match = Regex.Match(line, @"([\w\s]+):\s*(.*)");
                if (match.Success)
                {
                    // Save the previous key-value pair if any
                    if (currentKey != null)
                    {
                        result[currentKey] = currentValue.ToString().Trim();
                        currentValue.Clear();
                    }

                    // Start a new key-value pair
                    currentKey = match.Groups[1].Value.Trim();
                    currentValue.Append(match.Groups[2].Value.Trim());
                }
                else
                {
                    // If it's not a new key-value pair, append the line to the current value
                    if (currentKey != null)
                    {
                        currentValue.Append(" " + line.Trim());
                    }
                }
            }

            // Don't forget to save the last key-value pair
            if (currentKey != null)
            {
                result[currentKey] = currentValue.ToString().Trim();
            }

            // Special handling for "Comments" key, if needed
            if (result.ContainsKey("Comments"))
            {
                var comments = result["Comments"];
                var index = comments.IndexOf("\r");
                if (index != -1)
                {
                    result["Comments"] = comments.Substring(0, index).Trim();
                }
            }
            return result;
        }

    }

}


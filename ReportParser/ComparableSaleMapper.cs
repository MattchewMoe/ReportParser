using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using F23.StringSimilarity;
using System.Globalization;
using ReportParser.Entities;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BlobMonitor
{
    public class ComparableSaleMapper
    {
        private readonly Levenshtein levenshtein;
        private readonly ILogger logger;
        private readonly Dictionary<string, List<string>> PropertySynonyms;
        public ComparableSaleMapper(ILogger logger)
        {
            this.logger = logger;
            this.levenshtein = new Levenshtein();
            this.PropertySynonyms = new Dictionary<string, List<string>>
            {
                 { "Address", new List<string> { "Location", "StreetAddress", "PropertyLocation" } },
            { "CityTownship", new List<string> { "Municipality", "City", "Town" } },
            { "County", new List<string> { "Borough", "Parish", "Shire" } },
            { "ZipCode", new List<string> { "PostalCode", "ZIP" } },
            { "State", new List<string> { "Province", "Region" } },
            { "ParcelNumbers", new List<string> { "ParcelID", "LandID", "PropertyID", "ParcelNumber" } },
            { "SchoolDistrict", new List<string> { "EducationZone", "SchoolZone" } },
            { "Grantor", new List<string> { "Seller", "Vendor" } },
            { "Grantee", new List<string> { "Buyer", "Purchaser" } },
            { "SalePriceString", new List<string> { "TransactionAmount", "Cost", "Price", "SalePrice" } },
            { "DateOfSaleString", new List<string> { "TransactionDate", "SaleDate", "DateOfSale" } },
            { "RecordingReference", new List<string> { "DocumentID", "RecordID" } },
            { "PropertyRightsTransferred", new List<string> { "OwnershipTransferred", "RightsConveyed", "PropertyRights" } },
            { "CircumstancesOfSale", new List<string> { "SaleConditions", "TransactionType" } },
            { "Financing", new List<string> { "PaymentMethod", "LoanType" } },
            { "PresentUse", new List<string> { "CurrentUse", "Usage" } },
            { "HighestAndBestUse", new List<string> { "OptimalUse", "BestUse" } },
            { "ConstructionType", new List<string> { "BuildingMaterial", "StructureType" } },
            { "Stories", new List<string> { "Floors", "Levels" } },
            { "SizeInSF", new List<string> { "SquareFootage", "Area" } },
            { "SiteSize", new List<string> { "LandSize", "LotSize" } },
            {"BuildingSize", new List<string>{ "Size"} },
            { "NumberOfUnits", new List<string> { "UnitCount", "Apartments" } },
            { "RentData", new List<string> { "LeaseInfo", "RentalDetails" } },
            { "Configuration", new List<string> { "Layout", "Shape", "SiteConfiguration" } },
            { "Topography", new List<string> { "LandSlope", "Terrain" } },
            { "Utilities", new List<string> { "Services", "Amenities" } },
            { "Zoning", new List<string> { "LandUse", "Zone" } },
            { "FloodPlain", new List<string> { "FloodZone", "FloodArea" } },
            { "UnitIndicator", new List<string> { "PricePerUnit", "UnitPrice" } },
            { "VerificationName", new List<string> { "Verifier", "CheckedBy" } },
            { "VerifiersRelationshipToSale", new List<string> { "VerifierRole", "VerifierStatus" } },
            { "DateVerified", new List<string> { "VerificationDate", "CheckedOn" } },
            { "PersonWhoVerifiedSale", new List<string> { "VerifiedBy", "Checker" } },
            { "TelephoneNumber", new List<string> { "Phone", "ContactNumber" } },
            { "Comments", new List<string> { "Notes", "Remarks" } },
            { "Indication", new List<string> { "UnitIndication", "UnitIndicator", "Indicator", "UnitPrice" } },
            { "Parking", new List<string> { "ParkingRatio", "ParkingSpace" } },
            { "YearConstructedString", new List<string> { "YearBuilt", "DateofConstruction", "ConstructionDate", "BuildDate", "ConstructionYear","YearConstructed" } },
            { "Traffic", new List<string> { "TrafficCount" } },
            {"Type", new List<string> {"PropertyData"} }
            };
        }






        public Dictionary<string, List<string>> GetPropertySynonyms()
        {
            // Just return the instance field
            return PropertySynonyms;
        }


        public T MapDictionaryToObject<T>(Dictionary<string, string> dict) where T : new()
        {
            T obj = new T();
            Type type = typeof(T);
            int successfulMappings = 0;

            foreach (var key in dict.Keys)
            {
                PropertyInfo closestProperty = FindClosestProperty(type, key.Replace(" ", ""));
                if (closestProperty != null && closestProperty.CanWrite && closestProperty.PropertyType == typeof(string))
                {
                    try
                    {
                        closestProperty.SetValue(obj, dict[key]);
                        successfulMappings++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Error setting property {closestProperty.Name}: {ex.Message}");
                    }
                }
            }

            return successfulMappings >= 4 ? obj : default;
        }




        // FindClosestProperty.cs
        private PropertyInfo FindClosestProperty(Type type, string key)
        {
            key = key.ToLower(); // Convert key to lowercase
            PropertyInfo closestProperty = null;
            double closestDistance = double.MaxValue;
            double threshold = 0.2;  // Set your own threshold

            Levenshtein levenshtein = new Levenshtein(); // Assuming you have this class already defined

            foreach (var property in type.GetProperties())
            {
                double distance = levenshtein.Distance(key, property.Name.ToLower()); // Convert property name to lowercase

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestProperty = property;
                }

                if (closestDistance == 0)  // Early exit if perfect match
                {
                    return closestProperty;
                }

                // Check synonyms
                List<string> synonyms;
                if (GetPropertySynonyms().TryGetValue(property.Name, out synonyms)) // Using Lazy-loaded dictionary
                {
                    foreach (var synonym in synonyms)
                    {
                        distance = levenshtein.Distance(key, synonym.ToLower()); // Convert synonym to lowercase
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestProperty = property;
                        }

                        if (closestDistance == 0)  // Early exit if perfect match
                        {
                            return closestProperty;
                        }
                    }
                }
            }

            return closestDistance > threshold ? null : closestProperty;
        }


        public ComparableSale GenerateComparableSale(ComparableSale rawComparableSale, ILogger logger)
        {
            try
            {
                logger.LogInformation("Starting to generate ComparableSale object.");

                // Validate rawComparableSale object
                if (rawComparableSale == null)
                {
                    logger.LogError("rawComparableSale object is null. Exiting method.");
                    return null;
                }

                // Convert specific properties
                rawComparableSale.SalePrice = ParseSalePrice(rawComparableSale.SalePriceString);
                rawComparableSale.DateOfSale = ParseDateOfSale(rawComparableSale.DateOfSaleString);
                var yearConstructedResult = ParseYearConstructed(rawComparableSale.YearConstructedString);
                rawComparableSale.YearConstructed = yearConstructedResult.YearConstructed;
                rawComparableSale.RenovationYears = yearConstructedResult.RenovationYears;
                var indicationResult = ParseIndicationValueAndUnit(rawComparableSale.Indication);
                rawComparableSale.IndicationValue = indicationResult.Value;
                rawComparableSale.IndicationUnitType = indicationResult.UnitType;
                var siteSizeResult = ParseSiteSize(rawComparableSale.SiteSize);
                rawComparableSale.SiteSizeValue = siteSizeResult.Size;
                rawComparableSale.SiteSizeUnit = siteSizeResult.Unit;
                var buildingSizeResult = ParseBuildingSize(rawComparableSale.BuildingSize);
                rawComparableSale.BuildingSizeValue = buildingSizeResult.Size;
                rawComparableSale.BuildingSizeUnit = buildingSizeResult.Unit;

                logger.LogInformation("Successfully generated ComparableSale object.");
                return rawComparableSale;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while generating ComparableSale object.");
                throw;
            }
        }





        private decimal? ParseSalePrice(string salePriceStr)
        {
            if (string.IsNullOrEmpty(salePriceStr))
            {
                return null;
            }

            // Keep only numbers and the decimal point
            var cleanedSalePrice = new string(salePriceStr.Where(c => char.IsDigit(c) || c == '.').ToArray()).Replace("$", "");
            if (decimal.TryParse(cleanedSalePrice, out decimal parsedSalePrice))
            {
                return parsedSalePrice;
            }

            return null;
        }


        private DateTime? ParseDateOfSale(string dateOfSaleStr)
        {
            if (string.IsNullOrEmpty(dateOfSaleStr))
            {
                return null;
            }

            // Try parsing the string with various formats
            string[] formats = { "MMMM d, yyyy", "MMMM dd, yyyy", "MMMM d,yyyy", "MMMM dd,yyyy" };
            if (DateTime.TryParseExact(dateOfSaleStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                return parsedDate;
            }

            return null;
        }


        private (int? YearConstructed, List<int> RenovationYears) ParseYearConstructed(string yearConstructedStr)
        {
            if (string.IsNullOrEmpty(yearConstructedStr))
            {
                return (null, new List<int>());
            }

            var match = Regex.Match(yearConstructedStr, @"(\d{4})\s*(?:\((?:[a-zA-Z\s/]*)?\s*(\d{4}(?:/\d{4})*)\))?");

            int? yearConstructed = null;
            List<int> renovationYears = new List<int>();

            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int parsedYearConstructed))
                {
                    yearConstructed = parsedYearConstructed;
                }

                var renovationYearsStr = match.Groups[2].Value.Split('/');
                foreach (var yearStr in renovationYearsStr)
                {
                    if (int.TryParse(yearStr, out int parsedYear))
                    {
                        renovationYears.Add(parsedYear);
                    }
                }
            }

            return (yearConstructed, renovationYears);
        }

        // In ComparableSaleMapper.cs
        private (decimal? Value, string UnitType) ParseIndicationValueAndUnit(string indication)
        {
            if (string.IsNullOrEmpty(indication))
            {
                return (null, null);
            }

            var cleanedIndication = indication.Replace("$", "").Replace(",", "").Replace(" ", "");
            var match = Regex.Match(cleanedIndication, @"([\d.]+)/([a-zA-Z.]+)");

            if (match.Success && match.Groups.Count == 3)
            {
                decimal parsedValue;
                string unitType = match.Groups[2].Value;

                if (decimal.TryParse(match.Groups[1].Value, out parsedValue))
                {
                    return (parsedValue, unitType);
                }
            }

            return (null, null);
        }


        private (double? Size, string? Unit) ParseSiteSize(string siteSizeStr)
        {
            {
                if (string.IsNullOrEmpty(siteSizeStr))
                {
                    return (null, null);
                }

                // Regular expression to capture numbers and the word 'acres'
                Regex regex = new Regex(@"([0-9.]+)(?:\+/-|±)?\s*([a-zA-Z]+)");
                var match = regex.Match(siteSizeStr);
                if (match.Success)
                {
                    double? size = double.TryParse(match.Groups[1].Value, out double parsedSize) ? parsedSize : (double?)null;
                    string unit = match.Groups[2].Value;
                    return (size, unit);
                }
                return (null, null);
            }
        }
        private (double? Size, string? Unit) ParseBuildingSize(string buildingSizeStr)
        {
            if (string.IsNullOrEmpty(buildingSizeStr))
            {
                return (null, null);
            }

            Regex regex = new Regex(@"([0-9,]+)(?:\+/-|±)?\s*([a-zA-Z.]+)");
            var match = regex.Match(buildingSizeStr);
            if (match.Success)
            {
                double? size = double.TryParse(match.Groups[1].Value.Replace(",", ""), out double parsedSize) ? parsedSize : (double?)null;
                string unit = match.Groups[2].Value;
                return (size, unit);
            }
            return (null, null);
        }

    }


    public class ComparableSaleMapperFactory
    {
        public ComparableSaleMapper Create(ILogger logger)
        {
            return new ComparableSaleMapper(logger);
        }
    }
}

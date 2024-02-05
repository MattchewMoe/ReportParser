

using System;
using System.Collections.Generic;

namespace BlobMonitor
{
    public static class SynonymMapper
    {
        public static Dictionary<string, List<string>> PropertySynonyms;

        static SynonymMapper()
        {
            try
            {
                PropertySynonyms = new Dictionary<string, List<string>>
    {
        { "Address", new List<string> { "Location", "StreetAddress", "PropertyLocation" } },
        { "CityTownship", new List<string> { "Municipality", "City", "Town" } },
        { "County", new List<string> { "Borough", "Parish", "Shire" } },
        { "ZipCode", new List<string> { "PostalCode", "ZIP" } },
        { "State", new List<string> { "Province", "Region" } },
        { "ParcelNumbers", new List<string> { "ParcelID", "LandID", "PropertyID","ParcelNumber" } },
        { "SchoolDistrict", new List<string> { "EducationZone", "SchoolZone" } },
        { "Grantor", new List<string> { "Seller", "Vendor" } },
        { "Grantee", new List<string> { "Buyer", "Purchaser" } },
        { "SalePrice", new List<string> { "TransactionAmount", "Cost", "Price" } },
        { "DateOfSale", new List<string> { "TransactionDate", "SaleDate" } },
        { "RecordingReference", new List<string> { "DocumentID", "RecordID" } },
        { "PropertyRightsTransferred", new List<string> { "OwnershipTransferred", "RightsConveyed" } },
        { "CircumstancesOfSale", new List<string> { "SaleConditions", "TransactionType" } },
        { "Financing", new List<string> { "PaymentMethod", "LoanType" } },
        { "PresentUse", new List<string> { "CurrentUse", "Usage" } },
        { "HighestAndBestUse", new List<string> { "OptimalUse", "BestUse" } },
        { "ConstructionType", new List<string> { "BuildingMaterial", "StructureType" } },
        { "Stories", new List<string> { "Floors", "Levels" } },
        { "YearConstructed", new List<string> { "BuildDate", "ConstructionYear" } },
        { "SizeInSF", new List<string> { "SquareFootage", "Area" } },
        { "SiteSize", new List<string> { "LandSize", "LotSize" } },
        { "NumberOfUnits", new List<string> { "UnitCount", "Apartments" } },
        { "RentData", new List<string> { "LeaseInfo", "RentalDetails" } },
        { "SiteSizeInAcreage", new List<string> { "LandSize", "Acreage" } },
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
        {"PropertyRights", new List<string>{"PropertyRightsTransfered", "OwnershipTransferred", "RightsConveyed" } },
            {"Indication", new List<string>{"UnitIndication" } },
            {"Parking", new List<string>{"ParkingRatio", "ParkingSpace"} },
            {"YearConstructed", new List<string>{"YearBuilt", "DateofConstruction", "ConstructionDate","BuildDate" } },
            {"Traffic", new List<string>{"TrafficCount"} }




    };
            }
            catch (Exception ex)
            {
                // Log the exception or rethrow a more descriptive one
                throw new Exception("Failed to initialize PropertySynonyms: " + ex.Message, ex);
            }

        }
    }
}
/*
 * 
 * 
Sure, here are the unique entries from the list you provided:















Images

Topography



Construction Type
Stories
Year Constructed
Size
Condition
Land to Building Ratio

Improvement Size
*/
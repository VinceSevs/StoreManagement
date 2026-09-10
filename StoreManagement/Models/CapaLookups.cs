using System;
using System.Collections.Generic;

namespace IsseERP.Models
{
    // Placeholder lookup data for the CAPA form's dropdowns (Site Warehouse,
    // Issued To Department, Item catalog, CAR Classification Trucker/Vendor).
    // Swap these for real StoreManagementDB-backed lookups later if needed.

    public class Warehouse
    {
        public int WarehouseID { get; set; }
        public string WarehouseCode { get; set; }
        public string WarehouseName { get; set; }
        public string ExternalCode { get; set; }
    }

    public class Department
    {
        public int DepartmentID { get; set; }
        public string DepartmentDescription { get; set; }
    }

    public class Item
    {
        public int ItemID { get; set; }
        public string ItemDescription { get; set; }
        public string ItemCode { get; set; }
    }

    public class UOM
    {
        public int UOMID { get; set; }
        public string Text { get; set; }
        public string IsoCode { get; set; }
    }

    public class UserAccountLookup
    {
        public int Id { get; set; }
        public string Username { get; set; }
    }

    public class Trucker
    {
        public int TruckID { get; set; }
        public string TruckerName { get; set; }
        public string EmailAddress { get; set; }
    }

    public class Vendor
    {
        public int VendorID { get; set; }
        public string VendorName { get; set; }
        public string EmailAddress { get; set; }
    }

    // Used only by the non-CAPA warehouse-putaway actions still on PublicController.
    public class InterbinTransfer
    {
        public string WarehouseCode { get; set; }
        public DateTime TransferDate { get; set; }
        public int ProductType_ID { get; set; }
        public string PalletTag { get; set; }
    }

    public class DefectCatalogItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
    }

    // Single source of truth for the defect catalog, shared between the
    // DefectCatalog() JSON endpoint (CapaController) and CapaService's
    // DefectCategoryID -> name/category lookup when reading CAPA reports
    // back — there's no real tbl_DefectCategory, so this list's own Id
    // values are what DefectCategoryID actually stores.
    public static class DefectCatalogData
    {
        public static readonly List<DefectCatalogItem> Items = new List<DefectCatalogItem>
        {
            new DefectCatalogItem { Id = 1, Name = "Damaged / torn packaging", Category = "Packaging" },
            new DefectCatalogItem { Id = 2, Name = "Crushed or dented container", Category = "Packaging" },
            new DefectCatalogItem { Id = 3, Name = "Broken seal / tamper-evident seal missing", Category = "Packaging" },
            new DefectCatalogItem { Id = 4, Name = "Leaking or spillage", Category = "Packaging" },
            new DefectCatalogItem { Id = 5, Name = "Water-damaged packaging", Category = "Packaging" },
            new DefectCatalogItem { Id = 6, Name = "Expired product", Category = "Shelf Life" },
            new DefectCatalogItem { Id = 7, Name = "Near-expiry / short shelf life on receipt", Category = "Shelf Life" },
            new DefectCatalogItem { Id = 8, Name = "Temperature abuse / cold chain break", Category = "Temperature" },
            new DefectCatalogItem { Id = 9, Name = "Frozen product partially thawed", Category = "Temperature" },
            new DefectCatalogItem { Id = 10, Name = "Foreign object contamination", Category = "Contamination" },
            new DefectCatalogItem { Id = 11, Name = "Mold / mildew present", Category = "Contamination" },
            new DefectCatalogItem { Id = 12, Name = "Pest infestation", Category = "Contamination" },
            new DefectCatalogItem { Id = 13, Name = "Off odor / smell", Category = "Contamination" },
            new DefectCatalogItem { Id = 14, Name = "Discoloration", Category = "Quality" },
            new DefectCatalogItem { Id = 15, Name = "Texture defect (soft, mushy, hardened)", Category = "Quality" },
            new DefectCatalogItem { Id = 16, Name = "Underweight / incorrect fill weight", Category = "Quality" },
            new DefectCatalogItem { Id = 17, Name = "Wrong label / mislabeled item", Category = "Labeling" },
            new DefectCatalogItem { Id = 18, Name = "Missing label", Category = "Labeling" },
            new DefectCatalogItem { Id = 19, Name = "Illegible label / print smudge", Category = "Labeling" },
            new DefectCatalogItem { Id = 20, Name = "Incorrect SKU / mixed items in case", Category = "Labeling" },
            new DefectCatalogItem { Id = 21, Name = "Physical damage during transport", Category = "Handling" },
            new DefectCatalogItem { Id = 22, Name = "Received in wrong quantity", Category = "Handling" }
        };
    }
}

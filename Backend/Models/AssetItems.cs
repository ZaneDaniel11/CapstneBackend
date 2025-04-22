
namespace AssetItems.Models
{

public class DisposedAsset
{
    public int AssetID { get; set; }
    public int CategoryID { get; set; }
    public string AssetName { get; set; }
    public string AssetCode { get; set; }
    public DateTime DisposalDate { get; set; }
    public string DisposalReason { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal DisposedValue { get; set; }
    public decimal LossValue { get; set; }
}

  public class AssetTransferRequest
    {
      
        public int AssetID { get; set; }
        public string NewOwner { get; set; }
        public string NewLocation { get; set; }
        public int CategoryID { get; set; }
        public string Remarks { get; set; }
         public string PerformedBy { get; set; }  
    }
    public class AssetItem
    {
    
  public int AssetCategoryID { get; set; }
        public int AssetId { get; set; }  // Matches the AssetID primary key
        public int CategoryID { get; set; }
        public string AssetName { get; set; }

        public string AssetPicture { get; set; }  // Path to the asset's picture file
        public DateTime? DatePurchased { get; set; }
        public DateTime? DateIssued { get; set; }
        public string IssuedTo { get; set; }
        public string AssetVendor { get; set; }  // Vendor details
        public string CheckedBy { get; set; }  // Person who checked the asset
        public decimal AssetCost { get; set; }  // Asset cost
        public string AssetCode { get; set; }  // Unique code for the asset
        public string Remarks { get; set; }  // Additional remarks about the asset
        public string AssetLocation { get; set; }  // Location of the asset
        public DateTime? WarrantyStartDate { get; set; }  // Warranty start date
        public DateTime? WarrantyExpirationDate { get; set; }  // Warranty expiration date
        public string WarrantyVendor { get; set; }  // Vendor responsible for the warranty
        public string WarrantyContact { get; set; }  // Contact details for warranty vendor
        public string AssetStatus { get; set; }  // Current status of the asset
        public string AssetStype { get; set; } // Type/category of the asset
        public DateTime? PreventiveMaintenanceSchedule { get; set; }  // Next maintenance schedule
        public string Notes { get; set; }  // Additional notes for the asset
        public DateTime? OperationStartDate { get; set; }  // Start date of asset operation
        public DateTime? OperationEndDate { get; set; }  // End date of asset operation
        public DateTime? DisposalDate { get; set; }  // Date when the asset was disposed
        public decimal? DepreciationRate { get; set; }  // Depreciation rate (percentage)
        public decimal? DepreciationValue { get; set; }  // Depreciation value
        public string DepreciationPeriodType { get; set; }  // "month" or "year"
        public int DepreciationPeriodValue { get; set; }
        // Ensure proper spelling
        public string AssetPreventiveMaintenace { get; set; } // Number of months or years

    }
   

}

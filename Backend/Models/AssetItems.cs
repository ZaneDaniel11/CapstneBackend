using System;

namespace AssetItems.Models
{
 public class AssetItem
{
    public int AssetId { get; set; }  // Ensure this matches your table
    public int CategoryID { get; set; }
    public string AssetName { get; set; }
    public DateTime DatePurchased { get; set; }
    public DateTime? DateIssued { get; set; }
    public string IssuedTo { get; set; }
    public string CheckedBy { get; set; }
    public decimal Cost { get; set; }
    public string Location { get; set; }
    public string AssetCode { get; set; }
    public string Remarks { get; set; }
    public decimal? DepreciationRate { get; set; }  // Nullable to handle missing values
    public decimal? DepreciationValue { get; set; }  // Nullable to handle missing values
    public string DepreciationPeriodType { get; set; }  // "month" or "year"
    public int DepreciationPeriodValue { get; set; }  // Number of months or years
}

}

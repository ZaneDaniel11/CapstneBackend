
namespace AssetHistory.Models
{
   public class AssetHistory
{
    public int HistoryID { get; set; } // Primary Key
    public int AssetID { get; set; }   // Foreign Key (Asset Reference)
    public string ActionType { get; set; } // e.g., "Transfer", "Maintenance", "Disposal"
    public DateTime ActionDate { get; set; } // Timestamp of the action
    public string PerformedBy { get; set; }  // Who performed the action
    public string Remarks { get; set; }  // Optional notes about the action
}

}
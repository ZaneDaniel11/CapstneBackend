using System;

namespace Maintenace.Models
{
    public class Maintenance
    {
        public int MaintenanceID { get; set; }          // Unique identifier for each maintenance record
        public int ItemID { get; set; }                 // Foreign key referencing the item
        public int CategoryID { get; set; }             // Foreign key referencing the category
        public string AssetName { get; set; }           // Name of the asset
        public string AssetCode { get; set; }           // Code identifying the asset
        public string Location { get; set; }            // Location of the asset
        public string Issue { get; set; }               // Description of the issue
        public DateTime RequestDate { get; set; }       // Date and time the maintenance request was made
        public string RequestedBy { get; set; }         // Name of the person who requested the maintenance
        public int RequesterID { get; set; }            // ID of the requester
        public string MaintenanceStatus { get; set; }   // Status of the maintenance request
        public string AssignedTo { get; set; }          // The staff assigned to handle the maintenance
        public DateTime? ScheduledDate { get; set; }    // Scheduled date and time for maintenance (nullable)
        public DateTime? CompletionDate { get; set; }   // Date and time the maintenance was completed (nullable)
        public string Description { get; set; }         // Additional details about the maintenance
        public string ApprovalStatus { get; set; }      // Approval status of the request
        public string ApprovedByAdmin1 { get; set; }    // Admin1 who approved the request
        public string ApprovedByAdmin2 { get; set; }
        public string RejectBy { get; set; }
        public string RejectReason { get; set; }
    }
    public class MaintenanceAdmin1Aproval
    {
        public string Admin1Approval { get; set; } // Either "Approved" or "Declined"
        public string RejectReason { get; set; }  // Optional, only for "Declined"
        public string RejectBy { get; set; }      // Optional, only for "Declined"
    }
       public class MaintenanceAdmin2Approval
    {
        public string Admin2Approval { get; set; } // Either "Approved" or "Declined"
        public string RejectReason { get; set; }  // Optional, only for "Declined"
        public string RejectBy { get; set; }      // Optional, only for "Declined"
    }
}
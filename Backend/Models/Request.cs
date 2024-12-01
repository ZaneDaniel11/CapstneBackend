namespace Request.Models
{
    public class RequestItem
    {
        public int RequestID { get; set; }
        public string RequestedItem { get; set; }
        public string RequestedBy { get; set; }
        public string SuggestedDealer { get; set; }
        public string Purpose { get; set; }
        public decimal EstimatedCost { get; set; }
        public DateTime RequestedDate { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public string Admin1Approval { get; set; }
        public string Admin2Approval { get; set; }
        public string Admin3Approval { get; set; }
        public int BorrowerId { get; set; }

        public string Email { get; set; }
        // public string PDFFilePath { get; set; }
    }
}

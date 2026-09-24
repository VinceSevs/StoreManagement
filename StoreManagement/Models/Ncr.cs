using System;

namespace StoreManagement.Models
{
    public class Ncr
    {
        public int DocID { get; set; }
        public string NcrNumber { get; set; }
        public string StoreNumber { get; set; }
        public string StoreName { get; set; }
        public string StoreContact { get; set; }
        public string StoreEmail { get; set; }
        public string ManagerInCharge { get; set; }
        public string FiledBy { get; set; }

        public string ComplaintIssueDesc { get; set; }
        public string Subject { get; set; }
        public string Remarks { get; set; }

        public DateTime DateCreated { get; set; }
        public int NcrStatus { get; set; }

        public int CSRemarks { get; set; }
        public string CSFindings { get; set; }
        public DateTime? CSResDate { get; set; }

        public string Attachment1 { get; set; }
        public string Attachment2 { get; set; }
        public string Attachment3 { get; set; }
        public string Attachment4 { get; set; }
    }
}

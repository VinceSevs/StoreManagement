using System;

namespace StoreManagement.Models
{
    public class Pcr
    {
        public int DocID { get; set; }
        public string PcrNumber { get; set; }
        public string StoreNumber { get; set; }
        public string StoreName { get; set; }
        public string StoreContact { get; set; }
        public string StoreEmail { get; set; }
        public string ManagerInCharge { get; set; }
        public string FiledBy { get; set; }

        public string ItemID { get; set; }
        public string ItemDesc { get; set; }
        public string BatchCode { get; set; }
        public string VendorName { get; set; }
        public string UOM { get; set; }
        public int Qty { get; set; }
        public string Particulars { get; set; }
        public string ComplaintIssueDesc { get; set; }
        public string Remarks { get; set; }

        public DateTime DateCreated { get; set; }
        public DateTime IncidentDate { get; set; }
        public DateTime UTD { get; set; }
        public DateTime? DeliveryDate { get; set; }

        public int PcrStatus { get; set; }
        public int ComFlag { get; set; }
        public string CMno { get; set; }
        public string ChargedTo { get; set; }

        public int SupplierRemarks { get; set; }
        public string SupplierRemarksDesc { get; set; }
        public string SupplierFindings { get; set; }
        public DateTime? SupplierResDate { get; set; }

        public int QARemarks { get; set; }
        public string QARemarksDesc { get; set; }
        public string QAFindings { get; set; }
        public DateTime? QAResDate { get; set; }

        public int GADCRemarks { get; set; }
        public string GADCFindings { get; set; }
        public DateTime? GADCResDate { get; set; }

        public string Attachment1 { get; set; }
        public string Attachment2 { get; set; }
        public string Attachment3 { get; set; }
        public string Attachment4 { get; set; }
    }
}

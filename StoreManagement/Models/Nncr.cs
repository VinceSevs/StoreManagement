using System;
using System.Collections.Generic;

namespace StoreManagement.Models
{
    // NNCR workflow: QA files and it goes straight to the supplier (AwaitingResponse)
    // -> supplier approves/rejects via the emailed link (Approved or Rejected)
    // -> a role 49 user closes it (Closed).
    // "Overdue" is not stored; it is an AwaitingResponse NNCR past its DueDate.
    public static class NncrStatuses
    {
        public const string AwaitingResponse = "Awaiting Response";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";

        public static string FromDecision(string decision)
        {
            return decision == NncrDecisions.Approve ? Approved
                : decision == NncrDecisions.Reject ? Rejected
                : null;
        }
        public const string Closed = "Closed";
    }

    public static class NncrDecisions
    {
        public const string Approve = "Approve";
        public const string Reject = "Reject";
    }

    // Due date = filing date + 7 working days (Saturdays and Sundays are skipped).
    public static class NncrDueDates
    {
        public const int WorkingDaysToRespond = 7;
        public const int DueSoonWorkingDays = 3;

        public static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        public static DateTime AddWorkingDays(DateTime start, int workingDays)
        {
            var date = start.Date;
            while (workingDays > 0)
            {
                date = date.AddDays(1);
                if (!IsWeekend(date)) workingDays--;
            }
            return date;
        }

        // Working days after today up to and including the due date (0 = due today, negative = overdue).
        public static int WorkingDaysLeft(DateTime today, DateTime dueDate)
        {
            today = today.Date;
            dueDate = dueDate.Date;
            if (dueDate < today) return -1;

            int days = 0;
            for (var d = today.AddDays(1); d <= dueDate; d = d.AddDays(1))
            {
                if (!IsWeekend(d)) days++;
            }
            return days;
        }
    }

    // One NNCR row of tbl_QualityIncidentHdr plus display names resolved via JOIN.
    public class NncrReport
    {
        public int EventID { get; set; }
        public string ReportNumber { get; set; }
        public string ResponseToken { get; set; }
        public string Status { get; set; }

        public int IssuedBy { get; set; } // FK -> tbl_UserAccount.id
        public string IssuedByName { get; set; } // display only

        public int IssuedToSupplier { get; set; } // FK -> tbl_Vendor.VendorID
        public string SupplierName { get; set; } // display only
        public string SupplierEmail { get; set; } // tbl_Vendor.EmailAddress, display only

        public int SiteWarehouse { get; set; } // FK -> tbl_Warehouse.WarehouseID
        public string SiteWarehouseName { get; set; } // display only

        // Stored as "; "-separated labels, e.g. "Quality Issues; Others: torn label".
        public string NonconformanceType { get; set; }
        // Stored as "; "-separated labels with details, e.g. "Receiving: Dock 3; Storage: Chiller 2".
        public string NonconformanceInterception { get; set; }
        public string DetailsOfNonconformance { get; set; }
        public string QARemarks { get; set; }
        public DateTime? DateOfIncident { get; set; }

        public DateTime? DateFiled { get; set; }
        public DateTime? DueDate { get; set; }

        // Supplier response
        public string SupplierDecision { get; set; } // NncrDecisions.Approve / Reject, "" until responded
        public string VendorRemarks { get; set; }
        public string RespondedEmail { get; set; } // supplier email the response was submitted as
        public int AcknowledgeBy { get; set; } // tbl_Vendor.VendorID of the responding supplier, 0 until responded
        public DateTime? AcknowledgeDate { get; set; }

        // Closing by the QA supervisor (role 49); stored in the ApprovedBy / ApprovedDate columns.
        public int ClosedBy { get; set; } // ApprovedBy, FK -> tbl_UserAccount.id, 0 until closed
        public string ClosedByName { get; set; } // display only
        public DateTime? ClosedDate { get; set; } // ApprovedDate

        public List<NncrAttachment> Attachments { get; set; } = new List<NncrAttachment>();

        // Activity log (dbo.tbl_NncrLog), newest first; filled for the status page.
        public List<NncrLogEntry> Logs { get; set; } = new List<NncrLogEntry>();

        public bool IsAwaitingResponse => Status == NncrStatuses.AwaitingResponse;

        // The supplier has answered (Approved / Rejected), whether or not QA has closed it yet.
        public bool HasSupplierResponse => Status == NncrStatuses.Approved || Status == NncrStatuses.Rejected || Status == NncrStatuses.Closed;

        // Answered by the supplier and waiting for QA to close it.
        public bool IsReadyToClose => Status == NncrStatuses.Approved || Status == NncrStatuses.Rejected;

        public bool IsOverdue => IsAwaitingResponse && DueDate.HasValue && DateTime.Today > DueDate.Value.Date;

        public int? WorkingDaysLeft => IsAwaitingResponse && DueDate.HasValue && !IsOverdue
            ? NncrDueDates.WorkingDaysLeft(DateTime.Today, DueDate.Value)
            : (int?)null;

        public bool IsDueSoon => WorkingDaysLeft.HasValue && WorkingDaysLeft.Value <= NncrDueDates.DueSoonWorkingDays;

        // "awaiting" | "overdue" | "approved" | "rejected" | "closed" | "other" — CSS/filter key.
        public string StatusKey
        {
            get
            {
                if (IsOverdue) return "overdue";
                switch (Status)
                {
                    case NncrStatuses.AwaitingResponse: return "awaiting";
                    case NncrStatuses.Approved: return "approved";
                    case NncrStatuses.Rejected: return "rejected";
                    case NncrStatuses.Closed: return "closed";
                    default: return "other";
                }
            }
        }

        public string StatusLabel
        {
            get
            {
                if (IsOverdue) return "Overdue";
                switch (Status)
                {
                    case NncrStatuses.AwaitingResponse: return "Awaiting Response";
                    case NncrStatuses.Approved: return "Approved";
                    case NncrStatuses.Rejected: return "Rejected";
                    case NncrStatuses.Closed: return "Closed";
                    default: return Status;
                }
            }
        }

        // "Due today", "2 working days left", "Overdue by 3 days".
        public string DueText
        {
            get
            {
                if (!IsAwaitingResponse || !DueDate.HasValue) return "";
                if (IsOverdue)
                {
                    int late = (DateTime.Today - DueDate.Value.Date).Days;
                    return "Overdue by " + late + (late == 1 ? " day" : " days");
                }
                int left = WorkingDaysLeft ?? 0;
                if (left == 0) return "Due today";
                return left + (left == 1 ? " working day left" : " working days left");
            }
        }
    }

    // A file QA attached when filing, stored in App_Data/NncrAttachments/{ReportNumber}/.
    public class NncrAttachment
    {
        public string FileName { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public bool IsImage => ContentType != null && ContentType.StartsWith("image/") && ContentType != "image/svg+xml";
    }

    public class NncrFileRequest
    {
        public int IssuedToSupplier { get; set; }
        public int SiteWarehouse { get; set; }
        public string NonconformanceType { get; set; }
        public string NonconformanceInterception { get; set; }
        public string DetailsOfNonconformance { get; set; }
        public string QARemarks { get; set; }
        public DateTime? DateOfIncident { get; set; }
    }

    public class NncrSupplierResponseRequest
    {
        public string ResponseToken { get; set; }
        public string Decision { get; set; }
        public string VendorRemarks { get; set; }
    }

    public class NncrCloseRequest
    {
        public string ResponseToken { get; set; }
    }
}

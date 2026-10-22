using System;

namespace StoreManagement.Models
{
    // One row of dbo.tbl_NncrLog (see Database/tbl_NncrLog.sql).
    public class NncrLogEntry
    {
        public int LogID { get; set; }
        public string ReportNumber { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public int UserID { get; set; }
        public string UserName { get; set; }
        public DateTime LogDate { get; set; }
    }

    public static class NncrLogActions
    {
        public const string Filed = "Filed";
        public const string Emailed = "Emailed";
        public const string EmailFailed = "Email Failed";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Closed = "Closed";
    }
}

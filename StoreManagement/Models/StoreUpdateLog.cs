using System;

namespace StoreManagement.Models
{
    public class StoreUpdateLog
    {
        public int LogID { get; set; }
        public int StoreID { get; set; }
        public int LogTypeID { get; set; }
        public int OldCityID { get; set; }
        public int NewCityID { get; set; }
        public string OldCoordinates { get; set; }
        public string NewCoordinates { get; set; }
        public int UserID { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}

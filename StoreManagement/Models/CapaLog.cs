using System;

namespace IsseERP.Models
{
    // Maps to tbl_QualityIncidentLog — activity log for the CAR/CAPA module.
    public class CapaLog
    {
        public int LogID { get; set; }
        public string LogDescription { get; set; }
        public DateTime LogDate { get; set; }
        public string LogType { get; set; }
        public int UserID { get; set; }
    }
}

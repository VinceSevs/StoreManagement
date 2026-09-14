using System;

namespace IsseERP.Models
{
    public class CapaLog
    {
        public int LogID { get; set; }
        public string LogDescription { get; set; }
        public DateTime LogDate { get; set; }
        public string LogType { get; set; }
        public int UserID { get; set; }
    }
}

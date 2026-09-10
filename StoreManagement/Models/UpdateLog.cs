using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace StoreManagement.Models
{
    // Maps to tbl_StoreLog — the general activity log for the Store module
    // (LogDescription/LogType are plain strings for now, not lookup tables).
    public class UpdateLog
    {
        public int LogID { get; set; }
        public int StoreID { get; set; }
        public string LogDescription { get; set; }
        public DateTime LogDate { get; set; }
        public string LogType { get; set; }
        public int UserID { get; set; }
    }
}
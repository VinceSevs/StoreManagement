using System.Collections.Generic;

namespace StoreManagement.Models
{
    public class DashboardStats
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int InVerification { get; set; }
        public int Closed { get; set; }
        public int Cancelled { get; set; }
    }

    public class DashboardViewModel
    {
        public DashboardStats CarStats { get; set; }
        public DashboardStats ComplaintStats { get; set; }
        public DashboardStats PcrStats { get; set; }
        public DashboardStats NcrStats { get; set; }
        public int PcrTotal { get; set; }
        public int NcrTotal { get; set; }

        public List<string> TrendYearMonths { get; set; } = new List<string>();
        public List<string> TrendLabels { get; set; } = new List<string>();
        public List<int> CarTrend { get; set; } = new List<int>();
        public List<int> ComplaintTrend { get; set; } = new List<int>();
        public List<int> PcrTrend { get; set; } = new List<int>();
        public List<int> NcrTrend { get; set; } = new List<int>();

        public List<string> CarClassificationLabels { get; set; } = new List<string>();
        public List<int> CarClassificationCounts { get; set; } = new List<int>();
    }
}

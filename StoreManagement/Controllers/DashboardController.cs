using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IsseERP.Models;
using IsseERP.Services;
using StoreManagement.Models;

namespace StoreManagement.Controllers
{
    [Authorize]
    [SectionAccess(AppSection.Dashboard)]
    public class DashboardController : BaseController
    {
        private readonly CapaService capaService = new CapaService();
        private readonly ComplaintReportRepository pcrRepo = new ComplaintReportRepository();
        private readonly NcrRepository ncrRepo = new NcrRepository();

        public async Task<ActionResult> Index()
        {
            List<CapaReportModel> carReports;
            try
            {
                // Read-only dashboard: deliberately does NOT call CloseAllOverdue()
                // (that mutates the DB).
                carReports = await capaService.GetAllCapaReports();
            }
            catch
            {
                carReports = new List<CapaReportModel>();
            }

            var pcrs = pcrRepo.GetAllPCRs();
            var ncrs = ncrRepo.GetAllNCRs();

            var model = BuildDashboard(carReports, pcrs, ncrs);
            return View(model);
        }

        // Mirrors CapaList.cshtml's computeStatusKey so the dashboard's CAR
        // buckets match what the CAR list/status pages already show.
        private static string CarStatusKey(CapaReportModel c)
        {
            if (string.Equals(c.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) return "cancelled";
            if (string.Equals(c.Status, "Closed", StringComparison.OrdinalIgnoreCase)) return "closed";

            bool s2Done = !string.IsNullOrWhiteSpace(c.ImmediateAction);
            bool s2Verified = !string.IsNullOrWhiteSpace(c.IaVerifiedBy);
            bool s4Verified = !string.IsNullOrWhiteSpace(c.CapaVerifiedBy);

            if (!s2Done) return "response";
            if (!s2Verified || !s4Verified) return "verify";
            return "closed";
        }

        // PCR DocStatus codes: closing paths (QA remarks 8/9/10, or explicit close)
        // all settle on 2; 4 (Valid) is also a terminal outcome. 11 is cancel.
        private static string PcrBucket(int status)
        {
            switch (status)
            {
                case 2:
                case 4:
                case 8:
                case 9:
                case 10:
                    return "closed";
                case 11:
                    return "cancelled";
                default:
                    return "open";
            }
        }

        private static string NcrBucket(int status)
        {
            switch (status)
            {
                case 24: return "closed";
                case 29: return "cancelled";
                default: return "open";
            }
        }

        private static void Bucket(DashboardStats stats, string bucket)
        {
            switch (bucket)
            {
                case "closed": stats.Closed++; break;
                case "cancelled": stats.Cancelled++; break;
                default: stats.Open++; break;
            }
        }

        private DashboardViewModel BuildDashboard(List<CapaReportModel> carReports, List<Pcr> pcrs, List<Ncr> ncrs)
        {
            var model = new DashboardViewModel();

            // ---- CAR stats ----
            var carStats = new DashboardStats { Total = carReports.Count };
            foreach (var c in carReports)
            {
                switch (CarStatusKey(c))
                {
                    case "response": carStats.Open++; break;
                    case "verify": carStats.InVerification++; break;
                    case "cancelled": carStats.Cancelled++; break;
                    default: carStats.Closed++; break;
                }
            }
            model.CarStats = carStats;

            // ---- CAR by classification (who's responsible: LLII/Supplier/Trucker/etc.) ----
            var classificationGroups = carReports
                .GroupBy(c => string.IsNullOrWhiteSpace(c.CarClassificationType) ? "Unspecified" : c.CarClassificationType)
                .OrderByDescending(g => g.Count())
                .ToList();
            model.CarClassificationLabels = classificationGroups.Select(g => g.Key).ToList();
            model.CarClassificationCounts = classificationGroups.Select(g => g.Count()).ToList();

            // ---- PCR / NCR stats, tracked separately so we can compare them ----
            var pcrStats = new DashboardStats { Total = pcrs.Count };
            foreach (var p in pcrs) Bucket(pcrStats, PcrBucket(p.PcrStatus));
            model.PcrStats = pcrStats;

            var ncrStats = new DashboardStats { Total = ncrs.Count };
            foreach (var n in ncrs) Bucket(ncrStats, NcrBucket(n.NcrStatus));
            model.NcrStats = ncrStats;

            model.ComplaintStats = new DashboardStats
            {
                Total = pcrStats.Total + ncrStats.Total,
                Open = pcrStats.Open + ncrStats.Open,
                Closed = pcrStats.Closed + ncrStats.Closed,
                Cancelled = pcrStats.Cancelled + ncrStats.Cancelled
            };
            model.PcrTotal = pcrStats.Total;
            model.NcrTotal = ncrStats.Total;

            // ---- Trend: last 24 months (the view lets the user zoom into 3/6/12/24) ----
            var today = DateTime.Today;
            var months = new List<DateTime>();
            for (int i = 23; i >= 0; i--)
            {
                months.Add(new DateTime(today.Year, today.Month, 1).AddMonths(-i));
            }

            foreach (var m in months)
            {
                model.TrendYearMonths.Add(m.ToString("yyyy-MM"));
                model.TrendLabels.Add(m.ToString("MMM yyyy"));

                int carCount = carReports.Count(c => c.DateCreated.HasValue
                    && c.DateCreated.Value.Year == m.Year && c.DateCreated.Value.Month == m.Month);
                model.CarTrend.Add(carCount);

                int pcrCount = pcrs.Count(p => p.DateCreated.Year == m.Year && p.DateCreated.Month == m.Month);
                model.PcrTrend.Add(pcrCount);

                int ncrCount = ncrs.Count(n => n.DateCreated.Year == m.Year && n.DateCreated.Month == m.Month);
                model.NcrTrend.Add(ncrCount);

                model.ComplaintTrend.Add(pcrCount + ncrCount);
            }

            return model;
        }
    }
}

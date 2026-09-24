using System.Web.Mvc;
using StoreManagement.Models;

namespace StoreManagement.Controllers
{
    [Authorize]
    [SectionAccess(AppSection.ComplaintReport)]
    public class ComplaintReportController : BaseController
    {
        private readonly ComplaintReportRepository repo = new ComplaintReportRepository();
        private readonly AccountRepository accountRepo = new AccountRepository();

        [HttpGet]
        public ActionResult Index()
        {
            var pcrs = repo.GetAllPCRs();
            return View(pcrs);
        }

        [HttpGet]
        public ActionResult Details(int docId)
        {
            var pcr = repo.GetPCRDetails(docId);
            if (pcr == null)
            {
                TempData["PCRNotFoundMessage"] = "PCR Number Does Not Exist!";
                return RedirectToAction("Index");
            }
            return View(pcr);
        }

        [HttpPost]
        public JsonResult Respond(int docId, string qaRemarks, string qaFindings)
        {
            if (docId <= 0)
                return Json(new { success = false, message = "Invalid PCR." });

            if (string.IsNullOrWhiteSpace(qaFindings))
                return Json(new { success = false, message = "Findings cannot be empty." });

            if (string.IsNullOrWhiteSpace(qaRemarks))
                return Json(new { success = false, message = "Remarks cannot be empty." });

            var currentUser = accountRepo.GetByUsername(User.Identity.Name);
            int userId = currentUser != null ? currentUser.UserID : 0;

            bool success = repo.InsertQAResponse(docId, qaRemarks, qaFindings, userId, out string message);
            return Json(new { success = success, message = message });
        }
    }
}

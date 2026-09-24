using System.Web.Mvc;
using StoreManagement.Models;

namespace StoreManagement.Controllers
{
    [Authorize]
    [SectionAccess(AppSection.ComplaintReport)]
    public class NcrController : BaseController
    {
        private readonly NcrRepository repo = new NcrRepository();
        private readonly AccountRepository accountRepo = new AccountRepository();

        [HttpGet]
        public ActionResult Index()
        {
            var ncrs = repo.GetAllNCRs();
            return View(ncrs);
        }

        [HttpGet]
        public ActionResult Details(int docId)
        {
            var ncr = repo.GetNCRDetails(docId);
            if (ncr == null)
            {
                TempData["NCRNotFoundMessage"] = "NCR Number Does Not Exist!";
                return RedirectToAction("Index");
            }
            return View(ncr);
        }

        [HttpPost]
        public JsonResult Respond(int docId, string csRemarks, string csFindings)
        {
            if (docId <= 0)
                return Json(new { success = false, message = "Invalid NCR." });

            if (string.IsNullOrWhiteSpace(csFindings))
                return Json(new { success = false, message = "Findings cannot be empty." });

            if (string.IsNullOrWhiteSpace(csRemarks))
                return Json(new { success = false, message = "Remarks cannot be empty." });

            var currentUser = accountRepo.GetByUsername(User.Identity.Name);
            int userId = currentUser != null ? currentUser.UserID : 0;

            bool success = repo.InsertCSResponse(docId, csRemarks, csFindings, userId, out string message);
            return Json(new { success = success, message = message });
        }
    }
}

using System.Web.Mvc;
using StoreManagement.Models;

namespace StoreManagement.Controllers
{
    [Authorize]
    [SectionAccess(AppSection.PdfReceipt)]
    public class FinanceController : BaseController
    {
        // PDF parsing and receipt generation run in the browser (pdf.js / jsPDF); files are never uploaded.
        [HttpGet]
        public ActionResult PdfReceipt()
        {
            return View();
        }
    }
}

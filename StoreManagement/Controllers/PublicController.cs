using IsseERP.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace IsseERP.Controllers
{
    [RoutePrefix("capa/dashboard")]
    public class PublicController : Controller
    {

        private readonly PublicService _publicService = new PublicService();
        public ActionResult Index()
        {
            return View();
        }

        #region Inventory Putaway
        [Route("inventory/putaway")]
        public ActionResult InventoryWarehouse(string whse = "E-CAR")
        {
            ViewBag.Videos = GetVideos();
            var filteredTransfers = _publicService.GetAllTransfer(whse);


            return View(filteredTransfers);
        }



        [Route("inventory/putaway/category")]
        public ActionResult InboundTask(string whse = "E-CAR", string cat = "D")
        {
            ViewBag.Videos = GetVideos();
            ViewBag.Category = cat == "D" ? "Dry & PPOS" : "Frozen & Chilled";
            var filteredTransfers = _publicService.GetFilteredTransfers(whse, cat);
            return View(filteredTransfers);
        }
        #endregion

        #region Freight

        [Route("freight/in-transit")]
        public ActionResult Transit(string whse = "E-CAR")
        {
            return View();
        }

        #endregion

        #region Lobby / HR

        [Route("abcde")]
        public ActionResult LobbyArea(string whse = "E-CAR")
        {
            return View();
        }
        #endregion

        #region Json
        public JsonResult GetTask(string whse = "E-CAR", string cat = "D", int taskCount = 0)
        {
            string videoDir = Server.MapPath("~/Content/Images/Material/QA/");
            var videos = Directory.GetFiles(videoDir, "*.mp4")
                                  .Select(Path.GetFileName)
                                  .ToList();
            var filteredTransfers = _publicService.GetFilteredTransfers(whse, cat);
            int dbCount = filteredTransfers.Count;
            if (dbCount == taskCount)
            {
                return Json(new { updated = false }, JsonRequestBehavior.AllowGet);
            }
            return Json(new
            {
                updated = true,
                data = filteredTransfers,
                videos = videos,
                count = dbCount
            }, JsonRequestBehavior.AllowGet);
        }



        public JsonResult GetAllTask(string whse = "E-CAR", int taskCount = 0)
        {
            string videoDir = Server.MapPath("~/Content/Images/Material/QA/");
            var videos = Directory.GetFiles(videoDir, "*.mp4")
                                  .Select(Path.GetFileName)
                                  .ToList();
            var filteredTransfers = _publicService.GetAllTransfer(whse);

            return Json(new
            {
                updated = true,
                data = filteredTransfers,
                videos = videos,
                count = filteredTransfers.Count
            }, JsonRequestBehavior.AllowGet);
        }
        #endregion


        #region Video Helper
        // Video Helper
        private List<string> GetVideos()
        {
            string videoDir = Server.MapPath("~/Content/Images/Material/QA/");

            if (!Directory.Exists(videoDir))
                return new List<string>();

            return Directory.GetFiles(videoDir, "*.mp4")
                            .Select(Path.GetFileName)
                            .ToList();
        }
        // Video Helper
        #endregion
    }


}

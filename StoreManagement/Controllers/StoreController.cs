using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StoreManagement.Models;
using System.Data.Entity;
using System.Reflection.Emit;
using System.Web.UI.WebControls.Expressions;
using System.Drawing.Drawing2D;

namespace StoreManagement.Controllers
{
    [Authorize]
    public class StoreController : Controller
    {
        private StoreRepository repo = new StoreRepository();
        private AccountRepository accountRepo = new AccountRepository();

        //[Authorize]
        //[HttpGet]
        //public ActionResult Index()
        //{
        //    return View();
        //}

        // Main table
        [Authorize]
        [HttpGet]
        public ActionResult Index(
            int? storeID,
            string search,
            string storeNumber,
            string storeName,
            string cityName,
            string coordinates)
        {
            var stores = repo.GetAllStores(storeID, search, storeNumber, storeName, cityName, coordinates);
            ViewBag.StoreID = storeID;
            ViewBag.Search = search;
            ViewBag.StoreNumber = storeNumber;
            ViewBag.StoreName = storeName;
            ViewBag.CityName = cityName;
            ViewBag.Coordinates = coordinates;

            return View(stores);
        }

        // View modal
        public ActionResult Details(int id)
        {
            var store = repo.GetByStoreId(id);
            if (store == null) return HttpNotFound();
            return PartialView("_Details", store);
        }

        // Edit modal
        public ActionResult Edit(int id)
        {
            var store = repo.GetByStoreId(id);
            if (store == null) return HttpNotFound();
            ViewBag.Cities = new SelectList(repo.GetAllCities(), "CityID", "Name", store.CityID);
            return PartialView("_Edit", store);
        }

        // Confirm modal
        [HttpPost]
        public ActionResult Confirm(Store model)
        {
            var cities = repo.GetAllCities();
            var matchedCity = cities.Find(c => c.CityID == model.CityID);
            if (matchedCity != null)
            {
                model.CityName = matchedCity.Name;
            }
            return PartialView("_Confirm", model);
        }

        [HttpPost]
        public ActionResult Save(Store model)
        {
            var currentUser = accountRepo.GetByUsername(User.Identity.Name);
            int userID = currentUser != null ? currentUser.UserID : 0;

            repo.UpdateStore(model.StoreID, model.CityID, model.Coordinates, userID);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public JsonResult Suggestions(string field, string term)
        {
            var results = repo.GetSuggestions(field, term);
            return Json(results, JsonRequestBehavior.AllowGet);

        }

        //i already fx the getsearchsuggestions


        [HttpPost]
        public JsonResult GetSearchSuggestions(string term)
        {
            var suggestions = repo.GetSearchSuggestions(term);
            return Json(suggestions, JsonRequestBehavior.AllowGet);
        }

        //[HttpGet]
        //public JsonResult LogType()
        //{
        //    var result = LogTypeData.LogTypes
        //        .Select(logType => new
        //        {
        //            LogDescriptionID = logType.LogDescriptionID,
        //            LogDescription = logType.LogDescription,
        //            LogTypeName = logType.LogTypeName
        //        })
        //        .ToList();
        //    return Json(result, JsonRequestBehavior.AllowGet);
        //}
    }
}
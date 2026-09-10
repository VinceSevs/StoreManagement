using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StoreManagement.Controllers
{
    [Authorize]
    public class ItemController : Controller
    {
        public ActionResult Index()
        {
         return View();
        }
    }
}
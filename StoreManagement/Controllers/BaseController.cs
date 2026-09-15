using System.Web.Mvc;

namespace StoreManagement.Controllers
{
    public class BaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            if (User.Identity.IsAuthenticated)
            {
                if (Session["Username"] == null)
                {
                    Session["Username"] = User.Identity.Name;
                }
                ViewBag.CurrentUsername = User.Identity.Name;
            }
            else if (Session["Username"] != null)
            {
                Session.Clear();
            }
        }
    }
}

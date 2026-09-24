using System.Net;
using System.Web.Mvc;
using StoreManagement.Models;

namespace StoreManagement.Controllers
{
    // Blocks signed-in users whose department may not use this section (see UserAccess).
    // Anonymous requests pass through unchanged so [Authorize] and public pages (e.g. supplier CAR response) keep working.
    public class SectionAccessAttribute : ActionFilterAttribute
    {
        private readonly AppSection section;

        public SectionAccessAttribute(AppSection section)
        {
            this.section = section;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            if (http.User == null || !http.User.Identity.IsAuthenticated) return;

            var user = UserAccess.Current(http);
            if (UserAccess.CanAccess(user, section)) return;

            if (http.Request.IsAjaxRequest())
            {
                http.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, message = "You do not have access to this page." },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            var home = UserAccess.HomeRoute(user);
            if (home != null)
            {
                filterContext.Controller.TempData["AccessDeniedMessage"] = "You do not have access to that page.";
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary { { "action", home[0] }, { "controller", home[1] } });
                return;
            }

            filterContext.Result = new ViewResult { ViewName = "AccessDenied" };
        }
    }
}

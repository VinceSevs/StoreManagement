using Encryptionv2;
using StoreManagement.Models;
using System;
using System.Data.Entity;
using System.Web.Mvc;
using System.Web.Security;
using System.Diagnostics;

namespace StoreManagement.Controllers
{
    public class AccountController : BaseController
    {
        private AccountRepository repo = new AccountRepository();

        // Landing page depends on the user's department (see UserAccess).
        private string HomeUrl(Login user)
        {
            var home = UserAccess.HomeRoute(user);
            return home != null ? Url.Action(home[0], home[1]) : Url.Action("NoAccess", "Account");
        }

        private ActionResult RedirectToHome(Login user)
        {
            return Redirect(HomeUrl(user));
        }

        [Authorize]
        [HttpGet]
        public ActionResult NoAccess()
        {
            return View("AccessDenied");
        }

        [HttpGet]
        public ActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToHome(repo.GetByUsername(User.Identity.Name));
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            bool isAjax = Request.IsAjaxRequest();

            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "Please fill in your username and password." });
                }
                return View(model);
            }

            var user = repo.GetByUsername(model.Username);

            if (user != null && !string.IsNullOrEmpty(user.Password))
            {
                var crypto = new Encryptionv2.Encryptionv2();

                string encryptedPassword = crypto.EncryptPassword(model.Password);

                if (user.Password == encryptedPassword)
                {
                    FormsAuthentication.SetAuthCookie(user.Username, model.RememberMe);
                    Session["Username"] = user.Username;

                    if (isAjax)
                    {
                        return Json(new { success = true, redirectUrl = HomeUrl(user) });
                    }
                    return RedirectToHome(user);
                }
            }

            ModelState.AddModelError("", "Invalid username or password.");

            if (isAjax)
            {
                return Json(new { success = false, message = "Invalid username or password." });
            }
            return View(model);
        }


        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (repo.UsernameExists(model.Username))
            {
                ModelState.AddModelError("Username", "That username is already taken.");
                return View(model);
            }

            var crypto = new Encryptionv2.Encryptionv2();
            string hash = crypto.EncryptPassword(model.Password);
            repo.CreateUser(model.Username, hash);

            FormsAuthentication.SetAuthCookie(model.Username, false);
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [Authorize]
        [HttpGet]
        public ActionResult Account()
        {
            var user = repo.GetByUsername(User.Identity.Name);
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                return Json(new { success = false, message = "All fields are required." });

            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "New password and confirmation do not match." });

            if (newPassword.Length < 6)
                return Json(new { success = false, message = "New password must be at least 6 characters." });

            var user = repo.GetByUsername(User.Identity.Name);
            if (user == null)
                return Json(new { success = false, message = "Account not found." });

            var crypto = new Encryptionv2.Encryptionv2();

            if (user.Password != crypto.EncryptPassword(currentPassword))
                return Json(new { success = false, message = "Current password is incorrect." });

            repo.UpdatePassword(user.UserID, crypto.EncryptPassword(newPassword));

            return Json(new { success = true, message = "Password updated successfully." });
        }
    }
}

using Encryptionv2;
using StoreManagement.Models;
using System;
using System.Data.Entity;
using System.Web.Mvc;
using System.Web.Security;
using System.Diagnostics;

namespace StoreManagement.Controllers
{
    public class AccountController : Controller
    {
        private AccountRepository repo = new AccountRepository();

        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = repo.GetByUsername(model.Username);

            if (user != null && !string.IsNullOrEmpty(user.Password))
            {
                var crypto = new Encryptionv2.Encryptionv2();

                string encryptedPassword = crypto.EncryptPassword(model.Password);

                if (user.Password == encryptedPassword)
                {
                    FormsAuthentication.SetAuthCookie(user.Username, false);
                    Session["Username"] = user.Username;
                    return RedirectToAction("CapaList", "Capa");
                }
            }

            ModelState.AddModelError("", "Invalid username or password.");
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
    }
}

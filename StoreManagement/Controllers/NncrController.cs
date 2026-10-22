using IsseERP.Models;
using IsseERP.Services;
using StoreManagement.Controllers;
using StoreManagement.Models;
using StoreManagement.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace IsseERP.Controllers
{
    // NNCR workflow:
    //   Quality (QA files, Status = Awaiting Response, due in 7 working days) -> email to the supplier
    //     with a link to NncrResponse (shown as a popup while email sending is disabled)
    //   NncrResponse (supplier approves/rejects + remarks, Status = Responded)
    //   NncrStatus -> Close (role 49 only, Status = Closed)
    // NncrResponse / SubmitResponse / NncrAttachment are reached from the email and are not
    // authenticated (token only), so SectionAccess is applied per action instead of on the class.
    public class NncrController : BaseController
    {
        private const int MaxChecklistLength = 500; // NonconformanceType / NonconformanceInterception columns

        private readonly NncrRepository _nncrRepo = new NncrRepository();
        private readonly NncrLogRepository _nncrLog = new NncrLogRepository();
        private readonly NncrNotificationService _notifier = new NncrNotificationService();
        private readonly WarehouseService _warehouseService = new WarehouseService();
        private readonly VendorService _vendorService = new VendorService();
        private readonly CapaLogService _capaLogService = new CapaLogService();
        private readonly AccountRepository _accountRepo = new AccountRepository();

        private Login CurrentUser()
        {
            if (string.IsNullOrWhiteSpace(User?.Identity?.Name)) return null;
            return _accountRepo.GetByUsername(User.Identity.Name);
        }

        private string AbsoluteAction(string action, string token)
        {
            return Url.Action(action, "Nncr", new { token }, Request.Url.Scheme);
        }

        private NncrAttachmentService Attachments()
        {
            return new NncrAttachmentService(Server.MapPath("~/App_Data/NncrAttachments"));
        }

        private const string LogoPath = "~/Content/images/lead-logo.png";

        private NncrNotificationService.EmailMessage SupplierEmailPreview(NncrReport nncr)
        {
            string logoUrl = new Uri(Request.Url, Url.Content(LogoPath)).AbsoluteUri;
            return _notifier.BuildSupplierEmail(nncr, AbsoluteAction("NncrResponse", nncr.ResponseToken), logoUrl);
        }

        // Returns null when sent, otherwise why it was not sent.
        private string TrySendSupplierEmail(NncrReport nncr)
        {
            return _notifier.SendSupplierEmail(nncr, AbsoluteAction("NncrResponse", nncr.ResponseToken), Server.MapPath(LogoPath));
        }

        [Authorize]
        [SectionAccess(AppSection.Nncr)]
        public async Task<ActionResult> NncrList()
        {
            // Overdue first, then due within 3 working days (nearest due date first), then newest.
            var reports = (await _nncrRepo.GetAll())
                .OrderBy(r => r.IsOverdue ? 0 : r.IsDueSoon ? 1 : 2)
                .ThenBy(r => r.IsOverdue || r.IsDueSoon ? r.DueDate : null)
                .ThenByDescending(r => r.EventID)
                .ToList();
            return View(reports);
        }

        [Authorize]
        [SectionAccess(AppSection.Nncr)]
        public async Task<ActionResult> Quality()
        {
            var user = CurrentUser();
            ViewBag.IssuedByName = user?.Username ?? User.Identity.Name;
            ViewBag.Vendor = await _vendorService.GetVendors();
            ViewBag.Warehouse = await _warehouseService.GetWarehouse();
            return View();
        }

        // Posted as multipart/form-data: the NncrFileRequest fields plus "attachments" files.
        [Authorize]
        [SectionAccess(AppSection.Nncr)]
        [HttpPost]
        public async Task<JsonResult> SaveNncr(NncrFileRequest request, IEnumerable<HttpPostedFileBase> attachments)
        {
            var files = (attachments ?? Enumerable.Empty<HttpPostedFileBase>()).Where(f => f != null && f.ContentLength > 0).ToList();

            var user = CurrentUser();
            string error = null;
            if (user == null || user.UserID == 0) error = "Your session has expired. Please log in again.";
            else if (request == null) error = "No data received.";
            else if (request.IssuedToSupplier <= 0) error = "Please select the supplier (Issued To).";
            else if (request.SiteWarehouse <= 0) error = "Please select the site warehouse.";
            else if (string.IsNullOrWhiteSpace(request.NonconformanceType)) error = "Please tick at least one Type of Non-conformance.";
            else if (string.IsNullOrWhiteSpace(request.NonconformanceInterception)) error = "Please tick where the non-conformance was intercepted.";
            else if (string.IsNullOrWhiteSpace(request.DetailsOfNonconformance)) error = "Please enter the Details of the Non-conformance.";
            else if (request.NonconformanceType.Length > MaxChecklistLength || request.NonconformanceInterception.Length > MaxChecklistLength)
                error = $"Type / Interception details are too long (max {MaxChecklistLength} characters each).";
            else if (files.Count > NncrAttachmentService.MaxFiles)
                error = $"You can attach up to {NncrAttachmentService.MaxFiles} files.";
            else if (files.Any(f => f.ContentLength > NncrAttachmentService.MaxFileBytes))
                error = $"Each attachment must be {NncrAttachmentService.MaxFileBytes / (1024 * 1024)} MB or smaller.";
            else if (files.Sum(f => (long)f.ContentLength) > NncrAttachmentService.MaxTotalBytes)
                error = $"Attachments must total {NncrAttachmentService.MaxTotalBytes / (1024 * 1024)} MB or less.";

            if (error != null)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = error });
            }

            try
            {
                var (saved, duplicateError) = await _nncrRepo.Insert(request, user.UserID);
                if (saved == null)
                {
                    Response.StatusCode = 409;
                    return Json(new { success = false, message = duplicateError });
                }

                string attachmentError = null;
                try
                {
                    Attachments().Save(saved.ReportNumber, files);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("NNCR attachment save failed: " + ex);
                    attachmentError = "The NNCR was saved, but its attachments could not be saved: " + ex.Message;
                }

                var nncr = await _nncrRepo.GetByToken(saved.ResponseToken) ?? saved;
                string emailError = TrySendSupplierEmail(nncr);
                await _capaLogService.AddLog($"Filed {nncr.ReportNumber}", "Submit NNCR", user.UserID);
                await _nncrLog.Add(nncr.ReportNumber, NncrLogActions.Filed, $"NNCR filed against {nncr.SupplierName}", user.UserID, user.DisplayName);

                return Json(new
                {
                    success = true,
                    reportNumber = nncr.ReportNumber,
                    responseToken = nncr.ResponseToken,
                    attachmentError,
                    emailSent = emailError == null,
                    emailError,
                    email = SupplierEmailPreview(nncr)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                Response.StatusCode = 500;
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [AllowAnonymous]
        [Route("nncr-response")]
        public async Task<ActionResult> NncrResponse(string token)
        {
            var nncr = await _nncrRepo.GetByToken(token);
            if (nncr == null) return View("NncrResponseInvalid");

            nncr.Attachments = Attachments().List(nncr.ReportNumber);
            return View(nncr);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<JsonResult> SubmitResponse(NncrSupplierResponseRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ResponseToken))
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Missing response token." });
            }
            if (request.Decision != NncrDecisions.Approve && request.Decision != NncrDecisions.Reject)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Please choose Approve or Reject." });
            }
            if (string.IsNullOrWhiteSpace(request.VendorRemarks))
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Please enter your remarks." });
            }

            var nncr = await _nncrRepo.GetByToken(request.ResponseToken);
            if (nncr == null)
            {
                Response.StatusCode = 404;
                return Json(new { success = false, message = "NNCR not found." });
            }

            int? vendorId = await _nncrRepo.GetSupplierVendorId(request.ResponseToken);
            if (vendorId == null)
            {
                Response.StatusCode = 422;
                return Json(new { success = false, message = "This NNCR is not linked to a registered supplier, so the response cannot be accepted. Please contact QA." });
            }

            if (!await _nncrRepo.SubmitSupplierResponse(request.ResponseToken, request.Decision, request.VendorRemarks.Trim(),
                    vendorId.Value, nncr.SupplierEmail))
            {
                Response.StatusCode = 409;
                return Json(new { success = false, message = "This NNCR is no longer awaiting a response (it may have been answered already)." });
            }

            await _capaLogService.AddLog($"{nncr.SupplierName} responded to {nncr.ReportNumber} ({request.Decision})", "Submit NNCR Response", vendorId.Value);
            await _nncrLog.Add(nncr.ReportNumber, request.Decision == NncrDecisions.Approve ? NncrLogActions.Approved : NncrLogActions.Rejected, $"Supplier {(request.Decision == NncrDecisions.Approve ? "approved" : "rejected")} the NNCR", vendorId.Value, nncr.SupplierName);
            return Json(new { success = true });
        }

        [Authorize]
        [SectionAccess(AppSection.Nncr)]
        [Route("nncr-status")]
        public async Task<ActionResult> NncrStatus(string token)
        {
            var nncr = await _nncrRepo.GetByToken(token);
            if (nncr == null) return View("NncrResponseInvalid");

            nncr.Attachments = Attachments().List(nncr.ReportNumber);
            bool canViewLog = UserAccess.CanViewNncrLog(CurrentUser());
            ViewBag.CanViewLog = canViewLog;
            if (canViewLog) nncr.Logs = await _nncrLog.GetForReport(nncr.ReportNumber);
            ViewBag.CanClose = UserAccess.CanCloseNncr(CurrentUser());
            ViewBag.SupplierEmail = SupplierEmailPreview(nncr);
            return View(nncr);
        }

        // (Re)sends the supplier email, e.g. if it failed when filing or the supplier lost it.
        [Authorize]
        [SectionAccess(AppSection.Nncr)]
        [HttpPost]
        public async Task<JsonResult> SendSupplierEmail(NncrCloseRequest request)
        {
            var nncr = await _nncrRepo.GetByToken(request?.ResponseToken);
            if (nncr == null)
            {
                Response.StatusCode = 404;
                return Json(new { success = false, message = "NNCR not found." });
            }
            if (!nncr.IsAwaitingResponse)
            {
                Response.StatusCode = 409;
                return Json(new { success = false, message = "The supplier has already responded to this NNCR." });
            }

            string emailError = TrySendSupplierEmail(nncr);
            if (emailError != null)
            {
                Response.StatusCode = 502;
                return Json(new { success = false, message = emailError });
            }

            var user = CurrentUser();
            await _capaLogService.AddLog($"Emailed {nncr.ReportNumber} to {nncr.SupplierEmail}", "Send NNCR Email", user?.UserID ?? 0);
            await _nncrLog.Add(nncr.ReportNumber, NncrLogActions.Emailed, $"Supplier email re-sent to {nncr.SupplierEmail}", user?.UserID ?? 0, user?.DisplayName);
            return Json(new { success = true, to = nncr.SupplierEmail });
        }

        [Authorize]
        [SectionAccess(AppSection.Nncr)]
        [HttpPost]
        public async Task<JsonResult> CloseNncr(NncrCloseRequest request)
        {
            var user = CurrentUser();
            if (!UserAccess.CanCloseNncr(user))
            {
                Response.StatusCode = 403;
                return Json(new { success = false, message = "Only authorized users can close an NNCR." });
            }
            if (request == null || string.IsNullOrWhiteSpace(request.ResponseToken))
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Missing response token." });
            }

            if (!await _nncrRepo.Close(request.ResponseToken, user.UserID))
            {
                Response.StatusCode = 409;
                return Json(new { success = false, message = "This NNCR can only be closed after the supplier has approved or rejected it." });
            }

            var nncr = await _nncrRepo.GetByToken(request.ResponseToken);
            await _capaLogService.AddLog($"Closed {nncr?.ReportNumber}", "Close NNCR", user.UserID);
            await _nncrLog.Add(nncr?.ReportNumber, NncrLogActions.Closed, "NNCR closed", user.UserID, user.DisplayName);
            return Json(new { success = true });
        }

        // Serves one attachment. Reached from the status page and the supplier's emailed link,
        // so access is by the NNCR's token. Images and PDFs open in the browser; anything
        // else is always downloaded so an uploaded file can't run as a page on this site.
        [AllowAnonymous]
        public async Task<ActionResult> NncrAttachment(string token, string file)
        {
            var nncr = await _nncrRepo.GetByToken(token);
            string path = nncr == null ? null : Attachments().Resolve(nncr.ReportNumber, file);
            if (path == null) return HttpNotFound();

            string contentType = MimeMapping.GetMimeMapping(path);
            bool inline = (contentType.StartsWith("image/") && contentType != "image/svg+xml") || contentType == "application/pdf";
            Response.AppendHeader("X-Content-Type-Options", "nosniff");
            if (!inline) return File(path, "application/octet-stream", System.IO.Path.GetFileName(path));

            Response.AppendHeader("Content-Disposition", "inline; filename=\"" + System.IO.Path.GetFileName(path).Replace("\"", "") + "\"");
            return File(path, contentType);
        }
    }
}

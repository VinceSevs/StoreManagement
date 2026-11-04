using IsseERP.Models;
using IsseERP.Services;
using StoreManagement.Controllers;
using StoreManagement.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using StoreManagement.Repositories;

namespace IsseERP.Controllers
{
    [SectionAccess(AppSection.Car)]
    public class CapaController : BaseController
    {
        private readonly CapaService _capaService = new CapaService();
        private readonly PublicService _publicService = new PublicService();
        private readonly WarehouseService _warehouseService = new WarehouseService();
        private readonly ItemService _itemService = new ItemService();
        private readonly TruckService _truckService = new TruckService();
        private readonly VendorService _vendorService = new VendorService();
        private readonly UOMService _uomService = new UOMService();
        private readonly UserAccountService _userAccountService = new UserAccountService();
        private readonly CapaLogService _capaLogService = new CapaLogService();
        private readonly AccountRepository _accountRepo = new AccountRepository();

        private int CurrentUserID()
        {
            if (string.IsNullOrWhiteSpace(User?.Identity?.Name)) return 0;
            var account = _accountRepo.GetByUsername(User.Identity.Name);
            return account?.UserID ?? 0;
        }
        public async Task<ActionResult> CapaList()
        {
            var reports = await _capaService.GetAllCapaReports();

            // A CAR that was sent back gets a fresh 7 working days from the day it was
            // sent back. That date only exists in the loop history files.
            var historyStore = new CapaLoopHistoryStore(Server.MapPath("~/App_Data/CapaLoopHistory"));
            foreach (var report in reports.Where(r => r.LoopNumber > 0 && r.StatusKey == "response"))
            {
                var lastRound = historyStore.GetForReport(report.ReportNumber).LastOrDefault();
                if (lastRound != null && lastRound.SentBackDate > new DateTime(1900, 1, 2))
                    report.LastSentBackDate = lastRound.SentBackDate;
            }

            return View(reports);
        }

        public async Task<ActionResult> Quality()
        {
            List<Warehouse> warehouse = await _warehouseService.GetWarehouse();
            List<Item> items = await _itemService.GetItemlist();
            List<Trucker> truckers = await _truckService.GetTruckers();
            List<Vendor> vendors = await _vendorService.GetVendors();
            List<Department> department = await _capaService._GetDepartment();
            List<UOM> uomList = await _uomService.GetUOMList();

            ViewBag.Warehouse = warehouse;
            ViewBag.Item = items;
            ViewBag.Truck = truckers;
            ViewBag.Vendor = vendors;
            ViewBag.DcSites = warehouse;
            ViewBag.Capa = department;
            ViewBag.UOM = uomList;

            string reportNumber = await _capaService.GetNextCapaNo();

            ViewBag.ReportNumber = reportNumber;

            return View();
        }

        [HttpGet]
        public async Task<JsonResult> Catalog()
        {
            List<Item> catalog = await _itemService.GetItemlist();

            var result = catalog.Select(i => new
            {
                id = i.ItemID,
                name = i.ItemDescription,
                code = i.ItemCode
            }).ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult DefectCatalog()
        {
            var result = DefectCatalogData.Items
                .Select(d => new 
                { 
                    id = d.Id, 
                    name = d.Name, 
                    category = d.Category 
                }).ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<JsonResult> WarehouseList()
        {
            var warehouses = await _warehouseService.GetWarehouse();
            var result = warehouses.Select(w => new
            {
                id = w.WarehouseID,
                name = w.WarehouseName,
                code = w.ExternalCode
            }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<JsonResult> DepartmentList()
        {
            var departments = await _capaService._GetDepartment();
            var result = departments.Select(d => new
            {
                id = d.DepartmentID,
                name = d.DepartmentDescription
            }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<JsonResult> VendorList()
        {
            var vendors = await _vendorService.GetVendors();
            var result = vendors.Select(v => new
            {
                id = v.VendorID,
                name = v.VendorName,
                email = v.EmailAddress
            }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<JsonResult> VendorItemList(int vendorId)
        {
            var items = await _vendorService.GetItemsByVendor(vendorId);
            var result = items.Select(i => new
            {
                id = i.ItemID,
                name = i.ItemDescription,
                code = i.ItemCode
            }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<JsonResult> TruckerList()
        {
            var truckers = await _truckService.GetTruckers();
            var result = truckers.Select(t => new
            {
                id = t.TruckID,
                name = t.TruckerName,
                email = t.EmailAddress
            }).ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<JsonResult> SaveCapaReport()
        {
            // Without this IIS replaces our JSON 4xx/5xx bodies with its generic error page,
            // so the form could only ever show "HTTP 500" instead of the real message.
            Response.TrySkipIisCustomErrors = true;

            try
            {
                // 2. Read the raw incoming JSON stream manually
                Request.InputStream.Position = 0;
                string rawJson;
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    rawJson = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    Response.StatusCode = 400;
                    return Json(new { success = false, message = "No data received." });
                }

                // 3. Deserialize manually using Newtonsoft.Json (which ignores the 4MB limit)
                var model = Newtonsoft.Json.JsonConvert.DeserializeObject<CapaReportModel>(rawJson);

                if (model == null)
                {
                    Response.StatusCode = 400;
                    return Json(new { success = false, message = "Invalid data payload structure." });
                }

                // LLII / Others take a free-text list of emails: validate and store them normalised.
                if (model.CarClassificationType == "LLII" || model.CarClassificationType == "Others")
                {
                    string emails = CapaEmailList.Normalize(model.CarClassificationOtherText, out var invalidEmails);
                    if (invalidEmails.Count > 0)
                    {
                        Response.StatusCode = 400;
                        return Json(new
                        {
                            success = false,
                            message = "Invalid email address: " + string.Join(", ", invalidEmails)
                        });
                    }
                    model.CarClassificationOtherText = emails;
                }

                // 1. GENERATE REPORT NUMBER ON THE SERVER
                string reportNumber = await _capaService.GetNextCapaNo();
                model.ReportNumber = reportNumber;
                model.Status = CapaStatuses.AwaitingResponse;

                if (model.Items == null || model.Items.Count == 0)
                {
                    model.Items = new List<CapaItemModel> { new CapaItemModel { LineNumber = 1 } };
                }

                // 2. Insert the header row + detail rows using the generated report number
                bool inserted = await _capaService.InsertCapaReport(model);

                if (!inserted)
                {
                    Response.StatusCode = 500;
                    return Json(new
                    {
                        success = false,
                        message = "Could not save the report. Please try again."
                    });
                }

                await _capaService.MarkAwaitingResponse(reportNumber);

                // 3. Save attachments using the server-generated report number
                // The CAR is already in the database by now, so a file failure is reported
                // alongside the success instead of failing (and inviting a duplicate filing).
                string attachmentError = null;
                string attachmentFolder = null;
                int attachmentsSent = model.Items.Sum(i => i.Attachments == null ? 0 : i.Attachments.Count);
                int attachmentsSaved = 0;
                try
                {
                    string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
                    attachmentFolder = System.IO.Path.Combine(attachmentsRoot, reportNumber);
                    var fileService = new CapaAttachmentFileService(attachmentsRoot);
                    foreach (var item in model.Items)
                    {
                        attachmentsSaved += fileService.SaveItemAttachments(reportNumber, item.LineNumber, item.Attachments).Count;
                    }
                    if (attachmentsSaved < attachmentsSent)
                    {
                        attachmentError = $"{attachmentsSent} file(s) were received but only {attachmentsSaved} could be saved to {attachmentFolder}.";
                    }
                }
                catch (Exception attachEx)
                {
                    System.Diagnostics.Trace.TraceError("CAR attachment save failed for " + reportNumber + ": " + attachEx);
                    attachmentError = attachEx.GetType().Name + ": " + attachEx.Message;
                }

                await _capaLogService.AddLog(
                    $"Filed {reportNumber}",
                    "Submit to Database",
                    CurrentUserID());


                var savedCapa = await _capaService.GetCapaByReportNumber(reportNumber);

                // Return the newly generated report number back to the client
                return Json(new { success = true, ReportNumber = reportNumber, ResponseToken = savedCapa?.ResponseToken, attachmentError, attachmentsSent, attachmentsSaved, attachmentFolder });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());

                Response.StatusCode = 500;
                return Json(new
                {
                    success = false,
                    message = "Error: " + ex.Message
                });
            }
        }
        
        [Route("capa-response")]
        public async Task<ActionResult> CapaResponse(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View("CapaResponseInvalid");
            }
            var capa = await _capaService.GetCapaByToken(token);
            if (capa == null)
            {
                return View("CapaResponseInvalid");
            }

            string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
            var fileService = new CapaAttachmentFileService(attachmentsRoot);

            var itemAttachments = new Dictionary<int, List<SavedAttachment>>();
            var allAttachments = new List<SavedAttachment>();
            foreach (var item in capa.Items)
            {
                var photos = fileService.GetItemAttachmentsByCapaNo(capa.ReportNumber, item.LineNumber);
                itemAttachments[item.LineNumber] = photos;
                allAttachments.AddRange(photos);
            }
            ViewBag.AlreadyResponded = !string.IsNullOrWhiteSpace(capa.ImmediateAction);
            ViewBag.Status = capa.Status;

            ViewBag.Capa = capa;
            ViewBag.ItemAttachments = itemAttachments;
            ViewBag.Attachments = allAttachments;
            ViewBag.ResponseToken = token;
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<JsonResult> Cancel(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                Response.StatusCode = 400;
                return Json(new 
                { 
                    success = false, 
                    message = "Missing response token." 
                });
            }

            var capaBeforeCancel = await _capaService.GetCapaByToken(token);
            bool cancelled = await _capaService.CancelRequest(token);

            if (!cancelled)
            {
                Response.StatusCode = 400;
                return Json(new
                {
                    success = false,
                    message = "This report can no longer be cancelled — it may have already been responded to, verified, or closed."
                });
            }

            await _capaLogService.AddLog(
                $"Cancelled {(capaBeforeCancel != null ? capaBeforeCancel.ReportNumber : token)}",
                "Cancel CAR",
                CurrentUserID());

            if (capaBeforeCancel != null)
            {
                await _capaService.NotifyStatusChanged(
                    capaBeforeCancel.EventID, token, CapaStatuses.Cancelled, User?.Identity?.Name);
            }

            return Json(new
            {
                success = true
            });
        }

        public ActionResult CapaAttachment(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains(".."))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            string root = Server.MapPath("~/App_Data/CapaAttachments");
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(fullPath))
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            string contentType = MimeMapping.GetMimeMapping(fullPath);
            return File(fullPath, contentType);
        }

        [HttpPost]
        public async Task<JsonResult> SubmitCapaResponse(SendeeResponseRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ResponseToken))
            {
                Response.StatusCode = 400;
                return Json(new 
                { 
                    success = false, 
                    message = "Missing response token." 
                });
            }

            // The response page already hides the form for these, but a form opened before
            // QA cancelled (or a direct post) must not get through either.
            var capaBeforeResponse = await _capaService.GetCapaByToken(request.ResponseToken);
            if (capaBeforeResponse == null)
            {
                Response.StatusCode = 404;
                return Json(new
                {
                    success = false,
                    message = "This CAR could not be found."
                });
            }
            if (capaBeforeResponse.StatusKey != "response")
            {
                Response.StatusCode = 400;
                string reason = capaBeforeResponse.StatusKey == "cancelled"
                    ? "This CAR has been cancelled, so a response can no longer be submitted. Please contact QA."
                    : "This CAR is no longer waiting for a response.";
                return Json(new
                {
                    success = false,
                    message = reason
                });
            }

            bool success = await _capaService.SubmitSendeeResponse(request);

            if (!success)
            {
                Response.StatusCode = 500;
                return Json(new 
                { 
                    success = false, 
                    message = "Could not save your response. Please try again." 
                });
            }

            var respondedCapa = await _capaService.GetCapaByToken(request.ResponseToken);

            bool hasGeneral = request.Attachments != null && request.Attachments.Count > 0;
            bool hasRootCause = request.RootCauseAttachments != null && request.RootCauseAttachments.Count > 0;
            if (hasGeneral || hasRootCause)
            {
                try
                {
                    if (respondedCapa != null && !string.IsNullOrWhiteSpace(respondedCapa.ReportNumber))
                    {
                        string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
                        var fileService = new CapaAttachmentFileService(attachmentsRoot);
                        // loopNumber is 0 on the first response and goes up by one each
                        // time QA sends the CAR back, so this is the response round.
                        int round = respondedCapa.LoopNumber + 1;
                        if (hasGeneral)
                            fileService.SaveResponseAttachments(respondedCapa.ReportNumber, request.Attachments, round);
                        if (hasRootCause)
                            fileService.SaveResponseAttachments(respondedCapa.ReportNumber, request.RootCauseAttachments, round, "RootCause");
                    }
                }
                catch (Exception attachEx)
                {
                    System.Diagnostics.Trace.TraceError("CAR response attachment save failed: " + attachEx);
                }
            }

            string respondentEmail = !string.IsNullOrWhiteSpace(respondedCapa?.CarClassificationRefEmail)
                ? respondedCapa.CarClassificationRefEmail
                : respondedCapa?.CarClassificationOtherText;
            await _capaLogService.AddLog(
                $"{(!string.IsNullOrWhiteSpace(respondentEmail) ? respondentEmail : "Unknown respondent")} submitted a response",
                "Submit Response",
                1);

            if (respondedCapa != null)
            {
                await _capaService.NotifyStatusChanged(
                    respondedCapa.EventID, request.ResponseToken, CapaStatuses.AwaitingVerification, respondentEmail);
            }

            return Json(new 
            { 
                success = true 
            });
        }

        [Authorize]
        [Route("capa-verification")]
        public async Task<ActionResult> CapaVerification(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View("CapaResponseInvalid");
            }
            var capa = await _capaService.GetCapaByToken(token);
            if (capa == null)
            {
                return View("CapaResponseInvalid");
            }

            if (string.IsNullOrWhiteSpace(capa.ImmediateAction))
            {
                return RedirectToAction("CapaStatus", new 
                { 
                    token
                });
            }

            bool alreadyVerified = !string.IsNullOrWhiteSpace(capa.IaVerifiedBy) && !string.IsNullOrWhiteSpace(capa.CapaVerifiedBy);

            List<UserAccountLookup> userAccounts = await _userAccountService.GetAllUsers();

            string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
            var fileService = new CapaAttachmentFileService(attachmentsRoot);

            var itemAttachments = new Dictionary<int, List<SavedAttachment>>();
            foreach (var item in capa.Items)
            {
                itemAttachments[item.LineNumber] = fileService.GetItemAttachmentsByCapaNo(capa.ReportNumber, item.LineNumber);
            }

            ViewBag.Capa = capa;
            ViewBag.AlreadyVerified = alreadyVerified;
            ViewBag.ResponseToken = token;
            ViewBag.UserAccounts = userAccounts;
            ViewBag.ItemAttachments = itemAttachments;
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<JsonResult> SubmitVerification(VerificationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ResponseToken))
            {
                Response.StatusCode = 400;
                return Json(new 
                { 
                    success = false, 
                    message = "Missing response token." 
                });
            }

            var capaBeforeVerify = await _capaService.GetCapaByToken(request.ResponseToken);

            // Sending a CAR back (Not Effective) clears the response. Save a copy of it as
            // this round's history first; if that can't be written, don't send it back.
            var historyStore = new CapaLoopHistoryStore(Server.MapPath("~/App_Data/CapaLoopHistory"));
            int? historyRound = null;
            if (!request.IsEffective && capaBeforeVerify != null && !string.IsNullOrWhiteSpace(capaBeforeVerify.ImmediateAction))
            {
                try
                {
                    var snapshot = new CapaLoopHistoryEntry
                    {
                        RoundNumber = capaBeforeVerify.LoopNumber + 1,
                        ImmediateAction = capaBeforeVerify.ImmediateAction,
                        IaPerson = capaBeforeVerify.IaPerson,
                        IaTargetDate = capaBeforeVerify.IaTargetDate,
                        RootCause = capaBeforeVerify.RootCause,
                        CapaActions = capaBeforeVerify.CapaActions,
                        CapaPerson = capaBeforeVerify.CapaPerson,
                        CapaTargetDate = capaBeforeVerify.CapaTargetDate,
                        VerificationNotes = request.VerificationNotes,
                        SentBackBy = request.VerifiedByUsername,
                        SentBackDate = DateTime.Now
                    };
                    historyStore.Save(capaBeforeVerify.ReportNumber, snapshot);
                    historyRound = snapshot.RoundNumber;
                }
                catch (Exception historyEx)
                {
                    System.Diagnostics.Trace.TraceError("CAR loop history save failed: " + historyEx);
                    Response.StatusCode = 500;
                    return Json(new
                    {
                        success = false,
                        message = "Could not save the response history, so the CAR was not sent back. Please try again."
                    });
                }
            }

            bool success = await _capaService.SubmitVerification(request);

            if (!success)
            {
                // The send-back didn't happen, so the round isn't history.
                if (historyRound.HasValue)
                {
                    try { historyStore.Delete(capaBeforeVerify.ReportNumber, historyRound.Value); } catch { }
                }

                Response.StatusCode = 500;
                return Json(new
                {
                    success = false,
                    message = "Could not save the verification. Please try again."
                });
            }

            await _capaLogService.AddLog(
                $"Submitted verification for {(capaBeforeVerify != null ? capaBeforeVerify.ReportNumber : request.ResponseToken)} — {(request.IsEffective ? "Effective" : "Not Effective")}",
                "Submit Verification",
                CurrentUserID());

            if (capaBeforeVerify != null)
            {
                // Effective closes the CAR; Not Effective sends it back to the respondent.
                await _capaService.NotifyStatusChanged(
                    capaBeforeVerify.EventID,
                    request.ResponseToken,
                    request.IsEffective ? CapaStatuses.Closed : CapaStatuses.AwaitingResponse,
                    request.VerifiedByUsername ?? User?.Identity?.Name,
                    request.VerificationNotes);
            }

            return Json(new
            {
                success = true
            });
        }

        [Authorize]
        [Route("capa-status")]
        public async Task<ActionResult> CapaStatus(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View("CapaResponseInvalid");
            }
            var capa = await _capaService.GetCapaByToken(token);
            if (capa == null)
            {
                return View("CapaResponseInvalid");
            }

            string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
            var fileService = new CapaAttachmentFileService(attachmentsRoot);

            var itemAttachments = new Dictionary<int, List<SavedAttachment>>();
            var allAttachments = new List<SavedAttachment>();
            foreach (var item in capa.Items)
            {
                var photos = fileService.GetItemAttachmentsByCapaNo(capa.ReportNumber, item.LineNumber);
                itemAttachments[item.LineNumber] = photos;
                allAttachments.AddRange(photos);
            }

            // History of earlier rounds QA sent back: loaded only for IT, so for anyone
            // else the data never reaches the view.
            if (UserAccess.CanViewCarLoopHistory(UserAccess.Current(HttpContext)))
            {
                var loopHistory = new CapaLoopHistoryStore(Server.MapPath("~/App_Data/CapaLoopHistory")).GetForReport(capa.ReportNumber);
                foreach (var round in loopHistory)
                {
                    round.Attachments = fileService.GetResponseAttachmentsForRound(capa.ReportNumber, round.RoundNumber);
                }
                ViewBag.LoopHistory = loopHistory;
            }

            ViewBag.Capa = capa;
            ViewBag.ItemAttachments = itemAttachments;
            ViewBag.Attachments = allAttachments;
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> LogCapaAction(string logType, string reportNumber)
        {
            if (string.IsNullOrWhiteSpace(logType))
            {
                Response.StatusCode = 400;
                return Json(new { success = false });
            }

            string description = string.IsNullOrWhiteSpace(reportNumber)
                ? logType
                : $"{logType} for {reportNumber}";

            await _capaLogService.AddLog(description, logType, CurrentUserID());

            return Json(new { success = true });
        }
    }
}

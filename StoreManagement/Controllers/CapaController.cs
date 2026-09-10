using IsseERP.Models;
using IsseERP.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace IsseERP.Controllers
{
    public class CapaController : Controller
    {
        private readonly CapaService _capaService = new CapaService();
        private readonly PublicService _publicService = new PublicService();
        private readonly WarehouseService _warehouseService = new WarehouseService();
        private readonly ItemService _itemService = new ItemService();
        private readonly TruckService _truckService = new TruckService();
        private readonly VendorService _vendorService = new VendorService();
        private readonly UOMService _uomService = new UOMService();
        private readonly UserAccountService _userAccountService = new UserAccountService();
        public async Task<ActionResult> CapaList()
        {
            await _capaService.CloseAllOverdue();
            var reports = await _capaService.GetAllCapaReports();
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

                // 1. GENERATE REPORT NUMBER ON THE SERVER
                string reportNumber = await _capaService.GetNextCapaNo();
                model.ReportNumber = reportNumber;

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

                // 3. Save attachments using the server-generated report number
                string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
                var fileService = new CapaAttachmentFileService(attachmentsRoot);
                foreach (var item in model.Items)
                {
                    if (item.LineNumber == 1)
                    {
                        fileService.SaveAttachments(reportNumber, item.Attachments);
                    }
                    else
                    {
                        fileService.SaveItemAttachments(reportNumber, item.LineNumber, item.Attachments);
                    }
                }

                // Return the newly generated report number back to the client
                return Json(new { success = true, ReportNumber = reportNumber });
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
                var photos = item.LineNumber == 1
                    ? fileService.GetAttachmentsByCapaNo(capa.ReportNumber)
                    : fileService.GetItemAttachmentsByCapaNo(capa.ReportNumber, item.LineNumber);
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
        public async Task<JsonResult> CloseIfOverdue(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                Response.StatusCode = 400;
                return Json(new { success = false });
            }

            bool closed = await _capaService.CloseIfOverdue(token);
            return Json(new 
            { 
                success = true, 
                closed = closed 
            });
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

            if (request.Attachments != null && request.Attachments.Count > 0)
            {
                try
                {
                    var capa = await _capaService.GetCapaByToken(request.ResponseToken);
                    if (capa != null && !string.IsNullOrWhiteSpace(capa.ReportNumber))
                    {
                        string attachmentsRoot = Server.MapPath("~/App_Data/CapaAttachments");
                        var fileService = new CapaAttachmentFileService(attachmentsRoot);
                        fileService.SaveResponseAttachments(capa.ReportNumber, request.Attachments);
                    }
                }
                catch (Exception attachEx)
                {
                    System.Diagnostics.Trace.TraceError("CAPA response attachment save failed: " + attachEx);
                }
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
                itemAttachments[item.LineNumber] = item.LineNumber == 1
                    ? fileService.GetAttachmentsByCapaNo(capa.ReportNumber)
                    : fileService.GetItemAttachmentsByCapaNo(capa.ReportNumber, item.LineNumber);
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

            bool success = await _capaService.SubmitVerification(request);

            if (!success)
            {
                Response.StatusCode = 500;
                return Json(new 
                { 
                    success = false, 
                    message = "Could not save the verification. Please try again." 
                });
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
                var photos = item.LineNumber == 1
                    ? fileService.GetAttachmentsByCapaNo(capa.ReportNumber)
                    : fileService.GetItemAttachmentsByCapaNo(capa.ReportNumber, item.LineNumber);
                itemAttachments[item.LineNumber] = photos;
                allAttachments.AddRange(photos);
            }

            ViewBag.Capa = capa;
            ViewBag.ItemAttachments = itemAttachments;
            ViewBag.Attachments = allAttachments;
            return View();
        }
    }
}

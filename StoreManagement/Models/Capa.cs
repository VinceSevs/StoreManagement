using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IsseERP.Models

{
    // Property names here match the SQL columns 1:1 on purpose, so the
    // repository can map straight across without any translation step.
    public class CapaReportModel
    {
        public int EventID { get; set; }
        public string ReportNumber { get; set; } //CAR-00001 SAMPLE
        public string ResponseToken { get; set; }
        public DateTime? DateCreated { get; set; }

        // Header
        public string IssuedBy { get; set; }
        public int IssuedToDepartment { get; set; }
        public string IssuedToDepartmentName { get; set; } // display only, resolved via JOIN
        public int SiteWarehouse { get; set; }
        public string SiteWarehouseName { get; set; } // display only, resolved via JOIN
        public string RefNo { get; set; }
        public DateTime? CorrectionDueDate { get; set; }
        public DateTime? ReportDueDate { get; set; }

        // Workflow stage: Filed -> AwaitingResponse -> AwaitingVerification
        // -> AwaitingEffectivenessCheck -> Closed (loops back to
        // AwaitingResponse if the effectiveness check fails)
        public string Status { get; set; }

        // How many times this CAR has been sent back to Awaiting Response
        // after failing an effectiveness check. 0 the first time through.
        // Excluded from the JSON posted to api/capa/insert — that external
        // API never knew this field existed, and doesn't need it.
        [JsonIgnore]
        public int LoopNumber { get; set; }

        // Type of non-conformance — single-select. One of
        // "ProductQualityFoodSafety" | "EnvironmentHealthSafetySecurity" |
        // "InternalQualityAudit" | "ExternalQualityAudit" | "CustomerAudit".
        // ExternalAuditSubType ("Government" | "ThirdParty") only applies
        // when NonconformanceType == "ExternalQualityAudit".
        public string NonconformanceType { get; set; }
        public string ExternalAuditSubType { get; set; }

        // CAR classification — single-select. Type is one of
        // "LLII" | "Supplier" | "Trucker" | "DCSite" | "Others".
        // RefId/RefName are only populated for Supplier/Trucker/DCSite
        // (pointing into your existing lookup tables). OtherText is only
        // populated for LLII/Others.
        public string CarClassificationType { get; set; }
        public string CarClassificationRefId { get; set; }
        public string CarClassificationRefName { get; set; }
        public string CarClassificationRefEmail { get; set; }
        public string CarClassificationOtherText { get; set; }

        // Step 1

        // Step 2 — filled by whoever CAR Classification points to (entered
        // internally once QA has their response)
        public string ImmediateAction { get; set; }
        public string IaPerson { get; set; }
        public DateTime? IaTargetDate { get; set; }
        public string IaVerifiedBy { get; set; }
        public DateTime? IaVerifiedDate { get; set; }

        // Step 3
        public string RootCause { get; set; }

        // Step 4
        public string CapaActions { get; set; }
        public string CapaPerson { get; set; }
        public DateTime? CapaTargetDate { get; set; }
        public string CapaVerifiedBy { get; set; }
        public DateTime? CapaVerifiedDate { get; set; }

        // Step 7: Verification of effectiveness
        public string VerificationNotes { get; set; }
        public bool EffEffective { get; set; }
        public bool EffNotEffective { get; set; }
        public string EffRemarks { get; set; }
        public int? VerifiedBy { get; set; } // FK -> tbl_UserAccount.id
        public string VerifiedByName { get; set; } // display only
        public DateTime? VerifiedDate { get; set; }
        public int? ApprovedBy { get; set; } // FK -> tbl_UserAccount.id
        public string ApprovedByName { get; set; } // display only
        public DateTime? ApprovedDate { get; set; }
        public int StockEventID { get; set; } // 0 = SA OUT, 1 = OUT RIGHT

        // Repeatable items (tbl_QualityIncidentDtl) — one row per "Add
        // Item" block on the Quality form.
        public List<CapaItemModel> Items { get; set; } = new List<CapaItemModel>();
    }

    // One row in tbl_QualityIncidentDtl — a single item within a CAPA.
    public class CapaItemModel
    {
        public int LineNumber { get; set; }
        public int? ItemID { get; set; } // FK -> tbl_Item.id
        // display only — excluded from the JSON posted to api/capa/insert,
        // same reason as CapaReportModel.LoopNumber above.
        [JsonIgnore]
        public string ItemCode { get; set; }
        public string ItemDescription { get; set; } // display only
        public short? Quantity { get; set; }
        public short? UOMID { get; set; } // FK -> tbl_UOM.id
        public string UOMDisplay { get; set; } // display only, "Text - ISO_Code"
        public short? DefectCategoryID { get; set; } // FK into the defect catalog (id added there)
        public string DefectName { get; set; } // display only
        public string DefectCategoryName { get; set; } // display only
        public DateTime? UsedToDate { get; set; }
        public string Remarks { get; set; }
        public string Hauler { get; set; }
        public short? AdjustmentQty { get; set; }
        public int? AdjustmentBy { get; set; } // FK -> tbl_UserAccount.id
        public string AdjustmentByName { get; set; } // display only
        public DateTime? AdjustmentDate { get; set; }
        public List<CapaAttachmentModel> Attachments { get; set; } = new List<CapaAttachmentModel>();
    }

    public class CapaAttachmentModel
    {
        public string Caption { get; set; }

        // The full data URL as produced by the browser's FileReader,
        // e.g. "data:image/png;base64,iVBORw0KGgo...."
        public string DataUrl { get; set; }
    }

    // Represents an attachment after it has been decoded and written to disk —
    // this is what actually gets stored in the CapaAttachments table.
    public class SavedAttachment
    {
        // Path relative to the attachments root, e.g. "CAPA-0001/attachment_01.png"
        public string FilePath { get; set; }
        public string Caption { get; set; }
        public string ContentType { get; set; }
    }

    // A simple {Id, Name, Email} triple used to populate the
    // Supplier/Trucker/DC Site dropdowns in CAR Classification.
    public class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }


    public class CapaInsertResult
    {
        public string ReportNumber { get; set; }
        public string ResponseToken { get; set; }
    }

    // Submitted from the CapaResponse page (reached via the emailed link) —
    // looked up by ResponseToken rather than CapaNo/CapaReportId, since
    // whoever's filling this in doesn't have a login to this system.
    public class SendeeResponseRequest
    {
        public string ResponseToken { get; set; }

        // Step 2
        public string ImmediateAction { get; set; }
        public string IaPerson { get; set; }
        public DateTime? IaTargetDate { get; set; }

        // Step 3
        public string RootCause { get; set; }

        // Step 4
        public string CapaActions { get; set; }
        public string CapaPerson { get; set; }
        public DateTime? CapaTargetDate { get; set; }

        // Optional supporting-document photo(s) from the respondent. Handled
        // entirely locally (saved to disk via CapaAttachmentFileService,
        // same as the original filing's photos) — never actually needed by
        // the API/DB layer, so [JsonIgnore] keeps PublicService from
        // forwarding potentially large base64 image data to the API when it
        // serializes this object for the api/capa/respond call. This only
        // affects that outgoing Newtonsoft serialization — it has no effect
        // on the incoming client → controller model binding, which uses
        // MVC's own JSON binding and is unaffected by this attribute.
        [Newtonsoft.Json.JsonIgnore]
        public List<CapaAttachmentModel> Attachments { get; set; } = new List<CapaAttachmentModel>();
    }

    // Submitted from the CapaVerification page — QA signing off on the
    // respondent's Step 2 (Immediate Action) and Step 4 (Corrective/
    // Preventive Action) submissions, plus the header-level verification/
    // approval sign-off and any per-item stock adjustment. Looked up by
    // ResponseToken, same as SendeeResponseRequest.
    public class VerificationRequest
    {
        public string ResponseToken { get; set; }

        public string IaVerifiedBy { get; set; }
        public DateTime? IaVerifiedDate { get; set; }

        public string CapaVerifiedBy { get; set; }
        public DateTime? CapaVerifiedDate { get; set; }

        public string VerificationNotes { get; set; }

        // The client only knows the logged-in user's username (there's no
        // numeric user id available client-side) — CapaService resolves this
        // to tbl_UserAccount.id and uses it for both VerifiedBy and
        // ApprovedBy, since verification and approval are done by the same
        // person in one step.
        public string VerifiedByUsername { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }

        // Effective -> Status becomes Closed. Not Effective -> Status
        // becomes "Not Effective", which can later be manually reopened.
        public bool IsEffective { get; set; }

        public int StockEventID { get; set; } // 0 = SA OUT, 1 = OUT RIGHT

        public List<ItemVerificationUpdate> Items { get; set; } = new List<ItemVerificationUpdate>();
    }

    // One item's stock-adjustment sign-off, submitted alongside
    // VerificationRequest — updates the matching tbl_QualityIncidentDtl row
    // by (EventID, LineNumber).
    public class ItemVerificationUpdate
    {
        public int LineNumber { get; set; }
        public string Hauler { get; set; }
        public short? AdjustmentQty { get; set; }
        public int? AdjustmentBy { get; set; } // FK -> tbl_UserAccount.id
        public DateTime? AdjustmentDate { get; set; }
    }

}
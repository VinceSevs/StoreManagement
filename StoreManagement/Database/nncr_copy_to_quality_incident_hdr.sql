-- NNCR now lives in dbo.tbl_QualityIncidentHdr (rows with ReportNumber 'NNCR-####'; CARs are 'CAR-#####')
-- instead of dbo.tbl_NonconformanceHdr. No schema change: NNCR fields are stored in CAR columns of a
-- suitable type (see the mapping at the top of Repositories/NncrRepository.cs).
--
-- This script only copies NNCRs already filed in the old table, keeping their ResponseToken so emailed
-- links still work. Run it once per database that has NNCRs in tbl_NonconformanceHdr; safe to run more than once.
-- The old table is left in place; drop it yourself once you have checked the copy.
IF OBJECT_ID(N'dbo.tbl_NonconformanceHdr', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.tbl_QualityIncidentHdr
        (ReportNumber, ResponseToken, Status, IssuedBy, SiteWarehouse,
         CarClassificationType, CarClassificationRefId, CarClassificationRefName, CarClassificationRefEmail,
         CapaActions, ImmediateAction, RootCause, VerificationNotes, EffRemarks,
         ReportDueDate, DateCreated, CorrectionDueDate, ApprovedBy, ApprovedDate,
         VerifiedDate, ExternalAuditSubType, CapaPerson)
    SELECT n.ReportNumber, n.ResponseToken, n.Status, ISNULL(u.username, ''), n.SiteWarehouse,
           'Supplier', CAST(n.IssuedToSupplier AS VARCHAR(100)), ISNULL(v.VendorName, ''), LTRIM(RTRIM(ISNULL(v.EmailAddress, ''))),
           n.NonconformanceType, n.NonconformanceInterception, n.DetailsOfNonconformance, n.QARemarks, n.VendorRemarks,
           n.DateOfIncident, n.DateFiled, n.DueDate, n.ApprovedBy, n.ApprovedDate,
           n.AcknowledgeDate, n.SupplierDecision, n.RespondedEmail
    FROM dbo.tbl_NonconformanceHdr n
    LEFT JOIN dbo.tbl_UserAccount u ON u.id = n.IssuedBy
    LEFT JOIN dbo.tbl_Vendor v ON v.VendorID = n.IssuedToSupplier
    WHERE NOT EXISTS (SELECT 1 FROM dbo.tbl_QualityIncidentHdr q WHERE q.ReportNumber = n.ReportNumber)
    ORDER BY n.EventID;
END
GO

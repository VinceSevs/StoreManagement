-- NNCR statuses: "Responded" is replaced by "Approved" and "Rejected".
-- Converts existing rows by the supplier's decision. Run it after nncr_copy_to_quality_incident_hdr.sql. Safe to run more than once.
-- (The app already shows legacy "Responded" rows by their decision, and can close them,
--  so running this is tidy-up, not a requirement.)
UPDATE dbo.tbl_QualityIncidentHdr
SET Status = CASE ExternalAuditSubType
                 WHEN 'Approve' THEN 'Approved'
                 WHEN 'Reject'  THEN 'Rejected'
             END
WHERE ReportNumber LIKE 'NNCR-%'
  AND Status = 'Responded'
  AND ExternalAuditSubType IN ('Approve', 'Reject');
GO

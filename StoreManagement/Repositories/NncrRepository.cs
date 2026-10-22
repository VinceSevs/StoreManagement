using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using StoreManagement.Models;

namespace StoreManagement.Repositories
{
    // NNCRs are rows of tbl_QualityIncidentHdr whose ReportNumber is NNCR-#### (CARs are CAR-#####).
    // EventID is an IDENTITY column; the NNCR-#### number is assigned here inside a serializable transaction.
    // The table has no NNCR-specific columns, so NNCR fields borrow CAR columns of a suitable type
    // (the CAR meaning of those columns is not used for NNCR rows, which CapaService never reads):
    //   IssuedBy                  = issuer's username (as a CAR)
    //   supplier                  = CarClassificationType 'Supplier' + CarClassificationRefId/RefName/RefEmail
    //   DateFiled / DueDate       = DateCreated / CorrectionDueDate
    //   ClosedBy / ClosedDate     = ApprovedBy / ApprovedDate
    //   NonconformanceType        = CapaActions            (up to 500 chars; NonconformanceType is only varchar(100))
    //   NonconformanceInterception= ImmediateAction        (up to 500 chars)
    //   DetailsOfNonconformance   = RootCause
    //   QARemarks                 = VerificationNotes
    //   VendorRemarks             = EffRemarks
    //   DateOfIncident            = ReportDueDate
    //   SupplierDecision          = ExternalAuditSubType
    //   RespondedEmail            = CapaPerson
    //   AcknowledgeDate           = VerifiedDate           (AcknowledgeBy is the supplier id once responded)
    public class NncrRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        private const string SelectReport = @"
            SELECT h.*,
                   ISNULL(iu.id, 0) AS IssuedByID,
                   h.IssuedBy       AS IssuedByName,
                   ISNULL(TRY_CAST(h.CarClassificationRefId AS INT), 0) AS IssuedToSupplier,
                   v.VendorName    AS SupplierName,
                   LTRIM(RTRIM(ISNULL(v.EmailAddress, ''))) AS SupplierEmail,
                   w.WarehouseName AS SiteWarehouseName,
                   cu.username     AS ClosedByName,
                   h.CapaActions          AS NncrType,
                   h.ImmediateAction      AS NncrInterception,
                   h.RootCause            AS NncrDetails,
                   h.VerificationNotes    AS NncrQaRemarks,
                   h.EffRemarks           AS NncrVendorRemarks,
                   h.ReportDueDate        AS NncrIncidentDate,
                   h.ExternalAuditSubType AS NncrDecision,
                   h.CapaPerson           AS NncrRespondedEmail,
                   h.VerifiedDate         AS NncrRespondedDate
            FROM dbo.tbl_QualityIncidentHdr h
            LEFT JOIN tbl_UserAccount iu ON h.IssuedBy = iu.username
            LEFT JOIN tbl_Vendor v ON TRY_CAST(h.CarClassificationRefId AS INT) = v.VendorID
            LEFT JOIN tbl_Warehouse w ON h.SiteWarehouse = w.WarehouseID
            LEFT JOIN tbl_UserAccount cu ON h.ApprovedBy = cu.id
            WHERE h.ReportNumber LIKE 'NNCR-%'";

        private static string SafeString(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? "" : reader.GetValue(ordinal).ToString();
        }

        private static int SafeInt(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        // Unset dates hold the column default 1900-01-01; surface those as null.
        private static DateTime? SafeDate(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            var value = Convert.ToDateTime(reader.GetValue(ordinal));
            return value.Year <= 1900 ? (DateTime?)null : value;
        }

        // Rows saved before Approved / Rejected existed hold the old "Responded" status: show them by the supplier's decision.
        private static string NormalizeStatus(string status, string decision)
        {
            if (status == "Responded") return NncrStatuses.FromDecision(decision) ?? status;
            return status;
        }

        private static NncrReport Map(SqlDataReader reader)
        {
            var respondedDate = SafeDate(reader, "NncrRespondedDate");
            return new NncrReport
            {
                EventID = SafeInt(reader, "EventID"),
                ReportNumber = SafeString(reader, "ReportNumber"),
                ResponseToken = SafeString(reader, "ResponseToken"),
                Status = NormalizeStatus(SafeString(reader, "Status"), SafeString(reader, "NncrDecision")),
                IssuedBy = SafeInt(reader, "IssuedByID"),
                IssuedByName = SafeString(reader, "IssuedByName"),
                IssuedToSupplier = SafeInt(reader, "IssuedToSupplier"),
                SupplierName = SafeString(reader, "SupplierName"),
                SupplierEmail = SafeString(reader, "SupplierEmail"),
                SiteWarehouse = SafeInt(reader, "SiteWarehouse"),
                SiteWarehouseName = SafeString(reader, "SiteWarehouseName"),
                NonconformanceType = SafeString(reader, "NncrType"),
                NonconformanceInterception = SafeString(reader, "NncrInterception"),
                DetailsOfNonconformance = SafeString(reader, "NncrDetails"),
                QARemarks = SafeString(reader, "NncrQaRemarks"),
                DateOfIncident = SafeDate(reader, "NncrIncidentDate"),
                DateFiled = SafeDate(reader, "DateCreated"),
                DueDate = SafeDate(reader, "CorrectionDueDate"),
                SupplierDecision = SafeString(reader, "NncrDecision"),
                VendorRemarks = SafeString(reader, "NncrVendorRemarks"),
                RespondedEmail = SafeString(reader, "NncrRespondedEmail"),
                // Only the addressed supplier can respond, so the responder is the supplier.
                AcknowledgeBy = respondedDate.HasValue ? SafeInt(reader, "IssuedToSupplier") : 0,
                AcknowledgeDate = respondedDate,
                // The QA supervisor's (role 49) closing is stored as the NNCR's approval.
                ClosedBy = SafeInt(reader, "ApprovedBy"),
                ClosedByName = SafeString(reader, "ClosedByName"),
                ClosedDate = SafeDate(reader, "ApprovedDate")
            };
        }

        public async Task<List<NncrReport>> GetAll()
        {
            var list = new List<NncrReport>();
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(SelectReport + " ORDER BY h.EventID DESC", conn))
            {
                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) list.Add(Map(reader));
                }
            }
            return list;
        }

        public Task<NncrReport> GetByToken(string token)
        {
            return GetSingle(" AND h.ResponseToken = @Value", token);
        }

        public Task<NncrReport> GetByReportNumber(string reportNumber)
        {
            return GetSingle(" AND h.ReportNumber = @Value", reportNumber);
        }

        private async Task<NncrReport> GetSingle(string where, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(SelectReport + where, conn))
            {
                cmd.Parameters.AddWithValue("@Value", value);
                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    return await reader.ReadAsync() ? Map(reader) : null;
                }
            }
        }

        private static string FormatReportNumber(int series)
        {
            return "NNCR-" + series.ToString("0000");
        }

        private static async Task<int> NextSeries(SqlConnection conn, SqlTransaction tx)
        {
            const string sql = @"
                SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(ReportNumber, 6, 20) AS INT)), 0) + 1
                FROM dbo.tbl_QualityIncidentHdr WITH (UPDLOCK, HOLDLOCK)
                WHERE ReportNumber LIKE 'NNCR-%'";
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
        }

        // Inserts a new NNCR as Awaiting Response, due 7 working days after filing.
        // Returns the saved row, or null with a message when the generated
        // ReportNumber already exists (duplicate guard).
        public async Task<(NncrReport report, string error)> Insert(NncrFileRequest request, int issuedBy)
        {
            DateTime filed = DateTime.Now;
            DateTime due = NncrDueDates.AddWorkingDays(filed, NncrDueDates.WorkingDaysToRespond);

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var tx = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    string reportNumber = FormatReportNumber(await NextSeries(conn, tx));

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(1) FROM dbo.tbl_QualityIncidentHdr WHERE ReportNumber = @ReportNumber", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@ReportNumber", reportNumber);
                        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0)
                        {
                            tx.Rollback();
                            return (null, $"{reportNumber} already exists. Please submit again.");
                        }
                    }

                    string token = Guid.NewGuid().ToString("N");
                    int eventId;
                    // The not-yet-known columns get the "unset" values (0 / '' / 1900-01-01) instead of
                    // relying on column defaults. IssuedBy holds the issuer's username, like a CAR.
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.tbl_QualityIncidentHdr
                            (ReportNumber, ResponseToken, Status, IssuedBy, IssuedToDepartment, SiteWarehouse,
                             CarClassificationType, CarClassificationRefId, CarClassificationRefName, CarClassificationRefEmail,
                             CapaActions, ImmediateAction, RootCause, VerificationNotes,
                             EffRemarks, ReportDueDate, DateCreated, CorrectionDueDate,
                             ApprovedBy, ApprovedDate, VerifiedDate, ExternalAuditSubType, CapaPerson)
                        OUTPUT INSERTED.EventID
                        SELECT
                             @ReportNumber, @ResponseToken, @Status,
                             ISNULL((SELECT username FROM dbo.tbl_UserAccount WHERE id = @IssuedBy), ''), 0, @SiteWarehouse,
                             'Supplier', CAST(@IssuedToSupplier AS VARCHAR(100)),
                             ISNULL((SELECT VendorName FROM dbo.tbl_Vendor WHERE VendorID = @IssuedToSupplier), ''),
                             ISNULL((SELECT LTRIM(RTRIM(ISNULL(EmailAddress, ''))) FROM dbo.tbl_Vendor WHERE VendorID = @IssuedToSupplier), ''),
                             @NonconformanceType, @NonconformanceInterception, @DetailsOfNonconformance, @QARemarks,
                             '', ISNULL(@DateOfIncident, GETDATE()), @DateFiled, @DueDate,
                             0, '1900-01-01', '1900-01-01', '', ''", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@ReportNumber", reportNumber);
                        cmd.Parameters.AddWithValue("@ResponseToken", token);
                        cmd.Parameters.AddWithValue("@Status", NncrStatuses.AwaitingResponse);
                        cmd.Parameters.AddWithValue("@IssuedBy", issuedBy);
                        cmd.Parameters.AddWithValue("@IssuedToSupplier", request.IssuedToSupplier);
                        cmd.Parameters.AddWithValue("@SiteWarehouse", request.SiteWarehouse);
                        cmd.Parameters.AddWithValue("@NonconformanceType", request.NonconformanceType ?? "");
                        cmd.Parameters.AddWithValue("@NonconformanceInterception", request.NonconformanceInterception ?? "");
                        cmd.Parameters.AddWithValue("@DetailsOfNonconformance", request.DetailsOfNonconformance ?? "");
                        cmd.Parameters.AddWithValue("@QARemarks", request.QARemarks ?? "");
                        cmd.Parameters.Add("@DateOfIncident", SqlDbType.DateTime).Value =
                            (object)request.DateOfIncident ?? DBNull.Value;
                        cmd.Parameters.Add("@DateFiled", SqlDbType.DateTime).Value = filed;
                        cmd.Parameters.Add("@DueDate", SqlDbType.DateTime).Value = due;
                        eventId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    tx.Commit();
                    return (new NncrReport
                    {
                        EventID = eventId,
                        ReportNumber = reportNumber,
                        ResponseToken = token,
                        Status = NncrStatuses.AwaitingResponse,
                        DateFiled = filed,
                        DueDate = due
                    }, null);
                }
            }
        }

        // The responding supplier's VendorID, read from tbl_Vendor through the NNCR's IssuedToSupplier.
        // Null when the NNCR has no supplier or the supplier is not in tbl_Vendor: such a response is not accepted.
        public async Task<int?> GetSupplierVendorId(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            const string sql = @"
                SELECT v.VendorID
                FROM dbo.tbl_QualityIncidentHdr h
                INNER JOIN dbo.tbl_Vendor v ON v.VendorID = TRY_CAST(h.CarClassificationRefId AS INT)
                WHERE h.ResponseToken = @Token AND h.ReportNumber LIKE 'NNCR-%'";
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value) return null;
                int id = Convert.ToInt32(result);
                return id > 0 ? id : (int?)null;
            }
        }

        // Awaiting Response -> Approved / Rejected (by the supplier's decision). Returns false if the NNCR is no longer awaiting a response.
        public async Task<bool> SubmitSupplierResponse(string token, string decision, string vendorRemarks, int vendorId, string respondedEmail)
        {
            const string sql = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET Status = @NewStatus, ExternalAuditSubType = @Decision, EffRemarks = @VendorRemarks,
                    CapaPerson = @RespondedEmail, VerifiedDate = GETDATE()
                WHERE ResponseToken = @Token AND ReportNumber LIKE 'NNCR-%' AND Status = @AwaitingResponse
                  AND TRY_CAST(CarClassificationRefId AS INT) = @VendorID";
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@NewStatus", NncrStatuses.FromDecision(decision) ?? NncrStatuses.Approved);
                cmd.Parameters.AddWithValue("@AwaitingResponse", NncrStatuses.AwaitingResponse);
                cmd.Parameters.AddWithValue("@Decision", decision ?? "");
                cmd.Parameters.AddWithValue("@VendorRemarks", vendorRemarks ?? "");
                cmd.Parameters.AddWithValue("@RespondedEmail", respondedEmail ?? "");
                cmd.Parameters.AddWithValue("@VendorID", vendorId);
                cmd.Parameters.AddWithValue("@Token", token);
                await conn.OpenAsync();
                return await cmd.ExecuteNonQueryAsync() == 1;
            }
        }

        // Approved / Rejected -> Closed by the QA supervisor (role 49), recorded in ApprovedBy / ApprovedDate.
        // Returns false if the supplier has not responded yet (or it is already closed).
        public async Task<bool> Close(string token, int closedBy)
        {
            const string sql = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET Status = @Closed, ApprovedBy = @ClosedBy, ApprovedDate = GETDATE()
                WHERE ResponseToken = @Token AND ReportNumber LIKE 'NNCR-%' AND Status IN (@Approved, @Rejected, 'Responded')";
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Closed", NncrStatuses.Closed);
                cmd.Parameters.AddWithValue("@Approved", NncrStatuses.Approved);
                cmd.Parameters.AddWithValue("@Rejected", NncrStatuses.Rejected);
                cmd.Parameters.AddWithValue("@ClosedBy", closedBy);
                cmd.Parameters.AddWithValue("@Token", token);
                await conn.OpenAsync();
                return await cmd.ExecuteNonQueryAsync() == 1;
            }
        }
    }
}

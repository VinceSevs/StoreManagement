using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using StoreManagement.Models;

namespace StoreManagement.Repositories
{
    // tbl_NonconformanceHdr (NNCR). EventID is not an IDENTITY column, so the next EventID and
    // NNCR-#### number are assigned here inside a serializable transaction.
    public class NncrRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        private const string SelectReport = @"
            SELECT h.*,
                   iu.username     AS IssuedByName,
                   v.VendorName    AS SupplierName,
                   LTRIM(RTRIM(ISNULL(v.EmailAddress, ''))) AS SupplierEmail,
                   w.WarehouseName AS SiteWarehouseName,
                   cu.username     AS ClosedByName
            FROM dbo.tbl_NonconformanceHdr h
            LEFT JOIN tbl_UserAccount iu ON h.IssuedBy = iu.id
            LEFT JOIN tbl_Vendor v ON h.IssuedToSupplier = v.VendorID
            LEFT JOIN tbl_Warehouse w ON h.SiteWarehouse = w.WarehouseID
            LEFT JOIN tbl_UserAccount cu ON h.ApprovedBy = cu.id";

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

        private static NncrReport Map(SqlDataReader reader)
        {
            return new NncrReport
            {
                EventID = SafeInt(reader, "EventID"),
                ReportNumber = SafeString(reader, "ReportNumber"),
                ResponseToken = SafeString(reader, "ResponseToken"),
                Status = SafeString(reader, "Status"),
                IssuedBy = SafeInt(reader, "IssuedBy"),
                IssuedByName = SafeString(reader, "IssuedByName"),
                IssuedToSupplier = SafeInt(reader, "IssuedToSupplier"),
                SupplierName = SafeString(reader, "SupplierName"),
                SupplierEmail = SafeString(reader, "SupplierEmail"),
                SiteWarehouse = SafeInt(reader, "SiteWarehouse"),
                SiteWarehouseName = SafeString(reader, "SiteWarehouseName"),
                NonconformanceType = SafeString(reader, "NonconformanceType"),
                NonconformanceInterception = SafeString(reader, "NonconformanceInterception"),
                DetailsOfNonconformance = SafeString(reader, "DetailsOfNonconformance"),
                QARemarks = SafeString(reader, "QARemarks"),
                DateOfIncident = SafeDate(reader, "DateOfIncident"),
                DateFiled = SafeDate(reader, "DateFiled"),
                DueDate = SafeDate(reader, "DueDate"),
                SupplierDecision = SafeString(reader, "SupplierDecision"),
                VendorRemarks = SafeString(reader, "VendorRemarks"),
                RespondedEmail = SafeString(reader, "RespondedEmail"),
                AcknowledgeBy = SafeInt(reader, "AcknowledgeBy"),
                AcknowledgeDate = SafeDate(reader, "AcknowledgeDate"),
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
            return GetSingle(" WHERE h.ResponseToken = @Value", token);
        }

        public Task<NncrReport> GetByReportNumber(string reportNumber)
        {
            return GetSingle(" WHERE h.ReportNumber = @Value", reportNumber);
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
                FROM dbo.tbl_NonconformanceHdr WITH (UPDLOCK, HOLDLOCK)
                WHERE ReportNumber LIKE 'NNCR-%'";
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
        }

        // Inserts a new NNCR as Awaiting Response, due 7 working days after filing.
        // Returns the saved row, or null with a message when the generated EventID /
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
                    int eventId;
                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(MAX(EventID), 0) + 1 FROM dbo.tbl_NonconformanceHdr WITH (UPDLOCK, HOLDLOCK)", conn, tx))
                    {
                        eventId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                    string reportNumber = FormatReportNumber(await NextSeries(conn, tx));

                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(1) FROM dbo.tbl_NonconformanceHdr
                        WHERE EventID = @EventID OR ReportNumber = @ReportNumber", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@EventID", eventId);
                        cmd.Parameters.AddWithValue("@ReportNumber", reportNumber);
                        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0)
                        {
                            tx.Rollback();
                            return (null, $"{reportNumber} (EventID {eventId}) already exists. Please submit again.");
                        }
                    }

                    string token = Guid.NewGuid().ToString("N");
                    // Every column is NOT NULL; the not-yet-known ones get the "unset" values
                    // (0 / '' / 1900-01-01) instead of relying on column defaults.
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.tbl_NonconformanceHdr
                            (EventID, ReportNumber, ResponseToken, Status, IssuedBy, IssuedToSupplier, SiteWarehouse,
                             NonconformanceType, NonconformanceInterception, DetailsOfNonconformance, QARemarks,
                             VendorRemarks, DateOfIncident, DateFiled, DueDate,
                             ApprovedBy, ApprovedDate, AcknowledgeBy, AcknowledgeDate, SupplierDecision, RespondedEmail)
                        VALUES
                            (@EventID, @ReportNumber, @ResponseToken, @Status, @IssuedBy, @IssuedToSupplier, @SiteWarehouse,
                             @NonconformanceType, @NonconformanceInterception, @DetailsOfNonconformance, @QARemarks,
                             '', ISNULL(@DateOfIncident, GETDATE()), @DateFiled, @DueDate,
                             0, '1900-01-01', 0, '1900-01-01', '', '')", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@EventID", eventId);
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
                        await cmd.ExecuteNonQueryAsync();
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

        // Awaiting Response -> Responded. Returns false if the NNCR is no longer awaiting a response.
        public async Task<bool> SubmitSupplierResponse(string token, string decision, string vendorRemarks, int vendorId, string respondedEmail)
        {
            const string sql = @"
                UPDATE dbo.tbl_NonconformanceHdr
                SET Status = @Responded, SupplierDecision = @Decision, VendorRemarks = @VendorRemarks,
                    RespondedEmail = @RespondedEmail, AcknowledgeBy = @VendorID, AcknowledgeDate = GETDATE()
                WHERE ResponseToken = @Token AND Status = @AwaitingResponse AND IssuedToSupplier = @VendorID";
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Responded", NncrStatuses.Responded);
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

        // Responded -> Closed by the QA supervisor (role 49), recorded in ApprovedBy / ApprovedDate.
        // Returns false if the supplier has not responded yet (or it is already closed).
        public async Task<bool> Close(string token, int closedBy)
        {
            const string sql = @"
                UPDATE dbo.tbl_NonconformanceHdr
                SET Status = @Closed, ApprovedBy = @ClosedBy, ApprovedDate = GETDATE()
                WHERE ResponseToken = @Token AND Status = @Responded";
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Closed", NncrStatuses.Closed);
                cmd.Parameters.AddWithValue("@Responded", NncrStatuses.Responded);
                cmd.Parameters.AddWithValue("@ClosedBy", closedBy);
                cmd.Parameters.AddWithValue("@Token", token);
                await conn.OpenAsync();
                return await cmd.ExecuteNonQueryAsync() == 1;
            }
        }
    }
}

using IsseERP.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class CapaService : BaseApiClient
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        private readonly HttpClient _httpClient;

        public CapaService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_apiBaseUrl)
            };
        }

        public async Task<string> GetNextCapaNo()
        {
            const string query = @"
                SELECT
                    'CAR-' + RIGHT('00000' + CAST(
                        ISNULL(MAX(CAST(SUBSTRING(ReportNumber, 5, LEN(ReportNumber) - 4) AS INT)), 0) + 1
                    AS VARCHAR(10)), 5) AS NextCapaNo
                FROM dbo.tbl_QualityIncidentHdr
                WHERE ReportNumber LIKE 'CAR-%'
                  AND ISNUMERIC(SUBSTRING(ReportNumber, 5, LEN(ReportNumber) - 4)) = 1;
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync() && reader["NextCapaNo"] != DBNull.Value)
                    {
                        return reader["NextCapaNo"].ToString();
                    }
                }
            }
            return "CAR-00001";
        }

        public async Task<List<Department>> _GetDepartment()
        {
            List<Department> departments = new List<Department>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT DepartmentID, DepartmentDescription
                    FROM tbl_Department
                    ORDER BY DepartmentID ASC
                ";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Department department = new Department
                            {
                                DepartmentID = reader.GetInt32(reader.GetOrdinal("DepartmentID")),
                                DepartmentDescription = reader.GetString(reader.GetOrdinal("DepartmentDescription"))
                            };
                            departments.Add(department);
                        }
                    }
                }
            }
            return departments;
        }

        public async Task<bool> InsertCapaReport(CapaReportModel model)
        {
            string responseToken = Guid.NewGuid().ToString("N");

            const string hdrQuery = @"
                INSERT INTO dbo.tbl_QualityIncidentHdr
                (ResponseToken, IssuedBy, ReportNumber, IssuedToDepartment, SiteWarehouse, RefNo, CorrectionDueDate, ReportDueDate, Status,
                 NonconformanceType, ExternalAuditSubType, CarClassificationType, CarClassificationRefId, CarClassificationRefName, CarClassificationRefEmail, CarClassificationOtherText,
                 ImmediateAction, IaPerson, IaTargetDate, IaVerifiedBy, IaVerifiedDate,
                 RootCause, CapaActions, CapaPerson, CapaTargetDate, CapaVerifiedBy, CapaVerifiedDate,
                 VerificationNotes, EffEffective, EffNotEffective, EffRemarks, VerifiedBy, VerifiedDate, ApprovedBy, ApprovedDate,
                 StockEventID, DateCreated)
                OUTPUT INSERTED.EventID
                VALUES
                (@ResponseToken, @IssuedBy, @ReportNumber, @IssuedToDepartment, @SiteWarehouse, @RefNo, @CorrectionDueDate, @ReportDueDate, @Status,
                 @NonconformanceType, @ExternalAuditSubType, @CarClassificationType, @CarClassificationRefId, @CarClassificationRefName, @CarClassificationRefEmail, @CarClassificationOtherText,
                 @ImmediateAction, @IaPerson, @IaTargetDate, @IaVerifiedBy, @IaVerifiedDate,
                 @RootCause, @CapaActions, @CapaPerson, @CapaTargetDate, @CapaVerifiedBy, @CapaVerifiedDate,
                 @VerificationNotes, @EffEffective, @EffNotEffective, @EffRemarks, @VerifiedBy, @VerifiedDate, @ApprovedBy, @ApprovedDate,
                 @StockEventID, SYSDATETIME())
            ";

            const string dtlQuery = @"
                INSERT INTO dbo.tbl_QualityIncidentDtl
                (EventID, LineNumber, ItemID, Quantity, UOMID, DefectCategoryID, UsedToDate, Remarks, Hauler, AdjustmentQty, AdjustmentBy, AdjustmentDate)
                VALUES
                (@EventID, @LineNumber, @ItemID, @Quantity, @UOMID, @DefectCategoryID, @UsedToDate, @Remarks, @Hauler, @AdjustmentQty, @AdjustmentBy, @AdjustmentDate)
            ";

            var items = (model.Items != null && model.Items.Count > 0)
                ? model.Items
                : new List<CapaItemModel> { new CapaItemModel { LineNumber = 1 } };

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        int eventId;
                        using (SqlCommand cmd = new SqlCommand(hdrQuery, conn, tx))
                        {
                            AddHdrParameters(cmd, model, responseToken);
                            eventId = (int)await cmd.ExecuteScalarAsync();
                        }

                        foreach (var item in items)
                        {
                            using (SqlCommand cmd = new SqlCommand(dtlQuery, conn, tx))
                            {
                                AddDtlParameters(cmd, eventId, item);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        tx.Commit();

                        try
                        {
                            model.ResponseToken = responseToken;
                            var json = JsonConvert.SerializeObject(model);
                            var content = new StringContent(json, Encoding.UTF8, "application/json");
                            await _httpClient.PostAsync("api/capa/respond-again", content);
                        }
                        catch
                        {
                            // Swallow — the filing itself already succeeded.
                        }

                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        return false;
                    }
                }
            }
        }

        //public async Task<bool> LoopCapaReport(CapaReportModel model)
        //{
        //    try
        //    {
        //        var json = JsonConvert.SerializeObject(model);
        //        var content = new StringContent(json, Encoding.UTF8, "application/json");

        //        var response = await _httpClient.PostAsync("api/capa/respond-again", content);

        //        return response.IsSuccessStatusCode;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        //public async Task<bool> InsertCapaReport(CapaReportModel model)
        //{
        //    try
        //    {
        //        var json = JsonConvert.SerializeObject(model);
        //        var content = new StringContent(json, Encoding.UTF8, "application/json");

        //        var response = await _httpClient.PostAsync("api/capa/insert", content);

        //        return response.IsSuccessStatusCode;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        public async Task<List<CapaReportModel>> GetAllCapaReports()
        {
            var reports = new List<CapaReportModel>();
            var reportsById = new Dictionary<int, CapaReportModel>();

            const string hdrQuery = @"
                SELECT h.*,
                       d.DepartmentDescription AS IssuedToDepartmentName,
                       w.WarehouseName AS SiteWarehouseName,
                       uv.username AS VerifiedByName,
                       ua.username AS ApprovedByName
                FROM dbo.tbl_QualityIncidentHdr h
                LEFT JOIN tbl_Department d ON h.IssuedToDepartment = d.DepartmentID
                LEFT JOIN tbl_Warehouse w ON h.SiteWarehouse = w.WarehouseID
                LEFT JOIN tbl_UserAccount uv ON h.VerifiedBy = uv.id
                LEFT JOIN tbl_UserAccount ua ON h.ApprovedBy = ua.id
                ORDER BY h.EventID DESC";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(hdrQuery, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int eventId = (int)reader["EventID"];
                        var report = MapHdrReader(reader, eventId);
                        reports.Add(report);
                        reportsById[eventId] = report;
                    }
                }
            }

            var itemsById = await GetItemsGroupedByEventId(reportsById.Keys);
            foreach (var kvp in reportsById)
            {
                kvp.Value.Items = itemsById.TryGetValue(kvp.Key, out var items) ? items : new List<CapaItemModel>();
            }

            return reports;
        }

        public async Task<CapaReportModel> GetCapaByToken(string token)
        {
            const string hdrQuery = @"
                SELECT h.*,
                       d.DepartmentDescription AS IssuedToDepartmentName,
                       w.WarehouseName AS SiteWarehouseName,
                       uv.username AS VerifiedByName,
                       ua.username AS ApprovedByName
                FROM dbo.tbl_QualityIncidentHdr h
                LEFT JOIN tbl_Department d ON h.IssuedToDepartment = d.DepartmentID
                LEFT JOIN tbl_Warehouse w ON h.SiteWarehouse = w.WarehouseID
                LEFT JOIN tbl_UserAccount uv ON h.VerifiedBy = uv.id
                LEFT JOIN tbl_UserAccount ua ON h.ApprovedBy = ua.id
                WHERE h.ResponseToken = @ResponseToken";

            CapaReportModel report = null;
            int eventId = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(hdrQuery, conn))
            {
                cmd.Parameters.AddWithValue("@ResponseToken", token);
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        eventId = (int)reader["EventID"];
                        report = MapHdrReader(reader, eventId);
                    }
                }
            }

            if (report == null) return null;

            var itemsById = await GetItemsGroupedByEventId(new[] { eventId });
            report.Items = itemsById.TryGetValue(eventId, out var items) ? items : new List<CapaItemModel>();

            return report;
        }

        private async Task<Dictionary<int, List<CapaItemModel>>> GetItemsGroupedByEventId(IEnumerable<int> eventIds)
        {
            var result = new Dictionary<int, List<CapaItemModel>>();
            var idList = eventIds.ToList();
            if (idList.Count == 0) return result;

            string inClause = string.Join(",", idList);
            string dtlQuery = $@"
                SELECT dt.*,
                       it.item_desc AS ItemDescription,
                       it.item_code AS ItemCode,
                       u.Text AS UOMText,
                       u.ISO_Code AS UOMIso,
                       ab.username AS AdjustmentByName
                FROM dbo.tbl_QualityIncidentDtl dt
                LEFT JOIN tbl_Item it ON dt.ItemID = it.id
                LEFT JOIN tbl_UOM u ON dt.UOMID = u.id
                LEFT JOIN tbl_UserAccount ab ON dt.AdjustmentBy = ab.id
                WHERE dt.EventID IN ({inClause})
                ORDER BY dt.EventID, dt.LineNumber";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(dtlQuery, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int eventId = (int)reader["EventID"];

                        short? defectCategoryId = reader["DefectCategoryID"] as short?;
                        var defectEntry = defectCategoryId.HasValue
                            ? DefectCatalogData.Items.FirstOrDefault(d => d.Id == defectCategoryId.Value)
                            : null;

                        string uomText = reader["UOMText"] as string;
                        string uomIso = reader["UOMIso"] as string;

                        var item = new CapaItemModel
                        {
                            LineNumber = (int)reader["LineNumber"],
                            ItemID = reader["ItemID"] as int?,
                            ItemCode = reader["ItemCode"] as string,
                            ItemDescription = reader["ItemDescription"] as string,
                            Quantity = reader["Quantity"] as short?,
                            UOMID = reader["UOMID"] as short?,
                            UOMDisplay = !string.IsNullOrEmpty(uomText) ? uomText + (string.IsNullOrEmpty(uomIso) ? "" : " - " + uomIso) : null,
                            DefectCategoryID = defectCategoryId,
                            DefectName = defectEntry?.Name,
                            DefectCategoryName = defectEntry?.Category,
                            UsedToDate = reader["UsedToDate"] as DateTime?,
                            Remarks = reader["Remarks"] as string,
                            Hauler = reader["Hauler"] as string,
                            AdjustmentQty = reader["AdjustmentQty"] as short?,
                            AdjustmentBy = reader["AdjustmentBy"] as int?,
                            AdjustmentByName = reader["AdjustmentByName"] as string,
                            AdjustmentDate = reader["AdjustmentDate"] as DateTime?
                        };

                        if (!result.TryGetValue(eventId, out var list))
                        {
                            list = new List<CapaItemModel>();
                            result[eventId] = list;
                        }
                        list.Add(item);
                    }
                }
            }
            return result;
        }

        public async Task<bool> SubmitSendeeResponse(SendeeResponseRequest request)
        {
            const string query = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET ImmediateAction = @ImmediateAction,
                    IaPerson = @IaPerson,
                    IaTargetDate = @IaTargetDate,
                    RootCause = @RootCause,
                    CapaActions = @CapaActions,
                    CapaPerson = @CapaPerson,
                    CapaTargetDate = @CapaTargetDate
                WHERE ResponseToken = @ResponseToken
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ImmediateAction", (object)request.ImmediateAction ?? "");
                cmd.Parameters.AddWithValue("@IaPerson", (object)request.IaPerson ?? "");
                cmd.Parameters.AddWithValue("@IaTargetDate", (object)request.IaTargetDate ?? "1900-01-01");
                cmd.Parameters.AddWithValue("@RootCause", (object)request.RootCause ?? "");
                cmd.Parameters.AddWithValue("@CapaActions", (object)request.CapaActions ?? "");
                cmd.Parameters.AddWithValue("@CapaPerson", (object)request.CapaPerson ?? "");
                cmd.Parameters.AddWithValue("@CapaTargetDate", (object)request.CapaTargetDate ?? "1900-01-01");
                cmd.Parameters.AddWithValue("@ResponseToken", request.ResponseToken);

                await conn.OpenAsync();
                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }
        public async Task<bool> CancelRequest(string token)
        {
            const string query = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET Status = 'Cancelled'
                WHERE ResponseToken = @ResponseToken
                  AND (ImmediateAction IS NULL OR ImmediateAction = '')
                  AND (Status IS NULL OR Status NOT IN ('Closed', 'Cancelled'))
            ";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ResponseToken", token);
                await conn.OpenAsync();
                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        public async Task<bool> SubmitVerification(VerificationRequest request)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                if (!request.IsEffective)
                {
                    return await SubmitNotEffectiveLoop(conn, request.ResponseToken);
                }

                const string hdrQuery = @"
                    UPDATE dbo.tbl_QualityIncidentHdr
                    SET IaVerifiedBy = @IaVerifiedBy,
                        IaVerifiedDate = @IaVerifiedDate,
                        CapaVerifiedBy = @CapaVerifiedBy,
                        CapaVerifiedDate = @CapaVerifiedDate,
                        VerificationNotes = @VerificationNotes,
                        VerifiedBy = @VerifiedBy,
                        VerifiedDate = @VerifiedDate,
                        ApprovedBy = @ApprovedBy,
                        ApprovedDate = @ApprovedDate,
                        StockEventID = @StockEventID,
                        Status = 'Closed'
                    OUTPUT INSERTED.EventID
                    WHERE ResponseToken = @ResponseToken
                ";

                const string dtlQuery = @"
                    UPDATE dbo.tbl_QualityIncidentDtl
                    SET Hauler = @Hauler,
                        AdjustmentQty = @AdjustmentQty,
                        AdjustmentBy = @AdjustmentBy,
                        AdjustmentDate = @AdjustmentDate
                    WHERE EventID = @EventID AND LineNumber = @LineNumber
                ";
                int? verifiedUserId = null;
                if (!string.IsNullOrWhiteSpace(request.VerifiedByUsername))
                {
                    using (SqlCommand lookupCmd = new SqlCommand("SELECT id FROM tbl_UserAccount WHERE username = @Username", conn))
                    {
                        lookupCmd.Parameters.AddWithValue("@Username", request.VerifiedByUsername);
                        object lookupResult = await lookupCmd.ExecuteScalarAsync();
                        if (lookupResult != null) verifiedUserId = Convert.ToInt32(lookupResult);
                    }
                }
                if (!verifiedUserId.HasValue) return false;

                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        int eventId;
                        using (SqlCommand cmd = new SqlCommand(hdrQuery, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@IaVerifiedBy", (object)request.IaVerifiedBy ?? "");
                            cmd.Parameters.AddWithValue("@IaVerifiedDate", (object)request.IaVerifiedDate ?? "1900-01-01");
                            cmd.Parameters.AddWithValue("@CapaVerifiedBy", (object)request.CapaVerifiedBy ?? "");
                            cmd.Parameters.AddWithValue("@CapaVerifiedDate", (object)request.CapaVerifiedDate ?? "1900-01-01");
                            cmd.Parameters.AddWithValue("@VerificationNotes", (object)request.VerificationNotes ?? "");
                            cmd.Parameters.AddWithValue("@VerifiedBy", verifiedUserId.Value);
                            cmd.Parameters.AddWithValue("@VerifiedDate", (object)request.VerifiedDate ?? "1900-01-01");
                            cmd.Parameters.AddWithValue("@ApprovedBy", verifiedUserId.Value);
                            cmd.Parameters.AddWithValue("@ApprovedDate", (object)request.ApprovedDate ?? "1900-01-01");
                            cmd.Parameters.AddWithValue("@StockEventID", request.StockEventID);
                            cmd.Parameters.AddWithValue("@ResponseToken", request.ResponseToken);

                            object result = await cmd.ExecuteScalarAsync();
                            if (result == null)
                            {
                                tx.Rollback();
                                return false;
                            }
                            eventId = (int)result;
                        }

                        if (request.Items != null)
                        {
                            foreach (var item in request.Items)
                            {
                                using (SqlCommand cmd = new SqlCommand(dtlQuery, conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Hauler", (object)item.Hauler ?? "");
                                    cmd.Parameters.AddWithValue("@AdjustmentQty", (object)item.AdjustmentQty ?? 0);
                                    cmd.Parameters.AddWithValue("@AdjustmentBy", (object)item.AdjustmentBy ?? 0);
                                    cmd.Parameters.AddWithValue("@AdjustmentDate", (object)item.AdjustmentDate ?? "1900-01-01");
                                    cmd.Parameters.AddWithValue("@EventID", eventId);
                                    cmd.Parameters.AddWithValue("@LineNumber", item.LineNumber);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        tx.Commit();

                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        return false;
                    }
                }
            }
        }

        private async Task<bool> SubmitNotEffectiveLoop(SqlConnection conn, string token)
        {
            const string query = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET Status = '',
                    loopNumber = loopNumber + 1,
                    ImmediateAction = '',
                    IaPerson = '',
                    IaTargetDate = '1900-01-01',
                    RootCause = '',
                    CapaActions = '',
                    CapaPerson = '',
                    CapaTargetDate = '1900-01-01',
                    IaVerifiedBy = '',
                    IaVerifiedDate = '1900-01-01',
                    CapaVerifiedBy = '',
                    CapaVerifiedDate = '1900-01-01',
                    VerificationNotes = '',
                    VerifiedBy = 0,
                    VerifiedDate = '1900-01-01',
                    ApprovedBy = 0,
                    ApprovedDate = '1900-01-01'
                WHERE ResponseToken = @ResponseToken
            ";

            bool reset;
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ResponseToken", token);
                int rows = await cmd.ExecuteNonQueryAsync();
                reset = rows > 0;
            }

            if (reset)
            {
                try
                {
                    var capa = await GetCapaByToken(token);
                    if (capa != null)
                    {
                        var json = JsonConvert.SerializeObject(capa);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync("api/capa/respond-again", content);
                    }
                }
                catch
                {
                    // Swallow — the reset itself already succeeded.
                }
            }

            return reset;
        }

        public async Task<bool> CloseIfOverdue(string token)
        {
            const string query = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET Status = 'Closed'
                WHERE ResponseToken = @ResponseToken
                  AND CAST(CorrectionDueDate AS DATE) < CAST(SYSDATETIME() AS DATE)
                  AND (ImmediateAction IS NULL OR ImmediateAction = '')
                  AND (Status IS NULL OR Status NOT IN ('Closed', 'Cancelled'))
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ResponseToken", token);
                await conn.OpenAsync();
                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        public async Task CloseAllOverdue()
        {
            const string query = @"
                UPDATE dbo.tbl_QualityIncidentHdr
                SET Status = 'Closed'
                WHERE CAST(CorrectionDueDate AS DATE) < CAST(SYSDATETIME() AS DATE)
                  AND (ImmediateAction IS NULL OR ImmediateAction = '')
                  AND (Status IS NULL OR Status NOT IN ('Closed', 'Cancelled'))
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static void AddHdrParameters(SqlCommand cmd, CapaReportModel model, string responseToken)
        {
            cmd.Parameters.AddWithValue("@ResponseToken", responseToken);
            cmd.Parameters.AddWithValue("@IssuedBy", (object)model.IssuedBy ?? "");
            cmd.Parameters.AddWithValue("@ReportNumber", (object)model.ReportNumber ?? "");
            cmd.Parameters.AddWithValue("@IssuedToDepartment", model.IssuedToDepartment);
            cmd.Parameters.AddWithValue("@SiteWarehouse", model.SiteWarehouse);
            cmd.Parameters.AddWithValue("@RefNo", (object)model.RefNo ?? "");
            cmd.Parameters.AddWithValue("@CorrectionDueDate", (object)model.CorrectionDueDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@ReportDueDate", (object)model.ReportDueDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@Status", (object)model.Status ?? "Filed");
            cmd.Parameters.AddWithValue("@NonconformanceType", (object)model.NonconformanceType ?? "");
            cmd.Parameters.AddWithValue("@ExternalAuditSubType", (object)model.ExternalAuditSubType ?? "");
            cmd.Parameters.AddWithValue("@CarClassificationType", (object)model.CarClassificationType ?? "");
            cmd.Parameters.AddWithValue("@CarClassificationRefId", (object)model.CarClassificationRefId ?? "");
            cmd.Parameters.AddWithValue("@CarClassificationRefName", (object)model.CarClassificationRefName ?? "");
            cmd.Parameters.AddWithValue("@CarClassificationRefEmail", (object)model.CarClassificationRefEmail ?? "");
            cmd.Parameters.AddWithValue("@CarClassificationOtherText", (object)model.CarClassificationOtherText ?? "");
            cmd.Parameters.AddWithValue("@ImmediateAction", (object)model.ImmediateAction ?? "");
            cmd.Parameters.AddWithValue("@IaPerson", (object)model.IaPerson ?? "");
            cmd.Parameters.AddWithValue("@IaTargetDate", (object)model.IaTargetDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@IaVerifiedBy", (object)model.IaVerifiedBy ?? "");
            cmd.Parameters.AddWithValue("@IaVerifiedDate", (object)model.IaVerifiedDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@RootCause", (object)model.RootCause ?? "");
            cmd.Parameters.AddWithValue("@CapaActions", (object)model.CapaActions ?? "");
            cmd.Parameters.AddWithValue("@CapaPerson", (object)model.CapaPerson ?? "");
            cmd.Parameters.AddWithValue("@CapaTargetDate", (object)model.CapaTargetDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@CapaVerifiedBy", (object)model.CapaVerifiedBy ?? "");
            cmd.Parameters.AddWithValue("@CapaVerifiedDate", (object)model.CapaVerifiedDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@VerificationNotes", (object)model.VerificationNotes ?? "");
            cmd.Parameters.AddWithValue("@EffEffective", model.EffEffective);
            cmd.Parameters.AddWithValue("@EffNotEffective", model.EffNotEffective);
            cmd.Parameters.AddWithValue("@EffRemarks", (object)model.EffRemarks ?? "");
            cmd.Parameters.AddWithValue("@VerifiedBy", (object)model.VerifiedBy ?? 0);
            cmd.Parameters.AddWithValue("@VerifiedDate", (object)model.VerifiedDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@ApprovedBy", (object)model.ApprovedBy ?? 0);
            cmd.Parameters.AddWithValue("@ApprovedDate", (object)model.ApprovedDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@StockEventID", model.StockEventID);
        }

        private static void AddDtlParameters(SqlCommand cmd, int eventId, CapaItemModel item)
        {
            cmd.Parameters.AddWithValue("@EventID", eventId);
            cmd.Parameters.AddWithValue("@LineNumber", item.LineNumber);
            cmd.Parameters.AddWithValue("@ItemID", (object)item.ItemID ?? 0);
            cmd.Parameters.AddWithValue("@Quantity", (object)item.Quantity ?? 0);
            cmd.Parameters.AddWithValue("@UOMID", (object)item.UOMID ?? 0);
            cmd.Parameters.AddWithValue("@DefectCategoryID", (object)item.DefectCategoryID ?? 0);
            cmd.Parameters.AddWithValue("@UsedToDate", (object)item.UsedToDate ?? "1900-01-01");
            cmd.Parameters.AddWithValue("@Remarks", (object)item.Remarks ?? "");
            cmd.Parameters.AddWithValue("@Hauler", (object)item.Hauler ?? "");
            cmd.Parameters.AddWithValue("@AdjustmentQty", (object)item.AdjustmentQty ?? 0);
            cmd.Parameters.AddWithValue("@AdjustmentBy", (object)item.AdjustmentBy ?? 0);
            cmd.Parameters.AddWithValue("@AdjustmentDate", (object)item.AdjustmentDate ?? "1900-01-01");
        }

        private static CapaReportModel MapHdrReader(SqlDataReader reader, int eventId)
        {
            return new CapaReportModel
            {
                EventID = eventId,
                ResponseToken = reader["ResponseToken"] as string,
                DateCreated = reader["DateCreated"] as DateTime?,
                IssuedBy = reader["IssuedBy"] as string,
                ReportNumber = reader["ReportNumber"] as string,
                IssuedToDepartment = Convert.ToInt32(reader["IssuedToDepartment"]),
                IssuedToDepartmentName = reader["IssuedToDepartmentName"] as string,
                SiteWarehouse = Convert.ToInt32(reader["SiteWarehouse"]),
                SiteWarehouseName = reader["SiteWarehouseName"] as string,
                RefNo = reader["RefNo"] as string,
                CorrectionDueDate = reader["CorrectionDueDate"] as DateTime?,
                ReportDueDate = reader["ReportDueDate"] as DateTime?,
                Status = reader["Status"] as string,
                LoopNumber = reader["loopNumber"] != DBNull.Value ? Convert.ToInt32(reader["loopNumber"]) : 0,
                NonconformanceType = reader["NonconformanceType"] as string,
                ExternalAuditSubType = reader["ExternalAuditSubType"] as string,
                CarClassificationType = reader["CarClassificationType"] as string,
                CarClassificationRefId = reader["CarClassificationRefId"] as string,
                CarClassificationRefName = reader["CarClassificationRefName"] as string,
                CarClassificationRefEmail = reader["CarClassificationRefEmail"] as string,
                CarClassificationOtherText = reader["CarClassificationOtherText"] as string,
                ImmediateAction = reader["ImmediateAction"] as string,
                IaPerson = reader["IaPerson"] as string,
                IaTargetDate = reader["IaTargetDate"] as DateTime?,
                IaVerifiedBy = reader["IaVerifiedBy"] as string,
                IaVerifiedDate = reader["IaVerifiedDate"] as DateTime?,
                RootCause = reader["RootCause"] as string,
                CapaActions = reader["CapaActions"] as string,
                CapaPerson = reader["CapaPerson"] as string,
                CapaTargetDate = reader["CapaTargetDate"] as DateTime?,
                CapaVerifiedBy = reader["CapaVerifiedBy"] as string,
                CapaVerifiedDate = reader["CapaVerifiedDate"] as DateTime?,
                VerificationNotes = reader["VerificationNotes"] as string,
                EffEffective = reader["EffEffective"] != DBNull.Value && (bool)reader["EffEffective"],
                EffNotEffective = reader["EffNotEffective"] != DBNull.Value && (bool)reader["EffNotEffective"],
                EffRemarks = reader["EffRemarks"] as string,
                VerifiedBy = reader["VerifiedBy"] as int?,
                VerifiedByName = reader["VerifiedByName"] as string,
                VerifiedDate = reader["VerifiedDate"] as DateTime?,
                ApprovedBy = reader["ApprovedBy"] as int?,
                ApprovedByName = reader["ApprovedByName"] as string,
                ApprovedDate = reader["ApprovedDate"] as DateTime?,
                StockEventID = Convert.ToInt32(reader["StockEventID"])
            };
        }
    }
}

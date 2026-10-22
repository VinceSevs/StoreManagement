using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using StoreManagement.Models;

namespace StoreManagement.Repositories
{
    // dbo.tbl_NncrLog. The log must never get in the way of the NNCR workflow, so a failure
    // here (e.g. the table has not been created yet) is traced and swallowed.
    public class NncrLogRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task Add(string reportNumber, string action, string description, int userId, string userName)
        {
            if (string.IsNullOrWhiteSpace(reportNumber)) return;

            const string query = @"
                INSERT INTO dbo.tbl_NncrLog (ReportNumber, Action, Description, UserID, UserName)
                VALUES (@ReportNumber, @Action, @Description, @UserID, @UserName)";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ReportNumber", reportNumber);
                    cmd.Parameters.AddWithValue("@Action", action ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Description", Truncate(description, 500));
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@UserName", (object)Truncate(userName, 150) ?? DBNull.Value);
                    await conn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("NNCR log write failed: " + ex.Message);
            }
        }

        // Newest first.
        public async Task<List<NncrLogEntry>> GetForReport(string reportNumber)
        {
            var list = new List<NncrLogEntry>();
            if (string.IsNullOrWhiteSpace(reportNumber)) return list;

            const string query = @"
                SELECT LogID, ReportNumber, Action, Description, UserID, UserName, LogDate
                FROM dbo.tbl_NncrLog
                WHERE ReportNumber = @ReportNumber
                ORDER BY LogDate DESC, LogID DESC";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ReportNumber", reportNumber);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new NncrLogEntry
                            {
                                LogID = reader.GetInt32(0),
                                ReportNumber = reader.GetString(1),
                                Action = reader.GetString(2),
                                Description = reader.GetString(3),
                                UserID = reader.GetInt32(4),
                                UserName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                LogDate = reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("NNCR log read failed: " + ex.Message);
            }
            return list;
        }

        private static string Truncate(string value, int max)
        {
            if (value == null) return null;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;

namespace StoreManagement.Models
{
    public class UpdateLogRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        // LogID is a plain NOT NULL column (not an identity) — compute the
        // next value ourselves, in the same statement as the insert so the
        // read-then-write stays as one round trip.
        public void AddLog(int storeID, string logDescription, string logType, int userID)
        {
            const string query = @"
                INSERT INTO tbl_StoreLog (LogID, StoreID, LogDescription, LogDate, LogType, UserID)
                SELECT ISNULL(MAX(LogID), 0) + 1, @StoreID, @LogDescription, GETDATE(), @LogType, @UserID
                FROM tbl_StoreLog
            ";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@StoreID", storeID);
                cmd.Parameters.AddWithValue("@LogDescription", (object)logDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LogType", (object)logType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserID", userID > 0 ? (object)userID : DBNull.Value);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Every filter is optional — pass null to leave it out entirely.
        public List<UpdateLog> GetLogList(int? storeID = null, string logType = null, int? userID = null)
        {
            var logList = new List<UpdateLog>();

            const string query = @"
                SELECT LogID, StoreID, LogDescription, LogDate, LogType, UserID
                FROM tbl_StoreLog
                WHERE (@StoreID IS NULL OR StoreID = @StoreID)
                  AND (@LogType IS NULL OR LogType = @LogType)
                  AND (@UserID IS NULL OR UserID = @UserID)
                ORDER BY LogDate DESC
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@StoreID", (object)storeID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LogType", (object)logType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserID", (object)userID ?? DBNull.Value);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logList.Add(new UpdateLog
                        {
                            LogID = (int)reader["LogID"],
                            StoreID = (int)reader["StoreID"],
                            LogDescription = reader["LogDescription"] as string,
                            LogDate = (DateTime)reader["LogDate"],
                            LogType = reader["LogType"] as string,
                            UserID = reader["UserID"] == DBNull.Value ? 0 : (int)reader["UserID"]
                        });
                    }
                }
            }
            return logList;
        }
    }
}

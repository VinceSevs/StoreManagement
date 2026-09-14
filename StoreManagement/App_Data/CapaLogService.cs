using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class CapaLogService
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task AddLog(string logDescription, string logType, int userID)
        {
            const string query = @"
                INSERT INTO tbl_QualityIncidentLog (LogDescription, LogType, UserID)
                VALUES (@LogDescription, @LogType, @UserID)
            ";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@LogDescription", logDescription ?? string.Empty);
                cmd.Parameters.AddWithValue("@LogType", logType ?? string.Empty);
                cmd.Parameters.AddWithValue("@UserID", userID);
                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}

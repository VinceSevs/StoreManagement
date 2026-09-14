using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    // Writes to tbl_QualityIncidentLog — every column is NOT NULL, so every
    // call must supply a real LogDescription/LogType and a real UserID
    // (0 for actions taken outside a logged-in session, e.g. an external
    // respondent replying through their emailed token link). LogDate is
    // left out of the INSERT — the column defaults to GETDATE().
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

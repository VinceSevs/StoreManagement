using IsseERP.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    // Backs the VerifiedBy/ApprovedBy/AdjustmentBy pickers used during the
    // QA verification workflow (not on the initial Quality.cshtml filing
    // form — those fields are only filled in at a later review stage).
    public class UserAccountService
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task<List<UserAccountLookup>> GetAllUsers()
        {
            var list = new List<UserAccountLookup>();
            const string query = @"
                SELECT id, username
                FROM tbl_UserAccount
                ORDER BY username";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new UserAccountLookup
                        {
                            Id = Convert.ToInt32(reader["id"]),
                            Username = reader["username"] as string
                        });
                    }
                }
            }
            return list;
        }
    }
}

using IsseERP.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class UOMService
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task<List<UOM>> GetUOMList()
        {
            var list = new List<UOM>();
            const string query = @"
                SELECT id, Text, ISO_Code
                FROM tbl_UOM
                ORDER BY Text";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new UOM
                        {
                            UOMID = Convert.ToInt32(reader["id"]),
                            Text = reader["Text"] as string,
                            IsoCode = reader["ISO_Code"] as string
                        });
                    }
                }
            }
            return list;
        }
    }
}

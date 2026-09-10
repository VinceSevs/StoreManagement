using IsseERP.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class ItemService
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task<List<Item>> GetItemlist()
        {
            var items = new List<Item>();
            const string query = @"
                SELECT id, item_desc, item_code
                FROM tbl_Item
                ORDER BY item_desc";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new Item
                        {
                            ItemID = Convert.ToInt32(reader["id"]),
                            ItemDescription = reader["item_desc"] as string,
                            ItemCode = reader["item_code"] as string
                        });
                    }
                }
            }
            return items;
        }
    }
}

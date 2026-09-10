using IsseERP.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class WarehouseService
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task<List<Warehouse>> GetWarehouse()
        {
            var warehouses = new List<Warehouse>();
            const string query = @"
                SELECT WarehouseID, WarehouseCode, WarehouseName, ExternalCode
                FROM tbl_Warehouse
                ORDER BY WarehouseName
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        warehouses.Add(new Warehouse
                        {
                            WarehouseID = Convert.ToInt32(reader["WarehouseID"]),
                            WarehouseCode = reader["WarehouseCode"] as string,
                            WarehouseName = reader["WarehouseName"] as string,
                            ExternalCode = reader["ExternalCode"] as string
                        });
                    }
                }
            }
            return warehouses;
        }
    }
}

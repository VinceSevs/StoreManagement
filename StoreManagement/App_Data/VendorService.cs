using IsseERP.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace IsseERP.Services
{
    //public class VendorService : BaseApiClient
    //{
    //    private readonly HttpClient _httpClient;
    //    private readonly string connectionString =
    //        ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

    //    public VendorService()
    //    {
    //        _httpClient = new HttpClient
    //        {
    //            BaseAddress = new Uri(_apiBaseUrl)
    //        };
    //    }

    //    public async Task<List<Vendor>> GetVendors()
    //    {
    //        List<Vendor> vendors = new List<Vendor>();

    //        try
    //        {
    //            var response = await _httpClient.GetAsync("api/vendor");
    //            if (!response.IsSuccessStatusCode) return new List<Vendor>();

    //            var body = await response.Content.ReadAsStringAsync();
    //            var result = JsonConvert.DeserializeObject<List<Vendor>>(body) ?? new List<Vendor>();
    //            vendors = result.OrderByDescending(v => v.VendorId).ToList();
    //            return vendors;
    //        }
    //        catch
    //        {
    //            return new List<Vendor>();
    //        }
    //    }

    
    //}

    public class VendorService
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task<List<Vendor>> GetVendors()
        {
            var vendors = new List<Vendor>();
            const string query = @"
                SELECT VendorID, VendorName, EmailAddress
                FROM tbl_Vendor
                ORDER BY VendorName 
            ";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        vendors.Add(new Vendor
                        {
                            VendorID = Convert.ToInt32(reader["VendorID"]),
                            VendorName = reader["VendorName"] as string,
                            EmailAddress = reader["EmailAddress"] as string
                        });
                    }
                }
            }
            return vendors;
        }

        public async Task<List<Item>> GetItemsByVendor(int vendorId)
        {
            var items = new List<Item>();
            const string query = @"
                SELECT DISTINCT VI.ItemID, I.item_code, I.item_desc
                FROM tbl_Vendor V
                INNER JOIN tbl_VendorItemPrice_Log VI ON V.VendorID = VI.VendorID
                INNER JOIN tbl_Item I ON VI.ItemID = I.id
                WHERE V.VendorID = @VendorID
                ORDER BY I.item_desc
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@VendorID", vendorId);
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new Item
                        {
                            ItemID = Convert.ToInt32(reader["ItemID"]),
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


using IsseERP.Models;
using Microsoft.Ajax.Utilities;
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
    //public class TruckService : BaseApiClient
    //{
    //    private readonly HttpClient _httpClient;

    //    public TruckService()
    //    {
    //        _httpClient = new HttpClient
    //        {
    //            BaseAddress = new Uri(_apiBaseUrl)
    //        };
    //    }

    //    public async Task<List<Trucker>> GetTrucksAsync()
    //    {
    //        List<Trucker> vendors = new List<Trucker>();

    //        try
    //        {
    //            var response = await _httpClient.GetAsync("api/truck");
    //            if (!response.IsSuccessStatusCode) return new List<Trucker>();

    //            var body = await response.Content.ReadAsStringAsync();
    //            var result = JsonConvert.DeserializeObject<List<Trucker>>(body) ?? new List<Trucker>();
    //            vendors = result.OrderByDescending(t => t.TruckID).ToList();
    //            return vendors;
    //        }
    //        catch
    //        {
    //            return new List<Trucker>();
    //        }
    //    }
    //}

    public class TruckService
    {
        private readonly string connectionString = 
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public async Task<List<Trucker>> GetTruckers()
        {
            var truckers = new List<Trucker>();
            const string query = @"
                SELECT TruckID, TruckerName, EmailAddress
                FROM tbl_Truck
                ORDER BY TruckerName";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                await conn.OpenAsync();
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var trucker = new Trucker
                        {
                            TruckID = Convert.ToInt32(reader["TruckID"]),
                            TruckerName = reader["TruckerName"] as string,
                            EmailAddress = reader["EmailAddress"] as string
                        };
                        truckers.Add(trucker);
                    }
                }
            }
            return truckers;
        }
    }
}


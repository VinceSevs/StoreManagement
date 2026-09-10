using IsseERP.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class InboundService
    {
        private readonly HttpClient _httpClient;

        public InboundService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://leadlogistics.com.ph/leadsapi/")
            };
        }

        public List<InterbinTransfer> GetInterbinTransfers()
        {
            return GetInterbinTransfersAsync().Result;
        }

        public async Task<List<InterbinTransfer>> GetInterbinTransfersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/interbintransfer/list");
                if (!response.IsSuccessStatusCode) return new List<InterbinTransfer>();

                var body = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(body);
                if (result.success != true) return new List<InterbinTransfer>();

                return JsonConvert.DeserializeObject<List<InterbinTransfer>>(result.data.ToString());
            }
            catch
            {
                return new List<InterbinTransfer>();
            }
        }
    }
}

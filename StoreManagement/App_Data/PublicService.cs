using IsseERP.Models;
//using IsseERP.Repository;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace IsseERP.Services
{
    public class PublicService : BaseApiClient
    {
        private readonly HttpClient _httpClient;

        public PublicService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_apiBaseUrl)
            };
        }

        public List<InterbinTransfer> GetAllTransfer(string whse)
        {
            InboundService inboundService = new InboundService();
            var transfers = inboundService.GetInterbinTransfers();

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(2);

            return transfers
                .Where(t =>
                    t.WarehouseCode.Equals(whse, StringComparison.OrdinalIgnoreCase) &&
                    t.TransferDate >= today &&
                    t.TransferDate < tomorrow
                )
                .OrderBy(t => t.PalletTag)
                .ToList();
        }
        public List<InterbinTransfer> GetFilteredTransfers(string whse, string cat)
        {
            InboundService inboundService = new InboundService();
            var transfers = inboundService.GetInterbinTransfers();

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(2);

            return transfers
                .Where(t =>
                    t.WarehouseCode.Equals(whse, StringComparison.OrdinalIgnoreCase) &&
                    t.TransferDate >= today &&
                    t.TransferDate < tomorrow &&
                    (
                        cat == "D"
                            ? (t.ProductType_ID == 4 || t.ProductType_ID == 7 || t.ProductType_ID == 6)
                            : (t.ProductType_ID != 4 && t.ProductType_ID != 7)
                    )
                )
                .OrderBy(t => t.PalletTag)
                .ToList();
        }

        //public async Task<string> GetNextCapaNo()
        //{
        //    try
        //    {
        //        var response = await _httpClient.GetAsync("api/capa/next-no");
        //        if (!response.IsSuccessStatusCode) return "CAPA-00001";

        //        var body = await response.Content.ReadAsStringAsync();
        //        var result = JsonConvert.DeserializeObject<dynamic>(body);
        //        if (result.success != true) return "CAPA-00001";

        //        return result.data?.ToString() ?? "CAPA-00001";
        //    }
        //    catch
        //    {
        //        return "CAPA-00001";
        //    }
        //}
    }
}
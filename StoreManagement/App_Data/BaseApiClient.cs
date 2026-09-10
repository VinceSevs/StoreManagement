using IsseERP.Models;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace IsseERP.Services
{
    public class BaseApiClient
    {
        protected readonly string _apiBaseUrl;

        public BaseApiClient() {
            _apiBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"];
        }
    }
}

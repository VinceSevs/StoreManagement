using System.ComponentModel.DataAnnotations.Schema;

namespace StoreManagement.Models
{
    public class Store
    {
        public int StoreID { get; set; }
        public string StoreNumber { get; set; }
        public string StoreName { get; set; }
        public int CityID { get; set; }
        public string CityName { get; set; }
        public string Coordinates { get; set; }
        public string StateName { get; set; }
    }
}

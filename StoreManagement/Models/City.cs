using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace StoreManagement.Models
{
    public class City
    {
        public int CityID { get; set; }
        public string Name { get; set; }
        public int StateID { get; set; }
    }
}

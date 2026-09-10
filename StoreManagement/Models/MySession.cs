using System.Web;

namespace IsseERP.Models
{
    public class MySession
    {
        public string FullName { get; set; }
        public string FirstName { get; set; }

        public static MySession Current
        {
            get
            {
                var identity = HttpContext.Current?.User?.Identity;
                var username = (identity != null && identity.IsAuthenticated) ? identity.Name : null;
                //var identity2 = HttpContext.Current?.User?.Identity;
                var firstname = (identity != null && identity.IsAuthenticated) ? identity.Name : null;
                return new MySession { FullName = username, FirstName = firstname };
            }

        }

        //public static MySession Current2
        //{ 
        //    get
        //    {
        //        var identity2 = HttpContext.Current?.User?.Identity;
        //        var firstname = (identity2 != null && identity2.IsAuthenticated) ? identity2.Name : null;
        //        return new MySession { FirstName = firstname };
        //    }
        //}
    }
}

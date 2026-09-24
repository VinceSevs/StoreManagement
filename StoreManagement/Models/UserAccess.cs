using System.Web;

namespace StoreManagement.Models
{
    public enum AppSection
    {
        Dashboard,
        Car,
        ComplaintReport,
        PdfReceipt,
        Store
    }

    // Role-based access by tbl_Role.DepartmentID (tbl_UserAccount.role_id -> tbl_Role.RoleID).
    public static class UserAccess
    {
        public const int AllAccessDepartment = 2;
        public const int QualityDepartment = 11;
        public const int ProcurementDepartment = 12;

        private const string CacheKey = "UserAccess.CurrentUser";

        public static bool CanAccess(int? departmentId, AppSection section)
        {
            if (departmentId == null) return false;

            switch (departmentId.Value)
            {
                case AllAccessDepartment:
                    return true;
                case QualityDepartment:
                    return section == AppSection.Dashboard
                        || section == AppSection.Car
                        || section == AppSection.ComplaintReport;
                case ProcurementDepartment:
                    return section == AppSection.PdfReceipt;
                default:
                    return false;
            }
        }

        public static bool CanAccess(Login user, AppSection section)
        {
            return user != null && CanAccess(user.DepartmentId, section);
        }

        // Signed-in user's profile, loaded once per request.
        public static Login Current(HttpContextBase context)
        {
            if (context == null || context.User == null || !context.User.Identity.IsAuthenticated) return null;

            if (!context.Items.Contains(CacheKey))
            {
                context.Items[CacheKey] = new AccountRepository().GetByUsername(context.User.Identity.Name);
            }
            return context.Items[CacheKey] as Login;
        }

        // Landing page (action, controller) for the user's department; null when they can access nothing.
        public static string[] HomeRoute(Login user)
        {
            if (CanAccess(user, AppSection.Dashboard)) return new[] { "Index", "Dashboard" };
            if (CanAccess(user, AppSection.PdfReceipt)) return new[] { "PdfReceipt", "Finance" };
            return null;
        }
    }
}

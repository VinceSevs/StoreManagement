using System.Web;
using StoreManagement.Repositories;

namespace StoreManagement.Models
{
    public enum AppSection
    {
        Dashboard,
        Car,
        Nncr,
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
                    // Dashboard, PCR, NCR and NNCR are IT-only (AllAccessDepartment).
                    return section == AppSection.Car;
                case ProcurementDepartment:
                    return section == AppSection.PdfReceipt;
                default:
                    return false;
            }
        }

        // tbl_UserAccount.role_id values allowed to close NNCRs (QA supervisors); they also get NNCR access.
        public static readonly int[] NncrCloserRoles = { 49, 8 };

        public static bool CanAccess(Login user, AppSection section)
        {
            if (user == null) return false;
            if (section == AppSection.Nncr && CanCloseNncr(user)) return true;
            return CanAccess(user.DepartmentId, section);
        }

        public static bool CanCloseNncr(Login user)
        {
            return user != null && System.Array.IndexOf(NncrCloserRoles, user.RoleId) >= 0;
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
            if (CanAccess(user, AppSection.Car)) return new[] { "CapaList", "Capa" };
            if (CanAccess(user, AppSection.Nncr)) return new[] { "NncrList", "Nncr" };
            if (CanAccess(user, AppSection.PdfReceipt)) return new[] { "PdfReceipt", "Finance" };
            return null;
        }
    }
}

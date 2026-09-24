namespace StoreManagement.Models
{
    public class Login
    {
        public int UserID { get; set; }
        public string EmpId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string RoleJobTitle { get; set; }
        public int? DepartmentId { get; set; }

        public string DisplayName
        {
            get
            {
                string name = (FirstName + " " + LastName).Trim();
                return string.IsNullOrWhiteSpace(name) ? Username : name;
            }
        }
    }
}
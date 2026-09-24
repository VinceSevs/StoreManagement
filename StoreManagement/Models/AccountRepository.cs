using System.Configuration;
using System.Data.SqlClient;
using Encryptionv2;

namespace StoreManagement.Models
{
    public class AccountRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        public Login GetByUsername(string username)
        {
            Login login = null;

            string query = @"
                SELECT u.id, u.username, u.password, u.firstname, u.lastname, u.email, u.emp_id, r.RoleJobTitle, r.DepartmentID
                FROM tbl_UserAccount u
                LEFT JOIN tbl_Role r ON u.role_id = r.RoleID
                WHERE u.username = @username
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@username", username);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        login = new Login
                        {
                            UserID = (int)reader["id"],
                            Username = reader["username"].ToString(),
                            Password = reader["password"].ToString(),
                            FirstName = reader["firstname"] == System.DBNull.Value ? "" : reader["firstname"].ToString(),
                            LastName = reader["lastname"] == System.DBNull.Value ? "" : reader["lastname"].ToString(),
                            Email = reader["email"] == System.DBNull.Value ? "" : reader["email"].ToString(),
                            EmpId = reader["emp_id"] == System.DBNull.Value ? "" : reader["emp_id"].ToString(),
                            RoleJobTitle = reader["RoleJobTitle"] == System.DBNull.Value ? "" : reader["RoleJobTitle"].ToString(),
                            DepartmentId = reader["DepartmentID"] == System.DBNull.Value ? (int?)null : System.Convert.ToInt32(reader["DepartmentID"])
                        };
                    }
                }
            }
            return login;
        }

        public bool UsernameExists(string username)
        {
            string query = @"
                SELECT COUNT(1) 
                FROM tbl_UserAccount 
                WHERE username = @username
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@username", username);
                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public void CreateUser(string username, string passwordHash)
        {
            string query = @"
                INSERT INTO tbl_UserAccount (username, password)
                VALUES (@username, @password)
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password", passwordHash);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdatePassword(int userId, string newPasswordHash)
        {
            string query = @"
                UPDATE tbl_UserAccount
                SET password = @password
                WHERE id = @id
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@password", newPasswordHash);
                cmd.Parameters.AddWithValue("@id", userId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
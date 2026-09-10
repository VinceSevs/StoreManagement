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
                SELECT id, username, password 
                FROM tbl_UserAccount
                WHERE username = @username
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
                            //UserID = (int)reader["UserID"],
                            Username = reader["username"].ToString(),
                            Password = reader["password"].ToString()
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
    }
}
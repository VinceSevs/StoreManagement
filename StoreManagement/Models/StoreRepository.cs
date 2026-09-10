using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace StoreManagement.Models
{
    public class StoreRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;
        public List<Suggestion> GetSuggestions(string field, string term)
        {
            var suggestions = new List<Suggestion>();
            string query;

            switch (field)
            {
                case "search":
                    query = @"
                        SELECT DISTINCT TOP 10 Display, Value
                        FROM (
                            SELECT s.StoreNumber AS Display, s.StoreNumber AS Value
                                FROM tbl_Store s WHERE s.StoreNumber
                                    LIKE '%' + @Term + '%'
                            UNION
                            SELECT s.StoreName + ' (' + c.Name + ', ' + st.Name + ')', s.StoreName
                                FROM tbl_Store s
                                JOIN tbl_City c
                                    ON s.CityID = c.CityID
                                JOIN tbl_State st
                                    ON c.StateID = st.StateID
                                WHERE s.StoreName
                                    LIKE '%' + @Term + '%'
                            UNION
                            SELECT c.Name + ' (' + st.Name + ')', c.Name
                                FROM tbl_City c
                                JOIN tbl_State st
                                    ON c.StateID = st.StateID
                                WHERE c.Name
                                    LIKE '%' + @Term + '%'
                            UNION
                            SELECT s.Coordinates, s.Coordinates
                                FROM tbl_Store s WHERE s.Coordinates
                                    LIKE '%' + @Term + '%'
                        ) AS combined
                        ORDER BY Display";
                    break;

                default:
                    return suggestions;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Term", term ?? string.Empty);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        suggestions.Add(new Suggestion
                        {
                            Display = reader["Display"].ToString(),
                            Value = reader["Value"].ToString()
                        });
                    }
                }
            }
            return suggestions;
        }

        public List<Store> GetAllStores(int? storeID = null, string search = null, string storeNumber = null, string storeName = null, string cityName = null, string coordinates = null)
        {
            var stores = new List<Store>();

            string query = @"
                SELECT
                    s.StoreID,
                    s.StoreName,
                    s.StoreNumber,
                    s.CityID,
                    c.Name AS CityName,
                    s.Coordinates,
                    st.Name AS StateName
                FROM tbl_Store s
                JOIN tbl_City c 
                    ON s.CityID = c.CityID
                JOIN tbl_State st 
                    ON c.StateID = st.StateID
                WHERE
                    (@StoreID IS NULL OR s.StoreID = @StoreID)
                    AND (@Search IS NULL
                        OR s.StoreNumber LIKE '%' + @Search + '%'
                        OR s.StoreName LIKE '%' + @Search + '%'
                        OR c.Name LIKE '%' + @Search + '%'
                        OR s.Coordinates LIKE '%' + @Search + '%')
                    AND (@StoreNumber IS NULL OR s.StoreNumber 
                        LIKE '%' + @StoreNumber + '%')
                    AND (@StoreName IS NULL OR s.StoreName 
                        LIKE '%' + @StoreName + '%')
                    AND (@CityName IS NULL OR c.Name 
                        LIKE '%' + @CityName + '%')
                    AND (@Coordinates IS NULL OR s.Coordinates 
                        LIKE '%' + @Coordinates + '%');
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@StoreID", (object)storeID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Search", (object)search ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@StoreNumber", (object)storeNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@StoreName", (object)storeName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CityName", (object)cityName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Coordinates", (object)coordinates ?? DBNull.Value);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        stores.Add(new Store
                        {
                            StoreID = Convert.ToInt32(reader["StoreID"]),
                            StoreName = reader["StoreName"].ToString(),
                            StoreNumber = reader["StoreNumber"].ToString(),
                            CityID = Convert.ToInt32(reader["CityID"]),
                            CityName = reader["CityName"].ToString(),
                            Coordinates = reader["Coordinates"].ToString(),
                            StateName = reader["StateName"].ToString()
                        });
                    }
                }
            }
            return stores;
        }

        public List<City> GetAllCities()
        {
            var cities = new List<City>();

            string query = @"
                SELECT c.CityID, c.Name, c.StateID
                FROM tbl_City c
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cities.Add(new City
                        {
                            CityID = Convert.ToInt32(reader["CityID"]),
                            Name = reader["Name"].ToString(),
                            StateID = Convert.ToInt32(reader["StateID"])
                        });
                    }
                }
            }
            return cities;
        }

        public List<State> GetAllStates()
        {
            var states = new List<State>();

            string query = @"
                SELECT StateID, Name
                FROM tbl_State
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        states.Add(new State
                        {
                            StateID = Convert.ToInt32(reader["StateID"]),
                            Name = reader["Name"].ToString()
                        });
                    }
                }
            }
            return states;
        }

        public Store GetByStoreId(int storeId)
        {
            Store store = null;

            string query = @"
                SELECT s.StoreID, s.StoreName, s.StoreNumber, s.CityID, c.Name AS CityName, s.Coordinates, st.Name AS StateName
                FROM tbl_Store s
                JOIN tbl_City c 
                    ON s.CityID = c.CityID
                JOIN tbl_State st 
                    ON c.StateID = st.StateID
                WHERE s.StoreID = @StoreID
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                cmd.Parameters.AddWithValue("@StoreID", storeId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        store = new Store
                        {
                            StoreID = Convert.ToInt32(reader["StoreID"]),
                            StoreName = reader["StoreName"].ToString(),
                            StoreNumber = reader["StoreNumber"].ToString(),
                            CityID = Convert.ToInt32(reader["CityID"]),
                            CityName = reader["CityName"].ToString(),
                            Coordinates = reader["Coordinates"].ToString(),
                            StateName = reader["StateName"].ToString()
                        };
                    }
                }
            }
            return store;
        }

        public void UpdateStore(int StoreID, int CityID, string Coordinates, int userID)
        {
            int oldCityID;
            string oldCoordinates;

            string selectQuery = @"
                SELECT CityID, Coordinates 
                FROM tbl_Store 
                WHERE StoreID = @StoreID
            ";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(selectQuery, conn))
            {
                cmd.Parameters.AddWithValue("@StoreID", StoreID);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return;
                    oldCityID = Convert.ToInt32(reader["CityID"]);
                    oldCoordinates = reader["Coordinates"] is DBNull ? null : reader["Coordinates"].ToString();
                }
            }

            string updateQuery = @"
                UPDATE tbl_Store
                SET CityID = @CityID, Coordinates = @Coordinates
                WHERE StoreID = @StoreID
            ";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
            {
                cmd.Parameters.AddWithValue("@StoreID", StoreID);
                cmd.Parameters.AddWithValue("@CityID", CityID);
                cmd.Parameters.AddWithValue("@Coordinates", string.IsNullOrEmpty(Coordinates) ? string.Empty : Coordinates); 
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            bool cityChanged = oldCityID != CityID;

            // Normalize before comparing — a NULL in the database and an
            // empty/whitespace value posted back from the form are the same
            // "no coordinates", not a change.
            string normalizedOldCoordinates = (oldCoordinates ?? string.Empty).Trim();
            string normalizedNewCoordinates = (Coordinates ?? string.Empty).Trim();
            bool coordinatesChanged = normalizedOldCoordinates != normalizedNewCoordinates;

            if (!cityChanged && !coordinatesChanged) return;

            var logRepo = new UpdateLogRepository();

            if (cityChanged && coordinatesChanged)
            {
                logRepo.AddLog(StoreID, "Store city and coordinates update", "Store city and coordinates update", userID);
            }
            else if (cityChanged)
            {
                logRepo.AddLog(StoreID, "Store city update", "Store city update", userID);
            }
            else if (coordinatesChanged)
            {
                logRepo.AddLog(StoreID, "Store coordinate update", "Store coordinates update", userID);
            }
        }

        internal List<Suggestion> GetSearchSuggestions(string term)
        {
            return GetSuggestions("search", term);
        }
    }
}
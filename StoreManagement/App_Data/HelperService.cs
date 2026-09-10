//using IsseERP.Models;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Data.SqlClient;
//using System.Linq;
//using System.Net.Http;
//using System.Threading.Tasks;
//using System.Web.Helpers;

//namespace IsseERP.Services
//{
//    public class HelperService : BaseApiClient
//    {
//        private readonly HttpClient _httpClient;

//        public HelperService()
//        {
//            _httpClient = new HttpClient
//            {
//                BaseAddress = new Uri(_apiBaseUrl)
//            };
//        }

//        public async Task<List<Department>> _GetDepartment()

//        {

//            List<Department> departments = new List<Department>();


//            using (SqlConnection connection = new SqlConnection(MyHelper.stringConnection))

//            {

//                string query = @"SELECT DepartmentID, DepartmentDescription FROM tbl_Department ORDER BY DepartmentID ASC";


//                using (SqlCommand command = new SqlCommand(query, connection))

//                {

//                    await connection.OpenAsync();


//                    using (SqlDataReader reader = await command.ExecuteReaderAsync())

//                    {

//                        while (await reader.ReadAsync())

//                        {

//                            Department department = new Department
//                            {

//                                DepartmentID = reader.GetInt32(reader.GetOrdinal("DepartmentID")),

//                                DepartmentDescription = reader.GetString(reader.GetOrdinal("DepartmentDescription"))

//                            };


//                            departments.Add(department);

//                        }

//                    }

//                }

//            }


//            return departments;

//        }
//    }
//}



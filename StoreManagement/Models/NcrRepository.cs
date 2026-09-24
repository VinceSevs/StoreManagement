using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace StoreManagement.Models
{
    // Read-only view of the Non-Conformance Report (NCR) data that lives in Lead_Db
    // (same database used by LLIIOrderingSystem_V3, and the same tables as PCR —
    // tbl_ComplaintHdr / tbl_ComplaintDtl / tbl_ComplaintResponse). Creating a new
    // NCR is intentionally not supported here — only viewing the full list/detail
    // and, for Customer Care, adding a response to a specific NCR.
    public class NcrRepository
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["StoreContext"].ConnectionString;

        private static string SafeString(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? "" : reader.GetValue(ordinal).ToString();
        }

        private static int SafeInt(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static DateTime SafeDate(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        private static DateTime? SafeNullableDate(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(ordinal));
        }

        public List<Ncr> GetAllNCRs()
        {
            var result = new List<Ncr>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand("sp_SO_NCRList", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Ncr
                        {
                            DocID = SafeInt(reader, "DocID"),
                            NcrNumber = SafeString(reader, "ComNumber"),
                            StoreName = SafeString(reader, "StoreName"),
                            FiledBy = SafeString(reader, "FileBy"),
                            DateCreated = SafeDate(reader, "DateCreated"),
                            NcrStatus = SafeInt(reader, "DocStatus")
                        });
                    }
                }
            }

            return result;
        }

        public Ncr GetNCRDetails(int docId)
        {
            Ncr ncr = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand("sp_SO_StoreOngoingNCRDetails", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ncr = new Ncr
                            {
                                DocID = SafeInt(reader, "DocID"),
                                NcrNumber = SafeString(reader, "ncr_number"),
                                ComplaintIssueDesc = SafeString(reader, "ComplaintIssueDesc"),
                                StoreNumber = SafeString(reader, "StoreNumber"),
                                StoreName = SafeString(reader, "StoreName"),
                                StoreContact = SafeString(reader, "StoreContact"),
                                StoreEmail = SafeString(reader, "StoreEmail"),
                                ManagerInCharge = SafeString(reader, "ManagerInCharge"),
                                FiledBy = SafeString(reader, "FileBy"),
                                DateCreated = SafeDate(reader, "DateCreated"),
                                NcrStatus = SafeInt(reader, "ncrStatus"),
                                Subject = SafeString(reader, "Subject"),
                                Remarks = SafeString(reader, "Remarks"),
                                CSRemarks = SafeInt(reader, "CSRemarks"),
                                CSFindings = SafeString(reader, "CSFindings"),
                                CSResDate = SafeNullableDate(reader, "CSResDate"),
                            };
                        }
                    }
                }

                if (ncr == null) return null;

                // Attachments live in tbl_ComplaintDtl, shared with PCR (same
                // ~/PCRattachments/ folder on the LLIIOrderingSystem_V3 server).
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT Attachment1, Attachment2, Attachment3, Attachment4
                      FROM tbl_ComplaintDtl WHERE DocID = @DocID", conn))
                {
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ncr.Attachment1 = SafeString(reader, "Attachment1");
                            ncr.Attachment2 = SafeString(reader, "Attachment2");
                            ncr.Attachment3 = SafeString(reader, "Attachment3");
                            ncr.Attachment4 = SafeString(reader, "Attachment4");
                        }
                    }
                }
            }

            return ncr;
        }

        // Mirrors AdminController.UpdateCSFindings from LLIIOrderingSystem_V3, minus
        // the email notification.
        public bool InsertCSResponse(int docId, string csRemarks, string csFindings, int userId, out string message)
        {
            message = "";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand checkCmd = new SqlCommand(
                    "SELECT DocStatus FROM tbl_ComplaintHdr WHERE DocID = @DocID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@DocID", docId);
                    var statusObj = checkCmd.ExecuteScalar();
                    if (statusObj == null || statusObj == DBNull.Value)
                    {
                        message = "NCR not found.";
                        return false;
                    }
                    if (Convert.ToInt32(statusObj) == 5)
                    {
                        message = "This NCR has been marked as INVALID. You cannot update the response.";
                        return false;
                    }
                }

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        const string insertQuery = @"
                            INSERT INTO tbl_ComplaintResponse
                                (DocID, LineNumber, VendorName, ResponseMessage, ResponseType, DateCreated, CreatedBy)
                            SELECT
                                @DocID,
                                ISNULL(MAX(LineNumber), 0) + 1,
                                'LLII CS',
                                @Findings,
                                @Remarks,
                                GETDATE(),
                                @UserID
                            FROM tbl_ComplaintResponse WITH (UPDLOCK, HOLDLOCK)
                            WHERE DocID = @DocID;";

                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn, transaction))
                        {
                            cmd.Parameters.Add("@DocID", SqlDbType.Int).Value = docId;
                            cmd.Parameters.Add("@Findings", SqlDbType.NVarChar, 500).Value = csFindings.Trim();
                            cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar, 200).Value = csRemarks.Trim();
                            cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                            cmd.ExecuteNonQuery();
                        }

                        if (csRemarks == "21" || csRemarks == "22")
                        {
                            using (SqlCommand cmd = new SqlCommand(
                                "UPDATE tbl_ComplaintHdr SET DocStatus = 24 WHERE DocID = @DocID", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@DocID", docId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        message = "An error occurred while saving CS findings: " + ex.Message;
                        return false;
                    }
                }
            }

            message = "CS findings saved successfully.";
            return true;
        }
    }
}

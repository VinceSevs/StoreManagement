using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace StoreManagement.Models
{
    public class ComplaintReportRepository
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

        public List<Pcr> GetAllPCRs()
        {
            var result = new List<Pcr>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand("sp_SO_PCRList", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Pcr
                        {
                            DocID = SafeInt(reader, "DocID"),
                            PcrNumber = SafeString(reader, "ComNumber"),
                            StoreName = SafeString(reader, "StoreName"),
                            ItemID = SafeString(reader, "ItemCode"),
                            ComplaintIssueDesc = SafeString(reader, "ComplaintIssueDesc"),
                            VendorName = SafeString(reader, "VendorName"),
                            SupplierRemarksDesc = SafeString(reader, "VendorRemarks"),
                            SupplierFindings = SafeString(reader, "VendorFindings"),
                            QARemarksDesc = SafeString(reader, "QAremarks"),
                            PcrStatus = SafeInt(reader, "DocStatus"),
                            DateCreated = SafeDate(reader, "DateCreated")
                        });
                    }
                }
            }

            return result;
        }

        public Pcr GetPCRDetails(int docId)
        {
            Pcr pcr = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand("sp_SO_AdminStoreOngoingPCRDetails", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            pcr = new Pcr
                            {
                                DocID = SafeInt(reader, "DocID"),
                                PcrNumber = SafeString(reader, "pcr_number"),
                                ItemID = SafeString(reader, "item_code"),
                                ItemDesc = SafeString(reader, "item_desc"),
                                BatchCode = SafeString(reader, "BatchCode"),
                                VendorName = SafeString(reader, "VendorName"),
                                ComplaintIssueDesc = SafeString(reader, "ComplaintIssueDesc"),
                                Qty = SafeInt(reader, "qty"),
                                UOM = SafeString(reader, "UOM"),
                                StoreNumber = SafeString(reader, "StoreNumber"),
                                StoreName = SafeString(reader, "StoreName"),
                                ManagerInCharge = SafeString(reader, "ManagerInCharge"),
                                FiledBy = SafeString(reader, "FileBy"),
                                DateCreated = SafeDate(reader, "DateCreated"),
                                PcrStatus = SafeInt(reader, "pcrStatus"),
                                IncidentDate = SafeDate(reader, "incident_date"),
                                UTD = SafeDate(reader, "UpToDate"),
                                Remarks = SafeString(reader, "Remarks"),
                                SupplierRemarks = SafeInt(reader, "SupplierRemarks"),
                                SupplierFindings = SafeString(reader, "SupplierFindings"),
                                QARemarks = SafeInt(reader, "QARemarks"),
                                QAFindings = SafeString(reader, "QAFindings"),
                            };
                        }
                    }
                }

                if (pcr == null) return null;

                using (SqlCommand cmd = new SqlCommand(
                    "SELECT ComFlag, StoreEmail FROM tbl_ComplaintHdr WHERE DocID = @DocID", conn))
                {
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            pcr.ComFlag = SafeInt(reader, "ComFlag");
                            pcr.StoreEmail = SafeString(reader, "StoreEmail");
                        }
                    }
                }

                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT StoreContact, Particulars, DateDelivered, Attachment1, Attachment2, Attachment3, Attachment4
                      FROM tbl_ComplaintDtl WHERE DocID = @DocID", conn))
                {
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            pcr.StoreContact = SafeString(reader, "StoreContact");
                            pcr.Particulars = SafeString(reader, "Particulars");
                            pcr.DeliveryDate = SafeNullableDate(reader, "DateDelivered");
                            pcr.Attachment1 = SafeString(reader, "Attachment1");
                            pcr.Attachment2 = SafeString(reader, "Attachment2");
                            pcr.Attachment3 = SafeString(reader, "Attachment3");
                            pcr.Attachment4 = SafeString(reader, "Attachment4");
                        }
                    }
                }

                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT TOP 1 ResponseMessage, ResponseType, DateCreated
                      FROM tbl_ComplaintResponse WHERE DocID = @DocID AND VendorName = 'GADC'
                      ORDER BY LineNumber DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            pcr.GADCFindings = SafeString(reader, "ResponseMessage");
                            pcr.GADCRemarks = SafeInt(reader, "ResponseType");
                            pcr.GADCResDate = SafeNullableDate(reader, "DateCreated");
                        }
                    }
                }

                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT TOP 1 DateCreated FROM tbl_ComplaintResponse
                      WHERE DocID = @DocID AND VendorName = 'LLII QA' ORDER BY LineNumber DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value) pcr.QAResDate = Convert.ToDateTime(result);
                }
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT TOP 1 DateCreated FROM tbl_ComplaintResponse
                      WHERE DocID = @DocID AND VendorName NOT IN ('LLII QA', 'GADC') ORDER BY LineNumber DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@DocID", docId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value) pcr.SupplierResDate = Convert.ToDateTime(result);
                }
            }

            return pcr;
        }

        public bool InsertQAResponse(int docId, string qaRemarks, string qaFindings, int userId, out string message)
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
                        message = "PCR not found.";
                        return false;
                    }
                    if (Convert.ToInt32(statusObj) == 5)
                    {
                        message = "This PCR has been marked as INVALID. You cannot update the response.";
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
                                'LLII QA',
                                @Findings,
                                @Remarks,
                                GETDATE(),
                                @UserID
                            FROM tbl_ComplaintResponse WITH (UPDLOCK, HOLDLOCK)
                            WHERE DocID = @DocID;";

                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn, transaction))
                        {
                            cmd.Parameters.Add("@DocID", SqlDbType.Int).Value = docId;
                            cmd.Parameters.Add("@Findings", SqlDbType.NVarChar, 500).Value = qaFindings.Trim();
                            cmd.Parameters.Add("@Remarks", SqlDbType.NVarChar, 200).Value = qaRemarks.Trim();
                            cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                            cmd.ExecuteNonQuery();
                        }

                        if (qaRemarks == "8" || qaRemarks == "9" || qaRemarks == "10")
                        {
                            using (SqlCommand cmd = new SqlCommand(
                                "UPDATE tbl_ComplaintHdr SET DocStatus = 2 WHERE DocID = @DocID", conn, transaction))
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
                        message = "An error occurred while saving QA findings: " + ex.Message;
                        return false;
                    }
                }

                // Email notification is disabled for now. Uncomment SendQAResponseEmail below
                // (and set Smtp:Host / Smtp:User / Smtp:Password / Smtp:From in Web.config)
                // to re-enable it.
                // SendQAResponseEmail(docId, qaRemarks, qaFindings, conn);
            }

            message = "QA findings saved successfully.";
            return true;
        }

        /*
        private void SendQAResponseEmail(int docId, string qaRemarks, string qaFindings, SqlConnection conn)
        {
            string storeEmail = "";
            using (SqlCommand cmd = new SqlCommand(
                "SELECT StoreEmail FROM tbl_ComplaintHdr WHERE DocID = @DocID", conn))
            {
                cmd.Parameters.AddWithValue("@DocID", docId);
                var result = cmd.ExecuteScalar();
                if (result != null) storeEmail = result.ToString();
            }

            if (string.IsNullOrWhiteSpace(storeEmail)) return;

            var smtpHost = ConfigurationManager.AppSettings["Smtp:Host"];
            var smtpUser = ConfigurationManager.AppSettings["Smtp:User"];
            var smtpPassword = ConfigurationManager.AppSettings["Smtp:Password"];
            var smtpFrom = ConfigurationManager.AppSettings["Smtp:From"];

            using (var mail = new System.Net.Mail.MailMessage())
            {
                mail.From = new System.Net.Mail.MailAddress(smtpFrom);
                mail.To.Add(storeEmail);
                mail.Subject = "RE: PCR Update - LLII Product Complaint";
                mail.IsBodyHtml = true;
                mail.Body = $"<p>LLII QA has provided an update on your PCR.</p><p>Remarks: {qaRemarks}</p><p>Findings: {qaFindings}</p>";

                using (var client = new System.Net.Mail.SmtpClient(smtpHost)
                {
                    Port = 587,
                    Credentials = new System.Net.NetworkCredential(smtpUser, smtpPassword),
                    EnableSsl = true
                })
                {
                    client.Send(mail);
                }
            }
        }
        */
    }
}

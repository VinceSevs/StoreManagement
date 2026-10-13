using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using StoreManagement.Models;

namespace IsseERP.Services
{
    // The email a filed NNCR sends to the supplier (button to NncrResponse), laid out like the
    // "Action Required: CAR" email. SMTP settings come from Web.config appSettings:
    //   Smtp:Host, Smtp:Port, Smtp:EnableSsl, Smtp:User, Smtp:Password, Smtp:From, Smtp:FromName
    public class NncrNotificationService
    {
        private const string LogoContentId = "leadlogo";

        public class EmailMessage
        {
            public string To { get; set; }
            public string Subject { get; set; }
            public string BodyHtml { get; set; }
            public string ResponseUrl { get; set; }
        }

        // logoUrl: absolute URL of Content/images/lead-logo.png, used when the email is shown
        // in the browser (the sent email embeds the logo instead).
        public EmailMessage BuildSupplierEmail(NncrReport nncr, string responseUrl, string logoUrl)
        {
            return new EmailMessage
            {
                To = nncr.SupplierEmail ?? "",
                Subject = $"Action Required: NNCR {nncr.ReportNumber}",
                BodyHtml = BuildBody(nncr, responseUrl, logoUrl),
                ResponseUrl = responseUrl
            };
        }

        // Sends the email to the supplier. Returns null when sent, otherwise why it was not sent.
        public string SendSupplierEmail(NncrReport nncr, string responseUrl, string logoPhysicalPath)
        {
            if (string.IsNullOrWhiteSpace(nncr.SupplierEmail)) return "The supplier has no email address on file.";

            string host = ConfigurationManager.AppSettings["Smtp:Host"];
            string from = ConfigurationManager.AppSettings["Smtp:From"];
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
                return "Email is not set up yet (Smtp:Host / Smtp:From in Web.config).";

            int port;
            if (!int.TryParse(ConfigurationManager.AppSettings["Smtp:Port"], out port)) port = 587;
            bool enableSsl;
            if (!bool.TryParse(ConfigurationManager.AppSettings["Smtp:EnableSsl"], out enableSsl)) enableSsl = true;
            string user = ConfigurationManager.AppSettings["Smtp:User"];
            string password = ConfigurationManager.AppSettings["Smtp:Password"];
            string fromName = ConfigurationManager.AppSettings["Smtp:FromName"];

            try
            {
                using (var mail = new MailMessage())
                {
                    mail.From = string.IsNullOrWhiteSpace(fromName) ? new MailAddress(from) : new MailAddress(from, fromName);
                    foreach (var address in nncr.SupplierEmail.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        mail.To.Add(address.Trim());
                    }
                    mail.Subject = $"Action Required: NNCR {nncr.ReportNumber}";

                    // Outlook doesn't show data: images, so the logo goes in as an embedded (cid:) image.
                    var view = AlternateView.CreateAlternateViewFromString(
                        BuildBody(nncr, responseUrl, "cid:" + LogoContentId), null, MediaTypeNames.Text.Html);
                    if (!string.IsNullOrWhiteSpace(logoPhysicalPath) && System.IO.File.Exists(logoPhysicalPath))
                    {
                        view.LinkedResources.Add(new LinkedResource(logoPhysicalPath, "image/png") { ContentId = LogoContentId });
                    }
                    mail.AlternateViews.Add(view);

                    using (var client = new SmtpClient(host, port) { EnableSsl = enableSsl })
                    {
                        if (!string.IsNullOrWhiteSpace(user))
                        {
                            client.Credentials = new NetworkCredential(user, password);
                        }
                        client.Send(mail);
                    }
                }
                System.Diagnostics.Trace.TraceInformation($"[NNCR] {nncr.ReportNumber} emailed to {nncr.SupplierEmail}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[NNCR] {nncr.ReportNumber} email to {nncr.SupplierEmail} failed: {ex}");
                return "The email could not be sent: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value ?? "");

        // Keeps the line breaks typed into the details box.
        private static string EncodeMultiline(string value) => Encode(value).Replace("\r\n", "\n").Replace("\n", "<br>");

        private static string BuildBody(NncrReport nncr, string url, string logoSrc)
        {
            string types = string.IsNullOrWhiteSpace(nncr.NonconformanceType) ? "—" : nncr.NonconformanceType.Replace("; ", ", ");
            string interception = string.IsNullOrWhiteSpace(nncr.NonconformanceInterception) ? "—" : nncr.NonconformanceInterception.Replace("; ", ", ");
            const string cell = "border:1px solid #444;padding:4px 8px;text-align:left;vertical-align:top";

            return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#241316"">
  <div style=""text-align:center;margin:8px 0 32px"">
    <img src=""{Encode(logoSrc)}"" alt=""LEAD Logistics Innovations, Inc."" width=""144"" height=""95"">
  </div>
  <p>Dear Sir/Ma'am,</p>
  <p>Good day.</p>
  <p>A <b>Notice of Non-Conformance (NNCR)</b> has been issued, and <b>{Encode(nncr.SupplierName)}</b> has been identified as the supplier responsible for the non-conformance cited in the report.</p>
  <ul>
    <li><b>No.:</b> {Encode(nncr.ReportNumber)}</li>
    <li><b>Date of Incident:</b> {nncr.DateOfIncident?.ToString("MMMM dd, yyyy")}</li>
    <li><b>Site Warehouse:</b> {Encode(nncr.SiteWarehouseName)}</li>
    <li><b>Response Due:</b> {nncr.DueDate?.ToString("MMMM dd, yyyy")} ({NncrDueDates.WorkingDaysToRespond} working days from filing)</li>
  </ul>
  <p><b>Details of Non-conformance:</b></p>
  <table style=""border-collapse:collapse;font-size:13px"">
    <tr><th style=""{cell}"">Type of Non-conformance</th><td style=""{cell}"">{Encode(types)}</td></tr>
    <tr><th style=""{cell}"">Interception</th><td style=""{cell}"">{Encode(interception)}</td></tr>
    <tr><th style=""{cell}"">Details</th><td style=""{cell}"">{EncodeMultiline(nncr.DetailsOfNonconformance)}</td></tr>
  </table>
  <p>Kindly review the report and submit your response using the button below. Please indicate whether you <b>approve</b> or <b>reject</b> the NNCR and include your <b>remarks / action taken</b>.</p>
  <p style=""margin:20px 0"">
    <a href=""{Encode(url)}"" style=""background:#7d1f27;color:#fff;padding:10px 22px;border-radius:4px;text-decoration:none;font-weight:bold;display:inline-block"">Respond</a>
  </p>
  <p style=""font-size:12px;color:#6c5a5c"">If the button does not work, copy and paste this link into your browser: <a href=""{Encode(url)}"">{Encode(url)}</a></p>
  <p>Thank you for your prompt attention to this matter.</p>
  <p style=""margin-top:32px"">Best regards,<br>Lead Logistics Team</p>
</div>";
        }
    }
}

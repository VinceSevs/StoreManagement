using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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

        // A file to send with the email: where it is on disk, and the name it should show (the uploaded name).
        public class EmailFile
        {
            public string Path { get; set; }
            public string DisplayName { get; set; }
        }

        // What a send did: who got the email and who did not (with why).
        public class SendResult
        {
            public List<string> Sent { get; } = new List<string>();
            public List<string> Failed { get; } = new List<string>(); // "address (reason)"
            public string Error { get; set; } // set when nothing could be attempted (no recipients / no SMTP setup)

            // Null when every recipient was emailed, otherwise why not.
            public string Problem =>
                Error ?? (Failed.Count == 0 ? null : "The email could not be sent to: " + string.Join("; ", Failed));
        }

        // logoUrl: absolute URL of Content/images/lead-logo.png, used when the email is shown
        // in the browser (the sent email embeds the logo instead). The preview shows the first
        // recipient's copy; every recipient gets the same email with their own link.
        public EmailMessage BuildSupplierEmail(NncrReport nncr, IList<string> recipients, string responseUrl, string logoUrl, IList<string> attachmentNames = null)
        {
            return new EmailMessage
            {
                To = string.Join("; ", recipients ?? new List<string>()),
                Subject = $"Action Required: NNCR {nncr.ReportNumber}",
                // The preview only lists the files by name (ContentId null).
                BodyHtml = BuildBody(nncr, responseUrl, logoUrl,
                    (attachmentNames ?? new List<string>()).Select(n => new BodyAttachment { Name = n }).ToList()),
                ResponseUrl = responseUrl
            };
        }

        // Emails each recipient separately, each with their own link (urlFor(address)).
        // One address failing does not stop the others. attachmentPaths: the NNCR's uploaded files,
        // attached to every recipient's email.
        public SendResult SendSupplierEmail(NncrReport nncr, IList<string> recipients, Func<string, string> urlFor, string logoPhysicalPath, IList<EmailFile> attachmentFiles = null)
        {
            var result = new SendResult();
            if (recipients == null || recipients.Count == 0)
            {
                result.Error = "The supplier has no email address on file.";
                return result;
            }

            string host = ConfigurationManager.AppSettings["Smtp:Host"];
            string from = ConfigurationManager.AppSettings["Smtp:From"];
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                result.Error = "Email is not set up yet (Smtp:Host / Smtp:From in Web.config).";
                return result;
            }

            int port;
            if (!int.TryParse(ConfigurationManager.AppSettings["Smtp:Port"], out port)) port = 587;
            bool enableSsl;
            if (!bool.TryParse(ConfigurationManager.AppSettings["Smtp:EnableSsl"], out enableSsl)) enableSsl = true;
            string user = ConfigurationManager.AppSettings["Smtp:User"];
            string password = ConfigurationManager.AppSettings["Smtp:Password"];
            string fromName = ConfigurationManager.AppSettings["Smtp:FromName"];

            var bodyAttachments = (attachmentFiles ?? new List<EmailFile>())
                .Select((f, i) => new BodyAttachment
                {
                    Name = f.DisplayName,
                    Path = f.Path,
                    ContentId = IsInlineImage(f.Path) ? "nncratt" + i : null
                })
                .ToList();

            using (var client = new SmtpClient(host, port) { EnableSsl = enableSsl })
            {
                if (!string.IsNullOrWhiteSpace(user))
                {
                    client.Credentials = new NetworkCredential(user, password);
                }

                foreach (string address in recipients)
                {
                    try
                    {
                        using (var mail = new MailMessage())
                        {
                            mail.From = string.IsNullOrWhiteSpace(fromName) ? new MailAddress(from) : new MailAddress(from, fromName);
                            mail.To.Add(address);
                            mail.Subject = $"Action Required: NNCR {nncr.ReportNumber}";

                            // Outlook doesn't show data: images, so the logo goes in as an embedded (cid:) image.
                            var view = AlternateView.CreateAlternateViewFromString(
                                BuildBody(nncr, urlFor(address), "cid:" + LogoContentId, bodyAttachments),
                                null, MediaTypeNames.Text.Html);
                            if (!string.IsNullOrWhiteSpace(logoPhysicalPath) && System.IO.File.Exists(logoPhysicalPath))
                            {
                                view.LinkedResources.Add(new LinkedResource(logoPhysicalPath, "image/png") { ContentId = LogoContentId });
                            }

                            // Images are shown in the body under the details table (embedded, so Outlook
                            // does not also list them at the top). Other files can't be shown inline, so they
                            // are attached. Fresh objects per message: the message disposes their file streams.
                            foreach (var item in bodyAttachments)
                            {
                                if (item.ContentId != null)
                                    view.LinkedResources.Add(new LinkedResource(item.Path, System.Web.MimeMapping.GetMimeMapping(item.Path)) { ContentId = item.ContentId });
                                else
                                    mail.Attachments.Add(new Attachment(item.Path) { Name = item.Name }); // shown under the uploaded name
                            }
                            mail.AlternateViews.Add(view);

                            client.Send(mail);
                        }
                        result.Sent.Add(address);
                        System.Diagnostics.Trace.TraceInformation($"[NNCR] {nncr.ReportNumber} emailed to {address}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError($"[NNCR] {nncr.ReportNumber} email to {address} failed: {ex}");
                        result.Failed.Add($"{address} ({ex.InnerException?.Message ?? ex.Message})");
                    }
                }
            }
            return result;
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value ?? "");

        // Keeps the line breaks typed into the details box.
        private static string EncodeMultiline(string value) => Encode(value).Replace("\r\n", "\n").Replace("\n", "<br>");

        // One uploaded file as it appears in the email body. ContentId is set for images that are
        // embedded in the body; other files (and the browser preview) are just listed by name.
        private class BodyAttachment
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string ContentId { get; set; }
        }

        // Largest size an embedded picture is shown at (pixels). Pictures are scaled down to fit, never up.
        private const int MaxImageWidth = 480;
        private const int MaxImageHeight = 320;

        // width / height attributes for the <img>: Outlook ignores max-width / max-height in CSS,
        // so the fitted size is also written as real attributes. Empty when the size can't be read.
        private static string FittedSizeAttributes(string path)
        {
            try
            {
                using (var image = System.Drawing.Image.FromFile(path))
                {
                    double scale = Math.Min(1.0, Math.Min((double)MaxImageWidth / image.Width, (double)MaxImageHeight / image.Height));
                    return $" width=\"{Math.Max(1, (int)Math.Round(image.Width * scale))}\" height=\"{Math.Max(1, (int)Math.Round(image.Height * scale))}\"";
                }
            }
            catch
            {
                return "";
            }
        }

        // Image types every mail client (Outlook included) can show embedded.
        private static bool IsInlineImage(string path)
        {
            string ext = (System.IO.Path.GetExtension(path) ?? "").ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp";
        }

        // The embedded pictures, right under the Details of Non-conformance table. No file names are
        // shown; files that can't be shown inline are simply attached to the email.
        private static string BuildAttachmentList(IList<BodyAttachment> items)
        {
            var pictures = (items ?? new List<BodyAttachment>()).Where(i => i.ContentId != null).ToList();
            if (pictures.Count == 0) return "";

            var html = new System.Text.StringBuilder();
            html.Append($@"
  <p style=""margin:16px 0 4px""><b>Attachments ({pictures.Count}):</b></p>");
            foreach (var item in pictures)
            {
                html.Append($@"
  <div style=""margin:0 0 12px""><img src=""cid:{item.ContentId}"" alt=""Attachment""{FittedSizeAttributes(item.Path)} style=""max-width:{MaxImageWidth}px;max-height:{MaxImageHeight}px;border:1px solid #ccc""></div>");
            }
            return html.ToString();
        }

        private static string BuildBody(NncrReport nncr, string url, string logoSrc, IList<BodyAttachment> attachments = null)
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
  </table>{BuildAttachmentList(attachments)}
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

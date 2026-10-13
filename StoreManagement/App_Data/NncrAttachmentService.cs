using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using StoreManagement.Models;

namespace IsseERP.Services
{
    // NNCR attachments live on disk only, in {root}/{ReportNumber}/ under their original
    // (sanitized) file names, the same way CAR attachments are kept in App_Data.
    public class NncrAttachmentService
    {
        public const int MaxFiles = 10;
        public const int MaxFileBytes = 10 * 1024 * 1024;
        // Keeps the whole request under IIS's default 30 MB request limit.
        public const int MaxTotalBytes = 25 * 1024 * 1024;

        private readonly string _rootPhysicalPath;

        // rootPhysicalPath: Server.MapPath("~/App_Data/NncrAttachments")
        public NncrAttachmentService(string rootPhysicalPath)
        {
            _rootPhysicalPath = rootPhysicalPath;
        }

        public void Save(string reportNumber, IEnumerable<HttpPostedFileBase> files)
        {
            var uploads = (files ?? Enumerable.Empty<HttpPostedFileBase>()).Where(f => f != null && f.ContentLength > 0).ToList();
            if (uploads.Count == 0) return;

            string folder = Path.Combine(_rootPhysicalPath, reportNumber);
            Directory.CreateDirectory(folder);

            foreach (var file in uploads)
            {
                string name = SafeFileName(file.FileName);
                string path = Path.Combine(folder, name);

                // Same name uploaded twice: "photo.jpg", "photo (2).jpg", ...
                int n = 2;
                while (File.Exists(path))
                {
                    path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({n++}){Path.GetExtension(name)}");
                }
                file.SaveAs(path);
            }
        }

        public List<NncrAttachment> List(string reportNumber)
        {
            string folder = Path.Combine(_rootPhysicalPath, reportNumber ?? "");
            if (string.IsNullOrWhiteSpace(reportNumber) || !Directory.Exists(folder)) return new List<NncrAttachment>();

            return Directory.GetFiles(folder)
                .OrderBy(f => f)
                .Select(f => new NncrAttachment
                {
                    FileName = Path.GetFileName(f),
                    Size = new FileInfo(f).Length,
                    ContentType = MimeMapping.GetMimeMapping(f)
                })
                .ToList();
        }

        // Full path of one attachment, or null if it does not exist. Only the bare file name
        // is used, so a request can't reach outside the NNCR's own folder.
        public string Resolve(string reportNumber, string fileName)
        {
            if (string.IsNullOrWhiteSpace(reportNumber) || string.IsNullOrWhiteSpace(fileName)) return null;
            string path = Path.Combine(_rootPhysicalPath, reportNumber, Path.GetFileName(fileName));
            return File.Exists(path) ? path : null;
        }

        private static string SafeFileName(string uploadedName)
        {
            string name = Path.GetFileName(uploadedName ?? "") ?? "";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(name) ? "attachment" : name;
        }
    }
}

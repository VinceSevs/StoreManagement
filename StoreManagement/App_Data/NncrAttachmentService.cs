using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
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

            // Saved under the NNCR's number, not the uploaded name: NNCR-0011_01.jpg, NNCR-0011_02.pdf, ...
            // (the original extension is kept). The numbering continues after any files already saved.
            // The name it was uploaded with is kept in a small names file in the folder, so the status page,
            // downloads and the email can still show the original.
            var names = ReadNames(folder);
            int index = Directory.GetFiles(folder).Count(f => Path.GetFileName(f) != NamesFile);
            foreach (var file in uploads)
            {
                string original = SafeFileName(file.FileName);
                string extension = Path.GetExtension(original).ToLowerInvariant();
                string path;
                do
                {
                    path = Path.Combine(folder, $"{SafeFileName(reportNumber)}_{++index:D2}{extension}");
                } while (File.Exists(path));
                file.SaveAs(path);
                names[Path.GetFileName(path)] = original;
            }
            File.WriteAllText(Path.Combine(folder, NamesFile), JsonConvert.SerializeObject(names));
        }

        // Disk name -> name it was uploaded with. Missing (e.g. older NNCRs): the disk name is the original.
        private const string NamesFile = ".nncr-names.json";

        private static Dictionary<string, string> ReadNames(string folder)
        {
            try
            {
                string path = Path.Combine(folder, NamesFile);
                if (File.Exists(path))
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path))
                           ?? new Dictionary<string, string>();
            }
            catch
            {
                // an unreadable names file just means the disk names are shown
            }
            return new Dictionary<string, string>();
        }

        // The uploaded name of a saved file (its disk name if none was recorded).
        public string DisplayNameOf(string reportNumber, string fileName)
        {
            string name;
            return ReadNames(Path.Combine(_rootPhysicalPath, reportNumber ?? "")).TryGetValue(fileName ?? "", out name)
                ? name : fileName;
        }

        public List<NncrAttachment> List(string reportNumber)
        {
            string folder = Path.Combine(_rootPhysicalPath, reportNumber ?? "");
            if (string.IsNullOrWhiteSpace(reportNumber) || !Directory.Exists(folder)) return new List<NncrAttachment>();

            var names = ReadNames(folder);
            return Directory.GetFiles(folder)
                .Where(f => Path.GetFileName(f) != NamesFile)
                .OrderBy(f => f)
                .Select(f => new NncrAttachment
                {
                    FileName = Path.GetFileName(f),
                    DisplayName = names.TryGetValue(Path.GetFileName(f), out var original) ? original : Path.GetFileName(f),
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
            if (Path.GetFileName(fileName) == NamesFile) return null;
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

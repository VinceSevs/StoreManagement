using IsseERP.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace IsseERP.Services
{
	public class CapaAttachmentFileService
	{
		private readonly string _rootPhysicalPath;

		// rootPhysicalPath should be an absolute disk path, e.g.
		// Server.MapPath("~/App_Data/CapaAttachments") — passed in from the
		// controller so this class has no dependency on System.Web.
		public CapaAttachmentFileService(string rootPhysicalPath)
		{
			_rootPhysicalPath = rootPhysicalPath;
		}

		/// <summary>
		/// Decodes each attachment's data URL and writes it to
		/// {root}/{capaNo}/attachment_NN.ext. Returns the relative path (not
		/// the full disk path) to store in the database, so the DB stays
		/// portable if the app ever moves servers or drives.
		/// </summary>
		public List<SavedAttachment> SaveAttachments(string capaNo, List<CapaAttachmentModel> attachments)
		{
			var results = new List<SavedAttachment>();
			if (attachments == null || attachments.Count == 0) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo);
			Directory.CreateDirectory(folderPath); // no-op if it already exists

			int i = 0;
			foreach (var attachment in attachments)
			{
				i++;
				if (attachment == null || string.IsNullOrWhiteSpace(attachment.DataUrl)) continue;

				string contentType;
				byte[] bytes = ParseDataUrl(attachment.DataUrl, out contentType);
				if (bytes == null) continue; // skip anything that isn't a valid data URL

				string extension = GetExtension(contentType);
				string fileName = $"attachment_{i:D2}{extension}";
				string fullPath = Path.Combine(folderPath, fileName);

				File.WriteAllBytes(fullPath, bytes);

				results.Add(new SavedAttachment
				{
					FilePath = (capaNo + "/" + fileName),
					Caption = attachment.Caption,
					ContentType = contentType
				});
			}

			return results;
		}

		/// <summary>
		/// Saves one item's photos into {root}/{capaNo}/Item{lineNumber}/attachment_NN.ext
		/// so multiple items on the same CAPA never collide with each other's files.
		/// Item 1 also gets written to the flat {root}/{capaNo}/ folder (via
		/// SaveAttachments) so existing single-item pages keep working unchanged.
		/// </summary>
		public List<SavedAttachment> SaveItemAttachments(string capaNo, int lineNumber, List<CapaAttachmentModel> attachments)
		{
			var results = new List<SavedAttachment>();
			if (attachments == null || attachments.Count == 0) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo, "Item" + lineNumber);
			Directory.CreateDirectory(folderPath);

			int i = 0;
			foreach (var attachment in attachments)
			{
				i++;
				if (attachment == null || string.IsNullOrWhiteSpace(attachment.DataUrl)) continue;

				string contentType;
				byte[] bytes = ParseDataUrl(attachment.DataUrl, out contentType);
				if (bytes == null) continue;

				string extension = GetExtension(contentType);
				string fileName = $"attachment_{i:D2}{extension}";
				string fullPath = Path.Combine(folderPath, fileName);

				File.WriteAllBytes(fullPath, bytes);

				results.Add(new SavedAttachment
				{
					FilePath = capaNo + "/Item" + lineNumber + "/" + fileName,
					Caption = attachment.Caption,
					ContentType = contentType
				});
			}

			return results;
		}

		/// <summary>
		/// Saves the respondent's optional supporting-document photos into the
		/// same CapaNo folder as the original filing, using a "response_NN"
		/// filename prefix so they never collide with the filer's existing
		/// "attachment_NN" files. Both show up together when
		/// GetAttachmentsByCapaNo lists the folder.
		/// </summary>
		public List<SavedAttachment> SaveResponseAttachments(string capaNo, List<CapaAttachmentModel> attachments)
		{
			var results = new List<SavedAttachment>();
			if (attachments == null || attachments.Count == 0) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo);
			Directory.CreateDirectory(folderPath);

			int i = 0;
			foreach (var attachment in attachments)
			{
				i++;
				if (attachment == null || string.IsNullOrWhiteSpace(attachment.DataUrl)) continue;

				string contentType;
				byte[] bytes = ParseDataUrl(attachment.DataUrl, out contentType);
				if (bytes == null) continue;

				string extension = GetExtension(contentType);
				string fileName = $"response_{i:D2}{extension}";
				string fullPath = Path.Combine(folderPath, fileName);

				File.WriteAllBytes(fullPath, bytes);

				results.Add(new SavedAttachment
				{
					FilePath = capaNo + "/" + fileName,
					Caption = attachment.Caption,
					ContentType = contentType
				});
			}

			return results;
		}

		/// <summary>
		/// Lists whatever image files already exist in this CAPA's folder,
		/// located purely by CapaNo — no database lookup involved, since
		/// attachments are only ever tracked on disk. Used when rendering a
		/// previously-saved CAPA back (e.g. the CapaResponse page).
		/// </summary>
		public List<SavedAttachment> GetAttachmentsByCapaNo(string capaNo)
		{
			var results = new List<SavedAttachment>();
			if (string.IsNullOrWhiteSpace(capaNo)) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo);
			if (!Directory.Exists(folderPath)) return results;

			foreach (var filePath in Directory.GetFiles(folderPath).OrderBy(f => f))
			{
				results.Add(new SavedAttachment
				{
					FilePath = capaNo + "/" + Path.GetFileName(filePath),
					ContentType = GetContentTypeFromExtension(Path.GetExtension(filePath)),
					// Captions typed in at filing time aren't persisted anywhere
					// right now (no DB record, and the filename alone can't hold
					// one) — always comes back null. See note below if this needs fixing.
					Caption = null
				});
			}

			return results;
		}

		/// <summary>
		/// Lists whatever image files already exist in one item's own
		/// {root}/{capaNo}/Item{lineNumber}/ subfolder — the counterpart to
		/// GetAttachmentsByCapaNo for items 2+ (item 1's photos live in the
		/// flat {root}/{capaNo}/ folder instead, via GetAttachmentsByCapaNo).
		/// </summary>
		public List<SavedAttachment> GetItemAttachmentsByCapaNo(string capaNo, int lineNumber)
		{
			var results = new List<SavedAttachment>();
			if (string.IsNullOrWhiteSpace(capaNo)) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo, "Item" + lineNumber);
			if (!Directory.Exists(folderPath)) return results;

			foreach (var filePath in Directory.GetFiles(folderPath).OrderBy(f => f))
			{
				results.Add(new SavedAttachment
				{
					FilePath = capaNo + "/Item" + lineNumber + "/" + Path.GetFileName(filePath),
					ContentType = GetContentTypeFromExtension(Path.GetExtension(filePath)),
					Caption = null
				});
			}

			return results;
		}

		/// <summary>
		/// Splits a browser data URL ("data:image/png;base64,AAAA...") into
		/// its content-type and raw bytes.
		/// </summary>
		private byte[] ParseDataUrl(string dataUrl, out string contentType)
		{
			contentType = "application/octet-stream";

			int commaIndex = dataUrl.IndexOf(',');
			if (commaIndex < 0) return null;

			string header = dataUrl.Substring(0, commaIndex); // "data:image/png;base64"
			string base64 = dataUrl.Substring(commaIndex + 1);

			int typeStart = header.IndexOf(':') + 1;
			int typeEnd = header.IndexOf(';');
			if (typeStart > 0 && typeEnd > typeStart)
			{
				contentType = header.Substring(typeStart, typeEnd - typeStart);
			}

			try
			{
				return Convert.FromBase64String(base64);
			}
			catch (FormatException)
			{
				return null;
			}
		}

		private string GetExtension(string contentType)
		{
			switch (contentType)
			{
				case "image/png": return ".png";
				case "image/jpeg": return ".jpg";
				case "image/webp": return ".webp";
				case "image/gif": return ".gif";
				case "image/heic": return ".heic";
				case "image/heif": return ".heif";
				case "application/pdf": return ".pdf";
				case "application/msword": return ".doc";
				case "application/vnd.openxmlformats-officedocument.wordprocessingml.document": return ".docx";
				case "application/vnd.ms-excel": return ".xls";
				case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": return ".xlsx";
				default: return ".bin";
			}
		}

		private string GetContentTypeFromExtension(string extension)
		{
			switch (extension.ToLowerInvariant())
			{
				case ".png": return "image/png";
				case ".jpg":
				case ".jpeg": return "image/jpeg";
				case ".webp": return "image/webp";
				case ".gif": return "image/gif";
				case ".heic": return "image/heic";
				case ".heif": return "image/heif";
				case ".pdf": return "application/pdf";
				case ".doc": return "application/msword";
				case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
				case ".xls": return "application/vnd.ms-excel";
				case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
				default: return "application/octet-stream";
			}
		}
	}
}
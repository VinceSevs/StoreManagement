using IsseERP.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
		/// {root}/{capaNo}/NonConformance_Item1_NN.ext (item 1's "Photo / Document
		/// Attachments" — the name says which field and item it came from). Returns the relative path (not
		/// the full disk path) to store in the database, so the DB stays
		/// portable if the app ever moves servers or drives.
		/// </summary>
		public List<SavedAttachment> SaveAttachments(string capaNo, List<CapaAttachmentModel> attachments)
		{
			return SaveItemAttachments(capaNo, 1, attachments);
		}

		/// <summary>
		/// Saves one item's photos into the CAR's single folder as
		/// {root}/{capaNo}/NonConformance_Item{lineNumber}_NN.ext. The item number
		/// in the filename keeps multiple items on the same CAPA from colliding.
		/// </summary>
		public List<SavedAttachment> SaveItemAttachments(string capaNo, int lineNumber, List<CapaAttachmentModel> attachments)
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
				string fileName = $"NonConformance_Item{lineNumber}_{i:D2}{extension}";
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
		/// Saves the respondent's optional files into the CAR's single folder as
		/// "response_R{round}_NN.ext", or "response_R{round}_{fieldKey}_NN.ext" when
		/// fieldKey is given (e.g. response_R2_RootCause_01.pdf). The name says
		/// which response round and which form field the file came from, so a
		/// later round (after a Not Effective verification) never overwrites an
		/// earlier one. round 1 is the first response.
		/// </summary>
		public List<SavedAttachment> SaveResponseAttachments(string capaNo, List<CapaAttachmentModel> attachments, int round, string fieldKey = null)
		{
			var results = new List<SavedAttachment>();
			if (attachments == null || attachments.Count == 0) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo);
			Directory.CreateDirectory(folderPath);

			string prefix = $"response_R{round}_" + (string.IsNullOrWhiteSpace(fieldKey) ? "" : fieldKey + "_");

			int i = 0;
			foreach (var attachment in attachments)
			{
				i++;
				if (attachment == null || string.IsNullOrWhiteSpace(attachment.DataUrl)) continue;

				string contentType;
				byte[] bytes = ParseDataUrl(attachment.DataUrl, out contentType);
				if (bytes == null) continue;

				string extension = GetExtension(contentType);
				string fileName = $"{prefix}{i:D2}{extension}";
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
		/// Lists the respondent's files from one response round
		/// ("response_R{round}_*"), for the loop history. Files saved before the
		/// round was added to the name have no round and aren't listed here.
		/// </summary>
		public List<SavedAttachment> GetResponseAttachmentsForRound(string capaNo, int round)
		{
			var results = new List<SavedAttachment>();
			if (string.IsNullOrWhiteSpace(capaNo)) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo);
			if (!Directory.Exists(folderPath)) return results;

			string prefix = $"response_R{round}_";
			foreach (var filePath in Directory.GetFiles(folderPath).OrderBy(f => f))
			{
				string name = Path.GetFileName(filePath);
				if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

				results.Add(new SavedAttachment
				{
					FilePath = capaNo + "/" + name,
					ContentType = GetContentTypeFromExtension(Path.GetExtension(filePath)),
					Caption = null
				});
			}
			return results;
		}

		private static readonly Regex ItemFilePattern =
			new Regex(@"^NonConformance_Item(\d+)_", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Lists the files that belong to one item. Every file of a CAR lives in
		/// the single {root}/{capaNo}/ folder; the filer's files for item N are
		/// "NonConformance_ItemN_NN.ext". Item 1 also lists the files that don't
		/// belong to a specific other item — the respondent's "response_*" files
		/// and CARs filed before the rename ("attachment_NN") — so those keep
		/// showing exactly where they did. Items 2+ also read the legacy
		/// {capaNo}/Item{N}/ subfolder older CARs were saved to.
		/// </summary>
		public List<SavedAttachment> GetItemAttachmentsByCapaNo(string capaNo, int lineNumber)
		{
			var results = new List<SavedAttachment>();
			if (string.IsNullOrWhiteSpace(capaNo)) return results;

			string folderPath = Path.Combine(_rootPhysicalPath, capaNo);
			if (Directory.Exists(folderPath))
			{
				foreach (var filePath in Directory.GetFiles(folderPath).OrderBy(f => f))
				{
					string name = Path.GetFileName(filePath);
					var match = ItemFilePattern.Match(name);
					bool belongsHere = match.Success
						? int.Parse(match.Groups[1].Value) == lineNumber
						: lineNumber == 1;
					if (!belongsHere) continue;

					results.Add(new SavedAttachment
					{
						FilePath = capaNo + "/" + name,
						ContentType = GetContentTypeFromExtension(Path.GetExtension(filePath)),
						Caption = null
					});
				}
			}

			if (lineNumber > 1)
			{
				string legacyFolder = Path.Combine(folderPath, "Item" + lineNumber);
				if (Directory.Exists(legacyFolder))
				{
					foreach (var filePath in Directory.GetFiles(legacyFolder).OrderBy(f => f))
					{
						results.Add(new SavedAttachment
						{
							FilePath = capaNo + "/Item" + lineNumber + "/" + Path.GetFileName(filePath),
							ContentType = GetContentTypeFromExtension(Path.GetExtension(filePath)),
							Caption = null
						});
					}
				}
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
using IsseERP.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IsseERP.Services
{
	/// <summary>
	/// History of the response rounds QA sent back (verification = Not Effective).
	/// Kept on disk, one JSON file per round, under
	/// {root}/{capaNo}/Round{NN}.json — no database table. The root is a different
	/// folder from the CAR attachments, so these files never show up in an
	/// attachment list or through the attachment download action.
	/// </summary>
	public class CapaLoopHistoryStore
	{
		private readonly string _rootPhysicalPath;

		public CapaLoopHistoryStore(string rootPhysicalPath)
		{
			_rootPhysicalPath = rootPhysicalPath;
		}

		private string FilePathFor(string capaNo, int round)
		{
			return Path.Combine(_rootPhysicalPath, capaNo, $"Round{round:D2}.json");
		}

		/// <summary>
		/// Writes one round's snapshot. Throws if it can't be written, so the caller can
		/// stop before the response is cleared and the earlier round is lost.
		/// </summary>
		public void Save(string capaNo, CapaLoopHistoryEntry entry)
		{
			string path = FilePathFor(capaNo, entry.RoundNumber);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, JsonConvert.SerializeObject(entry, Formatting.Indented), Encoding.UTF8);
		}

		/// <summary>Removes a round's snapshot (used when the send-back that triggered it failed).</summary>
		public void Delete(string capaNo, int round)
		{
			string path = FilePathFor(capaNo, round);
			if (File.Exists(path)) File.Delete(path);
		}

		/// <summary>
		/// All saved rounds for a CAR, oldest first. A missing folder gives an empty
		/// list, and an unreadable file is skipped, so the status page never breaks
		/// over the history.
		/// </summary>
		public List<CapaLoopHistoryEntry> GetForReport(string capaNo)
		{
			var list = new List<CapaLoopHistoryEntry>();
			if (string.IsNullOrWhiteSpace(capaNo)) return list;

			string folder = Path.Combine(_rootPhysicalPath, capaNo);
			if (!Directory.Exists(folder)) return list;

			foreach (var file in Directory.GetFiles(folder, "Round*.json").OrderBy(f => f))
			{
				try
				{
					var entry = JsonConvert.DeserializeObject<CapaLoopHistoryEntry>(File.ReadAllText(file, Encoding.UTF8));
					if (entry != null) list.Add(entry);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Trace.TraceError("CAR loop history read failed for " + file + ": " + ex.Message);
				}
			}

			return list.OrderBy(e => e.RoundNumber).ToList();
		}
	}
}

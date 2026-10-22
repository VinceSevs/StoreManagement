/* NNCR activity log: Export to Excel and Download PDF (status page).
   Data comes from window.NNCR_LOG = { reportNumber, supplier, entries: [{ date, user, action, description }] }. */
(function () {
	var data = window.NNCR_LOG || { reportNumber: '', supplier: '', entries: [] };

	function safe(s) { return String(s || '').replace(/[^A-Za-z0-9-]+/g, '-'); }
	function escapeHtml(s) {
		var map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', '\'': '&#39;' };
		return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return map[c]; });
	}
	function notify(icon, title, text) {
		if (typeof Swal !== 'undefined') Swal.fire({ icon: icon, title: title, text: text, confirmButtonColor: '#c60404' });
		else alert(title + (text ? '\n' + text : ''));
	}
	function hasEntries() {
		if (data.entries && data.entries.length) return true;
		notify('info', 'Nothing to export', 'This NNCR has no activity log entries yet.');
		return false;
	}

	function exportExcel() {
		if (!hasEntries()) return;
		if (typeof XLSX === 'undefined') { notify('error', 'Export unavailable', 'The Excel library could not be loaded. Check your connection and try again.'); return; }

		var rows = data.entries.map(function (e) { return [e.date, e.user, e.action, e.description]; });
		var ws = XLSX.utils.aoa_to_sheet([['Date & Time', 'User', 'Action', 'Description']].concat(rows));
		ws['!cols'] = [{ wch: 22 }, { wch: 26 }, { wch: 16 }, { wch: 70 }];
		var wb = XLSX.utils.book_new();
		XLSX.utils.book_append_sheet(wb, ws, 'Activity Log');
		XLSX.writeFile(wb, safe(data.reportNumber) + '-Activity-Log.xlsx');
	}

	function setPdfButton(btn, busy) {
		btn.disabled = busy;
		btn.innerHTML = busy ? 'Generating...' : '<i class="fa fa-download"></i> Download PDF';
	}

	function downloadPdf() {
		var btn = document.getElementById('logPdfBtn');
		if (!btn || !hasEntries()) return;
		if (typeof html2pdf === 'undefined') { notify('error', 'PDF unavailable', 'The PDF library could not be loaded. Check your connection and try again.'); return; }

		setPdfButton(btn, true);

		var th = 'padding:6px 8px;border:1px solid #999;background:#f3f4f6;text-align:left;font-size:11px;';
		var td = 'padding:6px 8px;border:1px solid #bbb;font-size:11px;vertical-align:top;';
		var body = data.entries.map(function (e) {
			return '<tr>' +
				'<td style="' + td + 'white-space:nowrap;">' + escapeHtml(e.date) + '</td>' +
				'<td style="' + td + '">' + escapeHtml(e.user) + '</td>' +
				'<td style="' + td + 'white-space:nowrap;">' + escapeHtml(e.action) + '</td>' +
				'<td style="' + td + '">' + escapeHtml(e.description) + '</td></tr>';
		}).join('');

		var sheet = document.createElement('div');
		sheet.style.cssText = 'width:7.6in;padding:8px;background:#fff;color:#17191D;font-family:"Segoe UI",Arial,sans-serif;';
		sheet.innerHTML =
			'<div style="font-size:11px;color:#c60404;font-weight:700;letter-spacing:1px;">NNCR ACTIVITY LOG</div>' +
			'<h2 style="margin:2px 0 4px;font-size:20px;">Reference: ' + escapeHtml(data.reportNumber) + '</h2>' +
			'<div style="font-size:11px;color:#555;margin-bottom:12px;">' +
			(data.supplier ? 'Supplier: ' + escapeHtml(data.supplier) + ' &middot; ' : '') +
			data.entries.length + ' entr' + (data.entries.length === 1 ? 'y' : 'ies') +
			' &middot; Generated ' + escapeHtml(new Date().toLocaleString()) + '</div>' +
			'<table style="width:100%;border-collapse:collapse;table-layout:fixed;">' +
			'<colgroup><col style="width:1.45in"><col style="width:1.3in"><col style="width:1in"><col></colgroup>' +
			'<thead><tr><th style="' + th + '">Date &amp; Time</th><th style="' + th + '">User</th><th style="' + th + '">Action</th><th style="' + th + '">Description</th></tr></thead>' +
			'<tbody>' + body + '</tbody></table>';
		document.body.appendChild(sheet);

		var opt = {
			margin: 0.4,
			filename: safe(data.reportNumber) + '-Activity-Log.pdf',
			image: { type: 'jpeg', quality: 0.98 },
			html2canvas: { scale: 2, useCORS: true },
			jsPDF: { unit: 'in', format: 'letter', orientation: 'portrait' },
			pagebreak: { mode: ['css', 'legacy'], avoid: ['tr'] }
		};
		html2pdf().set(opt).from(sheet).save().then(function () {
			sheet.remove();
			setPdfButton(btn, false);
		}).catch(function (err) {
			sheet.remove();
			setPdfButton(btn, false);
			console.error('Log PDF generation failed:', err);
			notify('error', 'Could not generate PDF', err && err.message ? err.message : String(err));
		});
	}

	function init() {
		var x = document.getElementById('logExcelBtn');
		if (x) x.addEventListener('click', exportExcel);
		var p = document.getElementById('logPdfBtn');
		if (p) p.addEventListener('click', downloadPdf);
	}

	if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
	else init();
})();

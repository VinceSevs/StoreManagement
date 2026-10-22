// NNCR Status page (Views/Nncr/NncrStatus.cshtml) - QA view; role 49 can also close the NNCR here.
// Config: window.NNCR_STATUS_DATA = { reportNumber, statusLabel, responseToken, closeEndpoint,
//   email: { To, Subject, BodyHtml, ResponseUrl } }
(function () {
	'use strict';

	var CONFIG = window.NNCR_STATUS_DATA || {};
	var BRAND_COLOR = '#c60404';

	function escapeHtml(str) {
		return String(str == null ? '' : str).replace(/[&<>"']/g, function (c) {
			return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
		});
	}

	// ---------- Supplier email: preview, and send again while awaiting a response ----------
	async function sendSupplierEmail() {
		Swal.fire({ title: 'Sending email…', allowOutsideClick: false, didOpen: function () { Swal.showLoading(); } });
		try {
			var response = await fetch(CONFIG.sendEmailEndpoint, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
				body: JSON.stringify({ ResponseToken: CONFIG.responseToken })
			});
			var result = await response.json().catch(function () { return null; });
			if (response.ok && result && result.success) {
				Swal.fire({ icon: 'success', title: 'Email sent', text: 'Sent to ' + result.to + '.', confirmButtonColor: BRAND_COLOR });
			} else {
				Swal.fire({ icon: 'error', title: 'Email not sent', text: (result && result.message) || ('HTTP ' + response.status), confirmButtonColor: BRAND_COLOR });
			}
		} catch (err) {
			Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: BRAND_COLOR });
		}
	}

	function showSupplierEmail() {
		var email = CONFIG.email || {};
		var to = email.To
			? '<span class="font-mono">' + escapeHtml(email.To) + '</span>'
			: '<span class="text-danger">No email on file for this supplier</span>';

		Swal.fire({
			title: 'Email to supplier',
			width: 720,
			html:
				'<div class="text-start small">' +
				'<div class="border rounded">' +
				'<div class="border-bottom px-3 py-2 bg-light"><div><b>To:</b> ' + to + '</div><div><b>Subject:</b> ' + escapeHtml(email.Subject) + '</div></div>' +
				// BodyHtml is built and HTML-encoded on the server (NncrNotificationService).
				'<div class="px-3 py-2">' + (email.BodyHtml || '') + '</div>' +
				'</div></div>',
			showConfirmButton: !!CONFIG.canSendEmail && !!email.To,
			showDenyButton: true,
			showCancelButton: true,
			confirmButtonText: '<i class="fa fa-paper-plane-o"></i> Send email',
			denyButtonText: '<i class="fa fa-external-link"></i> Open response form',
			cancelButtonText: 'Close',
			confirmButtonColor: BRAND_COLOR,
			denyButtonColor: '#6c757d',
			preDeny: function () {
				if (email.ResponseUrl) window.open(email.ResponseUrl, '_blank', 'noopener');
				return false;
			}
		}).then(function (r) {
			if (r.isConfirmed) sendSupplierEmail();
		});
	}

	// ---------- Close NNCR (role 49, after the supplier responded) ----------
	async function closeNncr() {
		var btn = document.getElementById('closeNncrBtn');
		var confirm = await Swal.fire({
			icon: 'question',
			title: 'Close ' + (CONFIG.reportNumber || 'this NNCR') + '?',
			text: 'Closing marks this NNCR as done. It cannot be reopened.',
			showCancelButton: true,
			confirmButtonText: 'Close NNCR',
			cancelButtonText: 'Cancel',
			confirmButtonColor: BRAND_COLOR,
			reverseButtons: true
		});
		if (!confirm.isConfirmed) return;

		btn.disabled = true;
		try {
			var response = await fetch(CONFIG.closeEndpoint, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
				body: JSON.stringify({ ResponseToken: CONFIG.responseToken })
			});
			var result = await response.json().catch(function () { return null; });
			if (response.ok && result && result.success) {
				await Swal.fire({ icon: 'success', title: 'NNCR closed', confirmButtonColor: BRAND_COLOR });
				location.reload();
				return;
			}
			await Swal.fire({ icon: 'error', title: 'Could not close the NNCR', text: (result && result.message) || ('HTTP ' + response.status), confirmButtonColor: BRAND_COLOR });
		} catch (err) {
			await Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: BRAND_COLOR });
		}
		btn.disabled = false;
	}

	// ---------- Download PDF (html2pdf, like CAPA status) ----------
	function setPdfButton(btn, busy) {
		btn.disabled = busy;
		btn.innerHTML = busy ? 'Generating...' : '<i class="fa fa-download"></i> Download PDF';
	}

	function downloadPdf() {
		var btn = document.getElementById('downloadPdfBtn');
		var source = document.getElementById('nncrStatusPrintArea');
		if (!btn || !source || typeof html2pdf === 'undefined') return;

		setPdfButton(btn, true);

		var clone = source.cloneNode(true);
		clone.removeAttribute('id');
		clone.querySelectorAll('[data-pdf-exclude], .no-print').forEach(function (el) { el.remove(); });
		clone.querySelectorAll('.main-view-field dd').forEach(function (el) {
			el.style.backgroundColor = '#fff';
			el.style.border = '1px solid #999';
		});
		document.body.appendChild(clone);

		var safe = function (s) { return String(s || '').replace(/[^A-Za-z0-9-]+/g, '-'); };
		var opt = {
			margin: 0.4,
			filename: 'NNCR-Status-' + safe(CONFIG.reportNumber) + '-' + safe(CONFIG.statusLabel) + '.pdf',
			image: { type: 'jpeg', quality: 0.98 },
			html2canvas: { scale: 2, useCORS: true },
			jsPDF: { unit: 'in', format: 'letter', orientation: 'portrait' },
			pagebreak: { mode: ['avoid-all', 'css'], avoid: ['.nncr-card'] }
		};

		html2pdf().set(opt).from(clone).save().then(function () {
			clone.remove();
			setPdfButton(btn, false);
		}).catch(function (err) {
			clone.remove();
			setPdfButton(btn, false);
			console.error('PDF generation failed:', err);
			alert('Could not generate PDF: ' + (err && err.message ? err.message : err));
		});
	}

	function init() {
		var pdfBtn = document.getElementById('downloadPdfBtn');
		if (pdfBtn) pdfBtn.addEventListener('click', downloadPdf);

		var emailBtn = document.getElementById('supplierEmailBtn');
		if (emailBtn) emailBtn.addEventListener('click', showSupplierEmail);

		var closeBtn = document.getElementById('closeNncrBtn');
		if (closeBtn) closeBtn.addEventListener('click', closeNncr);
	}

	if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
	else init();
})();

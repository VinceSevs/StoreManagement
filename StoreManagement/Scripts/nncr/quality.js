/* NNCR filing form (Views/Nncr/Quality.cshtml).
   Mirrors Scripts/capa/capa-form.js: required-field checks, SweetAlert2
   confirm / success / error, button disabled while saving.
   Endpoints come from window.NNCR_QUALITY_CONFIG (set inline in the view). */

var NNCR_MAX_CHECKLIST_LENGTH = 500;
var NNCR_BRAND_COLOR = '#7d1f27';

function nncrConfig() {
	return window.NNCR_QUALITY_CONFIG || {};
}

function escapeHtml(str) {
	return String(str == null ? '' : str).replace(/[&<>"']/g, function (c) {
		return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
	});
}

function highlightMatch(text, query) {
	var idx = text.toLowerCase().indexOf(query.toLowerCase());
	if (!query || idx === -1) return escapeHtml(text);
	return escapeHtml(text.slice(0, idx)) + '<mark>' + escapeHtml(text.slice(idx, idx + query.length)) + '</mark>' + escapeHtml(text.slice(idx + query.length));
}

/* ---------- Defaults ---------- */
function toIsoDate(date) {
	return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0');
}

function setDefaultDate() {
	var input = document.getElementById('incidentDateInput');
	var today = toIsoDate(new Date());
	input.value = today;
	input.max = today; // an incident can't be in the future
}

/* ---------- Response due date (mirrors NncrDueDates.AddWorkingDays on the server) ---------- */
function addWorkingDays(start, workingDays) {
	var date = new Date(start.getFullYear(), start.getMonth(), start.getDate());
	while (workingDays > 0) {
		date.setDate(date.getDate() + 1);
		var day = date.getDay();
		if (day !== 0 && day !== 6) workingDays--;
	}
	return date;
}

function setDueDate() {
	var due = addWorkingDays(new Date(), nncrConfig().workingDaysToRespond || 7);
	document.getElementById('dueDateInput').value = due.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' });
}

/* ---------- Attachments ---------- */
// Files picked so far; picking again adds to the list instead of replacing it.
var selectedFiles = [];

function formatBytes(bytes) {
	if (bytes < 1024) return bytes + ' B';
	if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
	return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

function initAttachments() {
	var input = document.getElementById('attachmentsInput');
	input.addEventListener('change', function () {
		Array.from(input.files).forEach(function (file) {
			var duplicate = selectedFiles.some(function (f) { return f.name === file.name && f.size === file.size; });
			if (!duplicate) selectedFiles.push(file);
		});
		input.value = ''; // allow picking the same file again after removing it
		renderAttachments();
	});

	document.getElementById('attachmentsList').addEventListener('click', function (e) {
		var btn = e.target.closest('[data-remove-index]');
		if (!btn) return;
		selectedFiles.splice(Number(btn.dataset.removeIndex), 1);
		renderAttachments();
	});
}

function renderAttachments() {
	var cfg = nncrConfig();
	document.getElementById('attachmentsList').innerHTML = selectedFiles.map(function (f, i) {
		var tooBig = f.size > cfg.maxFileBytes;
		return '<li class="list-group-item d-flex align-items-center gap-2 px-0">' +
			'<i class="bi bi-paperclip text-secondary"></i>' +
			'<span class="text-truncate">' + escapeHtml(f.name) + '</span>' +
			'<span class="small ' + (tooBig ? 'text-danger fw-semibold' : 'text-secondary') + ' text-nowrap">' + formatBytes(f.size) + (tooBig ? ' &mdash; too large' : '') + '</span>' +
			'<button type="button" class="btn btn-sm btn-link text-danger ms-auto d-print-none" data-remove-index="' + i + '" title="Remove">' +
			'<i class="bi bi-x-lg"></i></button>' +
			'</li>';
	}).join('');
}

function totalAttachmentBytes() {
	return selectedFiles.reduce(function (sum, f) { return sum + f.size; }, 0);
}

/* ---------- Site Warehouse code auto-fill ---------- */
function handleWarehouseChange() {
	var sel = document.getElementById('siteWarehouseSelect');
	var opt = sel.selectedIndex >= 0 ? sel.options[sel.selectedIndex] : null;
	document.getElementById('warehouseCodeInput').value = (opt && opt.dataset.code) ? opt.dataset.code : '';
}

/* ---------- Checkboxes with a detail text box ---------- */
function initDetailCheckboxes() {
	document.querySelectorAll('input[type="checkbox"][data-detail]').forEach(function (cb) {
		cb.addEventListener('change', function () { syncDetailInput(cb, true); });
	});
}

function syncDetailInput(cb, focus) {
	var input = document.getElementById(cb.dataset.detail);
	if (!input) return;
	input.classList.toggle('d-none', !cb.checked);
	input.required = cb.checked && cb.dataset.detailRequired === 'true';
	if (!cb.checked) input.value = '';
	else if (focus) input.focus();
}

function detailText(cb) {
	var input = cb.dataset.detail ? document.getElementById(cb.dataset.detail) : null;
	return input ? input.value.replace(/\s+/g, ' ').trim() : '';
}

// "Quality Issues; Expired items; Others: torn label"
function serializeNonconformanceType() {
	return Array.from(document.querySelectorAll('input[name="nc_type"]:checked')).map(function (cb) {
		if (cb.value === 'Others') return 'Others: ' + detailText(cb);
		return cb.value;
	}).join('; ');
}

// "Receiving: dock 3; Storage; Others: during cycle count"
function serializeInterception() {
	return Array.from(document.querySelectorAll('input[name="interception"]:checked')).map(function (cb) {
		var detail = detailText(cb);
		return detail ? cb.value + ': ' + detail : cb.value;
	}).join('; ');
}

/* ---------- Supplier search (Issued To) ---------- */
// Filters the supplier list from NNCR_QUALITY_CONFIG.suppliers client-side; the picked VendorID goes in #issuedToId.
var supplierMatches = [];
var supplierActiveIndex = -1;

function initSupplierSearch() {
	var input = document.getElementById('supplierSearchInput');
	var dropdown = document.getElementById('supplierDropdown');

	input.addEventListener('input', function () {
		document.getElementById('issuedToId').value = ''; // typing invalidates the previous pick
		showSupplierEmail(null);
		renderSupplierMatches(input.value.trim());
	});
	input.addEventListener('focus', function () { renderSupplierMatches(input.value.trim()); });

	input.addEventListener('keydown', function (e) {
		if (!dropdown.classList.contains('open') || supplierMatches.length === 0) return;
		if (e.key === 'ArrowDown') {
			e.preventDefault();
			setSupplierActive(Math.min(supplierActiveIndex + 1, supplierMatches.length - 1));
		} else if (e.key === 'ArrowUp') {
			e.preventDefault();
			setSupplierActive(Math.max(supplierActiveIndex - 1, 0));
		} else if (e.key === 'Enter') {
			e.preventDefault();
			selectSupplier(supplierMatches[supplierActiveIndex >= 0 ? supplierActiveIndex : 0]);
		} else if (e.key === 'Escape') {
			closeSupplierDropdown();
		}
	});

	dropdown.addEventListener('mousedown', function (e) {
		var item = e.target.closest('.list-group-item[data-index]');
		if (!item) return;
		e.preventDefault();
		selectSupplier(supplierMatches[Number(item.dataset.index)]);
	});

	document.addEventListener('click', function (e) {
		if (!e.target.closest('#supplierSearchWrap')) {
			closeSupplierDropdown();
			// leaving the box without picking a supplier clears the half-typed text
			if (!document.getElementById('issuedToId').value) input.value = '';
		}
	});
}

var SUPPLIER_MAX_RESULTS = 10;

function renderSupplierMatches(term) {
	var dropdown = document.getElementById('supplierDropdown');
	if (!term) { closeSupplierDropdown(); return; } // only list suppliers once the user types

	var q = term.toLowerCase();
	var all = (nncrConfig().suppliers || [])
		.filter(function (s) { return String(s.name || '').toLowerCase().indexOf(q) !== -1; });
	supplierMatches = all.slice(0, SUPPLIER_MAX_RESULTS);
	supplierActiveIndex = -1;

	if (supplierMatches.length === 0) {
		dropdown.innerHTML = '<div class="list-group-item small text-secondary fst-italic">No matching supplier.</div>';
	} else {
		dropdown.innerHTML = supplierMatches.map(function (s, i) {
			return '<button type="button" class="list-group-item list-group-item-action" data-index="' + i + '">' +
				highlightMatch(String(s.name || ''), term) + '</button>';
		}).join('') +
			(all.length > SUPPLIER_MAX_RESULTS
				? '<div class="list-group-item small text-secondary fst-italic">Showing ' + SUPPLIER_MAX_RESULTS + ' of ' + all.length + ' &mdash; keep typing to narrow down.</div>'
				: '');
	}
	dropdown.classList.add('open');
}

function setSupplierActive(index) {
	var items = document.querySelectorAll('#supplierDropdown .list-group-item[data-index]');
	items.forEach(function (el, i) { el.classList.toggle('active', i === index); });
	supplierActiveIndex = index;
	if (items[index]) items[index].scrollIntoView({ block: 'nearest' });
}

function selectSupplier(supplier) {
	if (!supplier) return;
	document.getElementById('supplierSearchInput').value = supplier.name;
	document.getElementById('issuedToId').value = supplier.id;
	showSupplierEmail(supplier);
	closeSupplierDropdown();
}

// The NNCR is emailed to this address (tbl_Vendor.EmailAddress).
function showSupplierEmail(supplier) {
	var hint = document.getElementById('supplierEmailHint');
	if (!supplier) { hint.innerHTML = ''; return; }
	hint.innerHTML = supplier.email
		? '<i class="bi bi-envelope"></i> Will be sent to <span class="font-mono">' + escapeHtml(supplier.email) + '</span>'
		: '<span class="text-danger"><i class="bi bi-exclamation-triangle"></i> This supplier has no email on file.</span>';
}

function closeSupplierDropdown() {
	var dropdown = document.getElementById('supplierDropdown');
	dropdown.classList.remove('open');
	dropdown.innerHTML = '';
	supplierActiveIndex = -1;
}

/* ---------- Payload ---------- */
// multipart/form-data so the attachments go up with the form fields.
function buildNncrPayload() {
	var val = function (id) { var el = document.getElementById(id); return el ? el.value.trim() : ''; };
	var data = new FormData();
	data.append('IssuedToSupplier', Number(val('issuedToId')) || 0);
	data.append('SiteWarehouse', Number(val('siteWarehouseSelect')) || 0);
	data.append('NonconformanceType', serializeNonconformanceType());
	data.append('NonconformanceInterception', serializeInterception());
	data.append('DetailsOfNonconformance', val('detailsInput'));
	data.append('QARemarks', val('qaRemarksInput'));
	if (val('incidentDateInput')) data.append('DateOfIncident', val('incidentDateInput'));
	selectedFiles.forEach(function (file) { data.append('attachments', file, file.name); });
	return data;
}

/* ---------- Required-field validation ---------- */
function getRequiredFieldChecks() {
	var val = function (id) { var el = document.getElementById(id); return el ? el.value.trim() : ''; };
	var checked = function (id) { var el = document.getElementById(id); return !!(el && el.checked); };
	var cfg = nncrConfig();

	return [
		{ test: function () { return !!val('incidentDateInput'); }, label: 'Date of Incident' },
		{ test: function () { return !!val('issuedToId'); }, label: 'Issued To (Supplier Name) &mdash; pick a supplier from the list' },
		{ test: function () { return !!val('siteWarehouseSelect'); }, label: 'Site Warehouse' },
		{ test: function () { return !!document.querySelector('input[name="nc_type"]:checked'); }, label: 'Type of Non-conformance (tick at least one)' },
		{ test: function () { return !checked('ncTypeOthers') || !!val('ncTypeOthersText'); }, label: 'Type of Non-conformance &mdash; specify &ldquo;Others&rdquo;' },
		{ test: function () { return serializeNonconformanceType().length <= NNCR_MAX_CHECKLIST_LENGTH; }, label: 'Type of Non-conformance is too long (max ' + NNCR_MAX_CHECKLIST_LENGTH + ' characters including the details)' },
		{ test: function () { return !!document.querySelector('input[name="interception"]:checked'); }, label: 'Non-conformance Interception (tick at least one)' },
		{ test: function () { return !checked('icOthers') || !!val('icOthersText'); }, label: 'Non-conformance Interception &mdash; specify &ldquo;Others&rdquo;' },
		{ test: function () { return serializeInterception().length <= NNCR_MAX_CHECKLIST_LENGTH; }, label: 'Non-conformance Interception is too long (max ' + NNCR_MAX_CHECKLIST_LENGTH + ' characters including the details)' },
		{ test: function () { return !!val('detailsInput'); }, label: 'Details of the Non-conformance' },
		{ test: function () { return selectedFiles.length <= cfg.maxFiles; }, label: 'Attachments &mdash; up to ' + cfg.maxFiles + ' files only' },
		{ test: function () { return selectedFiles.every(function (f) { return f.size <= cfg.maxFileBytes; }); }, label: 'Attachments &mdash; remove the files marked too large (max ' + formatBytes(cfg.maxFileBytes) + ' each)' },
		{ test: function () { return totalAttachmentBytes() <= cfg.maxTotalBytes; }, label: 'Attachments &mdash; total size is over ' + formatBytes(cfg.maxTotalBytes) }
	];
}

/* ---------- Submit ---------- */
function setSaving(saving) {
	var btn = document.getElementById('submitBtn');
	btn.disabled = saving;
	btn.textContent = saving ? 'Saving…' : 'Submit NNCR';
}

async function submitNncrForm() {
	var statusEl = document.getElementById('submitStatus');

	var missing = getRequiredFieldChecks().filter(function (c) { return !c.test(); }).map(function (c) { return c.label; });
	if (missing.length > 0) {
		Swal.fire({
			icon: 'warning',
			title: 'Please complete the form',
			// labels are static strings defined above (no user text)
			html: '<ul style="text-align:left;margin:0;padding-left:1.2rem">' + missing.map(function (m) { return '<li>' + m + '</li>'; }).join('') + '</ul>',
			confirmButtonColor: NNCR_BRAND_COLOR
		});
		return;
	}

	var payload = buildNncrPayload();
	var supplierName = document.getElementById('supplierSearchInput').value;

	var confirm = await Swal.fire({
		icon: 'question',
		title: 'Submit this NNCR?',
		html: 'Issued to <b>' + escapeHtml(supplierName) + '</b>.<br>It will be sent directly to the supplier, with a response due on <b>' +
			escapeHtml(document.getElementById('dueDateInput').value) + '</b>.',
		showCancelButton: true,
		confirmButtonText: 'Submit',
		cancelButtonText: 'Cancel',
		confirmButtonColor: NNCR_BRAND_COLOR,
		reverseButtons: true
	});
	if (!confirm.isConfirmed) return;

	setSaving(true);
	statusEl.textContent = '';
	statusEl.className = 'me-auto small fw-semibold';

	try {
		// No Content-Type header: the browser sets the multipart boundary for FormData.
		var response = await fetch(nncrConfig().saveUrl, {
			method: 'POST',
			headers: { 'Accept': 'application/json' },
			body: payload
		});
		var result = await response.json().catch(function () { return null; });

		if (response.ok && result && result.success) {
			statusEl.textContent = 'Saved — ' + result.reportNumber;
			statusEl.className = 'me-auto small fw-semibold text-success';

			if (result.attachmentError) {
				await Swal.fire({ icon: 'warning', title: 'Attachments not saved', text: result.attachmentError, confirmButtonColor: NNCR_BRAND_COLOR });
			}

			var done = await showSupplierEmailPopup(result.reportNumber, result.email, result.emailSent, result.emailError);
			if (done.isDenied && result.responseToken) {
				window.location.href = nncrConfig().statusUrl + '?token=' + encodeURIComponent(result.responseToken);
			} else {
				resetNncrForm();
			}
			return; // keep the button disabled while navigating away
		}

		Swal.fire({
			icon: 'error',
			title: 'Could not save the NNCR',
			text: (result && result.message) || ('HTTP ' + response.status),
			confirmButtonColor: NNCR_BRAND_COLOR
		});
		setSaving(false);
	} catch (err) {
		Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: NNCR_BRAND_COLOR });
		setSaving(false);
	}
}

/* ---------- Supplier email popup ---------- */
// After filing, shows the email sent to the supplier (or why it wasn't sent).
// Its button opens the supplier's response form. email = { To, Subject, BodyHtml, ResponseUrl } (body is
// built and HTML-encoded on the server).
function showSupplierEmailPopup(reportNumber, email, emailSent, emailError) {
	email = email || {};
	var to = email.To
		? '<span class="font-mono">' + escapeHtml(email.To) + '</span>'
		: '<span class="text-danger">No email on file for this supplier</span>';
	var banner = emailSent
		? '<div class="alert alert-success py-2 mb-2"><i class="bi bi-envelope-check"></i> Email sent to the supplier.</div>'
		: '<div class="alert alert-warning py-2 mb-2"><i class="bi bi-exclamation-triangle"></i> Email not sent: ' + escapeHtml(emailError) +
			' You can send it later from the NNCR status page, or share the response form link.</div>';

	return Swal.fire({
		icon: emailSent ? 'success' : 'warning',
		title: 'NNCR ' + escapeHtml(reportNumber) + ' filed',
		width: 720,
		html:
			'<div class="text-start small">' +
			banner +
			'<div class="border rounded">' +
			'<div class="border-bottom px-3 py-2 bg-light"><div><b>To:</b> ' + to + '</div><div><b>Subject:</b> ' + escapeHtml(email.Subject) + '</div></div>' +
			'<div class="px-3 py-2">' + (email.BodyHtml || '') + '</div>' +
			'</div></div>',
		showDenyButton: true,
		showCancelButton: true,
		confirmButtonText: '<i class="bi bi-box-arrow-up-right"></i> Open response form',
		denyButtonText: 'View status',
		cancelButtonText: 'File another',
		confirmButtonColor: NNCR_BRAND_COLOR,
		denyButtonColor: '#6c757d',
		allowOutsideClick: false,
		allowEscapeKey: false,
		preConfirm: function () {
			// Open the supplier form in a new tab and keep this popup open.
			if (email.ResponseUrl) window.open(email.ResponseUrl, '_blank', 'noopener');
			return false;
		}
	});
}

/* ---------- Clear ---------- */
function resetNncrForm() {
	document.getElementById('nncrForm').reset();
	document.querySelectorAll('input[type="checkbox"][data-detail]').forEach(function (cb) { syncDetailInput(cb, false); });
	document.getElementById('warehouseCodeInput').value = '';
	closeSupplierDropdown();
	document.getElementById('issuedToId').value = '';
	showSupplierEmail(null);
	selectedFiles = [];
	renderAttachments();
	setDefaultDate();
	setDueDate();
	var statusEl = document.getElementById('submitStatus');
	statusEl.textContent = '';
	statusEl.className = 'me-auto small fw-semibold';
}

function clearNncrForm() {
	Swal.fire({
		icon: 'warning',
		title: 'Clear all fields on this form?',
		showCancelButton: true,
		confirmButtonText: 'Clear form',
		cancelButtonText: 'Cancel',
		confirmButtonColor: NNCR_BRAND_COLOR,
		reverseButtons: true
	}).then(function (r) { if (r.isConfirmed) resetNncrForm(); });
}

/* ---------- Init ---------- */
document.addEventListener('DOMContentLoaded', function () {
	initDetailCheckboxes();
	initSupplierSearch();
	initAttachments();
	setDefaultDate();
	setDueDate();
	handleWarehouseChange(); // browser may restore a selection on back-navigation
});

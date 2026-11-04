/* ---------- Default date ---------- */
function toIsoDate(date) {
	return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0');
}

function setDefaultDate() {
	document.getElementById('docDateInput').value = toIsoDate(new Date());
}
setDefaultDate();

/* ---------- Type of Non-conformance (single-select) ---------- */
function handleNonconformanceTypeChange() {
	const selected = document.querySelector('input[name="nonconformance_type"]:checked');
	const subWrap = document.getElementById('externalAuditSubWrap');
	if (selected && selected.value === 'ExternalQualityAudit') {
		subWrap.classList.remove('d-none');
	} else {
		subWrap.classList.add('d-none');
		document.querySelectorAll('input[name="external_audit_subtype"]').forEach(r => r.checked = false);
	}

	applyItemFieldsRequiredState();
}

function itemFieldsAreOptional() {
	const nc = document.querySelector('input[name="nonconformance_type"]:checked');
	return !!nc && nc.value !== 'ProductQualityFoodSafety';
}

function applyItemFieldsRequiredState(scopeEl) {
	const optional = itemFieldsAreOptional();
	const blocks = scopeEl ? [scopeEl] : Array.from(document.querySelectorAll('.capa-item-block'));

	blocks.forEach(block => {
		const itemSearch = block.querySelector('.item-search');
		const quantity = block.querySelector('[data-field="quantity"]');
		const uom = block.querySelector('[data-field="uom_id"]');
		const defectSelect = block.querySelector('[data-field="defect_category_id"]');

		// UTD is always optional, so it isn't part of this toggle.
		[itemSearch, quantity, uom, defectSelect].forEach(el => {
			if (el) el.required = !optional;
		});

		block.querySelectorAll('.optional-hint').forEach(hint => {
			hint.classList.toggle('d-none', !optional);
		});
		// The * on item fields only shows while those fields are actually required.
		block.querySelectorAll('.item-required-mark').forEach(mark => {
			mark.classList.toggle('d-none', optional);
		});
	});
}

/* ---------- CAR Classification (single-select, server-rendered options) ---------- */
function handleCarClassificationChange() {
	const selected = document.querySelector('input[name="car_classification_type"]:checked');
	const supplierSelect = document.getElementById('carSupplierSelect');
	const truckerSelect = document.getElementById('carTruckerSelect');
	const dcSiteSelect = document.getElementById('carDcSiteSelect');
	const otherInput = document.getElementById('carOtherText');

	[supplierSelect, truckerSelect, dcSiteSelect, otherInput].forEach(el => el.classList.add('d-none'));

	const refNoHint = document.getElementById('refNoOptionalHint');
	const removeReadonly = document.getElementById('removeReadonly');
    const tempRefNoHint = document.getElementById('tempRefNoOptionalHint');
	const type = selected ? selected.value : null;
	if (refNoHint) refNoHint.classList.toggle('d-none', type !== 'LLII' && type !== 'DCSite');
	const refNoMark = document.getElementById('refNoRequiredMark');
	if (refNoMark) refNoMark.classList.toggle('d-none', type === 'LLII' || type === 'DCSite');
	//if (removeReadonly) removeReadonly.classList.toggle('style', type !== 'Supplier' && type !== 'Trucker');

	// The Item field is restricted to a vendor-specific dropdown only while
	// CAR Classification is "Supplier" with a vendor picked — any other
	// classification (or none) reverts every item block back to search mode.
	if (type !== 'Supplier' && typeof resetVendorItemRestriction === 'function') {
		resetVendorItemRestriction();
	}

	if (!selected) return;

	if (type === 'Supplier') {
		supplierSelect.classList.remove('d-none');
		removeReadonly.readOnly = false;
		removeReadonly.style = false;
        tempRefNoHint.classList.add('d-none');
		if (supplierSelect.value && typeof loadVendorItemsForSupplier === 'function') {
			loadVendorItemsForSupplier(supplierSelect.value);
		}
	}
	else if (type === 'Trucker') {
		truckerSelect.classList.remove('d-none');
		removeReadonly.readOnly = false;
		removeReadonly.style = false;
        tempRefNoHint.classList.add('d-none');
	}
	else if (type === 'DCSite') {
		dcSiteSelect.classList.remove('d-none');
		tempRefNoHint.classList.add('d-none');
		removeReadonly.style = 'background-color:#f4e2e0';
        removeReadonly.readOnly = true;
	}
	else if (type === 'LLII') {
		// dcSiteSelect.classList.remove('d-none');
        otherInput.classList.remove('d-none');
		tempRefNoHint.classList.add('d-none');
		removeReadonly.style = 'background-color:#f4e2e0';
        removeReadonly.readOnly = true;
	}
	else otherInput.classList.remove('d-none'); // LLII / Others
}

/* ---------- LLII / Others: one or more emails, comma or semicolon separated ---------- */
const EMAIL_PATTERN = /^[^\s@;,<>]+@[^\s@;,<>]+\.[^\s@;,<>]+$/;

// Same rules as the server (CapaEmailList.Normalize): trim, drop blanks and duplicates.
function parseEmailList(raw) {
	const valid = [];
	const invalid = [];
	const seen = new Set();
	(raw || '').split(/[;,]/).forEach(part => {
		const email = part.trim();
		if (!email) return;
		if (!EMAIL_PATTERN.test(email)) invalid.push(email);
		else if (!seen.has(email.toLowerCase())) { seen.add(email.toLowerCase()); valid.push(email); }
	});
	return { valid, invalid, normalized: valid.join(',') };
}

/* Chip input: the To / CC fields under LLII. Each address becomes a removable chip. */
const carEmailLists = { to: [] };

function escapeChipText(text) {
	return text.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function showCarEmailError(message) {
	const el = document.getElementById('carEmailError');
	if (!el) return;
	el.textContent = message || '';
	el.classList.toggle('d-none', !message);
}

function renderCarEmailChips(list) {
	const box = document.querySelector(`.capa-chip-input[data-list="${list}"]`);
	if (!box) return;
	box.querySelectorAll('.capa-chip').forEach(chip => chip.remove());
	const input = box.querySelector('.capa-chip-text');
	carEmailLists[list].forEach(email => {
		const chip = document.createElement('span');
		chip.className = 'capa-chip';
		chip.innerHTML = `<span class="capa-chip-label">${escapeChipText(email)}</span>`
			+ `<button type="button" class="capa-chip-remove" aria-label="Remove ${escapeChipText(email)}">&times;</button>`;
		chip.querySelector('.capa-chip-remove').addEventListener('click', () => removeCarEmail(list, email));
		box.insertBefore(chip, input);
	});
}

function removeCarEmail(list, email) {
	carEmailLists[list] = carEmailLists[list].filter(e => e.toLowerCase() !== email.toLowerCase());
	renderCarEmailChips(list);
	showCarEmailError('');
}

// Adds every address found in `raw`. Returns the entries that were not valid emails,
// so the caller can leave them in the text box for the user to fix.
function addCarEmails(list, raw) {
	const parsed = parseEmailList(raw);
	parsed.valid.forEach(email => {
		const key = email.toLowerCase();
		if (!carEmailLists[list].some(e => e.toLowerCase() === key)) carEmailLists[list].push(email);
	});
	renderCarEmailChips(list);
	showCarEmailError(parsed.invalid.length ? `Not a valid email address: ${parsed.invalid.join(', ')}` : '');
	return parsed.invalid;
}

function commitCarEmailInput(input) {
	const list = input.closest('.capa-chip-input').dataset.list;
	const invalid = addCarEmails(list, input.value);
	input.value = invalid.join(', ');
	input.closest('.capa-chip-input').classList.toggle('is-invalid', invalid.length > 0);
	return invalid;
}

function initCarEmailChips() {
	document.querySelectorAll('.capa-chip-input').forEach(box => {
		const input = box.querySelector('.capa-chip-text');
		// Clicking anywhere in the box focuses the text field.
		box.addEventListener('click', e => { if (e.target === box) input.focus(); });
		input.addEventListener('keydown', e => {
			if (e.key === 'Enter' || e.key === ',' || e.key === ';' || (e.key === 'Tab' && input.value.trim())) {
				if (!input.value.trim()) { if (e.key !== 'Tab') e.preventDefault(); return; }
				if (e.key !== 'Tab') e.preventDefault();
				commitCarEmailInput(input);
			} else if (e.key === 'Backspace' && !input.value) {
				const list = box.dataset.list;
				const last = carEmailLists[list][carEmailLists[list].length - 1];
				if (last) removeCarEmail(list, last);
			}
		});
		input.addEventListener('input', () => box.classList.remove('is-invalid'));
		input.addEventListener('paste', e => {
			const text = (e.clipboardData || window.clipboardData).getData('text');
			if (!/[,;\s]/.test(text.trim())) return;
			e.preventDefault();
			addCarEmails(box.dataset.list, text.replace(/\s+/g, ','));
		});
		input.addEventListener('blur', () => { if (input.value.trim()) commitCarEmailInput(input); });
	});
}

function resetCarEmails() {
	carEmailLists.to = [];
	renderCarEmailChips('to');
	document.querySelectorAll('.capa-chip-text').forEach(i => { i.value = ''; });
	document.querySelectorAll('.capa-chip-input').forEach(b => b.classList.remove('is-invalid'));
	showCarEmailError('');
}

// Anything still typed in a box counts as an address too. Valid ones are turned into
// chips here; whatever is left over is invalid and gets reported.
function getInvalidClassificationEmails() {
	const selected = document.querySelector('input[name="car_classification_type"]:checked');
	if (!selected || (selected.value !== 'LLII' && selected.value !== 'Others')) return [];
	let invalid = [];
	document.querySelectorAll('.capa-chip-text').forEach(input => {
		if (input.value.trim()) invalid = invalid.concat(commitCarEmailInput(input));
	});
	return invalid;
}

document.addEventListener('DOMContentLoaded', initCarEmailChips);

function getCarClassificationPayload() {
	const selected = document.querySelector('input[name="car_classification_type"]:checked');
	if (!selected) {
		return { CarClassificationType: null, CarClassificationRefId: null, CarClassificationRefName: null, CarClassificationRefEmail: null, CarClassificationOtherText: null };
	}

	const type = selected.value;
	let select = null;
	if (type === 'Supplier') select = document.getElementById('carSupplierSelect');
	else if (type === 'Trucker') select = document.getElementById('carTruckerSelect');
	else if (type === 'DCSite') select = document.getElementById('carDcSiteSelect');

	if (select) {
		const opt = select.selectedIndex >= 0 ? select.options[select.selectedIndex] : null;
		return {
			CarClassificationType: type,
			CarClassificationRefId: select.value || null,
			CarClassificationRefName: opt ? opt.dataset.name : null,
			CarClassificationRefEmail: opt ? opt.dataset.email : null,
			CarClassificationOtherText: null
		};
	}

	// LLII / Others: the To chips go to OtherText as before.
	getInvalidClassificationEmails(); // turns anything still typed in the box into a chip
	return {
		CarClassificationType: type,
		CarClassificationRefId: null,
		CarClassificationRefName: null,
		CarClassificationRefEmail: null,
		CarClassificationOtherText: carEmailLists.to.join(',')
	};
}

/* ---------- Site Warehouse code auto-fill ---------- */
function handleWarehouseChange() {
	const sel = document.getElementById('siteWarehouseSelect');
	const codeInput = document.getElementById('warehouseCodeInput');
	const opt = sel.options[sel.selectedIndex];
	codeInput.value = (opt && opt.dataset.code) ? opt.dataset.code : '';
}

/* ---------- Item + Defect Category catalogs ----------
   Loaded once from the Catalog()/DefectCatalog() JSON endpoints. ITEM_CATALOG
   powers the Item search-with-suggestions input (initItemAutocomplete in
   Quality.cshtml); DEFECT_CATALOG populates each Defect Description <select>
   (populateDefectSelect in Quality.cshtml), which also gets a trailing
   "Other" option not present in this array. */
let ITEM_CATALOG = [];
let DEFECT_CATALOG = [];

async function loadItemCatalog() {
	try {
		const res = await fetch(window.CAPA_CONFIG.itemCatalogUrl);
		if (res.ok) { ITEM_CATALOG = await res.json(); }
	} catch (err) {
		console.error('Could not load item catalog', err);
	}
}

async function loadDefectCatalog() {
	try {
		const res = await fetch(window.CAPA_CONFIG.defectCatalogUrl);
		if (res.ok) {
			DEFECT_CATALOG = await res.json();
			if (typeof populateAllDefectSelects === 'function') populateAllDefectSelects();
		}
	} catch (err) {
		console.error('Could not load defect catalog', err);
	}
}

Promise.all([loadItemCatalog(), loadDefectCatalog()]);

function escapeHtml(str) {
	return String(str).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function highlightMatch(name, query) {
	const idx = name.toLowerCase().indexOf(query.toLowerCase());
	if (idx === -1 || !query) return escapeHtml(name);
	return escapeHtml(name.slice(0, idx)) + '<mark>' + escapeHtml(name.slice(idx, idx + query.length)) + '</mark>' + escapeHtml(name.slice(idx + query.length));
}

// With a supplier's item list, an empty query lists that supplier's items up
// front (scrollable); typing narrows them. The full catalog needs a query.
function filterCatalog(query, source) {
	const q = query.trim().toLowerCase();
	if (!source) {
		if (!q) return [];
		return ITEM_CATALOG.filter(it => it.name.toLowerCase().includes(q) || (it.code || '').toLowerCase().includes(q)).slice(0, 8);
	}
	if (!q) return source.slice(0, 100);
	return source.filter(it => it.name.toLowerCase().includes(q) || (it.code || '').toLowerCase().includes(q)).slice(0, 100);
}

/* ---------- Save to database (ASP.NET MVC + ADO.NET backend) ---------- */
const API_ENDPOINT = window.CAPA_CONFIG.saveCapaReportUrl;

function collectItemPayload(block, index) {
	const getField = (field) => {
		const el = block.querySelector(`[data-field="${field}"]`);
		return el ? el.value : '';
	};
	const toIntOrNull = (v) => (v === '' || v === null || v === undefined) ? null : Number(v);

	// Hauler is one value per CAR, not per item — every item gets the same
	// value from the single header-level "Accredited Hauler" field.
	const haulerEl = document.querySelector('[name="hauler"]');
	const hauler = haulerEl ? haulerEl.value : '';

	// "Other" is a catalog entry (category "Other"), but the defect itself is free
	// text — whatever the user typed gets folded into Remarks (there's no dedicated
	// free-text defect column on tbl_QualityIncidentDtl).
	const defectRaw = getField('defect_category_id');
	const defectSelectEl = block.querySelector('[data-field="defect_category_id"]');
	const isOtherDefect = !!(defectSelectEl && defectSelectEl.selectedIndex >= 0 &&
		defectSelectEl.options[defectSelectEl.selectedIndex].dataset.category === 'Other');
	const otherInput = block.querySelector('.defect-other-input');
	const otherText = otherInput ? otherInput.value.trim() : '';

	let remarks = getField('remarks') || '';
	if (isOtherDefect && otherText) {
		remarks = remarks ? `Defect: ${otherText}.\n - ${remarks}` : `Defect: ${otherText}`;
	}

	// The description is only meaningful for a picked item — typed-but-unselected
	// text clears item_id, so it must not be sent on its own.
	const itemId = toIntOrNull(getField('item_id'));
	let itemDescription = '';
	let itemCode = '';
	if (itemId) {
		const itemSearch = block.querySelector('.item-search');
		const vendorSelect = block.querySelector('.item-vendor-select');
		const itemCodeDisplay = block.querySelector('.item-code-display');
		itemDescription = itemSearch ? itemSearch.value.trim() : '';
		if (!itemDescription && vendorSelect && vendorSelect.selectedIndex >= 0) {
			itemDescription = vendorSelect.options[vendorSelect.selectedIndex].text.trim();
		}
		itemCode = itemCodeDisplay ? itemCodeDisplay.value.trim() : '';
	}

	// Name and category sent alongside DefectCategoryID, like ItemDescription.
	// "Other" has no catalog name, so the specified text stands in for it.
	let defectName = '';
	let defectCategoryName = '';
	if (defectRaw) {
		const defectSelect = block.querySelector('[data-field="defect_category_id"]');
		const categoryDisplay = block.querySelector('.defect-category-display');
		if (isOtherDefect) {
			defectName = otherText;
			defectCategoryName = 'Other';
		} else if (defectSelect && defectSelect.selectedIndex >= 0) {
			defectName = defectSelect.options[defectSelect.selectedIndex].text.trim();
			defectCategoryName = categoryDisplay ? categoryDisplay.value.trim() : '';
		}
	}

	return {
		LineNumber: index + 1,
		ItemID: itemId,
		ItemCode: itemCode,
		ItemDescription: itemDescription,
		Quantity: toIntOrNull(getField('quantity')),
		UOMID: toIntOrNull(getField('uom_id')),
		DefectCategoryID: toIntOrNull(defectRaw),
		DefectName: defectName,
		DefectCategoryName: defectCategoryName,
		UsedToDate: getField('used_to_date') || null,
		Remarks: remarks,
		Hauler: hauler || '',
		Attachments: (block.getItemAttachments ? block.getItemAttachments() : []).map(a => ({ Caption: a.caption, DataUrl: a.dataUrl }))
	};
}

function collectAllItems() {
	return Array.from(document.querySelectorAll('.capa-item-block')).map(collectItemPayload);
}

function buildCapaPayload() {
	const val = (name) => {
		const el = document.querySelector(`[name="${name}"]`);
		return el ? el.value : '';
	};

	//var carNo = '@ViewBag.ReportNumber';

	return {
		//ReportNumber: /* val('capa_no') */ carNo,
		IssuedBy: val('issued_by'),
		IssuedToDepartment: Number(val('issued_to')) || 0,
		SiteWarehouse: Number(val('site_warehouse')) || 0,
		RefNo: val('ref_no'),

		NonconformanceType: (document.querySelector('input[name="nonconformance_type"]:checked') || {}).value || null,
		ExternalAuditSubType: (document.querySelector('input[name="external_audit_subtype"]:checked') || {}).value || null,

		...getCarClassificationPayload(),

		StockEventID: Number((document.querySelector('input[name="stock_event"]:checked') || {}).value || 0),

		Items: collectAllItems()
	};
}

/* ---------- Required-field validation (SweetAlert2) ---------- */
function getRequiredFieldChecks() {
	const val = (name) => {
		const el = document.querySelector(`[name="${name}"]`);
		return el ? el.value.trim() : '';
	};

	const checks = [
		{ test: () => !!val('doc_date'), label: 'Date' },
		{ test: () => !!val('issued_by'), label: 'Issued by (Name / Department)' },
		//{ test: () => !!val('capa_no'), label: 'CAR No.' },
		{ test: () => !!val('issued_to'), label: 'Issued to (Department)' },
		{
			test: () => {
				const car = document.querySelector('input[name="car_classification_type"]:checked');
				const type = car ? car.value : null;
				// LLII and DC Site CARs aren't tied to an external QAR/PNCR reference.
				if (type === 'LLII' || type === 'DCSite') return true;
				return !!val('ref_no');
			},
			label: 'Ref. No. (QAR / PNCR)'
		},
		{ test: () => !!val('site_warehouse'), label: 'Site Warehouse' },
		{ test: () => !!document.querySelector('input[name="nonconformance_type"]:checked'), label: 'Type of Non-conformance' },
		{
			test: () => {
				const nc = document.querySelector('input[name="nonconformance_type"]:checked');
				return !nc || nc.value !== 'ExternalQualityAudit' || !!document.querySelector('input[name="external_audit_subtype"]:checked');
			},
			label: 'External Audit — Government or 3rd Party'
		},
		{ test: () => !!document.querySelector('input[name="car_classification_type"]:checked'), label: 'CAR Classification' },
		{
			test: () => {
				const car = document.querySelector('input[name="car_classification_type"]:checked');
				if (!car) return true; // already flagged above
				const type = car.value;
				// Only Supplier/Trucker need a specific contact to notify —
				// LLII, DC Site, and Others are left optional.
				if (type === 'Supplier') return !!document.getElementById('carSupplierSelect').value;
				if (type === 'Trucker') return !!document.getElementById('carTruckerSelect').value;
				return true;
			},
			label: 'CAR Classification detail (Supplier / Trucker)'
		},
		//{ test: () => !!document.querySelector('input[name="stock_event"]:checked'), label: 'Stock Movement (SA OUT / OUT RIGHT)' }
	];

	const itemFieldsOptional = itemFieldsAreOptional();

	document.querySelectorAll('.capa-item-block').forEach((block, i) => {
		const label = (field) => `${field} (Item ${i + 1})`;
		const getVal = (field) => {
			const el = block.querySelector(`[data-field="${field}"]`);
			return el ? el.value.trim() : '';
		};
		checks.push(
			{ test: () => itemFieldsOptional || !!block.querySelector('.supplier-id-field').value, label: label('Supplier') },
				{ test: () => itemFieldsOptional || !!getVal('item_id'), label: label('Item') },
			{ test: () => itemFieldsOptional || !!getVal('quantity'), label: label('Quantity of Affected Items') },
			{ test: () => itemFieldsOptional || !!getVal('uom_id'), label: label('UOM') },
			{
				test: () => {
					if (itemFieldsOptional) return true;
					const val = getVal('defect_category_id');
					if (!val) return false;
					const sel = block.querySelector('[data-field="defect_category_id"]');
						const picked = sel && sel.selectedIndex >= 0 ? sel.options[sel.selectedIndex] : null;
						if (picked && picked.dataset.category === 'Other') {
						const otherInput = block.querySelector('.defect-other-input');
						return !!(otherInput && otherInput.value.trim());
					}
					return true;
				},
				label: label('Defect Description')
			},
			{ test: () => !!getVal('remarks'), label: label('Remarks') }
		);
	});

	return checks;
}

async function submitCapaForm() {
	const btn = document.getElementById('submitBtn');
	const statusEl = document.getElementById('submitStatus');

	const missing = getRequiredFieldChecks().filter(c => !c.test()).map(c => c.label);
	if (missing.length > 0) {
		Swal.fire({
			icon: 'warning',
			title: 'Missing required fields',
			html: '<ul style="text-align:left;margin:0;padding-left:1.2rem">' + missing.map(m => `<li>${m}</li>`).join('') + '</ul>',
			confirmButtonColor: '#c60404'
		});
		return;
	}

	const invalidEmails = getInvalidClassificationEmails();
	if (invalidEmails.length > 0) {
		Swal.fire({
			icon: 'warning',
			title: 'Invalid email address',
			html: 'Please fix these, or separate several addresses with a comma or semicolon:<ul style="text-align:left;margin:0.5rem 0 0;padding-left:1.2rem">'
				+ invalidEmails.map(e => `<li>${e.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]))}</li>`).join('') + '</ul>',
			confirmButtonColor: '#c60404'
		});
		return;
	}

	btn.disabled = true;
	btn.textContent = 'Saving…';
	statusEl.textContent = '';
	statusEl.className = 'me-auto small fw-semibold';

	try {
		const payload = buildCapaPayload();
		const response = await fetch(API_ENDPOINT, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(payload)
		});
		const result = await response.json().catch(() => null);

		if (response.ok && result && result.success) {
			statusEl.textContent = `Saved — ${result.ReportNumber}`;
			statusEl.className = 'me-auto small fw-semibold text-success';

			if (result.ResponseToken) {
				window.open(
					`${window.CAPA_CONFIG.capaStatusUrl}?token=${encodeURIComponent(result.ResponseToken)}&autoDownloadPdf=1`,
					'_blank'
				);
			}

			// Attached files in the form but none reached the server, so there is nothing to save.
			const attachedCount = payload.Items.reduce((n, i) => n + i.Attachments.length, 0);
			if (!result.attachmentError && attachedCount > 0 && !result.attachmentsSent) {
				result.attachmentError = `${attachedCount} file(s) were attached in the form but the server received none.`;
			}
			if (!result.attachmentError && result.attachmentsSaved > 0) {
				console.info(`Saved ${result.attachmentsSaved} attachment(s) to ${result.attachmentFolder}`);
			}

			if (result.attachmentError) {
				Swal.fire({
					icon: 'warning',
					title: `${result.ReportNumber} saved, but its attachments were not`,
					text: result.attachmentError,
					confirmButtonColor: '#c60404',
					allowOutsideClick: false
				}).then(() => location.reload());
				return;
			}

			let timerInterval;

			Swal.fire({
				icon: 'success',
				title: 'CAPA Report Saved!',
				html: 'Reloading page in <b>3</b> seconds...',
				footer: result.attachmentsSaved > 0 ? `${result.attachmentsSaved} file(s) saved to ${result.attachmentFolder}` : undefined,
				timer: 3000,
				timerProgressBar: true,
				allowOutsideClick: false,
				allowEscapeKey: false,
				didOpen: () => {
					Swal.showLoading();
					const b = Swal.getHtmlContainer().querySelector('b');

					timerInterval = setInterval(() => {
						b.textContent = Math.ceil(Swal.getTimerLeft() / 1000);
					}, 100);
				},
				willClose: () => {
					clearInterval(timerInterval);
				}
			}).then((result) => {
				if (result.dismiss === Swal.DismissReason.timer) {
					location.reload();
				}
			});
		} else {
			Swal.fire({
				icon: 'error',
				title: 'Could not save the report',
				text: (result && result.message) || `HTTP ${response.status}`,
				confirmButtonColor: '#c60404'
			});
		}
	} catch (err) {
		Swal.fire({
			icon: 'error',
			title: 'Network error',
			text: err.message,
			confirmButtonColor: '#c60404'
		});
	} finally {
		btn.disabled = false;
		btn.textContent = 'Submit to Database';
	}
}

function clearForm() {
	if (confirm('Clear all fields on this form?')) {
		document.getElementById('capaForm').reset();
		document.querySelectorAll('#capaItemBlock0 ~ .capa-item-block').forEach(block => block.remove());

		const block0 = document.getElementById('capaItemBlock0');
		if (block0 && block0.resetAttachments) block0.resetAttachments();
		if (block0 && block0.resetSupplier) block0.resetSupplier();

		setDefaultDate();
		document.getElementById('warehouseCodeInput').value = '';
		['carSupplierSelect', 'carTruckerSelect', 'carDcSiteSelect'].forEach(id => {
			const el = document.getElementById(id);
			el.classList.add('d-none');
			el.selectedIndex = 0;
		});
		document.getElementById('carOtherText').classList.add('d-none');
		resetCarEmails();
		document.getElementById('externalAuditSubWrap').classList.add('d-none');
		document.getElementById('submitStatus').textContent = '';
		document.getElementById('submitStatus').className = 'me-auto small fw-semibold';
	}
}

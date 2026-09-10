/* ---------- Default date + due-date auto-calc ---------- */
function toIsoDate(date) {
	return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0');
}

function skipWeekend(date) {
	const day = date.getDay(); // 0 = Sunday, 6 = Saturday
	if (day === 6) date.setDate(date.getDate() + 2);      // Saturday -> Monday
	else if (day === 0) date.setDate(date.getDate() + 1); // Sunday -> Monday
	return date;
}

function setDefaultDate() {
	const today = new Date();
	document.getElementById('docDateInput').value = toIsoDate(today);

	const correctionDue = new Date(today);
	correctionDue.setDate(correctionDue.getDate() + 1);
	skipWeekend(correctionDue);
	document.getElementById('correctionDueInput').value = toIsoDate(correctionDue);

	const capaDue = new Date(today);
	capaDue.setDate(capaDue.getDate() + 7);
	document.getElementById('capaDueInput').value = toIsoDate(capaDue);
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
		const usedToDate = block.querySelector('[data-field="used_to_date"]');

		[itemSearch, quantity, uom, defectSelect, usedToDate].forEach(el => {
			if (el) el.required = !optional;
		});

		block.querySelectorAll('.optional-hint').forEach(hint => {
			hint.classList.toggle('d-none', !optional);
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

	const otherInput = document.getElementById('carOtherText');
	return {
		CarClassificationType: type,
		CarClassificationRefId: null,
		CarClassificationRefName: null,
		CarClassificationRefEmail: null,
		CarClassificationOtherText: otherInput ? otherInput.value : null
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

function filterCatalog(query) {
	const q = query.trim().toLowerCase();
	if (!q) return [];
	return ITEM_CATALOG.filter(it => it.name.toLowerCase().includes(q) || (it.code || '').toLowerCase().includes(q)).slice(0, 8);
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

	// "Other" isn't a real catalog entry — DefectCategoryID stays blank, and
	// whatever the user typed to specify it gets folded into Remarks instead
	// (there's no dedicated free-text defect column on tbl_QualityIncidentDtl).
	const defectRaw = getField('defect_category_id');
	const isOtherDefect = defectRaw === 'other';
	const otherInput = block.querySelector('.defect-other-input');
	const otherText = otherInput ? otherInput.value.trim() : '';

	let remarks = getField('remarks') || '';
	if (isOtherDefect && otherText) {
		remarks = remarks ? `Defect: ${otherText}.\n - ${remarks}` : `Defect: ${otherText}`;
	}

	return {
		LineNumber: index + 1,
		ItemID: toIntOrNull(getField('item_id')),
		Quantity: toIntOrNull(getField('quantity')),
		UOMID: toIntOrNull(getField('uom_id')),
		DefectCategoryID: isOtherDefect ? null : toIntOrNull(defectRaw),
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
		CorrectionDueDate: val('correction_due') || null,
		ReportDueDate: val('capa_due') || null,

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
		{ test: () => !!val('correction_due'), label: 'Correction / Immediate Action Due Date' },
		{ test: () => !!val('capa_due'), label: 'Corrective / Preventive Action Due Date' },
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
			{ test: () => itemFieldsOptional || !!getVal('item_id'), label: label('Item') },
			{ test: () => itemFieldsOptional || !!getVal('quantity'), label: label('Quantity of Affected Items') },
			{ test: () => itemFieldsOptional || !!getVal('uom_id'), label: label('UOM') },
			{
				test: () => {
					if (itemFieldsOptional) return true;
					const val = getVal('defect_category_id');
					if (!val) return false;
					if (val === 'other') {
						const otherInput = block.querySelector('.defect-other-input');
						return !!(otherInput && otherInput.value.trim());
					}
					return true;
				},
				label: label('Defect Description')
			},
			{ test: () => itemFieldsOptional || !!getVal('used_to_date'), label: label('Used-to Date') },
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
			confirmButtonColor: '#7d1f27'
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

			let timerInterval;

			Swal.fire({
				icon: 'success',
				title: 'CAPA Report Saved!',
				html: 'Reloading page in <b>3</b> seconds...',
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
				confirmButtonColor: '#7d1f27'
			});
		}
	} catch (err) {
		Swal.fire({
			icon: 'error',
			title: 'Network error',
			text: err.message,
			confirmButtonColor: '#7d1f27'
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

		setDefaultDate();
		document.getElementById('warehouseCodeInput').value = '';
		['carSupplierSelect', 'carTruckerSelect', 'carDcSiteSelect'].forEach(id => {
			const el = document.getElementById(id);
			el.classList.add('d-none');
			el.selectedIndex = 0;
		});
		document.getElementById('carOtherText').classList.add('d-none');
		document.getElementById('carOtherText').value = '';
		document.getElementById('externalAuditSubWrap').classList.add('d-none');
		document.getElementById('submitStatus').textContent = '';
		document.getElementById('submitStatus').className = 'me-auto small fw-semibold';
	}
}

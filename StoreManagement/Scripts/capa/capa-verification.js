const RESPONSE_TOKEN = window.CAPA_VERIFICATION_DATA.responseToken;
const SUBMIT_ENDPOINT = window.CAPA_VERIFICATION_DATA.submitEndpoint;

const ATTACHMENT_PHOTOS = window.CAPA_VERIFICATION_DATA.attachmentPhotos;
const VERIFICATION_EXPORT_ROWS = window.CAPA_VERIFICATION_DATA.exportRows;
let currentLightboxIndex = 0;

function openLightbox(index) {
	currentLightboxIndex = index;
	renderLightboxPhoto();
	document.getElementById('lightboxOverlay').classList.add('active');
	document.body.style.overflow = 'hidden';
}

function closeLightbox() {
	document.getElementById('lightboxOverlay').classList.remove('active');
	document.body.style.overflow = '';
}

function handleOverlayClick(e) {
	if (e.target.id === 'lightboxOverlay') closeLightbox();
}

function showLightboxPhoto(index) {
	if (index < 0 || index >= ATTACHMENT_PHOTOS.length) return;
	currentLightboxIndex = index;
	renderLightboxPhoto();
}

function renderLightboxPhoto() {
	const photo = ATTACHMENT_PHOTOS[currentLightboxIndex];
	if (!photo) return;

	document.getElementById('lightboxImage').src = photo.url;
	document.getElementById('lightboxDownload').href = photo.url;
	document.getElementById('lightboxDownload').setAttribute('download', photo.fileName || 'attachment');
	document.getElementById('lightboxCaption').textContent = photo.caption || '';
	document.getElementById('lightboxCounter').textContent = ATTACHMENT_PHOTOS.length > 1
		? `${currentLightboxIndex + 1} / ${ATTACHMENT_PHOTOS.length}` : '';

	const showNav = ATTACHMENT_PHOTOS.length > 1;
	document.getElementById('lightboxPrev').style.display = showNav ? 'flex' : 'none';
	document.getElementById('lightboxNext').style.display = showNav ? 'flex' : 'none';
}

document.addEventListener('keydown', (e) => {
	const overlay = document.getElementById('lightboxOverlay');
	if (!overlay || !overlay.classList.contains('active')) return;
	if (e.key === 'Escape') closeLightbox();
	else if (e.key === 'ArrowLeft') showLightboxPhoto(currentLightboxIndex - 1);
	else if (e.key === 'ArrowRight') showLightboxPhoto(currentLightboxIndex + 1);
});

function val(id) {
	const el = document.getElementById(id);
	return el ? el.value.trim() : '';
}

function collectItemUpdates() {
	const hauler = val('hauler');

	return Array.from(document.querySelectorAll('.verification-item-block')).map(block => {
		const getField = (field) => {
			const el = block.querySelector(`[data-field="${field}"]`);
			return el ? el.value : '';
		};
		const toIntOrNull = (v) => (v === '' || v === null || v === undefined) ? null : Number(v);

		return {
			LineNumber: Number(block.dataset.lineNumber),
			Hauler: hauler || '',
			AdjustmentQty: toIntOrNull(getField('adjustment_qty')),
			AdjustmentBy: toIntOrNull(getField('adjustment_by')),
			AdjustmentDate: getField('adjustment_date') || null
		};
	});
}

function GetDate() {
	const today = new Date();
	return today.getFullYear() + '-' + String(today.getMonth() + 1).padStart(2, '0') + '-' + String(today.getDate()).padStart(2, '0');
}

function exportVerificationToExcel(verifiedByName, notes, isEffective, dateStr) {
	const headers = [
		'CAR No.', 'Issued By', 'Site Warehouse',
		'Immediate Action', 'IA Person', 'IA Target Date',
		'Root Cause', 'Corrective Action', 'CAPA Person', 'CAPA Target Date',
		'Item', 'Quantity', 'UOM', 'Defect', 'UTD', 'Remarks', 'Hauler',
		'Verified / Approved By', 'Verification Notes', 'Effectiveness', 'Verification Date'
	];

	const effectivenessText = isEffective ? 'Effective' : 'Not Effective';

	const rows = VERIFICATION_EXPORT_ROWS.map(r => [
		r.reportNumber, r.issuedBy, r.siteWarehouse,
		r.immediateAction, r.iaPerson, r.iaTargetDate,
		r.rootCause, r.capaActions, r.capaPerson, r.capaTargetDate,
		r.itemDescription, r.quantity, r.uom, r.defect, r.utd, r.remarks, r.hauler,
		verifiedByName, notes, effectivenessText, dateStr
	]);

	const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
	worksheet['!cols'] = headers.map(() => ({ wch: 18 }));

	const workbook = XLSX.utils.book_new();
	XLSX.utils.book_append_sheet(workbook, worksheet, 'Verification Response');

	const reportNumber = (VERIFICATION_EXPORT_ROWS[0] && VERIFICATION_EXPORT_ROWS[0].reportNumber) || 'CAR';
	XLSX.writeFile(workbook, `${reportNumber}_Verification_Response.xlsx`);
}

async function submitVerification() {
	const required = [
		{ id: 'VerifiedBy', label: 'Verified / Approved By' }
	];
	const missing = required.filter(f => !val(f.id)).map(f => f.label);

	if (!document.querySelector('input[name="stock_event"]:checked')) {
		missing.push('Stock Movement (SA OUT / OUT RIGHT)');
	}

	if (missing.length > 0) {
		Swal.fire({
			icon: 'warning',
			title: 'Missing required fields',
			html: '<ul style="text-align:left;margin:0;padding-left:1.2rem">' + missing.map(m => `<li>${m}</li>`).join('') + '</ul>',
			confirmButtonColor: '#7d1f27'
		});
		return;
	}

	const btn = document.getElementById('submitBtn');
	const statusEl = document.getElementById('submitStatus');
	const today = GetDate();
	btn.disabled = true;
    btn.textContent = 'Submitting...';

    const verifiedByName = val('VerifiedBy');
    const isEffective = document.getElementById('effectivenessEffective').checked;

	const payload = {
		ResponseToken: RESPONSE_TOKEN,
		IaVerifiedBy: verifiedByName,
		IaVerifiedDate: today,
		CapaVerifiedBy: verifiedByName,
		CapaVerifiedDate: today,
		VerificationNotes: val('verificationNotes'),
		VerifiedByUsername: verifiedByName,
		VerifiedDate: today,
		ApprovedDate: today,
		IsEffective: isEffective,
		StockEventID: Number((document.querySelector('input[name="stock_event"]:checked') || {}).value || 0),
		Items: collectItemUpdates()
	};

	try {
		const response = await fetch(SUBMIT_ENDPOINT, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(payload)
		});
		const result = await response.json().catch(() => null);

		if (response.ok && result && result.success) {
			try {
				exportVerificationToExcel(verifiedByName, val('verificationNotes'), isEffective, today);
			} catch (exportErr) {
				console.error('Could not export verification to Excel', exportErr);
			}

			Swal.fire({
				icon: 'success',
				title: 'Verification saved',
				text: isEffective ? 'This CAPA is now closed.' : 'Not effective - this CAPA has been sent back to Awaiting Response for a new corrective action.',
				confirmButtonColor: '#7d1f27'
			}).then(() => {
				location.reload();
			});
		} else {
			Swal.fire({
				icon: 'error',
				title: 'Could not save verification',
				text: (result && result.message) || `HTTP ${response.status}`,
				confirmButtonColor: '#7d1f27'
			});
		}
	} catch (err) {
		Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: '#7d1f27' });
	} finally {
		btn.disabled = false;
		btn.textContent = 'Submit Verification';
	}
}

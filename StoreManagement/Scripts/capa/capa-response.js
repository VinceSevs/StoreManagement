const RESPONSE_TOKEN = window.CAPA_RESPONSE_DATA.responseToken;
const SUBMIT_ENDPOINT = window.CAPA_RESPONSE_DATA.submitEndpoint;
const CLOSE_IF_OVERDUE_ENDPOINT = window.CAPA_RESPONSE_DATA.closeIfOverdueEndpoint;
const ATTACHMENT_PHOTOS = window.CAPA_RESPONSE_DATA.attachmentPhotos;

function markResponseClosed() {
	const badge = document.getElementById('capaResponseStatusBadge');
	if (badge) {
		badge.textContent = 'Closed';
		badge.className = 'badge bg-dark px-3 py-2';
	}
	const submitBtn = document.getElementById('submitBtn');
	if (submitBtn) {
		submitBtn.disabled = true;
		submitBtn.textContent = 'Closed — Past Due';
	}
}

let responseAttachments = [];
let responseAttachIdSeq = 0;

const RESPONSE_ALLOWED_ATTACHMENT_TYPES = [
	'application/pdf',
	'application/msword',
	'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
	'application/vnd.ms-excel',
	'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
];

function responseEscapeHtml(str) {
	return String(str).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function responseFileIconFor(fileName) {
	const ext = (fileName.split('.').pop() || '').toLowerCase();
	if (ext === 'pdf') return '📕';
	if (ext === 'doc' || ext === 'docx') return '📄';
	if (ext === 'xls' || ext === 'xlsx') return '📊';
	return '📎';
}

function responseReadAsDataUrl(file) {
	return new Promise((resolve, reject) => {
		const reader = new FileReader();
		reader.onload = e => resolve(e.target.result);
		reader.onerror = reject;
		reader.readAsDataURL(file);
	});
}

function responseCompressImageFile(file, maxDimension = 1600, quality = 0.82) {
	return responseReadAsDataUrl(file).then(originalDataUrl => new Promise(resolve => {
		const img = new Image();
		img.onload = () => {
			let { width, height } = img;
			if (width <= maxDimension && height <= maxDimension) {
				resolve(originalDataUrl);
				return;
			}
			const scale = maxDimension / Math.max(width, height);
			width = Math.round(width * scale);
			height = Math.round(height * scale);
			const canvas = document.createElement('canvas');
			canvas.width = width;
			canvas.height = height;
			canvas.getContext('2d').drawImage(img, 0, 0, width, height);
			resolve(canvas.toDataURL('image/jpeg', quality));
		};
		img.onerror = () => resolve(originalDataUrl);
		img.src = originalDataUrl;
	}));
}

function handleResponseAttachFiles(event) {
	const files = Array.from(event.target.files || []);
	files.forEach(file => {
		if (!file.type.startsWith('image/') && !RESPONSE_ALLOWED_ATTACHMENT_TYPES.includes(file.type)) return;
		const isImage = file.type.startsWith('image/');
		const process = isImage ? responseCompressImageFile(file) : responseReadAsDataUrl(file);
		process.then(dataUrl => {
			responseAttachments.push({ id: ++responseAttachIdSeq, dataUrl, caption: '', fileName: file.name, isImage });
			renderResponseAttachments();
		});
	});
	event.target.value = '';
}

function removeResponseAttachment(id) {
	responseAttachments = responseAttachments.filter(a => a.id !== id);
	renderResponseAttachments();
}

function updateResponseCaption(id, value) {
	const a = responseAttachments.find(a => a.id === id);
	if (a) a.caption = value;
}

function renderResponseAttachments() {
	const grid = document.getElementById('responseAttachGrid');
	if (!grid) return;
	grid.innerHTML = responseAttachments.map(a => `
		<div class="col">
			<div class="card response-attach-thumb h-100">
				<button type="button" class="btn-close" aria-label="Remove" onclick="removeResponseAttachment(${a.id})"></button>
				${a.isImage
					? `<img src="${a.dataUrl}" class="card-img-top" alt="Attachment">`
					: `<div class="card-img-top d-flex flex-column align-items-center justify-content-center bg-light" style="aspect-ratio:4/3;">
						   <span style="font-size:2rem;line-height:1;">${responseFileIconFor(a.fileName)}</span>
						   <small class="text-secondary text-truncate px-2" style="max-width:90%;">${responseEscapeHtml(a.fileName)}</small>
					   </div>`}
				<div class="card-body p-2">
					<input type="text" class="form-control form-control-sm" placeholder="Caption (optional)" value="${a.caption.replace(/"/g, '&quot;')}" oninput="updateResponseCaption(${a.id}, this.value)">
				</div>
			</div>
		</div>
	`).join('');
}

function val(id) {
	const el = document.getElementById(id);
	return el ? el.value.trim() : '';
}

async function submitResponse() {
	const required = [
		{ id: 'immediateAction', label: 'Correction / Immediate Action Taken' },
		{ id: 'iaPerson', label: 'Person Responsible (Immediate Action)' },
		{ id: 'iaTargetDate', label: 'Date Completed' },
		{ id: 'rootCause', label: 'Root Cause Analysis' },
		{ id: 'capaActions', label: 'Corrective Action' },
		{ id: 'capaPerson', label: 'Person Responsible (Corrective Action)' },
		{ id: 'capaTargetDate', label: 'Target Completion Date' }
	];
	const missing = required.filter(f => !val(f.id)).map(f => f.label);

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
	btn.disabled = true;
	btn.textContent = 'Submitting…';

	const payload = {
		ResponseToken: RESPONSE_TOKEN,
		ImmediateAction: val('immediateAction'),
		IaPerson: val('iaPerson'),
		IaTargetDate: val('iaTargetDate') || null,
		RootCause: val('rootCause'),
		CapaActions: val('capaActions'),
		CapaPerson: val('capaPerson'),
		CapaTargetDate: val('capaTargetDate') || null,
		Attachments: responseAttachments.map(a => ({ Caption: a.caption, DataUrl: a.dataUrl }))
	};

	try {
		const response = await fetch(SUBMIT_ENDPOINT, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(payload)
		});
		const result = await response.json().catch(() => null);

		if (response.ok && result && result.success) {
			Swal.fire({
				icon: 'success',
				title: 'Response submitted',
				text: 'Thank you — QA has been notified and will verify your response.',
				confirmButtonColor: '#7d1f27'
			}).then(() => {
				location.reload();
			});
		} else if (response.status === 409) {
			Swal.fire({
				icon: 'info',
				title: 'Already submitted',
				text: (result && result.message) || 'This CAPA has already been responded to.',
				confirmButtonColor: '#7d1f27'
			}).then(() => location.reload());
		} else {
			Swal.fire({
				icon: 'error',
				title: 'Could not submit',
				text: (result && result.message) || `HTTP ${response.status}`,
				confirmButtonColor: '#7d1f27'
			});
		}
	} catch (err) {
		Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: '#7d1f27' });
	} finally {
		btn.disabled = false;
		btn.textContent = 'Submit Response';
	}
}

/* ---------- Lightbox (Facebook-style photo preview) ---------- */
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

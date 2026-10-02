const CANCEL_ENDPOINT = window.CAPA_STATUS_DATA.cancelEndpoint;
const CANCEL_TOKEN = window.CAPA_STATUS_DATA.cancelToken;

async function cancelCar() {
	const confirmResult = await Swal.fire({
		icon: 'warning',
		title: 'Cancel this CAR?',
		text: 'This cannot be undone.',
		showCancelButton: true,
		confirmButtonText: 'Yes, cancel it',
		cancelButtonText: 'No, keep it',
		confirmButtonColor: '#7d1f27'
	});
	if (!confirmResult.isConfirmed) return;

	const btn = document.getElementById('cancelCarBtn');
	btn.disabled = true;
	btn.textContent = 'Cancelling…';

	try {
		const response = await fetch(CANCEL_ENDPOINT, {
			method: 'POST',
			headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
			body: 'token=' + encodeURIComponent(CANCEL_TOKEN)
		});
		const result = await response.json().catch(() => null);

		if (response.ok && result && result.success) {
			Swal.fire({
				icon: 'success',
				title: 'CAR cancelled',
				confirmButtonColor: '#7d1f27'
			}).then(() => location.reload());
		} else {
			Swal.fire({
				icon: 'error',
				title: 'Could not cancel',
				text: (result && result.message) || `HTTP ${response.status}`,
				confirmButtonColor: '#7d1f27'
			});
			btn.disabled = false;
			btn.textContent = 'Cancel CAR';
		}
	} catch (err) {
		Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: '#7d1f27' });
		btn.disabled = false;
		btn.textContent = 'Cancel CAR';
	}
}

const ATTACHMENT_PHOTOS = window.CAPA_STATUS_DATA.attachmentPhotos;
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

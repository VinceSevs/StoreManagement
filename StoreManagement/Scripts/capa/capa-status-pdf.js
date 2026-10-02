function downloadCapaStatusPdf() {
	const btn = document.getElementById('downloadPdfBtn');
	const source = document.getElementById('capaStatusPrintArea');
	const capaNo = window.CAPA_STATUS_PDF_DATA.reportNumber;
	const carStatus = window.CAPA_STATUS_PDF_DATA.overallStatus.replace(/\s+/g, '-');

	btn.disabled = true;
	btn.textContent = 'Generating...';

	const clone = source.cloneNode(true);
	clone.removeAttribute('id');

	clone.querySelectorAll('.readonly-field').forEach(el => {
		el.style.backgroundColor = '#fff';
		el.style.border = '1px solid #000';
	});

	const photosBlocks = clone.querySelectorAll('.attached-photos-block');

	if (photosBlocks.length > 0) {
		const photosPage = document.createElement('div');
		photosPage.id = 'pdfAttachmentsPage';
		photosPage.className = 'card mb-3 stage-card';
		photosPage.innerHTML = '<div class="card-header bg-white fw-semibold">Attached Photos</div><div class="card-body"></div>';
		const photosBody = photosPage.querySelector('.card-body');

		photosBlocks.forEach(photosBlock => {
			photosBlock.remove();
			photosBlock.classList.remove('col-12');

			const photosRow = photosBlock.querySelector('.d-flex');
			if (photosRow) {
				photosRow.classList.remove('d-flex', 'flex-wrap', 'gap-2');
				photosRow.style.display = 'grid';
				photosRow.style.gridTemplateColumns = '1fr 1fr';
				photosRow.style.gap = '10px';
			}

			photosBlock.querySelectorAll('.attach-thumb-btn').forEach(btnEl => {
				btnEl.removeAttribute('onclick');
				btnEl.style.cursor = 'default';
				btnEl.style.width = '100%';
			});
			photosBlock.querySelectorAll('.attach-thumb-btn img').forEach(img => {
				img.style.width = '100%';
				img.style.height = '220px';
			});

			photosBody.appendChild(photosBlock);
		});

		clone.appendChild(photosPage);
	}
	document.body.appendChild(clone);

	const opt = {
		margin: 0.4,
		filename: 'CAR-Status-' + capaNo + '-' + carStatus + '.pdf',
		image: { type: 'jpeg', quality: 0.98 },
		html2canvas: { scale: 2, useCORS: true },
		jsPDF: { unit: 'in', format: 'letter', orientation: 'portrait' },
		pagebreak: { mode: ['avoid-all', 'css'], before: '#pdfAttachmentsPage', avoid: ['.stage-card', '.pdf-item-block', '.attach-thumb-btn'] }
	};

	html2pdf().set(opt).from(clone).save().then(() => {
		clone.remove();
		btn.disabled = false;
		btn.innerHTML = '<i class="bi bi-download"></i> Download PDF';

		fetch(window.CAPA_STATUS_PDF_DATA.logCapaActionUrl, {
			method: 'POST',
			headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
			body: 'logType=' + encodeURIComponent('Download as PDF') + '&reportNumber=' + encodeURIComponent(capaNo)
		}).catch(() => {});
	}).catch((err) => {
		clone.remove();
		btn.disabled = false;
		btn.innerHTML = '<i class="bi bi-download"></i> Download PDF';
		console.error('PDF generation failed:', err);
		alert('Could not generate PDF: ' + (err && err.message ? err.message : err));
	});
}

if (new URLSearchParams(window.location.search).get('autoDownloadPdf') === '1') {
	downloadCapaStatusPdf();
}

const PALLET_TAG_ITEMS = window.CAPA_STATUS_PALLET_TAG_DATA.palletTagItems;
const PALLET_TAG_STATUS_URL = window.CAPA_STATUS_PALLET_TAG_DATA.palletTagStatusUrl;

const DEFECT_CATEGORY_COLORS = {
	'Packaging': '#2563EB',
	'Shelf Life': '#D97706',
	'Temperature': '#0891B2',
	'Contamination': '#DC2626',
	'Quality': '#7C3AED',
	'Labeling': '#CA8A04',
	'Handling': '#059669',
	'Other': '#6B7280'
};
function colorForCategory(category) {
	return DEFECT_CATEGORY_COLORS[category] || DEFECT_CATEGORY_COLORS['Other'];
}

function generateQrDataUrl(text, size) {
	const holder = document.createElement('div');
	holder.style.position = 'fixed';
	holder.style.left = '-9999px';
	document.body.appendChild(holder);
	new QRCode(holder, { text: text, width: size, height: size, correctLevel: QRCode.CorrectLevel.M });
	const canvas = holder.querySelector('canvas');
	const dataUrl = canvas ? canvas.toDataURL('image/png') : (holder.querySelector('img') ? holder.querySelector('img').src : '');
	holder.remove();
	return dataUrl;
}

function printPalletTags() {
	const btn = document.getElementById('printPalletTagsBtn');
	const capaNo = window.CAPA_STATUS_PALLET_TAG_DATA.reportNumber;
	const dateCreated = window.CAPA_STATUS_PALLET_TAG_DATA.dateCreated;

	if (!PALLET_TAG_ITEMS || PALLET_TAG_ITEMS.length === 0) {
		alert('This CAR has no items to print pallet tags for.');
		return;
	}

	btn.disabled = true;
	btn.innerHTML = 'Generating...';

	const qrDataUrl = generateQrDataUrl(PALLET_TAG_STATUS_URL, 260);

	const container = document.createElement('div');

	PALLET_TAG_ITEMS.forEach(function (item) {
		const total = item.quantity > 0 ? item.quantity : 1;
		const categoryColor = colorForCategory(item.defectCategory);
		for (let i = 1; i <= total; i++) {
			const tag = document.createElement('div');
			tag.style.cssText = 'box-sizing:border-box;width:100%;min-height:9.5in;display:flex;align-items:center;justify-content:center;background:#fff;page-break-after:always;';
			tag.innerHTML =
				'<div style="text-align:center;border:2px solid #000;border-left:12px solid ' + categoryColor + ';padding:60px 50px;font-family:Arial,Helvetica,sans-serif;color:#000;min-width:5in;">' +
					'<div style="display:inline-block;padding:5px 16px;border-radius:20px;background:' + categoryColor + ';color:#fff;font-size:12px;font-weight:bold;letter-spacing:1px;text-transform:uppercase;margin-bottom:20px;">' + item.defectCategory + '</div>' +
					'<div style="font-size:26px;font-weight:bold;margin-bottom:18px;">' + capaNo + '</div>' +
					'<div style="font-size:16px;margin-bottom:10px;"><strong>Item:</strong> ' + (item.itemDescription || '') + '</div>' +
					'<div style="font-size:16px;margin-bottom:10px;"><strong>Defect:</strong> ' + (item.defect || '') + '</div>' +
					'<div style="font-size:14px;color:#444;margin-bottom:30px;"><strong>Date:</strong> ' + dateCreated + '</div>' +
					'<div style="font-size:40px;font-weight:bold;margin-bottom:24px;">' + i + ' / ' + total + '</div>' +
					'<img src="' + qrDataUrl + '" style="width:220px;height:220px;" />' +
					'<div style="font-size:11px;color:#777;margin-top:8px;">Scan to view this CAR</div>' +
				'</div>';
			container.appendChild(tag);
		}
	});

	if (container.lastChild) {
		container.lastChild.style.pageBreakAfter = 'auto';
	}

	document.body.appendChild(container);

	const opt = {
		margin: 0.4,
		filename: capaNo + '-Pallet-Tags.pdf',
		image: { type: 'jpeg', quality: 0.98 },
		html2canvas: { scale: 2, useCORS: true },
		jsPDF: { unit: 'in', format: 'letter', orientation: 'portrait' },
		pagebreak: { mode: ['css'] }
	};

	html2pdf().set(opt).from(container).save().then(() => {
		container.remove();
		btn.disabled = false;
		btn.innerHTML = '<i class="bi bi-tags"></i> Print Pallet Tags';
	}).catch((err) => {
		container.remove();
		btn.disabled = false;
		btn.innerHTML = '<i class="bi bi-tags"></i> Print Pallet Tags';
		console.error('Pallet tag PDF generation failed:', err);
		alert('Could not generate PDF: ' + (err && err.message ? err.message : err));
	});
}

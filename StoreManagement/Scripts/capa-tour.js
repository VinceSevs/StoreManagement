function buildFormTourSteps() {
	const steps = [];

	function addStep(selector, icon, title, intro, position, coverClass) {
		const el = typeof selector === 'string' ? document.querySelector(selector) : selector;
		if (selector !== null && (!el || el.offsetParent === null)) return;
		const step = { intro: intro, position: position || 'bottom', title: `${icon} ${title}` };
		if (el) step.element = el;
		if (coverClass) step.tooltipClass = 'capa-tour-tooltip capa-tour-cover';
		steps.push(step);
	}

	addStep(null, '👋', "Let's get you started", "This walks you through the CAPA form, field by field, top to bottom. Use the arrows or Esc anytime to skip ahead.", null, true);

	addStep('#docDateInput', '📅', 'Date', "Defaults to today automatically, but you can change it if needed.", 'left');
	addStep('#issuedByInput', '🙋', 'Issued By', "Auto-filled from your logged-in account. ");
	addStep('[name="capa_no"]', '🔖', 'CAR No.', "Generated automatically once you submit — you don't need to fill this in yourself.");
	addStep('#issuedToSelect', '🏢', 'Issued To', "Which department this is being raised to.");
	addStep('[name="ref_no"]', '📄', 'Reference No.', "Your QAR or PNCR reference number, if this CAPA is tied to one.");
	addStep('#siteWarehouseSelect', '🏭', 'Site Warehouse', "The warehouse where this was found. Picking one auto-fills the code next to it.");
	addStep('#warehouseCodeInput', '🔢', 'Warehouse Code', "Fills in automatically — no need to type this yourself.");

	addStep('#correctionDueInput', '⏰', 'Correction Due', "Defaults to the next business day — if that falls on a weekend, it automatically rolls forward to the following Monday.", 'top');
	addStep('#capaDueInput', '📆', 'CAPA Due', "Defaults to one week out — the deadline for the full corrective action.", 'top');

	addStep('#nonconformanceTypeSection', '⚠️', 'Type of Issue', "Pick exactly one category that best describes what happened.", 'top');
	addStep('#externalAuditSubWrap', '🕵️', 'Audit Type', "Since this is an External Quality Audit, also pick Government or 3rd Party.", 'top');
	addStep('#carClassificationSection', '👥', "Who's Responsible", "Pick who should respond to this CAPA — LLII, Supplier, Trucker, DC Site, or Others.", 'top');
	addStep('#carSupplierSelect', '🏬', 'Supplier', "Pick the specific supplier responsible.", 'top');
	addStep('#carTruckerSelect', '🚚', 'Trucker', "Pick the specific trucker responsible.", 'top');
	addStep('#carDcSiteSelect', '📍', 'DC Site', "Pick the specific DC site responsible.", 'top');
	addStep('#carOtherText', '✏️', 'Specify', "Type who's responsible here.", 'top');

	addStep('[name="stock_event"]', '📦', 'Stock Movement', "Pick whether this was recorded as an SA OUT or an OUT WRITE.", 'top');

	addStep('[data-field="item_id"]', '📦', 'Item', "Pick the affected item from the list.", 'top');
	addStep('[data-field="quantity"]', '🔢', 'Quantity', "How much of this item is affected.", 'top');
	addStep('[data-field="uom_id"]', '📏', 'UOM', "The unit of measure for the quantity above.", 'top');
	addStep('[data-field="defect_category_id"]', '🏷️', 'Defect Category', "Pick the defect category that best matches the issue.", 'top');
	addStep('[data-field="used_to_date"]', '📅', 'Used-to Date', "The item's used-to / best-before date, if applicable.", 'top');
	addStep('[data-field="hauler"]', '🚛', 'Hauler', "The accredited hauler who took it away, if applicable.", 'top');
	addStep('[data-field="remarks"]', '📝', 'Remarks', "Describe what happened — what, when, where, and which product/batch/shipment is affected. Be specific.", 'top');

	const attachCard = document.querySelector('#capaItemBlock0 .item-attach-input')?.closest('.card');
	addStep(attachCard || document.querySelector('#capaItemBlock0 .item-attach-empty'), '📸', 'Photos', "Attach photos of the issue here — multiple files are fine. Nothing uploads until you submit.", 'top');

	addStep('#submitBtn', '✅', 'Submit', "Saves the report and generates its CAPA number.", 'left');

	addStep(null, '🎉', "You're all set!", "That's everything. Fill in what you can, and don't worry about the order — you can always come back to a field before submitting.", null, true);

	return steps;
}

function startFormTour() {
	introJs().setOptions({
		steps: buildFormTourSteps(),
		showProgress: true,
		showBullets: false,
		exitOnOverlayClick: true,
		disableInteraction: false,
		tooltipClass: 'capa-tour-tooltip',
		highlightClass: 'capa-tour-highlight',
		nextLabel: 'Next →',
		prevLabel: '← Back',
		skipLabel: 'Skip tour',
		doneLabel: 'Got it! 🎉'
	}).start();
}

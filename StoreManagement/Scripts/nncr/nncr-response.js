// NNCR Response page (Views/Nncr/NncrResponse.cshtml) - supplier-facing, anonymous.
// Config: window.NNCR_RESPONSE_DATA = { responseToken, reportNumber, submitEndpoint }
(function () {
	'use strict';

	var CONFIG = window.NNCR_RESPONSE_DATA || {};
	var BRAND_COLOR = '#7d1f27';

	function setBadge(text, cls) {
		var badge = document.getElementById('nncrStatusBadge');
		if (!badge) return;
		badge.textContent = text;
		badge.className = 'badge nncr-status-badge ' + cls + ' px-3 py-2';
	}

	function setStatus(text, cls) {
		var el = document.getElementById('submitStatus');
		if (!el) return;
		el.textContent = text || '';
		el.className = 'me-auto small fw-semibold' + (cls ? ' ' + cls : '');
	}

	function showThankYou(remarks, decisionText, dateText) {
		var form = document.getElementById('responseForm');
		var bar = document.getElementById('responseSubmitBar');
		var card = document.getElementById('responseThankYou');
		if (form) form.classList.add('d-none');
		if (bar) bar.classList.add('d-none');
		if (!card) return;

		document.getElementById('thankYouRemarks').textContent = remarks;
		document.getElementById('thankYouDecision').textContent = decisionText;
		document.getElementById('thankYouDate').textContent = dateText;
		card.classList.remove('d-none');
		card.scrollIntoView({ behavior: 'smooth', block: 'start' });
	}

	async function submitResponse() {
		var remarksEl = document.getElementById('vendorRemarks');
		var decisionEl = document.getElementById('supplierDecision');
		var dateEl = document.getElementById('responseDate');
		var btn = document.getElementById('submitBtn');

		var remarks = remarksEl.value.trim();
		var decision = decisionEl.value;

		var missing = [];
		remarksEl.classList.toggle('is-invalid', !remarks);
		if (!remarks) missing.push('Remarks');

		if (missing.length > 0) {
			var list = document.createElement('ul');
			list.style.cssText = 'text-align:left;margin:0;padding-left:1.2rem';
			missing.forEach(function (m) {
				var li = document.createElement('li');
				li.textContent = m;
				list.appendChild(li);
			});
			Swal.fire({
				icon: 'warning',
				title: 'Missing required fields',
				html: list,
				confirmButtonColor: BRAND_COLOR
			});
			return;
		}

		var decisionText = decisionEl.options[decisionEl.selectedIndex].text;
		var confirm = await Swal.fire({
			icon: 'question',
			title: 'Submit your response?',
			text: 'Decision: ' + decisionText + '. Once submitted, your response to ' + (CONFIG.reportNumber || 'this NNCR') + ' cannot be changed.',
			showCancelButton: true,
			confirmButtonText: 'Yes, submit',
			cancelButtonText: 'Cancel',
			confirmButtonColor: BRAND_COLOR
		});
		if (!confirm.isConfirmed) return;

		btn.disabled = true;
		btn.textContent = 'Submitting...';
		setStatus('Submitting...', 'text-secondary');

		try {
			var response = await fetch(CONFIG.submitEndpoint, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({
					ResponseToken: CONFIG.responseToken,
					Decision: decision,
					VendorRemarks: remarks
				})
			});
			var result = await response.json().catch(function () { return null; });

			if (response.ok && result && result.success) {
				setBadge('Responded', 'is-responded');
				showThankYou(remarks, decisionText, dateEl ? dateEl.value : '');
				Swal.fire({
					icon: 'success',
					title: 'Response submitted',
					text: 'Thank you. Your response has been recorded.',
					confirmButtonColor: BRAND_COLOR
				});
				return;
			}

			var message = (result && result.message) || ('HTTP ' + response.status);
			setStatus('', '');
			await Swal.fire({
				icon: 'error',
				title: 'Could not submit response',
				text: message,
				confirmButtonColor: BRAND_COLOR
			});
			// 409: not open for a response any more (already answered) - reload to show the current state.
			if (response.status === 409) {
				location.reload();
				return;
			}
		} catch (err) {
			setStatus('', '');
			Swal.fire({ icon: 'error', title: 'Network error', text: err.message, confirmButtonColor: BRAND_COLOR });
		}

		btn.disabled = false;
		btn.textContent = 'Submit Response';
	}

	function init() {
		var btn = document.getElementById('submitBtn');
		if (btn) btn.addEventListener('click', submitResponse);

		['vendorRemarks'].forEach(function (id) {
			var el = document.getElementById(id);
			if (!el) return;
			var clear = function () { el.classList.remove('is-invalid'); };
			el.addEventListener('input', clear);
			el.addEventListener('change', clear);
		});
	}

	if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
	else init();
})();

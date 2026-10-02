    (function () {
        pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';

        var drop = document.getElementById('rcptDrop');
        var fileInput = document.getElementById('rcptFile');
        var onlyEsi = document.getElementById('rcptOnlyEsi');
        var statusEl = document.getElementById('rcptStatus');
        var resultCard = document.getElementById('rcptResult');
        var preview = document.getElementById('rcptPreview');
        var money = FinanceReceiptPdf.money;

        var parsed = [];      // FinancePdfToExcel.parse() results, one per file
        var receiptRows = []; // all rows mapped for receipts
        var pdfDoc = null;
        var pdfUrl = null;
        var baseName = 'TollReceipts';

        function setStatus(html, cls) {
            statusEl.className = 'rcpt-status ' + (cls || '');
            statusEl.innerHTML = html;
        }

        function esc(s) {
            var d = document.createElement('div');
            d.textContent = s == null ? '' : String(s);
            return d.innerHTML;
        }

        function selectedRows() {
            return onlyEsi.checked ? receiptRows.filter(function (r) { return r.esi; }) : receiptRows;
        }

        function stat(label, value, accent) {
            return '<div class="rcpt-stat' + (accent ? ' accent' : '') + '"><div class="label">' + label +
                '</div><div class="value" title="' + esc(value) + '">' + esc(value) + '</div></div>';
        }

        function generate() {
            var rows = selectedRows();
            if (pdfUrl) { URL.revokeObjectURL(pdfUrl); pdfUrl = null; }
            pdfDoc = rows.length ? FinanceReceiptPdf.buildPdf(rows) : null;
            if (pdfDoc) {
                pdfUrl = URL.createObjectURL(pdfDoc.output('blob'));
                preview.src = pdfUrl + '#view=FitH';
            } else {
                preview.removeAttribute('src');
            }

            var total = rows.reduce(function (a, r) { return a + r.amounts.total; }, 0);
            var vat = rows.reduce(function (a, r) { return a + r.amounts.vat; }, 0);
            var h = parsed.length ? parsed[0].header : {};
            document.getElementById('rcptAccountName').textContent = h.accountName || 'Transaction History Report';
            document.getElementById('rcptAccountMeta').innerHTML =
                (h.accountNumber ? '<span><i class="bi bi-person-vcard"></i> Account ' + esc(h.accountNumber) + '</span>' : '') +
                (h.period ? '<span><i class="bi bi-calendar3"></i> ' + esc(h.period) + '</span>' : '') +
                (h.tin ? '<span><i class="bi bi-hash"></i> TIN ' + esc(h.tin) + '</span>' : '');
            document.getElementById('rcptStats').innerHTML =
                stat('Receipts', rows.length.toLocaleString()) +
                stat('Pages', Math.ceil(rows.length / 4).toLocaleString()) +
                stat('Total VAT', 'Php ' + money(vat)) +
                stat('Total Amount', 'Php ' + money(total), true);

            document.getElementById('rcptRows').innerHTML = receiptRows.map(function (r) {
                var included = !onlyEsi.checked || r.esi;
                return '<tr' + (included ? '' : ' class="rcpt-muted-row" title="Not included (no E-SI No.)"') + '>' +
                    '<td>' + esc(r.plateNumber) + '</td><td class="font-mono">' + esc(r.refNo) +
                    '</td><td class="font-mono">' + (r.esi ? esc(r.esi) : '&mdash;') + '</td><td>' + esc(r.date) +
                    '</td><td>' + esc(r.time) + '</td><td>' + esc(r.zone) + '</td><td>' + esc(r.entry) +
                    '</td><td>' + esc(r.exit) + '</td><td class="text-end">' + money(r.amounts.vatable) +
                    '</td><td class="text-end">' + money(r.amounts.vat) + '</td><td class="text-end fw-semibold">' +
                    money(r.amounts.total) + '</td></tr>';
            }).join('');

            document.getElementById('rcptPdfBtn').disabled = !pdfDoc;
            resultCard.classList.remove('d-none');
        }

        async function handleFiles(fileList) {
            var files = Array.prototype.slice.call(fileList || []).filter(function (f) {
                return /\.pdf$/i.test(f.name) || f.type === 'application/pdf';
            });
            if (!files.length) {
                setStatus('<i class="bi bi-exclamation-circle"></i> Please choose a PDF file.', 'text-danger');
                return;
            }

            document.getElementById('rcptFileNames').textContent = files.map(function (f) { return f.name; }).join(', ');
            baseName = files[0].name.replace(/\.[^.]+$/, '');
            setStatus('<span class="spinner-border spinner-border-sm me-1"></span> Reading PDF and generating receipts...', 'text-secondary');

            try {
                parsed = [];
                for (var i = 0; i < files.length; i++) {
                    parsed.push(await FinancePdfToExcel.parse(await files[i].arrayBuffer()));
                }
                receiptRows = FinanceReceiptPdf.fromParsed(parsed);
                if (!receiptRows.length) {
                    resultCard.classList.add('d-none');
                    setStatus('<i class="bi bi-exclamation-circle"></i> No toll transactions were found. Make sure this is a Transaction History Report PDF.', 'text-danger');
                    return;
                }
                generate();
                setStatus('<i class="bi bi-check-circle-fill"></i> ' + receiptRows.length + ' transaction(s) read &mdash; receipts generated.', 'text-success');
            } catch (e) {
                resultCard.classList.add('d-none');
                setStatus('<i class="bi bi-exclamation-circle"></i> Could not read the PDF: ' + esc(e.message), 'text-danger');
            }
        }

        fileInput.addEventListener('change', function () {
            handleFiles(fileInput.files);
            fileInput.value = '';
        });

        ['dragenter', 'dragover'].forEach(function (ev) {
            drop.addEventListener(ev, function (e) { e.preventDefault(); drop.classList.add('dragover'); });
        });
        ['dragleave', 'drop'].forEach(function (ev) {
            drop.addEventListener(ev, function (e) { e.preventDefault(); drop.classList.remove('dragover'); });
        });
        drop.addEventListener('drop', function (e) { handleFiles(e.dataTransfer.files); });

        onlyEsi.addEventListener('change', function () { if (receiptRows.length) generate(); });

        document.querySelectorAll('.rcpt-tabs button').forEach(function (btn) {
            btn.addEventListener('click', function () {
                document.querySelectorAll('.rcpt-tabs button').forEach(function (b) { b.classList.toggle('active', b === btn); });
                document.querySelectorAll('[data-panel]').forEach(function (p) {
                    p.classList.toggle('d-none', p.getAttribute('data-panel') !== btn.getAttribute('data-tab'));
                });
            });
        });

        document.getElementById('rcptPdfBtn').addEventListener('click', function () {
            if (pdfDoc) pdfDoc.save(baseName + '_Receipts.pdf');
        });

        document.getElementById('rcptExcelBtn').addEventListener('click', function () {
            if (parsed.length) XLSX.writeFile(FinancePdfToExcel.buildWorkbook(parsed), baseName + '.xlsx');
        });
    })();

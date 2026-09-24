/*
 * Finance > PDF Receipt (parser + optional Excel export)
 * Parses the SMC toll "Transaction History Report" PDF into one row per
 * toll transaction line. A Ref No. with several entry/exit legs (e.g. SLEX + SKYWAY)
 * produces one row per leg, all sharing the same Ref No.
 *
 * Requires pdf.js (pdfjsLib) and SheetJS (XLSX).
 */
(function (window) {
    'use strict';

    // Column x-ranges (PDF points) taken from the report's table header.
    var COLS = {
        date: [50, 85],
        time: [85, 122],
        esi: [122, 196],
        zone: [196, 232],
        entry: [232, 370],
        exit: [370, 505],
        fee: [505, 600]
    };
    var DATE_RE = /^\d{2}\/\d{2}\/\d{4}$/;
    var AMOUNT_RE = /^[\d,]+\.\d{2}$/;

    var EXCEL_HEADERS = [
        'Account Number', 'Account Name', 'TIN', 'Address', 'Period',
        'Tag Number', 'Plate Number', 'Ref Type', 'Ref No.', 'Date', 'Time',
        'E-SI No.', 'Zone', 'Entry', 'Exit', 'Toll Fee', 'Vatable Sales', 'VAT'
    ];

    function round2(n) {
        return Math.round((n + Number.EPSILON) * 100) / 100;
    }

    function toAmount(s) {
        var n = parseFloat(String(s || '').replace(/,/g, ''));
        return isNaN(n) ? 0 : n;
    }

    async function readPages(arrayBuffer) {
        var doc = await pdfjsLib.getDocument({ data: new Uint8Array(arrayBuffer) }).promise;
        var pages = [];
        for (var n = 1; n <= doc.numPages; n++) {
            var page = await doc.getPage(n);
            var height = page.getViewport({ scale: 1 }).height;
            var content = await page.getTextContent();
            pages.push(content.items
                .filter(function (i) { return i.str && i.str.trim(); })
                .map(function (i) {
                    return { s: i.str.trim(), x: i.transform[4], y: height - i.transform[5] };
                }));
        }
        return pages;
    }

    // Groups text items into visual lines (same y within tolerance), sorted top-down, left-right.
    function toLines(items) {
        var sorted = items.slice().sort(function (a, b) { return a.y - b.y || a.x - b.x; });
        var lines = [];
        sorted.forEach(function (t) {
            var line = null;
            for (var i = 0; i < lines.length; i++) {
                if (Math.abs(lines[i].y - t.y) <= 2) { line = lines[i]; break; }
            }
            if (!line) { line = { y: t.y, items: [] }; lines.push(line); }
            line.items.push(t);
        });
        lines.forEach(function (l) { l.items.sort(function (a, b) { return a.x - b.x; }); });
        return lines.sort(function (a, b) { return a.y - b.y; });
    }

    function colText(line, range) {
        return line.items
            .filter(function (i) { return i.x >= range[0] && i.x < range[1]; })
            .map(function (i) { return i.s; })
            .join(' ')
            .trim();
    }

    function parseHeader(firstPageItems) {
        var lines = toLines(firstPageItems);
        var header = { accountNumber: '', accountName: '', tin: '', address: '', period: '' };
        var valueRange = [90, 350];

        function valueOf(line) {
            return line.items
                .filter(function (i) { return i.x > valueRange[0] && i.x < valueRange[1] && i.s !== ':'; })
                .map(function (i) { return i.s; })
                .join(' ')
                .trim();
        }

        lines.forEach(function (line, idx) {
            var label = line.items[0].s;
            if (label === 'Account Number') header.accountNumber = valueOf(line);
            else if (label === 'Account Name') header.accountName = valueOf(line);
            else if (label === 'TIN' && !header.tin) header.tin = valueOf(line);
            else if (label === 'Address') {
                var parts = [valueOf(line)];
                // Address wraps onto following lines that have no label of their own.
                for (var j = idx + 1; j < lines.length && lines[j].y - line.y < 30; j++) {
                    if (lines[j].items.some(function (i) { return i.x < valueRange[0]; })) break;
                    var more = valueOf(lines[j]);
                    if (more) parts.push(more);
                }
                header.address = parts.join(' ').replace(/\s+/g, ' ').trim();
            }
            var periodItem = line.items.filter(function (i) { return /^Period\s*:?/.test(i.s); })[0];
            if (periodItem && !header.period) {
                header.period = line.items
                    .filter(function (i) { return i.x >= periodItem.x; })
                    .map(function (i) { return i.s; })
                    .join(' ')
                    .replace(/^Period\s*:?\s*/, '')
                    .trim();
            }
        });
        return header;
    }

    function parseTransactions(pages) {
        var rows = [];
        var state = { inUsage: false, tag: '', plate: '', refType: '', ref: '' };

        pages.forEach(function (items) {
            toLines(items).forEach(function (line) {
                var first = line.items[0];
                var text = line.items.map(function (i) { return i.s; }).join(' ');

                if (first.s === 'Usage' && first.x < 50) { state.inUsage = true; return; }
                if (/Replenishment|Summary of Total Usage/.test(text)) { state.inUsage = false; return; }
                if (!state.inUsage) return;

                var tag = text.match(/TagNumber\s*:\s*(\S+)\s+Plate Number\s*:\s*(\S*)/);
                if (tag) { state.tag = tag[1]; state.plate = tag[2] || ''; return; }

                if (/^(Ref|IER) No\./.test(first.s) && first.x < 40) {
                    state.refType = first.s.indexOf('IER') === 0 ? 'IER' : 'Ref';
                    var num = line.items.filter(function (i) { return i.x >= 40 && i.x < 90; })[0];
                    state.ref = num ? num.s : '';
                    return;
                }

                var dateItem = line.items.filter(function (i) {
                    return i.x >= COLS.date[0] && i.x < COLS.date[1] && DATE_RE.test(i.s);
                })[0];

                if (dateItem) {
                    rows.push({
                        tagNumber: state.tag,
                        plateNumber: state.plate,
                        refType: state.refType,
                        refNo: state.ref,
                        date: dateItem.s,
                        time: colText(line, COLS.time),
                        esi: colText(line, COLS.esi),
                        zone: colText(line, COLS.zone),
                        entry: colText(line, COLS.entry),
                        exit: colText(line, COLS.exit),
                        tollFee: toAmount(colText(line, COLS.fee))
                    });
                    return;
                }

                // Long entry/exit names wrap onto a line of their own; append to the previous row.
                var last = rows[rows.length - 1];
                if (!last || /Total|TransNo|Page \d+ of/.test(text)) return;
                var entryMore = colText(line, COLS.entry);
                var exitMore = colText(line, COLS.exit);
                if (entryMore) last.entry = (last.entry + ' ' + entryMore).trim();
                if (exitMore) last.exit = (last.exit + ' ' + exitMore).trim();
            });
        });
        return rows;
    }

    function padTime(t) {
        var p = String(t || '').split(':');
        if (p.length !== 3) return t;
        return p.map(function (x) { return ('0' + x).slice(-2); }).join(':');
    }

    async function parse(arrayBuffer) {
        var pages = await readPages(arrayBuffer);
        if (!pages.length) throw new Error('The PDF has no pages.');
        var header = parseHeader(pages[0]);
        var rows = parseTransactions(pages);
        rows.forEach(function (r) {
            r.time = padTime(r.time);
            r.vatable = round2(r.tollFee / 1.12);
            r.vat = round2(r.tollFee - r.vatable);
        });
        return { header: header, rows: rows };
    }

    function buildWorkbook(results) {
        var aoa = [EXCEL_HEADERS];
        results.forEach(function (res) {
            var h = res.header;
            res.rows.forEach(function (r) {
                aoa.push([
                    h.accountNumber, h.accountName, h.tin, h.address, h.period,
                    r.tagNumber, r.plateNumber, r.refType, r.refNo, r.date, r.time,
                    r.esi, r.zone, r.entry, r.exit, r.tollFee, r.vatable, r.vat
                ]);
            });
        });

        var ws = XLSX.utils.aoa_to_sheet(aoa);
        var range = XLSX.utils.decode_range(ws['!ref']);
        for (var R = 1; R <= range.e.r; R++) {
            // Keep ids/dates as text so Excel does not reformat them.
            [0, 2, 5, 8, 9, 10, 11].forEach(function (C) {
                var cell = ws[XLSX.utils.encode_cell({ r: R, c: C })];
                if (cell) { cell.t = 's'; cell.v = String(cell.v); }
            });
            [15, 16, 17].forEach(function (C) {
                var cell = ws[XLSX.utils.encode_cell({ r: R, c: C })];
                if (cell) cell.z = '#,##0.00';
            });
        }
        ws['!cols'] = [12, 30, 16, 40, 24, 13, 13, 9, 13, 11, 9, 20, 9, 32, 32, 11, 13, 10]
            .map(function (w) { return { wch: w }; });
        ws['!autofilter'] = { ref: ws['!ref'] };

        var wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, 'Transactions');
        return wb;
    }

    window.FinancePdfToExcel = {
        parse: parse,
        buildWorkbook: buildWorkbook,
        headers: EXCEL_HEADERS
    };
})(window);

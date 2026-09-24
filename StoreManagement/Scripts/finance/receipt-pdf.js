/*
 * Finance > PDF Receipt
 * Renders sales-invoice style receipts from parsed toll transactions (see pdf-to-excel.js),
 * 4 per A4 page (2 x 2). VAT is always 12%, 2 decimals.
 *
 * Requires jsPDF (window.jspdf.jsPDF).
 */
(function (window) {
    'use strict';

    var VAT_RATE = 0.12;

    // Fixed receipt header, same on every receipt regardless of zone.
    var HEADER = {
        name: 'SMC SLEX INC.',
        lines: [
            '11/F SAN MIGUEL PROPERTIES CENTRE 7 ST. FRANCIS',
            'STREET, ORTIGAS CENTER WACK-WACK GREENHILLS CITY',
            'OF MANDALUYONG NCR, SECOND DISTRICT PHILIPPINES',
            'Business Style: SMC SLEX INC.',
            'VAT Reg TIN: 207-247-094-00000'
        ]
    };

    var DEFAULT_FOOTER = [
        'Accreditation No.: 0410084643192023091844',
        'Series: SLEX000000000000-999999999999',
        'Date Issued: October 25, 2023'
    ];

    var DEFAULT_CUSTOMER = {
        name: 'LEAD LOGISTICS INNOVATIONS, INC',
        tin: '009-789-818-000',
        address: '40 SUMULONG HIGHWAY BRGY. NINO, MARIKINA CITY',
        businessStyle: ''
    };

    function round2(n) { return Math.round((n + Number.EPSILON) * 100) / 100; }

    function money(n) {
        return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function computeVat(total) {
        var t = round2(total);
        var vatable = round2(t / (1 + VAT_RATE));
        return { total: t, vatable: vatable, vat: round2(t - vatable) };
    }

    // Maps FinancePdfToExcel.parse() results to receipt rows.
    function fromParsed(results) {
        var rows = [];
        results.forEach(function (res) {
            var h = res.header || {};
            res.rows.forEach(function (r) {
                rows.push({
                    esi: r.esi,
                    date: r.date,
                    time: r.time,
                    customerName: h.accountName,
                    tin: h.tin,
                    businessStyle: '',
                    address: h.address,
                    zone: String(r.zone || '').toUpperCase(),
                    entry: r.entry,
                    exit: r.exit,
                    refNo: r.refNo,
                    plateNumber: r.plateNumber,
                    amounts: computeVat(r.tollFee)
                });
            });
        });
        return rows;
    }

    // Draws one receipt inside the cell whose top-left corner is (ox, oy); cell is w x h mm.
    function drawReceipt(doc, r, customer, ox, oy, w, h) {
        var cx = ox + w / 2;
        var left = ox + 10;
        var right = ox + w - 10;
        var y = oy + 12;

        function dashed(yy, x1, x2) {
            doc.setLineDashPattern([0.5, 0.4], 0);
            doc.setLineWidth(0.2);
            doc.line(x1 || left, yy, x2 || right, yy);
            doc.setLineDashPattern([], 0);
        }
        function solid(yy) {
            doc.setLineWidth(0.3);
            doc.line(left, yy, right, yy);
        }

        // Header (fixed)
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(9);
        doc.text(HEADER.name, cx, y, { align: 'center' });
        y += 4;
        doc.setFontSize(7.4);
        HEADER.lines.forEach(function (line) {
            doc.text(line, cx, y, { align: 'center' });
            y += 3.4;
        });
        y += 0.6;

        // Invoice number
        dashed(y);
        y += 3.8;
        doc.setFontSize(7.4);
        doc.text('SALES INVOICE NUMBER: ' + (r.esi || ''), left, y);
        y += 2;
        dashed(y);
        y += 4.8;

        // Customer details
        var colonX = left + 27, valueX = left + 30, valueW = right - valueX;
        [
            ['Date', r.date],
            ['Time', r.time],
            ['Customer Name', r.customerName || customer.name],
            ['TIN', r.tin || customer.tin],
            ['Business Style', r.businessStyle || customer.businessStyle],
            ['Address', r.address || customer.address]
        ].forEach(function (f) {
            doc.setFont('helvetica', 'bold');
            doc.setFontSize(7);
            doc.text(f[0], left, y);
            doc.text(':', colonX, y);
            doc.setFont('helvetica', 'normal');
            var lines = doc.splitTextToSize(String(f[1] || ''), valueW);
            lines.forEach(function (line, i) { doc.text(line, valueX, y + i * 3.1); });
            y += 4.3 + Math.max(0, lines.length - 1) * 3.1;
        });
        y -= 1.2;
        solid(y);
        y += 4;

        // Entry / Exit
        [['Entry', r.entry], ['Exit', r.exit]].forEach(function (f) {
            doc.setFont('helvetica', 'bold');
            doc.text(f[0], left, y);
            doc.setFont('helvetica', 'normal');
            doc.text(': ' + (f[1] || ''), left + 17, y);
            y += 4.3;
        });
        y -= 1.8;
        dashed(y);
        y += 5;

        // Amounts (VAT 12%, always 2 decimals)
        var amtLabelX = left + 25, amtRightX = right - 10;
        var a = r.amounts;
        doc.setFont('helvetica', 'normal');
        [['Vatable Sales', money(a.vatable)],
            ['VAT-Exempt Sales', '0.00'],
            ['Zero-Rated Sales', '0.00'],
            ['VAT', money(a.vat)]].forEach(function (f) {
            doc.text(f[0], amtLabelX, y);
            doc.text(f[1], amtRightX, y, { align: 'right' });
            y += 4.3;
        });
        y -= 2.2;
        dashed(y, amtLabelX, amtRightX);
        y += 4.2;
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(7.6);
        doc.text('Total Amount', amtLabelX, y);
        doc.text('Php ' + money(a.total), amtRightX, y, { align: 'right' });

        // Tagline + footer (default), anchored to the bottom of the cell
        var fy = oy + h - 21;
        doc.setFont('helvetica', 'bolditalic');
        doc.setFontSize(7);
        doc.text('"THIS SERVES AS YOUR SALES INVOICE"', cx, fy - 8, { align: 'center' });

        dashed(fy);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(6.8);
        DEFAULT_FOOTER.forEach(function (line, i) {
            doc.text(line, right, fy + 4 + i * 3.3, { align: 'right' });
        });
    }

    function buildPdf(rows, customer) {
        var jsPDF = window.jspdf.jsPDF;
        var doc = new jsPDF({ unit: 'mm', format: 'a4', orientation: 'portrait' });
        var W = 210, H = 297, cw = W / 2, ch = H / 2;
        customer = customer || DEFAULT_CUSTOMER;
        doc.setProperties({ title: 'Toll Receipts' });

        rows.forEach(function (r, i) {
            var slot = i % 4;
            if (i > 0 && slot === 0) doc.addPage();
            if (slot === 0) {
                // Cut guides
                doc.setDrawColor(180);
                doc.setLineDashPattern([2, 2], 0);
                doc.setLineWidth(0.2);
                doc.line(cw, 4, cw, H - 4);
                doc.line(4, ch, W - 4, ch);
                doc.setLineDashPattern([], 0);
                doc.setDrawColor(0);
            }
            doc.setTextColor(0);
            drawReceipt(doc, r, customer, (slot % 2) * cw, Math.floor(slot / 2) * ch, cw, ch);
        });
        return doc;
    }

    window.FinanceReceiptPdf = {
        fromParsed: fromParsed,
        buildPdf: buildPdf,
        money: money
    };
})(window);

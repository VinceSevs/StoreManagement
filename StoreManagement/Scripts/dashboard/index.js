    var trendYearMonths = window.DASHBOARD_DATA.trendYearMonths;
    var trendLabels = window.DASHBOARD_DATA.trendLabels;
    var carTrend = window.DASHBOARD_DATA.carTrend;
    var pcrTrend = window.DASHBOARD_DATA.pcrTrend;
    var ncrTrend = window.DASHBOARD_DATA.ncrTrend;
    var complaintTrend = window.DASHBOARD_DATA.complaintTrend;
    var carClassificationLabels = window.DASHBOARD_DATA.carClassificationLabels;
    var carClassificationCounts = window.DASHBOARD_DATA.carClassificationCounts;

    var carStats = {
        open: window.DASHBOARD_DATA.carStats.open,
        verify: window.DASHBOARD_DATA.carStats.verify,
        closed: window.DASHBOARD_DATA.carStats.closed,
        cancelled: window.DASHBOARD_DATA.carStats.cancelled
    };

    var scopeData = {
        all: {
            total: window.DASHBOARD_DATA.complaintStats.total,
            open: window.DASHBOARD_DATA.complaintStats.open,
            closed: window.DASHBOARD_DATA.complaintStats.closed,
            cancelled: window.DASHBOARD_DATA.complaintStats.cancelled,
            trend: complaintTrend,
            trendLabel: 'PCR + NCR Filed',
            trendTitle: 'PCR + NCR Filed Over Time',
            summaryLabel: 'PCR/NCR',
            listUrl: window.DASHBOARD_DATA.complaintReportListUrl
        },
        pcr: {
            total: window.DASHBOARD_DATA.pcrStats.total,
            open: window.DASHBOARD_DATA.pcrStats.open,
            closed: window.DASHBOARD_DATA.pcrStats.closed,
            cancelled: window.DASHBOARD_DATA.pcrStats.cancelled,
            trend: pcrTrend,
            trendLabel: 'PCR Filed',
            trendTitle: 'PCR Filed Over Time',
            summaryLabel: 'PCR',
            listUrl: window.DASHBOARD_DATA.complaintReportListUrl
        },
        ncr: {
            total: window.DASHBOARD_DATA.ncrStats.total,
            open: window.DASHBOARD_DATA.ncrStats.open,
            closed: window.DASHBOARD_DATA.ncrStats.closed,
            cancelled: window.DASHBOARD_DATA.ncrStats.cancelled,
            trend: ncrTrend,
            trendLabel: 'NCR Filed',
            trendTitle: 'NCR Filed Over Time',
            summaryLabel: 'NCR',
            listUrl: window.DASHBOARD_DATA.ncrListUrl
        }
    };

    var pcrStats = { open: window.DASHBOARD_DATA.pcrStats.open, closed: window.DASHBOARD_DATA.pcrStats.closed, cancelled: window.DASHBOARD_DATA.pcrStats.cancelled };
    var ncrStats = { open: window.DASHBOARD_DATA.ncrStats.open, closed: window.DASHBOARD_DATA.ncrStats.closed, cancelled: window.DASHBOARD_DATA.ncrStats.cancelled };

    var currentComplaintScope = 'all';
    var currentRangeMonths = 6;

    Chart.defaults.font.family = "'Inter', Arial, sans-serif";
    Chart.defaults.color = '#8f7d7b';

    // A bad/empty dataset in one chart must never stop the rest of the
    // dashboard's charts from being created, so each one is isolated here.
    function safeCreateChart(canvasId, config) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return null;
        try {
            return new Chart(canvas, config);
        } catch (e) {
            console.error('Dashboard chart "' + canvasId + '" failed to render:', e);
            return null;
        }
    }

    function setSummary(elementId, text) {
        var el = document.getElementById(elementId);
        if (!el) return;
        var span = el.querySelector('span');
        if (span) span.textContent = text;
    }

    function buildStatusSummary(open, closed, cancelled, total, label) {
        if (total === 0) return 'No ' + label + ' records yet.';
        var buckets = [
            { name: 'open/ongoing', count: open },
            { name: 'closed', count: closed },
            { name: 'cancelled/invalid', count: cancelled }
        ];
        buckets.sort(function (a, b) { return b.count - a.count; });
        var top = buckets[0];
        var pct = Math.round((top.count / total) * 100);
        return 'Most ' + label + ' records (' + top.count + ' of ' + total + ', ' + pct + '%) are ' + top.name + '.';
    }

    // ---------- Simple linear-regression forecast (least squares) ----------
    var FORECAST_PERIODS = 2;

    function linearForecast(values, periodsAhead) {
        var n = values.length;
        if (n < 2) return new Array(periodsAhead).fill(0);
        var sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (var i = 0; i < n; i++) {
            sumX += i; sumY += values[i]; sumXY += i * values[i]; sumXX += i * i;
        }
        var denom = (n * sumXX - sumX * sumX) || 1;
        var slope = (n * sumXY - sumX * sumY) / denom;
        var intercept = (sumY - slope * sumX) / n;
        var out = [];
        for (var p = 1; p <= periodsAhead; p++) {
            out.push(Math.max(0, Math.round(slope * (n - 1 + p) + intercept)));
        }
        return out;
    }

    function monthLabelAfter(yearMonth, offset) {
        var parts = yearMonth.split('-');
        var d = new Date(parseInt(parts[0], 10), parseInt(parts[1], 10) - 1, 1);
        d.setMonth(d.getMonth() + offset);
        return d.toLocaleString('en-US', { month: 'short' }) + ' ' + d.getFullYear();
    }

    function monthOverMonthDelta(values) {
        if (values.length < 2) return null;
        var last = values[values.length - 1];
        var prev = values[values.length - 2];
        if (prev === 0) return last === 0 ? 0 : null;
        return Math.round(((last - prev) / prev) * 100);
    }

    function renderDeltaBadge(elementId, values) {
        var el = document.getElementById(elementId);
        if (!el) return;
        var delta = monthOverMonthDelta(values);
        if (delta === null) { el.innerHTML = ''; return; }
        var isUp = delta > 0;
        var icon = delta === 0 ? 'bi-dash' : (isUp ? 'bi-arrow-up-short' : 'bi-arrow-down-short');
        var cls = delta === 0 ? 'text-secondary' : (isUp ? 'text-danger' : 'text-success');
        el.innerHTML = '<span class="' + cls + ' small fw-semibold"><i class="bi ' + icon + '"></i> '
            + Math.abs(delta) + '% vs last month</span>';
    }

    function buildTrendSummary(valueSlice, forecastValues, label) {
        if (!valueSlice.length) return '';
        var first = valueSlice[0];
        var last = valueSlice[valueSlice.length - 1];
        var direction = last > first ? 'risen' : (last < first ? 'fallen' : 'stayed flat');
        var nextProj = forecastValues[forecastValues.length - 1];
        var projDirection = nextProj > last ? 'keep rising' : (nextProj < last ? 'ease off' : 'stay level');
        return label + ' have ' + direction + ' over the shown period and are projected (dashed) to ' + projDirection + ' over the next ' + forecastValues.length + ' months.';
    }

    // Slices to the selected range, appends a projected segment to the chart
    // and mirrors both (actual + projected) into the accompanying table.
    function renderTrend(opts) {
        var n = Math.min(opts.months, opts.yearMonths.length);
        var ymSlice = opts.yearMonths.slice(-n);
        var labelSlice = opts.labels.slice(-n);
        var valueSlice = opts.values.slice(-n);

        var forecastValues = linearForecast(valueSlice, FORECAST_PERIODS);
        var forecastLabels = [];
        for (var p = 1; p <= FORECAST_PERIODS; p++) {
            forecastLabels.push(monthLabelAfter(ymSlice[ymSlice.length - 1], p));
        }

        var allLabels = labelSlice.concat(forecastLabels);
        var actualData = valueSlice.concat(new Array(FORECAST_PERIODS).fill(null));
        var forecastData = new Array(valueSlice.length - 1).fill(null)
            .concat([valueSlice[valueSlice.length - 1]])
            .concat(forecastValues);

        if (opts.chart) {
            opts.chart.data.labels = allLabels;
            opts.chart.data.datasets[0].data = actualData;
            opts.chart.data.datasets[0].label = opts.seriesLabel;
            if (!opts.chart.data.datasets[1]) {
                opts.chart.data.datasets[1] = {
                    fill: false, tension: .35, pointRadius: 3, pointStyle: 'rectRot',
                    borderDash: [6, 4], backgroundColor: 'transparent'
                };
            }
            opts.chart.data.datasets[1].label = 'Projected';
            opts.chart.data.datasets[1].data = forecastData;
            opts.chart.data.datasets[1].borderColor = opts.color;
            opts.chart.update();
        }

        var head = document.getElementById(opts.tableHeadId);
        var body = document.getElementById(opts.tableBodyId);
        if (head && body) {
            head.innerHTML = '<tr>' + allLabels.map(function (l, i) {
                var proj = i >= labelSlice.length;
                return '<th' + (proj ? ' class="text-muted fst-italic"' : '') + '>' + l + (proj ? ' (proj.)' : '') + '</th>';
            }).join('') + '</tr>';

            var rowValues = valueSlice.concat(forecastValues);
            body.innerHTML = '<tr>' + rowValues.map(function (v, i) {
                var proj = i >= valueSlice.length;
                return '<td' + (proj ? ' class="text-muted fst-italic"' : '') + '>' + v + '</td>';
            }).join('') + '</tr>';
        }

        if (opts.summaryId) {
            setSummary(opts.summaryId, buildTrendSummary(valueSlice, forecastValues, opts.summaryLabel));
        }
    }

    function renderCarTrend() {
        renderTrend({
            chart: carTrendChart,
            tableHeadId: 'carTrendHead',
            tableBodyId: 'carTrendBody',
            yearMonths: trendYearMonths,
            labels: trendLabels,
            values: carTrend,
            seriesLabel: 'CAR Filed',
            color: '#7d1f27',
            months: currentRangeMonths,
            summaryId: 'carTrendSummary',
            summaryLabel: 'CAR filings'
        });
        renderDeltaBadge('carTotalDelta', carTrend);
    }

    function renderComplaintTrend() {
        var d = scopeData[currentComplaintScope];
        renderTrend({
            chart: complaintTrendChart,
            tableHeadId: 'complaintTrendHead',
            tableBodyId: 'complaintTrendBody',
            yearMonths: trendYearMonths,
            labels: trendLabels,
            values: d.trend,
            seriesLabel: d.trendLabel,
            color: '#c07a00',
            months: currentRangeMonths,
            summaryId: 'complaintTrendSummary',
            summaryLabel: d.summaryLabel + ' filings'
        });
        renderDeltaBadge('complaintTotalDelta', d.trend);
    }

    function renderComplaintStatusSummary() {
        var d = scopeData[currentComplaintScope];
        setSummary('complaintStatusSummary', buildStatusSummary(d.open, d.closed, d.cancelled, d.total, d.summaryLabel));
    }

    var carStatusChart = safeCreateChart('carStatusChart', {
        type: 'doughnut',
        data: {
            labels: ['Awaiting Response', 'Awaiting Verification', 'Closed', 'Cancelled'],
            datasets: [{
                data: [carStats.open, carStats.verify, carStats.closed, carStats.cancelled],
                backgroundColor: ['#c07a00', '#2f6690', '#2f6b46', '#8f7d7b'],
                borderWidth: 0
            }]
        },
        options: {
            maintainAspectRatio: false,
            onClick: function () { window.open(window.DASHBOARD_DATA.capaListUrl, '_blank'); },
            plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, padding: 12, font: { size: 11 } } } }
        }
    });

    var carTrendChart = safeCreateChart('carTrendChart', {
        type: 'line',
        data: { labels: [], datasets: [{ data: [], borderColor: '#7d1f27', backgroundColor: 'rgba(125,31,39,.12)', fill: true, tension: .35, pointRadius: 3 }] },
        options: {
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
        }
    });

    var carClassificationChart = safeCreateChart('carClassificationChart', {
        type: 'bar',
        data: {
            labels: carClassificationLabels,
            datasets: [{
                label: 'CAR Reports',
                data: carClassificationCounts,
                backgroundColor: '#7d1f27',
                borderRadius: 6,
                maxBarThickness: 42
            }]
        },
        options: {
            indexAxis: 'y',
            maintainAspectRatio: false,
            onClick: function () { window.open(window.DASHBOARD_DATA.capaListUrl, '_blank'); },
            plugins: { legend: { display: false } },
            scales: { x: { beginAtZero: true, ticks: { precision: 0 } } }
        }
    });

    var complaintStatusChart = safeCreateChart('complaintStatusChart', {
        type: 'doughnut',
        data: {
            labels: ['Open / Ongoing', 'Closed', 'Cancelled / Invalid'],
            datasets: [{
                data: [scopeData.all.open, scopeData.all.closed, scopeData.all.cancelled],
                backgroundColor: ['#c07a00', '#2f6b46', '#8f7d7b'],
                borderWidth: 0
            }]
        },
        options: {
            maintainAspectRatio: false,
            onClick: function () { window.open(scopeData[currentComplaintScope].listUrl, '_blank'); },
            plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, padding: 12, font: { size: 11 } } } }
        }
    });

    var complaintTrendChart = safeCreateChart('complaintTrendChart', {
        type: 'line',
        data: { labels: [], datasets: [{ data: [], borderColor: '#7d1f27', backgroundColor: 'rgba(125,31,39,.12)', fill: true, tension: .35, pointRadius: 3 }] },
        options: {
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
        }
    });

    var pcrVsNcrChart = safeCreateChart('pcrVsNcrChart', {
        type: 'bar',
        data: {
            labels: ['Open / Ongoing', 'Closed', 'Cancelled / Invalid'],
            datasets: [
                { label: 'PCR', data: [pcrStats.open, pcrStats.closed, pcrStats.cancelled], backgroundColor: '#7d1f27', borderRadius: 6, maxBarThickness: 46 },
                { label: 'NCR', data: [ncrStats.open, ncrStats.closed, ncrStats.cancelled], backgroundColor: '#c07a00', borderRadius: 6, maxBarThickness: 46 }
            ]
        },
        options: {
            maintainAspectRatio: false,
            onClick: function (evt, elements) {
                if (!elements || !elements.length) return;
                var url = elements[0].datasetIndex === 0 ? scopeData.pcr.listUrl : scopeData.ncr.listUrl;
                window.open(url, '_blank');
            },
            plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, padding: 12, font: { size: 11 } } } },
            scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
        }
    });

    var charts = {
        car: [carStatusChart, carTrendChart, carClassificationChart].filter(Boolean),
        complaint: [complaintStatusChart, complaintTrendChart, pcrVsNcrChart].filter(Boolean)
    };

    renderCarTrend();
    renderComplaintTrend();
    renderComplaintStatusSummary();

    document.getElementById('dashFilterSelect').addEventListener('change', function () {
        var value = this.value;
        var carSection = document.getElementById('carSection');
        var complaintSection = document.getElementById('complaintSection');

        var showCar = value === 'all' || value === 'car';
        var showComplaint = value === 'all' || value === 'complaint';

        carSection.style.display = showCar ? '' : 'none';
        complaintSection.style.display = showComplaint ? '' : 'none';

        if (showCar) charts.car.forEach(function (c) { c.resize(); });
        if (showComplaint) charts.complaint.forEach(function (c) { c.resize(); });
    });

    document.getElementById('dashTrendRangeSelect').addEventListener('change', function () {
        currentRangeMonths = parseInt(this.value, 10);
        renderCarTrend();
        renderComplaintTrend();
    });

    document.getElementById('complaintScopeSelect').addEventListener('change', function () {
        currentComplaintScope = this.value;
        var d = scopeData[currentComplaintScope];
        if (!d) return;

        var setText = function (id, val) {
            var el = document.getElementById(id);
            if (el) el.textContent = val;
        };

        setText('complaintTotalValue', d.total);
        setText('complaintOpenValue', d.open);
        setText('complaintClosedValue', d.closed);
        setText('complaintCancelledValue', d.cancelled);
        setText('complaintStatusOpenCell', d.open);
        setText('complaintStatusClosedCell', d.closed);
        setText('complaintStatusCancelledCell', d.cancelled);

        var titleEl = document.getElementById('complaintTrendTitle');
        if (titleEl) titleEl.textContent = d.trendTitle;

        var totalLink = document.getElementById('complaintTotalLink');
        if (totalLink) totalLink.setAttribute('href', d.listUrl);

        if (complaintStatusChart) {
            complaintStatusChart.data.datasets[0].data = [d.open, d.closed, d.cancelled];
            complaintStatusChart.update();
        }

        renderComplaintTrend();
        renderComplaintStatusSummary();

        var comparisonCard = document.getElementById('pcrVsNcrCard');
        if (comparisonCard) comparisonCard.style.display = (currentComplaintScope === 'all') ? '' : 'none';
    });

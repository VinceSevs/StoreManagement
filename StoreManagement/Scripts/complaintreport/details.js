    var pcrPrintTextNodes = [];

    function insertPrintTextBlocks() {
        pcrPrintTextNodes = [];
        document.querySelectorAll('textarea.pcr-findings-readonly').forEach(function (ta) {
            var div = document.createElement('div');
            div.className = 'pcr-print-text';
            div.textContent = ta.value;
            ta.insertAdjacentElement('afterend', div);
            pcrPrintTextNodes.push(div);
        });
    }

    function removePrintTextBlocks() {
        pcrPrintTextNodes.forEach(function (div) { div.remove(); });
        pcrPrintTextNodes = [];
    }

    function printPcrDetails() {
        var originalTitle = document.title;

        function cleanup() {
            document.title = originalTitle;
            removePrintTextBlocks();
            window.removeEventListener('afterprint', cleanup);
        }

        insertPrintTextBlocks();
        document.title = '​';
        window.addEventListener('afterprint', cleanup);
        window.print();
    }

    $(function () {
        $('#saveQAFindings').on('click', function () {
            var qaFindings = $('#QAFindings').val().trim();
            var docId = $('#DocID').val();
            var qaRemarks = $('#QARemarks').val();

            if (qaFindings === '') {
                alert('Please enter your findings before saving.');
                return;
            }

            if (!confirm('Submit these QA Remarks/Findings?')) return;

            $('#saveQAFindings').prop('disabled', true);

            $.ajax({
                url: window.COMPLAINT_REPORT_DETAILS.respondUrl,
                type: 'POST',
                data: { docId: docId, qaRemarks: qaRemarks, qaFindings: qaFindings },
                success: function (response) {
                    if (response.success) {
                        alert(response.message);
                        location.reload();
                    } else {
                        alert(response.message || 'Something went wrong.');
                        $('#saveQAFindings').prop('disabled', false);
                    }
                },
                error: function () {
                    alert('Server error. Please try again later.');
                    $('#saveQAFindings').prop('disabled', false);
                }
            });
        });
    });

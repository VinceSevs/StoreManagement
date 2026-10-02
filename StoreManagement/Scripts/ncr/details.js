    var ncrPrintTextNodes = [];

    function insertNcrPrintTextBlocks() {
        ncrPrintTextNodes = [];
        document.querySelectorAll('textarea.ncr-findings-readonly').forEach(function (ta) {
            var div = document.createElement('div');
            div.className = 'ncr-print-text';
            div.textContent = ta.value;
            ta.insertAdjacentElement('afterend', div);
            ncrPrintTextNodes.push(div);
        });
    }

    function removeNcrPrintTextBlocks() {
        ncrPrintTextNodes.forEach(function (div) { div.remove(); });
        ncrPrintTextNodes = [];
    }

    function printNcrDetails() {
        var originalTitle = document.title;

        function cleanup() {
            document.title = originalTitle;
            removeNcrPrintTextBlocks();
            window.removeEventListener('afterprint', cleanup);
        }

        insertNcrPrintTextBlocks();
        document.title = '​';
        window.addEventListener('afterprint', cleanup);
        window.print();
    }

    $(function () {
        $('#saveCSFindings').on('click', function () {
            var csFindings = $('#CSFindings').val().trim();
            var docId = $('#DocID').val();
            var csRemarks = $('#CSRemarks').val();

            if (csFindings === '') {
                alert('Please enter your findings before saving.');
                return;
            }

            if (!confirm('Submit these Customer Care Remarks/Findings?')) return;

            $('#saveCSFindings').prop('disabled', true);

            $.ajax({
                url: window.NCR_DETAILS.respondUrl,
                type: 'POST',
                data: { docId: docId, csRemarks: csRemarks, csFindings: csFindings },
                success: function (response) {
                    if (response.success) {
                        alert(response.message);
                        location.reload();
                    } else {
                        alert(response.message || 'Something went wrong.');
                        $('#saveCSFindings').prop('disabled', false);
                    }
                },
                error: function () {
                    alert('Server error. Please try again later.');
                    $('#saveCSFindings').prop('disabled', false);
                }
            });
        });
    });

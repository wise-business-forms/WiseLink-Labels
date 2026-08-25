/*
 * Quote line items grid.
 *
 * Behaviour deliberately differs from the charge fields this replaced:
 *   - the section is never hidden, so every charge stays reachable regardless of
 *     printing method (press proof used to be unreachable on black-only-digital);
 *   - a printing change never clears what the user typed. Rules may re-tick rows the
 *     user has not touched, and nothing more.
 *
 * The keepInSyncWithForm() serializer is the only writer of #line-items-json, which is
 * what the server binds. Selection and quantity are the only values the server trusts;
 * descriptions, prices and price bases are re-resolved from CERM on the way in.
 */
(function () {
    'use strict';

    // Mirrors WiseLabels.Models.LineItemPricing.Total. Keep the two in step.
    var BASIS = {
        TEXT: '0',
        FIXED: '2',
        PER_PIECE: '5',
        PER_100: '6',
        PER_1000: '7',
        PER_100000: '8'
    };

    function total(basis, unitPrice, quantity) {
        switch (String(basis || '').trim()) {
            case BASIS.TEXT: return 0;
            case BASIS.FIXED: return unitPrice;
            case BASIS.PER_PIECE: return unitPrice * quantity;
            case BASIS.PER_100: return unitPrice * quantity / 100;
            case BASIS.PER_1000: return unitPrice * quantity / 1000;
            case BASIS.PER_100000: return unitPrice * quantity / 100000;
            default: return 0;
        }
    }

    function money(value) {
        return value.toLocaleString('en-US', { style: 'currency', currency: 'USD' });
    }

    function rows() {
        return Array.prototype.slice.call(document.querySelectorAll('.line-item-row'));
    }

    function readRow(row) {
        var checkbox = row.querySelector('.line-item-selected');
        var qtyInput = row.querySelector('.line-item-qty');
        var quantity = parseFloat(qtyInput && qtyInput.value);
        if (!isFinite(quantity) || quantity < 0) quantity = 0;
        return {
            itemRef: row.dataset.itemRef || '',
            key: row.dataset.key || '',
            priceBasis: row.dataset.priceBasis || BASIS.PER_PIECE,
            unitPrice: parseFloat(row.dataset.unitPrice) || 0,
            forced: row.dataset.forced === 'true',
            selected: row.dataset.forced === 'true' || !!(checkbox && checkbox.checked),
            quantity: quantity
        };
    }

    function repaint() {
        var grand = 0;
        rows().forEach(function (row) {
            var data = readRow(row);
            var lineTotal = total(data.priceBasis, data.unitPrice, data.quantity);
            var cell = row.querySelector('.line-item-total');
            if (cell) cell.textContent = money(lineTotal);
            row.classList.toggle('table-active', data.selected);
            if (data.selected) grand += lineTotal;
        });
        var totalCell = document.getElementById('line-items-total');
        if (totalCell) totalCell.textContent = money(grand);
        serialize();
    }

    function serialize() {
        var field = document.getElementById('line-items-json');
        if (!field) return;
        field.value = JSON.stringify(rows().map(function (row) {
            var data = readRow(row);
            // Only what the server trusts. Prices are re-resolved server-side.
            return {
                itemRef: data.itemRef,
                key: data.key,
                selected: data.selected,
                quantity: data.quantity
            };
        }));
    }

    /**
     * Re-evaluates which rows should be pre-ticked after a spec change, without ever
     * discarding a row the user has touched.
     */
    function applyRules(context) {
        rows().forEach(function (row) {
            if (row.dataset.userTouched === 'true') return;
            var checkbox = row.querySelector('.line-item-selected');
            if (!checkbox || checkbox.disabled) return;

            var autoSelect = row.dataset.autoSelectPrintingIds;
            if (!autoSelect) return;

            var ids = autoSelect.split(',').map(function (s) { return s.trim().toLowerCase(); });
            var printingId = String((context && context.printingId) || '').trim().toLowerCase();
            checkbox.checked = printingId !== '' && ids.indexOf(printingId) !== -1;
        });
        repaint();
    }

    /**
     * The die charge is mandatory whenever no existing die size was chosen. This is the
     * billing trigger; it replaces the dimension-matching heuristic, which never fired
     * on the circle/diameter path and so never charged for circular custom dies.
     */
    function applyDieRule(hasExistingDie) {
        rows().forEach(function (row) {
            if (row.dataset.key !== 'CustomDie') return;
            var checkbox = row.querySelector('.line-item-selected');
            if (!checkbox) return;
            var forced = !hasExistingDie;
            row.dataset.forced = forced ? 'true' : 'false';
            checkbox.disabled = forced;
            if (forced) checkbox.checked = true;
        });
        repaint();
    }

    function init() {
        if (!document.getElementById('line-items-section')) return;

        rows().forEach(function (row) {
            var checkbox = row.querySelector('.line-item-selected');
            var qtyInput = row.querySelector('.line-item-qty');

            if (checkbox) {
                checkbox.addEventListener('change', function () {
                    row.dataset.userTouched = 'true';
                    repaint();
                });
            }
            if (qtyInput) {
                ['input', 'change'].forEach(function (evt) {
                    qtyInput.addEventListener(evt, function () {
                        row.dataset.userTouched = 'true';
                        // Ticking follows a quantity the user actually entered.
                        if (checkbox && !checkbox.disabled) {
                            var qty = parseFloat(qtyInput.value);
                            if (isFinite(qty) && qty > 0) checkbox.checked = true;
                        }
                        repaint();
                    });
                });
            }
        });

        // Rules key on the printing ID, never the label text. The quote form dispatches
        // this on window with detail { id, label, key, slug }.
        window.addEventListener('printingSelectionChanged', function (event) {
            applyRules({ printingId: event.detail && event.detail.id });
        });

        var existingDie = document.getElementById('existing-die-select');
        if (existingDie) {
            existingDie.addEventListener('change', function () {
                applyDieRule(!!existingDie.value);
            });
            applyDieRule(!!existingDie.value);
        }

        // Serialize before submit so a quote posted without any interaction still carries
        // its pre-ticked rows.
        var form = document.getElementById('quote-form');
        if (form) form.addEventListener('submit', serialize);

        repaint();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.WiseLineItems = { repaint: repaint, applyRules: applyRules, applyDieRule: applyDieRule, total: total };
})();

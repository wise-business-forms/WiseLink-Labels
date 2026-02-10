// Customer autocomplete logic extracted from quote.js
// This file should be included after the main DOMContentLoaded handler in quote.js

(function() {
    // Assumes referenceValueInput, referenceTypeSelect, updateSummaryPanel are available in the global scope
    // and that the DOM is ready
    if (!window.referenceValueInput || !window.referenceTypeSelect) return;

    // Create customer suggestions dropdown
    const inputParent = referenceValueInput.parentElement;
    const wrapper = document.createElement('div');
    wrapper.id = 'reference-value-wrapper';
    inputParent.insertBefore(wrapper, referenceValueInput);
    wrapper.appendChild(referenceValueInput);

    const customerSuggestionsContainer = document.createElement('div');
    customerSuggestionsContainer.id = 'customer-suggestions';
    customerSuggestionsContainer.className = 'customer-suggestions';
    customerSuggestionsContainer.style.display = 'none';
    wrapper.appendChild(customerSuggestionsContainer);

    let allCustomers = null;

    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"]'/g, m => map[m]);
    }

    function fetchAllCustomers() {
        fetch('/Api/Customers')
            .then(response => response.json())
            .then(customers => {
                if (!Array.isArray(customers) || customers.length === 0) {
                    allCustomers = [];
                    return;
                }
                allCustomers = customers;
            })
            .catch(error => {
                console.error('Error fetching all customers:', error);
                allCustomers = [];
            });
    }

    fetchAllCustomers();

    function filterCustomersForDropdown() {
        const referenceType = referenceTypeSelect.value;
        const searchText = referenceValueInput.value.trim().toLowerCase();

        if (referenceType !== 'company-name' || searchText.length === 0) {
            customerSuggestionsContainer.style.display = 'none';
            customerSuggestionsContainer.innerHTML = '';
            return;
        }

        if (!Array.isArray(allCustomers) || allCustomers.length === 0) {
            customerSuggestionsContainer.style.display = 'block';
            customerSuggestionsContainer.innerHTML = '<div class="suggestion-item no-results">No customers found</div>';
            return;
        }

        const filtered = allCustomers.filter(customer =>
            customer.name && customer.name.toLowerCase().includes(searchText)
        ).sort((a, b) => a.name.localeCompare(b.name));

        let suggestionsHtml = '';
        const suggestionsArr = [];
        filtered.slice(0, 10).forEach(customer => {
            var displayName = '';
            if (customer.name && customer.name.trim()) {
                displayName = customer.name.trim();
            } else if (customer.id) {
                displayName = customer.id;
            }
            suggestionsArr.push(displayName);
            suggestionsHtml += `<div class="suggestion-item" data-customer-name="${escapeHtml(displayName)}">${escapeHtml(displayName)}</div>`;
        });

        // Output suggestions to console for debugging
        console.log('Company Name dropdown suggestions:', suggestionsArr);

        customerSuggestionsContainer.innerHTML = suggestionsHtml;
        customerSuggestionsContainer.style.display = filtered.length > 0 ? 'block' : 'none';

        customerSuggestionsContainer.querySelectorAll('.suggestion-item').forEach(item => {
            item.addEventListener('click', function() {
                referenceValueInput.value = this.dataset.customerName;
                customerSuggestionsContainer.style.display = 'none';
                if (typeof updateSummaryPanel === 'function') updateSummaryPanel();
            });
        });
    }

    referenceValueInput.addEventListener('input', function() {
        filterCustomersForDropdown();
        if (typeof updateSummaryPanel === 'function') updateSummaryPanel();
    });

    referenceValueInput.addEventListener('blur', function() {
        setTimeout(() => {
            customerSuggestionsContainer.style.display = 'none';
        }, 200);
    });

    referenceTypeSelect.addEventListener('change', function() {
        customerSuggestionsContainer.style.display = 'none';
        customerSuggestionsContainer.innerHTML = '';
        if (typeof updateSummaryPanel === 'function') updateSummaryPanel();
    });

    customerSuggestionsContainer.addEventListener('mousedown', (e) => {
        e.preventDefault();
    });
})();

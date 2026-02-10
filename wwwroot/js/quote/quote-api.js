// quote-api.js
// API/data-fetching functions for quote form

class QuoteApi {
    static async loadMaterials(materialLoading, materialError, materialSelect, populateMaterials) {
        try {
            materialLoading.style.display = 'block';
            materialError.style.display = 'none';
            materialSelect.style.opacity = '0.5';
            materialSelect.disabled = false;
            const url = '/Api/Materials';
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load materials: ${response.status}`);
            }
            const data = await response.json();
            const materialsData = data.materials || data.data || data.items || data.results || data;
            populateMaterials(materialsData);
        } catch (error) {
            materialError.textContent = 'Error loading materials. ' + (error.message || 'Please refresh the page to retry.');
            materialError.style.display = 'block';
            materialSelect.innerHTML = '<option value="">Error loading materials. See error message below.</option>';
        } finally {
            materialLoading.style.display = 'none';
            materialSelect.style.opacity = '1';
            materialSelect.disabled = false;
        }
    }

    static async loadPrintingOptions(printingFilter, printingLoading, printingError, populatePrintingOptions) {
        try {
            printingLoading.style.display = 'block';
            printingError.style.display = 'none';
            printingFilter.style.opacity = '0.5';
            printingFilter.style.pointerEvents = 'none';
            const response = await fetch('/Api/ColorCodes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load printing options: ${response.status}`);
            }
            const data = await response.json();
            populatePrintingOptions(data);
        } catch (error) {
            printingError.textContent = 'Error loading printing options. ' + (error.message || 'Please refresh the page to retry.');
            printingError.style.display = 'block';
            printingFilter.innerHTML = '<div class="text-muted">Error loading printing options. Please refresh the page to retry.</div>';
        } finally {
            printingLoading.style.display = 'none';
            printingFilter.style.opacity = '1';
            printingFilter.style.pointerEvents = 'auto';
        }
    }

    static async loadCuttingDieOptions(printing, cuttingDieSelect, cuttingDieLoading, cuttingDieError) {
        try {
            cuttingDieLoading.style.display = 'block';
            cuttingDieError.style.display = 'none';
            cuttingDieSelect.style.opacity = '0.5';
            cuttingDieSelect.disabled = true;
            cuttingDieSelect.innerHTML = '<option value="">Loading cutting die options...</option>';
            const url = `/Api/CuttingDie?printing=${encodeURIComponent(printing)}`;
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load cutting die options: ${response.status}`);
            }
            const data = await response.json();
            const cuttingDieOptions = data.cuttingDieOptions || data.data || data.items || data.results || [];
            cuttingDieSelect.innerHTML = '';
            if (cuttingDieOptions.length === 0) {
                cuttingDieSelect.innerHTML = '<option value="">No cutting die options available</option>';
            } else {
                cuttingDieOptions.forEach(option => {
                    const optionElement = document.createElement('option');
                    const optionValue = option.stns_ref || option.stnsRef || '';
                    optionElement.value = optionValue;
                    optionElement.textContent = option.stnsOms || option.StnsOms || option.stns_oms || 'Unknown';
                    const cleanedRef = optionElement.textContent.replace(/^QQ-R-/, '');
                    if (optionValue) {
                        optionElement.dataset.rawRef = cleanedRef;
                        optionElement.text = cleanedRef;
                    }
                    cuttingDieSelect.appendChild(optionElement);
                });
            }
        } catch (error) {
            cuttingDieError.textContent = 'Error loading cutting die options. ' + (error.message || 'Please try again.');
            cuttingDieError.style.display = 'block';
            cuttingDieSelect.innerHTML = '<option value="">Error loading cutting die options</option>';
        } finally {
            cuttingDieLoading.style.display = 'none';
            cuttingDieSelect.style.opacity = '1';
            cuttingDieSelect.disabled = false;
        }
    }

    // Add more API functions as needed (e.g., finishing options)
}

window.QuoteApi = QuoteApi;
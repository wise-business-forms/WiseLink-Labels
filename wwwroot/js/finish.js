// Load finishing options from server-side proxy endpoint and populate #finish select
// This function is called from quote.js when a printing option is selected
// printingText: The text of the selected printing option (used for filtering)
async function loadFinishingOptions(printingText) {
    const finishSelect = document.getElementById('finish');
    const finishLoading = document.getElementById('finish-loading');
    const finishError = document.getElementById('finish-error');

    if (!finishSelect) {
        console.debug('Finish select (#finish) not found - skipping finishing types load.');
        return;
    }

    if (finishLoading) finishLoading.style.display = 'block';
    if (finishError) finishError.style.display = 'none';
    finishSelect.style.opacity = '0.5';
    finishSelect.disabled = true;
    finishSelect.innerHTML = '<option value="">Loading finishes...</option>';

    try {
        const resp = await fetch('/Api/FinishingTypes');
        if (!resp.ok) {
            console.warn('Failed to load finishing types:', resp.status, resp.statusText);
            if (finishError) {
                finishError.textContent = 'Unable to load finishes';
                finishError.style.display = 'block';
            }
            finishSelect.innerHTML = '<option value="">Error loading finishes</option>';
            return;
        }

        const data = await resp.json();
        console.debug('Finishing types data received:', data);

        // Clear existing options
        finishSelect.innerHTML = '';

        // Explicitly support the server returning { materials: [...] }
        let items = [];
        if (Array.isArray(data)) {
            items = data;
        } else if (data && Array.isArray(data.materials)) {
            items = data.materials;
        } else {
            // Fallback: find the first array value in the returned object
            const possibleArray = Object.values(data).find(v => Array.isArray(v));
            if (Array.isArray(possibleArray)) {
                items = possibleArray;
            }
        }

        if (!Array.isArray(items) || items.length === 0) {
            console.warn('Finishing types response has no array payload:', data);
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = 'No finishes available';
            finishSelect.appendChild(placeholder);
            return;
        }

        // Add a blank placeholder option
        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = 'Select finish';
        finishSelect.appendChild(placeholder);

        // Filter based on what was shown on the Printing button:
        // - If Printing contains "Digital": hide finishes that contain "Flexo"
        // - If Printing contains "Flexo": hide finishes that contain "Digital"
        const printingLower = (printingText || '').toLowerCase();
        const printingMode =
            printingLower.includes('digital') ? 'digital' :
            printingLower.includes('flexo') ? 'flexo' :
            null;

        const getFinishDisplayTextLower = (item) => {
            // Prefer en-US description (Descriptions[]), fallback to other text fields
            let text = '';
            if (Array.isArray(item?.Descriptions)) {
                const enDesc = item.Descriptions.find(d => (d?.ISOLanguageCode || '').toLowerCase() === 'en-us' || (d?.ISOLanguageCode || '').toLowerCase() === 'en');
                if (enDesc && (enDesc.Description || enDesc.description)) {
                    text = enDesc.Description || enDesc.description;
                }
            }
            if (!text) {
                text = item?.Description ?? item?.description ?? item?.Name ?? item?.name ?? '';
            }
            return (text || '').toLowerCase();
        };

        let itemsToRender = items;
        if (printingMode === 'digital') {
            itemsToRender = items.filter(i => !getFinishDisplayTextLower(i).includes('flexo'));
        } else if (printingMode === 'flexo') {
            itemsToRender = items.filter(i => !getFinishDisplayTextLower(i).includes('digital'));
        }

        itemsToRender.forEach(item => {
            const opt = document.createElement('option');

            // Prefer the Descriptions array entry where ISOLanguageCode === 'en-US'
            let text = '';
            if (Array.isArray(item?.Descriptions)) {
                const enDesc = item.Descriptions.find(d => (d?.ISOLanguageCode || '').toLowerCase() === 'en-us' || (d?.ISOLanguageCode || '').toLowerCase() === 'en');
                if (enDesc && (enDesc.Description || enDesc.description)) {
                    text = enDesc.Description || enDesc.description;
                }
            }

            // Fallback heuristics for value and text
            const value = item?.Id ?? item?.id ?? item?.Value ?? item?.value ?? item?.name ?? item?.Name ?? text ?? JSON.stringify(item);
            if (!text) text = item?.Description ?? item?.description ?? item?.Name ?? item?.name ?? value;

            opt.value = value;
            opt.textContent = text;

            // Preserve any raw reference if present
            if (item?.RawReference) opt.dataset.rawRef = item.RawReference;
            if (item?.rawRef) opt.dataset.rawRef = item.rawRef;

            finishSelect.appendChild(opt);
        });

        if (printingMode !== null && itemsToRender.length === 0) {
            const none = document.createElement('option');
            none.value = '';
            none.textContent = 'No finishes available for this printing option';
            finishSelect.appendChild(none);
        }

        // If a previously selected value exists, attempt to restore it
        const saved = finishSelect.getAttribute('data-selected');
        if (saved) {
            const savedOption = Array.from(finishSelect.options).find(opt => opt.value === saved);
            if (savedOption) {
                finishSelect.value = saved;
            }
        }
    } catch (err) {
        console.error('Error loading finishing types:', err);
        if (finishError) {
            finishError.textContent = 'Error loading finishes';
            finishError.style.display = 'block';
        }
        finishSelect.innerHTML = '<option value="">Error loading finishes</option>';
    } finally {
        if (finishLoading) finishLoading.style.display = 'none';
        finishSelect.style.opacity = '1';
        finishSelect.disabled = false;
    }
}
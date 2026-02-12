document.addEventListener('DOMContentLoaded', function() {
        // Stub for populateMaterials to prevent ReferenceError
        function populateMaterials(data) {
            // TODO: Implement material dropdown population logic here
            console.log('populateMaterials called with:', data);
        }
    // Ensure loadMaterials is defined before use, at correct scope
    function loadMaterials() {
        QuoteApi.loadMaterials(materialLoading, materialError, materialSelect, populateMaterials);
    }
    // Note: shapeInputs are now loaded dynamically, so we don't query them here
    const widthInput = document.getElementById('label-width');
    const heightInput = document.getElementById('label-height');
    const dieSizeModeSelect = document.getElementById('die-size-mode');
    const dieSizeModeContainer = document.getElementById('die-size-mode-container');
    const customLabelSizeInputs = document.getElementById('custom-label-size-inputs');
    const diameterInput = document.getElementById('diameter');
    const sizeValidation = document.getElementById('size-validation');
    const diameterValidation = document.getElementById('diameter-validation');
    const cornersSection = document.getElementById('corners-section');
    const labelSizeSection = document.getElementById('label-size-section');
    const diameterSection = document.getElementById('diameter-section');
    const materialSelect = document.getElementById('material-select');
    const materialLoading = document.getElementById('material-loading');
    const materialError = document.getElementById('material-error');
    const printingFilter = document.getElementById('printing-filter');
    const printingLoading = document.getElementById('printing-loading');
    const printingError = document.getElementById('printing-error');
    const printingInput = document.getElementById('printing-input');
    const printingValueInput = document.getElementById('printing-value');

    if (printingFilter && printingInput && printingValueInput) {
        const syncPrintingFields = (button) => {
            if (!button) {
                return;
            }
            const label = button.getAttribute('data-printing-text') || button.textContent.trim();
            const id = button.getAttribute('data-printing-id') || button.getAttribute('data-printing-key') || button.getAttribute('data-value') || '';
            printingInput.value = label;
            printingValueInput.value = id;
        };

        printingFilter.addEventListener('click', event => {
            const button = event.target.closest('button[data-printing-id], button[data-printing-key], button[data-value]');
            if (!button || !printingFilter.contains(button)) {
                return;
            }
            syncPrintingFields(button);
        });

        const initiallyActive = printingFilter.querySelector('button.active');
        if (initiallyActive) {
            syncPrintingFields(initiallyActive);
        }
    }

    // Store materials data for filtering
    let allMaterialsData = null;

    function getCornersMode() {
        const selected = document.querySelector('input[name="corners"]:checked')?.value;
        return (selected || '').toLowerCase(); // 'rounded' | 'square' | ''
    }

    function getDieSizeMode() {
        return (dieSizeModeSelect?.value || 'existing').toLowerCase();
    }

    function isLabelSizeWidthHeightVisible() {
        return customLabelSizeInputs && customLabelSizeInputs.style.display !== 'none';
    }

    function isCuttingDieRequiredForCurrentState() {
        const shape = document.querySelector('input[name="shape"]:checked')?.value;
        return isCuttingDieApplicableForShape(shape) && getDieSizeMode() === 'existing';
    }

    function toggleLabelSizeInputsForMode() {
        // Only applies when label size section is shown (non-circle/oval)
        const shape = document.querySelector('input[name="shape"]:checked')?.value;
        const isCircleOrOval = shape === 'circle' || shape === 'oval';
        if (isCircleOrOval) return;

        // Special shapes cannot use an "existing die" selection in this flow.
        if ((shape || '').toLowerCase() === 'special') {
            if (dieSizeModeSelect) dieSizeModeSelect.value = 'custom';
        }

        // If user selects Square corners (instead of Rounded), force custom size entry and
        // replace the Die Size dropdown with Width/Height inputs.
        const cornersMode = getCornersMode();
        if (cornersMode === 'square') {
            if (dieSizeModeSelect) {
                // Remember the user's prior selection so we can restore it when switching back to Rounded
                if (dieSizeModeSelect.dataset.forcedByCorners !== 'true') {
                    dieSizeModeSelect.dataset.prevMode = dieSizeModeSelect.value || 'existing';
                }
                dieSizeModeSelect.dataset.forcedByCorners = 'true';
                dieSizeModeSelect.value = 'custom';
            }
            if (dieSizeModeContainer) dieSizeModeContainer.style.display = 'none';
        } else {
            if (dieSizeModeContainer) dieSizeModeContainer.style.display = 'block';
            // If Custom was forced by Square corners, restore prior/default selection now.
            if (dieSizeModeSelect && dieSizeModeSelect.dataset.forcedByCorners === 'true') {
                const prev = dieSizeModeSelect.dataset.prevMode || 'existing';
                dieSizeModeSelect.value = prev;
                delete dieSizeModeSelect.dataset.forcedByCorners;
                delete dieSizeModeSelect.dataset.prevMode;
            }
        }

        const mode = getDieSizeMode();
        const showCustom = mode === 'custom';

        if (customLabelSizeInputs) {
            // Keep flex layout so Width [X] Height stays inline
            customLabelSizeInputs.style.display = showCustom ? 'flex' : 'none';
        }

        // Lock the manual inputs when using an existing die (they'll be populated from Cutting Die)
        if (widthInput) widthInput.disabled = !showCustom;
        if (heightInput) heightInput.disabled = !showCustom;

        // When switching to custom, clear cutting die (since it won't be used)
        if (showCustom) {
            const cuttingDieSelect = document.getElementById('cutting-die');
            if (cuttingDieSelect) {
                cuttingDieSelect.value = '';
            }
        }
    }

    function isCuttingDieApplicableForShape(shapeValue) {
        const v = (shapeValue || '').toLowerCase();
        // Business rule: If circle, oval, or special is selected, do not show Cutting Die.
        return !(v === 'circle' || v === 'oval' || v === 'special');
    }

    function toggleCuttingDieSection() {
        const cuttingDieSelect = document.getElementById('cutting-die');
        if (!cuttingDieSelect) return;

        const section = cuttingDieSelect.closest('.form-section');
        const selectedShape = document.querySelector('input[name="shape"]:checked')?.value;
        const applicable = isCuttingDieApplicableForShape(selectedShape) && getDieSizeMode() === 'existing';

        if (section) {
            section.style.display = applicable ? 'block' : 'none';
        }

        const cuttingDieError = document.getElementById('cutting-die-error');
        if (cuttingDieError) cuttingDieError.style.display = 'none';

        if (!applicable) {
            // Clear and disable when not applicable.
            cuttingDieSelect.innerHTML = getDieSizeMode() === 'custom'
                ? '<option value="">Cutting die not required for custom die size</option>'
                : '<option value="">Cutting die not applicable for selected shape</option>';
            cuttingDieSelect.value = '';
            cuttingDieSelect.disabled = true;
            return;
        }

        // If applicable, ensure the select is enabled/loaded when Printing is available.
        const selectedPrintingText = getSelectedPrinting();
        const selectedPrintingId = getSelectedPrintingId();
        if (selectedPrintingId || selectedPrintingText) {
            loadCuttingDieOptions(selectedPrintingId || selectedPrintingText);
        } else {
            cuttingDieSelect.innerHTML = '<option value="">Please select a Printing option first</option>';
            cuttingDieSelect.value = '';
            cuttingDieSelect.disabled = true;
        }
    }

    // Fetch Materials from API via server-side endpoint (no prerequisites)
    async function loadFinishing() {
        try {
            materialLoading.style.display = 'block';
            materialError.style.display = 'none';
            materialSelect.style.opacity = '0.5';
            materialSelect.disabled = true;

            const response = await fetch('/Api/Materials', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load materials: ${response.status}`);
            }

            const data = await response.json();
            populateMaterials(data);
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

    function normalizePrintingItems(payload) {
        if (!payload) return [];
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload.data)) return payload.data;
        if (Array.isArray(payload.Data)) return payload.Data;
        if (Array.isArray(payload.items)) return payload.items;
        if (Array.isArray(payload.results)) return payload.results;
        return [];
    }

    function populatePrintingOptions(colorCodes) {
        if (!printingFilter) {
            return;
        }

        const items = normalizePrintingItems(colorCodes);
        const printingMap = new Map();

        items.forEach(colorCode => {
            const colourBacking = colorCode?.ColourBacking || colorCode?.colourBacking;
            const dataSource = colourBacking || colorCode;
            let colorCodeId = null;
            if (colourBacking) {
                colorCodeId = colourBacking.Id || colourBacking.id || colourBacking.ID;
            } else {
                colorCodeId = colorCode.Id || colorCode.id || colorCode.ID
                    || colorCode.ColourCodeId || colorCode.colourCodeId
                    || colorCode.ColorCodeId || colorCode.colorCodeId;
            }
            if (!colorCodeId || printingMap.has(colorCodeId)) {
                return;
            }

            let description = 'Unknown';
            const descriptionsArray = dataSource?.Descriptions || dataSource?.Discriptions
                || dataSource?.descriptions || dataSource?.discriptions;
            if (Array.isArray(descriptionsArray) && descriptionsArray.length > 0) {
                const enUSDesc = descriptionsArray.find(d => {
                    const lang = d?.ISOLanguageCode || d?.isoLanguageCode || d?.ISOLanguagecode;
                    return lang?.toLowerCase() === 'en-us';
                });
                const useDesc = enUSDesc || descriptionsArray[0];
                description = useDesc?.Description || useDesc?.description || description;
            }

            printingMap.set(colorCodeId, description);
        });

        const sortedPrintingOptions = Array.from(printingMap.entries()).sort((a, b) => a[1].localeCompare(b[1]));

        printingFilter.innerHTML = '';

        if (sortedPrintingOptions.length === 0) {
            printingFilter.innerHTML = '<div class="text-muted">No printing options available.</div>';
            return;
        }

        const initialPrintingId = (printingFilter.dataset.initialId || '').trim();
        const initialPrintingLabel = (printingFilter.dataset.initialLabel || '').trim();

        const applyPrintingSelection = (button, triggerUpdates) => {
            printingFilter.querySelectorAll('button').forEach(b => b.classList.remove('active'));
            button.classList.add('active');

            const selectedPrintingText = button.getAttribute('data-printing-text') || button.textContent.trim();
            const selectedPrintingId = button.getAttribute('data-printing-id') || '';

            if (printingInput) printingInput.value = selectedPrintingText;
            if (printingValueInput) printingValueInput.value = selectedPrintingId;

            if (!triggerUpdates) {
                return;
            }

            const currentShape = document.querySelector('input[name="shape"]:checked')?.value;
            if (isCuttingDieApplicableForShape(currentShape) && getDieSizeMode() === 'existing') {
                if (selectedPrintingId || selectedPrintingText) {
                    loadCuttingDieOptions(selectedPrintingId || selectedPrintingText);
                } else {
                    const cuttingDieSelect = document.getElementById('cutting-die');
                    if (cuttingDieSelect) {
                        cuttingDieSelect.innerHTML = '<option value="">Please select a Printing option first</option>';
                        cuttingDieSelect.disabled = true;
                    }
                }
            }

            filterFinishingOptions(selectedPrintingText);
            updateSummaryPanel();
        };

        sortedPrintingOptions.forEach(([id, description]) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'filter-btn';
            button.textContent = description;
            button.setAttribute('data-printing-id', id);
            button.setAttribute('data-printing-text', description);

            button.addEventListener('click', e => {
                e.preventDefault();
                applyPrintingSelection(button, true);
            });

            printingFilter.appendChild(button);

            const matchesSavedId = initialPrintingId && id === initialPrintingId;
            const matchesSavedLabel = !initialPrintingId && initialPrintingLabel
                && description.localeCompare(initialPrintingLabel, undefined, { sensitivity: 'accent' }) === 0;
            if (matchesSavedId || matchesSavedLabel) {
                applyPrintingSelection(button, true);
            }
        });
    }

    // Function to filter Finishing options based on Printing selection
    function filterFinishingOptions(printingText) {
        const finishSelect = document.getElementById('finish-select');
        if (!finishSelect) return;
        
        console.log('filterFinishingOptions called with printingText:', printingText);
        
        // Check the printing button text for "Flexo" or "Digital"
        let allowedType = null;
        if (printingText && printingText.toLowerCase().includes('flexo')) {
            allowedType = 1; // Inline (Type 1) for Flexo
            console.log('Printing contains Flexo - filtering to Type 1 (Inline)');
        } else if (printingText && printingText.toLowerCase().includes('digital')) {
            allowedType = 2; // Offline (Type 2) for Digital
            console.log('Printing contains Digital - filtering to Type 2 (Offline)');
        } else {
            console.log('Printing does not contain Flexo or Digital - showing all finishing options');
        }
        
        // Get all existing options from the select element
        const allOptions = Array.from(finishSelect.options);
        
        // Store the currently selected value before clearing
        const currentValue = finishSelect.value;
        
        // Clear current options
        finishSelect.innerHTML = '';
        
        // Filter and add options based on their Type
        // Determine type by checking if option text/value contains "laminate" (Type 1) or "varnish" (Type 2)
        allOptions.forEach(option => {
            const optionText = (option.textContent || option.text || '').toLowerCase();
            const optionValue = (option.value || '').toLowerCase();
            const combinedText = optionText + ' ' + optionValue;
            
            // Determine type: laminate = Type 1 (Inline), varnish = Type 2 (Offline)
            let optionType = null;
            if (combinedText.includes('laminate')) {
                optionType = 1; // Inline
            } else if (combinedText.includes('varnish')) {
                optionType = 2; // Offline
            }
            
            // Add option if:
            // - No type filtering needed (allowedType is null), OR
            // - Option has a type and it matches the allowed type
            if (allowedType === null || (optionType !== null && optionType === allowedType)) {
                const newOption = document.createElement('option');
                newOption.value = option.value;
                newOption.textContent = option.textContent || option.text;
                finishSelect.appendChild(newOption);
            }
        });
        
        // Restore previously selected value if it's still available
        if (currentValue) {
            const optionToRestore = Array.from(finishSelect.options).find(opt => opt.value === currentValue);
            if (optionToRestore) {
                finishSelect.value = currentValue;
            } else if (finishSelect.options.length > 0) {
                // If previous selection is no longer available, select first option
                finishSelect.selectedIndex = 0;
            }
        }
        
        // Also check saved data for restoration
        const savedDataScript = document.getElementById('saved-quote-data');
        if (savedDataScript) {
            try {
                const savedData = JSON.parse(savedDataScript.textContent);
                const savedFinishValue = savedData.finishValue || savedData.finish;
                if (savedFinishValue) {
                    // Try to find by value
                    const optionToSelect = Array.from(finishSelect.options).find(opt => 
                        opt.value === savedFinishValue || opt.text === savedFinishValue
                    );
                    if (optionToSelect) {
                        finishSelect.value = optionToSelect.value;
                      }
                  }
               } catch (e) {
                console.error('Error restoring finish:', e);
              }
        }
    }

    // Function to validate email (required)
    function validateEmail() {
        const emailInput = document.getElementById('contact-email');
        const emailValidation = document.getElementById('email-validation');
        const email = emailInput.value.trim();
        
        if (!email) {
            emailValidation.textContent = 'Email is required.';
            emailValidation.style.display = 'block';
            emailInput.classList.add('is-invalid');
            return false;
        }
        
        // Basic email validation regex (build pattern to avoid Razor parsing issues)
        const atSymbol = String.fromCharCode(64); // email address symbol
        const emailRegex = new RegExp('^[^\\s' + atSymbol + ']+' + atSymbol + '[^\\s' + atSymbol + ']+\\.[^\\s' + atSymbol + ']+$');
        
        if (!emailRegex.test(email)) {
            emailValidation.textContent = 'Please enter a valid email address.';
            emailValidation.style.display = 'block';
            emailInput.classList.add('is-invalid');
            return false;
        }
        
        emailValidation.style.display = 'none';
        emailInput.classList.remove('is-invalid');
        return true;
    }

    // Function to validate phone (required)
    function validatePhone() {
        const phoneInput = document.getElementById('contact-phone');
        const phoneValidation = document.getElementById('phone-validation');
        const phone = phoneInput.value.trim();
        
        if (!phone) {
            phoneValidation.textContent = 'Phone number is required.';
            phoneValidation.style.display = 'block';
            phoneInput.classList.add('is-invalid');
            return false;
        }
        
        // Remove common phone formatting characters for validation
        const cleanedPhone = phone.replace(/[\s\-\(\)\.]/g, '');
        
        // Validate: should contain only digits and optionally start with +
        const phoneRegex = /^\+?\d{10,15}$/;
        
        if (!phoneRegex.test(cleanedPhone)) {
            phoneValidation.textContent = 'Please enter a valid phone number (10-15 digits, e.g. 111-222-3333).';
            phoneValidation.style.display = 'block';
            phoneInput.classList.add('is-invalid');
            return false;
        }
        
        phoneValidation.style.display = 'none';
        phoneInput.classList.remove('is-invalid');
        return true;
    }

    // Function to validate name (required)
    function validateName() {
        const nameInput = document.getElementById('contact-name');
        const nameValidation = document.getElementById('name-validation');
        const name = nameInput.value.trim();
        
        if (!name) {
            nameValidation.textContent = 'Name is required.';
            nameValidation.style.display = 'block';
            nameInput.classList.add('is-invalid');
            return false;
        }
        
        // Check if name is too short or too long
        if (name.length < 2) {
            nameValidation.textContent = 'Name must be at least 2 characters long.';
            nameValidation.style.display = 'block';
            nameInput.classList.add('is-invalid');
            return false;
        }
        
        if (name.length > 100) {
            nameValidation.textContent = 'Name must be 100 characters or less.';
            nameValidation.style.display = 'block';
            nameInput.classList.add('is-invalid');
            return false;
        }
        
        nameValidation.style.display = 'none';
        nameInput.classList.remove('is-invalid');
        return true;
    }


    // Use QuoteHelpers for formatting, rounding, and validation
    const formatNumber = QuoteHelpers.formatNumber;
    const roundToNearest32nd = QuoteHelpers.roundToNearest32nd;
    const roundToNearestHundredth = QuoteHelpers.roundToNearestHundredth;

    // Function to validate diameter
    function validateDiameter() {
        const selectedShape = document.querySelector('input[name="shape"]:checked').value;
        const diameter = parseFloat(diameterInput.value);

        if (!diameterInput.value) {
            diameterValidation.style.display = 'none';
            return;
        }

        if (isNaN(diameter)) {
            diameterValidation.textContent = 'Please enter a valid number.';
            diameterValidation.className = 'size-validation-message error';
            diameterValidation.style.display = 'block';
            return;
        }

        // Size constraints for diameter (same as width max, but for diameter)
        const minDiameter = 0.5;
        const maxDiameter = 12.375; // Max is the smaller of width/height max

        if (diameter < minDiameter || diameter > maxDiameter) {
            diameterValidation.textContent = 'Diameter must be between 0.5" and 12.375".';
            diameterValidation.className = 'size-validation-message error';
            diameterValidation.style.display = 'block';
            return;
        }

        diameterValidation.style.display = 'none';
    }

    // Function to validate label size based on shape
    function validateLabelSize() {
        const selectedShape = document.querySelector('input[name="shape"]:checked').value;
        
        // Skip validation for circle and oval (they use diameter)
        if (selectedShape === 'circle' || selectedShape === 'oval') {
            sizeValidation.style.display = 'none';
            return;
        }

        // Skip validation when width/height are hidden (Existing Die Size)
        if (!isLabelSizeWidthHeightVisible()) {
            sizeValidation.style.display = 'none';
            return;
        }

        const width = parseFloat(widthInput.value);
        const height = parseFloat(heightInput.value);

        if (!widthInput.value || !heightInput.value) {
            sizeValidation.style.display = 'none';
            return;
        }

        if (isNaN(width) || isNaN(height)) {
            sizeValidation.textContent = 'Please enter valid numbers for width and height.';
            sizeValidation.className = 'size-validation-message error';
            sizeValidation.style.display = 'block';
            return;
        }

        // Size constraints
        const minWidth = 0.5;
        const maxWidth = 24.5;
        const minHeight = 0.5;
        const maxHeight = 12.375;

        // Check size constraints
        if (width < minWidth || width > maxWidth || height < minHeight || height > maxHeight) {
            sizeValidation.textContent = 'Labels must normally be between 0.5" and 24.5" horizontally and 0.5" and 12.375" vertically.';
            sizeValidation.className = 'size-validation-message error';
            sizeValidation.style.display = 'block';
            return;
        }

        // Shape-specific validation
        if (selectedShape === 'rectangle' && width === height) {
            sizeValidation.textContent = 'You selected Rectangle shape, width and height should not be same.';
            sizeValidation.className = 'size-validation-message warning';
            sizeValidation.style.display = 'block';
            return;
        }

        if (selectedShape === 'square' && width !== height) {
            sizeValidation.textContent = 'You selected Square shape, width and height should be the same.';
            sizeValidation.className = 'size-validation-message warning';
            sizeValidation.style.display = 'block';
            return;
        }

        if (selectedShape === 'oval' && width === height) {
            sizeValidation.textContent = 'You selected Oval shape, width and height should not be same.';
            sizeValidation.className = 'size-validation-message warning';
            sizeValidation.style.display = 'block';
            return;
        }

        if (selectedShape === 'circle' && width !== height) {
            sizeValidation.textContent = 'You selected Circle shape, width and height should be the same.';
            sizeValidation.className = 'size-validation-message warning';
            sizeValidation.style.display = 'block';
            return;
        }

        // Check if dimensions seem unusual
        const aspectRatio = width / height;
        if (aspectRatio > 10 || aspectRatio < 0.1) {
            sizeValidation.textContent = 'Are you sure you\'ve entered the right size? Please double-check your label width and height before proceeding.';
            sizeValidation.className = 'size-validation-message warning';
            sizeValidation.style.display = 'block';
            return;
        }

        // Show notification when user manually enters label size values
        sizeValidation.textContent = 'Save and use an existing die size.';
        sizeValidation.className = 'size-validation-message warning';
        sizeValidation.style.display = 'block';
    }

    // If a Cutting Die option includes a size like "4.00X3.00",
    // populate Label Size Width/Height from that selection.
    function applyCuttingDieToLabelSize() {
        const cuttingDieSelect = document.getElementById('cutting-die');
        if (!cuttingDieSelect || cuttingDieSelect.selectedIndex < 0) return;
        if (!widthInput || !heightInput) return;

        const selectedOption = cuttingDieSelect.options[cuttingDieSelect.selectedIndex];
        const text = (selectedOption?.text || selectedOption?.textContent || '').trim();
        if (!text) return;

        // Find first occurrence of "<float> X <float>" (case-insensitive)
        // Examples: "4.00X3.00", "4x3", "4.0 x 3.0"
        const match = text.match(/(\d+(?:\.\d+)?)\s*[xX]\s*(\d+(?:\.\d+)?)/);
        if (!match) return;

        const w = match[1];
        const h = match[2];

        widthInput.value = w;
        heightInput.value = h;

        // Re-run validations and UI updates that depend on size
        validateLabelSize();
        updateSummaryPanel();
    }

    // Function to toggle corners section visibility
    function toggleCornersSection() {
        const selectedShape = document.querySelector('input[name="shape"]:checked').value;
        if (selectedShape === 'rectangle' || selectedShape === 'square') {
            cornersSection.style.display = 'block';
        } else {
            cornersSection.style.display = 'none';
        }
    }

// Function to toggle label size and diameter sections
function toggleSizeSections() {
    const selectedShape = document.querySelector('input[name="shape"]:checked').value;
    if (selectedShape === 'circle' || selectedShape === 'oval') {
        labelSizeSection.style.display = 'none';
        diameterSection.style.display = 'block';
    } else {
        labelSizeSection.style.display = 'block';
        diameterSection.style.display = 'none';
    }
}

    // Round width/height inputs to nearest 1/32" on blur
    function handleSizeInputBlur(input) {
        const rounded = roundToNearest32nd(input.value);
        if (rounded && rounded !== input.value) {
            input.value = rounded;
            validateLabelSize();
        }
    }

    // Round diameter input to nearest hundredth on blur
    function handleDiameterInputBlur(input) {
        const rounded = roundToNearestHundredth(input.value);
        if (rounded && rounded !== input.value) {
            input.value = rounded;
            validateDiameter();
        }
    }


    const validateNumericInput = QuoteHelpers.validateNumericInput;
    const formatLabel = QuoteHelpers.formatLabel;

    // Function to get selected printing option
    function getSelectedPrinting() {
        // Find the printing section
        let printingFilter = null;
        Array.from(document.querySelectorAll('.form-section')).forEach(section => {
            const label = section.querySelector('.form-section-label');
            if (label && label.textContent.trim() === 'Printing') {
                printingFilter = section.querySelector('#printing-filter');
            }
        });
        
        if (printingFilter) {
            const activeBtn = printingFilter.querySelector('button.active');
            if (activeBtn) {
                return activeBtn.getAttribute('data-printing-text') || activeBtn.textContent.trim();
            }
        }
        return '';
    }

    // Get selected printing Id (e.g. "2F") for server-side queries
    function getSelectedPrintingId() {
        let printingFilter = null;
        Array.from(document.querySelectorAll('.form-section')).forEach(section => {
            const label = section.querySelector('.form-section-label');
            if (label && label.textContent.trim() === 'Printing') {
                printingFilter = section.querySelector('#printing-filter');
            }
        });

        if (printingFilter) {
            const activeBtn = printingFilter.querySelector('button.active');
            if (activeBtn) {
                return activeBtn.getAttribute('data-printing-id') || '';
            }
        }
        return '';
    }

    // Function to update summary panel with selected choices
    function updateSummaryPanel() {
        const summaryContainer = document.getElementById('selected-choices-summary');
        const choices = [];

        // Reference (optional) - show first when present
        const refType = document.getElementById('reference-type')?.value;
        const refValue = document.getElementById('reference-value')?.value?.trim();
        if (refValue) {
            const refLabels = { 'company-name': 'Company Name', 'account-number': 'Account Number', 'purchase-order-number': 'Purchase Order Number', 'invoice-number': 'Invoice Number' };
            choices.push({ label: refLabels[refType] || 'Reference', value: refValue });
        }

        // Description - always show in summary
        const description = document.getElementById('description')?.value.trim();
        choices.push({ 
            label: 'Description', 
            value: description || 'Not specified' 
        });

        // Shape
        const shape = document.querySelector('input[name="shape"]:checked');
        if (shape) {
            choices.push({ label: 'Shape', value: formatLabel(shape.value) });
        }

        // Size (Width x Height or Diameter)
        const selectedShape = shape ? shape.value : '';
        if (selectedShape === 'circle' || selectedShape === 'oval') {
            const diameter = document.getElementById('diameter')?.value.trim();
            if (diameter) {
                choices.push({ label: 'Diameter', value: `${diameter}"` });
            }
        } else {
            const width = document.getElementById('label-width')?.value.trim();
            const height = document.getElementById('label-height')?.value.trim();
            if (width || height) {
                choices.push({ label: 'Size', value: `${width || ''}" × ${height || ''}"` });
            }
        }

        // Corners (only for rectangle/square)
        if (selectedShape === 'rectangle' || selectedShape === 'square') {
            const corners = document.querySelector('input[name="corners"]:checked');
            if (corners) {
                choices.push({ label: 'Corners', value: formatLabel(corners.value) });
            }
        }

        // Cutting Die
        const shapeForDie = document.querySelector('input[name="shape"]:checked')?.value;
        const cuttingDie = document.getElementById('cutting-die')?.value;
        if (isCuttingDieApplicableForShape(shapeForDie) && cuttingDie) {
            const cuttingDieSelect = document.getElementById('cutting-die');
            const selectedOption = cuttingDieSelect.options[cuttingDieSelect.selectedIndex];
            choices.push({ label: 'Cutting Die', value: selectedOption.text });
        }

        // Printing
        const printing = getSelectedPrinting();
        if (printing) {
            choices.push({ label: 'Printing', value: printing });
        }

        // Material
        const material = document.getElementById('material')?.value;
        if (material) {
            const materialSelect = document.getElementById('material');
            const selectedOption = materialSelect.options[materialSelect.selectedIndex];
            choices.push({ label: 'Material', value: selectedOption.text });
        }


        // Finish
        const finish = document.getElementById('finish')?.value;
        if (finish) {
            const finishSelect = document.getElementById('finish');
            const selectedOption = finishSelect.options[finishSelect.selectedIndex];
            choices.push({ label: 'Finish', value: selectedOption.text });
        }

        // Application Method
        const applicationMethod = document.querySelector('input[name="application-method"]:checked');
        if (applicationMethod) {
            const label = applicationMethod.closest('label').textContent.trim().replace(/\s*\([^)]*\)/, '');
            choices.push({ label: 'Application Method', value: label });
        }

        // Unwind Direction
        const unwindDirection = document.querySelector('input[name="unwind-direction"]:checked');
        if (unwindDirection) {
            const label = unwindDirection.closest('label').textContent.trim();
            choices.push({ label: 'Unwind Direction', value: label });
        }

        // Quantities
        const quantityInputs = document.querySelectorAll('.quantity-input');
        const quantityValues = Array.from(quantityInputs)
            .map(el => parseInt(el.value?.trim(), 10))
            .filter(n => !isNaN(n) && n >= 1);
        if (quantityValues.length > 0) {
            choices.push({ label: 'Quantity', value: quantityValues.map(q => q.toLocaleString()).join(' / ') });
        }

        // Artwork Option
        const artworkOption = document.querySelector('input[name="artwork-option"]:checked');
        if (artworkOption) {
            let artworkValue = '';
            switch(artworkOption.value) {
                case 'upload-now':
                    artworkValue = 'Upload artwork now';
                    break;
                case 'artwork-not-ready':
                    artworkValue = 'Artwork is not ready';
                    break;
                case 'upload-later':
                    artworkValue = 'Upload artwork later';
                    break;
                default:
                    artworkValue = artworkOption.value;
            }
            choices.push({ label: 'Artwork', value: artworkValue });
        }

        // Build HTML
        if (choices.length === 0) {
            summaryContainer.innerHTML = '<p class="summary-description" style="color: #6c757d; font-style: italic;">No selections made yet.</p>';
            return;
        }

        let html = '';
        choices.forEach((choice, index) => {
            html += `
                <div class="summary-item" ${index > 0 ? 'style="margin-top: 1rem;"' : ''}>
                    <span class="summary-label">${choice.label}</span>
                    <p class="summary-description" style="margin-top: 0.25rem; margin-bottom: 0;">${choice.value}</p>
                </div>
            `;
        });

        summaryContainer.innerHTML = html;
    }

    // Reference field listeners (update summary)
    document.getElementById('reference-type')?.addEventListener('change', updateSummaryPanel);
    document.getElementById('reference-value')?.addEventListener('input', updateSummaryPanel);
    document.getElementById('reference-value')?.addEventListener('blur', updateSummaryPanel);

    // Customer autocomplete logic has been moved to wwwroot/js/quote/quote-autocomplete.js
    // Please ensure that file is included in your HTML after quote.js
    if (widthInput) {
        widthInput.addEventListener('blur', function() { 
            handleSizeInputBlur(this);
            updateSummaryPanel();
        });
    }
    if (heightInput) {
        heightInput.addEventListener('input', function() {
            if (validateNumericInput(this)) {
                validateLabelSize();
                updateSummaryPanel();
            }
        });
        heightInput.addEventListener('blur', function() { 
            handleSizeInputBlur(this);
            updateSummaryPanel();
        });
    }
    // Diameter input handlers
    if (diameterInput) {
        diameterInput.addEventListener('input', function() {
            if (validateNumericInput(this)) {
                validateDiameter();
                updateSummaryPanel();
            }
        });
        diameterInput.addEventListener('blur', function() { 
            handleDiameterInputBlur(this);
            updateSummaryPanel();
        });
    }

    // Corners change handler
    document.querySelectorAll('input[name="corners"]').forEach(input => {
        input.addEventListener('change', function() {
            toggleLabelSizeInputsForMode();
            toggleCuttingDieSection();
            validateLabelSize();
            updateSummaryPanel();
        });
    });

    // Cutting Die change handler:
    // - updates summary
    // - auto-populates label size when option contains "W x H" (e.g. "4.00X3.00")
    document.getElementById('cutting-die')?.addEventListener('change', function() {
        applyCuttingDieToLabelSize();
        updateSummaryPanel();
    });

    // Add quantity button (max 5)
    const quantityContainer = document.getElementById('quantity-inputs-container');
    const addQuantityBtn = document.getElementById('add-quantity-btn');
    const MAX_QUANTITIES = 5;
    function updateAddQuantityButton() {
        const count = quantityContainer?.querySelectorAll('.quantity-row').length || 0;
        if (addQuantityBtn) addQuantityBtn.disabled = count >= MAX_QUANTITIES;
    }
    function addQuantityRow(value = '') {
        const count = quantityContainer?.querySelectorAll('.quantity-row').length || 0;
        if (count >= MAX_QUANTITIES) return;
        const row = document.createElement('div');
        row.className = 'quantity-row d-flex align-items-center gap-2';
        row.innerHTML = '<label class="mb-0" style="min-width: 5rem;">Quantity</label><input type="number" class="form-control quantity-input" name="quantity" min="1" placeholder="e.g. 1000" value="' + (value || '') + '"><button type="button" class="btn btn-outline-danger btn-sm remove-quantity-btn" title="Remove">×</button>';
        quantityContainer?.appendChild(row);
        row.querySelector('.remove-quantity-btn')?.addEventListener('click', function() {
            row.remove();
            updateAddQuantityButton();
            updateSummaryPanel();
        });
        row.querySelector('.quantity-input')?.addEventListener('input', updateSummaryPanel);
        updateAddQuantityButton();
        updateSummaryPanel();
    }
    if (addQuantityBtn) {
        addQuantityBtn.addEventListener('click', function() { addQuantityRow(); });
    }
    // Init from data-initial-quantities (e.g. when returning from Edit)
    const initialQtys = quantityContainer?.dataset.initialQuantities?.split(',').map(s => s.trim()).filter(Boolean) || [];
    if (initialQtys.length > 1) {
        const firstInput = quantityContainer?.querySelector('.quantity-input');
        if (firstInput) firstInput.value = initialQtys[0] || '';
        for (let i = 1; i < Math.min(initialQtys.length, MAX_QUANTITIES); i++) {
            addQuantityRow(initialQtys[i]);
        }
    }
    updateAddQuantityButton();
    quantityContainer?.addEventListener('input', function(e) {
        if (e.target?.classList?.contains('quantity-input')) updateSummaryPanel();
    });

    // Printing buttons are handled in populatePrintingOptions function

    // Material change handler
    document.getElementById('material')?.addEventListener('change', updateSummaryPanel);

    // Finish change handler
    document.getElementById('finish')?.addEventListener('change', updateSummaryPanel);

    // Application Method change handler
    document.querySelectorAll('input[name="application-method"]').forEach(input => {
        input.addEventListener('change', updateSummaryPanel);
    });

    // Unwind Direction change handler
    document.querySelectorAll('input[name="unwind-direction"]').forEach(input => {
        input.addEventListener('change', updateSummaryPanel);
    });

    // Versions and Total Quantity change handlers
    document.getElementById('versions')?.addEventListener('input', updateSummaryPanel);
    document.getElementById('versions')?.addEventListener('change', updateSummaryPanel);
    document.getElementById('total-quantity')?.addEventListener('input', updateSummaryPanel);
    document.getElementById('total-quantity')?.addEventListener('change', updateSummaryPanel);

    // Artwork option change handler
    document.querySelectorAll('input[name="artwork-option"]').forEach(input => {
        input.addEventListener('change', updateSummaryPanel);
    });

    // Description change handler
    const descriptionInput = document.getElementById('description');
    const descriptionCharCount = document.getElementById('description-char-count');
    const DESCRIPTION_MAX = 50;
    function updateDescriptionCharCount() {
        if (!descriptionCharCount) return;
        const len = (descriptionInput?.value ?? '').length;
        descriptionCharCount.textContent = `${len}/${DESCRIPTION_MAX} characters`;
        descriptionCharCount.classList.toggle('text-danger', len >= DESCRIPTION_MAX);
        descriptionCharCount.classList.toggle('text-muted', len < DESCRIPTION_MAX);
    }
    descriptionInput?.addEventListener('input', function() {
        updateDescriptionCharCount();
        updateSummaryPanel();
    });
    descriptionInput?.addEventListener('change', function() {
        updateDescriptionCharCount();
        updateSummaryPanel();
    });
    updateDescriptionCharCount();

    // Contact information validation handlers
    document.getElementById('contact-name')?.addEventListener('blur', validateName);
    document.getElementById('contact-name')?.addEventListener('input', function() {
        if (this.classList.contains('is-invalid')) {
            validateName();
        }
    });

    document.getElementById('contact-email')?.addEventListener('blur', validateEmail);
    document.getElementById('contact-email')?.addEventListener('input', function() {
        if (this.classList.contains('is-invalid')) {
            validateEmail();
        }
    });

    document.getElementById('contact-phone')?.addEventListener('blur', validatePhone);
    document.getElementById('contact-phone')?.addEventListener('input', function() {
        if (this.classList.contains('is-invalid')) {
            validatePhone();
        }
    });

    // Initial setup
    toggleCornersSection();
    toggleSizeSections();
    toggleLabelSizeInputsForMode();
    toggleCuttingDieSection();
    validateLabelSize();
    validateDiameter();
    updateSummaryPanel();
    
    // Load materials immediately (no dependency on Printing)
    loadMaterials().catch(error => {
        console.error('Failed to load materials:', error);
        if (materialError) {
            materialError.textContent = 'Error loading materials. Please refresh the page.';
            materialError.style.display = 'block';
        }
    });
    
    
    // Load printing options (ColorCodes) from API on page load (runs asynchronously, doesn't block UI)
    if (printingFilter && printingLoading) {
        loadPrintingOptions().then(() => {
            // After printing options load, check if printing is already selected
            const selectedPrintingText = getSelectedPrinting();
            const selectedPrintingId = getSelectedPrintingId();
            if (selectedPrintingId || selectedPrintingText) {
                // Load cutting die and finishing options based on selected printing
                const currentShape = document.querySelector('input[name="shape"]:checked')?.value;
                if (isCuttingDieRequiredForCurrentState() && isCuttingDieApplicableForShape(currentShape)) {
                    loadCuttingDieOptions(selectedPrintingId || selectedPrintingText);
                }
                if (selectedPrintingText) {
                    loadFinishingOptions(selectedPrintingText);
                }
            }
            // Restore form data after everything loads
            setTimeout(() => restoreFormData(), 300);
        }).catch(error => {
            console.error('Failed to load printing options:', error);
            // Still restore form data even if printing options fail
            setTimeout(() => restoreFormData(), 300);
        });
    } else {
        console.error('Cannot load printing options - required elements not found. printingFilter:', printingFilter, 'printingLoading:', printingLoading);
        // Still restore form data
        setTimeout(() => restoreFormData(), 300);
    }

    // Restore form data if returning from confirmation page
    function restoreFormData() {
        const savedDataScript = document.getElementById('saved-quote-data');
        if (!savedDataScript) {
            console.log('No saved quote data found - this is a new form');
            return;
        }

        try {
            const savedData = JSON.parse(savedDataScript.textContent);
            console.log('Restoring form data:', savedData);
            
            // Restore contact information
            if (savedData.name) {
                document.getElementById('contact-name').value = savedData.name;
            }
            if (savedData.email) {
                document.getElementById('contact-email').value = savedData.email;
            }
            if (savedData.phone) {
                document.getElementById('contact-phone').value = savedData.phone;
            }

            // Restore reference
            if (savedData.referenceType) {
                const refType = document.getElementById('reference-type');
                if (refType) refType.value = savedData.referenceType;
            }
            if (savedData.referenceValue) {
                const refValue = document.getElementById('reference-value');
                if (refValue) refValue.value = savedData.referenceValue;
            }
            
            // Restore description
            if (savedData.description) {
                document.getElementById('description').value = savedData.description;
                if (typeof updateDescriptionCharCount === 'function') updateDescriptionCharCount();
            }

            // Restore shape
            if (savedData.shapeValue) {
                const shapeRadio = document.querySelector(`input[name="shape"][value="${savedData.shapeValue}"]`);
                if (shapeRadio) {
                    shapeRadio.checked = true;
                    toggleCornersSection();
                    toggleSizeSections();
                }
            } else if (savedData.shape) {
                // Fallback: try to match by display text
                const shapeValue = savedData.shape.toLowerCase();
                const shapeRadio = document.querySelector(`input[name="shape"][value="${shapeValue}"]`);
                if (shapeRadio) {
                    shapeRadio.checked = true;
                    toggleCornersSection();
                    toggleSizeSections();
                }
            }

            // Restore size (width/height or diameter)
            if (savedData.diameter) {
                document.getElementById('diameter').value = savedData.diameter;
            } else {
                if (savedData.labelWidth) {
                    document.getElementById('label-width').value = savedData.labelWidth;
                }
                if (savedData.labelHeight) {
                    document.getElementById('label-height').value = savedData.labelHeight;
                }
            }

            // Restore corners
            if (savedData.cornersValue) {
                const cornersRadio = document.querySelector(`input[name="corners"][value="${savedData.cornersValue}"]`);
                if (cornersRadio) {
                    cornersRadio.checked = true;
                }
            } else if (savedData.corners) {
                // Fallback: try to match by display text
                const cornersValue = savedData.corners.toLowerCase();
                const cornersRadio = document.querySelector(`input[name="corners"][value="${cornersValue}"]`);
                if (cornersRadio) {
                    cornersRadio.checked = true;
                }
            }

            // Restore cutting die
            if (savedData.cuttingDieValue) {
                document.getElementById('cutting-die').value = savedData.cuttingDieValue;
            } else if (savedData.cuttingDie) {
                // Fallback: try to find option by text
                const cuttingDieSelect = document.getElementById('cutting-die');
                for (let option of cuttingDieSelect.options) {
                    if (option.text === savedData.cuttingDie) {
                        option.selected = true;
                        break;
                    }
                }
            }

            // Printing restoration is handled in populatePrintingOptions function
            // No additional restoration needed here since buttons are created dynamically

            // Restore material - wait for materials to load first
            if (savedData.materialValue) {
                const restoreMaterial = () => {
                    const materialSelect = document.getElementById('material');
                    if (materialSelect) {
                        materialSelect.value = savedData.materialValue;
                    }
                };
                
                // Wait a bit for materials to load, then restore
                setTimeout(restoreMaterial, 500);
                setTimeout(restoreMaterial, 1500);
                setTimeout(restoreMaterial, 3000);
            } else if (savedData.material) {
                // Fallback: try to find by text
                const restoreMaterial = () => {
                    const materialSelect = document.getElementById('material');
                    if (materialSelect && materialSelect.options.length > 1) {
                        for (let option of materialSelect.options) {
                            if (option.text === savedData.material) {
                                option.selected = true;
                                break;
                            }
                        }
                    }
                };
                setTimeout(restoreMaterial, 500);
                setTimeout(restoreMaterial, 1500);
                setTimeout(restoreMaterial, 3000);
            }

            // Printing restoration is handled in populatePrintingOptions function
            // No additional restoration needed here since buttons are created dynamically

            // Restore finish
            if (savedData.finishValue) {
                document.getElementById('finish').value = savedData.finishValue;
            } else if (savedData.finish) {
                // Fallback: try to find by text
                const finishSelect = document.getElementById('finish');
                for (let option of finishSelect.options) {
                    if (option.text === savedData.finish) {
                        option.selected = true;
                        break;
                    }
                }
            }

            // Restore application method
            if (savedData.applicationMethodValue) {
                const appMethodRadio = document.querySelector(`input[name="application-method"][value="${savedData.applicationMethodValue}"]`);
                if (appMethodRadio) {
                    appMethodRadio.checked = true;
                }
            } else if (savedData.applicationMethod) {
                // Fallback: try to match by display text
                const appMethodValue = savedData.applicationMethod.toLowerCase();
                const appMethodRadio = document.querySelector(`input[name="application-method"][value="${appMethodValue}"]`);
                if (appMethodRadio) {
                    appMethodRadio.checked = true;
                }
            }

            // Restore unwind direction
            if (savedData.unwindDirectionValue) {
                const unwindRadio = document.querySelector(`input[name="unwind-direction"][value="${savedData.unwindDirectionValue}"]`);
                if (unwindRadio) {
                    unwindRadio.checked = true;
                }
            } else if (savedData.unwindDirection) {
                // Fallback: try to match by display text
                const unwindRadios = document.querySelectorAll('input[name="unwind-direction"]');
                unwindRadios.forEach(radio => {
                    const label = radio.closest('label');
                    if (label && label.textContent.trim() === savedData.unwindDirection) {
                        radio.checked = true;
                    }
                });
            }

            // Restore total quantity
            if (savedData.totalQuantity) {
                document.getElementById('total-quantity').value = savedData.totalQuantity;
            }

            // Restore artwork option
            if (savedData.artworkOptionValue) {
                const artworkRadio = document.querySelector(`input[name="artwork-option"][value="${savedData.artworkOptionValue}"]`);
                if (artworkRadio) {
                    artworkRadio.checked = true;
                }
            } else if (savedData.artworkOption) {
                // Fallback: try to match by display text
                let artworkValue = '';
                if (savedData.artworkOption === 'Upload artwork now') {
                    artworkValue = 'upload-now';
                } else if (savedData.artworkOption === 'Artwork is not ready') {
                    artworkValue = 'artwork-not-ready';
                } else if (savedData.artworkOption === 'Upload artwork later') {
                    artworkValue = 'upload-later';
                }
                
                if (artworkValue) {
                    const artworkRadio = document.querySelector(`input[name="artwork-option"][value="${artworkValue}"]`);
                    if (artworkRadio) {
                        artworkRadio.checked = true;
                    }
                }
            }

            // Update summary panel after restoring
            setTimeout(() => {
                updateSummaryPanel();
                console.log('Form data restored successfully');
            }, 500);
        } catch (error) {
            console.error('Error restoring form data:', error);
        }
    }

    // Restore form data if returning from confirmation page
    // Try immediately first (most fields don't need materials to load)
    restoreFormData();
    
    // Also try after a short delay to catch any timing issues
    setTimeout(() => restoreFormData(), 100);
    
    // And again after materials potentially load
    setTimeout(() => restoreFormData(), 2000);

    // Form submission handler for "Send Quote" button
    const sendQuoteBtn = document.getElementById('send-quote-btn');
    if (sendQuoteBtn) {
        sendQuoteBtn.addEventListener('click', function(e) {
            e.preventDefault();
            
            // Validate required fields before submission
            const description = document.getElementById('description')?.value.trim();
            if (!description || description.length < 5) {
                alert('Please enter a description (at least 5 characters).');
                document.getElementById('description')?.focus();
                return;
            }
            if (description.length > 50) {
                alert('Description cannot exceed 50 characters.');
                document.getElementById('description')?.focus();
                return;
            }

            // Validate contact information if provided
            if (!validateName() || !validateEmail() || !validatePhone()) {
                alert('Please correct the contact information errors before submitting.');
                return;
            }

            // Validate label size based on shape
            const selectedShape = document.querySelector('input[name="shape"]:checked')?.value;
            if (!selectedShape) {
                alert('Please select a shape.');
                return;
            }

            if (selectedShape === 'circle' || selectedShape === 'oval') {
                const diameter = document.getElementById('diameter')?.value.trim();
                if (!diameter || isNaN(parseFloat(diameter))) {
                    alert('Please enter a valid diameter.');
                    document.getElementById('diameter')?.focus();
                    return;
                }
                validateDiameter();
                const diameterValidation = document.getElementById('diameter-validation');
                if (diameterValidation.style.display === 'block' && diameterValidation.classList.contains('error')) {
                    alert('Please correct the diameter value.');
                    return;
                }
            } else {
                if (isLabelSizeWidthHeightVisible()) {
                    const width = document.getElementById('label-width')?.value.trim();
                    const height = document.getElementById('label-height')?.value.trim();
                    if (!width || !height || isNaN(parseFloat(width)) || isNaN(parseFloat(height))) {
                        alert('Please enter valid width and height values.');
                        return;
                    }
                    validateLabelSize();
                    const sizeValidation = document.getElementById('size-validation');
                    if (sizeValidation.style.display === 'block' && sizeValidation.classList.contains('error')) {
                        alert('Please correct the label size values.');
                        return;
                    }
                }
            }

            // Validate printing selection
            const selectedPrinting = getSelectedPrinting();
            if (!selectedPrinting) {
                alert('Please select a printing option.');
                return;
            }

            // Validate material selection
            const materialSelect = document.getElementById('material');
            const selectedMaterial = materialSelect?.value;
            if (!selectedMaterial) {
                alert('Please select a material.');
                return;
            }

            // Validate cutting die selection
            const cuttingDieSelect = document.getElementById('cutting-die');
            const selectedCuttingDie = cuttingDieSelect?.value;
            if (isCuttingDieRequiredForCurrentState()) {
                if (!selectedCuttingDie) {
                    alert('Please select a cutting die option.');
                    return;
                }
            }

            // Validate finish selection
            const finishSelect = document.getElementById('finish-select');
            const selectedFinish = finishSelect?.value;
            if (!selectedFinish) {
                alert('Please select a finish option.');
                return;
            }

            // Validate quantities (at least one valid quantity)
            const quantityInputs = document.querySelectorAll('.quantity-input');
            const quantities = Array.from(quantityInputs)
                .map(el => parseInt(el.value?.trim(), 10))
                .filter(n => !isNaN(n) && n >= 1);
            if (quantities.length === 0) {
                alert('Please enter at least one valid quantity (1 or more).');
                const first = document.querySelector('.quantity-input');
                if (first) first.focus();
                return;
            }

            // Validate artwork option
            const artworkOption = document.querySelector('input[name="artwork-option"]:checked')?.value;
            if (!artworkOption) {
                alert('Please select an artwork option.');
                return;
            }

            // Get the form element
            const form = document.getElementById('quote-form');
            if (!form) {
                console.error('Quote form not found');
                return;
            }

            // Get display text for dropdowns
            const getSelectedText = (selectElement) => {
                if (!selectElement || !selectElement.value) return '';
                const selectedOption = selectElement.options[selectElement.selectedIndex];
                return selectedOption ? selectedOption.text : '';
            };

            // Get application method display text
            const getApplicationMethodText = () => {
                const selected = document.querySelector('input[name="application-method"]:checked');
                if (!selected) return '';
                const label = selected.closest('label');
                return label ? label.textContent.trim().replace(/\s*\([^)]*\)/, '').trim() : '';
            };

            // Get unwind direction display text
            const getUnwindDirectionText = () => {
                const selected = document.querySelector('input[name="unwind-direction"]:checked');
                if (!selected) return '';
                const label = selected.closest('label');
                return label ? label.textContent.trim() : '';
            };

            // Get printing button data
            const printingFilter = document.getElementById('printing-filter');
            const activePrintingBtn = printingFilter?.querySelector('button.active');
            const printingId = activePrintingBtn?.getAttribute('data-printing-id') || '';
            const printingText = activePrintingBtn?.getAttribute('data-printing-text') || selectedPrinting;

            // Add hidden fields to the form for backend mapping
            const addHiddenField = (name, value) => {
                let hidden = form.querySelector(`input[name="${name}"]`);
                if (!hidden) {
                    hidden = document.createElement('input');
                    hidden.type = 'hidden';
                    hidden.name = name;
                    form.appendChild(hidden);
                }
                hidden.value = value;
            };

            // Map form fields to backend expected names and add display values
            // Set the form action to include the handler for ASP.NET Core Razor Pages
            const currentUrl = window.location.pathname;
            form.action = currentUrl + '?handler=Submit';
            addHiddenField('name', document.getElementById('contact-name')?.value.trim() || '');
            addHiddenField('email', document.getElementById('contact-email')?.value.trim() || '');
            addHiddenField('phone', document.getElementById('contact-phone')?.value.trim() || '');
            addHiddenField('referenceType', document.getElementById('reference-type')?.value || '');
            addHiddenField('referenceValue', document.getElementById('reference-value')?.value.trim() || '');
            addHiddenField('labelWidth', document.getElementById('label-width')?.value.trim() || '');
            addHiddenField('labelHeight', document.getElementById('label-height')?.value.trim() || '');
            addHiddenField('cuttingDie', getSelectedText(cuttingDieSelect));
            addHiddenField('cuttingDieValue', selectedCuttingDie);
            addHiddenField('printing', printingId);
            addHiddenField('printingValue', printingId);
            addHiddenField('colorCode', printingId);
            addHiddenField('colorCodeValue', printingId);
            addHiddenField('material', getSelectedText(materialSelect));
            addHiddenField('materialValue', selectedMaterial);
            addHiddenField('finish', getSelectedText(finishSelect));
            addHiddenField('finishValue', selectedFinish);
            addHiddenField('applicationMethod', getApplicationMethodText());
            addHiddenField('applicationMethodValue', document.querySelector('input[name="application-method"]:checked')?.value || '');
            addHiddenField('unwindDirection', getUnwindDirectionText());
            addHiddenField('unwindDirectionValue', document.querySelector('input[name="unwind-direction"]:checked')?.value || '');
            addHiddenField('totalQuantity', quantities.reduce((a, b) => a + b, 0).toString());
            addHiddenField('artworkOption', document.querySelector('input[name="artwork-option"]:checked')?.closest('label')?.textContent.trim() || '');
            addHiddenField('artworkOptionValue', artworkOption);
            
            // Get shape ID from data-shape-id attribute (ID from API), fall back to value if not present
            const selectedShapeInput = document.querySelector('input[name="shape"]:checked');
            const shapeId = selectedShapeInput?.getAttribute('data-shape-id') || selectedShapeInput?.value || '';
            addHiddenField('shapeValue', shapeId); // Use the ID from API
            addHiddenField('shape', selectedShapeInput?.closest('label')?.querySelector('.shape-label')?.textContent.trim() || selectedShape); // Keep display text
            
            // Corners
            const selectedCorners = document.querySelector('input[name="corners"]:checked');
            if (selectedCorners) {
                addHiddenField('cornersValue', selectedCorners.value);
                addHiddenField('corners', selectedCorners.closest('label')?.querySelector('.shape-label')?.textContent.trim() || selectedCorners.value);
            }

            // Submit the form
            form.submit();
        });
    } else {
        console.error('Send Quote button not found');
    }

    // --- Test case prefill (Load test case / Clear form) ---
    const loadTestCaseSelect = document.getElementById('load-test-case');
    const clearFormBtn = document.getElementById('clear-form-btn');

    function applyTestCase(tc) {
        if (!tc || !tc.data) return;
        const d = tc.data;

        // 1. Simple fields
        const set = (id, v) => { const el = document.getElementById(id); if (el && v != null && v !== '') el.value = String(v); };
        const setRadio = (name, val) => {
            const r = document.querySelector(`input[name="${name}"][value="${val}"]`);
            if (r) r.checked = true;
        };
        if (d.referenceType) {
            const rt = document.getElementById('reference-type');
            if (rt) rt.value = d.referenceType;
        }
        set('reference-value', d.referenceValue);
        set('description', d.description);
        if (typeof updateDescriptionCharCount === 'function') updateDescriptionCharCount();
        set('contact-name', d.name);
        set('contact-email', d.email);
        set('contact-phone', d.phone);
        // Quantities: set first input, add more if provided
        const qtys = Array.isArray(d.quantities) ? d.quantities : (d.totalQuantity != null && d.totalQuantity !== '' ? [d.totalQuantity] : []);
        const firstQtyInput = document.querySelector('.quantity-input');
        if (firstQtyInput) firstQtyInput.value = qtys[0] != null ? String(qtys[0]) : '';
        for (let i = 1; i < Math.min(qtys.length, MAX_QUANTITIES); i++) {
            addQuantityRow(qtys[i] != null ? String(qtys[i]) : '');
        }
        updateAddQuantityButton();

        // 2. Shape → toggles
        if (d.shapeValue) {
            setRadio('shape', d.shapeValue);
            toggleCornersSection();
            toggleSizeSections();
        }
        if (d.cornersValue) {
            setRadio('corners', d.cornersValue);
            toggleLabelSizeInputsForMode();
            toggleCuttingDieSection();
        }
        if (d.dieSizeMode && dieSizeModeSelect && (d.cornersValue || '').toLowerCase() !== 'square') {
            if (dieSizeModeSelect.dataset.forcedByCorners === 'true') {
                delete dieSizeModeSelect.dataset.forcedByCorners;
                delete dieSizeModeSelect.dataset.prevMode;
            }
            dieSizeModeSelect.value = d.dieSizeMode;
            toggleLabelSizeInputsForMode();
            toggleCuttingDieSection();
        }

        // 3. Size: diameter vs width/height
        const shape = (d.shapeValue || '').toLowerCase();
        if (shape === 'circle' || shape === 'oval') {
            if (diameterInput) diameterInput.value = d.diameter != null && d.diameter !== '' ? String(d.diameter) : '';
            if (widthInput) widthInput.value = '';
            if (heightInput) heightInput.value = '';
        } else {
            if (diameterInput) diameterInput.value = '';
            if (widthInput) widthInput.value = d.labelWidth != null && d.labelWidth !== '' ? String(d.labelWidth) : '';
            if (heightInput) heightInput.value = d.labelHeight != null && d.labelHeight !== '' ? String(d.labelHeight) : '';
        }
        validateLabelSize();
        validateDiameter();

        // 4. Application, unwind, artwork
        if (d.applicationMethodValue) setRadio('application-method', d.applicationMethodValue);
        if (d.unwindDirectionValue) setRadio('unwind-direction', d.unwindDirectionValue);
        if (d.artworkOptionValue) setRadio('artwork-option', d.artworkOptionValue);

        // 5. Printing: click matching button → loads finish + cutting die
        const pv = (d.printingValue || '').trim();
        if (pv && printingFilter) {
            const btn = printingFilter.querySelector(`button[data-printing-id="${pv}"]`);
            if (btn) {
                printingFilter.querySelectorAll('button').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                btn.click();
            }
        }

        // 6. After delay: material, finish, cutting die (options may still be loading)
        const applyDelayed = () => {
            const matSel = document.getElementById('material');
            const finSel = document.getElementById('finish');
            const dieSel = document.getElementById('cutting-die');
            const norm = (s) => (s || '').toString().trim().toLowerCase();

            const optText = (o) => norm((o.text || o.textContent || '').toString());
            if (matSel && (d.materialValue || d.material)) {
                let optMat = d.materialValue ? Array.from(matSel.options).find(o => o.value === d.materialValue) : null;
                if (!optMat && d.material) {
                    const m = norm(d.material);
                    optMat = Array.from(matSel.options).find(o => o.value && optText(o) === m)
                        || Array.from(matSel.options).find(o => o.value && optText(o).includes(m));
                }
                if (optMat) matSel.value = optMat.value;
            }
            if (finSel && (d.finishValue || d.finish)) {
                let optFin = d.finishValue ? Array.from(finSel.options).find(o => o.value === d.finishValue) : null;
                if (!optFin && d.finish) {
                    const f = norm(d.finish);
                    optFin = Array.from(finSel.options).find(o => o.value && optText(o) === f)
                        || Array.from(finSel.options).find(o => o.value && optText(o).includes(f));
                }
                if (optFin) finSel.value = optFin.value;
            }
            if (d.cuttingDieValue && dieSel) {
                const opt = Array.from(dieSel.options).find(o => o.value === d.cuttingDieValue || o.text === d.cuttingDieValue);
                if (opt) {
                    dieSel.value = opt.value;
                    applyCuttingDieToLabelSize();
                }
            }
            toggleCuttingDieSection();
            validateLabelSize();
            updateSummaryPanel();
        };
        setTimeout(applyDelayed, 600);
        setTimeout(applyDelayed, 1200);
        setTimeout(applyDelayed, 2500);
        setTimeout(applyDelayed, 3500);
    }

    function clearForm() {
        const set = (id, v) => { const el = document.getElementById(id); if (el) el.value = v; };
        const setRadio = (name, val) => {
            const r = document.querySelector(`input[name="${name}"][value="${val}"]`);
            if (r) r.checked = true;
        };
        set('description', '');
        if (typeof updateDescriptionCharCount === 'function') updateDescriptionCharCount();
        set('reference-value', '');
        const refType = document.getElementById('reference-type');
        if (refType) refType.value = 'company-name';
        set('contact-name', '');
        set('contact-email', '');
        set('contact-phone', '');
        set('diameter', '');
        // Reset quantities to single empty input
        const qtyContainer = document.getElementById('quantity-inputs-container');
        if (qtyContainer) {
            const rows = qtyContainer.querySelectorAll('.quantity-row');
            for (let i = 1; i < rows.length; i++) rows[i].remove();
            const first = qtyContainer.querySelector('.quantity-input');
            if (first) first.value = '';
            updateAddQuantityButton();
        }
        if (widthInput) widthInput.value = '';
        if (heightInput) heightInput.value = '';
        setRadio('shape', 'rectangle');
        setRadio('corners', 'rounded');
        setRadio('application-method', 'hand');
        setRadio('unwind-direction', 'top-off-first');
        setRadio('artwork-option', 'upload-now');
        if (dieSizeModeSelect) {
            delete dieSizeModeSelect.dataset.forcedByCorners;
            delete dieSizeModeSelect.dataset.prevMode;
            dieSizeModeSelect.value = 'existing';
        }
        toggleCornersSection();
        toggleSizeSections();
        toggleLabelSizeInputsForMode();
        toggleCuttingDieSection();
        const matSel = document.getElementById('material');
        const finSel = document.getElementById('finish');
        const dieSel = document.getElementById('cutting-die');
        if (matSel) matSel.value = '';
        if (printingFilter) printingFilter.querySelectorAll('button').forEach(b => b.classList.remove('active'));
        if (finSel) {
            finSel.innerHTML = '<option value="">Please select a Printing option first</option>';
            finSel.disabled = true;
        }
        if (dieSel) {
            dieSel.innerHTML = '<option value="">Please select a Printing option first</option>';
            dieSel.disabled = true;
        }
        validateLabelSize();
        validateDiameter();
        updateSummaryPanel();
    }

    if (loadTestCaseSelect) {
        fetch('/data/test-cases.json')
            .then(r => r.ok ? r.json() : [])
            .then(arr => {
                if (!Array.isArray(arr)) return;
                arr.forEach(tc => {
                    const opt = document.createElement('option');
                    opt.value = tc.id || '';
                    opt.textContent = tc.name || tc.id || 'Unnamed';
                    loadTestCaseSelect.appendChild(opt);
                });
            })
            .catch(() => {});
        loadTestCaseSelect.addEventListener('change', function() {
            const id = this.value;
            if (!id) return;
            fetch('/data/test-cases.json')
                .then(r => r.ok ? r.json() : [])
                .then(arr => {
                    const tc = Array.isArray(arr) ? arr.find(t => t.id === id) : null;
                    if (tc) {
                        clearForm();
                        applyTestCase(tc);
                    }
                })
                .catch(() => {});
            this.value = '';
        });
    }
    if (clearFormBtn) clearFormBtn.addEventListener('click', clearForm);

});

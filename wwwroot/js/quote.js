document.addEventListener('DOMContentLoaded', function() {
    // Note: shapeInputs are now loaded dynamically, so we don't query them here
    const widthInput = document.getElementById('label-width');
    const heightInput = document.getElementById('label-height');
    const diameterInput = document.getElementById('diameter');
    const sizeValidation = document.getElementById('size-validation');
    const diameterValidation = document.getElementById('diameter-validation');
    const cornersSection = document.getElementById('corners-section');
    const labelSizeSection = document.getElementById('label-size-section');
    const diameterSection = document.getElementById('diameter-section');
    const materialSelect = document.getElementById('material');
    const materialLoading = document.getElementById('material-loading');
    const materialError = document.getElementById('material-error');
    const printingFilter = document.getElementById('printing-filter');
    const printingLoading = document.getElementById('printing-loading');
    const printingError = document.getElementById('printing-error');

    // Store materials data for filtering
    let allMaterialsData = null;

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
        const applicable = isCuttingDieApplicableForShape(selectedShape);

        if (section) {
            section.style.display = applicable ? 'block' : 'none';
        }

        const cuttingDieError = document.getElementById('cutting-die-error');
        if (cuttingDieError) cuttingDieError.style.display = 'none';

        if (!applicable) {
            // Clear and disable when not applicable.
            cuttingDieSelect.innerHTML = '<option value="">Cutting die not applicable for selected shape</option>';
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
            // Show loading state
            materialLoading.style.display = 'block';
            materialError.style.display = 'none';
            materialSelect.style.opacity = '0.5';
            materialSelect.disabled = false; // Materials load immediately, no dependency on Printing

            // Materials URL - no printing filter for now
            const url = '/Api/Materials';

            // Call server-side endpoint (credentials are handled server-side)
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                }
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load materials: ${response.status}`);
            }

            const data = await response.json();

            // Extract materials from response (handle different response structures)
            const materialsData = data.materials || data.data || data.items || data.results || data;
            allMaterialsData = materialsData; // Store for potential re-filtering
            populateMaterials(materialsData);

        } catch (error) {
            console.error('Error loading materials:', error);
            let errorMessage = 'Error loading materials. ';
            if (error.message && (error.message.includes('Failed to fetch') || error.message.includes('NetworkError'))) {
                errorMessage += 'Please check your network connection or API availability.';
            } else {
                errorMessage += (error.message || 'Please refresh the page to retry.');
            }
            materialError.textContent = errorMessage;
            materialError.style.display = 'block';
            materialSelect.innerHTML = '<option value="">Error loading materials. See error message below.</option>';
        } finally {
            materialLoading.style.display = 'none';
            materialSelect.style.opacity = '1';
            materialSelect.disabled = false;
        }
    }

    // Fetch Materials from API via server-side endpoint (no prerequisites)
    async function loadMaterials() {
        try {
            // Show loading state
            materialLoading.style.display = 'block';
            materialError.style.display = 'none';
            materialSelect.style.opacity = '0.5';
            materialSelect.disabled = false; // Materials load immediately, no dependency on Printing

            // Materials URL - no printing filter for now
            const url = '/Api/Materials';

            // Call server-side endpoint (credentials are handled server-side)
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                }
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load materials: ${response.status}`);
            }

            const data = await response.json();
            
            // Extract materials from response (handle different response structures)
            const materialsData = data.materials || data.data || data.items || data.results || data;
            allMaterialsData = materialsData; // Store for potential re-filtering
            populateMaterials(materialsData);

        } catch (error) {
            console.error('Error loading materials:', error);
            let errorMessage = 'Error loading materials. ';
            if (error.message && (error.message.includes('Failed to fetch') || error.message.includes('NetworkError'))) {
                errorMessage += 'Please check your network connection or API availability.';
            } else {
                errorMessage += (error.message || 'Please refresh the page to retry.');
            }
            materialError.textContent = errorMessage;
            materialError.style.display = 'block';
            materialSelect.innerHTML = '<option value="">Error loading materials. See error message below.</option>';
        } finally {
            materialLoading.style.display = 'none';
            materialSelect.style.opacity = '1';
            materialSelect.disabled = false;
        }
    }

    // Fetch Printing options (ColorCodes) from API via server-side endpoint
    async function loadPrintingOptions() {
        console.log('loadPrintingOptions called');
        try {
            console.log('Checking required elements...');
            if (!printingFilter || !printingLoading) {
                console.error('Required elements not found:', { printingFilter, printingLoading });
                return;
            }
            console.log('Required elements found, proceeding...');
            
            // Show loading state
            printingLoading.style.display = 'block';
            printingError.style.display = 'none';
            printingFilter.style.opacity = '0.5';
            printingFilter.style.pointerEvents = 'none';

            console.log('Fetching /Api/ColorCodes...');
            // Call server-side endpoint (credentials are handled server-side)
            const response = await fetch('/Api/ColorCodes', {
                method: 'GET',
                headers:
                 {
                    'Accept': 'application/json'
                }
            });
            
            console.log('Response received:', response.status, response.statusText, response.ok);

            if (!response.ok) {
                console.error('Response not OK, parsing error data...');
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                console.error('API error:', errorData);
                throw new Error(errorData.error || `Failed to load printing options: ${response.status}`);
            }

            console.log('Response OK, parsing JSON...');
            const data = await response.json();
            console.log('Data received from API:', data);
            console.log('About to call populatePrintingOptions with data type:', typeof data, 'isArray:', Array.isArray(data));
            console.log('Calling populatePrintingOptions now...');
            try {
                populatePrintingOptions(data);
                console.log('populatePrintingOptions call completed');
            } catch (populateError) {
                console.error('Error in populatePrintingOptions:', populateError);
                console.error('Error stack:', populateError.stack);
                throw populateError;
            }

        } catch (error) {
            console.error('Error loading printing options:', error);
            let errorMessage = 'Error loading printing options. ';
            if (error.message && (error.message.includes('Failed to fetch') || error.message.includes('NetworkError'))) {
                errorMessage += 'Please check your network connection or API availability.';
            } else {
                errorMessage += (error.message || 'Please refresh the page to retry.');
            }
            printingError.textContent = errorMessage;
            printingError.style.display = 'block';
            printingFilter.innerHTML = '<div class="text-muted">Error loading printing options. Please refresh the page to retry.</div>';
        } finally {
            printingLoading.style.display = 'none';
            printingFilter.style.opacity = '1';
            printingFilter.style.pointerEvents = 'auto';
        }
    }


    // Populate Printing options (ColorCodes) buttons
    function populatePrintingOptions(data) {
        console.log('populatePrintingOptions called with data:', data);
        if (!printingFilter) {
            console.error('Cannot populate printing options - printingFilter element not found');
            return;
        }
        
        // Clear existing buttons
        printingFilter.innerHTML = '';
        
        console.log('Raw API response:', data);
        
        // Handle different response structures
        let colorCodes = [];
        if (Array.isArray(data)) {
            colorCodes = data;
        } else if (data.Data && Array.isArray(data.Data)) {
            colorCodes = data.Data;
        } else if (data.items && Array.isArray(data.items)) {
            colorCodes = data.items;
        } else if (data.results && Array.isArray(data.results)) {
            colorCodes = data.results;
        } else {
            console.error('Unexpected data structure:', data);
            printingError.textContent = 'Unexpected response format from API';
            printingError.style.display = 'block';
            return;
        }

        console.log('Total printing options received:', colorCodes.length);
        console.log('Printing options data:', colorCodes);
        
        if (colorCodes.length === 0) {
            console.warn('No printing options found in API response');
            printingFilter.innerHTML = '<div class="text-muted">No printing options available.</div>';
            return;
        }

        // Extract printing options with deduplication by Id
        const printingMap = new Map();
        
        colorCodes.forEach((colorCode, index) => {
            console.log(`Processing color code ${index}:`, colorCode);
            
            // Handle both nested (ColourBacking) and flat structures
            // Some endpoints return {ColourBacking: {Id, Descriptions}}
            // Others return {Id, Descriptions} directly
            const colourBacking = colorCode.ColourBacking || colorCode.colourBacking;
            console.log(`  ColourBacking for item ${index}:`, colourBacking);
            
            // If ColourBacking is null, the data might be directly on the colorCode object
            const dataSource = colourBacking || colorCode;
            
            // Get the ID - handle both Id and id (case-insensitive)
            // Check both nested and flat structures
            // Also check for other possible property names
            let colorCodeId = null;
            if (colourBacking) {
                colorCodeId = colourBacking.Id || colourBacking.id || colourBacking.ID;
                console.log(`  ID from ColourBacking for item ${index}:`, colorCodeId);
            } else {
                // Try all possible ID property names
                colorCodeId = colorCode.Id || colorCode.id || colorCode.ID || 
                             colorCode.ColourCodeId || colorCode.colourCodeId ||
                             colorCode.ColorCodeId || colorCode.colorCodeId;
                console.log(`  ID from flat structure for item ${index}:`, colorCodeId);
                console.log(`  All properties on colorCode ${index}:`, Object.keys(colorCode));
            }
                
            if (!colorCodeId) {
                //console.warn(`Skipping printing option ${index} - missing Id. Full object:`, JSON.stringify(colorCode, null, 2));
                return; // Skip this option
            }
            
            // Skip if we've already seen this Id (deduplication)
            if (printingMap.has(colorCodeId)) {
                console.log('Skipping duplicate printing option Id:', colorCodeId);
                return;
            }
            
            // Get description - specifically look for en-US, fallback to first if not found
            let description = 'Unknown';
            
            // Handle both "Descriptions" and "Discriptions" (typo in API)
            // Handle both PascalCase and camelCase
            // Check both nested and flat structures
            const descriptionsArray = dataSource.Descriptions || dataSource.Discriptions || 
                                     dataSource.descriptions || dataSource.discriptions;
            if (descriptionsArray && Array.isArray(descriptionsArray) && descriptionsArray.length > 0) {
                // First, try to find exact match for "en-US"
                const enUSDesc = descriptionsArray.find(d => {
                    const langCode = d.ISOLanguageCode || d.isoLanguageCode || d.ISOLanguagecode;
                    return langCode === 'en-US' || langCode === 'en-us';
                });
                
                if (enUSDesc) {
                    description = enUSDesc.Description || enUSDesc.description || 'Unknown';
                } else {
                    // Fallback to first description if en-US not found
                    const firstDesc = descriptionsArray[0];
                    description = firstDesc.Description || firstDesc.description || 'Unknown';
                }
            }
            
            console.log('Adding printing option - Id:', colorCodeId, 'Description:', description);
            
            // Store in map using Id as key (value is description)
            // This ensures the ID from the API is used, not the description
            printingMap.set(colorCodeId, description);
        });

        // Sort by description text
        const sortedPrintingOptions = Array.from(printingMap.entries()).sort((a, b) => 
            a[1].localeCompare(b[1])
        );

        console.log('Unique printing options after deduplication:', sortedPrintingOptions.length);
        console.log('Printing options being displayed:', sortedPrintingOptions.map(([id, desc]) => `${id}: ${desc}`));
        
        if (sortedPrintingOptions.length === 0) {
            console.warn('No printing options after processing');
            printingFilter.innerHTML = '<div class="text-muted">No printing options available after processing.</div>';
            return;
        }

        // Create buttons for each unique printing option
        sortedPrintingOptions.forEach(([id, description]) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'filter-btn';
            button.textContent = description;
            button.setAttribute('data-printing-id', id);
            button.setAttribute('data-printing-text', description);
            
            // Add click handler - this triggers Materials loading
            button.addEventListener('click', function(e) {
                e.preventDefault();
                // Remove active from all buttons in this filter group
                printingFilter.querySelectorAll('button').forEach(b => b.classList.remove('active'));
                // Add active to clicked button
                this.classList.add('active');
                
                // Get selected printing (both Id and display text)
                const selectedPrintingText = this.getAttribute('data-printing-text') || this.textContent.trim();
                const selectedPrintingId = this.getAttribute('data-printing-id') || '';
                
                // Load cutting die options based on printing selection
                // Prefer passing the Id (e.g. "2F") so the server can reliably determine materie_
                const currentShape = document.querySelector('input[name="shape"]:checked')?.value;
                if (isCuttingDieApplicableForShape(currentShape) && (selectedPrintingId || selectedPrintingText)) {
                    loadCuttingDieOptions(selectedPrintingId || selectedPrintingText);
                } else {
                    // Clear cutting die if no printing selected
                    const cuttingDieSelect = document.getElementById('cutting-die');
                    if (cuttingDieSelect) {
                        cuttingDieSelect.innerHTML = isCuttingDieApplicableForShape(currentShape)
                            ? '<option value="">Please select a Printing option first</option>'
                            : '<option value="">Cutting die not applicable for selected shape</option>';
                        cuttingDieSelect.disabled = true;
                    }
                }
                
                // Load Finishing options based on Printing selection
                if (selectedPrintingText) {
                    // finish.js exposes a global loadFinishingOptions(printingText)
                    loadFinishingOptions(selectedPrintingText);
                } else {
                    // Clear finish if no printing selected
                    const finishSelect = document.getElementById('finish');
                    if (finishSelect) {
                        finishSelect.innerHTML = '<option value="">Please select a Printing option first</option>';
                        finishSelect.disabled = true;
                    }
                }
                
                updateSummaryPanel();
            });
            
            printingFilter.appendChild(button);
        });

        // Restore saved printing value if available
        const savedDataScript = document.getElementById('saved-quote-data');
        if (savedDataScript) {
            try {
                const savedData = JSON.parse(savedDataScript.textContent);
                const savedPrintingValue = savedData.printingValue || savedData.printing;
                if (savedPrintingValue) {
                    // Try to find button by Id first (if saved as Id)
                    let buttonToActivate = printingFilter.querySelector(`button[data-printing-id="${savedPrintingValue}"]`);
                    // If not found by Id, try by text
                    if (!buttonToActivate) {
                        buttonToActivate = Array.from(printingFilter.querySelectorAll('button')).find(btn => 
                            btn.textContent.trim() === savedPrintingValue || 
                            btn.getAttribute('data-printing-text') === savedPrintingValue
                        );
                    }
                    if (buttonToActivate) {
                        buttonToActivate.classList.add('active');
                        // Trigger materials load if printing is restored
                        const selectedPrintingText = buttonToActivate.getAttribute('data-printing-text') || buttonToActivate.textContent.trim();
                        const selectedPrintingId = buttonToActivate.getAttribute('data-printing-id') || '';
                        if (selectedPrintingId || selectedPrintingText) {
                            const currentShape = document.querySelector('input[name="shape"]:checked')?.value;
                            if (isCuttingDieApplicableForShape(currentShape)) {
                                loadCuttingDieOptions(selectedPrintingId || selectedPrintingText);
                            }
                            if (selectedPrintingText) {
                                loadFinishingOptions(selectedPrintingText);
                            }
                        }
                    }
                }
            } catch (e) {
                console.error('Error restoring printing option:', e);
            }
        }

        // Update summary panel
        updateSummaryPanel();
    }

    // Populate Materials dropdown
    function populateMaterials(data) {
        // Clear existing options
        materialSelect.innerHTML = '';
        
        console.log('populateMaterials called with data:', data);
        console.log('populateMaterials data type:', typeof data, 'isArray:', Array.isArray(data));
        
        // Handle different response structures
        let materials = [];
        if (Array.isArray(data)) {
            materials = data;
        } else if (data.Data && Array.isArray(data.Data)) {
            materials = data.Data;
        } else if (data.items && Array.isArray(data.items)) {
            materials = data.items;
        } else if (data.results && Array.isArray(data.results)) {
            materials = data.results;
        }

        console.log('Materials array after parsing:', materials.length);
        if (materials.length > 0) {
            console.log('First material structure:', materials[0]);
            console.log('First material keys:', Object.keys(materials[0]));
        }

        if (materials.length === 0) {
            materialSelect.innerHTML = '<option value="">No materials available</option>';
            console.warn('No materials found in response');
            return;
        }

        let addedCount = 0;
        let skippedCount = 0;
        
        // Populate dropdown
        materials.forEach((material, index) => {
            console.log(`Processing material ${index}:`, material);
            
            // Get Id from root level (flat structure)
            const materialId = material.Id || material.id;
            if (!materialId) {
                console.warn(`  Skipping material ${index} - no Id found. Material keys:`, Object.keys(material));
                skippedCount++;
                return;
            }
            
            console.log(`  Material ID found: ${materialId}`);
            
            const option = document.createElement('option');
            
            // Use Id as the form value
            option.value = materialId;
            
            // Get description - specifically look for en-US, fallback to first if not found
            let description = 'Unknown';
            
            // Get Descriptions array from root level (flat structure)
            const descriptionsArray = material.Descriptions || material.descriptions;
            console.log(`  Descriptions array:`, descriptionsArray);
            
            if (descriptionsArray && Array.isArray(descriptionsArray) && descriptionsArray.length > 0) {
                console.log(`  Found ${descriptionsArray.length} descriptions`);
                // First, try to find exact match for "en-US"
                const enUSDesc = descriptionsArray.find(d => {
                    const langCode = d.ISOLanguageCode || d.isoLanguageCode || d.ISOLanguagecode;
                    return langCode === 'en-US' || langCode === 'en-us';
                });
                
                if (enUSDesc) {
                    description = enUSDesc.Description || enUSDesc.description || 'Unknown';
                    console.log(`  Using en-US description: ${description}`);
                } else {
                    // Fallback to first description if en-US not found
                    const firstDesc = descriptionsArray[0];
                    description = firstDesc.Description || firstDesc.description || 'Unknown';
                    console.log(`  Using first description (${firstDesc.ISOLanguageCode || 'unknown lang'}): ${description}`);
                }
            } else {
                console.warn(`  No descriptions found for material ${index}`);
            }
            
            option.textContent = description;
            materialSelect.appendChild(option);
            addedCount++;
        });
        
        console.log(`Materials population complete: ${addedCount} added, ${skippedCount} skipped`);
        
        if (addedCount === 0) {
            materialSelect.innerHTML = '<option value="">No valid materials found</option>';
        }

        // Restore saved material value if available (after options are populated)
        const savedMaterialValue = materialSelect.getAttribute('data-saved-value');
        if (savedMaterialValue) {
            // Try to find and select the option with this value
            const optionToSelect = Array.from(materialSelect.options).find(opt => opt.value === savedMaterialValue);
            if (optionToSelect) {
                materialSelect.value = savedMaterialValue;
            }
        }

        // Update summary panel if material was already selected
        updateSummaryPanel();
    }

    // Fetch Cutting Die options (depends on Printing selection)
    async function loadCuttingDieOptions(printing) {
        const cuttingDieSelect = document.getElementById('cutting-die');
        const cuttingDieLoading = document.getElementById('cutting-die-loading');
        const cuttingDieError = document.getElementById('cutting-die-error');
        
        if (!cuttingDieSelect || !cuttingDieLoading || !cuttingDieError) {
            console.error('Cutting die elements not found');
            return;
        }
        
        try {
            // Show loading state
            cuttingDieLoading.style.display = 'block';
            cuttingDieError.style.display = 'none';
            cuttingDieSelect.style.opacity = '0.5';
            cuttingDieSelect.disabled = true;
            cuttingDieSelect.innerHTML = '<option value="">Loading cutting die options...</option>';
            
            // Call server-side endpoint with printing parameter
            const url = `/Api/CuttingDie?printing=${encodeURIComponent(printing)}`;
            
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                }
            });
            
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Unknown error occurred' }));
                throw new Error(errorData.error || `Failed to load cutting die options: ${response.status}`);
            }
            
            const data = await response.json();
            
            // Extract cutting die options from response
            const cuttingDieOptions = data.cuttingDieOptions || data.data || data.items || data.results || [];
            
            // Clear and populate dropdown
            cuttingDieSelect.innerHTML = '';
            
            if (cuttingDieOptions.length === 0) {
                cuttingDieSelect.innerHTML = '<option value="">No cutting die options available</option>';
            } else {
                // Populate dropdown with options
                // Note: ASP.NET Core JSON serialization defaults to camelCase
                cuttingDieOptions.forEach(option => {
                    const optionElement = document.createElement('option');
                    // Get the raw reference (handle possible naming variations)
                    const optionValue = option.stns_ref || option.stnsRef || '';

                    // Use cleanedRef as the option value so "QQ-R-" is removed
                    optionElement.value = optionValue;

                    // Human-readable text for the option
                    optionElement.textContent = option.stnsOms || option.StnsOms || option.stns_oms || 'Unknown';

                    // Remove leading "QQ-R-" prefix if present
                    const cleanedRef = optionElement.textContent.replace(/^QQ-R-/, '');

                    // Preserve the original raw reference for server-side needs
                    if (optionValue) {
                        optionElement.dataset.rawRef = cleanedRef;
                        optionElement.text = cleanedRef;
                    }

                    cuttingDieSelect.appendChild(optionElement);
                });
            }
            
            // Restore saved value if available
            const savedDataScript = document.getElementById('saved-quote-data');
            if (savedDataScript) {
                try {
                    const savedData = JSON.parse(savedDataScript.textContent);
                    const savedCuttingDieValue = savedData.cuttingDieValue || savedData.cuttingDie;
                    if (savedCuttingDieValue) {
                        const optionToSelect = Array.from(cuttingDieSelect.options).find(opt => 
                            opt.value === savedCuttingDieValue || opt.text === savedCuttingDieValue
                        );
                        if (optionToSelect) {
                            cuttingDieSelect.value = optionToSelect.value;
                        }
                    }
                } catch (e) {
                    console.error('Error restoring cutting die:', e);
                }
            }
            
        } catch (error) {
            console.error('Error loading cutting die options:', error);
            let errorMessage = 'Error loading cutting die options. ';
            if (error.message && (error.message.includes('Failed to fetch') || error.message.includes('NetworkError'))) {
                errorMessage += 'Please check your network connection or database availability.';
            } else {
                errorMessage += (error.message || 'Please try again.');
            }
            cuttingDieError.textContent = errorMessage;
            cuttingDieError.style.display = 'block';
            cuttingDieSelect.innerHTML = '<option value="">Error loading cutting die options</option>';
        } finally {
            cuttingDieLoading.style.display = 'none';
            cuttingDieSelect.style.opacity = '1';
            cuttingDieSelect.disabled = false;
        }
    }

    // Function to filter Finishing options based on Printing selection
    function filterFinishingOptions(printingText) {
        const finishSelect = document.getElementById('finish');
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

    // Function to validate email
    function validateEmail() {
        const emailInput = document.getElementById('contact-email');
        const emailValidation = document.getElementById('email-validation');
        const email = emailInput.value.trim();
        
        if (!email) {
            emailValidation.style.display = 'none';
            emailInput.classList.remove('is-invalid');
            return true; // Email is optional
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

    // Function to validate phone
    function validatePhone() {
        const phoneInput = document.getElementById('contact-phone');
        const phoneValidation = document.getElementById('phone-validation');
        const phone = phoneInput.value.trim();
        
        if (!phone) {
            phoneValidation.style.display = 'none';
            phoneInput.classList.remove('is-invalid');
            return true; // Phone is optional
        }
        
        // Remove common phone formatting characters for validation
        const cleanedPhone = phone.replace(/[\s\-\(\)\.]/g, '');
        
        // Validate: should contain only digits and optionally start with +
        const phoneRegex = /^\+?\d{10,15}$/;
        
        if (!phoneRegex.test(cleanedPhone)) {
            phoneValidation.textContent = 'Please enter a valid phone number (10-15 digits).';
            phoneValidation.style.display = 'block';
            phoneInput.classList.add('is-invalid');
            return false;
        }
        
        phoneValidation.style.display = 'none';
        phoneInput.classList.remove('is-invalid');
        return true;
    }

    // Function to validate name (optional, but if provided should be reasonable)
    function validateName() {
        const nameInput = document.getElementById('contact-name');
        const nameValidation = document.getElementById('name-validation');
        const name = nameInput.value.trim();
        
        if (!name) {
            nameValidation.style.display = 'none';
            nameInput.classList.remove('is-invalid');
            return true; // Name is optional
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

    // Function to format number without trailing zeros
    function formatNumber(value) {
        if (!value || isNaN(value)) return '';
        const num = parseFloat(value);
        // Remove trailing zeros
        return num.toString().replace(/\.0+$/, '').replace(/(\d+\.\d*?)0+$/, '$1');
    }

    // Function to round to nearest 1/32"
    function roundToNearest32nd(value) {
        if (!value || isNaN(value)) return '';
        const num = parseFloat(value);
        return formatNumber(Math.round(num * 32) / 32);
    }

    // Function to round to nearest hundredth
    function roundToNearestHundredth(value) {
        if (!value || isNaN(value)) return '';
        const num = parseFloat(value);
        return formatNumber(Math.round(num * 100) / 100);
    }

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

    // Validate input is numeric (float or int) - show error if invalid but don't block typing
    function validateNumericInput(input) {
        const value = input.value;
        if (value === '') return true;
        // Allow: numbers, decimals, negative sign at start
        const regex = /^-?\d*\.?\d*$/;
        return regex.test(value);
    }

    // Function to format text for display
    function formatLabel(text) {
        return text.split(/-|_/).map(word => 
            word.charAt(0).toUpperCase() + word.slice(1)
        ).join(' ');
    }

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

        // Versions and Total Quantity
        const versions = document.getElementById('versions')?.value;
        const totalQuantity = document.getElementById('total-quantity')?.value;
        if (versions || totalQuantity) {
            let qtyText = '';
            if (versions) qtyText += `${versions} version${versions != 1 ? 's' : ''}`;
            if (versions && totalQuantity) qtyText += ', ';
            if (totalQuantity) qtyText += `${totalQuantity} total labels`;
            if (qtyText) {
                choices.push({ label: 'Quantity', value: qtyText });
            }
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

    // Event listeners
    // Shape input event listeners
    document.querySelectorAll('input[name="shape"]').forEach(input => {
        input.addEventListener('change', function() {
            toggleCornersSection();
            toggleSizeSections();
            toggleCuttingDieSection();
            validateLabelSize();
            validateDiameter();
            updateSummaryPanel();
        });
    });

    // Width and Height input handlers
    if (widthInput) {
        widthInput.addEventListener('input', function () {
            if (validateNumericInput(this)) {
                validateLabelSize();
                updateSummaryPanel();
            }
        });
    }
    widthInput.addEventListener('blur', function() { 
        handleSizeInputBlur(this);
        updateSummaryPanel();
    });
    
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

    // Diameter input handlers
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

    // Corners change handler
    document.querySelectorAll('input[name="corners"]').forEach(input => {
        input.addEventListener('change', updateSummaryPanel);
    });

    // Cutting Die change handler:
    // - updates summary
    // - auto-populates label size when option contains "W x H" (e.g. "4.00X3.00")
    document.getElementById('cutting-die')?.addEventListener('change', function() {
        applyCuttingDieToLabelSize();
        updateSummaryPanel();
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
    document.getElementById('description')?.addEventListener('input', updateSummaryPanel);
    document.getElementById('description')?.addEventListener('change', updateSummaryPanel);

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
                if (isCuttingDieApplicableForShape(currentShape)) {
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
            
            // Restore description
            if (savedData.description) {
                document.getElementById('description').value = savedData.description;
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
            const currentShape = document.querySelector('input[name="shape"]:checked')?.value;
            if (isCuttingDieApplicableForShape(currentShape)) {
                if (!selectedCuttingDie) {
                    alert('Please select a cutting die option.');
                    return;
                }
            }

            // Validate finish selection
            const finishSelect = document.getElementById('finish');
            const selectedFinish = finishSelect?.value;
            if (!selectedFinish) {
                alert('Please select a finish option.');
                return;
            }

            // Validate total quantity
            const totalQuantity = document.getElementById('total-quantity')?.value.trim();
            if (!totalQuantity || isNaN(parseInt(totalQuantity)) || parseInt(totalQuantity) < 1) {
                alert('Please enter a valid total quantity (at least 1).');
                document.getElementById('total-quantity')?.focus();
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
            addHiddenField('totalQuantity', totalQuantity);
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

});

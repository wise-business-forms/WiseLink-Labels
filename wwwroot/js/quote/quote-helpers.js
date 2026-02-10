// quote-helpers.js
// Utility functions for formatting, rounding, and numeric validation

class QuoteHelpers {
    // Format number without trailing zeros
    static formatNumber(value) {
        if (!value || isNaN(value)) return '';
        const num = parseFloat(value);
        return num.toString().replace(/\.0+$/, '').replace(/(\d+\.\d*?)0+$/, '$1');
    }

    // Round to nearest 1/32"
    static roundToNearest32nd(value) {
        if (!value || isNaN(value)) return '';
        const num = parseFloat(value);
        return QuoteHelpers.formatNumber(Math.round(num * 32) / 32);
    }

    // Round to nearest hundredth
    static roundToNearestHundredth(value) {
        if (!value || isNaN(value)) return '';
        const num = parseFloat(value);
        return QuoteHelpers.formatNumber(Math.round(num * 100) / 100);
    }

    // Validate input is numeric (float or int)
    static validateNumericInput(input) {
        const value = input.value;
        if (value === '') return true;
        // Allow: numbers, decimals, negative sign at start
        const regex = /^-?\d*\.?\d*$/;
        return regex.test(value);
    }

    // Format text for display (e.g., "rectangle_shape" => "Rectangle Shape")
    static formatLabel(text) {
        return text.split(/-|_/).map(word => 
            word.charAt(0).toUpperCase() + word.slice(1)
        ).join(' ');
    }
}

// Attach to window for global access if not using modules
window.QuoteHelpers = QuoteHelpers;
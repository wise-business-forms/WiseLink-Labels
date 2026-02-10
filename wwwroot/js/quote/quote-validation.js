// quote-validation.js
// Validation functions for quote form fields

class QuoteValidation {
    static validateEmail() {
        const emailInput = document.getElementById('contact-email');
        const emailValidation = document.getElementById('email-validation');
        const email = emailInput.value.trim();
        if (!email) {
            emailValidation.textContent = 'Email is required.';
            emailValidation.style.display = 'block';
            emailInput.classList.add('is-invalid');
            return false;
        }
        const atSymbol = String.fromCharCode(64);
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

    static validatePhone() {
        const phoneInput = document.getElementById('contact-phone');
        const phoneValidation = document.getElementById('phone-validation');
        const phone = phoneInput.value.trim();
        if (!phone) {
            phoneValidation.textContent = 'Phone number is required.';
            phoneValidation.style.display = 'block';
            phoneInput.classList.add('is-invalid');
            return false;
        }
        const cleanedPhone = phone.replace(/[\s\-\(\)\.]/g, '');
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

    static validateName() {
        const nameInput = document.getElementById('contact-name');
        const nameValidation = document.getElementById('name-validation');
        const name = nameInput.value.trim();
        if (!name) {
            nameValidation.textContent = 'Name is required.';
            nameValidation.style.display = 'block';
            nameInput.classList.add('is-invalid');
            return false;
        }
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

    static validateDiameter(diameterInput, diameterValidation) {
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
        const minDiameter = 0.5;
        const maxDiameter = 12.375;
        if (diameter < minDiameter || diameter > maxDiameter) {
            diameterValidation.textContent = 'Diameter must be between 0.5" and 12.375".';
            diameterValidation.className = 'size-validation-message error';
            diameterValidation.style.display = 'block';
            return;
        }
        diameterValidation.style.display = 'none';
    }

    static validateLabelSize(widthInput, heightInput, sizeValidation) {
        const selectedShape = document.querySelector('input[name="shape"]:checked').value;
        if (selectedShape === 'circle' || selectedShape === 'oval') {
            sizeValidation.style.display = 'none';
            return;
        }
        if (!widthInput || !heightInput || !widthInput.value || !heightInput.value) {
            sizeValidation.style.display = 'none';
            return;
        }
        const width = parseFloat(widthInput.value);
        const height = parseFloat(heightInput.value);
        if (isNaN(width) || isNaN(height)) {
            sizeValidation.textContent = 'Please enter valid numbers for width and height.';
            sizeValidation.className = 'size-validation-message error';
            sizeValidation.style.display = 'block';
            return;
        }
        const minWidth = 0.5;
        const maxWidth = 24.5;
        const minHeight = 0.5;
        const maxHeight = 12.375;
        if (width < minWidth || width > maxWidth || height < minHeight || height > maxHeight) {
            sizeValidation.textContent = 'Labels must normally be between 0.5" and 24.5" horizontally and 0.5" and 12.375" vertically.';
            sizeValidation.className = 'size-validation-message error';
            sizeValidation.style.display = 'block';
            return;
        }
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
        const aspectRatio = width / height;
        if (aspectRatio > 10 || aspectRatio < 0.1) {
            sizeValidation.textContent = 'Are you sure you\'ve entered the right size? Please double-check your label width and height before proceeding.';
            sizeValidation.className = 'size-validation-message warning';
            sizeValidation.style.display = 'block';
            return;
        }
        sizeValidation.textContent = 'Save and use an existing die size.';
        sizeValidation.className = 'size-validation-message warning';
        sizeValidation.style.display = 'block';
    }
}

window.QuoteValidation = QuoteValidation;
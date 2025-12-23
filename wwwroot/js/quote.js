
document.addEventListener('DOMContentLoaded', function () {
    const shapeInputs = document.querySelectorAll('input[name="shape"]');
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

    console.log(printingFilter);
}); // document.addEventListener
// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

function initGuidePreviewTriggers() {
    const previewTriggers = document.querySelectorAll('.guide-preview-trigger[data-guide-image]');
    if (!previewTriggers.length) {
        return;
    }

    const preview = document.createElement('div');
    preview.className = 'guide-preview-floating';
    const previewImage = document.createElement('img');
    preview.appendChild(previewImage);
    document.body.appendChild(preview);

    const showPreview = trigger => {
        const imageUrl = trigger.getAttribute('data-guide-image');
        if (!imageUrl) {
            return;
        }

        const altText = trigger.getAttribute('data-guide-alt') ?? '';
        previewImage.src = imageUrl;
        previewImage.alt = altText;

        const rect = trigger.getBoundingClientRect();
        const top = window.scrollY + rect.bottom + 8;
        const left = window.scrollX + rect.left;

        preview.style.top = `${top}px`;
        preview.style.left = `${left}px`;
        preview.classList.add('is-visible');
    };

    const hidePreview = () => {
        preview.classList.remove('is-visible');
    };

    previewTriggers.forEach(trigger => {
        trigger.setAttribute('aria-haspopup', 'true');
        trigger.setAttribute('aria-expanded', 'false');

        const handleShow = () => {
            showPreview(trigger);
            trigger.setAttribute('aria-expanded', 'true');
        };

        const handleHide = () => {
            hidePreview();
            trigger.setAttribute('aria-expanded', 'false');
        };

        trigger.addEventListener('mouseenter', handleShow);
        trigger.addEventListener('focus', handleShow);
        trigger.addEventListener('mouseleave', handleHide);
        trigger.addEventListener('blur', handleHide);
    });
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initGuidePreviewTriggers);
} else {
    initGuidePreviewTriggers();
}

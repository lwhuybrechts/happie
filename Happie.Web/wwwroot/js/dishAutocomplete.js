window.happie = window.happie || {};

window.happie.getCursorAtEnd = (inputElement) => {
    return inputElement.selectionStart === inputElement.value.length
        && inputElement.selectionEnd === inputElement.value.length;
};

window.happie.setCursorPosition = (inputElement, position) => {
    // Use requestAnimationFrame to ensure the DOM value is updated before scrolling.
    requestAnimationFrame(() => {
        inputElement.setSelectionRange(position, position);
        inputElement.scrollLeft = inputElement.scrollWidth;
    });
};

window.happie.syncGhostScroll = (inputElement) => {
    // Sync the ghost text overlay with the input's scroll position,
    // and nudge the input scroll forward to reveal ghost text at the right edge.
    const container = inputElement.closest('[class*="input-container"]') || inputElement.parentElement;
    const ghostText = container.querySelector('.dish-panel__ghost-text');
    if (!ghostText) return;

    const inputWidth = inputElement.clientWidth;
    const inputPadding = 32; // 16px left + 16px right padding.
    const availableWidth = inputWidth - inputPadding;

    // Measure how wide the typed text is using a temporary canvas.
    const style = window.getComputedStyle(inputElement);
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');
    ctx.font = style.font;
    const typedWidth = ctx.measureText(inputElement.value).width;

    // If typed text overflows, scroll forward so the end of text + ghost text peek is visible.
    if (typedWidth > availableWidth) {
        // Scroll so that ~60px of space remains at the right for the suggestion.
        const peekAmount = 60;
        inputElement.scrollLeft = typedWidth - availableWidth + peekAmount;
    }

    // Offset the ghost text overlay to match the input's scroll position.
    ghostText.style.transform = `translateX(-${inputElement.scrollLeft}px)`;
};

window.happie.enableTabPrevention = (inputElement) => {
    if (inputElement._happieTabHandler) return;
    const container = inputElement.closest('[class*="input-container"]') || inputElement.parentElement;
    inputElement._happieTabHandler = (e) => {
        if (e.key === 'Tab' && container.querySelector('.dish-panel__ghost-text-suggestion')) {
            e.preventDefault();
        }
    };
    inputElement.addEventListener('keydown', inputElement._happieTabHandler);
};

window.happie.disableTabPrevention = (inputElement) => {
    if (inputElement._happieTabHandler) {
        inputElement.removeEventListener('keydown', inputElement._happieTabHandler);
        delete inputElement._happieTabHandler;
    }
};

// Swipe carousel handler for DayPlanPage three-panel carousel.
// Translates the .swipe-carousel wrapper during drag; all three panels move in unison.
(function () {
    'use strict';

    window.happie = window.happie || {};

    // Constants.
    const SWIPE_THRESHOLD = 60;
    const DEAD_ZONE = 10;
    const EDGE_EXCLUSION = 20;
    const MAX_OVERSHOOT_FACTOR = 1.2;
    const ANIMATION_DURATION = 300;

    // Registry for cleanup.
    const _carouselRegistry = new Map();

    // Rubber-band physics: linear up to viewport width, then diminishing returns.
    function rubberBand(dragDistance, viewportWidth) {
        var absolute = Math.abs(dragDistance);
        if (absolute <= viewportWidth)
            return dragDistance;

        var over = absolute - viewportWidth;
        var maxOvershoot = viewportWidth * (MAX_OVERSHOOT_FACTOR - 1.0);
        var dampened = viewportWidth + maxOvershoot * (1 - Math.exp(-over / viewportWidth));
        return dragDistance > 0 ? dampened : -dampened;
    }

    // Check if the touch target is an excluded input element.
    function isExcludedTarget(target) {
        return target.closest('input, textarea, select, [contenteditable="true"], [role="dialog"]');
    }

    // Check if clientX is in the edge exclusion zone.
    function isInEdgeExclusionZone(clientX, viewportWidth) {
        return clientX <= EDGE_EXCLUSION || clientX >= viewportWidth - EDGE_EXCLUSION;
    }

    happie.registerSwipeCarousel = function (element, dotNetRef) {
        if (!element) return;

        // The viewport is the parent of the carousel track element.
        var viewport = element.parentElement;

        var state = {
            startX: 0,
            startY: 0,
            currentX: 0,
            tracking: false,
            directionLocked: false,
            isHorizontal: false,
            animating: false,
            snapBackAnimationId: null
        };

        var rafId = null;
        var pendingTranslateX = null;

        // Apply translateX via requestAnimationFrame for 60fps.
        function scheduleTranslate(translateValue) {
            pendingTranslateX = translateValue;
            if (rafId === null) {
                rafId = requestAnimationFrame(function () {
                    if (pendingTranslateX !== null)
                        element.style.transform = 'translateX(' + pendingTranslateX + 'px)';
                    rafId = null;
                });
            }
        }

        // Cancel any pending rAF.
        function cancelScheduledTranslate() {
            if (rafId !== null) {
                cancelAnimationFrame(rafId);
                rafId = null;
            }
            pendingTranslateX = null;
        }

        // Cancel snap-back animation in progress.
        function cancelSnapBack() {
            if (state.snapBackAnimationId !== null) {
                cancelAnimationFrame(state.snapBackAnimationId);
                state.snapBackAnimationId = null;
                // Remove transition so we can resume from current position.
                element.style.transition = 'none';
            }
        }

        // Get the current translateX from the element's computed transform matrix.
        function getCurrentTranslateX() {
            var style = window.getComputedStyle(element);
            var matrix = style.transform;
            if (!matrix || matrix === 'none')
                return 0;
            // matrix(a, b, c, d, tx, ty) — tx is the translateX value.
            var values = matrix.match(/matrix\((.+)\)/);
            if (values) {
                var parts = values[1].split(',');
                return parseFloat(parts[4]) || 0;
            }
            return 0;
        }

        // Snap-back animation using requestAnimationFrame.
        function animateSnapBack(fromX) {
            var startTime = null;
            var startX = fromX;

            function step(timestamp) {
                if (startTime === null)
                    startTime = timestamp;

                var elapsed = timestamp - startTime;
                var progress = Math.min(elapsed / ANIMATION_DURATION, 1);
                // Ease-out cubic for smooth deceleration.
                var eased = 1 - Math.pow(1 - progress, 3);
                var currentValue = startX * (1 - eased);

                element.style.transform = 'translateX(' + currentValue + 'px)';

                if (progress < 1) {
                    state.snapBackAnimationId = requestAnimationFrame(step);
                } else {
                    // Animation complete.
                    element.style.transform = '';
                    element.style.willChange = '';
                    viewport.style.minHeight = '';
                    state.snapBackAnimationId = null;
                }
            }

            state.snapBackAnimationId = requestAnimationFrame(step);
        }

        // Completion animation (slide full viewport width).
        function animateCompletion(direction) {
            state.animating = true;
            var targetX = direction * window.innerWidth;
            var startX = getCurrentTranslateX();
            var startTime = null;

            function step(timestamp) {
                if (startTime === null)
                    startTime = timestamp;

                var elapsed = timestamp - startTime;
                var progress = Math.min(elapsed / ANIMATION_DURATION, 1);
                // Ease-out cubic.
                var eased = 1 - Math.pow(1 - progress, 3);
                var currentValue = startX + (targetX - startX) * eased;

                element.style.transform = 'translateX(' + currentValue + 'px)';

                if (progress < 1) {
                    requestAnimationFrame(step);
                } else {
                    // Animation complete — call .NET and reset.
                    element.style.transform = '';
                    element.style.willChange = '';
                    viewport.style.minHeight = '';

                    if (direction < 0)
                        dotNetRef.invokeMethodAsync('SwipeLeftAsync').then(function () { state.animating = false; });
                    else
                        dotNetRef.invokeMethodAsync('SwipeRightAsync').then(function () { state.animating = false; });
                }
            }

            requestAnimationFrame(step);
        }

        var onTouchStart = function (e) {
            // Block new gestures during completion animation.
            if (state.animating) return;

            var touch = e.touches[0];
            var clientX = touch.clientX;
            var viewportWidth = window.innerWidth;

            // Edge exclusion zone check.
            if (isInEdgeExclusionZone(clientX, viewportWidth)) return;

            // Input element exclusion.
            if (isExcludedTarget(e.target)) return;

            // If snap-back is in progress, cancel it and resume from current position.
            if (state.snapBackAnimationId !== null) {
                var currentPos = getCurrentTranslateX();
                cancelSnapBack();
                state.currentX = currentPos;
            } else {
                state.currentX = 0;
            }

            state.startX = clientX;
            state.startY = touch.clientY;
            state.tracking = true;
            state.directionLocked = false;
            state.isHorizontal = false;

            element.style.transition = 'none';
            element.style.willChange = 'transform';
        };

        var onTouchMove = function (e) {
            if (!state.tracking || state.animating) return;

            var touch = e.touches[0];
            var deltaX = touch.clientX - state.startX;
            var deltaY = touch.clientY - state.startY;

            // Direction lock: first significant movement (>10px on any axis) determines direction.
            if (!state.directionLocked) {
                if (Math.abs(deltaX) < DEAD_ZONE && Math.abs(deltaY) < DEAD_ZONE)
                    return;

                state.directionLocked = true;

                if (Math.abs(deltaX) >= Math.abs(deltaY)) {
                    state.isHorizontal = true;
                    // Expand viewport to fit the tallest panel so adjacent content isn't clipped.
                    var activePanel = element.querySelector('.swipe-carousel__panel--active');
                    var prevPanel = element.querySelector('.swipe-carousel__panel--prev');
                    var nextPanel = element.querySelector('.swipe-carousel__panel--next');
                    var maxHeight = activePanel ? activePanel.scrollHeight : 0;
                    if (prevPanel) maxHeight = Math.max(maxHeight, prevPanel.scrollHeight);
                    if (nextPanel) maxHeight = Math.max(maxHeight, nextPanel.scrollHeight);
                    if (maxHeight > 0)
                        viewport.style.minHeight = maxHeight + 'px';
                } else {
                    // Vertical — abort tracking, allow normal scroll.
                    state.isHorizontal = false;
                    state.tracking = false;
                    element.style.willChange = '';
                    element.style.transform = '';
                    return;
                }
            }

            if (!state.isHorizontal) return;

            // Prevent vertical scrolling while horizontal swipe is active.
            e.preventDefault();

            // Accumulate drag including any offset from interrupted snap-back.
            var totalDrag = deltaX + state.currentX;
            var viewportWidth = window.innerWidth;
            var translated = rubberBand(totalDrag, viewportWidth);

            scheduleTranslate(translated);
        };

        var onTouchEnd = function () {
            if (!state.tracking || !state.directionLocked || !state.isHorizontal) {
                state.tracking = false;
                if (!state.animating) {
                    element.style.willChange = '';
                    viewport.style.minHeight = '';
                }
                return;
            }
            state.tracking = false;
            cancelScheduledTranslate();

            var currentTranslateX = getCurrentTranslateX();
            var absDelta = Math.abs(currentTranslateX);

            if (absDelta >= SWIPE_THRESHOLD) {
                // Threshold met — completion animation.
                var direction = currentTranslateX > 0 ? 1 : -1;
                animateCompletion(direction);
            } else {
                // Below threshold — snap-back.
                animateSnapBack(currentTranslateX);
            }
        };

        var onTouchCancel = function () {
            // Treat same as touchend below threshold (snap back).
            if (!state.tracking) return;
            state.tracking = false;
            cancelScheduledTranslate();

            var currentTranslateX = getCurrentTranslateX();
            if (currentTranslateX !== 0)
                animateSnapBack(currentTranslateX);
            else {
                element.style.willChange = '';
                viewport.style.minHeight = '';
            }
        };

        element.addEventListener('touchstart', onTouchStart, { passive: true });
        element.addEventListener('touchmove', onTouchMove, { passive: false });
        element.addEventListener('touchend', onTouchEnd, { passive: true });
        element.addEventListener('touchcancel', onTouchCancel, { passive: true });

        _carouselRegistry.set(element, {
            onTouchStart, onTouchMove, onTouchEnd, onTouchCancel,
            dotNetRef, state, cancelScheduledTranslate
        });
    };

    happie.disposeSwipeCarousel = function (element) {
        if (!element) return;
        var entry = _carouselRegistry.get(element);
        if (!entry) return;

        // Cancel any in-progress snap-back animation.
        if (entry.state.snapBackAnimationId !== null) {
            cancelAnimationFrame(entry.state.snapBackAnimationId);
            entry.state.snapBackAnimationId = null;
        }

        // Cancel any pending rAF from scheduleTranslate.
        entry.cancelScheduledTranslate();

        // Reset inline styles.
        element.style.transform = '';
        element.style.transition = '';
        element.style.willChange = '';
        if (element.parentElement)
            element.parentElement.style.minHeight = '';

        // Remove all event listeners.
        element.removeEventListener('touchstart', entry.onTouchStart);
        element.removeEventListener('touchmove', entry.onTouchMove);
        element.removeEventListener('touchend', entry.onTouchEnd);
        element.removeEventListener('touchcancel', entry.onTouchCancel);

        _carouselRegistry.delete(element);
    };
})();

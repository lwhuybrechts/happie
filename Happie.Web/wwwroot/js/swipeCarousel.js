// Swipe carousel handler for DayPlanPage three-panel carousel.
// Translates the .swipe-carousel wrapper during drag; all three panels move in unison.
(function () {
    'use strict';

    window.happie = window.happie || {};

    // Constants.
    const SWIPE_THRESHOLD = 100;
    const DEAD_ZONE = 10;
    const EDGE_EXCLUSION = 20;
    const MAX_OVERSHOOT_FACTOR = 1.2;
    const ANIMATION_DURATION = 300;
    const FADED_OPACITY = 0.6;

    // Registry for cleanup.
    const _carouselRegistry = new Map();

    // Rubber-band physics: linear up to viewport width, then diminishing returns.
    // Mirror: SwipeCarouselMath.RubberBand — update that C# class if this logic changes.
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
    // Mirror: SwipeCarouselMath.IsInEdgeExclusionZone handles the edge zone part — update that C# class if exclusion logic changes.
    function isExcludedTarget(target) {
        return target.closest('input, textarea, select, [contenteditable="true"], [role="dialog"], .nudge-modal__overlay');
    }

    // Check if clientX is in the edge exclusion zone.
    // Mirror: SwipeCarouselMath.IsInEdgeExclusionZone — update that C# class if this logic changes.
    function isInEdgeExclusionZone(clientX, viewportWidth) {
        return clientX <= EDGE_EXCLUSION || clientX >= viewportWidth - EDGE_EXCLUSION;
    }

    happie.registerSwipeCarousel = function (touchTarget, carousel, dotNetRef) {
        if (!touchTarget || !carousel) return;

        // The viewport is the parent of the carousel track element.
        var viewport = carousel.parentElement;

        // Find the date navigation slider track and arrows in the static header.
        var sliderTrack = touchTarget.querySelector('.date-nav__slider-track');
        var leftArrow = touchTarget.querySelector('.date-nav__arrow--left');
        var rightArrow = touchTarget.querySelector('.date-nav__arrow--right');

        // Size the slider items to match the carousel panel width so dates and pages
        // are spaced identically and slide in perfect pixel-for-pixel sync.
        var sliderViewport = sliderTrack ? sliderTrack.parentElement : null;
        var sliderRestOffset = 0;

        function recalcSliderSizing() {
            var cw = viewport.offsetWidth || window.innerWidth;
            // Calculate the resting offset: center the middle item's content within the slider viewport.
            // Track layout: [item0][item1][item2], each cw wide.
            // Middle item center is at 1.5 * cw from track left.
            // We want that to align with the slider viewport's center (vpW / 2 from its left edge).
            var vpW = sliderViewport ? sliderViewport.offsetWidth : cw;
            sliderRestOffset = vpW / 2 - 1.5 * cw;
            if (sliderTrack) {
                sliderTrack.style.width = (cw * 3) + 'px';
                sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';
                var items = sliderTrack.querySelectorAll('.date-nav__slider-item');
                for (var i = 0; i < items.length; i++) {
                    items[i].style.flex = '0 0 ' + cw + 'px';
                    items[i].style.width = cw + 'px';
                }
            }
        }

        recalcSliderSizing();

        // Recalculate on resize so the centering stays correct after orientation/viewport changes.
        var onResize = function () { recalcSliderSizing(); };
        window.addEventListener('resize', onResize);

        var state = {
            startX: 0,
            startY: 0,
            currentX: 0,
            tracking: false,
            directionLocked: false,
            isHorizontal: false,
            animating: false,
            snapBackAnimationId: null,
            completionAnimationId: null,
            disposed: false,
            thresholdMet: false,
            incomingPanel: null
        };

        var rafId = null;
        var pendingTranslateX = null;

        // Apply translateX via requestAnimationFrame for 60fps.
        function scheduleTranslate(translateValue) {
            pendingTranslateX = translateValue;
            if (rafId === null) {
                rafId = requestAnimationFrame(function () {
                    if (pendingTranslateX !== null) {
                        carousel.style.transform = 'translateX(' + pendingTranslateX + 'px)';
                        // Translate the date slider track by the same pixel amount as the carousel
                        // so they move at exactly the same speed during swipe.
                        if (sliderTrack)
                            sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + pendingTranslateX) + 'px)';
                    }
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
                carousel.style.transition = 'none';
            }
        }

        // Get the current translateX from the carousel's computed transform matrix.
        function getCurrentTranslateX() {
            var style = window.getComputedStyle(carousel);
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

                carousel.style.transform = 'translateX(' + currentValue + 'px)';

                // Animate slider track back in sync (same pixel offset).
                if (sliderTrack)
                    sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + currentValue) + 'px)';

                if (progress < 1) {
                    state.snapBackAnimationId = requestAnimationFrame(step);
                } else {
                    // Animation complete.
                    carousel.style.transform = '';
                    carousel.style.willChange = '';
                    viewport.style.minHeight = '';
                    if (sliderTrack)
                        sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';
                    state.snapBackAnimationId = null;
                }
            }

            state.snapBackAnimationId = requestAnimationFrame(step);
        }

        // Completion animation (slide full carousel width).
        function animateCompletion(direction) {
            state.animating = true;
            var cw = viewport.offsetWidth || window.innerWidth;
            var targetX = direction * cw;
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

                carousel.style.transform = 'translateX(' + currentValue + 'px)';

                // Animate slider track in sync (same pixel offset).
                if (sliderTrack)
                    sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + currentValue) + 'px)';

                if (progress < 1) {
                    state.completionAnimationId = requestAnimationFrame(step);
                } else {
                    state.completionAnimationId = null;
                    // Animation complete — call .NET and reset.
                    carousel.style.transform = '';
                    carousel.style.willChange = '';
                    viewport.style.minHeight = '';
                    if (sliderTrack)
                        sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';

                    // Guard against disposed DotNetObjectReference.
                    if (state.disposed) {
                        state.animating = false;
                        return;
                    }

                    if (direction < 0)
                        dotNetRef.invokeMethodAsync('SwipeLeftAsync').then(function () { state.animating = false; }).catch(function () { state.animating = false; });
                    else
                        dotNetRef.invokeMethodAsync('SwipeRightAsync').then(function () { state.animating = false; }).catch(function () { state.animating = false; });
                }
            }

            state.completionAnimationId = requestAnimationFrame(step);
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

            carousel.style.transition = 'none';
            carousel.style.willChange = 'transform';
        };

        var onTouchMove = function (e) {
            if (!state.tracking || state.animating) return;

            var touch = e.touches[0];
            var deltaX = touch.clientX - state.startX;
            var deltaY = touch.clientY - state.startY;

            // Direction lock: first significant movement (>10px on any axis) determines direction.
            // Mirror: SwipeCarouselMath.DetermineDirectionLock — update that C# class if this logic changes.
            if (!state.directionLocked) {
                if (Math.abs(deltaX) < DEAD_ZONE && Math.abs(deltaY) < DEAD_ZONE)
                    return;

                state.directionLocked = true;

                if (Math.abs(deltaX) >= Math.abs(deltaY)) {
                    state.isHorizontal = true;
                    // Expand viewport to fit the tallest panel so adjacent content isn't clipped.
                    var activePanel = carousel.querySelector('.swipe-carousel__panel--active');
                    var prevPanel = carousel.querySelector('.swipe-carousel__panel--prev');
                    var nextPanel = carousel.querySelector('.swipe-carousel__panel--next');
                    var maxHeight = activePanel ? activePanel.scrollHeight : 0;
                    if (prevPanel) maxHeight = Math.max(maxHeight, prevPanel.scrollHeight);
                    if (nextPanel) maxHeight = Math.max(maxHeight, nextPanel.scrollHeight);
                    if (maxHeight > 0)
                        viewport.style.minHeight = maxHeight + 'px';
                } else {
                    // Vertical — abort tracking, allow normal scroll.
                    state.isHorizontal = false;
                    state.tracking = false;
                    carousel.style.willChange = '';
                    carousel.style.transform = '';
                    return;
                }
            }

            if (!state.isHorizontal) return;

            // Prevent vertical scrolling while horizontal swipe is active.
            if (e.cancelable)
                e.preventDefault();

            // Accumulate drag including any offset from interrupted snap-back.
            var totalDrag = deltaX + state.currentX;
            var carouselWidth = viewport.offsetWidth || window.innerWidth;
            var translated = rubberBand(totalDrag, carouselWidth);

            scheduleTranslate(translated);

            // Toggle opacity on the incoming panel based on drag progress.
            // Two phases: 0→80% of threshold linearly from START_OPACITY to PRE_BUMP_OPACITY,
            // then 80%→100% of threshold bumps from PRE_BUMP_OPACITY to 1.0 for clear feedback.
            var absDrag = Math.abs(totalDrag);
            var newIncomingPanel = totalDrag > 0
                ? carousel.querySelector('.swipe-carousel__panel--prev')
                : carousel.querySelector('.swipe-carousel__panel--next');

            // If direction reversed, reset the old panel's opacity and switch.
            if (state.incomingPanel && state.incomingPanel !== newIncomingPanel) {
                state.incomingPanel.style.opacity = '';
            }
            state.incomingPanel = newIncomingPanel;

            var START_OPACITY = 0.3;
            var PRE_BUMP_OPACITY = 0.8;
            var BUMP_POINT = 0.8; // 80% of threshold distance.

            if (absDrag >= SWIPE_THRESHOLD) {
                if (!state.thresholdMet) {
                    state.thresholdMet = true;
                    // Highlight the relevant navigation arrow.
                    if (totalDrag > 0 && leftArrow)
                        leftArrow.classList.add('date-nav__arrow--highlight');
                    else if (totalDrag < 0 && rightArrow)
                        rightArrow.classList.add('date-nav__arrow--highlight');
                }
                // Snap to full opacity once threshold is reached.
                if (state.incomingPanel)
                    state.incomingPanel.style.opacity = '';
            } else {
                if (state.thresholdMet) {
                    state.thresholdMet = false;
                    // Remove arrow highlight.
                    if (leftArrow)
                        leftArrow.classList.remove('date-nav__arrow--highlight');
                    if (rightArrow)
                        rightArrow.classList.remove('date-nav__arrow--highlight');
                }
                // Linear from START_OPACITY to PRE_BUMP_OPACITY, then hold until threshold snaps to 1.0.
                if (state.incomingPanel) {
                    var progress = absDrag / SWIPE_THRESHOLD;
                    var opacity;
                    if (progress <= BUMP_POINT) {
                        opacity = START_OPACITY + (PRE_BUMP_OPACITY - START_OPACITY) * (progress / BUMP_POINT);
                    } else {
                        // Hold at PRE_BUMP_OPACITY — the snap to 1.0 happens at threshold.
                        opacity = PRE_BUMP_OPACITY;
                    }
                    state.incomingPanel.style.opacity = opacity.toString();
                }
            }
        };

        var onTouchEnd = function () {
            if (!state.tracking || !state.directionLocked || !state.isHorizontal) {
                state.tracking = false;
                if (!state.animating) {
                    carousel.style.willChange = '';
                    viewport.style.minHeight = '';
                }
                return;
            }
            state.tracking = false;
            cancelScheduledTranslate();

            // Reset panel opacities.
            if (state.incomingPanel) {
                state.incomingPanel.style.opacity = '';
                state.incomingPanel = null;
            }
            state.thresholdMet = false;

            // Reset arrow highlights.
            if (leftArrow)
                leftArrow.classList.remove('date-nav__arrow--highlight');
            if (rightArrow)
                rightArrow.classList.remove('date-nav__arrow--highlight');

            var currentTranslateX = getCurrentTranslateX();
            var absDelta = Math.abs(currentTranslateX);

            if (absDelta >= SWIPE_THRESHOLD) {
                // Threshold met — completion animation.
                // Mirror: SwipeCarouselMath.ShouldNavigate — update that C# class if this logic changes.
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

            // Reset panel opacities.
            if (state.incomingPanel) {
                state.incomingPanel.style.opacity = '';
                state.incomingPanel = null;
            }
            state.thresholdMet = false;

            // Reset arrow highlights.
            if (leftArrow)
                leftArrow.classList.remove('date-nav__arrow--highlight');
            if (rightArrow)
                rightArrow.classList.remove('date-nav__arrow--highlight');

            var currentTranslateX = getCurrentTranslateX();
            if (currentTranslateX !== 0)
                animateSnapBack(currentTranslateX);
            else {
                carousel.style.willChange = '';
                viewport.style.minHeight = '';
            }
        };

        touchTarget.addEventListener('touchstart', onTouchStart, { passive: true });
        touchTarget.addEventListener('touchmove', onTouchMove, { passive: false });
        touchTarget.addEventListener('touchend', onTouchEnd, { passive: true });
        touchTarget.addEventListener('touchcancel', onTouchCancel, { passive: true });

        _carouselRegistry.set(touchTarget, {
            onTouchStart, onTouchMove, onTouchEnd, onTouchCancel, onResize,
            dotNetRef, state, cancelScheduledTranslate,
            sliderTrack, leftArrow, rightArrow, carousel
        });
    };

    // Returns a promise that resolves when the browser is idle (or after 200ms timeout).
    // Used to defer adjacent panel rendering so the active panel stays responsive.
    happie.waitForIdle = function () {
        return new Promise(function (resolve) {
            if (typeof requestIdleCallback === 'function') {
                requestIdleCallback(resolve, { timeout: 200 });
            } else {
                setTimeout(resolve, 200);
            }
        });
    };

    // Returns true if the device supports touch input, false otherwise.
    // Used to decide whether arrow buttons should trigger the slide animation.
    happie.hasTouchSupport = function () {
        return ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);
    };

    // Programmatically trigger the slide animation as if the user had swiped.
    // direction: -1 = slide left (navigate to next day), 1 = slide right (navigate to previous day).
    // Returns true if the animation was started, false if it couldn't be started (e.g., already animating).
    happie.triggerSlideNavigation = function (touchTarget, direction) {
        if (!touchTarget) return false;
        var entry = _carouselRegistry.get(touchTarget);
        if (!entry) return false;
        if (entry.state.animating) return false;

        // Cancel any in-progress snap-back.
        if (entry.state.snapBackAnimationId !== null) {
            cancelAnimationFrame(entry.state.snapBackAnimationId);
            entry.state.snapBackAnimationId = null;
        }

        // Ensure carousel starts from 0.
        entry.carousel.style.transition = 'none';
        entry.carousel.style.transform = '';
        entry.carousel.style.willChange = 'transform';

        // Expand viewport to fit the tallest panel so adjacent content isn't clipped.
        var viewport = entry.carousel.parentElement;
        var activePanel = entry.carousel.querySelector('.swipe-carousel__panel--active');
        var prevPanel = entry.carousel.querySelector('.swipe-carousel__panel--prev');
        var nextPanel = entry.carousel.querySelector('.swipe-carousel__panel--next');
        var maxHeight = activePanel ? activePanel.scrollHeight : 0;
        if (prevPanel) maxHeight = Math.max(maxHeight, prevPanel.scrollHeight);
        if (nextPanel) maxHeight = Math.max(maxHeight, nextPanel.scrollHeight);
        if (maxHeight > 0)
            viewport.style.minHeight = maxHeight + 'px';

        // Start the completion animation in the requested direction.
        entry.state.animating = true;
        var cw = viewport.offsetWidth || window.innerWidth;
        var targetX = direction * cw;
        var startTime = null;
        var sliderTrack = entry.sliderTrack;
        var carousel = entry.carousel;

        // Recalculate slider rest offset.
        var sliderViewport = sliderTrack ? sliderTrack.parentElement : null;
        var vpW = sliderViewport ? sliderViewport.offsetWidth : cw;
        var sliderRestOffset = vpW / 2 - 1.5 * cw;

        function step(timestamp) {
            if (startTime === null)
                startTime = timestamp;

            var elapsed = timestamp - startTime;
            var progress = Math.min(elapsed / ANIMATION_DURATION, 1);
            // Ease-out cubic.
            var eased = 1 - Math.pow(1 - progress, 3);
            var currentValue = targetX * eased;

            carousel.style.transform = 'translateX(' + currentValue + 'px)';

            // Animate slider track in sync.
            if (sliderTrack)
                sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + currentValue) + 'px)';

            if (progress < 1) {
                entry.state.completionAnimationId = requestAnimationFrame(step);
            } else {
                entry.state.completionAnimationId = null;
                // Animation complete — call .NET and reset.
                carousel.style.transform = '';
                carousel.style.willChange = '';
                viewport.style.minHeight = '';
                if (sliderTrack)
                    sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';

                // Guard against disposed DotNetObjectReference.
                if (entry.state.disposed) {
                    entry.state.animating = false;
                    return;
                }

                if (direction < 0)
                    entry.dotNetRef.invokeMethodAsync('SwipeLeftAsync').then(function () { entry.state.animating = false; }).catch(function () { entry.state.animating = false; });
                else
                    entry.dotNetRef.invokeMethodAsync('SwipeRightAsync').then(function () { entry.state.animating = false; }).catch(function () { entry.state.animating = false; });
            }
        }

        entry.state.completionAnimationId = requestAnimationFrame(step);
        return true;
    };

    happie.disposeSwipeCarousel = function (touchTarget) {
        if (!touchTarget) return;
        var entry = _carouselRegistry.get(touchTarget);
        if (!entry) return;

        // Mark as disposed so in-flight completion animations skip the dotNetRef call.
        entry.state.disposed = true;

        // Cancel any in-progress snap-back animation.
        if (entry.state.snapBackAnimationId !== null) {
            cancelAnimationFrame(entry.state.snapBackAnimationId);
            entry.state.snapBackAnimationId = null;
        }

        // Cancel any in-progress completion animation.
        if (entry.state.completionAnimationId !== null) {
            cancelAnimationFrame(entry.state.completionAnimationId);
            entry.state.completionAnimationId = null;
        }

        // Cancel any pending rAF from scheduleTranslate.
        entry.cancelScheduledTranslate();

        // Reset inline styles on the carousel element.
        if (entry.carousel) {
            entry.carousel.style.transform = '';
            entry.carousel.style.transition = '';
            entry.carousel.style.willChange = '';
        }
        if (entry.carousel && entry.carousel.parentElement)
            entry.carousel.parentElement.style.minHeight = '';

        // Reset slider track and arrow highlights.
        if (entry.sliderTrack) {
            // Reset to CSS default (JS sizing will be re-applied on next registration).
            entry.sliderTrack.style.width = '';
            entry.sliderTrack.style.transform = '';
            var items = entry.sliderTrack.querySelectorAll('.date-nav__slider-item');
            for (var i = 0; i < items.length; i++) {
                items[i].style.flex = '';
                items[i].style.width = '';
            }
        }
        if (entry.leftArrow)
            entry.leftArrow.classList.remove('date-nav__arrow--highlight');
        if (entry.rightArrow)
            entry.rightArrow.classList.remove('date-nav__arrow--highlight');

        // Remove all event listeners.
        touchTarget.removeEventListener('touchstart', entry.onTouchStart);
        touchTarget.removeEventListener('touchmove', entry.onTouchMove);
        touchTarget.removeEventListener('touchend', entry.onTouchEnd);
        touchTarget.removeEventListener('touchcancel', entry.onTouchCancel);
        window.removeEventListener('resize', entry.onResize);

        _carouselRegistry.delete(touchTarget);
    };

    // ========================================================================
    // Month Slider — standalone swipe handler for CalendarPage month navigation.
    // Only moves the date-nav slider track (month/year label); no page-content carousel.
    // ========================================================================

    const _monthSliderRegistry = new Map();

    happie.registerMonthSlider = function (navElement, dotNetRef, touchTarget) {
        if (!navElement) return;

        // touchTarget defaults to navElement if not provided (backwards-compatible).
        var eventTarget = touchTarget || navElement;

        // If already registered for this event target, check if the nav element changed
        // (Blazor's @key may have recreated it). If unchanged, skip (idempotent).
        var existing = _monthSliderRegistry.get(eventTarget);
        if (existing) {
            if (existing.navElement === navElement)
                return;
            // Nav element changed — dispose old registration and re-register with new slider track.
            happie.disposeMonthSlider(existing.navElement, touchTarget);
        }

        var sliderTrack = navElement.querySelector('.date-nav__slider-track');
        var sliderViewport = sliderTrack ? sliderTrack.parentElement : null;
        var leftArrow = navElement.querySelector('.date-nav__arrow--left');
        var rightArrow = navElement.querySelector('.date-nav__arrow--right');
        var sliderRestOffset = 0;

        // Use the slider viewport width (space between the arrows) for the slide distance
        // so the month label slides behind the arrows rather than traversing the full nav width.
        function getSlideWidth() {
            return sliderViewport ? sliderViewport.offsetWidth : (navElement.offsetWidth || window.innerWidth);
        }

        function recalcSliderSizing() {
            var cw = getSlideWidth();
            var vpW = sliderViewport ? sliderViewport.offsetWidth : cw;
            sliderRestOffset = vpW / 2 - 1.5 * cw;
            if (sliderTrack) {
                sliderTrack.style.width = (cw * 3) + 'px';
                sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';
                var items = sliderTrack.querySelectorAll('.date-nav__slider-item');
                for (var i = 0; i < items.length; i++) {
                    items[i].style.flex = '0 0 ' + cw + 'px';
                    items[i].style.width = cw + 'px';
                }
            }
        }

        recalcSliderSizing();

        var onResize = function () { recalcSliderSizing(); };
        window.addEventListener('resize', onResize);

        var state = {
            startX: 0,
            startY: 0,
            currentX: 0,
            tracking: false,
            directionLocked: false,
            isHorizontal: false,
            animating: false,
            snapBackAnimationId: null,
            completionAnimationId: null,
            disposed: false
        };

        var rafId = null;
        var pendingTranslateX = null;

        function scheduleTranslate(translateValue) {
            pendingTranslateX = translateValue;
            if (rafId === null) {
                rafId = requestAnimationFrame(function () {
                    if (pendingTranslateX !== null && sliderTrack)
                        sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + pendingTranslateX) + 'px)';
                    rafId = null;
                });
            }
        }

        function cancelScheduledTranslate() {
            if (rafId !== null) {
                cancelAnimationFrame(rafId);
                rafId = null;
            }
            pendingTranslateX = null;
        }

        function getCurrentTranslateX() {
            if (!sliderTrack) return 0;
            var style = window.getComputedStyle(sliderTrack);
            var matrix = style.transform;
            if (!matrix || matrix === 'none') return 0;
            var values = matrix.match(/matrix\((.+)\)/);
            if (values) {
                var parts = values[1].split(',');
                return (parseFloat(parts[4]) || 0) - sliderRestOffset;
            }
            return 0;
        }

        function animateSnapBack(fromX) {
            var startTime = null;

            function step(timestamp) {
                if (startTime === null) startTime = timestamp;
                var elapsed = timestamp - startTime;
                var progress = Math.min(elapsed / ANIMATION_DURATION, 1);
                var eased = 1 - Math.pow(1 - progress, 3);
                var currentValue = fromX * (1 - eased);

                if (sliderTrack)
                    sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + currentValue) + 'px)';

                if (progress < 1) {
                    state.snapBackAnimationId = requestAnimationFrame(step);
                } else {
                    if (sliderTrack)
                        sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';
                    state.snapBackAnimationId = null;
                }
            }

            state.snapBackAnimationId = requestAnimationFrame(step);
        }

        function animateCompletion(direction) {
            state.animating = true;
            var cw = getSlideWidth();
            var targetX = direction * cw;
            var startX = getCurrentTranslateX();
            var startTime = null;

            function step(timestamp) {
                if (startTime === null) startTime = timestamp;
                var elapsed = timestamp - startTime;
                var progress = Math.min(elapsed / ANIMATION_DURATION, 1);
                var eased = 1 - Math.pow(1 - progress, 3);
                var currentValue = startX + (targetX - startX) * eased;

                if (sliderTrack)
                    sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + currentValue) + 'px)';

                if (progress < 1) {
                    state.completionAnimationId = requestAnimationFrame(step);
                } else {
                    state.completionAnimationId = null;
                    // Do NOT reset the slider track here — leave it off-screen.
                    // Blazor's @key will destroy and recreate the element with the new month text.

                    if (state.disposed) {
                        state.animating = false;
                        return;
                    }

                    // direction > 0 means dragged right → go to previous month.
                    // direction < 0 means dragged left → go to next month.
                    if (direction < 0)
                        dotNetRef.invokeMethodAsync('SlideLeftAsync').then(function () { state.animating = false; }).catch(function () { state.animating = false; });
                    else
                        dotNetRef.invokeMethodAsync('SlideRightAsync').then(function () { state.animating = false; }).catch(function () { state.animating = false; });
                }
            }

            state.completionAnimationId = requestAnimationFrame(step);
        }

        var onTouchStart = function (e) {
            if (state.animating) return;
            var touch = e.touches[0];
            var clientX = touch.clientX;

            // Input element exclusion.
            if (isExcludedTarget(e.target)) return;

            // If snap-back is in progress, cancel it and resume from current position.
            if (state.snapBackAnimationId !== null) {
                var currentPos = getCurrentTranslateX();
                cancelAnimationFrame(state.snapBackAnimationId);
                state.snapBackAnimationId = null;
                state.currentX = currentPos;
            } else {
                state.currentX = 0;
            }

            state.startX = clientX;
            state.startY = touch.clientY;
            state.tracking = true;
            state.directionLocked = false;
            state.isHorizontal = false;
        };

        var onTouchMove = function (e) {
            if (!state.tracking || state.animating) return;

            var touch = e.touches[0];
            var deltaX = touch.clientX - state.startX;
            var deltaY = touch.clientY - state.startY;

            if (!state.directionLocked) {
                if (Math.abs(deltaX) < DEAD_ZONE && Math.abs(deltaY) < DEAD_ZONE) return;
                state.directionLocked = true;
                if (Math.abs(deltaX) >= Math.abs(deltaY)) {
                    state.isHorizontal = true;
                } else {
                    state.isHorizontal = false;
                    state.tracking = false;
                    return;
                }
            }

            if (!state.isHorizontal) return;

            if (e.cancelable) e.preventDefault();

            var totalDrag = deltaX + state.currentX;
            var cw = getSlideWidth();
            var translated = rubberBand(totalDrag, cw);

            scheduleTranslate(translated);

            // Highlight arrow when threshold is met.
            var absDrag = Math.abs(totalDrag);
            if (absDrag >= SWIPE_THRESHOLD) {
                if (totalDrag > 0 && leftArrow)
                    leftArrow.classList.add('date-nav__arrow--highlight');
                else if (totalDrag < 0 && rightArrow)
                    rightArrow.classList.add('date-nav__arrow--highlight');
            } else {
                if (leftArrow) leftArrow.classList.remove('date-nav__arrow--highlight');
                if (rightArrow) rightArrow.classList.remove('date-nav__arrow--highlight');
            }
        };

        var onTouchEnd = function () {
            if (!state.tracking) return;
            state.tracking = false;
            cancelScheduledTranslate();

            if (leftArrow) leftArrow.classList.remove('date-nav__arrow--highlight');
            if (rightArrow) rightArrow.classList.remove('date-nav__arrow--highlight');

            var currentTranslateX = getCurrentTranslateX();
            var absDelta = Math.abs(currentTranslateX);

            if (absDelta >= SWIPE_THRESHOLD) {
                var direction = currentTranslateX > 0 ? 1 : -1;
                animateCompletion(direction);
            } else if (currentTranslateX !== 0) {
                animateSnapBack(currentTranslateX);
            }
        };

        var onTouchCancel = function () {
            if (!state.tracking) return;
            state.tracking = false;
            cancelScheduledTranslate();

            if (leftArrow) leftArrow.classList.remove('date-nav__arrow--highlight');
            if (rightArrow) rightArrow.classList.remove('date-nav__arrow--highlight');

            var currentTranslateX = getCurrentTranslateX();
            if (currentTranslateX !== 0)
                animateSnapBack(currentTranslateX);
        };

        eventTarget.addEventListener('touchstart', onTouchStart, { passive: true });
        eventTarget.addEventListener('touchmove', onTouchMove, { passive: false });
        eventTarget.addEventListener('touchend', onTouchEnd, { passive: true });
        eventTarget.addEventListener('touchcancel', onTouchCancel, { passive: true });

        _monthSliderRegistry.set(eventTarget, {
            onTouchStart, onTouchMove, onTouchEnd, onTouchCancel, onResize,
            dotNetRef, state, cancelScheduledTranslate, sliderTrack, leftArrow, rightArrow,
            navElement
        });
    };

    // Programmatically trigger month slide animation (for arrow clicks on touch devices).
    // direction: -1 = slide left (next month), 1 = slide right (previous month).
    happie.triggerMonthSlide = function (navElement, direction, touchTarget) {
        if (!navElement) return false;
        var eventTarget = touchTarget || navElement;
        var entry = _monthSliderRegistry.get(eventTarget);
        if (!entry) return false;
        if (entry.state.animating) return false;

        // Cancel any in-progress snap-back.
        if (entry.state.snapBackAnimationId !== null) {
            cancelAnimationFrame(entry.state.snapBackAnimationId);
            entry.state.snapBackAnimationId = null;
        }

        // Use slider viewport width (space between arrows) for the slide distance.
        var sliderViewport = entry.sliderTrack ? entry.sliderTrack.parentElement : null;
        var cw = sliderViewport ? sliderViewport.offsetWidth : (navElement.offsetWidth || window.innerWidth);
        var vpW = cw;
        var sliderRestOffset = vpW / 2 - 1.5 * cw;

        if (entry.sliderTrack)
            entry.sliderTrack.style.transform = 'translateX(' + sliderRestOffset + 'px)';

        // Start completion animation.
        entry.state.animating = true;
        var targetX = direction * cw;
        var startTime = null;

        function step(timestamp) {
            if (startTime === null) startTime = timestamp;
            var elapsed = timestamp - startTime;
            var progress = Math.min(elapsed / ANIMATION_DURATION, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            var currentValue = targetX * eased;

            if (entry.sliderTrack)
                entry.sliderTrack.style.transform = 'translateX(' + (sliderRestOffset + currentValue) + 'px)';

            if (progress < 1) {
                entry.state.completionAnimationId = requestAnimationFrame(step);
            } else {
                entry.state.completionAnimationId = null;
                // Do NOT reset the slider track here — leave it off-screen.
                // Blazor's @key will destroy and recreate the element with the new month text.

                if (entry.state.disposed) {
                    entry.state.animating = false;
                    return;
                }

                if (direction < 0)
                    entry.dotNetRef.invokeMethodAsync('SlideLeftAsync').then(function () { entry.state.animating = false; }).catch(function () { entry.state.animating = false; });
                else
                    entry.dotNetRef.invokeMethodAsync('SlideRightAsync').then(function () { entry.state.animating = false; }).catch(function () { entry.state.animating = false; });
            }
        }

        entry.state.completionAnimationId = requestAnimationFrame(step);
        return true;
    };

    happie.disposeMonthSlider = function (navElement, touchTarget) {
        if (!navElement) return;
        var eventTarget = touchTarget || navElement;
        var entry = _monthSliderRegistry.get(eventTarget);
        if (!entry) return;

        entry.state.disposed = true;

        if (entry.state.snapBackAnimationId !== null) {
            cancelAnimationFrame(entry.state.snapBackAnimationId);
            entry.state.snapBackAnimationId = null;
        }
        if (entry.state.completionAnimationId !== null) {
            cancelAnimationFrame(entry.state.completionAnimationId);
            entry.state.completionAnimationId = null;
        }
        entry.cancelScheduledTranslate();

        if (entry.sliderTrack) {
            entry.sliderTrack.style.width = '';
            entry.sliderTrack.style.transform = '';
            var items = entry.sliderTrack.querySelectorAll('.date-nav__slider-item');
            for (var i = 0; i < items.length; i++) {
                items[i].style.flex = '';
                items[i].style.width = '';
            }
        }
        if (entry.leftArrow) entry.leftArrow.classList.remove('date-nav__arrow--highlight');
        if (entry.rightArrow) entry.rightArrow.classList.remove('date-nav__arrow--highlight');

        eventTarget.removeEventListener('touchstart', entry.onTouchStart);
        eventTarget.removeEventListener('touchmove', entry.onTouchMove);
        eventTarget.removeEventListener('touchend', entry.onTouchEnd);
        eventTarget.removeEventListener('touchcancel', entry.onTouchCancel);
        window.removeEventListener('resize', entry.onResize);

        _monthSliderRegistry.delete(eventTarget);
    };
})();

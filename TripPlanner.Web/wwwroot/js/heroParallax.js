window.heroParallax = (function () {
    // Sensitivity constants: multiply normalised mouse delta (-1..1) by these values to get px offset.
    var HORIZONTAL_SENSITIVITY = 25;
    var VERTICAL_SENSITIVITY = 10;
    // Fraction of scroll offset (in px) applied as upward parallax shift per speed unit.
    var SCROLL_DAMPING = 0.4;

    var layers = [];
    var hero = null;
    var scrollContainer = null;
    var mouseX = 0, mouseY = 0, scrollOffset = 0;
    var rafId = null;

    function findScrollParent(el) {
        while (el && el !== document.documentElement) {
            var style = window.getComputedStyle(el);
            if (/auto|scroll/.test(style.overflow + ' ' + style.overflowY)) {
                return el;
            }
            el = el.parentElement;
        }
        return window;
    }

    function applyTransforms() {
        layers.forEach(function (layer) {
            var speed = parseFloat(layer.dataset.parallax) || 0;
            var tx = mouseX * speed * HORIZONTAL_SENSITIVITY;
            var ty = mouseY * speed * VERTICAL_SENSITIVITY - scrollOffset * speed * SCROLL_DAMPING;
            layer.style.transform = 'translate(' + tx + 'px, ' + ty + 'px)';
        });
        rafId = null;
    }

    function schedule() {
        if (!rafId) {
            rafId = requestAnimationFrame(applyTransforms);
        }
    }

    function onMouseMove(e) {
        mouseX = (e.clientX - window.innerWidth / 2) / (window.innerWidth / 2);
        mouseY = (e.clientY - window.innerHeight / 2) / (window.innerHeight / 2);
        schedule();
    }

    function onScroll() {
        scrollOffset = scrollContainer === window
            ? window.scrollY
            : scrollContainer.scrollTop;
        schedule();
    }

    return {
        init: function () {
            hero = document.querySelector('.home-hero');
            if (!hero) return;
            layers = Array.from(hero.querySelectorAll('[data-parallax]'));
            scrollContainer = findScrollParent(hero.parentElement);
            document.addEventListener('mousemove', onMouseMove);
            scrollContainer.addEventListener('scroll', onScroll, { passive: true });
        },
        destroy: function () {
            document.removeEventListener('mousemove', onMouseMove);
            if (scrollContainer) {
                scrollContainer.removeEventListener('scroll', onScroll);
            }
            if (rafId) {
                cancelAnimationFrame(rafId);
                rafId = null;
            }
            layers = [];
            hero = null;
            scrollContainer = null;
            mouseX = 0;
            mouseY = 0;
            scrollOffset = 0;
        }
    };
})();

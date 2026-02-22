window.heroParallax = (function () {
    // Sensitivity constants: multiply normalised mouse delta (-1..1) by these values to get px offset.
    var HORIZONTAL_SENSITIVITY = 25;
    var VERTICAL_SENSITIVITY = 10;
    // Fraction of scroll offset (in px) applied as upward parallax shift per speed unit.
    var SCROLL_DAMPING = 0.2;

    var layers = [];
    var hero = null;
    var mouseX = 0, mouseY = 0, scrollOffset = 0;
    var rafId = null;

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
        var rect = hero.getBoundingClientRect();
        mouseX = (e.clientX - rect.left - rect.width / 2) / (rect.width / 2);
        mouseY = (e.clientY - rect.top - rect.height / 2) / (rect.height / 2);
        schedule();
    }

    function onMouseLeave() {
        mouseX = 0;
        mouseY = 0;
        schedule();
    }

    function onScroll() {
        scrollOffset = window.scrollY;
        schedule();
    }

    return {
        init: function () {
            hero = document.querySelector('.home-hero');
            if (!hero) return;
            layers = Array.from(hero.querySelectorAll('[data-parallax]'));
            hero.addEventListener('mousemove', onMouseMove);
            hero.addEventListener('mouseleave', onMouseLeave);
            window.addEventListener('scroll', onScroll, { passive: true });
        },
        destroy: function () {
            if (hero) {
                hero.removeEventListener('mousemove', onMouseMove);
                hero.removeEventListener('mouseleave', onMouseLeave);
            }
            window.removeEventListener('scroll', onScroll);
            if (rafId) {
                cancelAnimationFrame(rafId);
                rafId = null;
            }
            layers = [];
            hero = null;
            mouseX = 0;
            mouseY = 0;
            scrollOffset = 0;
        }
    };
})();

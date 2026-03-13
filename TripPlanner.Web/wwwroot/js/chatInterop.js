window.chatInterop = (function () {
    var MAX_TEXTAREA_HEIGHT = 150;

    return {
        scrollToBottom: function (elementId) {
            var el = document.getElementById(elementId);
            if (el) el.scrollTop = el.scrollHeight;
        },

        autoResizeTextArea: function (hostElement) {
            if (CSS.supports('field-sizing', 'content')) return;
            var textarea = hostElement && hostElement.shadowRoot
                ? hostElement.shadowRoot.querySelector('textarea')
                : null;
            if (!textarea) return;
            textarea.style.height = 'auto';
            textarea.style.height = Math.min(textarea.scrollHeight, MAX_TEXTAREA_HEIGHT) + 'px';
        },

        resetTextAreaHeight: function (hostElement) {
            if (CSS.supports('field-sizing', 'content')) return;
            var textarea = hostElement && hostElement.shadowRoot
                ? hostElement.shadowRoot.querySelector('textarea')
                : null;
            if (textarea) textarea.style.height = '';
        },

        getLocation: function () {
            return new Promise(function (resolve) {
                if (!navigator.geolocation) {
                    resolve(null);
                    return;
                }
                navigator.geolocation.getCurrentPosition(
                    function (pos) {
                        resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude });
                    },
                    function () {
                        resolve(null);
                    },
                    { timeout: 10000 }
                );
            });
        }
    };
})();

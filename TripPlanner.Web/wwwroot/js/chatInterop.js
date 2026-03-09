window.chatInterop = {
    scrollToBottom: function (elementId) {
        var el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
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

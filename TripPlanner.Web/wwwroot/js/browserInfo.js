window.browserInfo = {
    getTimezone: function () {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
        } catch (e) {
            return 'UTC';
        }
    },
    getCulture: function () {
        try {
            return navigator.language || 'en-US';
        } catch (e) {
            return 'en-US';
        }
    }
};

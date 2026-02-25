window.cookieConsent = {
    getItem: function () {
        return localStorage.getItem('cookie-consent');
    },
    setItem: function () {
        localStorage.setItem('cookie-consent', 'accepted');
    }
};

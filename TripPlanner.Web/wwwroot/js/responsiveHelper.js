window.responsiveHelper = {
    _handler: null,
    isSmallScreen: () => window.innerWidth < 960,
    registerResizeCallback: (dotnetRef) => {
        window.responsiveHelper._handler = () => {
            dotnetRef.invokeMethodAsync('UpdateScreenSize', window.innerWidth < 960);
        };
        window.addEventListener('resize', window.responsiveHelper._handler);
    },
    unregisterResizeCallback: () => {
        if (window.responsiveHelper._handler) {
            window.removeEventListener('resize', window.responsiveHelper._handler);
            window.responsiveHelper._handler = null;
        }
    }
};

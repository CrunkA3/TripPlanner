window.screenOrientationHelper = {
    getOrientation: function () {
        if (screen.orientation && screen.orientation.type) {
            return screen.orientation.type;
        }
        // Fallback for older browsers
        if (window.matchMedia("(orientation: portrait)").matches) {
            return "portrait";
        }
        return "landscape";
    },
    onOrientationChange: function (dotNetObjRef) {
        if (screen.orientation) {
            screen.orientation.addEventListener("change", () => {
                dotNetObjRef.invokeMethodAsync("OnOrientationChanged", screen.orientation.type);
            });
        } else {
            // Fallback for older browsers
            window.addEventListener("orientationchange", () => {
                let type = window.matchMedia("(orientation: portrait)").matches ? "portrait" : "landscape";
                dotNetObjRef.invokeMethodAsync("OnOrientationChanged", type);
            });
        }
    }
};

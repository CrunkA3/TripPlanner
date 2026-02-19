window.mapInterop = {
    map: null,
    markers: [],
    routes: [],
    gpxTracks: [],

    initializeMap: function (containerId, lat, lng, zoom) {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
        this.markers = [];
        this.routes = [];
        this.gpxTracks = [];

        this.map = L.map(containerId).setView([lat, lng], zoom);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            maxZoom: 19
        }).addTo(this.map);
    },

    addMarker: function (id, lat, lng, name, category, color) {
        if (!this.map) return;
        var marker = L.circleMarker([lat, lng], {
            radius: 8,
            fillColor: color,
            color: '#fff',
            weight: 2,
            opacity: 1,
            fillOpacity: 0.9
        }).addTo(this.map);
        marker.bindPopup('<b>' + name + '</b><br>' + category);
        this.markers.push({ id: id, marker: marker });
    },

    clearMarkers: function () {
        var self = this;
        this.markers.forEach(function (m) {
            if (self.map) self.map.removeLayer(m.marker);
        });
        this.markers = [];
    },

    addRoute: function (coordinates, color, weight) {
        if (!this.map) return;
        var latLngs = coordinates.map(function (c) { return [c.latitude, c.longitude]; });
        var route = L.polyline(latLngs, { color: color, weight: weight }).addTo(this.map);
        this.routes.push(route);
    },

    clearRoutes: function () {
        var self = this;
        this.routes.forEach(function (r) {
            if (self.map) self.map.removeLayer(r);
        });
        this.routes = [];
    },

    addGpxTrack: function (points, color) {
        if (!this.map) return;
        var latLngs = points.map(function (p) { return [p.latitude, p.longitude]; });
        var track = L.polyline(latLngs, { color: color, weight: 3 }).addTo(this.map);
        this.gpxTracks.push(track);
    },

    clearGpxTracks: function () {
        var self = this;
        this.gpxTracks.forEach(function (t) {
            if (self.map) self.map.removeLayer(t);
        });
        this.gpxTracks = [];
    },

    fitBounds: function () {
        if (!this.map) return;
        var allLayers = this.markers.map(function (m) { return m.marker; })
            .concat(this.routes)
            .concat(this.gpxTracks);
        if (allLayers.length > 0) {
            var group = L.featureGroup(allLayers);
            var bounds = group.getBounds();
            if (bounds.isValid()) {
                this.map.fitBounds(bounds.pad(0.1));
            }
        }
    },

    destroyMap: function () {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
        this.markers = [];
        this.routes = [];
        this.gpxTracks = [];
    }
};

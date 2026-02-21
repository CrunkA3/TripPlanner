window.mapInterop = {
    map: null,
    markers: [],
    routes: [],
    gpxTracks: [],
    homeMarker: null,
    mapClickHandler: null,

    initializeMap: function (containerId, lat, lng, zoom) {
        var self = this;
        return new Promise(function (resolve) {
            if (self.map) {
                self.map.remove();
                self.map = null;
            }
            self.markers = [];
            self.routes = [];
            self.gpxTracks = [];

            if (self.homeMarker) {
                self.homeMarker.remove();
                self.homeMarker = null;
            }
            if (self.mapClickHandler) {
                self.map.off('click', self.mapClickHandler);
                self.mapClickHandler = null;
            }

            self.map = new maplibregl.Map({
                container: containerId,
                style: {
                    version: 8,
                    sources: {
                        'osm': {
                            type: 'raster',
                            tiles: [
                                'https://a.tile.openstreetmap.org/{z}/{x}/{y}.png',
                                'https://b.tile.openstreetmap.org/{z}/{x}/{y}.png',
                                'https://c.tile.openstreetmap.org/{z}/{x}/{y}.png'
                            ],
                            tileSize: 256,
                            attribution: '\u00a9 <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                        }
                    },
                    layers: [{ id: 'osm', type: 'raster', source: 'osm' }]
                },
                center: [lng, lat],
                zoom: zoom
            });
            self.map.on('load', function () { resolve(); });
        });
    },

    addAccommodationMarker: function (id, lat, lng, name, checkIn, checkOut) {
        if (!this.map) return;
        var el = document.createElement('div');
        el.style.width = '22px';
        el.style.height = '22px';
        el.style.borderRadius = '4px';
        el.style.backgroundColor = '#9C27B0';
        el.style.border = '2px solid white';
        el.style.boxShadow = '0 0 4px rgba(0,0,0,0.4)';
        el.style.cursor = 'pointer';
        el.style.display = 'flex';
        el.style.alignItems = 'center';
        el.style.justifyContent = 'center';
        el.style.color = 'white';
        el.style.fontSize = '12px';
        el.style.fontWeight = 'bold';
        el.innerText = 'H';

        var popupHtml = '<b>' + name + '</b>';
        if (checkIn) popupHtml += '<br>\uD83D\uDCC5 Check-in: ' + checkIn;
        if (checkOut) popupHtml += '<br>\uD83D\uDCC5 Check-out: ' + checkOut;

        var popup = new maplibregl.Popup({ offset: 12 })
            .setHTML(popupHtml);

        var marker = new maplibregl.Marker({ element: el })
            .setLngLat([lng, lat])
            .setPopup(popup)
            .addTo(this.map);

        this.markers.push({ id: id, marker: marker, lngLat: [lng, lat] });
    },

    addMarker: function (id, lat, lng, name, category, color) {
        if (!this.map) return;
        var el = document.createElement('div');
        el.style.width = '16px';
        el.style.height = '16px';
        el.style.borderRadius = '50%';
        el.style.backgroundColor = color;
        el.style.border = '2px solid white';
        el.style.boxShadow = '0 0 4px rgba(0,0,0,0.4)';
        el.style.cursor = 'pointer';

        var popup = new maplibregl.Popup({ offset: 10 })
            .setHTML('<b>' + name + '</b><br>' + category);

        var marker = new maplibregl.Marker({ element: el })
            .setLngLat([lng, lat])
            .setPopup(popup)
            .addTo(this.map);

        this.markers.push({ id: id, marker: marker, lngLat: [lng, lat] });
    },

    clearMarkers: function () {
        this.markers.forEach(function (m) { m.marker.remove(); });
        this.markers = [];
    },

    addRoute: function (coordinates, color, weight) {
        if (!this.map) return;
        var routeId = 'route-' + this.routes.length;
        var coords = coordinates.map(function (c) { return [c.longitude, c.latitude]; });
        this.map.addSource(routeId, {
            type: 'geojson',
            data: { type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: coords } }
        });
        this.map.addLayer({
            id: routeId, type: 'line', source: routeId,
            paint: { 'line-color': color, 'line-width': weight }
        });
        this.routes.push({ id: routeId, coords: coords });
    },

    clearRoutes: function () {
        var self = this;
        this.routes.forEach(function (r) {
            if (self.map && self.map.getLayer(r.id)) self.map.removeLayer(r.id);
            if (self.map && self.map.getSource(r.id)) self.map.removeSource(r.id);
        });
        this.routes = [];
    },

    addGpxTrack: function (points, color) {
        if (!this.map) return;
        var trackId = 'gpx-' + this.gpxTracks.length;
        var coords = points.map(function (p) { return [p.longitude, p.latitude]; });
        this.map.addSource(trackId, {
            type: 'geojson',
            data: { type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: coords } }
        });
        this.map.addLayer({
            id: trackId, type: 'line', source: trackId,
            paint: { 'line-color': color, 'line-width': 3 }
        });
        this.gpxTracks.push({ id: trackId, coords: coords });
    },

    clearGpxTracks: function () {
        var self = this;
        this.gpxTracks.forEach(function (t) {
            if (self.map && self.map.getLayer(t.id)) self.map.removeLayer(t.id);
            if (self.map && self.map.getSource(t.id)) self.map.removeSource(t.id);
        });
        this.gpxTracks = [];
    },

    fitBounds: function () {
        if (!this.map) return;
        if (this.markers.length === 0 && this.routes.length === 0 && this.gpxTracks.length === 0) return;
        var bounds = new maplibregl.LngLatBounds();
        this.markers.forEach(function (m) { bounds.extend(m.lngLat); });
        this.routes.forEach(function (r) { r.coords.forEach(function (c) { bounds.extend(c); }); });
        this.gpxTracks.forEach(function (t) { t.coords.forEach(function (c) { bounds.extend(c); }); });
        if (!bounds.isEmpty()) {
            this.map.fitBounds(bounds, { padding: 50 });
        }
    },

    getCurrentLocation: function () {
        return new Promise(function (resolve, reject) {
            if (!navigator.geolocation) {
                reject(new Error('Geolocation is not supported by this browser.'));
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (position) { resolve([position.coords.latitude, position.coords.longitude]); },
                function (error) { reject(error); }
            );
        });
    },

    addHomeMarker: function (lat, lng, name) {
        if (!this.map) return;
        if (this.homeMarker) {
            this.homeMarker.remove();
            this.homeMarker = null;
        }
        var el = document.createElement('div');
        el.style.width = '24px';
        el.style.height = '24px';
        el.style.borderRadius = '4px';
        el.style.backgroundColor = '#1A73E8';
        el.style.border = '3px solid white';
        el.style.boxShadow = '0 0 6px rgba(0,0,0,0.5)';
        el.style.cursor = 'pointer';
        el.title = name || 'Home';
        el.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="white" style="width:18px;height:18px;margin:0px;"><path d="M10 2L2 9h2v9h5v-5h2v5h5V9h2L10 2z"/></svg>';

        var popup = new maplibregl.Popup({ offset: 12 })
            .setHTML('<b>' + (name || 'Home') + '</b>');

        this.homeMarker = new maplibregl.Marker({ element: el })
            .setLngLat([lng, lat])
            .setPopup(popup)
            .addTo(this.map);
    },

    removeHomeMarker: function () {
        if (this.homeMarker) {
            this.homeMarker.remove();
            this.homeMarker = null;
        }
    },

    setMapClickHandler: function (dotNetRef, methodName) {
        if (!this.map) return;
        var self = this;
        if (self.mapClickHandler) {
            self.map.off('click', self.mapClickHandler);
        }
        self.mapClickHandler = function (e) {
            dotNetRef.invokeMethodAsync(methodName, e.lngLat.lat, e.lngLat.lng);
        };
        self.map.on('click', self.mapClickHandler);
    },

    removeMapClickHandler: function () {
        if (this.map && this.mapClickHandler) {
            this.map.off('click', this.mapClickHandler);
            this.mapClickHandler = null;
        }
    },

    destroyMap: function () {
        if (this.map) {
            if (this.mapClickHandler) {
                this.map.off('click', this.mapClickHandler);
                this.mapClickHandler = null;
            }
            if (this.homeMarker) {
                this.homeMarker.remove();
                this.homeMarker = null;
            }
            this.map.remove();
            this.map = null;
        }
        this.markers = [];
        this.routes = [];
        this.gpxTracks = [];
    }
};

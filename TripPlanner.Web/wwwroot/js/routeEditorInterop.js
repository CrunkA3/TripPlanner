window.gpxRouteEditor = {
    instances: {},

    create: async function (editorId, containerId, lat, lng, zoom, dotNetRef, callbackMethod) {
        this.destroy(editorId);

        var map = new maplibregl.Map({
            container: containerId,
            style: {
                version: 8,
                sources: {
                    osm: {
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

        var instance = {
            map: map,
            points: [],
            waypoints: [],
            routeMarkers: [],
            midpointMarkers: [],
            waypointMarkers: [],
            sourceId: editorId + '-route',
            layerId: editorId + '-route-layer',
            dotNetRef: dotNetRef,
            callbackMethod: callbackMethod,
            addWaypointMode: false
        };

        this.instances[editorId] = instance;
        var self = this;

        await new Promise(function (resolve) {
            map.on('load', resolve);
        });

        map.addSource(instance.sourceId, {
            type: 'geojson',
            data: {
                type: 'Feature',
                properties: {},
                geometry: { type: 'LineString', coordinates: [] }
            }
        });

        map.addLayer({
            id: instance.layerId,
            type: 'line',
            source: instance.sourceId,
            paint: {
                'line-color': '#FF5722',
                'line-width': 4
            }
        });

        map.on('click', function (e) {
            if (!self.instances[editorId]) return;
            if (instance.addWaypointMode) {
                var waypointId = (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : Math.random().toString(36).slice(2);
                instance.waypoints.push({
                    id: waypointId,
                    name: 'Waypoint ' + (instance.waypoints.length + 1),
                    latitude: e.lngLat.lat,
                    longitude: e.lngLat.lng,
                    order: instance.waypoints.length + 1
                });
                self._reorderWaypoints(instance);
                self._refreshWaypoints(editorId);
            } else {
                instance.points.push({
                    latitude: e.lngLat.lat,
                    longitude: e.lngLat.lng,
                    order: instance.points.length + 1
                });
                self._refreshRoute(editorId);
            }
            self._notifyChange(editorId);
        });
    },

    destroy: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance) return;
        try {
            this._clearMarkers(instance.routeMarkers);
            this._clearMarkers(instance.midpointMarkers);
            this._clearMarkers(instance.waypointMarkers);
            if (instance.map) instance.map.remove();
        } catch {
            // ignore cleanup failures
        }
        delete this.instances[editorId];
    },

    setData: function (editorId, points, waypoints) {
        var instance = this.instances[editorId];
        if (!instance) return;

        instance.points = (points || []).map(function (p, idx) {
            return {
                latitude: p.latitude,
                longitude: p.longitude,
                order: idx + 1
            };
        });
        instance.waypoints = (waypoints || []).map(function (w, idx) {
            return {
                id: w.id || ((window.crypto && crypto.randomUUID) ? crypto.randomUUID() : Math.random().toString(36).slice(2)),
                name: w.name || '',
                latitude: w.latitude,
                longitude: w.longitude,
                order: idx + 1
            };
        });

        this._refreshRoute(editorId);
        this._refreshWaypoints(editorId);
        this._fit(editorId);
    },

    getData: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance) return { points: [], waypoints: [] };
        return {
            points: instance.points.map(function (p, idx) {
                return {
                    latitude: p.latitude,
                    longitude: p.longitude,
                    order: idx + 1
                };
            }),
            waypoints: instance.waypoints.map(function (w, idx) {
                return {
                    id: w.id,
                    name: w.name || '',
                    latitude: w.latitude,
                    longitude: w.longitude,
                    order: idx + 1
                };
            })
        };
    },

    setWaypointCreateMode: function (editorId, enabled) {
        var instance = this.instances[editorId];
        if (!instance) return;
        instance.addWaypointMode = !!enabled;
    },

    addWaypointAtCenter: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance || !instance.map) return;
        var center = instance.map.getCenter();
        var waypointId = (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : Math.random().toString(36).slice(2);
        instance.waypoints.push({
            id: waypointId,
            name: 'Waypoint ' + (instance.waypoints.length + 1),
            latitude: center.lat,
            longitude: center.lng,
            order: instance.waypoints.length + 1
        });
        this._reorderWaypoints(instance);
        this._refreshWaypoints(editorId);
        this._notifyChange(editorId);
    },

    renameWaypoint: function (editorId, waypointId, name) {
        var instance = this.instances[editorId];
        if (!instance) return;
        var waypoint = instance.waypoints.find(function (w) { return w.id === waypointId; });
        if (!waypoint) return;
        waypoint.name = name || '';
        this._notifyChange(editorId);
    },

    removeWaypoint: function (editorId, waypointId) {
        var instance = this.instances[editorId];
        if (!instance) return;
        instance.waypoints = instance.waypoints.filter(function (w) { return w.id !== waypointId; });
        this._reorderWaypoints(instance);
        this._refreshWaypoints(editorId);
        this._notifyChange(editorId);
    },

    clearRoute: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance) return;
        instance.points = [];
        this._refreshRoute(editorId);
        this._notifyChange(editorId);
    },

    _refreshRoute: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance || !instance.map) return;

        this._clearMarkers(instance.routeMarkers);
        this._clearMarkers(instance.midpointMarkers);
        instance.routeMarkers = [];
        instance.midpointMarkers = [];

        var coords = instance.points.map(function (p) { return [p.longitude, p.latitude]; });
        var source = instance.map.getSource(instance.sourceId);
        if (source) {
            source.setData({
                type: 'Feature',
                properties: {},
                geometry: { type: 'LineString', coordinates: coords }
            });
        }

        var self = this;
        instance.points.forEach(function (point, idx) {
            var markerEl = document.createElement('div');
            markerEl.style.width = '14px';
            markerEl.style.height = '14px';
            markerEl.style.borderRadius = '50%';
            markerEl.style.backgroundColor = '#FF5722';
            markerEl.style.border = '2px solid white';
            markerEl.style.boxShadow = '0 0 3px rgba(0,0,0,0.4)';
            markerEl.style.cursor = 'move';

            var marker = new maplibregl.Marker({ element: markerEl, draggable: true })
                .setLngLat([point.longitude, point.latitude])
                .addTo(instance.map);

            marker.on('dragend', function () {
                var lngLat = marker.getLngLat();
                instance.points[idx].latitude = lngLat.lat;
                instance.points[idx].longitude = lngLat.lng;
                self._refreshRoute(editorId);
                self._notifyChange(editorId);
            });

            instance.routeMarkers.push(marker);
        });

        for (var i = 0; i < instance.points.length - 1; i++) {
            var p1 = instance.points[i];
            var p2 = instance.points[i + 1];
            var midpointLng = (p1.longitude + p2.longitude) / 2;
            var midpointLat = (p1.latitude + p2.latitude) / 2;

            var midpointEl = document.createElement('div');
            midpointEl.style.width = '10px';
            midpointEl.style.height = '10px';
            midpointEl.style.borderRadius = '3px';
            midpointEl.style.backgroundColor = '#1976D2';
            midpointEl.style.border = '1px solid white';
            midpointEl.style.boxShadow = '0 0 2px rgba(0,0,0,0.35)';
            midpointEl.style.cursor = 'pointer';
            midpointEl.title = 'Zwischenpunkt einfügen';

            (function (insertIndex) {
                midpointEl.addEventListener('click', function (evt) {
                    evt.stopPropagation();
                    instance.points.splice(insertIndex, 0, {
                        latitude: midpointLat,
                        longitude: midpointLng,
                        order: insertIndex + 1
                    });
                    self._refreshRoute(editorId);
                    self._notifyChange(editorId);
                });
            })(i + 1);

            var midpointMarker = new maplibregl.Marker({ element: midpointEl })
                .setLngLat([midpointLng, midpointLat])
                .addTo(instance.map);
            instance.midpointMarkers.push(midpointMarker);
        }
    },

    _refreshWaypoints: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance || !instance.map) return;

        this._clearMarkers(instance.waypointMarkers);
        instance.waypointMarkers = [];

        var self = this;
        instance.waypoints.forEach(function (waypoint) {
            var markerEl = document.createElement('div');
            markerEl.style.width = '20px';
            markerEl.style.height = '20px';
            markerEl.style.borderRadius = '4px';
            markerEl.style.backgroundColor = '#1A73E8';
            markerEl.style.border = '2px solid white';
            markerEl.style.color = 'white';
            markerEl.style.fontSize = '11px';
            markerEl.style.fontWeight = 'bold';
            markerEl.style.display = 'flex';
            markerEl.style.alignItems = 'center';
            markerEl.style.justifyContent = 'center';
            markerEl.style.cursor = 'move';
            markerEl.textContent = 'W';

            var popup = new maplibregl.Popup({ offset: 10 }).setText(waypoint.name || 'Waypoint');
            var marker = new maplibregl.Marker({ element: markerEl, draggable: true })
                .setLngLat([waypoint.longitude, waypoint.latitude])
                .setPopup(popup)
                .addTo(instance.map);

            marker.on('dragend', function () {
                var lngLat = marker.getLngLat();
                waypoint.latitude = lngLat.lat;
                waypoint.longitude = lngLat.lng;
                self._notifyChange(editorId);
            });

            instance.waypointMarkers.push(marker);
        });
    },

    _notifyChange: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance || !instance.dotNetRef || !instance.callbackMethod) return;
        var data = this.getData(editorId);
        instance.dotNetRef.invokeMethodAsync(instance.callbackMethod, data);
    },

    _fit: function (editorId) {
        var instance = this.instances[editorId];
        if (!instance || !instance.map) return;
        var bounds = new maplibregl.LngLatBounds();
        var hasData = false;
        instance.points.forEach(function (p) {
            bounds.extend([p.longitude, p.latitude]);
            hasData = true;
        });
        instance.waypoints.forEach(function (w) {
            bounds.extend([w.longitude, w.latitude]);
            hasData = true;
        });
        if (hasData) {
            instance.map.fitBounds(bounds, { padding: 40, maxZoom: 14 });
        }
    },

    _reorderWaypoints: function (instance) {
        instance.waypoints.forEach(function (w, idx) { w.order = idx + 1; });
    },

    _clearMarkers: function (markers) {
        (markers || []).forEach(function (marker) {
            try { marker.remove(); } catch { }
        });
    }
};

// Leaflet.js Map Interop for Blazor

let mapInstance = null;
let markersLayer = null;
let routesLayer = null;
let gpxLayer = null;

window.mapInterop = {
    // Initialize the map
    initializeMap: function (elementId, latitude, longitude, zoom) {
        try {
            if (mapInstance) {
                mapInstance.remove();
            }

            // Create the map
            mapInstance = L.map(elementId).setView([latitude, longitude], zoom);

            // Add OpenStreetMap tiles
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                maxZoom: 19
            }).addTo(mapInstance);

            // Create feature groups for different layers
            markersLayer = L.featureGroup().addTo(mapInstance);
            routesLayer = L.featureGroup().addTo(mapInstance);
            gpxLayer = L.featureGroup().addTo(mapInstance);

            return true;
        } catch (error) {
            console.error('Error initializing map:', error);
            return false;
        }
    },

    // Add a marker to the map
    addMarker: function (id, latitude, longitude, title, category, color) {
        try {
            if (!mapInstance) return false;

            const icon = L.divIcon({
                className: 'custom-marker',
                html: `<div style="background-color: ${color || '#4285F4'}; width: 24px; height: 24px; border-radius: 50%; border: 2px solid white; box-shadow: 0 2px 6px rgba(0,0,0,0.3);"></div>`,
                iconSize: [24, 24],
                iconAnchor: [12, 12]
            });

            const marker = L.marker([latitude, longitude], { icon: icon })
                .bindPopup(`<b>${title}</b><br>${category}`)
                .addTo(markersLayer);

            marker._leaflet_id = id;
            return true;
        } catch (error) {
            console.error('Error adding marker:', error);
            return false;
        }
    },

    // Clear all markers
    clearMarkers: function () {
        try {
            if (markersLayer) {
                markersLayer.clearLayers();
            }
            return true;
        } catch (error) {
            console.error('Error clearing markers:', error);
            return false;
        }
    },

    // Add a route (polyline) to the map
    addRoute: function (coordinates, color, weight) {
        try {
            if (!mapInstance || !routesLayer) return false;

            const latlngs = coordinates.map(coord => [coord.latitude, coord.longitude]);
            
            L.polyline(latlngs, {
                color: color || '#FF5722',
                weight: weight || 3,
                opacity: 0.7
            }).addTo(routesLayer);

            return true;
        } catch (error) {
            console.error('Error adding route:', error);
            return false;
        }
    },

    // Clear all routes
    clearRoutes: function () {
        try {
            if (routesLayer) {
                routesLayer.clearLayers();
            }
            return true;
        } catch (error) {
            console.error('Error clearing routes:', error);
            return false;
        }
    },

    // Add GPX track to the map
    addGpxTrack: function (points, color) {
        try {
            if (!mapInstance || !gpxLayer) return false;

            const latlngs = points.map(p => [p.latitude, p.longitude]);
            
            L.polyline(latlngs, {
                color: color || '#9C27B0',
                weight: 2,
                opacity: 0.8
            }).addTo(gpxLayer);

            return true;
        } catch (error) {
            console.error('Error adding GPX track:', error);
            return false;
        }
    },

    // Clear all GPX tracks
    clearGpxTracks: function () {
        try {
            if (gpxLayer) {
                gpxLayer.clearLayers();
            }
            return true;
        } catch (error) {
            console.error('Error clearing GPX tracks:', error);
            return false;
        }
    },

    // Fit map bounds to show all markers
    fitBounds: function () {
        try {
            if (!mapInstance) return false;

            const allLayers = [];
            if (markersLayer && markersLayer.getLayers().length > 0) {
                allLayers.push(...markersLayer.getLayers());
            }
            if (routesLayer && routesLayer.getLayers().length > 0) {
                allLayers.push(...routesLayer.getLayers());
            }
            if (gpxLayer && gpxLayer.getLayers().length > 0) {
                allLayers.push(...gpxLayer.getLayers());
            }

            if (allLayers.length > 0) {
                const group = L.featureGroup(allLayers);
                mapInstance.fitBounds(group.getBounds().pad(0.1));
            }

            return true;
        } catch (error) {
            console.error('Error fitting bounds:', error);
            return false;
        }
    },

    // Set map view to specific location
    setView: function (latitude, longitude, zoom) {
        try {
            if (!mapInstance) return false;
            mapInstance.setView([latitude, longitude], zoom);
            return true;
        } catch (error) {
            console.error('Error setting view:', error);
            return false;
        }
    },

    // Toggle layer visibility
    toggleLayer: function (layerName, visible) {
        try {
            if (!mapInstance) return false;

            let layer = null;
            switch (layerName) {
                case 'markers':
                    layer = markersLayer;
                    break;
                case 'routes':
                    layer = routesLayer;
                    break;
                case 'gpx':
                    layer = gpxLayer;
                    break;
            }

            if (layer) {
                if (visible && !mapInstance.hasLayer(layer)) {
                    mapInstance.addLayer(layer);
                } else if (!visible && mapInstance.hasLayer(layer)) {
                    mapInstance.removeLayer(layer);
                }
            }

            return true;
        } catch (error) {
            console.error('Error toggling layer:', error);
            return false;
        }
    },

    // Invalidate map size (call after container resize)
    invalidateSize: function () {
        try {
            if (mapInstance) {
                setTimeout(() => {
                    mapInstance.invalidateSize();
                }, 100);
            }
            return true;
        } catch (error) {
            console.error('Error invalidating size:', error);
            return false;
        }
    },

    // Destroy the map
    destroyMap: function () {
        try {
            if (mapInstance) {
                mapInstance.remove();
                mapInstance = null;
                markersLayer = null;
                routesLayer = null;
                gpxLayer = null;
            }
            return true;
        } catch (error) {
            console.error('Error destroying map:', error);
            return false;
        }
    }
};

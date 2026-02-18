// Drag and Drop Interop for Blazor

window.dragDropInterop = {
    // Enable drag and drop on an element
    enableDragDrop: function (elementId) {
        try {
            const element = document.getElementById(elementId);
            if (!element) {
                console.error('Element not found:', elementId);
                return false;
            }

            // Make element draggable
            element.setAttribute('draggable', 'true');

            // Drag start event
            element.addEventListener('dragstart', function (e) {
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', elementId);
                element.classList.add('dragging');
            });

            // Drag end event
            element.addEventListener('dragend', function (e) {
                element.classList.remove('dragging');
            });

            return true;
        } catch (error) {
            console.error('Error enabling drag and drop:', error);
            return false;
        }
    },

    // Enable drop zone
    enableDropZone: function (elementId, dotNetHelper, methodName) {
        try {
            const element = document.getElementById(elementId);
            if (!element) {
                console.error('Element not found:', elementId);
                return false;
            }

            element.classList.add('drop-zone');

            // Drag over event
            element.addEventListener('dragover', function (e) {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
                element.classList.add('drag-over');
            });

            // Drag leave event
            element.addEventListener('dragleave', function (e) {
                element.classList.remove('drag-over');
            });

            // Drop event
            element.addEventListener('drop', function (e) {
                e.preventDefault();
                element.classList.remove('drag-over');
                
                const draggedElementId = e.dataTransfer.getData('text/plain');
                const targetElementId = elementId;
                
                // Call back to Blazor
                if (dotNetHelper && methodName) {
                    dotNetHelper.invokeMethodAsync(methodName, draggedElementId, targetElementId);
                }
            });

            return true;
        } catch (error) {
            console.error('Error enabling drop zone:', error);
            return false;
        }
    },

    // Clean up drag and drop
    cleanupDragDrop: function (elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.removeAttribute('draggable');
                element.classList.remove('dragging', 'drag-over', 'drop-zone');
            }
            return true;
        } catch (error) {
            console.error('Error cleaning up drag and drop:', error);
            return false;
        }
    }
};

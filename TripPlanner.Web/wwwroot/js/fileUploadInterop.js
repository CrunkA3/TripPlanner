// File Upload Interop for Blazor

window.fileUploadInterop = {
    // Read file as text
    readFileAsText: async function (fileInputElement) {
        try {
            if (!fileInputElement || !fileInputElement.files || fileInputElement.files.length === 0) {
                return null;
            }

            const file = fileInputElement.files[0];
            
            return new Promise((resolve, reject) => {
                const reader = new FileReader();
                
                reader.onload = function (e) {
                    resolve({
                        fileName: file.name,
                        fileSize: file.size,
                        fileType: file.type,
                        content: e.target.result
                    });
                };
                
                reader.onerror = function () {
                    reject(new Error('Failed to read file'));
                };
                
                reader.readAsText(file);
            });
        } catch (error) {
            console.error('Error reading file:', error);
            return null;
        }
    },

    // Reset file input
    resetFileInput: function (fileInputElement) {
        try {
            if (fileInputElement) {
                fileInputElement.value = '';
            }
            return true;
        } catch (error) {
            console.error('Error resetting file input:', error);
            return false;
        }
    }
};

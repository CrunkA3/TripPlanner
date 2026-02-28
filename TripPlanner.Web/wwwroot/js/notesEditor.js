window.notesEditor = (function () {
    'use strict';

    const _pasteHandlers = {};

    function init(editorId, dotNetHelper) {
        const el = document.getElementById(editorId);
        if (!el) return;

        const handler = async function (e) {
            const items = e.clipboardData && e.clipboardData.items;
            if (!items) return;
            for (let i = 0; i < items.length; i++) {
                const item = items[i];
                if (item.type.startsWith('image/')) {
                    e.preventDefault();
                    const file = item.getAsFile();
                    if (!file) continue;
                    const reader = new FileReader();
                    reader.onload = async function (evt) {
                        await dotNetHelper.invokeMethodAsync('InsertImageAtCursor', evt.target.result);
                    };
                    reader.readAsDataURL(file);
                    break;
                }
            }
        };

        _pasteHandlers[editorId] = handler;
        el.addEventListener('paste', handler);
    }

    function setValue(editorId, value) {
        const el = document.getElementById(editorId);
        if (el) el.value = value || '';
    }

    function getValue(editorId) {
        const el = document.getElementById(editorId);
        return el ? el.value : '';
    }

    function insertAtCursor(editorId, text) {
        const el = document.getElementById(editorId);
        if (!el) return;
        const start = el.selectionStart;
        const end = el.selectionEnd;
        const current = el.value;
        el.value = current.substring(0, start) + text + current.substring(end);
        el.selectionStart = el.selectionEnd = start + text.length;
        el.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function sanitizeHtml(html) {
        if (typeof DOMPurify !== 'undefined') {
            return DOMPurify.sanitize(html, { ADD_TAGS: ['foreignObject'] });
        }
        // Fallback: strip script tags when DOMPurify is unavailable
        const tmp = document.createElement('div');
        tmp.innerHTML = html;
        tmp.querySelectorAll('script, iframe, object, embed').forEach(el => el.remove());
        return tmp.innerHTML;
    }

    async function renderPreview(previewId, markdown) {
        const el = document.getElementById(previewId);
        if (!el) return;

        if (typeof marked === 'undefined') {
            console.warn('marked.js not loaded; showing raw Markdown text.');
            el.textContent = markdown || '';
            return;
        }

        const rawHtml = marked.parse(markdown || '');
        el.innerHTML = sanitizeHtml(rawHtml);

        if (typeof mermaid !== 'undefined') {
            const blocks = el.querySelectorAll('pre > code.language-mermaid');
            for (let i = 0; i < blocks.length; i++) {
                const block = blocks[i];
                const code = block.textContent;
                const mermaidDiv = document.createElement('div');
                mermaidDiv.className = 'mermaid';
                mermaidDiv.textContent = code;
                block.parentElement.replaceWith(mermaidDiv);
                try {
                    await mermaid.run({ nodes: [mermaidDiv] });
                } catch (err) {
                    mermaidDiv.textContent = 'Mermaid Error: ' + err.message;
                }
            }
        }
    }

    function dispose(editorId) {
        const el = document.getElementById(editorId);
        if (el && _pasteHandlers[editorId]) {
            el.removeEventListener('paste', _pasteHandlers[editorId]);
            delete _pasteHandlers[editorId];
        }
    }

    return { init, setValue, getValue, insertAtCursor, renderPreview, dispose };
})();

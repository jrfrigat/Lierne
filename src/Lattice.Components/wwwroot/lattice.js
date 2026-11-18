// Browser helpers for the Lattice host. Loaded as an ES module so the components can import it from
// their own static web assets and keep the host free of global script names.

export function downloadText(fileName, text, mimeType) {
    const blob = new Blob([text], { type: mimeType || "text/plain" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "document.txt";
    link.click();
    window.setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
}

const layoutWatches = new Map();

export function watchLayoutSizes(id, callback) {
    unwatchLayoutSizes(id);
    const root = document.getElementById(id);
    if (!root) return;

    const selector = ".flare-ide-layout__resizer[data-region]";
    let timer;
    const report = () => {
        const readSize = region => {
            const value = root.querySelector(`${selector}[data-region="${region}"]`)?.getAttribute("aria-valuenow");
            const size = Number(value);
            return Number.isInteger(size) && size > 0 ? size : null;
        };
        try {
            callback.invokeMethodAsync("OnPanelSizesChanged", readSize("Left"), readSize("Right"), readSize("Bottom"))
                .catch(() => {});
        } catch {
            // A server circuit can disconnect before the interop call returns a promise.
        }
    };
    const observer = new MutationObserver(changes => {
        if (changes.some(change => change.target.closest("[data-flare-ide-dock-scope]") === root &&
            change.target.matches(selector))) {
            clearTimeout(timer);
            timer = setTimeout(report, 150);
        }
    });
    observer.observe(root, { subtree: true, attributes: true, attributeFilter: ["aria-valuenow"] });
    layoutWatches.set(id, () => {
        clearTimeout(timer);
        observer.disconnect();
    });
}

export function unwatchLayoutSizes(id) {
    layoutWatches.get(id)?.();
    layoutWatches.delete(id);
}

// Browser helpers for the Lierne host. Loaded as an ES module so the components can import it from
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

const workspaceWatches = new Map();

export function watchWorkspaceSize(id, callback) {
    unwatchWorkspaceSize(id);
    const root = document.getElementById(id);
    if (!root) return;

    let frame;
    const report = () => {
        const rect = root.getBoundingClientRect();
        try {
            callback.invokeMethodAsync("OnViewportChanged", rect.width, rect.height).catch(() => {});
        } catch {
            // A server circuit can disconnect before the interop call returns a promise.
        }
    };
    const observer = new ResizeObserver(() => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(report);
    });
    observer.observe(root);
    report();
    workspaceWatches.set(id, () => {
        cancelAnimationFrame(frame);
        observer.disconnect();
    });
}

export function unwatchWorkspaceSize(id) {
    workspaceWatches.get(id)?.();
    workspaceWatches.delete(id);
}

// Returns the two pane lengths along the split axis, so .NET can turn the splitter's pixel
// report into the persisted first-branch share without knowing the container size in advance.
export function measureSplit(first, second, axis) {
    if (!first || !second) return [0, 0];
    const property = axis === "horizontal" ? "width" : "height";
    return [first.getBoundingClientRect()[property], second.getBoundingClientRect()[property]];
}

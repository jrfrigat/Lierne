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

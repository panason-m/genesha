import mermaid from "mermaid";
import { onNativeMessage, postToNative, type LayoutNode } from "./bridge";

mermaid.initialize({ startOnLoad: false, theme: "dark" });

const rootEl = document.getElementById("root") as HTMLDivElement;
const sourcePane = document.getElementById("source-pane") as HTMLDivElement;
const sourceEl = document.getElementById("source") as HTMLTextAreaElement;
const splitterEl = document.getElementById("splitter") as HTMLDivElement;
const viewportEl = document.getElementById("preview-viewport") as HTMLDivElement;
const previewEl = document.getElementById("preview") as HTMLDivElement;
const collapseBtn = document.getElementById("collapse-btn") as HTMLButtonElement;
const expandBtn = document.getElementById("expand-btn") as HTMLButtonElement;
const resetLayoutBtn = document.getElementById("reset-layout-btn") as HTMLButtonElement;
const zoomInBtn = document.getElementById("zoom-in-btn") as HTMLButtonElement;
const zoomOutBtn = document.getElementById("zoom-out-btn") as HTMLButtonElement;
const zoomResetBtn = document.getElementById("zoom-reset-btn") as HTMLButtonElement;
const exportPngBtn = document.getElementById("export-png-btn") as HTMLButtonElement;
const exportStatusEl = document.getElementById("export-status") as HTMLSpanElement;

// --- Diagram rendering --------------------------------------------------

let renderSeq = 0;
let renderDebounceHandle: number | undefined;

async function renderPreview(text: string): Promise<void> {
  const id = `mermaid-preview-${++renderSeq}`;
  try {
    const { svg } = await mermaid.render(id, text || "flowchart TD");
    previewEl.classList.remove("error");
    previewEl.innerHTML = svg;
    const svgEl = previewEl.querySelector("svg");
    if (svgEl) setupInteractiveLayout(svgEl as unknown as SVGSVGElement);
  } catch (err) {
    previewEl.classList.add("error");
    previewEl.textContent = err instanceof Error ? err.message : String(err);
  }
}

// --- Draggable nodes/subgraphs + programmatic layout inspection --------
//
// Mermaid's auto-layout diagram types (flowchart, etc.) don't carry explicit
// coordinates in the source text, so dragging can't be written back into the
// Mermaid syntax itself. Instead this gives a manual visual override on top
// of the rendered SVG (cleared by "Reset layout" or by any new render), and
// reports every node/subgraph's rendered bounding box back to the native
// host after every render and drag, so a tool/assistant can inspect the
// actual layout without needing a screenshot.

function getTranslate(el: SVGGraphicsElement): { x: number; y: number } {
  const match = /translate\(\s*([-\d.]+)[ ,]+([-\d.]+)\s*\)/.exec(el.getAttribute("transform") ?? "");
  return match ? { x: parseFloat(match[1]), y: parseFloat(match[2]) } : { x: 0, y: 0 };
}

function setTranslate(el: SVGGraphicsElement, x: number, y: number): void {
  const rest = (el.getAttribute("transform") ?? "").replace(/translate\([^)]*\)\s*/, "").trim();
  el.setAttribute("transform", `translate(${x} ${y}) ${rest}`.trim());
}

function labelOf(el: SVGGraphicsElement): string {
  const labelEl = el.querySelector(".nodeLabel, .cluster-label, .clusterLabel, text");
  return labelEl?.textContent?.trim() ?? "";
}

function computeLayout(svgEl: SVGSVGElement): LayoutNode[] {
  const nodes: LayoutNode[] = [];
  for (const el of Array.from(svgEl.querySelectorAll("g.node, g.cluster")) as SVGGraphicsElement[]) {
    let box: DOMRect;
    try {
      box = el.getBBox();
    } catch {
      continue;
    }
    nodes.push({
      id: el.id || "",
      label: labelOf(el),
      kind: el.classList.contains("cluster") ? "cluster" : "node",
      x: Math.round(box.x),
      y: Math.round(box.y),
      width: Math.round(box.width),
      height: Math.round(box.height),
    });
  }
  return nodes;
}

function reportLayout(svgEl: SVGSVGElement): void {
  postToNative({ type: "layout", payload: { nodes: computeLayout(svgEl) } });
}

function attachDrag(handle: SVGGraphicsElement, moving: SVGGraphicsElement[], svgEl: SVGSVGElement): void {
  let dragging = false;
  let startX = 0;
  let startY = 0;
  const startOffsets = new Map<SVGGraphicsElement, { x: number; y: number }>();

  handle.addEventListener("pointerdown", (e) => {
    e.stopPropagation();
    dragging = true;
    startX = e.clientX;
    startY = e.clientY;
    for (const el of moving) startOffsets.set(el, getTranslate(el));
    handle.classList.add("dragging");
    handle.setPointerCapture(e.pointerId);
  });

  handle.addEventListener("pointermove", (e) => {
    if (!dragging) return;
    e.stopPropagation();
    const dx = (e.clientX - startX) / scale;
    const dy = (e.clientY - startY) / scale;
    for (const el of moving) {
      const start = startOffsets.get(el)!;
      setTranslate(el, start.x + dx, start.y + dy);
    }
  });

  function endDrag(e: PointerEvent): void {
    if (!dragging) return;
    e.stopPropagation();
    dragging = false;
    handle.classList.remove("dragging");
    reportLayout(svgEl);
  }

  handle.addEventListener("pointerup", endDrag);
  handle.addEventListener("pointercancel", endDrag);
}

function setupInteractiveLayout(svgEl: SVGSVGElement): void {
  const nodeGroups = Array.from(svgEl.querySelectorAll("g.node")) as SVGGraphicsElement[];
  const clusterGroups = Array.from(svgEl.querySelectorAll("g.cluster")) as SVGGraphicsElement[];

  const clusterMembers = new Map<SVGGraphicsElement, SVGGraphicsElement[]>();
  for (const cluster of clusterGroups) {
    let cb: DOMRect;
    try {
      cb = cluster.getBBox();
    } catch {
      continue;
    }
    const members = nodeGroups.filter((node) => {
      try {
        const nb = node.getBBox();
        const cx = nb.x + nb.width / 2;
        const cy = nb.y + nb.height / 2;
        return cx >= cb.x && cx <= cb.x + cb.width && cy >= cb.y && cy <= cb.y + cb.height;
      } catch {
        return false;
      }
    });
    clusterMembers.set(cluster, members);
  }
  const clusteredNodes = new Set(Array.from(clusterMembers.values()).flat());

  for (const cluster of clusterGroups) attachDrag(cluster, [cluster, ...(clusterMembers.get(cluster) ?? [])], svgEl);
  for (const node of nodeGroups) if (!clusteredNodes.has(node)) attachDrag(node, [node], svgEl);

  reportLayout(svgEl);
}

resetLayoutBtn.addEventListener("click", () => {
  void renderPreview(sourceEl.value);
});

onNativeMessage((message) => {
  if (message.type !== "getLayout") return;
  const svgEl = previewEl.querySelector("svg");
  if (svgEl) reportLayout(svgEl as unknown as SVGSVGElement);
});

// --- Export the rendered diagram as PNG ---------------------------------

async function blobToBase64(blob: Blob): Promise<string> {
  const buffer = await blob.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.byteLength; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary);
}

interface ForeignObjectLabel {
  x: number;
  y: number;
  width: number;
  height: number;
  text: string;
  fontSize: string;
  fontFamily: string;
  color: string;
}

// Read plain text + computed style from each foreignObject in the *live* (attached, styled) SVG —
// getComputedStyle only works on elements attached to the document, so this has to run before
// the SVG is cloned. Order matches svgEl.querySelectorAll("foreignObject") 1:1 with the clone's,
// since cloneNode(true) preserves document order exactly.
function collectForeignObjectLabels(svgEl: SVGSVGElement): ForeignObjectLabel[] {
  return Array.from(svgEl.querySelectorAll("foreignObject")).map((fo) => {
    const foEl = fo as SVGForeignObjectElement;
    const htmlRoot = foEl.firstElementChild as HTMLElement | null;
    const computed = htmlRoot ? getComputedStyle(htmlRoot) : null;
    return {
      x: foEl.x.baseVal.value,
      y: foEl.y.baseVal.value,
      width: foEl.width.baseVal.value,
      height: foEl.height.baseVal.value,
      text: (foEl.textContent ?? "").replace(/\s+/g, " ").trim(),
      fontSize: computed?.fontSize || "14px",
      fontFamily: computed?.fontFamily || "trebuchet ms, verdana, arial, sans-serif",
      color: computed?.color || "#000000",
    };
  });
}

function wrapTextToWidth(text: string, maxWidth: number, font: string): string[] {
  if (!text) return [];
  const ctx = document.createElement("canvas").getContext("2d")!;
  ctx.font = font;

  const lines: string[] = [];
  let current = "";
  for (const word of text.split(" ")) {
    const candidate = current ? `${current} ${word}` : word;
    if (current && ctx.measureText(candidate).width > maxWidth) {
      lines.push(current);
      current = word;
    } else {
      current = candidate;
    }
  }
  if (current) lines.push(current);
  return lines;
}

function replaceForeignObjectsWithText(clonedSvg: SVGSVGElement, labels: ForeignObjectLabel[]): void {
  const foreignObjects = Array.from(clonedSvg.querySelectorAll("foreignObject"));
  foreignObjects.forEach((fo, i) => {
    const label = labels[i];
    if (!label) {
      fo.remove();
      return;
    }

    const font = `${label.fontSize} ${label.fontFamily}`;
    const lines = wrapTextToWidth(label.text, label.width, font);
    const lineHeight = parseFloat(label.fontSize) * 1.25 || 16;

    const textEl = document.createElementNS("http://www.w3.org/2000/svg", "text");
    textEl.setAttribute("text-anchor", "middle");
    textEl.setAttribute("font-size", label.fontSize);
    textEl.setAttribute("font-family", label.fontFamily);
    textEl.setAttribute("fill", label.color);

    const startY = label.y + label.height / 2 - ((lines.length - 1) * lineHeight) / 2 + lineHeight * 0.32;
    lines.forEach((line, li) => {
      const tspan = document.createElementNS("http://www.w3.org/2000/svg", "tspan");
      tspan.setAttribute("x", String(label.x + label.width / 2));
      tspan.setAttribute("y", String(startY + li * lineHeight));
      tspan.textContent = line;
      textEl.appendChild(tspan);
    });

    fo.replaceWith(textEl);
  });
}

async function exportPreviewAsPng(): Promise<void> {
  const svgEl = previewEl.querySelector("svg") as SVGSVGElement | null;
  if (!svgEl) return;

  exportPngBtn.disabled = true;
  exportStatusEl.classList.remove("error");
  exportStatusEl.textContent = "Exporting…";

  // Mermaid renders node/edge labels as <foreignObject><div>…</div></foreignObject>. Chromium
  // taints any canvas that ever drew an SVG containing a foreignObject (it can't statically prove
  // the embedded HTML has no external references), which makes canvas.toBlob() throw. Since the
  // labels are already read from computed styles here, replace them with plain SVG <text> in the
  // exported copy only — the live, draggable preview keeps its HTML labels untouched.
  const labels = collectForeignObjectLabels(svgEl);
  const clonedSvg = svgEl.cloneNode(true) as SVGSVGElement;
  replaceForeignObjectsWithText(clonedSvg, labels);

  const width = svgEl.viewBox.baseVal.width || svgEl.clientWidth;
  const height = svgEl.viewBox.baseVal.height || svgEl.clientHeight;
  clonedSvg.setAttribute("width", String(width));
  clonedSvg.setAttribute("height", String(height));

  const svgString = new XMLSerializer().serializeToString(clonedSvg);
  const svgUrl = URL.createObjectURL(new Blob([svgString], { type: "image/svg+xml;charset=utf-8" }));

  try {
    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error("Failed to rasterize diagram"));
      img.src = svgUrl;
    });

    const scale = 2;
    const canvas = document.createElement("canvas");
    canvas.width = Math.ceil(width * scale);
    canvas.height = Math.ceil(height * scale);
    const ctx = canvas.getContext("2d")!;
    ctx.fillStyle = "#1e1e1e";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.scale(scale, scale);
    ctx.drawImage(img, 0, 0, width, height);

    const pngBlob: Blob = await new Promise((resolve, reject) =>
      canvas.toBlob((b) => (b ? resolve(b) : reject(new Error("Failed to encode PNG"))), "image/png"),
    );
    const pngBase64 = await blobToBase64(pngBlob);

    postToNative({
      type: "exportPng",
      payload: { requestId: crypto.randomUUID(), pngBase64, width: canvas.width, height: canvas.height },
    });
  } catch (err) {
    exportPngBtn.disabled = false;
    exportStatusEl.classList.add("error");
    exportStatusEl.textContent = err instanceof Error ? err.message : String(err);
  } finally {
    URL.revokeObjectURL(svgUrl);
  }
}

exportPngBtn.addEventListener("click", () => {
  void exportPreviewAsPng();
});

onNativeMessage((message) => {
  if (message.type !== "exportPngResult") return;
  exportPngBtn.disabled = false;
  const { ok, fileName, error } = message.payload;
  if (!ok && error === "cancelled") {
    exportStatusEl.classList.remove("error");
    exportStatusEl.textContent = "";
    return;
  }
  exportStatusEl.classList.toggle("error", !ok);
  exportStatusEl.textContent = ok ? `Exported ${fileName}` : (error ?? "Export failed");
});

onNativeMessage((message) => {
  if (message.type !== "loadContent") return;
  sourceEl.value = message.payload.text;
  void renderPreview(message.payload.text);
});

sourceEl.addEventListener("input", () => {
  window.clearTimeout(renderDebounceHandle);
  renderDebounceHandle = window.setTimeout(() => {
    const text = sourceEl.value;
    void renderPreview(text);
    postToNative({ type: "contentChanged", payload: { text } });
  }, 400);
});

// --- Zoom & pan on the preview ------------------------------------------

let scale = 1;
let panX = 0;
let panY = 0;

function applyTransform(): void {
  previewEl.style.transform = `translate(-50%, -50%) translate(${panX}px, ${panY}px) scale(${scale})`;
  zoomResetBtn.textContent = `${Math.round(scale * 100)}%`;
}

function setZoom(nextScale: number): void {
  scale = Math.min(5, Math.max(0.1, nextScale));
  applyTransform();
}

function resetView(): void {
  panX = 0;
  panY = 0;
  scale = 1;
  applyTransform();
}

zoomInBtn.addEventListener("click", () => setZoom(scale * 1.25));
zoomOutBtn.addEventListener("click", () => setZoom(scale / 1.25));
zoomResetBtn.addEventListener("click", resetView);

viewportEl.addEventListener(
  "wheel",
  (e) => {
    e.preventDefault();
    setZoom(scale * (e.deltaY < 0 ? 1.1 : 1 / 1.1));
  },
  { passive: false },
);

let isPanning = false;
let panPointerId: number | null = null;
let panStartX = 0;
let panStartY = 0;
let panOriginX = 0;
let panOriginY = 0;

viewportEl.addEventListener("pointerdown", (e) => {
  isPanning = true;
  panPointerId = e.pointerId;
  panStartX = e.clientX;
  panStartY = e.clientY;
  panOriginX = panX;
  panOriginY = panY;
  viewportEl.classList.add("panning");
  document.body.classList.add("dragging");
  viewportEl.setPointerCapture(e.pointerId);
});

viewportEl.addEventListener("pointermove", (e) => {
  if (!isPanning || e.pointerId !== panPointerId) return;
  panX = panOriginX + (e.clientX - panStartX);
  panY = panOriginY + (e.clientY - panStartY);
  applyTransform();
});

function endPan(e: PointerEvent): void {
  if (e.pointerId !== panPointerId) return;
  isPanning = false;
  panPointerId = null;
  viewportEl.classList.remove("panning");
  document.body.classList.remove("dragging");
}

viewportEl.addEventListener("pointerup", endPan);
viewportEl.addEventListener("pointercancel", endPan);
viewportEl.addEventListener("lostpointercapture", endPan);

applyTransform();

// --- Collapse / expand the code pane -------------------------------------

collapseBtn.addEventListener("click", () => {
  sourcePane.classList.add("collapsed");
  splitterEl.classList.add("collapsed");
  expandBtn.style.display = "inline-block";
});

expandBtn.addEventListener("click", () => {
  sourcePane.classList.remove("collapsed");
  splitterEl.classList.remove("collapsed");
  expandBtn.style.display = "none";
});

// --- Resizable split between code and preview -----------------------------

let isResizing = false;

splitterEl.addEventListener("pointerdown", (e) => {
  isResizing = true;
  splitterEl.classList.add("dragging");
  document.body.classList.add("dragging");
  splitterEl.setPointerCapture(e.pointerId);
});

splitterEl.addEventListener("pointermove", (e) => {
  if (!isResizing) return;
  const rootRect = rootEl.getBoundingClientRect();
  const minWidth = 160;
  const maxWidth = rootRect.width - 160;
  const newWidth = Math.min(maxWidth, Math.max(minWidth, e.clientX - rootRect.left));
  sourcePane.style.width = `${newWidth}px`;
});

function endResize(): void {
  isResizing = false;
  splitterEl.classList.remove("dragging");
  document.body.classList.remove("dragging");
}

splitterEl.addEventListener("pointerup", endResize);
splitterEl.addEventListener("pointercancel", endResize);
splitterEl.addEventListener("lostpointercapture", endResize);

postToNative({ type: "ready", payload: {} });

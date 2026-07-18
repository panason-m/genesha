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

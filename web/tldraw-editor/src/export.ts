import type { Editor, TLArrowShape, TLGeoShape, TLShape, TLShapeId } from "tldraw";
import { getArrowBindings, renderPlaintextFromRichText } from "tldraw";
import { postToNative } from "./bridge";

export interface FlowValidationResult {
  valid: boolean;
  reason?: string;
}

export function getGroupDescendantShapes(editor: Editor, groupId: TLShapeId): TLShape[] {
  const ids = editor.getShapeAndDescendantIds([groupId]);
  const shapes: TLShape[] = [];
  for (const id of ids) {
    if (id === groupId) continue;
    const shape = editor.getShape(id);
    if (shape) shapes.push(shape);
  }
  return shapes;
}

export function validateFlowGroup(editor: Editor, groupId: TLShapeId): FlowValidationResult {
  const descendants = getGroupDescendantShapes(editor, groupId);

  const otherTypes = new Set(
    descendants.filter((shape) => shape.type !== "geo" && shape.type !== "arrow").map((shape) => shape.type),
  );
  if (otherTypes.size > 0) {
    return {
      valid: false,
      reason: `Flow groups can only contain shapes and arrows. Remove: ${[...otherTypes].join(", ")}.`,
    };
  }

  const geoCount = descendants.filter((shape) => shape.type === "geo").length;
  if (geoCount === 0) {
    return { valid: false, reason: "Flow group needs at least one shape." };
  }

  return { valid: true };
}

function sanitizeMermaidId(shapeId: string): string {
  return shapeId.replace(/^shape:/, "").replace(/[^A-Za-z0-9_]/g, "_");
}

function escapeMermaidLabel(label: string): string {
  return label.replace(/"/g, "'").replace(/\r?\n/g, " ").trim();
}

function byIndex(a: TLShape, b: TLShape): number {
  return a.index < b.index ? -1 : a.index > b.index ? 1 : 0;
}

export type MermaidBuildResult = { ok: true; text: string } | { ok: false; reason: string };

export function buildMermaidFromFlowGroup(editor: Editor, groupId: TLShapeId): MermaidBuildResult {
  const validation = validateFlowGroup(editor, groupId);
  if (!validation.valid) return { ok: false, reason: validation.reason! };

  const descendants = getGroupDescendantShapes(editor, groupId);
  const geoShapes = descendants.filter((shape): shape is TLGeoShape => shape.type === "geo").sort(byIndex);
  const arrowShapes = descendants.filter((shape): shape is TLArrowShape => shape.type === "arrow").sort(byIndex);
  const geoIds = new Set<TLShapeId>(geoShapes.map((shape) => shape.id));

  const lines: string[] = ["flowchart TD"];

  for (const shape of geoShapes) {
    const nodeId = sanitizeMermaidId(shape.id);
    const label = escapeMermaidLabel(renderPlaintextFromRichText(editor, shape.props.richText));
    lines.push(`  ${nodeId}["${label || "Shape"}"]`);
  }

  for (const arrow of arrowShapes) {
    const bindings = getArrowBindings(editor, arrow);
    const sourceId = bindings.start?.toId;
    const targetId = bindings.end?.toId;
    if (!sourceId || !targetId) continue;
    if (!geoIds.has(sourceId) || !geoIds.has(targetId)) continue;

    const from = sanitizeMermaidId(sourceId);
    const to = sanitizeMermaidId(targetId);
    const label = escapeMermaidLabel(arrow.props.text);
    lines.push(label ? `  ${from} -->|${label}| ${to}` : `  ${from} --> ${to}`);
  }

  return { ok: true, text: lines.join("\n") + "\n" };
}

async function blobToBase64(blob: Blob): Promise<string> {
  const buffer = await blob.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.byteLength; i++) binary += String.fromCharCode(bytes[i]);
  return btoa(binary);
}

export async function exportGroupAsPng(editor: Editor, groupId: TLShapeId): Promise<void> {
  const result = await editor.toImage([groupId], { format: "png", background: true });
  const pngBase64 = await blobToBase64(result.blob);
  postToNative({
    type: "exportGroupPng",
    payload: {
      requestId: crypto.randomUUID(),
      groupId,
      pngBase64,
      width: result.width,
      height: result.height,
    },
  });
}

export function exportFlowAsMermaid(editor: Editor, groupId: TLShapeId): MermaidBuildResult {
  const result = buildMermaidFromFlowGroup(editor, groupId);
  if (!result.ok) return result;

  postToNative({
    type: "exportFlowMermaid",
    payload: {
      requestId: crypto.randomUUID(),
      groupId,
      mermaidText: result.text,
    },
  });
  return result;
}

import {
  DefaultContextMenu,
  DefaultContextMenuContent,
  TLUiContextMenuProps,
  TldrawUiMenuGroup,
  TldrawUiMenuItem,
  track,
  useEditor,
  useToasts,
} from "tldraw";
import { exportFlowAsMermaid, exportGroupAsPng, validateFlowGroup } from "./export";

export const CustomContextMenu = track(function CustomContextMenu(props: TLUiContextMenuProps) {
  const editor = useEditor();
  const toasts = useToasts();

  const selectedShapes = editor.getSelectedShapes();
  const groupShape = selectedShapes.length === 1 && selectedShapes[0].type === "group" ? selectedShapes[0] : null;
  const isFlow = groupShape?.meta.flow === true;

  return (
    <DefaultContextMenu {...props}>
      {groupShape && (
        <TldrawUiMenuGroup id="genesha-export">
          <TldrawUiMenuItem
            id="genesha-export-png"
            label="Export Group as PNG"
            onSelect={async () => {
              try {
                await exportGroupAsPng(editor, groupShape.id);
              } catch (err) {
                toasts.addToast({
                  severity: "error",
                  title: "Export failed",
                  description: err instanceof Error ? err.message : String(err),
                });
              }
            }}
          />
          <TldrawUiMenuItem
            id="genesha-toggle-flow"
            label={isFlow ? "Remove Flow Tag" : "Mark as Flow"}
            onSelect={() => {
              if (!isFlow) {
                const validation = validateFlowGroup(editor, groupShape.id);
                if (!validation.valid) {
                  toasts.addToast({
                    severity: "error",
                    title: "Cannot mark as flow",
                    description: validation.reason,
                  });
                  return;
                }
              }
              editor.updateShape({
                id: groupShape.id,
                type: "group",
                meta: { ...groupShape.meta, flow: !isFlow },
              });
            }}
          />
          {isFlow && (
            <TldrawUiMenuItem
              id="genesha-export-mermaid"
              label="Export Flow as Mermaid"
              onSelect={() => {
                const result = exportFlowAsMermaid(editor, groupShape.id);
                if (!result.ok) {
                  toasts.addToast({
                    severity: "error",
                    title: "Cannot export flow",
                    description: result.reason,
                  });
                }
              }}
            />
          )}
        </TldrawUiMenuGroup>
      )}
      <DefaultContextMenuContent />
    </DefaultContextMenu>
  );
});

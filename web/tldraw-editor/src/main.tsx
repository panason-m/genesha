import { StrictMode, useCallback, useEffect, useRef } from "react";
import { createRoot } from "react-dom/client";
import { Tldraw, Editor, getSnapshot, loadSnapshot, useToasts } from "tldraw";
import "tldraw/tldraw.css";
import { onNativeMessage, postToNative } from "./bridge";
import { CustomContextMenu } from "./CustomContextMenu";

function NativeExportResultToasts() {
  const toasts = useToasts();

  useEffect(() => {
    onNativeMessage((message) => {
      if (message.type === "exportGroupPngResult") {
        const { ok, fileName, error } = message.payload;
        toasts.addToast(
          ok
            ? { severity: "success", title: "PNG exported", description: fileName }
            : { severity: "error", title: "PNG export failed", description: error },
        );
      } else if (message.type === "exportFlowMermaidResult") {
        const { ok, fileName, error } = message.payload;
        toasts.addToast(
          ok
            ? { severity: "success", title: "Mermaid Chart created", description: fileName }
            : { severity: "error", title: "Mermaid export failed", description: error },
        );
      }
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return null;
}

function App() {
  const debounceRef = useRef<number | undefined>(undefined);
  const loadingRef = useRef(false);

  const handleMount = useCallback((editor: Editor) => {
    onNativeMessage((message) => {
      if (message.type !== "loadContent") return;
      const snapshot = message.payload.snapshot;
      if (!snapshot) return;
      loadingRef.current = true;
      loadSnapshot(editor.store, snapshot as Parameters<typeof loadSnapshot>[1]);
      // Some store listeners fire on a later tick than loadSnapshot's synchronous return,
      // so clearing the flag immediately would let that trailing change re-trigger a save.
      requestAnimationFrame(() => {
        loadingRef.current = false;
      });
    });

    editor.store.listen(
      () => {
        if (loadingRef.current) return;
        window.clearTimeout(debounceRef.current);
        debounceRef.current = window.setTimeout(() => {
          const snapshot = getSnapshot(editor.store);
          postToNative({ type: "contentChanged", payload: { snapshot } });
        }, 500);
      },
      { source: "user", scope: "document" },
    );

    postToNative({ type: "ready", payload: {} });
  }, []);

  return (
    <div style={{ position: "fixed", inset: 0 }}>
      <Tldraw onMount={handleMount} components={{ ContextMenu: CustomContextMenu }}>
        <NativeExportResultToasts />
      </Tldraw>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);

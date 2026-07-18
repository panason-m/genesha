import { StrictMode, useEffect, useRef } from "react";
import { createRoot } from "react-dom/client";
import { useCreateBlockNote } from "@blocknote/react";
import { BlockNoteView } from "@blocknote/mantine";
import "@blocknote/core/fonts/inter.css";
import "@blocknote/mantine/style.css";
import { onNativeMessage, postToNative } from "./bridge";

function Editor() {
  const editor = useCreateBlockNote();
  const debounceRef = useRef<number | undefined>(undefined);
  const loadingRef = useRef(false);

  useEffect(() => {
    onNativeMessage(async (message) => {
      if (message.type !== "loadContent") return;
      loadingRef.current = true;
      const blocks = await editor.tryParseMarkdownToBlocks(message.payload.markdown ?? "");
      editor.replaceBlocks(editor.document, blocks);
      loadingRef.current = false;
    });

    postToNative({ type: "ready", payload: {} });
  }, [editor]);

  useEffect(() => {
    return editor.onChange(() => {
      if (loadingRef.current) return;
      window.clearTimeout(debounceRef.current);
      debounceRef.current = window.setTimeout(async () => {
        const markdown = await editor.blocksToMarkdownLossy(editor.document);
        postToNative({ type: "contentChanged", payload: { markdown } });
      }, 400);
    });
  }, [editor]);

  return <BlockNoteView editor={editor} theme="dark" />;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <Editor />
  </StrictMode>,
);

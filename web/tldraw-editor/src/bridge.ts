export type BridgeMessage =
  | { type: "ready"; payload: Record<string, never> }
  | { type: "contentChanged"; payload: { snapshot: unknown } }
  | { type: "loadContent"; payload: { snapshot: unknown } }
  | {
      type: "exportGroupPng";
      payload: { requestId: string; groupId: string; pngBase64: string; width: number; height: number };
    }
  | {
      type: "exportGroupPngResult";
      payload: { requestId: string; ok: boolean; fileName?: string; error?: string };
    }
  | {
      type: "exportFlowMermaid";
      payload: { requestId: string; groupId: string; mermaidText: string };
    }
  | {
      type: "exportFlowMermaidResult";
      payload: { requestId: string; ok: boolean; fileName?: string; error?: string };
    };

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
        addEventListener: (type: "message", listener: (event: MessageEvent) => void) => void;
      };
    };
  }
}

export function postToNative(message: BridgeMessage): void {
  window.chrome?.webview?.postMessage(message);
}

export function onNativeMessage(handler: (message: BridgeMessage) => void): void {
  window.chrome?.webview?.addEventListener("message", (event) => {
    handler(event.data as BridgeMessage);
  });
}

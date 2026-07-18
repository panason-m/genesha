export interface LayoutNode {
  id: string;
  label: string;
  kind: "node" | "cluster";
  x: number;
  y: number;
  width: number;
  height: number;
}

export type BridgeMessage =
  | { type: "ready"; payload: Record<string, never> }
  | { type: "contentChanged"; payload: { text: string } }
  | { type: "loadContent"; payload: { text: string } }
  | { type: "layout"; payload: { nodes: LayoutNode[] } }
  | { type: "getLayout"; payload: Record<string, never> };

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

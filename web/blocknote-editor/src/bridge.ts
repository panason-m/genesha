export type BridgeMessage =
  | { type: "ready"; payload: Record<string, never> }
  | { type: "contentChanged"; payload: { markdown: string } }
  | { type: "loadContent"; payload: { markdown: string } }
  | { type: "requestPrintHtml"; payload: { requestId: string } }
  | { type: "printHtml"; payload: { requestId: string; html: string } };

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

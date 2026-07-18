export const FrontendEvent = {
  EngineEvent: "engine.event",
  MenubarItemClicked: "menubar.itemClicked",
  NotificationActivated: "notification.activated",
  NotificationDismissed: "notification.dismissed",
  WebviewPaneClosed: "webviewPane.closed",
  WebviewPaneDownloadCompleted: "webviewPane.downloadCompleted",
  WebviewPaneDownloadFailed: "webviewPane.downloadFailed",
  WebviewPaneDownloadProgress: "webviewPane.downloadProgress",
  WebviewPaneDownloadRequested: "webviewPane.downloadRequested",
  WebviewPaneFaviconChanged: "webviewPane.faviconChanged",
  WebviewPaneLoadStateChanged: "webviewPane.loadStateChanged",
  WebviewPaneNavigated: "webviewPane.navigated",
  WebviewPanePermissionRequested: "webviewPane.permissionRequested",
  WebviewPaneProcessTerminated: "webviewPane.processTerminated",
  WebviewPaneTitleChanged: "webviewPane.titleChanged",
  WindowBlurred: "window.blurred",
  WindowFocused: "window.focused",
} as const;

export type FrontendEvent = (typeof FrontendEvent)[keyof typeof FrontendEvent];

export interface ClusterState {
  updateStaged: boolean;
}

export interface ClusterTool {
  id: string;
  icon: string;
  title: string;
  action: string;
  visible: boolean;
}

export function clusterTools(state: ClusterState): ClusterTool[] {
  const tools: ClusterTool[] = [
    { id: "find-anything", icon: "⌕", title: "Find anything", action: "tool.palette", visible: true },
    { id: "inspect", icon: "⌖", title: "Inspect UI — report a bug (dev)", action: "tool.inspect", visible: true },
    { id: "zoom-out", icon: "−", title: "Zoom out the app", action: "app.zoom-out", visible: true },
    { id: "zoom-in", icon: "+", title: "Zoom in the app", action: "app.zoom-in", visible: true },
    { id: "settings", icon: "⚙", title: "Settings", action: "app.settings", visible: true },
    { id: "update", icon: "⟳", title: "Update ready — restart to apply", action: "app.update", visible: state.updateStaged },
  ];
  return tools.filter((t) => t.visible);
}

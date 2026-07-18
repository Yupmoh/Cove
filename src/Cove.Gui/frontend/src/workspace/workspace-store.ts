export interface Subtab {
  documentId: string;
  nookType: string;
  title: string | null;
}

export interface NookLeaf {
  kind: "leaf";
  nookId: string;
  subtabs: Subtab[];
  activeSubtab: number;
}

export interface SplitNode {
  kind: "split";
  orientation: number | string;
  ratio: number;
  childA: MosaicNode;
  childB: MosaicNode;
}

export type MosaicNode = SplitNode | NookLeaf;

export interface ShoreSnapshot {
  id: string;
  name: string;
  layoutTree: MosaicNode;
  zoomedNookId: string | null;
  wingId?: string;
  pinned?: boolean;
}

export interface WingSnapshot {
  id: string;
  name: string;
  iconKind?: string | null;
  iconValue?: string | null;
}

export interface BaySnapshot {
  schemaVersion: number;
  id: string;
  name: string;
  projectDir: string;
  activeShoreId: string | null;
  shores: ShoreSnapshot[];
  wings?: WingSnapshot[];
  activeWingId?: string | null;
  focusedNookId?: string | null;
}

export function workspaceNookIds(node: MosaicNode): string[] {
  if (node.kind === "leaf") {
    return node.subtabs.length > 0 ? node.subtabs.map((subtab) => subtab.documentId) : [node.nookId];
  }
  return [...workspaceNookIds(node.childA), ...workspaceNookIds(node.childB)];
}

export class WorkspaceStore {
  private canonicalSnapshot: BaySnapshot | null = null;
  private selectedShoreId: string | null = null;
  private selectedNookId: string | null = null;

  get snapshot(): BaySnapshot | null {
    return this.canonicalSnapshot;
  }

  set snapshot(snapshot: BaySnapshot | null) {
    if (snapshot) this.applySnapshot(snapshot);
    else this.clear();
  }

  get activeShoreId(): string | null {
    return this.selectedShoreId;
  }

  set activeShoreId(shoreId: string | null) {
    this.selectedShoreId = shoreId;
  }

  get focusedNookId(): string | null {
    return this.selectedNookId;
  }

  set focusedNookId(nookId: string | null) {
    this.selectedNookId = nookId;
  }

  applySnapshot(snapshot: BaySnapshot): void {
    this.canonicalSnapshot = snapshot;
    const selectedShoreStillExists = this.selectedShoreId !== null
      && snapshot.shores.some((shore) => shore.id === this.selectedShoreId);
    if (!selectedShoreStillExists) {
      const requested = snapshot.activeShoreId;
      this.selectedShoreId = requested && snapshot.shores.some((shore) => shore.id === requested)
        ? requested
        : snapshot.shores[0]?.id ?? null;
    }
    const availableNooks = this.activeShore() ? workspaceNookIds(this.activeShore()!.layoutTree) : [];
    if (!this.selectedNookId || !availableNooks.includes(this.selectedNookId)) {
      this.selectedNookId = snapshot.focusedNookId && availableNooks.includes(snapshot.focusedNookId)
        ? snapshot.focusedNookId
        : availableNooks[0] ?? null;
    }
  }

  selectShore(shoreId: string, focusedNookId?: string | null): void {
    const shore = this.canonicalSnapshot?.shores.find((candidate) => candidate.id === shoreId);
    if (!shore) return;
    this.selectedShoreId = shoreId;
    const availableNooks = workspaceNookIds(shore.layoutTree);
    this.selectedNookId = focusedNookId && availableNooks.includes(focusedNookId)
      ? focusedNookId
      : availableNooks[0] ?? null;
  }

  clearFocus(): void {
    this.selectedNookId = null;
  }

  clearSelection(): void {
    this.selectedShoreId = null;
    this.selectedNookId = null;
  }

  reorderShores(shoreIds: string[]): void {
    if (!this.canonicalSnapshot) return;
    const byId = new Map(this.canonicalSnapshot.shores.map((shore) => [shore.id, shore]));
    const ordered = shoreIds.map((id) => byId.get(id)).filter((shore): shore is ShoreSnapshot => shore !== undefined);
    for (const shore of this.canonicalSnapshot.shores) {
      if (!shoreIds.includes(shore.id)) ordered.push(shore);
    }
    this.canonicalSnapshot = { ...this.canonicalSnapshot, shores: ordered };
  }

  activateSubtab(shoreId: string, leafId: string, index: number): void {
    if (!this.canonicalSnapshot) return;
    const updateNode = (node: MosaicNode): MosaicNode => {
      if (node.kind === "leaf") {
        if (node.nookId !== leafId || index < 0 || index >= node.subtabs.length) return node;
        return { ...node, activeSubtab: index };
      }
      const childA = updateNode(node.childA);
      const childB = updateNode(node.childB);
      return childA === node.childA && childB === node.childB ? node : { ...node, childA, childB };
    };
    const shores = this.canonicalSnapshot.shores.map((shore) => {
      if (shore.id !== shoreId) return shore;
      const layoutTree = updateNode(shore.layoutTree);
      return layoutTree === shore.layoutTree ? shore : { ...shore, layoutTree };
    });
    this.canonicalSnapshot = { ...this.canonicalSnapshot, shores };
  }

  private activeShore(): ShoreSnapshot | undefined {
    return this.canonicalSnapshot?.shores.find((shore) => shore.id === this.selectedShoreId);
  }

  private clear(): void {
    this.canonicalSnapshot = null;
    this.clearSelection();
  }
}

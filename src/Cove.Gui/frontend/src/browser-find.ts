export interface FindResult {
  matches: number;
  activeIndex: number;
}

export function formatFindCounter(matches: number, activeIndex: number): string {
  if (matches <= 0) return "0/0";
  return `${activeIndex}/${matches}`;
}

export class FindBarState {
  open = false;
  query = "";
  matchCase = false;
  matches = 0;
  activeIndex = 0;

  openBar(): void {
    this.open = true;
  }

  closeBar(): void {
    this.open = false;
    this.clearResults();
  }

  setQuery(text: string): void {
    this.query = text;
    if (text.length === 0) this.clearResults();
  }

  toggleMatchCase(): void {
    this.matchCase = !this.matchCase;
  }

  applyResult(result: FindResult): void {
    this.matches = Math.max(0, result.matches);
    this.activeIndex = this.matches === 0 ? 0 : Math.max(1, result.activeIndex);
  }

  onNavigate(): void {
    this.clearResults();
  }

  get canSearch(): boolean {
    return this.query.length > 0;
  }

  get counter(): string {
    return formatFindCounter(this.matches, this.activeIndex);
  }

  private clearResults(): void {
    this.matches = 0;
    this.activeIndex = 0;
  }
}

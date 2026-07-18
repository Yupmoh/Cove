import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import { brandLogoAt, nextBrandIndex, parseBrandIndex } from "../../brand";

const BRAND_LOGO_STORAGE_KEY = "cove.brandLogo";

export interface BrandFeatureDependencies {
  document: Document;
  storage: Storage;
}

export interface BrandFeature extends ComponentHandle {
  readonly currentIndex: number;
  start(): void;
  apply(): void;
}

export function createBrandFeature(dependencies: BrandFeatureDependencies): BrandFeature {
  const lifecycle = new LifecycleScope();
  let currentIndex = parseBrandIndex(dependencies.storage.getItem(BRAND_LOGO_STORAGE_KEY));
  let started = false;

  dependencies.storage.setItem(BRAND_LOGO_STORAGE_KEY, String(nextBrandIndex(currentIndex)));

  function apply(): void {
    const source = brandLogoAt(currentIndex);
    const wordmark = dependencies.document.getElementById("wordmark-img") as HTMLImageElement | null;
    if (wordmark) wordmark.src = source;
    for (const image of dependencies.document.querySelectorAll<HTMLImageElement>(".cl-brand-img")) {
      image.src = source;
    }
  }

  function advance(): void {
    currentIndex = nextBrandIndex(currentIndex);
    dependencies.storage.setItem(BRAND_LOGO_STORAGE_KEY, String(nextBrandIndex(currentIndex)));
    apply();
  }

  function start(): void {
    if (started) return;
    started = true;
    apply();
    const wordmark = dependencies.document.getElementById("wordmark-img");
    if (wordmark) lifecycle.listen(wordmark, "click", advance);
  }

  return {
    get currentIndex(): number {
      return currentIndex;
    },
    start,
    apply,
    dispose: () => lifecycle.dispose(),
  };
}

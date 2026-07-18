import { Window } from "happy-dom";
import { describe, expect, it } from "vitest";
import { brandLogoAt } from "../../brand";
import { createBrandFeature } from "./brand-feature";

function fixture(storedIndex: string | null = null) {
  const window = new Window();
  const document = window.document;
  document.body.innerHTML = `
    <img id="wordmark-img">
    <img class="cl-brand-img">
  `;
  if (storedIndex !== null) window.localStorage.setItem("cove.brandLogo", storedIndex);
  const feature = createBrandFeature({
    document: document as unknown as Document,
    storage: window.localStorage as unknown as Storage,
  });
  return { window, document, feature };
}

describe("BrandFeature", () => {
  it("loads the current logo, persists the next index, and applies it on start", () => {
    const { window, document, feature } = fixture("1");

    expect(feature.currentIndex).toBe(1);
    expect(window.localStorage.getItem("cove.brandLogo")).toBe("2");

    feature.start();

    expect(document.querySelector("#wordmark-img")?.getAttribute("src")).toBe(brandLogoAt(1));
    expect(document.querySelector(".cl-brand-img")?.getAttribute("src")).toBe(brandLogoAt(1));
  });

  it("advances the logo and persists the following index when the wordmark is clicked", () => {
    const { window, document, feature } = fixture("1");
    feature.start();

    (document.querySelector("#wordmark-img") as unknown as HTMLElement).click();

    expect(feature.currentIndex).toBe(2);
    expect(window.localStorage.getItem("cove.brandLogo")).toBe("0");
    expect(document.querySelector("#wordmark-img")?.getAttribute("src")).toBe(brandLogoAt(2));
    expect(document.querySelector(".cl-brand-img")?.getAttribute("src")).toBe(brandLogoAt(2));
  });

  it("applies the current logo to launcher images added after start", () => {
    const { document, feature } = fixture("2");
    feature.start();
    const launcherImage = document.createElement("img");
    launcherImage.className = "cl-brand-img";
    document.body.appendChild(launcherImage);

    feature.apply();

    expect(launcherImage.getAttribute("src")).toBe(brandLogoAt(2));
  });

  it("removes the click listener when repeatedly disposed", async () => {
    const { window, document, feature } = fixture("0");
    feature.start();
    await feature.dispose();
    await feature.dispose();

    (document.querySelector("#wordmark-img") as unknown as HTMLElement).click();

    expect(feature.currentIndex).toBe(0);
    expect(window.localStorage.getItem("cove.brandLogo")).toBe("1");
    expect(document.querySelector("#wordmark-img")?.getAttribute("src")).toBe(brandLogoAt(0));
  });
});

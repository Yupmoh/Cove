import { describe, it, expect } from "vitest";
import { detectLanguage } from "./monaco-lang";

describe("detectLanguage", () => {
  it("detects .ts as typescript", () => {
    expect(detectLanguage("file.ts")).toBe("typescript");
  });

  it("detects .cs as csharp", () => {
    expect(detectLanguage("file.cs")).toBe("csharp");
  });

  it("detects .json as json", () => {
    expect(detectLanguage("file.json")).toBe("json");
  });

  it("detects .md as markdown", () => {
    expect(detectLanguage("file.md")).toBe("markdown");
  });

  it("defaults to plaintext for unknown", () => {
    expect(detectLanguage("file.unknownext")).toBe("plaintext");
  });

  it("defaults to plaintext for no extension", () => {
    expect(detectLanguage("Makefile")).toBe("plaintext");
  });

  it("detects .py as python", () => {
    expect(detectLanguage("script.py")).toBe("python");
  });

  it("detects .rs as rust", () => {
    expect(detectLanguage("main.rs")).toBe("rust");
  });

  it("detects Dockerfile", () => {
    expect(detectLanguage("Dockerfile")).toBe("dockerfile");
    expect(detectLanguage("path/to/Dockerfile")).toBe("dockerfile");
  });

  it("detects Makefile as plaintext", () => {
    expect(detectLanguage("Makefile")).toBe("plaintext");
  });

  it("detects .css as css", () => {
    expect(detectLanguage("style.css")).toBe("css");
  });

  it("detects .go as go", () => {
    expect(detectLanguage("main.go")).toBe("go");
  });
});

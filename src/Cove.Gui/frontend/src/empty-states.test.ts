import { describe, it, expect } from "vitest";
import { EmptyStateMessages, type EmptyStateConfig } from "./empty-states";

describe("EmptyStateMessages", () => {
  it("has all expected messages", () => {
    expect(EmptyStateMessages.noShores).toBe("No shores yet");
    expect(EmptyStateMessages.noNotes).toBe("No notes yet");
    expect(EmptyStateMessages.noSearchResults).toBe("No results");
    expect(EmptyStateMessages.noChanges).toBe("No changes");
    expect(EmptyStateMessages.noTasks).toBe("No tasks");
    expect(EmptyStateMessages.noteDeleted).toBe("This note was deleted");
    expect(EmptyStateMessages.noTimeline).toBe("No activity yet");
  });
});

describe("EmptyStateConfig", () => {
  it("message is required", () => {
    const config: EmptyStateConfig = { message: "test" };
    expect(config.message).toBe("test");
    expect(config.actionLabel).toBeUndefined();
  });

  it("actionLabel and actionIcon are optional", () => {
    const config: EmptyStateConfig = { message: "test", actionLabel: "Click", actionIcon: "+" };
    expect(config.actionLabel).toBe("Click");
    expect(config.actionIcon).toBe("+");
  });
});

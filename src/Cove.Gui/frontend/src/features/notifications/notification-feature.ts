import { EngineEventRouter } from "../../app/engine-event-router";
import { FrontendCommand } from "../../app/frontend-command";
import { FrontendEvent } from "../../app/frontend-event";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import {
  NotificationBridge,
  type NotificationDeliverPayload,
} from "../../notifications";

export interface NotificationFeatureDependencies {
  engineEvents: EngineEventRouter;
  observe(event: FrontendEvent, callback: (data: unknown) => void): () => void;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  invokeNative<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  reveal(nookId: string): void;
  toast(payload: NotificationDeliverPayload, onClick: () => void): void;
  warn(message: string): void;
}

export interface NotificationFeature extends ComponentHandle {
  start(): void;
}

export function createNotificationFeature(
  dependencies: NotificationFeatureDependencies,
): NotificationFeature {
  const lifecycle = new LifecycleScope();
  const bridge = new NotificationBridge({
    isPermissionGranted: () =>
      dependencies.invoke<boolean>(FrontendCommand.NotificationIsPermissionGranted, {})
        .catch(() => false),
    requestPermission: () =>
      dependencies.invoke<boolean>(FrontendCommand.NotificationRequestPermission, {})
        .catch(() => false),
    send: async (payload) => {
      await dependencies.invokeNative(FrontendCommand.NotificationSendWithId, {
        id: payload.id,
        title: payload.title,
        body: payload.body,
      });
    },
    reveal: dependencies.reveal,
    toast: (payload) => dependencies.toast(
      payload,
      () => dependencies.reveal(payload.nookId),
    ),
    warn: dependencies.warn,
  });
  let started = false;

  function notificationId(data: unknown): string | null {
    if (typeof data === "string") return data;
    return (data as { id?: string } | null)?.id ?? null;
  }

  function start(): void {
    if (started) return;
    started = true;
    const registration = dependencies.engineEvents.register(
      "notification.deliver",
      (payload) => {
        const event = payload as NotificationDeliverPayload | undefined;
        if (!event?.id) {
          dependencies.warn("notification.deliver: malformed payload");
          return;
        }
        void bridge.deliver(event);
      },
    );
    lifecycle.own(() => registration.dispose());
    lifecycle.own(dependencies.observe(FrontendEvent.NotificationActivated, (data) => {
      const id = notificationId(data);
      if (id) bridge.onActivated(id);
    }));
    lifecycle.own(dependencies.observe(FrontendEvent.NotificationDismissed, (data) => {
      const id = notificationId(data);
      if (id) bridge.onDismissed(id);
    }));
  }

  return {
    start,
    dispose: () => lifecycle.dispose(),
  };
}

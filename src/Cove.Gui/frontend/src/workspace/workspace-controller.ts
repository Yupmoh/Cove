import { FrontendCommand } from "../app/frontend-command";

export type WorkspaceInvoke = (command: FrontendCommand, args: unknown) => Promise<unknown>;

export class WorkspaceController {
  private mutationTail: Promise<void> = Promise.resolve();
  private transactionTail: Promise<void> = Promise.resolve();

  constructor(private readonly invoke: WorkspaceInvoke) {}

  mutate<T = unknown>(operation: string, payload: Record<string, unknown>): Promise<T> {
    const request = { ...payload, op: operation };
    const result = this.mutationTail.then(() => this.invoke(FrontendCommand.AppLayoutMutate, request) as Promise<T>);
    this.mutationTail = result.then(
      () => undefined,
      () => undefined,
    );
    return result;
  }

  transaction<T>(work: () => Promise<T>): Promise<T> {
    const result = this.transactionTail.then(work);
    this.transactionTail = result.then(
      () => undefined,
      () => undefined,
    );
    return result;
  }
}

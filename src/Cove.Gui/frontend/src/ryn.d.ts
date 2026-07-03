interface RynBridge {
  invoke(command: string, args?: Record<string, unknown>): Promise<unknown>
  on(event: string, callback: (data: unknown) => void): void
  off(event: string, callback: (data: unknown) => void): void
}

interface Window {
  __ryn: RynBridge
}
import '@testing-library/jest-dom'

// jsdom does not implement ResizeObserver, which recharts (the decomposition waterfall) relies on.
// Provide a no-op stub for the test environment only.
class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver

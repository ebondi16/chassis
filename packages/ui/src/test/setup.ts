import "@testing-library/jest-dom/vitest";

import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

export interface MockRequest {
  url: string;
  method: string;
  /** Parsed JSON request body, or undefined for GET/HEAD. */
  body: any;
}

type Handler = (request: MockRequest) => unknown;

let handler: Handler | null = null;

/**
 * Register the API behavior for the current test. Return a plain value to get a
 * 200 JSON response, or return a `Response` for full control (status, headers).
 *
 * openapi-fetch captures `globalThis.fetch` when the client module is first
 * imported — before any `beforeEach` runs — so the stub is installed here at
 * setup time and each test swaps in its own handler.
 */
export function mockApi(next: Handler): void {
  handler = next;
}

vi.stubGlobal("fetch", async (input: Request | string | URL, init?: RequestInit) => {
  const request = input instanceof Request ? input : new Request(String(input), init);

  if (!handler) {
    throw new Error(`Unhandled fetch in test: ${request.method} ${request.url}`);
  }

  const hasBody = request.method !== "GET" && request.method !== "HEAD";
  const rawBody = hasBody ? await request.text() : "";

  const result = await handler({
    url: request.url,
    method: request.method,
    body: rawBody ? JSON.parse(rawBody) : undefined,
  });

  return result instanceof Response
    ? result
    : new Response(JSON.stringify(result), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
});

afterEach(() => {
  cleanup();
  handler = null;
});

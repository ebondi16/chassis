import createClient from "openapi-fetch";

import type { paths } from "./schema";

export type { paths, components, operations } from "./schema";

/**
 * Creates a typed client for the Chassis API.
 *
 * `baseUrl` defaults to same-origin (`"/"`), which is how both deployment modes
 * serve the API in production — the SPA and the API are the same ASP.NET Core
 * app (template §7.2). Pass an absolute URL only for local dev against a
 * separately-running server.
 */
export function createChassisClient(baseUrl = "/") {
  return createClient<paths>({ baseUrl });
}

export type ChassisClient = ReturnType<typeof createChassisClient>;

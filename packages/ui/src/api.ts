import { createChassisClient } from "@chassis/api-client";

/**
 * The app's single API client. Same-origin by default; override with
 * `VITE_API_BASE_URL` if you ever serve the SPA from a different origin than
 * the API.
 */
export const api = createChassisClient(import.meta.env.VITE_API_BASE_URL ?? "/");

/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Dev-only: the SPA calls the API at a relative path (`/api/...`), same as in
// production where Chassis.Api serves this build itself (§5.5). In `vite dev`
// there's no such server, so proxy those paths to the separately-running API.
// Matches Chassis.Api's default launch URL (server/src/Chassis.Api/Properties/launchSettings.json).
const API_ORIGIN = "http://localhost:5030";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: API_ORIGIN, changeOrigin: true },
      "/openapi": { target: API_ORIGIN, changeOrigin: true },
      "/scalar": { target: API_ORIGIN, changeOrigin: true },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
  test: {
    environment: "jsdom",
    // Node's global fetch/Request (undici) won't resolve relative URLs against a
    // document origin the way a browser does, so give the client an absolute
    // base URL under test. Production still defaults to same-origin ("/").
    env: { VITE_API_BASE_URL: "http://localhost:5173" },
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});

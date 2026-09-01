# @chassis/api-client

Typed TypeScript client for the Chassis API. **Types are generated, not
hand-written** (template §6, §9.5): `openapi-typescript` turns the API's OpenAPI
document into `src/schema.d.ts`, and `openapi-fetch` provides the thin typed
`fetch` wrapper. No runtime, no framework lock-in.

## Usage

```ts
import { createChassisClient } from "@chassis/api-client";

const api = createChassisClient(); // same-origin ("/") by default
const { data, error } = await api.GET("/api/notes");
```

## Regenerating after an API change

The client is generated from `server/src/Chassis.Api/Chassis.Api.json`, which
`Chassis.Api` emits on **every `dotnet build`** (`OpenApiGenerateDocumentsOnBuild`).
So:

```sh
dotnet build server/Chassis.slnx      # refreshes Chassis.Api.json
npm run generate:api-client           # refreshes src/schema.d.ts  (from repo root)
```

Both `Chassis.Api.json` and `src/schema.d.ts` are committed — a clean checkout
can build the UI without running .NET, and API-surface changes show up in diffs.
No running server is needed for generation.

This package exposes its TypeScript source directly (no build step); consumers
(`@chassis/ui`) bundle it.

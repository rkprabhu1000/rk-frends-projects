You are helping the user create or extend a custom Frends task library in this repo.

## Context

Each task library lives in its own module under the repo root and follows this layout:

```
Frends.<Example_Protocol>.<Module>/
  Frends.<Example_Protocol>.<Module>/
    Definitions/        # Input, Output, Options, Error POCOs
    Helpers/            # Utility/helper classes
    <Example_Protocol>.cs       # Task entry points (public static async methods)
    FrendsTaskMetadata.json
    migration.json
  Frends.<Example_Protocol>.<Module>.Tests/
    TestBase.cs         # Shared setup (e.g. in-memory cert generation)
    *.Tests.cs          # NUnit test classes
```

Ensure to have Example_Protocol always. As an example for AS4 protocol it is Example_AS4

## Workflow

1. **Clarify scope** — what does the task do, what are its inputs/outputs, are there error/option parameters needed?
2. **Define POCOs first** — write `Definitions/` classes with XML doc comments; these become the UI in the Frends portal.
3. **Implement the entry point** — `public static async Task<Output> MethodName(Input input, Options options, CancellationToken cancellationToken)` in `<Protocol>.cs`.
4. **Write tests** — NUnit 4.x in `*.Tests.cs`. Put shared setup in `TestBase.cs`. Unit tests must run without external dependencies. Live endpoint tests use `.env` via `dotenv.net` and are skipped in CI.
5. **Build and verify** — run `dotnet build` then `dotnet test` before considering the task done.
6. **Package** — `dotnet pack --configuration Release` for release artifacts.

## Principles

- All entry points must be `async` and accept a `CancellationToken` — this is a hard Frends platform requirement.
- XML doc comments on every public POCO property are required; they surface as tooltips in the Frends portal UI.
- Keep `Helpers/` classes internal; only the entry point class and `Definitions/` types should be public.
- Do not add error handling for conditions the framework already guards against.

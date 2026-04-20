# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the **rk-frends-projects** monorepo — the central workspace for all Frends-related development. Work here falls into two categories:

1. **Frends process automation** — creating and managing integration processes on the Frends tenant (`rktestfrends.frendsapp.com`) via the Frends Platform API (`https://rktestfrends.frendsapp.com/api/v1/`). Always target environment **Development**, agent group **Development**, agent **Default01** unless the user says otherwise.
2. **Custom Frends task development** — building C# / .NET 8.0 task libraries that extend the Frends EiPaaS platform. Completed tasks are packaged with `dotnet pack --configuration Release` and manually uploaded to the tenant via the Frends portal.

### Process naming convention

`XXXX1000: Short description` — 2–4 capital letters (customer/use-case prefix) + 4-digit number starting at 1000 + colon + 3–4 word description. Example: `ITM81000: Sync data from SAP`.

The current custom task modules are:
- **Frends.AS4.Send** — Sends files to remote AS4 Message Service Handlers (MSH)
- **Frends.AS4.Receive** — Processes inbound AS4 messages
- **Frends.AS4.PullRequest** — Handles AS4 eb:PullRequest pull-pattern exchanges

## Commands

```bash
# Build all projects
dotnet build

# Run all tests
dotnet test

# Run tests for a specific project
dotnet test Frends.AS4.Send/Frends.AS4.Send.Tests/Frends.AS4.Send.Tests.csproj

# Package for release
dotnet pack --configuration Release
```

## Architecture

Each module follows the same internal layout:

```
Frends.<Example_Protocol>.<Module>/
  Frends.<Example_Protocol>.<Module>/
    Definitions/        # Input, Output, Options, Error POCOs
    Helpers/            # Utility/helper classes
    Example_Protocol.cs              # Task entry point (public static methods)
    FrendsTaskMetadata.json  # Declares task entry points for Frends platform
    migration.json      # Version tracking
  Frends.<Example_Protocol>.<Module>.Tests/
    TestBase.cs         # Shared certificate generation utilities
    *.Tests.cs          # NUnit test classes
```

### Key patterns

- All task entry points are `async` and accept a `CancellationToken`
- `Definitions/` POCOs are plain C# classes with XML doc comments — these surface as UI in the Frends portal
- Live endpoint tests load credentials/endpoints from `.env` files via `dotenv.net` — these tests are skipped in CI and require manual setup

## Testing

Unit tests use **NUnit 4.x**. `TestBase.cs` in each test project provides in-memory X.509 certificate generation so cryptographic tests run without external dependencies.

For live endpoint integration tests, create a `.env` file in the test project directory with the required variables (see each test class for expected variable names).

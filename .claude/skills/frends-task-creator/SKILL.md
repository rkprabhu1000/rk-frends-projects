---
name: frends-task-creator
description: Build, review, and refactor Frends Custom Tasks (C#/.NET class libraries packaged as NuGet and imported into the Frends Control Panel). Use this skill whenever the user mentions a Frends Task, Custom Task, Frends Connector, the Frends task template, FrendsTaskMetadata.json, a task Input/Connection/Options tab, ThrowErrorOnFailure, a Frends task NuGet package, or asks to scaffold, code, document, version, test, or code-review anything that will run as a Task inside a Frends Process — even if they don't say "Custom Task" explicitly. Always starts from the official `dotnet new frends-task` template and enforces the in-house rule that the reserved `Frends.*` prefix is never used for custom tasks.
---

# Frends Custom Task Development

Custom Tasks are C# class libraries compiled outside Frends, packaged as NuGet, and imported into a Frends tenant, where they are used as **Task** shapes inside a **Process**.

**Always start from the official template** (`dotnet new frends-task`). It already produces a guideline-compliant solution — project layout, tabs, error handler, validation, analyzers, tests, workflows, metadata. Hand-writing the skeleton reintroduces mistakes the template has already solved. The work in this skill is: generate → de-brand → implement → review.

For platform terminology (Process, Agent Group, Trigger, `#env`, deployment) consult the `frends-canonical-knowledge` skill and use its vocabulary — Process (not "flow"), deploy to an Agent Group (not "to an Environment").

---

## Rule zero: the party name is never `Frends`

The template calls the first name segment the **Party**. For Frends' own catalog tasks the Party is `Frends`; for a custom task it is the **owning organisation**.

`Frends.*` is reserved for the official catalog at `tasks.frends.com`. A custom task using it collides with catalog package IDs, implies Frends support that doesn't exist, and destroys the distinction between vendor-supported and in-house code.

**Naming pattern:** `<Party>.<SYSTEM>.<ACTION>`

| Element | Value | Example |
|---|---|---|
| Namespace / assembly / NuGet id | `<Party>.<SYSTEM>.<ACTION>` | `Contoso.Salesforce.Query` |
| Class name | `<SYSTEM>` | `Salesforce` |
| Method name | `<ACTION>` | `Query` |
| `TaskMethod` in metadata | `<Party>.<SYSTEM>.<ACTION>.<SYSTEM>.<ACTION>` | `Contoso.Salesforce.Query.Salesforce.Query` |

`<Party>` is the owning organisation's short name in PascalCase. If the user hasn't given one, ask once rather than inventing it — it becomes the NuGet package ID and can't be changed later without a breaking release.

The template's `-F` default is `Frends.Echo.Execute`. **Never accept that default.** Treat any `Frends.`-prefixed identifier in a custom task as a blocking review finding.

The word "Frends" legitimately remains in: the filename `FrendsTaskMetadata.json`, the `FrendsTaskAnalyzers` package reference, `PackageTags` (aids discovery), and prose about the platform.

---

## Workflow

### 1. Install the template (once per machine)

```bash
dotnet new install frendstasktemplate --nuget-source https://pkgs.dev.azure.com/frends-platform/frends-tasks/_packaging/main/nuget/v3/index.json
```

Requires .NET SDK 8.0 or newer. `dotnet new update` refreshes it; `dotnet new frends-task -h` shows the options.

### 2. Agree the identity, then generate

Confirm `<Party>`, `<SYSTEM>`, `<ACTION>` before generating — renaming afterwards means touching every file. Check that `<ACTION>` names an entity type (`UploadObject`, `ListFiles`, `ReadBlob`), not something vague like `Process` or `Handle`. PascalCase throughout, including abbreviations: `Csv`, `Url`, `Api` — never `CSV`, `URL`.

Run from the **repository root folder**:

```bash
dotnet new frends-task -F Contoso.Salesforce.Query -D "Executes a SOQL query against Salesforce."
```

### 3. De-brand the generated output — mandatory

The template ships pre-filled for Frends' own repositories and CI. Several values are wrong for a custom task and must be corrected immediately after generation, before the first commit. `references/template-output.md` lists every file, the exact value the template emits, and what to change it to.

The short version: csproj `Copyright`/`Product`/`PackageProjectUrl`/`RepositoryUrl`, the `[Documentation](...)` link in the task class, the README badges and clone URL, and the GitHub workflow `uses:`/`secrets:` lines all still point at Frends.

### 4. Implement against the contracts

Tab layout, result shape, and error handling are fixed contracts, not style preferences — Frends users rely on every Task behaving the same way. Read `references/task-anatomy.md` before writing the method body. The template marks its decision points with `// TODO:` comments; resolve every one of them.

### 5. Document, test, secure

- `references/documentation-and-metadata.md` — XML docs, `FrendsTaskMetadata.json`, `migration.json`, CHANGELOG, versioning
- `references/testing-and-cicd.md` — 80% coverage, dotenv, Docker, workflows, licences
- `references/security-checklist.md` — OWASP-aligned review points; run this before calling a Task done

### 6. Self-review

Walk the checklist at the end of this file. Report findings as blocking / non-blocking rather than silently fixing, unless the user asked for a fix.

---

## Non-negotiable contracts (summary)

**Signature** — one public static method, one tab class per tab, cancellation token last:

```csharp
public static async Task<Result> Query(
    [PropertyTab] Input input,
    [PropertyTab] Connection connection,
    [PropertyTab] Options options,
    CancellationToken cancellationToken)
```

**Tabs**
- **Input** — first tab, parameters essential to function
- **Connection** — URLs, tokens, client IDs/secrets, connection strings. Database connection strings belong here without exception. Source/Destination credential parameters move here too. Delete the class if the Task connects to nothing.
- **Options** — optional behaviour controls, and always `ThrowErrorOnFailure` (default `true`) and `ErrorMessageOnFailure`

**Result** — always a `Success` boolean plus an `Error` object:

```jsonc
{
  Success: true/false,
  TaskSpecificReturnValue: ...,   // "Data" for weakly-defined data; semantic names otherwise
  Error: { Message: "string", AdditionalInfo: ... }
}
```

**Error handling** — one `try/catch` around the body delegating to the template's `ex.Handle(options)` extension. Never let a raw exception escape when `ThrowErrorOnFailure` is `false`, and never swallow `OperationCanceledException`.

**Multi-operation tasks** — keep going after a single failure where it makes logical sense, set `Success = false`, list every failed operation in `AdditionalInfo`, and throw at the very end if `ThrowErrorOnFailure` is `true`.

**No third-party types on the public surface** — parameters and results must not expose e.g. `System.Security.Claims.ClaimsPrincipal` or an SDK response class. Map to your own DTOs. Frends deserialises across an assembly load context boundary, and leaking SDK types couples the Task's public contract to a dependency's versioning.

**Undefined-shape properties** are `dynamic` with actual type `JToken` (Newtonsoft), matching how Processes handle JSON.

---

## Target framework

The template targets **net8.0**, which is the correct default. The official guidelines PDF contains a stale sample csproj showing `net6.0`; ignore it — the surrounding text mandates .NET 8, and .NET 6 needs an approved reason.

Before shipping, confirm the target Agent's runtime: Frends 6.3 Agents and Processes run on **.NET 10**. A `net8.0` assembly loads on a .NET 10 Agent, but check for APIs removed between the two and say you checked rather than assuming.

---

## Repository layout

The template generates one subfolder per task, each with its own solution — tasks are atomic and independently releasable. Shared code between tasks is not allowed: a change to shared code would silently require re-releasing every dependent task, which is unmanageable to track.

```
repo-root/
  LICENSE                       # one file for all tasks
  README.md                     # links to each task README
  .gitignore                    # one file, covers Visual Studio + Rider
  .github/workflows/
    Query_test_on_push.yml
    Query_test_on_main.yml
    Query_release.yml
  Contoso.Salesforce.Query/
    README.md
    CHANGELOG.md
    Contoso.Salesforce.Query.sln
    Contoso.Salesforce.Query/
      FrendsTaskMetadata.json
      migration.json
      Contoso.Salesforce.Query.csproj
      Contoso.Salesforce.Query.cs
      Definitions/     Input.cs  Connection.cs  Options.cs  Result.cs  Error.cs
      Helpers/         ErrorHandler.cs  ValidationHandler.cs
      Attributes/      RequiredIfAttribute.cs
    Contoso.Salesforce.Query.Tests/
      Contoso.Salesforce.Query.Tests.csproj
      TestBase.cs  FunctionalTests.cs  ErrorHandlerTest.cs  .env.example
```

If a NuGet package is used by more than one task in the repo, pin the same version across them to avoid conflicting dependency resolution on the Agent.

---

## Licensing

The template sets MIT. Custom tasks default to **MIT** unless the owning organisation says otherwise — for internal-only tasks a proprietary licence is legitimate, so ask rather than assuming.

Dependency licences are not optional to check. Permitted: **MIT, Apache 2.0, BSD**. GPL, AGPL, LGPL and anything else are forbidden. Honour attribution requirements in the task `README.md`. Flag any dependency whose licence you cannot verify instead of guessing.

---

## Review checklist

Naming and branding
- [ ] No `Frends.` party prefix in namespace, assembly, package id, or `TaskMethod`
- [ ] `<Party>.<SYSTEM>.<ACTION>` with class = `<SYSTEM>`, method = `<ACTION>`
- [ ] csproj `Copyright`, `Product`, `PackageProjectUrl`, `RepositoryUrl` attribute the owning organisation
- [ ] `[Documentation](...)` link points at the owning repo, not `tasks.frends.com`
- [ ] README badges and clone URL point at the owning repo, not `FrendsPlatform/*`
- [ ] Workflows do not depend on Frends-internal secrets or feeds

Contract
- [ ] `[PropertyTab]` classes; Input first; credentials on Connection
- [ ] `ThrowErrorOnFailure` (default `true`) and `ErrorMessageOnFailure` on Options
- [ ] Result has `Success` and `Error { Message, AdditionalInfo }`
- [ ] No third-party types on parameters or result
- [ ] `CancellationToken` accepted **and actually passed through** to every async/IO call
- [ ] Every template `// TODO:` resolved or deliberately kept

Quality
- [ ] Builds with zero warnings; any `#pragma`/`NoWarn` suppression carries a comment explaining why
- [ ] ≥80% unit test coverage; secrets via dotenv, `.env` never committed
- [ ] All public members, parameters and result properties documented with `<example>` blocks
- [ ] CHANGELOG updated; version bumped correctly (breaking = major); `migration.json` updated if parameters moved
- [ ] Security checklist in `references/security-checklist.md` passed

# Template Output and Required De-branding

What `dotnet new frends-task` generates, and everything that must change for a **custom (non-Frends) task**.

The template's placeholder solution is `Party.Echo.Execute`. Running with `-F Contoso.Salesforce.Query` substitutes:

| Placeholder | Derived from `-F` | Example |
|---|---|---|
| `Party` | first segment | `Contoso` |
| `Echo` | second segment (class name) | `Salesforce` |
| `Execute` | third segment (method name) | `Query` |
| `TaskDescription` | `-D` value | `Executes a SOQL query against Salesforce.` |
| `GeneratedDate` / `GeneratedYear` | today | `2026-08-20` |

The Party segment being a template parameter is exactly why this works for custom tasks — the template is not hardcoded to Frends. What *is* hardcoded to Frends is the branding and CI wiring below.

---

## 1. Task project csproj — 4 values to change

Generated `Contoso.Salesforce.Query/Contoso.Salesforce.Query/Contoso.Salesforce.Query.csproj`:

| Property | Template emits | Change to | Why |
|---|---|---|---|
| `Authors` | `Contoso` | *(correct)* | Derived from Party |
| `Company` | `Contoso` | *(correct)* | Derived from Party |
| `Copyright` | `Frends` | `Contoso` | **Wrong owner.** Frends does not hold copyright in your code |
| `Product` | `Frends` | `Contoso Frends Tasks` | The product is yours, not Frends' |
| `PackageTags` | `Frends` | `Frends;Contoso` | Keep `Frends` — it aids discovery — and add your own |
| `PackageProjectUrl` | `https://frends.com/` | your repo or intranet page | Misattributes the project |
| `RepositoryUrl` | `https://github.com/FrendsPlatform/Contoso.Salesforce/tree/main/...` | your actual repository URL | Points at a repo you don't own |

Left as-is: `TargetFramework net8.0`, `LangVersion latest`, `Version 1.0.0`, `PackageLicenseExpression MIT` (change only if the licence differs), `GenerateDocumentationFile true`, the `Content`/`AdditionalFiles` items for `migration.json`, `CHANGELOG.md` and `FrendsTaskMetadata.json`.

The template sets `<Nullable>disable</Nullable>`. Enabling it is an improvement, but do it before writing code, not halfway through.

Two analyzer packages come pre-referenced and should stay — they enforce these conventions at build time:
- `StyleCop.Analyzers` (1.2.0-beta.556; the README explains the pre-release choice)
- `FrendsTaskAnalyzers` — Frends' own Task convention analyzers

If `FrendsTaskAnalyzers` fails to restore, it is likely served from the same Azure DevOps feed as the template rather than nuget.org. Add that feed to `nuget.config` rather than deleting the reference; verify the current feed before advising, don't guess.

---

## 2. Task class — the documentation link

`Contoso.Salesforce.Query.cs` is generated with:

```csharp
/// [Documentation](https://tasks.frends.com/tasks/frends-tasks/Contoso-Salesforce-Query)
```

That path only resolves for tasks published to the official Frends catalog. For a custom task it is a dead link shown to every integration developer in the Control Panel. Replace it with the task's own README:

```csharp
/// [Documentation](https://github.com/contoso/frends-tasks/blob/main/Contoso.Salesforce.Query/README.md)
```

Also in this file: `// TODO: Remove Connection parameter if the task does not make connections`. Decide, don't leave it.

---

## 3. README.md — badges and clone URL

Generated badges point at `github.com/FrendsPlatform/Contoso.Salesforce` and at `app-github-custom-badges.azurewebsites.net`, which is Frends' internal badge service.

- Repoint the build badge and clone URL at your repository.
- The coverage badge depends on the Frends badge service and its API key — either drop it or replace it with a coverage provider your organisation actually uses (Codecov, Coveralls, or an artifact from your own pipeline). Leaving it produces a permanently broken badge.
- Keep the Installing / Building / Run tests / Create a NuGet package sections; extend them with anything a developer needs locally (certificates, Docker, how to obtain test credentials).

---

## 4. GitHub workflows — the biggest change

Three workflows are generated in `.github/workflows/`:

| File | Trigger | Calls |
|---|---|---|
| `Query_test_on_push.yml` | push to non-main branches | `FrendsPlatform/FrendsTasks/.github/workflows/linux_build_test.yml@main` |
| `Query_test_on_main.yml` | push to main | `.../linux_build_main.yml@main` |
| `Query_release.yml` | manual dispatch | `.../release.yml@main` |

They reference Frends-internal secrets: `BADGE_SERVICE_API_KEY`, `TASKS_TEST_FEED_API_KEY`, `TASKS_FEED_API_KEY`. In a customer repository those secrets don't exist, and the release workflow would target Frends' package feed rather than yours.

Choose deliberately:

**Option A — own workflows (preferred).** Replace the `uses:` calls with your own build/test/pack/push steps targeting your organisation's NuGet feed. No dependency on a third party's default branch, and the pipeline can enforce your coverage gate.

**Option B — keep reusing the Frends workflows.** Acceptable if they fit, but:
- Pin to a commit SHA, not `@main`. A mutable reference to another organisation's branch is an unpinned build-supply-chain dependency that can change under you between releases (OWASP A08).
- Remove the `badge_service_api_key` secret and the badge that depends on it.
- Confirm the release workflow can be pointed at your feed; if it can't, Option A is the only correct answer.

Keep in both cases: `dotnet_version: 8.0.x`, `strict_analyzers: true`, the `paths:` filter scoping each workflow to its own task folder, and the least-privilege `permissions:` blocks the template already sets (`contents: read` for tests, `contents: write` for release).

---

## 5. Files that are already correct

Reviewed and correct as generated — don't churn them:

- `FrendsTaskMetadata.json` — `{ "Tasks": [ { "TaskMethod": "Contoso.Salesforce.Query.Salesforce.Query" } ] }`
- `migration.json` — seeded with a `1.0.0` entry; add entries when parameters move or are renamed
- `Helpers/ErrorHandler.cs` — the `ex.Handle(options)` extension, including correct `OperationCanceledException` rethrow
- `Helpers/ValidationHandler.cs` — runs DataAnnotations validation across all tab objects
- `Attributes/RequiredIfAttribute.cs` — conditional-required validation
- `Definitions/*.cs` — the tab and result skeletons; replace the Echo sample properties with real ones
- `Tests/TestBase.cs`, `.env.example` — dotenv wiring for secrets
- `GlobalSuppressions.cs` — keep, but any suppression you add needs a justification comment
- `.gitignore` — one per repository, already covers Visual Studio and Rider

---

## 6. Post-generation verification

```bash
# No Frends party prefix anywhere in identifiers
grep -rn "Frends\." --include=*.cs --include=*.csproj --include=*.json . \
  | grep -v FrendsTaskMetadata.json | grep -v FrendsTaskAnalyzers

# Leftover Frends branding and dead links
grep -rn "frends.com\|FrendsPlatform\|app-github-custom-badges" .

# Unresolved template decisions
grep -rn "TODO:" --include=*.cs .

# Builds clean, tests pass
dotnet build -warnaserror && dotnet test --collect:"XPlat Code Coverage"
```

Every hit in the first two commands is a finding. `FrendsTaskMetadata.json`, the `FrendsTaskAnalyzers` package reference, and `Frends` in `PackageTags` are the only acceptable matches.

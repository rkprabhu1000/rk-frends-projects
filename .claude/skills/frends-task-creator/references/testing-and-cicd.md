# Testing, Docker and CI/CD

Contents:
1. Unit testing
2. Secrets in tests
3. Docker
4. GitHub Actions
5. Dependencies and licences
6. Publishing and importing

---

## 1. Unit testing

The template generates the test project with NUnit 4, `Microsoft.NET.Test.Sdk`, `coverlet.collector`, `dotenv.net` and StyleCop, plus three starting files: `TestBase.cs`, `FunctionalTests.cs` and `ErrorHandlerTest.cs`. Build on those rather than starting a test project from scratch.

Coverage must be **at least 80%**. Treat that as a floor, not a goal — the tests that matter most are the ones covering the error contract, because that is what Processes branch on.

Cover at minimum:
- Happy path: `Success == true` and `Error == null`
- Failure with `ThrowErrorOnFailure = true` → exception thrown, original exception preserved as inner
- Failure with `ThrowErrorOnFailure = false` → `Success == false`, `Error.Message` populated, `ErrorMessageOnFailure` prefixed when set
- Cancellation via a pre-cancelled `CancellationToken` → `OperationCanceledException` propagates, not converted to a failed Result
- Validation: missing `[Required]` input produces a `ValidationException` before any side effect
- Multi-operation partial failure: remaining operations still attempted, all failures listed in `AdditionalInfo`

`TestBase` supplies `DefaultInput()`, `DefaultConnection()` and `DefaultOptions()` factories — extend those instead of constructing tab objects inline in every test, so a new parameter doesn't break every test at once.

Structure the Task so logic is testable without the external system: keep IO behind a small internal seam rather than calling SDK statics inline.

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 2. Secrets in tests

The template wires **dotenv** in `TestBase`:

```csharp
DotEnv.Load();
SecretKey = GetEnvVar("FRENDS_SECRET_KEY");
```

and ships `.env.example` for the developer to copy to `.env`. Rules:

- Never commit a populated `.env`. The repository `.gitignore` already excludes it — verify before the first commit.
- `.env.example` holds synthetic placeholders only, never real values.
- Credentials for third-party systems live in **1Password**, not in the repository, not in CI logs, not pasted into issues or chat.
- In CI, inject them as repository or organisation secrets.
- Replace the placeholder `FRENDS_SECRET_KEY` name with something meaningful for the target system, and remove it if the Task needs no secret.

Assert on synthetic data only. If tests need realistic payloads, generate anonymised fixtures — never copy production data containing personal information into the repository.

---

## 3. Docker

Where the target system has a usable image (most databases, message brokers, S3-compatible stores), spin it up with Docker rather than mocking — it catches the driver-level bugs mocks hide.

- Use `docker-compose.yml` so anyone can reproduce the setup.
- Document the setup steps in the task `README.md`.
- The standard GitHub workflows take a parameter for spinning up containers during tests.

If no image exists, mock the service inside the test project. If mocking is unrealistic and the real system is required, document in the README how to set up e.g. a test account.

---

## 4. GitHub Actions

The template generates three workflows into `.github/workflows` — the only directory GitHub reads them from — named after the action:

| File | Trigger | Reusable workflow called |
|---|---|---|
| `Query_test_on_push.yml` | push to non-main branches, `paths:` scoped to the task folder | `linux_build_test.yml@main` |
| `Query_test_on_main.yml` | push to main | `linux_build_main.yml@main` |
| `Query_release.yml` | manual dispatch | `release.yml@main` |

All three call reusable workflows from `FrendsPlatform/FrendsTasks` and pass Frends-internal secrets (`BADGE_SERVICE_API_KEY`, `TASKS_TEST_FEED_API_KEY`, `TASKS_FEED_API_KEY`):

```yaml
jobs:
  build:
    uses: FrendsPlatform/FrendsTasks/.github/workflows/linux_build_test.yml@main
    with:
      workdir: Contoso.Salesforce.Query
      dotnet_version: 8.0.x
      strict_analyzers: true
```

**For a custom task repository this is the part that needs a deliberate decision**, because those secrets don't exist in your organisation and the release workflow targets Frends' package feed rather than yours. See `template-output.md` §4 for the two options in full. In short: prefer your own workflows publishing to your own feed; if you keep the Frends reusable workflows, pin them to a commit SHA instead of `@main`, since a mutable reference to another organisation's default branch is an unpinned build-supply-chain dependency.

Keep regardless of which option you choose:
- `dotnet_version: 8.0.x`, matching the target framework
- `strict_analyzers: true`
- the `paths:` filter scoping each workflow to its own task folder, so one task's change doesn't rebuild the whole repo
- the least-privilege `permissions:` blocks the template sets (`contents: read` for tests, `contents: write` for release)
- a coverage gate enforcing the 80% threshold rather than merely reporting it

---

## 5. Dependencies and licences

- Permitted licences: **MIT, Apache 2.0, BSD**. GPL, AGPL, LGPL and everything else are forbidden.
- Verify each dependency's licence before adding it; flag any you cannot confirm rather than assuming.
- Honour attribution requirements in the task `README.md`.
- One `LICENSE` file in the repository root covering all tasks.
- Keep the dependency surface small — every dependency is loaded into the Agent's assembly load context alongside other tasks.
- If the same NuGet package is used by several tasks in the repository, keep versions identical to avoid conflicting resolution.
- Prefer packages that are actively maintained and free of known advisories; run `dotnet list package --vulnerable --include-transitive` as part of review.

---

## 6. Publishing and importing

The build produces a `.nupkg` which is imported into the Frends tenant (uploaded directly, or pulled from the organisation's NuGet feed once configured). The package must contain the compiled assembly, `FrendsTaskMetadata.json`, the XML documentation file, and `CHANGELOG.md`.

Two practical points:
- Frends will not import the same version twice — bump the patch number for every test import.
- The imported Task is used by Processes deployed to an Agent Group; changing a Task's contract affects every Process using it, which is why breaking changes need a major version and a documented upgrade path.

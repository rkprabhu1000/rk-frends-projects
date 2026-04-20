# rk-frends-projects

Central monorepo for all Frends-related development. Work here falls into two categories:

1. **Frends process automation** — creating and managing integration processes on the Frends tenant via the Platform API
2. **Custom Frends task development** — building C# / .NET 8.0 task libraries that extend the Frends EiPaaS platform

---

## Frends process automation (`/frends-process`)

Processes are created and managed **directly on the tenant** (`rktestfrends.frendsapp.com`) via the Platform API. Nothing is stored locally.

**Default deployment target:**

| Setting | Value |
|---|---|
| Environment | Development |
| Agent Group | Development |
| Agent | Default01 |

**Process naming convention:** `XXXX1000: Short description`
- 2–4 capital letters for customer/use-case prefix (e.g. `ITM8`)
- 4-digit number starting at `1000`, incrementing per process in the same group
- Example: `ITM81000: Sync data from SAP`

**Authentication:** Bearer token via Azure AD client credentials. Client secret is stored in a gitignored `.env` file at the repo root as `FRENDS_CLIENT_SECRET`. See `.env.example` for the required variables.

---

## Custom task development (`/frends-task`)

Each task library lives in its own module under the repo root:

```
Frends.<Example_Protocol>.<Module>/
  Frends.<Example_Protocol>.<Module>/
    Definitions/             # Input, Output, Options, Error POCOs
    Helpers/                 # Internal utility classes
    <Example_Protocol>.cs    # Task entry points (public static async methods)
    FrendsTaskMetadata.json  # Declares which methods are exposed as Tasks
    migration.json           # Version tracking for upgrade compatibility
  Frends.<Example_Protocol>.<Module>.Tests/
    TestBase.cs              # Shared setup (e.g. in-memory cert generation)
    *.Tests.cs               # NUnit 4.x test classes
  CHANGELOG.md
```

**Current modules:**

| Module | Description |
|---|---|
| `Frends.AS4.Send` | Sends files to remote AS4 Message Service Handlers |
| `Frends.AS4.Receive` | Processes inbound AS4 messages |
| `Frends.AS4.PullRequest` | Handles AS4 eb:PullRequest pull-pattern exchanges |

**Build commands:**

```bash
dotnet build
dotnet test
dotnet pack --configuration Release   # .nupkg output: bin/Release/net8.0/
```

Completed `.nupkg` files are manually uploaded to the Frends portal.

---

## Local setup

1. Clone the repo:
   ```bash
   git clone https://github.com/rkprabhu1000/rk-frends-projects.git
   ```

2. Copy `.env.example` to `.env` and fill in `FRENDS_CLIENT_SECRET`.

3. To build a specific module:
   ```bash
   cd Frends.AS4.Send/Frends.AS4.Send
   dotnet build
   dotnet test ../Frends.AS4.Send.Tests/Frends.AS4.Send.Tests.csproj
   dotnet pack --configuration Release
   ```

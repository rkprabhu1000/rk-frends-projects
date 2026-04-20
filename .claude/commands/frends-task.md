You are helping the user create or extend a custom Frends task library in this repo.

Use `mcp__frends-docs__searchDocumentation` and `mcp__frends-docs__getPage` to look up Frends platform requirements or task conventions when they are not clear from the existing codebase.

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

## Standard NuGet packages

### Main task project (`.csproj`)

Always include these two as analyzers only (`PrivateAssets=all`):

```xml
<PackageReference Include="FrendsTaskAnalyzers" Version="1.*">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

Add protocol-specific runtime packages as needed (e.g. `MimeKit 4.*`, `System.Security.Cryptography.Xml 8.*` for AS4).

Also include in `<PropertyGroup>`:
```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>CS1591,</NoWarn>
```

### Test project (`.Tests.csproj`)

```xml
<PackageReference Include="NUnit" Version="4.*"/>
<PackageReference Include="NUnit3TestAdapter" Version="6.*"/>
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.*"/>
<PackageReference Include="coverlet.collector" Version="6.*"/>
<PackageReference Include="dotenv.net" Version="4.*"/>
<PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556"/>
```

## Packaging configuration

Add this `<ItemGroup>` to the main task `.csproj` so all required files are bundled into the `.nupkg`:

```xml
<ItemGroup>
    <Content Include="migration.json" PackagePath="/" Pack="true"/>
    <Content Include="../CHANGELOG.md" PackagePath="/" Pack="true"/>
    <Content Include="FrendsTaskMetadata.json"
             Pack="true"
             PackagePath="/"
             CopyToOutputDirectory="PreserveNewest"/>
    <AdditionalFiles Include="FrendsTaskMetadata.json"/>
</ItemGroup>
```

## FrendsTaskMetadata.json

Controls which public static methods are exposed as Tasks. Format:

```json
{
  "Tasks": [
    {
      "TaskMethod": "Frends.<Example_Protocol>.<Module>.<Example_Protocol>.<MethodName>"
    }
  ]
}
```

Example for `Frends.AS4.Send` with a single `Send` method on the `AS4` class:
```json
{
  "Tasks": [
    {
      "TaskMethod": "Frends.Example_AS4.Send.Example_AS4.Send"
    }
  ]
}
```

## migration.json

Tracks version history for task upgrade compatibility. Start with:

```json
[
  {
    "Task": "Frends.<Example_Protocol>.<Module>",
    "Migrations": [
      {
        "Version": "1.0.0",
        "Description": "",
        "Migration": []
      }
    ]
  }
]
```

Add a new entry to `Migrations` each time a breaking change is introduced in a later version.

## Workflow

1. **Clarify scope** — what does the task do, what are its inputs/outputs, are there error/option parameters needed?
2. **Define POCOs first** — write `Definitions/` classes with XML doc comments; these become the UI in the Frends portal.
3. **Implement the entry point** — `public static async Task<Output> MethodName(Input input, Options options, CancellationToken cancellationToken)` in `<Example_Protocol>.cs`.
4. **Write tests** — NUnit 4.x in `*.Tests.cs`. Put shared setup in `TestBase.cs`. Unit tests must run without external dependencies. Live endpoint tests use `.env` via `dotenv.net` and are skipped in CI.
5. **Build and verify** — run `dotnet build` then `dotnet test` (Claude executes these). Fix any errors before proceeding.
6. **Package** — run `dotnet pack --configuration Release` (Claude executes this). The `.nupkg` appears in `bin/Release/net8.0/`.
7. **Deploy** — tell the user the exact path to the `.nupkg` file. The user manually uploads it to the Frends portal.

## Principles

- All entry points must be `async` and accept a `CancellationToken` — this is a hard Frends platform requirement.
- XML doc comments on every public POCO property are required; they surface as tooltips in the Frends portal UI.
- Keep `Helpers/` classes internal; only the entry point class and `Definitions/` types should be public.
- Do not add error handling for conditions the framework already guards against.

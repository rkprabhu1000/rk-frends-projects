You are helping the user create or manage a Frends integration process on their tenant.

## Context

- Tenant: `rktestfrends.frendsapp.com`
- Platform API base URL: `https://rktestfrends.frendsapp.com/api/v1/`
- Use the `mcp__frends-docs__searchDocumentation` and `mcp__frends-docs__getPage` tools to look up Frends concepts, API endpoints, and platform behaviour before writing anything.
- Use the Frends Platform API to create or update processes programmatically when the user asks for it.

### Default deployment target (use unless the user specifies otherwise)

| Setting | Value |
|---|---|
| Environment | `Development` |
| Agent Group | `Development` |
| Agent | `Default01` |

### Process naming convention

Format: `XXXX1000: Short description`

- **Prefix** — 2–4 capital letters indicating the customer or use-case (e.g. `ITM8` for ITM8 customer)
- **Number** — 4 digits starting at `1000`; increment by 1 for each additional process in the same group (1001, 1002, …)
- **Separator** — a literal colon `: `
- **Description** — 3–4 words describing what the process does (e.g. `Sync data from X`)

Example: `ITM81000: Sync data from SAP`

Always confirm the prefix and starting number with the user if not provided.

## Authentication

The Frends Platform API uses **Bearer token authentication** via an Azure AD app (client credentials flow).

**App registration details:**
- Azure AD Tenant ID: `97759401-0ff9-42fb-8eae-9163e29d19bf`
- Client ID: `b805d042-f146-4bb1-b7e8-515fdadca192`
- Scope: `api://b805d042-f146-4bb1-b7e8-515fdadca192/.default`

**Client secret:** Never stored in this file. Read it from the `FRENDS_CLIENT_SECRET` environment variable or a local `.env` file (gitignored).

To obtain a token:
```
POST https://login.microsoftonline.com/97759401-0ff9-42fb-8eae-9163e29d19bf/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=b805d042-f146-4bb1-b7e8-515fdadca192
&client_secret=<FRENDS_CLIENT_SECRET>
&scope=api://b805d042-f146-4bb1-b7e8-515fdadca192/.default
```

Use the returned `access_token` as `Authorization: Bearer <token>` on all Platform API requests.

## Workflow

1. **Clarify intent** — understand what the process should do: what triggers it, what it integrates, what the expected output is.
2. **Look up docs** — search Frends documentation for relevant tasks, triggers, connections, or agents before writing anything. Frends docs are the authoritative source — do not rely solely on exported processes as templates.
3. **Design the process** — describe the flow in plain language first (trigger → steps → output). Confirm with the user before building.
4. **Build via Platform API** — there is **no `POST /api/v1/processes`**. Processes are created by building a JSON payload and posting it to `POST /api/v1/processes/batch-import` (multipart/form-data, `file=@process.json`, `importConflict=NewActiveElement`). To update an existing process use `importConflict=NewVersion` (matches by GUID). See *Process creation method* below.
5. **Deploy** — `POST /api/v1/process-deployments` with `agentGroupId: 51`, `runOn: "Default01"`, `buildVersion: 0`.
6. **Verify** — after creation, confirm the process exists and is runnable on the tenant.

## Process creation method

Frends has no direct process creation endpoint. The only path is **export → modify → batch-import**:

1. Export a similar process as a template: `GET /api/v1/processes/{guid}/versions/{version}/export`
2. Modify the JSON: set new `Name`, `UniqueIdentifier` (new UUID), update `ElementParameters`, `Bpmn`, `TriggersJson`
3. **Critical:** when changing the `UniqueIdentifier`, also rename the matching key in `LinkedTasks`:
   ```python
   data['LinkedTasks'][new_guid] = data['LinkedTasks'].pop(old_guid)
   ```
4. Import: `POST /api/v1/processes/batch-import` with `importConflict=NewActiveElement` (new process) or `importConflict=NewVersion` (update existing by GUID)
5. Deploy immediately after import

**Valid `importConflict` values:** `Error`, `UseExisting`, `NewVersion`, `NewActiveElement`, `NewInactiveElement`. The value `OverwriteExistingActiveElement` does not exist.

**Never delete a deployed process via API** — it leaves stale deployment records on the agent and creates orphaned ghost records that block future `NewVersion` imports. Use the Frends UI to remove old processes.

## ElementParameters critical rules

These are the most common failure points when building process JSON:

### Foreach loop
- The inner flow **must end with a Return shape (Type 5)**, not InnerEnd (Type 14). Using Type 14 causes Frends to render a misconfigured "Catch" shape in the UI with "No outgoing connections" and "Variable name missing" errors.
- Multiple Return shapes are allowed inside a Foreach (one per branch).
- The BPMN element is `<bpmn2:endEvent>` for both — only the ElementParameter `Type` and `Parameters.expression` differ.

```json
{ "Id": "Event_fe_end", "Type": 5, "Name": "Iteration Complete",
  "Parameters": { "expression": {"mode": "csharp", "value": "true"} } }
```

### HTTP.Request tasks (Type 1)
Always include the **full options object** — partial options cause C# compilation errors on import. Key specifics:
- Bearer token auth → `"Authentication": {"mode": "select", "value": "OAuth"}` (not `"Bearer"`)
- `"ConnectionTimeoutSeconds": {"mode": "integer", "value": 30}` (mode must be `"integer"`, not `"csharp"`)

### Code elements (Type 12)
- `variableName` is a **plain string**, not a `{"mode": "text", "value": "..."}` object
- Use `return value;` not `#var.X = ...` assignment syntax
- Always include `shouldAssignVariable` — the server reads it unconditionally
- Do **not** use `try { } catch { }` — empty catch blocks cause "no outgoing connections" validation errors

### Gateways
- **ExclusiveGateway (Type 2):** condition expression goes on the **gateway** element; outgoing flows have empty Parameters
- **InclusiveGateway (Type 3):** conditions go on each **outgoing flow** (Type 4); the gateway element has no expression

### #result references
Use `#result[Element Name]` — no quotes inside the brackets. `#result["Element Name"]` is treated as a C# string indexer and causes "reference not valid" errors.

### ProcessScope (Type 8)
Do not wrap elements in a ProcessScope by default. Place tasks, gateways, and flows directly in the main process flow. Only use a scope when the design explicitly requires one.

## Principles

- Prefer reusing existing connections and agents on the development environment over creating new ones.
- Keep processes focused: one process per integration concern.
- If the user provides a rough description, ask clarifying questions rather than making assumptions about trigger type, error handling, or data mappings.
- Process trigger patterns are use-case driven — do not default to any particular trigger type; always clarify with the user.

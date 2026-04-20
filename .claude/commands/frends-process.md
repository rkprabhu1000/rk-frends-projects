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
2. **Look up docs** — search Frends documentation for relevant tasks, triggers, connections, or agents before proposing a design.
3. **Design the process** — describe the flow in plain language first (trigger → steps → output). Confirm with the user before building.
4. **Build via Platform API** — use the Frends Platform API to create processes, add tasks, configure connections, and deploy. Always target the development environment on `rktestfrends.frendsapp.com`.
5. **Verify** — after creation, confirm the process exists and is runnable on the tenant.

## Principles

- Prefer reusing existing connections and agents on the development environment over creating new ones.
- Keep processes focused: one process per integration concern.
- If the user provides a rough description, ask clarifying questions rather than making assumptions about trigger type, error handling, or data mappings.
- Process trigger patterns are use-case driven — do not default to any particular trigger type; always clarify with the user.

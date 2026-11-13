# Security and Data Protection Checklist

Run through this before declaring a Custom Task done, and during any code review. A Custom Task runs inside the Agent process with the Agent's privileges and is reused across many Processes, so a weakness here has a much wider blast radius than a single Process.

Findings map to OWASP Top 10 categories where applicable.

---

## Secrets and configuration — A02, A05

- [ ] No connection strings, API keys, tokens, passwords, certificates or tenant URLs in source, csproj, tests, sample code, or comments
- [ ] Secrets reach the Task as **parameters on the Connection tab**, supplied in Frends via Environment Variables (`#env.Group.Name`) — the Task itself never reads a config file or environment variable for credentials
- [ ] Secret-bearing parameters marked `[PasswordPropertyText]` so they are masked in the Control Panel
- [ ] The template's placeholder `ConnectionString` on the Connection tab replaced with real, correctly-typed parameters — or the Connection class deleted if the Task connects to nothing
- [ ] Tests use dotenv with an uncommitted `.env`; `.env.example` holds synthetic placeholders only
- [ ] No secret is written to the Result, exception messages, or any log output — including in `Error.AdditionalInfo`

## Injection — A03

- [ ] Database queries use parameterised commands; no string concatenation or interpolation of user input into SQL
- [ ] Command/shell invocation avoided; where unavoidable, arguments passed as an argument list, never a concatenated command line
- [ ] LDAP, XPath, and NoSQL queries escaped/parameterised the same way
- [ ] XML parsing has DTD processing and external entity resolution disabled (`XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }`)
- [ ] Deserialisation uses a safe configuration — no `TypeNameHandling.All` in Newtonsoft, no `BinaryFormatter`

## Request forgery and network — A10

- [ ] Tasks that accept a caller-supplied URL validate or allow-list the target where the Task is not intentionally a general-purpose HTTP client
- [ ] TLS certificate validation is **never** disabled; if the target genuinely needs a private CA, expose an explicit certificate/thumbprint parameter instead of a "skip validation" flag
- [ ] Redirects are followed only where intended, and not blindly across hosts
- [ ] Timeouts are set on every outbound call so a hung dependency cannot pin an Agent thread indefinitely

## Access control and least privilege — A01

- [ ] The Task requests the narrowest scope/permission set the operation requires; documented in the README
- [ ] File operations canonicalise paths and reject traversal outside the intended directory
- [ ] No ambient use of the Agent's own identity or filesystem beyond what the parameters describe

## Cryptography — A02

- [ ] No custom cryptography; use platform primitives
- [ ] No MD5/SHA-1 for security purposes, no ECB mode, no hardcoded IVs or keys
- [ ] Randomness for security purposes from `RandomNumberGenerator`, not `Random`

## Dependencies and integrity — A06, A08

- [ ] `dotnet list package --vulnerable --include-transitive` is clean, or findings are documented and accepted
- [ ] Every dependency licence verified as MIT / Apache 2.0 / BSD
- [ ] CI workflow references pinned (commit SHA for third-party reusable workflows and actions, not `@main`) — the template ships `@main` references to `FrendsPlatform/FrendsTasks`
- [ ] Dependency surface kept minimal — the assembly loads alongside other tasks in the Agent

## Logging and monitoring — A09

- [ ] Errors are actionable: `Error.Message` explains what failed, `AdditionalInfo` carries identifiers, status codes, and the failing items
- [ ] Payload bodies and personal data are **not** dumped into results or logs by default; if verbose diagnostics are useful, gate them behind an opt-in Options flag and document the risk
- [ ] Original exceptions preserved as inner exceptions so Process Instance View can be used for diagnosis

## Personal data — GDPR, data minimisation

- [ ] The Task processes only the fields necessary for its stated purpose; it does not fetch or return whole records "just in case"
- [ ] Where the source system supports field selection, the Task exposes it so integrations can minimise
- [ ] No personal data in test fixtures, sample values, `<example>` blocks, README, or CHANGELOG — use synthetic values
- [ ] No unnecessary local persistence: temp files are cleaned up, including on the failure path

## AI-related tasks — EU AI Act awareness

If the Task calls a model or wraps AI functionality:

- [ ] Model, provider, and version are explicit and configurable, not hidden constants — needed for auditability
- [ ] Inputs and outputs are traceable enough that a Process Instance can be reconstructed after the fact
- [ ] The README states that the output is model-generated and may be incorrect
- [ ] The Task does not make irrevocable decisions about individuals on its own — document that a human-in-the-loop step belongs in the calling Process

## Reliability

- [ ] `CancellationToken` honoured throughout, so stopping a Process actually stops the work
- [ ] `IDisposable` resources disposed on both success and failure paths
- [ ] `HttpClient` not created per call in a loop (socket exhaustion); reuse a static instance or a factory
- [ ] Idempotency considered for retry-able operations — Frends built-in retry uses a fixed delay and cannot filter by exception type, so a non-idempotent Task can duplicate work on retry

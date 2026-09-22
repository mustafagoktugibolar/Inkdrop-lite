# Inkdrop-lite — Product Requirements Document

Status: living document. Last reviewed against the code on 2026-09-20.

This file is the product-level source of truth for humans and coding agents.
Engineering conventions live in [AGENTS.md](../AGENTS.md) and [CLAUDE.md](../CLAUDE.md);
this file covers **what the product is, what it must do, and what is deliberately out of scope**.
Where this file and the code disagree, the code describes today and this file describes intent —
raise the mismatch instead of silently "fixing" either side.

Legend used below: **[Done]** implemented and tested, **[API only]** backend exists, no UI,
**[Planned]** agreed direction, not built, **[Open]** undecided — do not implement without a decision.

---

## 1. Summary

Inkdrop-lite is a lightweight, self-hosted Markdown note-taking app inspired by Inkdrop's data model.
Notes live in a nested notebook hierarchy, can be tagged, can carry file attachments, and can be
created from other notes acting as templates. A Vue SPA talks to an ASP.NET Core API backed by SQLite.
Sign-in is Microsoft Entra ID.

It is also a **knowledge base for agentic development workflows**: AI agents read and write notes
through the built-in MCP server (§9), and linking notes with Azure DevOps work items is a planned next step.

## 2. Users

| User | Description |
|---|---|
| Note author (primary) | An individual signed in with Entra ID who writes and organizes Markdown notes. |
| Agent | An AI agent acting on behalf of a signed-in user via the MCP server (§9). Must be subject to the same per-user isolation as the human. |

There is no sharing, collaboration, or public access. Each user only ever sees their own data.

## 3. Goals and non-goals

### Goals
- Fast, simple Markdown note taking with real hierarchy (notebooks), labels (tags) and attachments.
- Correct, boring relational modeling on SQLite; safe delete semantics; no data corruption from enum reordering.
- Strict per-user data isolation, enforced centrally, not by convention in each service.
- A stable, typed HTTP contract that a UI **and** an MCP server can both rely on.
- Small, maintainable codebase: a single API project, no speculative abstractions.

### Non-goals (current)
- Real-time collaboration, note sharing, public links.
- Trash / restore. (If added later: an explicit `DeletedAt`/`IsDeleted` on Note — never a CouchDB-style `bookId = "trash"`.)
- Full-text search infrastructure (SQLite FTS). Search, when built, starts as plain queries over
  `Note.Title`, `Note.Content`, `Tag.Name`, `Notebook.Name`.
- Persisted counters (`NoteCount`, `UsageCount`, task counts). Always derive them.
- Inkdrop/CouchDB artifacts: `_rev`, `_conflicts`, prefixed IDs (`note:xxx`), `doctype`, `share`.
- Repository pattern, generic repository, UnitOfWork, MediatR, CQRS, AutoMapper, event bus,
  separate Domain/Application/Infrastructure projects — **unless a concrete requirement appears**
  (see the decision log, §8).

## 4. Domain model

Guid primary keys everywhere. The backend owns `CreatedAt`/`UpdatedAt` (UTC); clients can never set them.
All owned entities carry `OwnerId` (`{tid}:{oid}` from the Entra token).

### Entities
| Entity | Key fields | Notes |
|---|---|---|
| **Notebook** | `Name` (≤64), `ParentNotebookId?`, `Order?`, `IconType` (`None`/`Svg`/`Attachment`), `IconSvg?` (≤256 KB), `IconAttachmentId?` | Self-referencing tree. |
| **Note** | `Title` (≤256), `Content` (Markdown, ≤1 MiB), `Status` (`None`/`Active`/`OnHold`/`Completed`/`Dropped`), `Pinned`, `NotebookId` (required), `SourceTemplateId?` | Templates are ordinary notes; there is no Template entity. |
| **Tag** | `Name` (≤64), `Color` (14 named colors, default `Default`) | Unique per owner, case-insensitive. |
| **Attachment** | `Name`, `ContentType`, `ContentLength` (≥0), `StoragePath` (unique, relative, e.g. `attachments/{guid}-x.png`), `Hash?` | **Metadata only.** Bytes live on disk, never in SQLite. Any content type. |
| NoteTag / NoteAttachment | `(NoteId, TagId)` / `(NoteId, AttachmentId)` composite PK | Implicit many-to-many join tables, no metadata, no `OwnerId`. |

Enums are persisted as strings **and** serialized as strings in JSON. Never persist or expose ordinals.

### Relationships and delete behavior
| Relationship | On delete |
|---|---|
| Notebook → child Notebook | RESTRICT (service returns 409) |
| Notebook → Note | RESTRICT (service returns 409) |
| Attachment → Notebook icon | SET NULL |
| Template Note → derived Note | SET NULL (derived notes survive) |
| Note/Tag → NoteTag | CASCADE (tag delete never deletes notes) |
| Note/Attachment → NoteAttachment | CASCADE (attachment delete never deletes notes) |

Deletes are **hard deletes**. Soft delete was intentionally removed so the FK behaviors above are real database behavior.

### Domain rules
- A notebook cannot be its own parent, nor moved under one of its descendants (checked in the service; a DB `CHECK` guards the self case).
- A note cannot be its own template (DB `CHECK`). `SourceTemplateId` is set at creation only.
- Every note belongs to exactly one notebook.
- Tag names are case-insensitive unique per owner (`NOCASE` collation on the unique index).
- Relationship-only changes (adding a tag) do not bump the note's `UpdatedAt`; editing the note's own fields does.
- Referenced entities (notebook, tags, parent, icon attachment, template) must belong to the caller, otherwise the request is rejected with 400.

## 5. Functional requirements and status

### Notes
- CRUD, list ordered by `UpdatedAt` desc, tag assignment by `TagIds` **[Done]**
- Status, pinned flag, move between notebooks **[Done]** (UI: pin toggle, status, agent/app source badge)
- Create from a template via `SourceTemplateId` **[Done]** (UI: "New note from this template" copies the content client-side; the API only records the link)
- Attach files to notes **[Done]** — `GET/PUT/DELETE /api/notes/{id}/attachments[/{attachmentId}]`; UI: attach, download, delete on the editor

### Notebooks
- CRUD, nesting via `ParentNotebookId`, manual `Order`, SVG/attachment icons **[Done]** (UI: collapsible tree, parent picker, order field, SVG or uploaded image icon; selecting a notebook includes its descendants). Icon attachments must be `image/*`.
- Drag-to-reorder **[Planned]**

### Tags
- CRUD with color **[Done]** API, name-only in UI **[Planned: color picker, usage counts]**
- Usage count is computed on demand, never stored **[Planned]**

### Attachments
- **[Done]** `POST /api/attachments` (multipart `file`), `GET /api/attachments`, `GET /{id}`, `GET /{id}/content`, `DELETE /{id}`.
- Storage layout: `{Attachments:Root}/attachments/{guid}{ext}` (default root `<content root>/data`; set `Attachments__Root` to `/data` in containers); the DB stores the relative path only. The client file name is display-only and never used on disk.
- Limit `Attachments:MaxBytes` (default 10 MiB, over-limit → 413; Kestrel's 30 MB request cap still applies). Empty files → 400. Content type is normalised, unknown → `application/octet-stream`.
- Downloads are always served as attachments with `nosniff` and `Content-Security-Policy: sandbox`, so uploaded HTML/SVG cannot run script in the API origin. The SPA fetches bytes with the bearer token (blob) since `<img>`/`<a>` cannot send it.
- Attachments are hard-deleted (row, then file); note links cascade and notebook icons are set to null by the database. No per-user quota yet.

### Search **[Done]** — plain `LIKE` queries over title, content, notebook name and tag name, via MCP `search_notes` and `GET /api/notes/search?query&notebookId&tagId&status&limit` (summaries, no content). The UI search box uses it (debounced). FTS5 only when needed; vector search deliberately deferred (needs an embedding provider and a second tenant-isolation path).

### Auth and tenancy **[Done]**
- Entra ID sign-in (MSAL in the SPA), bearer tokens validated by the API, `ApiScope` policy on all controllers, fallback policy requires authentication.
- Tenant isolation lives in `AppDbContext` (global query filter on `OwnerId`; writes stamp/verify `OwnerId`; writes without a user throw). **Services must not re-implement owner filtering.**

## 6. API surface

Everything except health checks requires the `ApiScope`. Rate limited by the `fixed` policy.

| Resource | Endpoints |
|---|---|
| `/api/notes` | `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` |
| `/api/notes/search` | `GET` (summaries) |
| `/api/notes/{id}/attachments` | `GET`, `PUT /{attachmentId}`, `DELETE /{attachmentId}` |
| `/api/attachments` | `GET`, `GET /{id}`, `GET /{id}/content`, `POST` (multipart), `DELETE /{id}` |
| `/api/notebooks` | same as notes CRUD |
| `/api/tags` | same |
| `/mcp` | MCP Streamable HTTP, same auth (see §9) |
| `/health/live`, `/health/ready` | anonymous |

Conventions: `POST` → 201 + body; `PUT`/`DELETE` → 204; unknown id (or another user's id) → 404;
rule violation → 400 ProblemDetails; blocked delete → 409 ProblemDetails.
Request/response records live in each feature's `Contracts/`. EF entities are never used as HTTP contracts.
Clients may not send `Id`, `CreatedAt`, `UpdatedAt`, navigation graphs, or join collections.
Foreign keys are accepted only where the operation needs them (`NotebookId`, `ParentNotebookId`, `TagIds`, `IconAttachmentId`, `SourceTemplateId` on create).

## 7. Non-functional requirements
- **Time:** all timestamps UTC (`Kind = Utc`) end to end; enforced by EF value converters registered as conventions.
- **Database:** SQLite; migrations applied at startup; unique indexes must respect the owner scope.
- **Reads** use `AsNoTracking`; tracked queries only when the entity will be modified.
- **Security:** no cross-tenant reads or writes, ever; every new owned entity derives from `UserOwnedEntity`/`UpdatableUserOwnedEntity` and gets a query filter.
- **Observability:** Serilog request logging, health checks, ProblemDetails for errors.
- **Testing:** service tests on in-memory SQLite (`SqliteTestDb`); boundary tests through `WebApplicationFactory` with a header-driven fake auth handler; every delete-behavior or isolation rule needs a test. No frontend test suite yet — run `npm run lint` and `npm run build`.

## 8. Decision log
| Date | Decision | Why |
|---|---|---|
| 2026-09-20 | Keep `OwnerId` multi-tenancy, drop soft delete | Entra users must stay isolated; soft delete masked FK delete semantics. |
| 2026-09-20 | Enums as strings in DB and JSON | Immune to reordering; `NoteStatus.None` shifted ordinals anyway. |
| 2026-09-20 | Tag uniqueness is `(OwnerId, Name)` NOCASE, not global | Global uniqueness would leak/collide across users. |
| 2026-09-20 | Rule violations via `RuleViolationException` → ProblemDetails | Service-level 400/409 without controller boilerplate. |
| 2026-09-20 | Stay a single API project; no CQRS/MediatR/DDD layering | No concrete need yet. Candidate first use of domain events: delete the physical file when an Attachment is removed (dispatch after `SaveChanges`). |
| 2026-09-20 | MCP server hosted in this API, same Entra token, read + create/update tools only | Reuses auth and tenant isolation with no extra code path; agents cannot cause irreversible loss. |
| 2026-09-20 | MCP login through an in-app OAuth facade in front of Entra (DCR + `resource` handling), agents sign in as a user | Entra lacks DCR and mishandles `resource` for `api://` registrations; clients then need no client id. Facade stays stateless and cannot widen scopes. |
| 2026-09-20 | Dedicated, optional `AzureAd:McpScope`; per-IP `oauth` rate-limit policy | MCP tokens must not double as API tokens; anonymous endpoints must not starve signed-in users. |
| 2026-09-20 | Track `CreatedSource`/`UpdatedSource` on notes, not a revision table | Answers "who wrote this" cheaply; history/undo deferred. |
| 2026-09-20 | `get_note` pages and outlines large notes instead of truncating silently | Agents pay context per character; let them pick the section they need. |
| 2026-09-20 | Migrations reset to one `InitialCreate` | Schema rewrite during early development; local DB files must be deleted. |

## 9. Roadmap and open questions

### Agent access — MCP server **[Done: v1]**
Agents connect to `POST /mcp` (Streamable HTTP, stateless) hosted **inside this API**. The endpoint uses the same
Entra bearer token, `ApiScope` policy and rate limit as `/api`, so an agent acts **as the signed-in user** and is bound by
the same per-user isolation. Tools call the existing feature services; there is no second data path.

**Login (OAuth facade).** MCP clients must be able to sign in without any per-client setup, but Entra has no Dynamic Client
Registration and rejects the RFC 8707 `resource` parameter unless it equals an HTTPS Application ID URI. So this API hosts a
thin authorization-server facade in front of Entra (`Features/Mcp/OAuth/McpOAuthEndpoints.cs`), all anonymous and rate limited:

| Endpoint | Role |
|---|---|
| `/.well-known/oauth-protected-resource/mcp` | Says the authorization server is **this API** (not Entra). `resource` = `{base}/mcp`. |
| `/.well-known/oauth-authorization-server` (+ `openid-configuration`) | Advertises the endpoints below, PKCE `S256`, public clients. |
| `POST /oauth/register` | RFC 7591. Returns the same public Entra `client_id` to every client; echoes validated `redirect_uris`. |
| `GET /oauth/authorize` | Requires `response_type=code` and PKCE S256, then 302s to Entra's authorize URL with the **fixed** client id and scopes and **without** `resource`. Fixed destination host, so not an open redirect. |
| `POST /oauth/token` | Server-side proxy to Entra's token endpoint for `authorization_code` and `refresh_token` only; forwards a whitelist of fields, replaces scopes, drops `resource`; never logs bodies. |

Flow: client hits `/mcp` → 401 with `resource_metadata` → reads metadata → registers → browser goes to `/oauth/authorize` → Entra signs the
user in and redirects straight back to the client's loopback URI with the code → client redeems it at `/oauth/token` → calls `/mcp` with the
Entra-issued access token, which `JwtBearer` validates like any other.

Design rules (do not weaken without a decision in §8):
- The facade issues no tokens, stores no state, holds no secrets. Entra authenticates the user, enforces the redirect URIs registered
  on the app registration, and mints the token.
- Clients can never choose their scopes, client id, or grant types. Scopes are always fixed by the server (see "Least privilege" below) plus `offline_access`.
- Anonymous OAuth endpoints use their own per-IP rate limit (`oauth` policy, 60/min/IP), separate from the shared `fixed` policy,
  so an unauthenticated flood cannot exhaust the quota signed-in users depend on. Behind a reverse proxy the client IP is the proxy's
  until forwarded headers are configured — do that before deploying behind one.

**Least privilege (`AzureAd:McpScope`).** Without it, an agent's token carries the same scope as the web UI's and could call
`DELETE /api/...` directly, so "the MCP tools have no delete" would only be a convenience, not a boundary. Setting `AzureAd:McpScope`
(e.g. `mcp_access`, defined under *Expose an API* in the app registration) makes:
- the OAuth facade request **only** that scope, so MCP tokens carry `scp=mcp_access`;
- `/mcp` accept only tokens with that scope (`McpScope` policy) and `/api` accept only `AzureAd:Scopes` (`ApiScope` policy) — an MCP token
  gets 403 on `/api`, a web token gets 403 on `/mcp`.
It must differ from `AzureAd:Scopes` (startup fails otherwise). When unset, MCP shares the API scope (current default until the scope
exists in Entra). Do not set it in configuration before the scope exists in the tenant, or MCP sign-in will fail.
- Configuration: `AzureAd:*` as for the API; optional `AzureAd:Audience` (Application ID URI, default `api://{ClientId}`) and
  `Mcp:PublicUrl` (set it when the app is behind a proxy so `issuer` and `resource` carry the public URL).

Entra app registration checklist (one-time, in the API's existing registration):
1. **Authentication → Add a platform → Mobile and desktop applications**, redirect URI `http://localhost/callback`
   (Entra ignores the port for loopback URIs; the path must match the client's, e.g. Claude Code uses `/callback`).
   Add other clients' redirect URIs as they show up in `AADSTS50011` errors.
2. **Expose an API → Add a scope** `mcp_access` (admins and users), then add the same scope under **API permissions** (My APIs → this app →
   Delegated) and grant consent; then set `AzureAd:McpScope` to `mcp_access`. See "Least privilege".
   The Application ID URI can stay `api://{ClientId}` because `resource` never reaches Entra.
3. Client setup is just `claude mcp add --transport http inkdrop <url>/mcp`, then `/mcp` → Authenticate. No client id needed.

Verified against a running instance: 401 challenge, both metadata documents, registration, and the 302 to the real Entra tenant;
the token proxy reached Entra and relayed its `invalid_grant` for a bogus code. **Not yet verified with a real MCP client completing a
sign-in** — do that first when connecting an agent.

| Tool | Kind | Purpose |
|---|---|---|
| `list_notebooks`, `list_tags` | read | Discover ids and hierarchy. |
| `search_notes` | read | `query` over title/content/notebook name/tag name (case-insensitive, wildcards literal); filters `notebookId`, `tagId`, `status`; `limit` 1-100. Returns summaries **with `contentLength`** (characters) so the agent can judge reading cost first. |
| `get_note` | read | Note metadata plus content. Whole note if it fits in one page; otherwise a page (`maxChars`, default 12000) with `hasMore`/`nextOffset`, a heading `outline`, and a `hint`. `section=<heading>` reads just that section (case-insensitive, substring match, ends at the next heading of the same or higher level); `offset` pages within the note or section. Headings inside code fences are ignored. |
| `create_note` | write | Requires `title` and `notebookId`. |
| `update_note` | write | Partial update; only passed fields change; `tagIds` replaces the tag set. |

**Audit trail.** Notes record `CreatedSource` and `UpdatedSource`: `app` for the web UI/HTTP API, or
`mcp:{clientName}/{clientVersion}` (`mcp` if the client sent no name) for agents. They are stamped centrally in
`AppDbContext.SaveChanges` from `ICurrentUser.ChangeSource`; clients cannot set them. This tells *which client* wrote a note,
not a full revision history (no per-change diffs, no undo). Only notes are tracked because agents can only write notes.

Rules for this surface:
- **No delete tools.** Deletes are hard and irreversible; adding any destructive tool needs a decision in §8. This limits what agents
  do *through MCP*; it is only a real boundary once `AzureAd:McpScope` is set (see Least privilege).
- Tools re-run the contract's DataAnnotations validation (controllers get that from model binding, tools do not) and translate
  `RuleViolationException` into MCP tool errors. Other exceptions stay generic.
- New tools live in `Features/Mcp/InkdropTools.cs`; each needs a test in `Inkdrop-lite.Tests/Api/McpEndpointTests.cs`
  including a cross-user isolation check. Keep agent context cost in mind: return summaries by default, content on demand.

Still **[Open]**:
- Notebook/tag write tools, attachment tools, and any destructive tool.
- Non-interactive agent auth: a user must sign in; there is no service-principal flow.
- Refresh-token lifetime and revocation follow Entra; the facade adds no session management.
- Smarter context: heading outline and paging exist; semantic search/ranking and snippets in search results do not.
- Real revision history (who changed what, restore) — only "last writer" is tracked today.

### Azure DevOps integration **[Planned]**
Two directions were mentioned; neither is designed yet.
- **Work item → note:** link/embed an Azure DevOps work item in a note.
- **Note → work item:** create a work item from a note.

Open questions **[Open]**: link model (a `WorkItemLink` entity vs. front-matter/metadata), one-way vs. two-way sync,
which fields map (title/status/description), auth to Azure DevOps (PAT vs. Entra), conflict handling, and whether
this belongs in the MCP server or the API.

### Other candidates
Attachment storage and endpoints; notebook tree UI; tag color/usage UI; pinned notes UI; template-based note creation UI;
search; domain events for side effects (file cleanup, future search index).

## 10. Working agreements for agents
1. Read [AGENTS.md](../AGENTS.md) and [CLAUDE.md](../CLAUDE.md) first; then this file.
2. Do not implement **[Open]** items or anything in the non-goals list without an explicit decision recorded in §8.
3. Backend change checklist: update entity + `AppDbContext` mapping → contracts → service → controller → tests → migration
   (`dotnet ef migrations add <Name> --project Inkdrop-lite`); check the generated migration, and discard noise-only snapshot diffs.
4. Any new owned entity: derive from the owned base, add an owner query filter, keep unique indexes owner-scoped.
5. Verify before claiming done: `dotnet build`, `dotnet test`; for client changes `npm run lint` and `npm run build`.
6. Record product-level decisions in the decision log; do not bury them in commit messages.

## 11. Known gaps
- Duplicate tag name currently surfaces as a database exception (500), not a friendly 409.
- Frontend does not yet expose parent notebooks, order, icons, tag colors, pinned, or templates; notes require a notebook (UI falls back to the first one).
- Nothing yet exposes `createdSource`/`updatedSource` in the UI.
- No attachment endpoints or file storage; no search endpoint/UI (only the MCP tool); no tag usage counts.
- `npm run lint` reports pre-existing `vue/multi-word-component-names` errors in generated `components/ui/`.
- Deleting a notebook with notes/children is blocked (409); there is no "move contents and delete" flow.

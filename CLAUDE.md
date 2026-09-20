# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

`AGENTS.md` covers repo layout, coding style, naming, testing and commit/PR conventions — read it too. This file only adds commands and cross-file architecture.

## Commands

Backend (repo root; solution is `Inkdrop-lite.slnx`, .NET 10, SQLite):

```sh
dotnet build Inkdrop-lite.slnx
dotnet test Inkdrop-lite.Tests/Inkdrop-lite.Tests.csproj
dotnet test Inkdrop-lite.Tests/Inkdrop-lite.Tests.csproj --filter "FullyQualifiedName~NoteServiceTests.GetAll_returns_notes_by_most_recent_update"   # single test
dotnet run --project Inkdrop-lite/Inkdrop-lite.csproj --launch-profile https   # http://localhost:5208, https://localhost:7168
dotnet ef migrations add <Name> --project Inkdrop-lite   # EF migrations live in Inkdrop-lite/Migrations
```

Frontend (`inkdrop-lite.client/`, Node ^22.18 or >=24.12): `npm run dev` (port 52364, strict), `npm run lint` (oxlint then ESLint, both with `--fix`), `npm run build` (vue-tsc type-check + vite build). There is no frontend test suite.

## Architecture

**Auth is Microsoft Entra ID end to end.** The SPA acquires a token via MSAL (`src/auth/entra.ts`, redirect handled by the separate `auth-redirect.html` entry, which is a second Vite rollup input) and `src/api/client.ts` attaches it as a bearer token to every call. The API validates it with `AddMicrosoftIdentityWebApi`; a fallback policy requires an authenticated user, and the `ApiScope` policy checks the `scp` claim against `AzureAd:Scopes` (startup throws if none is configured). Local Entra values go in `inkdrop-lite.client/.env.development` (copy `.env.example`); Vite proxies `/api` and `/health` to `VITE_API_PROXY_TARGET`.

**Multi-tenancy is enforced in `AppDbContext`, not in services.** All entities derive from `UserOwnedEntity` (`Domain/Common`). `AppDbContext` takes `ICurrentUser` (`HttpContextCurrentUser` builds the id as `{tid}:{oid}`) and:
- applies a global query filter `!IsDeleted && OwnerId == CurrentOwnerId` to Note, Notebook, Tag and NoteTag, so services never filter by owner or deleted state themselves;
- overrides `SaveChanges*` to stamp `OwnerId`/`CreatedAt`/`UpdatedAt` on add, refuse edits to other owners' rows, and convert `Delete` into a soft delete (`IsDeleted`, `DeletedAt`);
- throws if a write happens with no authenticated user.

Because deletes are soft, unique indexes (Notebook/Tag name per owner, NoteTag) are filtered with `"IsDeleted" = 0` — keep that filter on any new unique index. Use `IgnoreQueryFilters()` deliberately if you ever need deleted or cross-owner rows.

**Backend layering:** Controllers (thin, `RequireRateLimiting("fixed")`) → `Features/{Notes,Notebooks,Tags}` scoped services behind `I*Service` interfaces, with request/response records in each feature's `Contracts/`. Services take `AppDbContext` directly (no repository layer). Migrations are applied automatically at startup in `Program.cs` (`MigrateAsync`), and `public partial class Program` exists so tests can use `WebApplicationFactory`.

**Tests:** `InkdropWebApplicationFactory` swaps in a per-run temp SQLite file and a `TestAuthenticationHandler` driven by request headers (`CreateAuthenticatedClient(scope, userId)`), so boundary tests exercise real auth policies and per-user isolation without Entra. Service tests use `SqliteTestDb`.

**Frontend:** `src/api/{notes,notebooks,tags}.ts` are thin typed wrappers over `apiRequest` in `client.ts`, which throws `ApiError` carrying ASP.NET `ProblemDetails`. UI is shadcn-vue components in `src/components/ui/` (generated — prefer the `shadcn-vue` skill in `.agents/skills/` and `components.json` for adding/customizing them).

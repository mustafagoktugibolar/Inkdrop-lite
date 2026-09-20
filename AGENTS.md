# Repository Guidelines

## Project Structure & Module Organization

The solution is split into three projects:

- `Inkdrop-lite/` is the .NET 10 ASP.NET Core API. Controllers expose endpoints; `Features/{Notes,Notebooks,Tags}/` contains services, interfaces, and request/response contracts; `Models/` and `Domain/` hold entities; `Data/` and `Migrations/` manage EF Core and SQLite.
- `Inkdrop-lite.Tests/` contains xUnit tests, grouped by `Api/`, `Contracts/`, and `Features/`, with shared fixtures in `Infrastructure/`.
- `inkdrop-lite.client/` is the Vue 3, TypeScript, Vite, and Tailwind CSS client. Application code lives in `src/`; API adapters are in `src/api/`, authentication in `src/auth/`, and reusable shadcn-vue components in `src/components/ui/`.

Do not commit generated `bin/`, `obj/`, `dist/`, logs, databases, coverage output, or local environment files.

## Build, Test, and Development Commands

From the repository root:

```sh
dotnet restore Inkdrop-lite.slnx
dotnet build Inkdrop-lite.slnx
dotnet test Inkdrop-lite.Tests/Inkdrop-lite.Tests.csproj
dotnet run --project Inkdrop-lite/Inkdrop-lite.csproj --launch-profile https
```

From `inkdrop-lite.client/`:

```sh
npm install          # install locked dependencies
npm run dev          # start Vite; /api proxies to localhost:5208
npm run lint         # run oxlint and ESLint with fixes
npm run build        # type-check and create the production bundle
```

Copy `.env.example` to `.env.development` and supply local Entra ID values. Never commit credentials.

## Coding Style & Naming Conventions

Honor both `.editorconfig` files: C# uses repository defaults and CRLF; client files use two spaces, LF, and a 100-character target. Keep nullable reference types enabled. Use PascalCase for C# types/methods, camelCase for locals, and `Async` suffixes for asynchronous methods. Vue components use PascalCase filenames; TypeScript variables and functions use camelCase. Import client modules through the `@/` alias.

Prefer existing shadcn-vue components and semantic Tailwind tokens. Use `gap-*` layouts, `cn()` for conditional classes, and Hugeicons as configured in `components.json`.

## Testing Guidelines

Name xUnit classes `*Tests` and tests as behavior-focused underscore phrases, such as `GetAll_returns_notes_by_most_recent_update`. Add service tests for domain behavior and boundary tests for HTTP or authorization changes. There is no configured frontend test suite or coverage threshold; always run `npm run lint` and `npm run build` for client changes.

## Commit & Pull Request Guidelines

Recent history favors concise imperative subjects, commonly Conventional Commit prefixes such as `feat:`. Use prefixes like `feat:`, `fix:`, or `test:` and keep each commit focused. Pull requests should explain the behavior change, list validation commands, link related issues, call out migrations/configuration changes, and include screenshots for visible UI work.

# InstaSafe Backend — Session Handoff

## Canonical path
`C:\Users\MUSTAPHA\source\repos\InstaSafe Backend\InstaSafe Backend`
Solution: `InstaSafe Backend.slnx` (slnx format, `/src/` + `/tests/` folders).

## Structure (all net10.0, Nullable + ImplicitUsings enabled)
- `API/API.csproj` (Web SDK) ? refs Application, Infrastructure. Packages: DotNetEnv 3.2.0, FluentValidation.DI 11.11.0, MediatR 12.4.1, Microsoft.AspNetCore.OpenApi 10.0.12, EFCore.Design 9.0.8, Serilog.AspNetCore 9.0.0, Serilog.Settings.Configuration 9.0.0, Swashbuckle.AspNetCore 10.2.3.
- `Application/Application.csproj` ? refs Domain. Packages: AutoMapper 15.1.3, FluentValidation 11.11.0, MediatR 12.4.1, EFCore 9.0.8, Config.Abstractions 9.0.8, Logging.Abstractions 9.0.8.
- `Domain/Domain.csproj` ? no refs/packages.
- `Infrastructure/Infrastructure.csproj` ? refs Domain + Application. Packages: DotNetEnv, HtmlSanitizer 9.1.973, MediatR, EFCore 9.0.8 (+Design, +Relational), Config.Json, Hosting, Http, Logging.Abstractions, Npgsql.EFCore.PostgreSQL 9.0.4. Includes EF migrations (`Migrations/20260922152108_Initial`).
- `tests/` — API.IntegrationTests, Application.UnitTests (has real OrderMappingTests), Domain.UnitTests. xunit 2.9.3, Test.Sdk 17.14.1, coverlet 6.0.4.

## Verified working (2026-09-23)
- `dotnet restore` + `dotnet build "InstaSafe Backend.slnx"` ? 7/7, 0 warnings, 0 errors.
- `dotnet test` ? 5/5 passed.
- `dotnet run --project API/API.csproj --launch-profile http` ? `http://localhost:5080/health` OK, Postgres connected.
- VS: set `API` as startup project, `http` profile ? https://localhost:7080 + http://localhost:5080, Swagger opens.

## Config
- Real secrets live in root `.env` (copied from old repo; gitignored). `appsettings.json` holds non-secret structure.
- NOTE: old stray `src/Application/Common/Behaviors/appsettings.json` (contained live secrets, wrong location) was deliberately NOT migrated.
- `API/API.http` rewritten with real endpoints (health, orders CRUD, parse) on :5080.

## Runtime notes
- `OutboxPublisher` (Infrastructure/Outbox) polls `OutboxMessages` every 15s ? repeating SELECT in logs is expected. Currently MVP (marks published, no real dispatch).
- To quiet EF command logs in dev: set `Microsoft.EntityFrameworkCore.Database.Command: Warning` in `API/appsettings.Development.json`.
- Webhooks: `POST /webhooks/openwa` (register public URL at OpenWA gateway `POST /api/sessions/{sessionId}/webhooks`, events ["message.received"]), `POST /webhooks/paystack` (set in Paystack dashboard; secret verified via HMAC, dev-bypass when empty).
- Public exposure: `cloudflared tunnel --url http://localhost:5080` (user previously used trycloudflare URLs). Tunnel URL changes per restart ? re-register webhooks each time. User mentioned "outray" — unconfirmed which tool they meant.

## Pending
- Delete empty locked shell `C:\Users\MUSTAPHA\source\repos\Instasafe Api` (contents already removed; requires closing VS/processes first).
- Migration source of truth was the old repo; it is gone. This folder is now canonical.


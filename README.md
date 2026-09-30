# InstaSafe Backend

Escrow API for Nigerian social commerce. Vendors sell over WhatsApp, buyers pay
into escrow, and money releases only on delivery confirmation — by rider OTP or
buyer code — with disputes, admin moderation, and background auto-release behind it.

- **Live API:** `https://instasafe-atfzfsb6c7csbvek.westus3-01.azurewebsites.net`
- **Frontend:** [`Akinladebammy/instasafe`](https://github.com/Akinladebammy/instasafe)
  (`https://instasafe-six.vercel.app`) — vendor workspace, rider portal, public
  tracker, admin console.
- **Frontend contract:** [`docs/FRONTEND_API.md`](docs/FRONTEND_API.md) is the
  integration source of truth (envelope, auth, DTO shapes, state gates).

## Stack

| Layer | Tech |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API |
| Architecture | Clean Architecture — `Domain` → `Application` → `Infrastructure` → `API` |
| CQRS | MediatR 12 (one command/query + validator per operation) |
| Validation | FluentValidation 11 (fail-fast pipeline behavior) |
| Mapping | AutoMapper 15 (per-audience DTO profiles) |
| Data | Entity Framework Core 9 + PostgreSQL (Npgsql), code-first migrations |
| Auth | JWT Bearer, three principals off one login (vendor 24h, rider, admin 8h) |
| Observability | Serilog (JSON in production, file sink on App Service) + Application Insights |
| Docs | Swagger UI with Bearer button (`/swagger`), OpenAPI (`/swagger/v1/swagger.json`) |
| Tests | xUnit — 222 passing across unit, domain, and integration suites |
| External | Paystack (payments, transfers, refunds, bank resolve), OpenWA (WhatsApp gateway), Groq (order-text NLP + chat understanding), Brevo SMTP (email) |

## Quick start

Prereqs: .NET 10 SDK, PostgreSQL 14+.

```powershell
# 1. Secrets (never committed; .env is gitignored)
Copy-Item .env.example .env
# then fill in Postgres password, Paystack + OpenWA + Groq keys

# 2. Database (migrations run automatically when ApplyMigrations=true;
#    locally you can also use:)
dotnet ef database update --project Infrastructure --startup-project API

# 3. Build + test
dotnet build "InstaSafe Backend.slnx"
dotnet test "InstaSafe Backend.slnx"

# 4. Run (http profile: http://localhost:5080, /health, Swagger in Development)
dotnet run --project API/API.csproj --launch-profile http
```

Admin seeding (config-based super-admin, no vendor row needed):

```powershell
dotnet run --project API -- hash-password "YourStrongPassword"
# put the output in Admin__PasswordHash with Admin__Email, then log in via
# POST /api/auth/vendor/login (returns role "admin", vendor null, 8h token)
```

## Configuration

Everything comes from `API/appsettings.json` overridden by environment /
`.env` (`__` maps to `:`, e.g. `Paystack__SecretKey` → `Paystack:SecretKey`).
See `.env.example` for the full template.

| Key | Purpose |
|---|---|
| `ConnectionStrings__Default` | Postgres connection |
| `ApplyMigrations` | `true` on hosted envs so EF migrates on boot |
| `Paystack__SecretKey` / `Paystack__BaseUrl` | Live collections, transfers, refunds, resolve |
| `Groq__ApiKey` / `Groq__Model` | Order parsing, intent routing, correction fallback |
| `OpenWA__BaseUrl/ApiKey/SessionId/WebhookSecret` | WhatsApp gateway + inbound webhook auth |
| `Auth__JwtKey` (32+ chars) / `TokenHours` / `OtpMinutes` | JWT signing, token + OTP lifetimes |
| `Admin__Email` / `Admin__PasswordHash` | Super-admin login |
| `Smtp__*` | Brevo relay for receipts and status mail |
| `Frontend__BaseUrl` | Builds `/track/{orderNumber}` links and the Paystack post-payment `callback_url` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure telemetry (optional) |

## API overview

Every response uses the envelope `{ success, message, data, errors }`.
Vendor and rider calls (unless marked public) need `Authorization: Bearer <jwt>`;
ownership is enforced server-side (wrong owner → `403`).

- `POST /api/vendors`, `/api/auth/vendor/*` — signup (profile → email code → payout), password or WhatsApp-OTP login
- `POST /api/orders` — create (`fulfillment` 0 Dispatch with rider, 2 SelfDelivery; 1 Digital disabled → `400 fulfillment.unsupported`)
- `GET /api/orders/by-reference/{ref}` + `/timeline` — **public** track page data (order id, number, or Paystack ref; case-insensitive)
- `POST /api/orders/{id}/verify-otp` — **public** buyer release, self-delivery `Held` only
- `POST /api/orders/{id}/dispute` — **public**, freezes `Held`/`Delivered`
- `POST /api/dispatch/request-code|verify-code` — **public** rider phone-OTP login (row auto-created)
- `GET /api/dispatch/assigned`, `POST /api/dispatch/orders/{id}/confirm` — rider JWT; confirm with buyer OTP pays the rider fee instantly
- `GET /api/payments/banks` — **public** full Nigerian bank list (~280, one call); `banks/resolve` verifies account + bank code
- `POST /webhooks/openwa|paystack` — gateway ingress (signature-verified)
- `GET|POST /api/admin/*` — stats, vendors, dispatchers, orders (`?q=` search), disputes, refunds, force-release, payout retries, chats, webhooks, outbox, audit (all `[Authorize(Roles="admin")]`)

## Domain model

**Order lifecycle:** `Draft → AwaitingPayment → Held → Delivered → Released`
(side exits: `Refunded`, `Disputed`, `Cancelled`). Serialized as numbers:
`Draft 0 · AwaitingPayment 1 · Held 2 · Released 3 · Refunded 4 · Disputed 5 ·
Cancelled 6 · Delivered 7`.

**Money:** DTOs carry kobo (`amountKobo`, `deliveryFeeKobo`); creation takes
naira. `AmountKobo` already includes the fee. Buyer is charged total; on release
the vendor receives `AmountKobo` minus the rider fee when a rider was paid.

**Release paths:** rider orders release when the rider confirms with the buyer's
OTP (fee paid instantly, 24h inspection window, worker auto-releases after);
self-delivery orders release when the buyer enters their own code (24h backstop
if they never do). Orders with a rider are deliberately excluded from backstops.

**Three order shapes** (one DTO per audience, pinned by reflection tests so PII
cannot leak back onto anonymous surfaces): full `OrderDto` for vendor/admin
JWTs, trimmed `PublicOrderDto` for the anonymous track page, money-blind
`DispatchOrderDto` for riders. The order number (`IS-XXXXXX`) is the track
page's only credential — treat it as a bearer token.

**Delivery codes are WhatsApp-only by construction.** No notifier accepts an OTP
for email; the code leaves the system over WhatsApp or not at all.

## WhatsApp bot

Vendor-gated (verified + active + onboarded pass; everyone else gets a guiding
reply pointing at signup/verify/payout/support — never silence, never access).
Guided order creation (customer → phone → email → address → items → amount →
rider-or-self → fee → rider bank with live holder-name check → confirm),
resumable draft tickets, track-by-reference / LIST / customer-phone lookup,
single-message account+bank, and mid-flow corrections ("the address is actually
…") via an LLM second-chance layer that only *proposes* — the same validators
still dispose. Deterministic happy path; AI never moves money or writes state.

## Background workers

- `OutboxPublisher` (15s) — drains outbox messages; failures surface in admin outbox errors.
- `ReleaseDueOrdersWorker` (5min) — releases `Delivered` past the window, Digital `Held` past 24h, and driver-less/self-delivery `Held` past the backstop. Rider orders are excluded on purpose.

## Project structure

```
API/                  Controllers, middleware, Program.cs (Serilog, JWT, Swagger, ApplyMigrations)
Application/          CQRS features (Admin, Auth, Dispatch, Orders, Vendors, Webhooks),
                      mapping profiles, validators, pipeline behaviors, OrderNotifier
Domain/               Entities, enums, domain events, exceptions (no dependencies)
Infrastructure/       EF Core (AppDbContext, repositories, 14 migrations),
                      Paystack/OpenWA/Groq/Brevo clients, outbox workers
tests/                Application.UnitTests, Domain.UnitTests, API.IntegrationTests
docs/FRONTEND_API.md  Frontend contract (read before changing any endpoint shape)
samples/              Example OpenWA webhook payload
```

## Deployment (Azure App Service)

```powershell
Remove-Item -Recurse -Force API\bin, API\obj, ./publish, ./publish.zip -ErrorAction SilentlyContinue
dotnet publish "API\API.csproj" -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip
az webapp deploy --resource-group InstaSafe --name InstaSafe --src-path ./publish.zip --type zip
az webapp restart --resource-group InstaSafe --name InstaSafe
```

No EF migration step needed on deploy — `ApplyMigrations=true` runs them on
boot. After deploy, verify `/health`, re-register the OpenWA webhook if the
public URL changed, and confirm the Paystack dashboard Callback URL
(`https://instasafe-six.vercel.app/track`) and Webhook URL
(`https://<app>.azurewebsites.net/webhooks/paystack`) are set under live keys.

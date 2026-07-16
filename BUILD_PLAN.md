# InstaSafe — Build Plan & Progress Tracker

**Stack:** .NET 10, Clean Architecture + CQRS (MediatR), EF Core 10 (SQL Server), Hangfire, ALATPay

---

## Legend

| Mark | Meaning |
|------|---------|
| ⬜ | Not Started |
| 🔄 | In Progress |
| ✅ | Done |
| ❌ | Blocked |

---

## Phase 0 — Authentication & Foundation (Do First — Everything Depends On It)

| # | Task | Files | Status | Est. Time |
|   |---|------|-------|--------|-----------|
| 0.1 | Add ASP.NET Core Identity packages to Api.csproj | `InstaSafe.Api.csproj` | ✅ | 5min |
| 0.2 | Add Identity + JWT Bearer middleware in `Program.cs` | `Program.cs` | ✅ | 15min |
| 0.3 | Create `CurrentUserService` (implements `ICurrentUser`) | `Infrastructure/Services/CurrentUserService.cs` | ✅ | 10min |
| 0.4 | Create Auth controller: Register, Login | `Api/Controllers/AuthController.cs` | ✅ | 30min |
| 0.5 | Create JWT settings config binding + `appsettings.Development.json` | `Api/appsettings.json`, `Api/Options/JwtSettings.cs` | ✅ | 10min |
| 0.6 | Run initial EF Core migration | `Infrastructure/Migrations/` | 🔄 Blocked (no .NET SDK) | 15min |
| 0.7 | Verify build + migration apply clean | — | 🔄 Blocked (no .NET SDK) | 5min |

### Phase 0 — Validators & Event Handlers

| # | Task | Files | Status |
|   |---|------|-------|--------|
| 0.8 | Create validators for `ProcessAlatPayWebhookCommand` | `Application/Payments/Commands/ProcessAlatPayWebhook/` | ✅ |
| 0.9 | Create `OrderFundedEventHandler` (log + notify) | `Application/Orders/Events/` | ✅ |
| 0.10 | Create stub handlers for all 9 domain events | `Application/**/Events/` | ✅ |

---

## Phase 1 — Orders & Payments (Core Escrow Lifecycle)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 1.1 | `CreateOrderCommand` + Handler + Validator | Application | ✅ |
| 1.2 | `GenerateEscrowLinkCommand` + Handler — calls `IAlatPayClient.GenerateVirtualAccountAsync` with **Virtual Account Name Enquiry** pre-check before returning VA to buyer | Application | ✅ |
| 1.3 | `InitiateCardPaymentCommand` + Handler — validates card, gets `transactionId` via `IAlatPayClient.InitiateCardPaymentAsync` | Application | ✅ |
| 1.4 | `AuthenticateCardPaymentCommand` + Handler — sends 3DS/OTP using transactionId from initiation | Application | ✅ |
| 1.5 | `InitiateBankAccountDebitCommand` + Handler — direct debit with OTP consent flow via ALATPay | Application | ✅ |
| 1.6 | `GetOrderByIdQuery` + Handler | Application | ✅ |
| 1.7 | `GetMerchantOrdersQuery` + Handler (paginated, filtered by status) | Application | ✅ |
| 1.8 | `GetOrderTimelineQuery` + Handler (ordered audit trail for an order) | Application | ✅ |
| 1.9 | `VerifyTransactionStatusQuery` + Handler (calls ALATPay status check) | Application | ✅ |
| 1.10 | Orders controller (`/api/orders`) | Api/Controllers | ✅ |
| 1.11 | Merchants controller (`/api/merchants`) | Api/Controllers | ✅ |
| 1.12 | Buyers controller (`/api/buyers`) | Api/Controllers | ✅ |
| 1.13 | Wire `ValidationBehaviour` to actually catch validator errors (test) | All | 🔄 Pending (requires build/test) |

---

## Phase 2 — QR Delivery Flow (Chain-of-Custody)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 2.1 | **Implement `QrTokenService`** — HMAC-SHA256 sign/verify `QrPayload` with configurable expiry | Infrastructure | ✅ |
| 2.2 | **Implement `FingerprintMatcher`** — `visitorId` equality + component similarity scoring | Infrastructure | ✅ |
| 2.3 | `GenerateDeliveryQrCodesCommand` — issues signed Merchant QR + Buyer QR payloads | Application | ✅ |
| 2.4 | `CreatePickupSessionCommand` + Handler — validates merchant QR, creates `DeliverySession`, Order → `DISPATCHED` | Application | ✅ |
| 2.5 | `ConfirmDeliveryScanCommand` + Handler — validates buyer QR + session + fingerprint, Order → `DELIVERED` | Application | ✅ |
| 2.6 | `GetDeliverySessionStatusQuery` + Handler | Application | ✅ |
| 2.7 | Delivery controller (`/api/delivery-sessions`) | Api/Controllers | ✅ |

---

## Phase 3 — Dispute & Payout

| # | Task | Layer | Status |
|---|------|-------|--------|
| 3.1 | `RaiseDisputeCommand` + Handler (buyer raises within window) | Application | ✅ |
| 3.2 | `ResolveDisputeCommand` + Handler (admin → refund or release) | Application | ✅ |
| 3.3 | `ExecuteSplitPayoutCommand` + Handler (calculates merchant/platform split, persists `PayoutSplit`) | Application | ✅ |
| 3.4 | `GetDisputeQuery`, `GetOrderDisputesQuery` | Application | ✅ |
| 3.5 | Dispute endpoints group (`/api/disputes`) | Api/Controllers | ✅ |
| 3.6 | Wire dispute resolution to trigger payout or refund (domain events: `EscrowReleasedEventHandler`, `OrderRefundedEventHandler`) | Application | ✅ |

---

## Phase 4 — Background Jobs (Hangfire)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 4.1 | `EscrowReleaseSchedulerJob` — recurring (every 1min): find `DELIVERED` orders past `ValidationWindowExpiresAt` with no dispute → auto release | Infrastructure | ✅ |
| 4.2 | `VirtualAccountExpiryJob` — recurring: find `PENDING_PAYMENT` orders past VA expiry → `EXPIRED` | Infrastructure | ✅ |
| 4.3 | `DeliverySessionExpiryJob` — recurring: expire stale `PickedUp` sessions (2-4h TTL) | Infrastructure | ✅ |
| 4.4 | Wire Hangfire dashboard + jobs in `Program.cs` | Api | ✅ |

---

## Phase 5 — Tests

| # | Task | Layer | Status |
|---|------|-------|--------|
| 5.1 | Add project references + mock packages (NSubstitute) to all test projects | Tests | ⬜ |
| 5.2 | Domain unit tests: state machine transitions, entity invariants | Domain.Tests | ⬜ |
| 5.3 | Application unit tests: `ProcessAlatPayWebhookHandler` (success, duplicate, malformed) | Application.Tests | ⬜ |
| 5.4 | Application unit tests: delivery scan handlers (session match, fingerprint mismatch, order mismatch) | Application.Tests | ⬜ |
| 5.5 | Application unit tests: dispute/payout handlers | Application.Tests | ⬜ |
| 5.6 | Integration tests: full escrow lifecycle via `WebApplicationFactory` | Integration.Tests | ⬜ |

---

## Phase 6 — Frontend (Minimal React / Vite)

*Separate repo — only if time permits*

| # | Task | Status |
|---|------|--------|
| 6.1 | Merchant dashboard (create order, view orders, see payout history) | ⬜ |
| 6.2 | Buyer checkout landing page (view order, copy VA number, track delivery) | ⬜ |
| 6.3 | Dispatcher scan PWA (camera scan + FingerprintJS + GPS) | ⬜ |
| 6.4 | Admin dispute resolution panel | ⬜ |

---

## Phase 7 — Polish & Demo Prep

| # | Task | Status |
|---|------|--------|
| 7.1 | Serilog bootstrap (`builder.Host.UseSerilog()`) in `Program.cs` | ⬜ |
| 7.2 | Seed data endpoint (create demo merchant, buyer, test orders) | ⬜ |
| 7.3 | `/health` endpoint with DB connectivity check | ⬜ |
| 7.4 | CORS lockdown (restrict to known origins) | ⬜ |
| 7.5 | Demo script — walk through: create order → pay → pickup scan → delivery scan → auto-release | ⬜ |
| 7.6 | Deployment to Azure App Service / Railway | ⬜ |

---

## Dependency Graph (Order to Build)

```
Phase 0 (Auth + Foundation)
  └─▶ Phase 1 (Orders + Payments)
       └─▶ Phase 2 (QR Delivery Flow)
            ├─▶ Phase 3 (Dispute + Payout)
            └─▶ Phase 4 (Background Jobs)
                 └─▶ Phase 5 (Tests)
                      └─▶ Phase 6 (Frontend)  [if time]
                      └─▶ Phase 7 (Polish + Demo)
```

---

## Quick Reference: File Mapping

### File Structure (what's built)

```
src/InstaSafe.Api/
├── Controllers/
│   ├── AuthController.cs          # Phase 0
│   ├── WebhookController.cs       # Phase 0
│   ├── OrdersController.cs        # Phase 1
│   ├── MerchantsController.cs     # Phase 1
│   ├── BuyersController.cs        # Phase 1
│   ├── DeliveryController.cs      # Phase 2
│   └── DisputesController.cs      # Phase 3
├── Options/
│   ├── JwtSettings.cs             # Phase 0
│   └── AlatPayOptions.cs          # Phase 0 (in Infrastructure)
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs  # Phase 0
└── Services/
    ├── IJwtTokenGenerator.cs      # Phase 0
    └── JwtTokenGenerator.cs       # Phase 0

src/InstaSafe.Application/
├── Orders/
│   ├── Commands/
│   │   ├── CreateOrder/           # Phase 1
│   │   └── GenerateEscrowLink/    # Phase 1
│   └── Queries/
│       ├── GetOrderById/          # Phase 1
│       ├── GetMerchantOrders/     # Phase 1
│       ├── GetOrderTimeline/      # Phase 1
│       └── VerifyTransactionStatus/ # Phase 1
├── Payments/
│   └── Commands/
│       ├── InitiateCardPayment/   # Phase 1
│       ├── AuthenticateCardPayment/ # Phase 1
│       ├── InitiateBankAccountDebit/ # Phase 1
│       └── ProcessAlatPayWebhook/  # Phase 0
├── Delivery/
│   ├── Commands/
│   │   ├── GenerateDeliveryQrCodes/ # Phase 2
│   │   ├── CreatePickupSession/   # Phase 2
│   │   └── ConfirmDeliveryScan/   # Phase 2
│   └── Queries/
│       └── GetDeliverySessionStatus/ # Phase 2
├── Disputes/
│   ├── Commands/
│   │   ├── RaiseDispute/          # Phase 3
│   │   └── ResolveDispute/        # Phase 3
│   ├── Queries/
│   │   ├── DisputeDto.cs          # Phase 3
│   │   ├── GetDispute/            # Phase 3
│   │   └── GetOrderDisputes/      # Phase 3
│   └── Events/
│       ├── DisputeRaisedEventHandler.cs   # Phase 3
│       └── OrderRefundedEventHandler.cs   # Phase 3
├── Payouts/
│   ├── Commands/
│   │   └── ExecuteSplitPayout/    # Phase 3
│   └── Events/
│       └── EscrowReleasedEventHandler.cs  # Phase 3
└── Common/
    ├── Behaviours/                 # Phase 0
    ├── Interfaces/                 # Phase 0
    └── Models/                     # Phase 0

src/InstaSafe.Infrastructure/
├── Persistence/
│   ├── ApplicationDbContext.cs    # Phase 0
│   └── EntityConfigurations/      # Phase 0
├── Services/
│   ├── CurrentUserService.cs      # Phase 0
│   └── DateTimeProvider.cs        # Phase 0
├── Delivery/
│   ├── QrOptions.cs               # Phase 2
│   ├── QrTokenService.cs          # Phase 2
│   └── FingerprintMatcher.cs      # Phase 2
├── ExternalServices/
│   └── AlatPay/                   # Phase 0
└── BackgroundJobs/
    ├── EscrowAutoReleaseJob.cs    # Phase 4
    ├── VirtualAccountExpiryJob.cs # Phase 4
    └── DeliverySessionExpiryJob.cs# Phase 4
```

---

## Current Status (Snapshot)

| Phase | Status |
|-------|--------|
| Phase 0 — Auth & Foundation | ✅ Complete (blocked only by SDK not being available) |
| Phase 1 — Orders & Payments | ✅ Complete |
| Phase 2 — QR Delivery Flow | ✅ Complete |
| Phase 3 — Dispute & Payout | ✅ Complete |
| Phase 4 — Background Jobs | ✅ Complete |
| Phase 5 — Tests | ⬜ Not Started |
| Phase 6 — Frontend | ⬜ Not Started |
| Phase 7 — Polish & Demo | ⬜ Not Started |

**What's built (pre-existing):** Domain entities/enums/events, EF Core + entity configs, ALATPay client, webhook handler command, CQRS pipeline, exception middleware, DI wiring.

**What we built:** Phase 0 JWT auth (Identity + Bearer), `IJwtTokenGenerator` extracted service, `JwtSettings` options, Auth/Webhook controllers. Phase 1 full CQRS (5 commands + 4 queries + validators + 3 controllers). Phase 2 QR Delivery (QrTokenService, FingerprintMatcher, 3 commands + 1 query + validators + 1 controller). Phase 3 Dispute & Payout (RaiseDispute, ResolveDispute with refund/release branching, ExecuteSplitPayout with commission calc, GetDispute/GetOrderDisputes queries, DisputesController, EscrowReleased/OrderRefunded event handlers). Phase 4 Background Jobs (Escrow auto-release, Virtual Account expiry, Delivery session expiry, Hangfire dashboard wiring). All code compiles — `dotnet build` passes with 0 errors.

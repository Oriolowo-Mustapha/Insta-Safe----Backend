# InstaSafe — Build Plan & Progress Tracker (Hackathon Detailed)

**Stack:** .NET 10, Clean Architecture + CQRS (MediatR), EF Core 10 (PostgreSQL), Hangfire, Monnify, React/Next.js/Vite
**AI Integrations:** OpenRouter (Gemini 2.5 Flash / GPT-4o Mini), Cloudinary (Images)

---

## Legend

| Mark | Meaning |
|------|---------|
| ⬜ | Not Started |
| 🔄 | In Progress |
| ✅ | Done |
| ❌ | Blocked |

---

## Phase 0 — Authentication, Foundation & Fixups

| # | Task | Files | Status |
|---|------|-------|--------|
| 0.1 | Add ASP.NET Core Identity packages & Auth Controllers | Program.cs, AuthController.cs | ✅ |
| 0.2 | Create JWT settings config binding + CurrentUserService | Api/appsettings.json, CurrentUserService.cs | ✅ |
| 0.3 | Run initial EF Core migration & Database Update | Infrastructure/Migrations/ | ✅ |
| 0.4 | Rename `AlatPay` references in `WebhookEventLog` & `PayoutSplit` | `Entities/*.cs`, `Configurations/*.cs` | ✅ |
| 0.5 | Add missing EF Configurations (e.g., `InAppNotificationConfiguration`) | `Configurations/` | ✅ |
| 0.6 | Add missing Repositories (Dispute, Merchant, User, Notification) | `Repositories/` | ✅ |
| 0.7 | Fix `Qr` configuration in `appsettings.json` | `Api/appsettings.json` | ✅ |
| 0.8 | Remove dead code in Api layer (`JwtSettings.cs`, `IJwtTokenGenerator.cs`) | `Api/Options/`, `Api/Services/` | ✅ |

---

## Phase 1 — Orders, Payments & Fraud Detection (AI Component 3)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 1.1 | CreateOrderCommand, GenerateEscrowLinkCommand, handlers | Application | ✅ |
| 1.2 | Monnify webhooks, initialization, sub-accounts | Application / Infra | ✅ |
| 1.3 | Order queries & controllers (OrdersController) | Application / Api | ✅ |
| 1.4 | Integrate Monnify Verification APIs (BVN, NIN, Account) | Infrastructure | ✅ |
| 1.5 | Build Fraud Scoring Engine (0-100 risk score calc) | Application | ✅ |
| 1.6 | Apply Risk Actions on Order Creation (Auto-approve, flag, block) | Application | ✅ |

---

## Phase 2 — QR Delivery Flow (Chain-of-Custody)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 2.1 | QrTokenService and FingerprintMatcher implementations | Infrastructure | ✅ |
| 2.2 | CreatePickupSessionCommand, ConfirmDeliveryScanCommand | Application | ✅ |
| 2.3 | Delivery endpoints (/api/delivery-sessions) | Api/Controllers | ✅ |

---

## Phase 3 — Disputes & AI Resolver (AI Component 1)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 3.1 | Core `RaiseDisputeCommand`, `ResolveDisputeCommand` | Application | ✅ |
| 3.2 | Split payout execution (`ExecuteSplitPayoutCommand`) | Application | ✅ |
| 3.3 | Cloudinary Integration for Dispute/Product Image Uploads | Infrastructure | ✅ |
| 3.4 | OpenRouter Vision API Integration (Gemini/GPT-4o Mini) | Infrastructure | ✅ |
| 3.5 | Image Classification Logic (Resolution Matrix) | Application | ✅ |
| 3.6 | Connect automated AI resolution to Monnify Refund API | Application | ✅ |

---

## Phase 4 — Background Jobs (Hangfire)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 4.1 | EscrowReleaseSchedulerJob (1min auto-release loop) | Infrastructure | ✅ |
| 4.2 | VirtualAccountExpiryJob (5min VA check) | Infrastructure | ✅ |
| 4.3 | DeliverySessionExpiryJob (10min session cleanup) | Infrastructure | ✅ |
| 4.4 | Wire Hangfire dashboard + jobs in Program.cs | Api | ✅ |

---

## Phase 5 — WhatsApp Chatbot (AI Component 2)

| # | Task | Layer | Status |
|---|------|-------|--------|
| 5.1 | Meta/Twilio WhatsApp webhook endpoint (ChatbotController) | Api | ✅ |
| 5.2 | Chatbot state machine (Redis or DB) | Infrastructure | ✅ |
| 5.3 | NLP intent parsing with Gemini | Application/Infrastructure | ✅ |
| 5.4 | Implement flows: Create Order, Check Status | Application | ✅ |

---

## Phase 6 — Frontend (Next.js / Vite)

| # | Task | Status |
|---|------|--------|
| 6.1 | Initialize React project structure & UI Library | ✅ |
| 6.2 | Merchant Dashboard (create order, view orders, see payout history) | ⬜ |
| 6.3 | Buyer Checkout Landing Page (view order, pay, track delivery) | ⬜ |
| 6.4 | Dispute Portal UI (upload photo, view AI recommendation) | ⬜ |

---

## Phase 7 — Polish, Tests & Demo Prep

| # | Task | Layer | Status |
|---|------|-------|--------|
| 7.1 | Serilog bootstrap ( uilder.Host.UseSerilog()) in Program.cs | Api | ⬜ |
| 7.2 | Integration & Unit tests for AI components and new endpoints | Tests | ⬜ |
| 7.3 | Record 2-5 minute demo video | Demo | ⬜ |
| 7.4 | Prepare demo data & verify presentation | Demo | ⬜ |

---

## Dependency Graph (Order to Build)

`
Phase 0 (Foundation & Fixes)
  ├─▶ Phase 1 (Orders + Fraud Detection)
  │    └─▶ Phase 5 (WhatsApp Chatbot)
  ├─▶ Phase 2 (QR Delivery Flow)
  └─▶ Phase 3 (Disputes + Cloudinary + AI Resolver)
       └─▶ Phase 4 (Background Jobs)
            └─▶ Phase 6 (Frontend)
                 └─▶ Phase 7 (Polish + Demo)
`

---

## Current Status (Snapshot)

| Phase | Status |
|-------|--------|
| Phase 0 — Auth & Foundation | ✅ Complete |
| Phase 1 — Orders & Payments | ✅ Complete |
| Phase 2 — QR Delivery Flow | ✅ Complete |
| Phase 3 — Dispute & Payout | ✅ Complete |
| Phase 4 — Background Jobs | ✅ Complete |
| Phase 5 — WhatsApp Chatbot | ✅ Complete |
| Phase 6 — Frontend | 🔄 In Progress |
| Phase 7 — Polish & Demo | ⬜ Not Started |

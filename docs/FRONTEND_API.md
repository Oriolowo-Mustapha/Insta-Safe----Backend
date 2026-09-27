# InstaSafe Backend — Frontend API Guide

Base URL (production): `https://instasafe-atfzfsb6c7csbvek.westus3-01.azurewebsites.net`
Local: `http://localhost:5080` · Interactive reference: `/swagger/index.html`

## Conventions

### Envelope
Every response is wrapped:
```json
{ "success": true, "message": "Optional human message", "data": {}, "errors": null }
```
`success: false` → `message` explains, `errors` may list field problems.

### Auth
Vendor and driver calls (unless marked **Public**) need:
```
Authorization: Bearer <jwt>
```
- Vendor JWT: `POST /api/auth/vendor/login` (or phone-OTP verify). Contains `vendor_id` + phone. Expires in 24h (`expiresInHours` in the login response).
- Driver JWT: `POST /api/dispatch/verify-code`. Contains `dispatcher_id`.
- Ownership is enforced server-side: a vendor can only touch their own profile, payout, orders. Wrong owner → `403`.

### Status codes you will see
| Code | Meaning here |
|---|---|
| 200 | OK (`success` may still be false — always check the envelope) |
| 400 | Validation / business-rule failure (`message` tells why) |
| 401 | Missing/invalid token, or wrong login/OTP code |
| 403 | Valid token but not your resource |
| 404 | Unknown id/reference |
| 409 | Conflict, e.g. wrong state for the action (message names the state) |

### Money
All amounts in DTOs are **kobo** (`amountKobo`, `deliveryFeeKobo`). Display naira = value / 100.
Order creation takes **naira** (`amountNgn`, `deliveryFeeNgn`). Buyer is charged `amountNgn + deliveryFeeNgn`.

### Phone numbers
Nigerian mobiles, canonical `234...` form everywhere. The API normalizes `080...`/`+234...`/`234...` automatically, but **validate client-side**: 11 digits starting `070/080/081/090/091` (or `234` + 10 digits). Anything else → `400 "must be a valid Nigerian mobile number"`. Store and display the `234...` form the API returns.

### OrderStatus
`Draft | AwaitingPayment | Held | Delivered | Released | Refunded | Disputed | Cancelled`
(New: `Delivered = 7`, funds frozen in 24h inspection window.)

---

## 1. Vendor signup (3 steps, web)

### Step 1 — Register
`POST /api/vendors` **(Public)**
```json
{
  "phone": "08012345678",
  "displayName": "Ada Boutique",
  "firstName": "Ada",
  "lastName": "Obi",
  "email": "ada@example.com",
  "password": "s3cretPass!"
}
```
- Profile + password only — **no bank details here**. Payout setup happens after email verification (step 2).
- Duplicate phone or email → `400 "already exists"`. Malformed phone → `400` (Nigerian mobile rule above).
- Sends a 6-digit code to the email. Returns the vendor with `emailVerified: false`, `onboardingCompleted: false`.

### Step 1b — Resend email code
`POST /api/auth/vendor/request-email-code` **(Public)** `{ "email": "..." }`
Fails if already verified.

### Step 1c — Verify email
`POST /api/auth/vendor/verify-email` **(Public)** `{ "email": "...", "code": "123456" }`
Code: 10-min expiry, 5-attempt lock. Returns vendor with `emailVerified: true`.

### Step 2 — Payout setup (finishes onboarding)
`PUT /api/vendors/{id}/payout` (vendor JWT, own id)
`{ "accountNumber": "0123456789", "bankCode": "058" }` → creates Paystack recipient, sets `onboardingCompleted: true`.
Build the form as: bank dropdown (`GET /api/payments/banks`, public) → account number input → **live verify** (`GET /api/payments/banks/resolve?accountNumber=...&bankCode=...`, public) → show returned `accountName` and ask the vendor to confirm it matches their name → only then `PUT payout`.

### Recommended frontend gating
Read `vendor.emailVerified` / `vendor.onboardingCompleted` from any vendor response:
- `!emailVerified` → show verify screen
- `!onboardingCompleted` → show payout form
- both true → dashboard + "you can now use the WhatsApp bot"

---

## 2. Login

### Primary — password (phone OR email)
`POST /api/auth/vendor/login` **(Public)**
`{ "loginId": "ada@example.com", "password": "s3cretPass!" }`
`loginId` accepts email or phone. Wrong details → `401`. Success → `{ token, vendor, expiresInHours }`.

### Fallback — WhatsApp OTP (recovery only, keep it out of the happy path)
`POST /api/auth/vendor/request-code` `{ "phone": "..." }` → code via WhatsApp →
`POST /api/auth/vendor/verify-code` `{ "phone": "...", "code": "..." }` → same JWT shape.

---

## 3. Vendor profile

All vendor JWT, own id only (else `403`):
- `GET /api/vendors/{id}` — profile
- `GET /api/vendors/by-phone?phone=...` — lookup (must be own phone)
- `GET /api/vendors` — returns `[self]` (no directory listing by design)
- `PUT /api/vendors/{id}` `{ "displayName": "..." }` — rename business
- `PUT /api/vendors/{id}/phone` `{ "phone": "07031602720" }` — correct a mistyped number (normalized, uniqueness-checked, same Nigerian rule). **Forces re-login** (JWT holds the old number) — route to login after.
- `POST /api/vendors/{id}/deactivate` / `.../reactivate`

`VendorDto`: `id, phone, displayName, firstName, lastName, email, accountNumber, bankCode, paystackRecipientCode, isActive, emailVerified, onboardingCompleted, createdAt, updatedAt`.

---

## 4. Orders (vendor dashboard)

### Create order
`POST /api/orders` (vendor JWT; `vendorPhone` must match the token phone)
```json
{
  "vendorPhone": "08012345678",
  "customerName": "Chidi",
  "customerPhone": "08087654321",
  "deliveryAddress": "Lekki Phase 1",
  "buyerEmail": "buyer@example.com",
  "amountNgn": 45000,
  "fulfillment": 0,
  "deliveryFeeNgn": 5000,
  "driverPhone": "08055556666",
  "driverAccountNumber": "0123456789",
  "driverBankCode": "058",
  "items": [{ "description": "Sneakers", "quantity": 2, "unitPriceNgn": 22500 }]
}
```
- `fulfillment`: `0` = Dispatch (rider + OTP + 24h window), `1` = Digital (buyer Satisfied-button flow; fee must be `0`).
- Buyer charged `amountNgn + deliveryFeeNgn`. Driver/vendor bank fields fall back to stored details when omitted.
- `buyerEmail` must be real — receipts + status mails go there.
- Returns `paystackAuthUrl` (card/link payment) + `paystackReference`.

### Bank-transfer rail (dedicated virtual account)
- `POST /api/orders/{id}/request-bank-transfer` (vendor JWT, own order) `{ "preferredBank": "wema-bank" }` (optional; omit for default). Idempotent — repeat calls return the same account. Only from `AwaitingPayment`/`Draft`.
- Returns `payVirtualAccountNumber/Bank/Name`. Buyer transfers the **exact** total; confirmation is automatic via webhook.
- `GET /api/payments/banks` **(Public)** → full Nigerian bank list `[{ name, slug, code }]` for dropdowns and valid `preferredBank` slugs. Add `?transferOnly=true` for the short DVA-receivable subset.
- `GET /api/payments/banks/resolve?accountNumber=...&bankCode=...` **(Public)** → `{ accountNumber, bankCode, accountName }`. Wrong details → `400` with Paystack's reason included. Service down/rate-limited → **`503`** — show "couldn't verify, proceed carefully" instead of "wrong account". (Note: Paystack test mode allows ~3 live resolves/day; use code `001` or live keys for volume testing.)

### Vendor order views (JWT, own orders only)
- `GET /api/orders?page=&pageSize=` — my orders, newest first
- `GET /api/orders/{id}` — one order
- `GET /api/vendors/{id}/orders` — same, via vendor route

### Money actions (vendor JWT + ownership)
- `POST /api/orders/{id}/refund` — real Paystack refund from `Held`/`Delivered`; plain cancel from unpaid. `Released` → `409`.
- `POST /api/orders/{id}/resolve-dispute` `{ "resolution": "release" | "refund" }` — resolve a frozen dispute.

### Buyer/guest actions (all **Public**, no token)
These power the track page. The OTP/code is the credential; OTPs expire (24h) and lock after 5 tries.
- `GET /api/orders/by-reference/{ref}` — order detail (`ref` = Paystack reference or order id)
- `GET /api/orders/by-reference/{ref}/timeline` — ordered tracker events:
  `created → payment_pending → funds_held → delivered (+inspection deadline) → released/refunded/disputed`, each `{ key, label, at }`. Render this list as the tracker UI.
- `POST /api/orders/{id}/confirm-satisfaction` — digital orders: buyer confirms → instant release
- `POST /api/orders/{id}/dispute` `{ "reason": "..." }` — freezes funds (`Held`/`Delivered` only)
- `POST /api/orders/{id}/verify-otp` `{ "otp": "123456" }` — legacy/rider path: releases digital orders and driver-less dispatch orders. Orders **with** an assigned driver must use the driver portal below.

`OrderDto` (amounts in kobo): `id, vendorPhone, customerName, customerPhone, buyerEmail, deliveryAddress, items[{description,quantity,unitPriceKobo}], amountKobo, currency, status, paystackReference, paystackAuthUrl, heldAt, releasedAt, transferReference, refundReference, fulfillment, deliveryFeeKobo, driverPhone, driverTransferReference, deliveredAt, releaseDueAt, disputeReason, payVirtualAccountNumber/Bank`.

---

## 5. Driver portal (phone-OTP login, no accounts)

- `POST /api/dispatch/request-code` **(Public)** `{ "phone": "..." }` — works for **any** phone (row auto-created). Code via WhatsApp, 10-min expiry.
- `POST /api/dispatch/verify-code` **(Public)** → `{ token, dispatcher, expiresInHours }`.
- `GET /api/dispatch/assigned?page=&pageSize=` (driver JWT) — my `Held`/`Delivered` deliveries only.
- `POST /api/dispatch/orders/{id}/confirm` (driver JWT) `{ "otp": "<buyer code>" }` — marks `Delivered`, starts the 24h window, pays the rider fee instantly. Wrong code → 400; locked/expired → message says so.

---

## 6. End-to-end flows for the UI

**Vendor onboarding:** register → verify-email → payout → login → dashboard. Gate on the two flags.
**Sell:** create order → show buyer `paystackAuthUrl` (card) and/or `request-bank-transfer` details → buyer pays → `Held` (buyer gets OTP) → dispatch confirm → `Delivered` (24h window) → auto-release (worker) or dispute → resolve.
**Track page (public):** `by-reference` for header facts + `timeline` for the stepper; dispute + confirm-satisfaction buttons call the guest endpoints.
**Driver app:** request-code → verify-code → assigned list → confirm with buyer OTP.

## 7. WhatsApp bot (for context, not frontend work)
Same backend via chat: gated to verified + onboarded vendors (others get a signup/onboarding nudge with frontend links — set `Frontend__BaseUrl` so links render). Menu: create link (guided: customer → phone → **buyer email** → address → items → amount → fee → driver phone/account/**bank name** → holder confirm → order confirm), `4. Continue unfinished order` (resumable saved drafts with summaries, discard via `D2`), track (short status), help. Order creation errors always reply instead of silence. Chat is audited server-side; no frontend action needed.

---

## 8. Admin console (super-admin only)

**Setup (one time):** generate a hash locally and store it in App Settings — never commit it:
```powershell
dotnet run --project API -- hash-password "YourStrongPassword"
```
```
Admin__Email=admin@instasafe.ng
Admin__PasswordHash=<output above>
```
**Login:** same `POST /api/auth/vendor/login` with the admin email + password → response has `"role": "admin"` and `vendor: null`, token valid 8h. All endpoints below need `Authorization: Bearer <admin-token>` (`[Authorize(Roles="admin")]` — vendor/driver tokens get `403`).

- `GET /api/admin/stats` — vendors (total/active), orders per status, held GMV (kobo), released-today (kobo), open disputes/drafts, failed webhooks (24h), outbox backlog
- `GET /api/admin/vendors?q=&page=` — search phone/name/email; `GET /api/admin/vendors/{id}`
- `POST /api/admin/vendors/{id}/deactivate|reactivate`, `PUT /api/admin/vendors/{id}/phone`
- `GET /api/admin/dispatchers`, `POST /api/admin/dispatchers/{id}/deactivate|reactivate`
- `GET /api/admin/orders?status=&page=`, `GET /api/admin/orders/{id}`
- `GET /api/admin/disputes` — oldest first
- `POST /api/admin/orders/{id}/resolve-dispute` `{ "resolution": "release" | "refund" }`
- `POST /api/admin/orders/{id}/refund`
- `POST /api/admin/orders/{id}/force-release` `{ "note": "..." }` — pays the vendor remainder from `Held`/`Delivered`/`Disputed`; anything else → `409`
- `GET /api/admin/chats?phone=&from=&to=` — WhatsApp audit transcript
- `GET /api/admin/webhooks?provider=&event=&validOnly=` — deliveries incl. signature failures
- `GET /api/admin/outbox` — backlog + recent errors; `GET /api/admin/audit` — every moderation action above, with actor + note

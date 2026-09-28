# InstaSafe Backend — Frontend API Guide

Base URL (production): `https://instasafe-atfzfsb6c7csbvek.westus3-01.azurewebsites.net`
Local: `http://localhost:5080` · Interactive reference: `/swagger/index.html`

Frontend origin (production): `https://instasafe-six.vercel.app`

## What changed since the last handoff

Read this section first — it is the delta.

### ⚠️ BREAKING: three order shapes now exist

There used to be one `OrderDto` for every surface. There are now three, chosen per endpoint:

| DTO | Used by | Auth |
|---|---|---|
| `OrderDto` | Vendor dashboard + admin console | vendor/admin JWT |
| `PublicOrderDto` | Public track page + guest actions | **none** |
| `DispatchOrderDto` | Rider portal | driver JWT |

**If you read fields off the track page or the rider app, you must re-check them — the response is a different, smaller object.** Nothing was renamed; fields were *removed*. Details and the exact field lists are in [Order shapes](#order-shapes-whichever-dto-you-get).

Affected endpoints:
- `GET /api/orders/by-reference/{ref}` → `PublicOrderDto` **(breaking)**
- `POST /api/orders/{id}/verify-otp` → `PublicOrderDto` **(breaking)**
- `POST /api/orders/{id}/confirm-satisfaction` → `PublicOrderDto` **(breaking)**
- `POST /api/orders/{id}/dispute` → `PublicOrderDto` **(breaking)**
- `GET /api/dispatch/assigned` → `List<DispatchOrderDto>` **(breaking)**
- `POST /api/dispatch/orders/{id}/confirm` → `DispatchOrderDto` **(breaking)**

Why: the track endpoints are anonymous and the order number is the only credential, so returning the full DTO let anyone holding a leaked link read buyer contact details, the Paystack virtual account number, and the payment auth URL. The rider portal had the same problem by handing a third party the buyer's email and the escrow internals. The new shapes are deliberately minimal per audience.

### The rest of the delta

1. **`/track/{orderNumber}` is now a required frontend route.** The backend puts a live link in the buyer's payment-link WhatsApp, bank-transfer WhatsApp, status emails, **and the dispatcher's assignment WhatsApp**. See [The `/track/{orderNumber}` page](#the-trackordernumber-page).
2. **`GET /api/admin/orders`** gained `?q=` (search by order number or Paystack reference).
3. **`POST /api/admin/orders/{id}/retry-payout`** is new (admin only).
4. **`GET /api/payments/banks`** now returns the complete Nigerian bank list in one call (no paging, no truncation) — call it once and cache.
5. `OrderDto` now always includes `orderNumber` (`IS-XXXXXX`).

The only breaking changes are the six DTO swaps above. No route, HTTP method, envelope, status code, or field name changed.


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

### Order numbers vs payment references
Every order gets a stable customer-facing number on creation: `IS-XXXXXX` (6 unambiguous chars, e.g. `IS-8K4N2Q`). Use it **everywhere** buyer-facing — tracker, WhatsApp, emails, receipts screen.
- Lookup endpoints accept **order id, order number (any case), or Paystack reference** — old links keep working.
- `OrderDto.orderNumber` + timeline `reference` carry the number; `paystackReference` stays for the details section ("Payment ref" for receipt matching).
- Internally (refunds, verification, DVA matching) the Paystack reference rules — untouched.

### Phone numbers
Nigerian mobiles, canonical `234...` form everywhere. The API normalizes `080...`/`+234...`/`234...` automatically, but **validate client-side**: 11 digits starting `070/080/081/090/091` (or `234` + 10 digits). Anything else → `400 "must be a valid Nigerian mobile number"`. Store and display the `234...` form the API returns.

### OrderStatus
`Draft | AwaitingPayment | Held | Delivered | Released | Refunded | Disputed | Cancelled`
(New: `Delivered = 7`, funds frozen in 24h inspection window.)

---

## Order shapes (which DTO you get)

Amounts are **kobo** everywhere. Item prices are kobo too.

### `OrderDto` — vendor + admin (JWT)
The full record. Use this for the vendor dashboard and the admin console.
```
id, vendorPhone, customerName, customerPhone, buyerEmail, deliveryAddress,
items[{description, quantity, unitPriceKobo}], amountKobo, currency, status,
paystackReference, paystackAuthUrl, heldAt, releasedAt, transferReference,
refundReference, fulfillment, deliveryFeeKobo, driverPhone,
driverTransferReference, deliveredAt, releaseDueAt, disputeReason,
payVirtualAccountNumber, payVirtualAccountBank, orderNumber
```

### `PublicOrderDto` — public track page (no auth)
Same order, trimmed. **Removed vs `OrderDto`:** `vendorPhone`, `customerPhone`, `buyerEmail`, `paystackReference`, `paystackAuthUrl`, `transferReference`, `refundReference`, `payVirtualAccountNumber`, `payVirtualAccountBank`, `driverPhone`, `driverTransferReference`.
```
id, orderNumber, status, fulfillment, amountKobo, deliveryFeeKobo, currency,
customerName, deliveryAddress, items[{description, quantity, unitPriceKobo}],
heldAt, deliveredAt, releaseDueAt, releasedAt, disputeReason
```
`id` is still here on purpose — the guest buttons post to `/api/orders/{id}/…`.

### `DispatchOrderDto` — rider portal (driver JWT)
What a rider needs to make the drop, and nothing about the money behind it. **Removed vs `OrderDto`:** `amountKobo` (escrow — the rider never handles the order value), `vendorPhone`, `buyerEmail`, `paystackReference`, `paystackAuthUrl`, `transferReference`, `refundReference`, `payVirtualAccountNumber`, `payVirtualAccountBank`, `driverTransferReference`.
```
id, orderNumber, status, fulfillment, customerName, customerPhone,
deliveryAddress, deliveryFeeKobo, currency, items[{...}], driverPhone,
deliveredAt, releaseDueAt
```
`customerPhone` and `deliveryAddress` are **kept** — coordinating the drop is the rider's actual job. `deliveryFeeKobo` is their own payout.

### Migration cheat-sheet
| You were reading this on the track page | Now |
|---|---|
| `customerPhone` | gone — show `customerName` + `deliveryAddress` |
| `buyerEmail` | gone |
| `paystackAuthUrl` | gone — the buyer already paid, or the payment link came by WhatsApp/email |
| `paystackReference` | gone — display `orderNumber` instead |
| `transferReference` / `refundReference` | gone — the timeline conveys the outcome |
| `payVirtualAccountNumber/Bank` | gone — never expose the escrow account on a shareable page |

| You were reading this in the rider app | Now |
|---|---|
| `amountKobo` | gone — show `deliveryFeeKobo` only |
| `buyerEmail`, `payVirtualAccountNumber`, all `*Reference` | gone |
| `customerPhone`, `deliveryAddress`, `deliveryFeeKobo` | unchanged |

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
`loginId` accepts email or phone. Wrong details → `401`. Success → `{ token, vendor, expiresInHours, role }` (`role` is `"vendor"` here, `"admin"` for the super-admin — route dashboards by it).

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
- Buyer charged `amountNgn + deliveryFeeNgn`. Vendor bank falls back to stored payout details when omitted; **driver bank must be supplied per order** (drivers hold no stored details).
- `buyerEmail` must be real — receipts + status mails go there. `customerPhone` must be a WhatsApp number — the payment link goes there by chat + mail.
- Returns `orderNumber` (`IS-XXXXXX`, show it everywhere), `paystackAuthUrl` (card/link payment) + `paystackReference`.

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
These power the track page. The order number is the only credential; OTPs expire (24h) and lock after 5 tries. All five return **`PublicOrderDto`** (see [Order shapes](#order-shapes-whichever-dto-you-get)) — not `OrderDto`.
- `GET /api/orders/by-reference/{ref}` — order detail (`ref` = order number, Paystack reference, or order id)
- `GET /api/orders/by-reference/{ref}/timeline` — ordered tracker events:
  `created → payment_pending → funds_held → delivered (+inspection deadline) → released/refunded/disputed`, each `{ key, label, at }`. Render this list as the tracker UI. Returns `OrderTimelineDto` (unchanged).
- `POST /api/orders/{id}/confirm-satisfaction` — digital orders: buyer confirms → instant release
- `POST /api/orders/{id}/dispute` `{ "reason": "..." }` — freezes funds (`Held`/`Delivered` only)
- `POST /api/orders/{id}/verify-otp` `{ "otp": "123456" }` — legacy/rider path: releases digital orders and driver-less dispatch orders. Orders **with** an assigned driver must use the driver portal below.

Vendor and admin surfaces return the full `OrderDto`: `id, vendorPhone, customerName, customerPhone, buyerEmail, deliveryAddress, items[{description,quantity,unitPriceKobo}], amountKobo, currency, status, paystackReference, paystackAuthUrl, heldAt, releasedAt, transferReference, refundReference, fulfillment, deliveryFeeKobo, driverPhone, driverTransferReference, deliveredAt, releaseDueAt, disputeReason, payVirtualAccountNumber/Bank, orderNumber`.

### The `/track/{orderNumber}` page

The backend emails/WhatsApps a link shaped exactly like:

```
https://instasafe-six.vercel.app/track/IS-8K4N2Q
```

So you must have a route that resolves `/track/[orderNumber]`. **The link is already live in production messages** — buyer payment link, buyer bank-transfer instructions, buyer status emails, and the dispatcher's "delivery assigned" WhatsApp. It renders as a short line (`Track it live here: <url>`) in WhatsApp and a real anchor in email, so nothing else about the messages changes.

Wiring it needs no auth and no new endpoint — both calls are **Public**:
1. `GET /api/orders/by-reference/{orderNumber}` — header facts (status, amount, vendor, items).
2. `GET /api/orders/by-reference/{orderNumber}/timeline` — the stepper.
   `OrderTimelineDto`: `{ orderId, reference, status, amountKobo, events[{ key, label, at }] }`.
   Event keys in order: `created → payment_pending → funds_held → delivered (carries the inspection deadline) → released | refunded | disputed`.

Behaviour notes:
- `{orderNumber}` is case-insensitive (`is-8k4n2q` works). The same endpoint still accepts a Paystack reference or an order GUID, so older links keep resolving.
- Unknown number → `404` with `success: false`. Render "order not found", not an error page.
- The dispatcher's link is the **public** page, not a driver-only view. It shows the same buyer-facing tracker. If you later want a rider-specific view, keep it behind the driver JWT (`/api/dispatch/*`) — do not put anything extra on the public track page.
- **Treat the order number as a secret.** These endpoints are anonymous and the order number is the only credential (6 chars from a 30-char alphabet via CSPRNG, so it is not guessable, but it *is* a bearer token). The response is now a trimmed `PublicOrderDto` — it deliberately excludes `customerPhone`, `buyerEmail`, every Paystack/transfer/refund reference, the payment auth URL, and the escrow virtual account, so a leaked link no longer hands over buyer contact details or anything payable. Do not send the number to analytics, error trackers, or third-party scripts, and do not put it in a `Referer` to an external origin.
- **If you need richer data on the track page** (payment references for a receipt, the virtual account for a pending transfer), it is no longer available anonymously. Get it from the vendor dashboard, or ask us to add a specific field back to `PublicOrderDto` — do not just widen the DTO client-side.


---

## 5. Driver portal (phone-OTP login, no accounts)

- `POST /api/dispatch/request-code` **(Public)** `{ "phone": "..." }` — works for **any** phone (row auto-created). Code via WhatsApp, 10-min expiry.
- `POST /api/dispatch/verify-code` **(Public)** → `{ token, dispatcher, expiresInHours }`.
- `GET /api/dispatch/assigned?page=&pageSize=` (driver JWT) — my `Held`/`Delivered` deliveries only, as `List<DispatchOrderDto>`. `pageSize` is clamped to 1–100.
- `POST /api/dispatch/orders/{id}/confirm` (driver JWT) `{ "otp": "<buyer code>" }` — marks `Delivered`, starts the 24h window, pays the rider fee instantly. Returns a `DispatchOrderDto`. Wrong code → 400; locked/expired → message says so.

Both dispatch endpoints return **`DispatchOrderDto`**, not `OrderDto` — see [Order shapes](#order-shapes-whichever-dto-you-get). The rider app no longer receives `amountKobo`, `buyerEmail`, the Paystack virtual account, or any transfer reference.

**Dispatcher WhatsApp (backend-sent, no frontend work).** On assignment the rider gets the order number, delivery address, fee, a `/track/{orderNumber}` link, and a nudge to the driver portal. If your rider login screen needs to be reachable from that message, point the portal link at your rider route — the backend does not deep-link into it.

---

## 6. End-to-end flows for the UI

**Vendor onboarding:** register → verify-email → payout → login → dashboard. Gate on the two flags.
**Sell:** create order → show buyer `paystackAuthUrl` (card) and/or `request-bank-transfer` details → buyer pays → `Held` (buyer gets OTP) → dispatch confirm → `Delivered` (24h window) → auto-release (worker) or dispute → resolve.
**Track page (public):** route `/track/{orderNumber}` → `by-reference/{orderNumber}` for header facts + `by-reference/{orderNumber}/timeline` for the stepper; dispute + confirm-satisfaction buttons call the guest endpoints. Also linked from the dispatcher's assignment WhatsApp.
**Driver app:** request-code → verify-code → assigned list → confirm with buyer OTP.

## 7. WhatsApp bot (for context, not frontend work)
**Vendor-only.** The bot answers verified + onboarded vendors and no one else: unknown/unverified/deactivated/unfinished-onboarding senders get **silence** (logged to the audit trail, never replied to). Buyers are served purely through notifications (payment link, OTP, delivered, released, refunded) + the public track page — a buyer replying to the bot gets no answer by design. (If a vendor changes SIM, fix via `PUT /api/vendors/{id}/phone` on web.)
Menu: create link (guided: customer → phone → buyer email → address → items → amount → fee → driver phone/account/**bank name** → holder confirm → order confirm), `4. Continue unfinished order` (resumable saved drafts with summaries, discard via `D2`), track (short status), help. `BACK`/`EDIT` steps back to the previous answered question; `MENU`/`CANCEL` preserve the draft as a ticket. Order creation errors always reply instead of silence. Chat is audited server-side; no frontend action needed.

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
- `GET /api/admin/orders?status=&q=&page=&pageSize=`, `GET /api/admin/orders/{id}` — `q` searches order number **or** Paystack reference (use it to jump straight to an order a support agent is reading out).
- `GET /api/admin/disputes` — oldest first
- `POST /api/admin/orders/{id}/resolve-dispute` `{ "resolution": "release" | "refund" }`
- `POST /api/admin/orders/{id}/refund`
- `POST /api/admin/orders/{id}/force-release` `{ "note": "..." }` — pays the vendor remainder from `Held`/`Delivered`/`Disputed`; anything else → `409`
- `POST /api/admin/orders/{id}/retry-payout` (no body) — re-runs the vendor payout for an order already marked `Released` whose transfer never completed (missing `transferReference`). Returns `409` if the status is not `Released`, if a transfer reference already exists (never pay twice), or if the vendor has no payout recipient. On success the reference is written to the order **and** the escrow ledger. Two failure messages matter: "Paystack rejected the transfer" (nothing was sent — safe to retry once balance/recipient is fixed) vs "outcome is unknown" (check the Paystack transfers list for `InstaSafe payout {orderId}` before retrying, to avoid a double payment). Success responses do **not** re-send buyer/vendor notifications, so a retry will not spam anyone.
- `GET /api/admin/chats?phone=&from=&to=` — WhatsApp audit transcript
- `GET /api/admin/webhooks?provider=&event=&validOnly=` — deliveries incl. signature failures
- `GET /api/admin/outbox` — backlog + recent errors; `GET /api/admin/audit` — every moderation action above, with actor + note

# Billing MS2 foundation and public pricing

Status: foundation implemented; MS2 as a whole remains PARTIAL. MS1 is still open.

## Current implementation

- One project `Billing.API/Billing.API.csproj`, using Controllers, Services/Interfaces,
  Repositories/Interfaces (+ IUnitOfWork for multi-table transactions), Entities, DTOs, Data, Messaging (workers)
  and Errors. Same layering as Exam: services never use BillingDbContext directly.
- Identity-compatible RS256 public-key validation through the shared BuildingBlocks helper (same as Exam), with
  canonical GUID `sub`, role claims and fail-fast JWT configuration (`AddJwtAuthentication` in
  `DependencyInjection`), and ProblemDetails. No production auth probe.
- Billing-owned Plans/PlanLimits schema and offline initial migration source. No
  automatic database creation, migration or seeding. SQL Server is the runtime provider.
- Anonymous `GET /api/plans` reads active plans and limits from Billing's own DB.
  No rows returns an empty list; missing per-feature limits returns 503, never
  implicit unlimited access. A persisted null limit explicitly represents unlimited.
- `/Billing/Plans` uses the current Storefront design and BillingApiClient via YARP.
  It distinguishes unavailable pricing from an empty list, encodes content, and
  does not show a functioning checkout before VNPay is implemented.
- Gateway `/billing/...` strips its prefix to local port 5011. Compose uses
  `http://billing.api:8080/`; each API is its own container.

- UC-23.1 `GET /api/subscriptions/me` (Learner only — Admin/Moderator do not buy plans; own data only): Active / Expired / Free,
  expiry evaluated on read (`EndDate > now`), daysRemaining rounded up. Missing FREE plan row returns 503.
- UC-21.4 grant `POST /api/admin/subscriptions/grant` (Admin only): body `requestId, userId, planId,
  durationDays? (1–365), note? (≤500)`. Active subscriptions are extended from the current EndDate; expired
  ones are closed and a new period starts now. No Order is created. Every grant is audited in
  `SubscriptionGrants`; `requestId` is unique so a replay returns the original result and never extends twice.
  Concurrency: filtered unique index (one Active row per user) + optimistic `Version` token, one retry, then 409.
- UC-23.2 `GET /api/subscriptions/me/usage` (Learner only, own data only): effective plan as UC-23.1;
  per feature `limitValue` (null = unlimited), `period`, `resetAt` (00:00 UTC+7 next day/month, UTC; Lifetime null).
  `used`/`remaining` are null with `usageAvailable=false`: usage counting needs Exam/Learning integration
  (next version), so no fabricated zero. Missing limit row returns 503. AI part calls ai-service
  `GET /api/ai/usage/me` with the caller JWT (3 s timeout) at `AiService:BaseUrl`; empty URL or any failure
  gives `ai.available=false` with 200. No `UsageCounter` table yet (created with the consume API later).
- Migration `20261105054128_AddSubscriptions` generated offline only (not applied).
- Grant verifies the recipient through Identity's existing Admin API `GET /api/users/{id}` (Admin JWT forwarded,
  3 s timeout, `Identity:BaseUrl`): not found 404; not an active Learner (Admin/Moderator/non-Active) 422;
  Identity unavailable 503 (fail closed, nothing written). The Identity email is snapshotted on the subscription
  for reminder emails. Replays are answered from the stored grant without calling Identity.
- UC-21.3 `GET /api/orders/me` (status filter, paging 20/100) and `GET /api/orders/{orderCode}` (other user or
  missing = 404), Learner only. Pending past `ExpiresAt` is read as Expired in SQL; filters agree.
- UC-21.4 `GET /api/admin/orders` (Admin): status, userId, planCode, from/to (inclusive VN dates on CreatedAt),
  sortBy createdAt|amount, sortDir, paging. `Accept: text/csv` exports one VN calendar month (`month=yyyy-MM`,
  required) with the same filters, ignoring paging, max 10,000 rows (else 422), via the registered
  `CsvOutputFormatter` (RFC 4180 quoting, formula-injection prefix, UTF-8 BOM). Errors stay problem+json.
- `Orders` table with immutable plan/price snapshot and VNPay columns (nullable) — migration
  `20261105094611_AddOrders` (offline, not applied). No order is created until checkout (VNPay task).
- Storefront `/Billing/Subscription` (now with the usage table and AI block) and `/Admin/Billing/Orders` (grant form only; order list comes later).

Deferred: `SubscriptionActivatedEvent`/grant notification (events task), Admin order list (Orders task),
checking that `userId` exists in Identity (next version — cross-service), quota/order/revenue/renewal/VNPay.
No menu/layout changes have been made. Pricing is accessible by its direct page route.
UC-21.1 is PARTIAL until approved real data and live integration are verified.

## Explicit configuration

Database configuration follows the same pattern in Notification, Billing and AI:
`ConnectionStrings:Default` in appsettings.json (local SQL Server, Windows auth, `AcadPrep_BillingDb`, no
secret); `ConnectionStrings__Default` overrides it. The launch profile uses port 5011 and references only
Identity's existing public key (`Jwt__PublicKeyPath` or `Jwt__PublicKey`; use a public key only).

Set `BILLING_CONNECTION_STRING` in Compose's ignored `.env` for `AcadPrep_BillingDb`.
Compose mounts the public key read-only and waits for the SQL Server healthcheck.
Startup does not apply schema. Do not run all Compose services just to inspect Billing.

EF tooling uses `Data/BillingDbContextFactory` (same default; `ConnectionStrings__Default` overrides it).
Generating migration source does not apply it. Do not run `database update` or seed without permission.

## Business reference selected by Huy

Read-only reference: `D:/FPTU/Ki8-FA26/PRN222/AcadPrep-Individual`.

| Rule | Evidence in reference | v2 direction |
|---|---|---|
| Pro monthly | AppDbContextSeed: 79,000 VND / 30 days | Map `pro_monthly` to `PRO_MONTH`; no DB seed performed |
| Pro quarterly | AppDbContextSeed: 199,000 VND / 90 days | Map `pro_quarterly` to `PRO_QUARTER`; no DB seed performed |
| Free full tests | Reference had 1/month; **Huy approved 3 per Vietnam calendar month** | FullTest/Monthly; enforcement needs approved Exam contract |
| Free practice | Reference had 3/day; **Huy approved 5 per Vietnam calendar day** | Practice/Daily; enforcement needs approved Exam contract |
| Free saved vocab | Reference had 50; **Huy approved 100 currently saved words** | Vocab/Lifetime occupancy; delete/release must be coordinated |
| Pro quotas | BillingAccessService bypasses test/practice/vocab limits | Explicit per-feature null limits, never missing configuration |
| Renewal | SubscriptionActivationService extends current valid end date | Preserve remaining days with concurrency/idempotency protection |

Do not copy the old UI or Clean Architecture layout. Source IDs are integers;
v2 identities/entities use GUIDs. The reference reads users/exams/vocabulary from
one DB; v2 must use agreed API/event contracts and separate DB ownership.

The reference groups Admin/Moderator as staff internally; **Staff is not a new
role**. No automatic Pro/quota exemption has been enabled for those roles in v2.
The reference creates a manual Paid order for Admin grants; the v2 specification
requires an audited manual subscription without a payment order. Huy confirmed the
v2 rule (grant never creates an Order), so revenue remains correct.

Reminder cadence, dependency failure behavior, release/reservation contracts,
late IPN policy and provider configuration still need decisions. AI usage remains
explicitly unavailable while AI is absent, never fabricated as zero.

VNPay configuration field locations were identified in the reference; values were
not printed or copied into this repo. Secure transfer/validation happens in B7,
the final Billing backend feature. No secret is shipped in source/image/docs.
Return/IPN URLs must be adapted to the new Gateway topology, not copied blindly.

## Tests and next task

```powershell
dotnet test tests/Billing.API.Tests/Billing.API.Tests.csproj -m:1
dotnet test tests/Billing.UI.Tests/Billing.UI.Tests.csproj -m:1
docker build -f src/Services/Billing/Billing.API/Dockerfile -t acadprep-billing:ms2-foundation .
docker compose config --quiet
```

Tests create and destroy only their in-memory SQLite database. Numeric decimal
conversion is test-only for SQLite constraints; a separate SQL Server model test
verifies production precision, migrations and absence of seed data without connecting.
UI fixtures are test-only; production calls the Gateway.

Next: approved data provisioning, then quota,
orders, revenue/CSV, renewal/events, VNPay backend last, final UI/runtime verification.
Each task reports its own evidence. A passing image build is not a running deployment.

Executed on 2026-11-05: Billing API tests 20 passed; Billing UI tests 5 passed;
Notification UI regression 54 passed; Docker image build and Compose config validation
passed. Migration `20261105010917_InitialBillingPlans` was generated offline and its
snapshot matches the model. No migration was applied, no real database seeded,
no live payment made, and no production browser/container flow was verified.

Executed on 2026-11-05 (UC-23.1 + grant UC-21.4): Billing API tests 53 passed; Billing UI tests
27 passed; billing-validation.js node tests 8 passed; Notification UI regression 54 passed.
All use in-memory SQLite or test-only upstream fakes. SQL Server concurrency, Gateway, browser and
container runs are NOT TESTED. Migration `20261105054128_AddSubscriptions` not applied; no seed.

Executed on 2026-11-05 (UC-23.2): Billing API tests 73 passed; Billing UI tests 31 passed; JS tests 8 passed;
Notification UI regression 54 passed. AI integration verified only with a test-only fake handler; the real
ai-service does not exist yet. No schema change in this task.

Executed on 2026-11-05 (Orders 21.3/21.4 + Learner-only + grant recipient check): Billing API tests 128 passed;
Billing UI tests 44 passed; JS tests 15 passed; Notification UI regression 54 passed; `docker compose config`
PASS with a placeholder connection string (no container started). Identity is exercised only through a test fake;
SQL Server is verified only by offline translation tests.

Applied on 2026-11-05 with Huy's approval: `dotnet ef database update` created `AcadPrep_BillingDb` on the
local SQL Server (Windows auth) and applied InitialBillingPlans, AddSubscriptions, AddOrders and SeedPlans.
Seeded catalog (approved): FREE 0 đ (FullTest 3/month, Practice 5/day, Vocab 100 saved words),
PRO_MONTH 79,000 đ / 30 days, PRO_QUARTER 199,000 đ / 90 days, Pro unlimited. Re-running the seed SQL inside a
rolled-back transaction inserted nothing (3 plans / 9 limits). No subscriptions, grants or orders were created.

## UC-21.5 Revenue (2026-11-05)

`GET /api/admin/revenue?from&to&groupBy=day|month` (Admin only). Dates are inclusive Vietnam calendar days;
default is the last 30 days including today (VN). Limits: from ≤ to, ≤ 366 days (day) / ≤ 60 months (month),
else 400. Revenue = sum of Paid orders by `PaidAt`, bucketed by VN day/month, every period present (zeros).
`totals.paidCount/failedCount/pendingCount/expiredCount` count orders by `CreatedAt` in the range (Pending past
`ExpiresAt` counts as Expired); `points[].paidCount` counts orders paid in that period. Grants never count.
`Accept: text/csv` returns `period,revenue,paidCount` for the same range through `CsvOutputFormatter`.

Cache: Redis via `IDistributedCache` (`Redis:ConnectionString`; empty locally = no cache, database only; Compose
`redis:6379`). TTL 1 hour; keys `billing:revenue:v{version}:{groupBy}:{from}:{to}`. `IRevenueCache.InvalidateAsync`
replaces the version so all cached ranges are dropped at once — the VNPay task must call it after a successful IPN.
Any Redis failure is logged and the database answers (200). Pending/expired counts may lag up to the TTL.

Executed: Billing API tests 145 passed; Billing UI 52 passed; JS 15 passed; Notification UI 54+2 passed;
`docker compose config` PASS. Redis was exercised with an in-memory/failing test cache only (no Redis server).

## UC-23.3 reminders and expiry (2026-11-05) — renewal endpoint deferred to the VNPay task

`SubscriptionLifecycleWorker` runs hourly (first run at startup): from 08:00 Vietnam time it queues a
`SubscriptionExpiringEvent` for Active subscriptions whose EndDate falls on the VN day three days ahead; any
Active subscription with `EndDate <= now` is set to Expired and a `SubscriptionExpiredEvent` is queued.
`SubscriptionReminderLogs` is unique on (SubscriptionId, Kind, EndDate): re-runs/second instances never repeat,
and a renewed period (new EndDate) gets its own reminders. Each item and its `OutboxMessages` row are written in
one transaction; `OutboxPublisherWorker` publishes every 5 s through BuildingBlocks.EventBus (RabbitMQ) and keeps
failed messages pending with an attempt count (e.g. broker down or Notification queue not bound yet).
Events carry the email snapshot (`Subscriptions.UserEmail`); unknown email = in-app notification only.
Shared contract: new file `BuildingBlocks.Common/Events/SubscriptionEvents.cs` (no existing file changed).
`BackgroundJobs:Enabled=false` disables both workers (tests). Migration `20261105105155_AddSubscriptionLifecycle`
generated offline — NOT applied yet (needs approval). `POST /api/subscriptions/renew` is part of the VNPay task.

Executed: Billing API tests 153 passed (lifecycle/outbox with a test-only bus; no RabbitMQ server).

## UC-21.2 VNPay checkout, return and IPN + UC-23.3 renewal (2026-11-05)

- `POST /api/orders/checkout {planId}` and `POST /api/subscriptions/renew {planId}` (Learner only). Amount,
  plan and duration come from Billing data (snapshot on the order); FREE -> 422 `PlanNotPurchasable`; renew
  without any past subscription -> 422 `NoSubscriptionToRenew`; VNPay not configured -> 503.
- Anti-spam (Huy): at most one unexpired Pending order per user and plan (filtered unique index). A repeated
  click returns the same order and its stored signed link (200, `reused: true`); an expired Pending is closed and
  replaced. This replaces spec v0.2's "3 pending -> 409".
- VNPay PAY v2.1.0 (sandbox docs checked 2026-11-05): HMAC-SHA512 over ordinal-sorted, URL-encoded `vnp_*`
  parameters; amount x100; create/expire dates GMT+7; `vnp_Locale=vn`, `vnp_OrderType=other`, no bank code;
  `vnp_IpAddr` from the forwarded browser IP. OrderCode = 14 CSPRNG digits.
- **Deviation from spec v0.2 (decided by Huy, option B):** the signed Return URL confirms the payment through the
  same idempotent path as the IPN, so the sandbox works on localhost without a public IPN URL. Both verify the
  checksum and merchant, the order, the exact amount, then update with the order `Version` token. Whichever
  arrives first confirms; the other gets `02`. Success requires ResponseCode and TransactionStatus `00`.
  Late success for an order already Expired is honoured; Paid/Failed orders answer `02`.
- On success, in one transaction: order Paid, subscription activated/extended (`SubscriptionActivator`, shared
  with Admin grants), `SubscriptionActivatedEvent` queued in the outbox (Notification: in-app + email); then the
  revenue cache is invalidated. `PaymentLogs` records Create/Return/Ipn with the raw VNPay query (no secret).
- IPN `GET /api/payments/vnpay-ipn` always answers 200 `{"RspCode","Message"}`; Return redirects only to
  `Frontend:PaymentResultUrl`. `OrderExpiryWorker` marks Pending past ExpiresAt as Expired every 5 minutes.
- Config: `Vnpay:TmnCode`/`Vnpay:HashSecret` from user-secrets/`.env` only; `Vnpay:PaymentUrl`, `Vnpay:ReturnUrl`
  (Gateway URL) and `Frontend:PaymentResultUrl` in appsettings. For production, register the IPN URL
  `https://<domain>/billing/api/payments/vnpay-ipn` in the VNPay merchant portal.
- Storefront: `/Billing/Checkout` (custom `ValidPlanForCheckout` FE adapter + server check), `/Billing/PaymentResult`
  (status from the API only), "Mua gói" on `/Billing/Plans`, "Gia hạn" on `/Billing/Subscription`.
- Migration `20261105115016_AddPayments` generated offline (PaymentLogs, Orders.PaymentUrl, pending index).

Executed: Billing API 182 passed; Billing UI 69 passed; JS 39 passed; Notification API 269 passed / 11 SQL
skipped; Notification UI 56 passed; solution build and `docker compose config` PASS. Callbacks are signed in
tests with a test-only secret; no real VNPay sandbox payment was made.

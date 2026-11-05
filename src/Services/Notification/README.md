# Notification runtime configuration

## MS1 v2 structure

Notification now builds from one project: `Notifications.API/Notifications.API.csproj`.
Controllers depend on service interfaces; Services contain business rules,
Repositories contain writes/state handling, and Data contains EF configuration,
converters and the existing migrations. Entities, Enums, DTOs and Mappings live
in the same project. The three former layer projects are removed from the solution.

The initial migration ID remains `20261018161833_InitialNotifications`; no schema
change, database update or seed is part of this refactor. Do not generate a new
initial migration or delete existing history to make the new layout work.

AutoMapper maps the validated Admin request to the entity and the entity to the
created response. Server identity, timestamps, type and metadata are ignored by
the request mapping and set by the service. AutoMapper 15.1.3 uses its own license
terms; a key can be provided with `AUTOMAPPER_LICENSE_KEY`. No key is included or
license logging suppressed. List/count retain their database-side EF projection.

## Existing Storefront integration and Ajax

Use the existing `/Notifications` and `/Admin/Notifications/Create` Razor Pages.
They call `ToeicApiClient.Notifications.cs`, which forwards the session Bearer
token through Gateway. No mock backend is registered in production.

Read/delete remain antiforgery-protected POST handlers. Normal forms still use
redirect-after-post. `notification-actions.js` enhances those forms with fetch:
the handler returns JSON for `X-Requested-With: XMLHttpRequest`, then the script
fetches the existing page again and replaces only the notification panel and
badge. Counts and paging come from the API, not guessed client-side deltas.
Ajax guards return 401/403/503 instead of redirecting to login HTML. A 401 sends
the browser to the existing login with a local ReturnUrl. No JWT is added to this JS.

If a write succeeded but the refresh failed, the UI preserves that distinction
and asks the user to refresh; it does not automatically replay the mutation.
Deleting/reading the last item of the last page clamps the displayed page.

## Visual Studio F5

Select the Project profiles for Identity.API (5001), Notifications.API (5010),
ApiGateway (5080) and Storefront.Web (5100). Add an existing Identity project to
your local VS session if it is not listed; do not rewrite Identity to run Notification.
Set environment configuration for each project's approved test database first.
Identity must have `DatabaseInitialization__Enabled=false` to prevent automatic
migration/seed; confirm its current startup flags before running. Notification
never initializes the database. Set Storefront `GatewayUrl=http://localhost:5080`.
Use existing prepared accounts; no account provisioning is included in MS1.

Start the four applications only after checking ports and obtaining runtime/DB
approval. Open Storefront on port 5100, sign in, use the bell to open Notifications,
and open the Admin create link from the list with an Admin account. Inspect browser
Network to confirm read/delete use background POST/GET, while writes reach API
through Gateway. Verify Learner denial and two-user isolation with prepared data.

## Verification and remaining gates

Run from repository root (SQL Server tests stay disabled unless explicitly approved):

```powershell
$env:ACADPREP_NOTIFICATION_SQL_TESTS = '0'
dotnet test tests/Notifications.API.Tests/Notifications.API.Tests.csproj -m:1
dotnet test tests/Notifications.UI.Tests/Notifications.UI.Tests.csproj -m:1
node --test tests/Notifications.UI.Tests/notification-validation.test.cjs tests/Notifications.UI.Tests/notification-actions.test.cjs
```

If DLLs are locked by a running application, use a separate BaseOutputPath under
the repository; do not stop someone else's process. UI tests replace upstream
HTTP only inside test factories. They do not prove a live Identity/Gateway/SQL flow.
Events/SMTP remain pending contract and failure-policy decisions; no new consumer,
Billing dependency or fake production email sender is introduced here.

## Local

Run Notifications.API using its launch profile. It resolves the existing Identity
public key at `../../Identity/Identity.API/keys/public_key.pem`, relative to the
Notification API content root. No key is copied into Notification.

For `--no-launch-profile`, published output or a different working directory,
set `Jwt__PublicKeyPath` to the absolute path of the matching Identity public key.
`ConnectionStrings:Default` in appsettings.json targets `AcadPrep_NotificationsDb` (Windows auth, no secret);
`ConnectionStrings__Default` overrides it (same pattern as Billing and AI). JWT uses the shared BuildingBlocks
helper (same as Exam) plus `AddJwtAuthentication` in `DependencyInjection`.
Notification startup never applies migrations or seeds data.

## Docker Compose

Set `NOTIFICATIONS_CONNECTION_STRING` in Compose's ignored `.env` for
`AcadPrep_NotificationsDb`. For local development, point it at the Compose
`sqlserver` service.

`notifications.api` uses container port 8080 and host port 5010. It mounts only
the existing Identity public key, read-only, at `/app/keys/public_key.pem`.
The source must already be a valid public-key file. No private key is copied,
mounted or included in the Notification Docker build context.

Validate without starting services: `docker compose config --quiet`.
Notification waits for the local SQL Server health check before starting.

Gateway's local `appsettings.json` retains `http://localhost:5010/`.
Compose overrides only the Notification destination with
`ReverseProxy__Clusters__notification__Destinations__d1__Address=http://notifications.api:8080/`.
Both services use the existing default Compose network and Docker DNS. The public
route `/notification/{**catch-all}` strips `/notification` before forwarding;
for example `/notification/api/notifications` becomes `/api/notifications`.
There is no Authorization-header removal or replacement transform.

Host port 5010 is published for direct local diagnostics. Do not start the container
while the Windows Notification process occupies 5010. Gateway port 5080 and
Storefront port 5100 also conflict with the manual Windows stack. Coordinate a
separate runtime step before starting containers; never stop that stack implicitly.

Build the image from the repository root when the Docker daemon is available:

```powershell
docker build -f src/Services/Notification/Notifications.API/Dockerfile -t acadprep-notifications:ms1-v2 .
```

Dockerfile-specific ignore rules restrict build context to Notification and
BuildingBlocks.Common and exclude key files. The .NET 9 multi-stage image publishes
Notifications.API, listens on 8080 and runs as the base image's non-root APP_UID.

Before runtime, supply a database connection reachable from inside the container.
Windows Integrated Security from the local host is not automatically available
in a Linux container. Confirm test-only SQL credentials/network access separately;
no migration, database provisioning or container startup is performed by N10a.
Compose already routes Identity to `identity.api:5001`; validate the matching
public key and runtime configuration with its owner. Building the Notification
image does not start any service or verify login against a live database.

## Recipient verification (2026-11-05)

`POST /api/admin/notifications` with `audience=User` now checks `targetUserId` through Identity's existing
Admin API `GET /api/users/{id}` (Identity unchanged), forwarding the calling Admin's JWT with a 3 s timeout
(`Identity:BaseUrl`, local `http://localhost:5001/`, Compose `http://identity.api:5001/`).
- Recipient may have any role and any account status; only a missing user is rejected: 422 with a
  `targetUserId` field error. Broadcast audiences (All/Learner/Moderator) never call Identity.
- Identity unavailable (timeout, network, non-404 error): 503, nothing stored (fail closed; Admin retries).
- `/Admin/Notifications/Create` shows "Người dùng không tồn tại." on the target field, or a retry message on 503.

Executed: Notification API tests 260 passed / 11 SQL skipped; Notification UI 56 passed; JS 19 passed.
Identity is exercised only through a test-only fake; the real Identity call is NOT TESTED.

## Billing reminders: in-app + email (UC-23.3, 2026-11-05)

`ReminderSubscriberWorker` subscribes `SubscriptionExpiringEvent`/`SubscriptionExpiredEvent` (BuildingBlocks.EventBus,
RabbitMQ) and re-subscribes every 30 s, so it recovers when the broker starts later. Each event creates one
`Reminder` notification (`Audience=User`, `SourceEventId = event Id`, unique: redelivery never duplicates) shown in
`/Notifications` and the badge. Email uses `SmtpEmailSender` (System.Net.Mail, plain text, same text as the
notification); `EmailDeliveries` (unique SourceEventId) records Pending/Sent/Failed/Skipped: at most 3 attempts,
a mail failure never removes the notification, and a sent mail is never re-sent. No address or SMTP not
configured = Skipped (in-app only).

SMTP comes only from user-secrets/environment: `Smtp__Host`, `Smtp__Port` (587), `Smtp__User`, `Smtp__Password`,
`Smtp__From`, `Smtp__EnableSsl` (true). Compose reads `SMTP_HOST/PORT/USER/PASSWORD/FROM`. Never commit credentials.
Migration `20261105105336_AddEmailDeliveries` generated offline — NOT applied yet (needs approval).

Executed: Notification API tests 267 passed / 11 SQL skipped (test-only fake email sender; no SMTP or RabbitMQ).

## Database (2026-11-05)

Notification v2 uses `AcadPrep_NotificationsDb` (local SQL Server, Windows auth; same name as Compose), created
with Huy's approval by `dotnet ef database update`: `InitialNotifications` + `AddEmailDeliveries` applied.
The older `AcadPrep_NotificationDb` has a different, non-migrated schema (e.g. `NotificationTargetUsers`) and
existing rows; it was left untouched and must not be targeted by v2. The design-time factory and
`appsettings.json` point to the new database; `ConnectionStrings__Default` overrides both.

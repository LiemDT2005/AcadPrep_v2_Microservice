# Notification runtime configuration

## Local

Run Notifications.API using its launch profile. It resolves the existing Identity
public key at `../../Identity/Identity.API/keys/public_key.pem`, relative to the
Notification API content root. No key is copied into Notification.

For `--no-launch-profile`, published output or a different working directory,
set `Jwt__PublicKeyPath` to the absolute path of the matching Identity public key.
Supply `ConnectionStrings__Default` explicitly for the approved database.
Notification startup never applies migrations or seeds data.

## Docker Compose

Set `NOTIFICATIONS_CONNECTION_STRING` in the invoking process environment using
secure configuration. The Compose service deliberately refuses a missing value.
Do not put credentials in tracked files or command-line arguments.

`notifications.api` uses container port 8080 and host port 5010. It mounts only
the existing Identity public key, read-only, at `/app/keys/public_key.pem`.
The source must already be a valid public-key file. No private key is copied,
mounted or included in the Notification Docker build context.

Validate without starting services: `docker compose config --quiet`.
Do not run Compose until the database target and environment are approved.

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
docker build -f src/Services/Notification/Notifications.API/Dockerfile -t acadprep-notifications:n10a .
```

Dockerfile-specific ignore rules restrict build context to Notification and
BuildingBlocks.Common and exclude key files. The .NET 9 multi-stage image publishes
Notifications.API, listens on 8080 and runs as the base image's non-root APP_UID.

Before runtime, supply a database connection reachable from inside the container.
Windows Integrated Security from the local host is not automatically available
in a Linux container. Confirm test-only SQL credentials/network access separately;
no migration, database provisioning or container startup is performed by N10a.
Containerized Identity login is a separate topology dependency: the existing
Identity Gateway destination remains unchanged and must be coordinated with its owner.

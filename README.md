# Coaching Class Manager

Android-first tuition management app built with Expo/React Native, ASP.NET Core, and PostgreSQL. The current implementation is the Phase 1 foundation: sign-in, institute setup, protected account access, and cached account reopening.

Students, batches, attendance, fee collection, sync of business records, and receipts belong to subsequent phases. See [the implementation plan](docs/implementation-plan.md).

See [Phase 1 validation and remaining work](docs/phase-1-status.md), including the installed-device check and outstanding upstream dependency advisories.

## Prerequisites

- .NET 10 SDK (the repository SDK selection is in `global.json`).
- Node.js 24 and npm.
- Docker Desktop with Linux containers.
- An Android development build for device testing; Android SDK/ADB for a local native build, or an Expo account for EAS builds.

## Start the database and API

From the repository root in PowerShell:

```powershell
./scripts/setup-dev.ps1
docker compose --env-file infra/.env -f infra/compose.dev.yml up -d --wait
dotnet restore api/CoachingClassManager.slnx --locked-mode
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile --project api/CoachingClassManager.Api -- --migrate
dotnet run --project api/CoachingClassManager.Api
```

Run setup once. It generates independent random secrets and a database password in ignored local files. Re-running refuses to overwrite database credentials. Never use these files for staging or commit them.

The API listens on port 5080. `/health/live` checks the process; `/health/ready` checks database connectivity. Development binding permits a phone on the local network to reach the API. Keep it on a trusted development network.

## Start the mobile app

```powershell
cd app
npm ci
$env:APP_VARIANT = 'development'
npx expo start --dev-client
```

Set `EXPO_PUBLIC_API_URL` in `app/.env`:

- Android emulator: `http://10.0.2.2:5080`.
- Physical Android phone: `http://<your-PC-LAN-address>:5080`; use the same network and permit the development port through your firewall if necessary.

Restart Metro after changing environment variables. The Android development build must use `APP_VARIANT=development` to allow local HTTP traffic; preview/production builds require HTTPS.

Build locally with configured Android tooling:

```powershell
$env:APP_VARIANT = 'development'
npx expo run:android
```

Or link the app to your Expo account with `npx eas-cli@latest init`, configure build environment variables, then use `npx eas-cli@latest build --platform android --profile development`. Cloud build/signing has not been performed by this implementation.

## Sign in locally

Enter an Indian mobile number. The six-digit random development code is displayed on the verification screen in a debug build. No SMS is sent.

Both the server's Development environment and `Auth:EnableDevelopmentOtp=true` are required. Enabling this option in Staging or Production fails startup. With it disabled, OTP request/verification returns 503 until a real SMS provider is integrated. The app never displays development codes in release builds.

After verification, save your institute name and language preference. Reopen the app to restore the account and verify its session online. With no reachable server, cached account details can be viewed for up to seven days after verification, bounded by refresh-token expiry. Updating the institute and revoking a session require connectivity.

## Validation

```powershell
./scripts/test-api.ps1
cd app
npm run typecheck
npm run lint
npm test
npm run export:android
npx expo-doctor
```

API integration tests create and remove a randomly named `coaching_test_*` PostgreSQL database. They do not reset the application database. The database user used for tests needs database creation permission. CI provisions a separate PostgreSQL service and runs backend and mobile checks.

Tests cover unauthorized/cross-account access, malformed phones, OTP throttling/attempt limits/expiry/single use, concurrent verification, token rotation/replay, session expiry/logout, and the development-provider environment boundary. Mobile policy tests cover offline grace and expiry. Android export validates bundling; it does not replace an installed-device smoke test.

## API routes

| Method | Route | Behavior |
|---|---|---|
| POST | `/api/auth/otp/request` | `{ phone: "+91…" }`; requests challenge |
| POST | `/api/auth/otp/verify` | `{ challengeId, code }`; issues access/refresh tokens and profile |
| POST | `/api/auth/refresh` | `{ refreshToken }`; rotates device session |
| POST | `/api/auth/logout` | Revokes the authenticated device-session family |
| GET | `/api/me` | Current teacher/institute profile |
| PUT | `/api/institute` | `{ name, language }`; changes only the authenticated institute |
| GET | `/api/institutes/{id}` | Returns 404 for another account's institute |

Access tokens last 15 minutes; refresh families last 30 days from sign-in. Rotation retains the original family expiry. A reused refresh token revokes its replacement family. Retrying a refresh after a lost response may require signing in again; the client does not automatically retry token rotation. Every protected request checks the persisted session, so logout invalidates access immediately.

OTP challenges expire after five minutes, allow five attempts, enforce a 60-second resend wait, and have per-phone/global daily budgets. An IP limiter restricts authentication requests. PostgreSQL advisory transaction locking serializes budget/challenge/rotation decisions across API instances. This intentionally favors correctness over authentication throughput for the initial deployment.

## Staging preparation

`infra/compose.staging.yml`, `infra/staging.env.example`, and `infra/nginx.staging.conf.example` provide configuration templates. No remote deployment has been performed.

1. Set independent random staging secrets and a real hostname. Configure `TRUSTED_PROXY_IP` to the reverse proxy's address as seen by the API container; forwarded headers are accepted only from known proxies.
2. Terminate TLS with Nginx and an actual certificate. The API port is bound only to localhost and PostgreSQL is not exposed by staging Compose.
3. Run migrations as an explicit deployment step, before starting the new API version. Do not automatically run schema migrations on every replica startup.
4. Keep the development OTP option disabled. Implement/test the selected SMS provider before opening staging sign-in to teachers.
5. Configure a public Sentry DSN if desired. Auth tokens, phone details, request bodies, user metadata, and breadcrumbs must not reach crash reports. Source-map upload and native crash-report verification still need the project's actual monitoring account.

Backups, restore rehearsals, export/deletion, and production operations are Phase 7 work. Validate teacher workflows before proceeding to financial features. A phone-number change is not yet an account recovery process; see [Phase 0 assumptions](docs/phase-0-assumptions.md).

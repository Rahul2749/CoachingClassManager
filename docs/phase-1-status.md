# Phase 1 status — 13 October 2026

The software foundation is implemented. The Phase 1 exit gate remains open until an Android development build is installed and the workflow is exercised on a device.

## Implemented

- Expo SDK 57 / React Native 0.86 TypeScript app with Expo Router.
- Mobile number sign-in, code verification/resend, institute setup/editing, and account home.
- SecureStore token vault and SQLite account-profile cache/migration.
- Session reopening, refresh rotation, server-confirmed logout, and bounded offline cached-account access.
- ASP.NET Core 10 API, PostgreSQL identity schema and EF migration.
- OTP expiry, attempt limits, resend throttling, daily per-phone/global budgets, and IP throttling.
- Hashed OTP/refresh secrets, single-use challenges, refresh replay detection, and persisted session checks.
- Tenant context and account-scoped access, with tests attempting another account's institute ID.
- Docker development database, independent random local secrets, staging/TLS proxy templates, CI configuration, and setup documentation.
- Localization preference and initial translated home text; full translation is later work.
- Optional Sentry initialization with request/user/breadcrumb filtering. No project DSN or source-map upload has been configured.

## Verified locally

| Check | Result |
|---|---|
| PostgreSQL initial migration | Applied successfully |
| API Release build | Passed; zero warnings/errors |
| PostgreSQL integration tests | 13 passed |
| TypeScript checking | Passed |
| Expo lint | Passed; zero warnings/errors |
| Offline account policy tests | 5 passed |
| Expo Doctor | 21/21 checks passed |
| Android production Hermes bundle export | Passed; 1,846 modules, about 5.4 MB bytecode |
| Live API and database readiness endpoints | `ok` / `ready` |
| NuGet vulnerability check | No vulnerable API/test packages reported by current sources |
| Secrets/build-output exclusion | Local secrets, cache, and export output ignored by Git |

API verification used the actual project's local PostgreSQL service. Tests created a separate temporary database and removed it after running. Tests have not yet run on a hosted CI service because the repository has not been pushed.

Android export verifies bundling, not an APK installation or native runtime behavior. Android SDK/ADB was not found here. The local API was started on port 5080 and the PostgreSQL container was left running for development.

## Dependencies to resolve before release

`npm audit` reports 30 affected packages (19 high, 11 moderate), including transitive effects through Expo/Metro. A non-forced audit fix was applied; the remaining recommendations include breaking Expo/React Native downgrades or an incompatible router major upgrade and were not applied.

Direct advisory roots reported by the audit:

- [braces pattern nesting denial of service](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm).
- [node-forge signature verification](https://github.com/advisories/GHSA-86w9-cpqp-85rv).
- [decode-uri-component malformed-input denial of service](https://github.com/advisories/GHSA-vcc3-ghjq-m6fr).
- [uuid buffer bounds](https://github.com/advisories/GHSA-w5hq-g745-h8pq).

Reassess supported upstream fixes and the affected call paths before public release. Expo Doctor passing does not establish that the dependency audit is clean.

## Remaining external checks

- Build/sign/install an Android development client using Android tooling or the user's Expo project.
- Exercise sign-in, institute setup, restart, reconnect, and logout on a low-end phone; verify SecureStore and SQLite behavior there.
- Select and integrate an SMS provider, then verify delivery/error handling and spending controls with its actual service.
- Deploy staging using a real domain, TLS certificate, secrets, and trusted proxy configuration.
- Configure the monitoring project and verify native crash reporting and redaction.
- Conduct teacher interviews and confirm Phase 0 assumptions before fee-ledger development.

Production/staging never falls back to development OTP. With no real provider configured, OTP request and verification intentionally return 503.

The next software phase is batches, students, enrolments, and import with atomic local change/outbox capture. Business-record synchronization is Phase 3; it is not implemented by the account-profile cache.

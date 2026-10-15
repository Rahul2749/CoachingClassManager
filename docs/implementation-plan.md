# Coaching Class Manager — Phased Implementation Plan

Planning date: 13 October 2026

Implementation status: Phase 1 software foundation is implemented and locally verified. Installed-device validation and real SMS/staging integrations remain pending; see [Phase 1 status](phase-1-status.md). Phase 0 interview assumptions are recorded separately and have not been validated with teachers yet.

## Outcome and assumptions

Build an Android app for solo tutors to manage students, attendance, monthly fees, and parent communication. Start with a closed pilot, then prepare a paid public release.

- One developer; Android first; one teacher owns each institute account.
- Design and test for up to 150 active students per account. Commercial plan limits remain a separate decision.
- Multiple devices may reconnect to an account, even without multi-teacher support.
- English plus the pilot group's preferred language first; English, Hindi, and Marathi before public release.
- Initial login requires connectivity. Previously authenticated teachers can continue supported local work offline, subject to a documented local-access policy.
- Effort ranges are planning estimates, not delivery commitments. Re-estimate after the sync prototype and pilot.

## Stack decisions

| Area | Planned choice |
|---|---|
| Mobile | React Native, Expo development builds, TypeScript |
| Navigation | Expo Router |
| Local data | expo-sqlite, SQL migrations, durable operation queue |
| Network | TanStack Query and Axios for remote requests and online resources |
| UI state | Zustand for transient UI preferences; SQLite owns durable business data |
| Forms | React Hook Form and Zod |
| Languages | i18next |
| Authentication | Phone OTP, short-lived access tokens, rotating refresh tokens in SecureStore |
| Backend | ASP.NET Core modular monolith, Entity Framework Core |
| Database | PostgreSQL |
| Background jobs | Hangfire with PostgreSQL-compatible storage verified during setup |
| Receipts | QuestPDF on server; expo-sharing on device |
| Monitoring | Sentry with sensitive-data filtering |
| Deployment | Docker, Nginx, TLS, one initial server, separate off-server backups |
| Distribution | EAS Build; EAS Update only for compatible updates |
| Initial communication | WhatsApp prefilled messages and manual sharing |
| Later integrations | Play subscription billing; SMS provider; optional website Razorpay checkout |

Use supported, mutually compatible package versions selected and pinned in Phase 1. Verify licensing before adopting server PDF and job-storage dependencies.

SQLite persists app data across restarts, but synchronization must be implemented separately. Its selection here keeps local tables explicit; it does not remove the sync work. See [Expo SQLite documentation](https://docs.expo.dev/versions/latest/sdk/sqlite/). WatermelonDB remains an alternative only if the Phase 3 prototype justifies a change; it also requires a backend sync implementation: [WatermelonDB sync introduction](https://watermelondb.dev/docs/Sync/Intro).

## Phase overview

| Phase | Deliverable | Estimated focused effort | Depends on |
|---|---|---:|---|
| 0 | Validated workflow and business rules | 8–12 hours, plus interview scheduling | — |
| 1 | App, API, database, authentication, CI | 20–30 hours | 0 |
| 2 | Batches, students, enrolments, import | 20–30 hours | 1 |
| 3 | Reliable offline synchronization | 35–55 hours | 2 |
| 4 | Attendance workflow | 15–25 hours | 3 |
| 5 | Fee ledger, dues, partial payments | 40–60 hours | 3, 4 |
| 6 | Receipts, WhatsApp sharing, localization | 20–30 hours | 5 |
| 7 | Recovery, privacy controls, closed pilot | 25–40 hours, plus pilot observation | 6 |
| 8 | Subscription billing and public release | 25–40 hours, plus external review | 7 |
| 9 | Automated communication and expansion | Estimate after pilot | 8 |

Phases 0–8 total approximately 210–325 focused hours. With a 20% contingency, budget roughly 250–390 hours: about 17–26 weeks at 15 hours per week, plus external waiting time. Pilot observation should span a complete fee-collection cycle and can overlap release preparation. These estimates assume familiarity with the chosen stack.

## Phase 0 — Validate the workflow and freeze pilot rules

**Build/prepare**

- Interview 10–15 teachers and recruit five pilot users. Observe an actual attendance session and fee register with permission.
- Sketch onboarding, home, batch, attendance, student profile, fee collection, and receipt flows.
- Confirm billing frequency, discounts, multi-batch charging, joining/leaving rules, and payment corrections.
- Select the pilot's regional language and representative low-end Android devices.
- Define what data can be collected during the pilot and the applicable notice/consent process before using real student data.
- Record the price as a hypothesis; measure paid continuation after the pilot.

**Proposed pilot defaults to validate**

- Monthly charges per enrolment, with an effective-dated agreed fee copied into each generated due.
- No automatic proration in the pilot; the teacher confirms the first month's amount.
- Allocate payments to the oldest outstanding dues by default, showing the allocation before confirmation.
- Keep excess payments as unallocated credit; apply them explicitly to later dues.
- Exiting students retain historical records and stop receiving future charges from the agreed end date.
- Cash and externally received UPI payments are recorded manually.

**Exit gate:** five teachers agree to test, and the billing/offline rules are written down with concrete examples. If interviews change the rules, update the plan before building the ledger.

## Phase 1 — Foundation and authentication

**Build**

- Create `app/`, `api/`, `docs/`, and `infra/`; initialize source control and development instructions.
- Add Expo development builds, API health checks, PostgreSQL, migrations, and CI build/type-check checks.
- Create OTP request/verification with expiry, attempt limits, resend throttling, and provider spending controls. Use a development-only fake provider locally.
- Add institute setup, secure token handling, session renewal/revocation, and an offline-session policy.
- Establish tenant ownership and authorization centrally. Include tenant identity in relevant relationships and uniqueness constraints.
- Set up staging, structured logging, crash reporting, and secret handling without logging OTPs, tokens, or student details.
- Document phone-number change and account recovery rules.

**Exit gate:** install on a physical Android device, log in, create an institute, reopen the app, and demonstrate that another account cannot access its records. Development OTP bypass is unavailable in production configuration.

## Phase 2 — Batches, students, and enrolments

**Build**

- Batch names, subjects, schedules, and default monthly fees.
- Student and parent contact details; enrolment start/end dates and fee overrides.
- Local SQLite repositories and migrations. All durable UI reads use these repositories.
- Archive flows that preserve attendance and payment history.
- Pasted-list or CSV import with preview, validation, and duplicate warnings. Direct XLSX import can follow pilot feedback.
- Capture local changes and their outgoing operations atomically from the beginning; Phase 3 completes transport and reconciliation.

**Exit gate:** load a representative 150-student dataset, assign students to multiple batches, correct import errors, and restart without losing records. A teacher can complete assisted setup without entering every student manually.

## Phase 3 — Offline synchronization foundation

**Build**

- Durable outbox entries with operation IDs, record IDs, expected versions, payloads, and retry state.
- Atomic local record/outbox writes and atomic server operation/deduplication writes.
- Dependency-aware push ordering, retry with backoff, and explicit handling for validation failures.
- Server-issued pull cursors with consistent pagination; avoid client-clock timestamps as the source of truth.
- Server deletion tracking and defined behavior for clients older than the retained change history.
- Version checks and visible conflict handling for mutable records. Financial operations get stricter rules in Phase 5.
- Sync on foreground/reconnection and manual refresh; correctness must not depend on background execution.
- Visible last-sync time, queued changes, and actionable sync errors.
- Account switching and migration rules that protect pending changes and prevent cross-account local data exposure.

**Exit gate:** demonstrate airplane-mode edits, app termination, reconnect, lost server acknowledgments, duplicate requests, two-device conflicts, and database migration with pending changes. Nothing disappears silently or creates duplicate business effects.

## Phase 4 — Attendance

**Build**

- Today's batches and a fast roster with present, absent, late, and unmarked states.
- Explicit “Mark all present,” individual corrections, and a clear save action.
- Attendance uniqueness by tenant, batch, student, and local class date for the pilot's one-session-per-day rule.
- Saved versus unsynced indicators, history, and attendance percentages based only on held, marked sessions.
- Define cancelled classes and enrolment date boundaries so missing sessions do not automatically count as absences.

**Exit gate:** a pilot teacher marks a typical 30-student batch within 30 seconds on a target phone. Attendance survives offline restart and reconnect without duplicate rows or an unmarked session becoming present automatically.

## Phase 5 — Fee ledger and payment recording

**Build**

- Fee dues with billing period, due date, enrolment, amount snapshot, and auditable adjustments.
- Idempotent monthly generation plus catch-up after job downtime. Enforce one regular charge per enrolment and billing period.
- Payments, payment allocations, unallocated credit, and explicit reversal/correction records.
- Store money as integer paise, use transactions for allocations, and derive outstanding balances from ledger entries.
- Pending dashboard, overdue age, student account history, and cash/UPI recording.
- Offline payment capture against downloaded dues. Pending local entries affect the device's provisional balance and are clearly identified.
- If dues are not downloaded for a new month, show that the balance is incomplete; permit unallocated payment capture and reconcile after sync.
- On conflicting allocations from multiple devices, preserve the payment and flag allocation for resolution; do not silently drop money.

**Minimum entities**

`Tenant`, `Teacher`, `Batch`, `Student`, `Enrollment`, `AttendanceSession`, `Attendance`, `FeeDue`, `FeeAdjustment`, `Payment`, `PaymentAllocation`, `PaymentReversal`, and sync/audit records. Every business entity has explicit tenant ownership.

**Exit gate:** verify partial payment, one payment across months/batches, overpayment, reversal, fee change, joining/leaving, repeated monthly jobs, and reconnect retries. For example, dues of ₹500 + ₹500 and a ₹700 payment must leave ₹300 outstanding on every device after reconciliation.

## Phase 6 — Receipts, parent sharing, and languages

**Build**

- Generate immutable receipt snapshots and unique server-assigned receipt numbers after a payment is accepted.
- Generate PDFs with QuestPDF; download/cache them and share through expo-sharing.
- Show offline payments as “recorded on this device; receipt pending sync.” Previously downloaded PDFs remain available offline.
- Add localized WhatsApp reminder text and an explicit teacher action to open/share it.
- Record “share initiated” separately from “delivered”; manual WhatsApp sharing does not establish delivery.
- Translate all pilot screens and errors; prepare English, Hindi, and Marathi for release.
- Verify long names, large text settings, Devanagari fonts in PDFs, and common phone-number formats.

**Exit gate:** record an offline partial payment, reconnect, generate one receipt, and share it with correct student details and balances. Repeated requests reuse the same receipt identity.

## Phase 7 — Recovery, privacy controls, and closed pilot

**Build and operate**

- Cloud sync for all pilot/free users; essential data recovery is not a paid-only feature.
- Automated encrypted off-server backups with documented retention and a successful restore rehearsal.
- New-phone recovery for synced records; clearly explain that unsynced data cannot be restored from the server.
- User export and deletion flows, including the defined treatment of retained backups.
- Review the applicable privacy requirements, notices, data handling, and provider agreements before admitting real pilot records.
- Run the five-teacher pilot for a full fee-collection cycle. Observe attendance, collections, support requests, and failed sync attempts.
- Measure setup completion, time to mark attendance, repeated usage, ledger discrepancies, and willingness to continue with actual payment.

**Exit gate:** no unresolved cross-account access or data-loss defects; a restore succeeds; teachers can reconcile app balances with their register; repeated use is observed through a collection cycle. Decide explicitly whether to proceed, revise the workflow, or extend the pilot.

## Phase 8 — Paid plans and public Android launch

**Build**

- Select the compliant purchase flow before implementing checkout. Default planning assumption: Play Billing for subscriptions purchased in the Play-distributed app.
- Treat website Razorpay checkout or India's alternative billing program as separate options requiring a documented distribution decision. See [Google's India billing guidance](https://support.google.com/googleplay/android-developer/answer/13306652?hl=en).
- Implement server-verified entitlements, renewals, cancellation, expiry, refunds, and idempotent billing notifications.
- Define offline entitlement grace and downgrade behavior. Preserve read/export access to existing records after expiry; show limits before blocking new entries.
- Finalize price and student limits using pilot evidence and a cost model covering support, OTP, hosting, and billing costs.
- Complete Hindi/Marathi QA, privacy policy, store declarations, account deletion requirements, listing assets, and the account's current required testing/review process.
- Run a release-candidate recovery/migration check, staged rollout, monitoring, and rollback rehearsal.

**Exit gate:** a test purchase, renewal, cancellation, refund, and expiry produce correct access; the release meets current distribution requirements; restore and rollback instructions are usable.

## Phase 9 — Expand after demonstrated usage

Prioritize based on pilot demand and support burden:

1. Automated SMS with provider onboarding, approved templates where applicable, consent/preferences, retry deduplication, delivery callbacks, and credit accounting based on provider charging rules.
2. Official WhatsApp Business integration after validating operating cost and demand.
3. Additional fee cycles, family payments, scholarships, and direct XLSX import.
4. Multi-teacher roles and permissions; replace single-owner assumptions deliberately.
5. Online student-fee collection, with provider webhook reconciliation distinct from app subscription billing.
6. Reports, exams, homework, parent access, and iOS only when justified by usage.

Push notifications are optional in this phase unless the pilot identifies a specific need. Server-scheduled SMS must not depend on a teacher's phone running in the background.

## Offline behavior contract

| Action | Offline behavior |
|---|---|
| First login / OTP | Requires internet |
| View cached students and batches | Available |
| Add/edit student or enrolment | Saved locally and queued; conflicts resolved on sync |
| Mark attendance | Saved locally and queued |
| View fee balances | Includes downloaded dues and pending local payments; freshness shown |
| Generate authoritative monthly dues | Server operation; missing periods explicitly shown |
| Record payment | Saved locally as pending; server validates/reconciles |
| Generate a new numbered PDF receipt | Requires successful sync and server generation |
| Open an already cached PDF | Available |
| Prepare WhatsApp text | Available from local data; actual transmission depends on WhatsApp connectivity |
| Buy subscription or SMS credits | Requires internet |
| Restore onto a new phone | Requires internet; restores server-synced records |

## First implementation work package

After Phase 0 decisions are recorded, implement Phase 1 as one reviewable foundation:

- [x] Repository structure and setup README.
- [x] Expo TypeScript app and ASP.NET Core solution.
- [x] Local PostgreSQL and initial tenant/user migrations.
- [x] Development OTP provider behind a production-safe configuration boundary.
- [x] Authentication screens and institute setup.
- [x] Tenant authorization tests and CI build checks configured; local equivalents pass.
- [x] Staging configuration templates.
- [ ] Deploy staging with actual domain, TLS, monitoring, and SMS configuration.
- [ ] Physical-device smoke test.

Complete each phase's exit gate before treating it as finished. Revise estimates when evidence changes; keep the pilot's attendance, fee accuracy, and recovery outcomes as the priority.

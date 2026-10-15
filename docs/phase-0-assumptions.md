# Phase 0 decisions carried into development

The user authorized starting implementation on 13 October 2026. Teacher interviews and pilot recruitment have not been performed by this agent.

Foundation assumptions: Android first; Indian mobile sign-in; one teacher per institute; English/Hindi/Marathi preference; online first login; seven-day cached-account access after successful server verification, bounded by refresh expiry. Cached access is a device UX policy, not proof that a server-side session remains valid.

Billing defaults remain the proposed defaults in the implementation plan and must be validated before Phase 5. No real student records are used in foundation testing.

Phone-number changes and account recovery are deliberately not self-service in this foundation. Signing in with a different number creates a separate account. A later recovery process must verify ownership independently; do not merge accounts or transfer an institute based only on possession of a new number.

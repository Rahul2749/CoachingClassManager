import type { StoredSession } from './vault';

export const OFFLINE_GRACE_MS = 7 * 24 * 60 * 60 * 1000;

export function canUseCachedAccount(session: StoredSession, now = Date.now()): boolean {
  return session.lastVerifiedAt <= now
    && now - session.lastVerifiedAt <= OFFLINE_GRACE_MS
    && Date.parse(session.refreshExpiresAt) > now;
}

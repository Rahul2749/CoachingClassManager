import assert from 'node:assert/strict';
import { test } from 'node:test';
import { canUseCachedAccount, OFFLINE_GRACE_MS } from '../src/auth/policy.ts';

const now = Date.UTC(2026, 9, 13);
const session = {
  teacherId: 'teacher', accessToken: 'access', refreshToken: 'refresh',
  accessExpiresAt: new Date(now - 1000).toISOString(),
  refreshExpiresAt: new Date(now + 30 * 86400000).toISOString(),
  lastVerifiedAt: now - 86400000,
};

test('expired access token can use a recently verified cached account', () => {
  assert.equal(canUseCachedAccount(session, now), true);
});
test('cached access ends after seven days even if refresh remains valid', () => {
  assert.equal(canUseCachedAccount({ ...session, lastVerifiedAt: now - OFFLINE_GRACE_MS - 1 }, now), false);
});
test('expired refresh prevents offline access', () => {
  assert.equal(canUseCachedAccount({ ...session, refreshExpiresAt: new Date(now).toISOString() }, now), false);
});
test('moving the device clock before its last verification blocks cached access', () => {
  assert.equal(canUseCachedAccount({ ...session, lastVerifiedAt: now + 1 }, now), false);
});
test('invalid expiry cannot grant cached access', () => {
  assert.equal(canUseCachedAccount({ ...session, refreshExpiresAt: 'invalid' }, now), false);
});

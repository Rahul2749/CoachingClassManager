import { isAxiosError, type AxiosRequestConfig } from 'axios';
import { create } from 'zustand';
import { api } from '../api/client';
import type { Challenge, Profile, TokenResponse } from '../api/types';
import { clearProfile, readProfile, saveProfile } from '../db/profile';
import { canUseCachedAccount } from './policy';
import { deleteSession, readSession, saveSession, type StoredSession } from './vault';

type AuthState = {
  ready: boolean;
  session: StoredSession | null;
  profile: Profile | null;
  challenge: Challenge | null;
  offline: boolean;
  startupError: string | null;
};
export const useAuth = create<AuthState>(() => ({
  ready: false, session: null, profile: null, challenge: null, offline: false, startupError: null,
}));

let refreshInFlight: Promise<void> | undefined;

async function acceptTokens(response: TokenResponse) {
  const { profile, ...tokens } = response;
  const session: StoredSession = { ...tokens, teacherId: profile.teacherId, lastVerifiedAt: Date.now() };
  await saveSession(session);
  await saveProfile(profile);
  useAuth.setState({ session, profile, offline: false, challenge: null });
}

async function clearAccount() {
  const teacherId = useAuth.getState().session?.teacherId;
  await deleteSession();
  if (teacherId) await clearProfile(teacherId);
  useAuth.setState({ session: null, profile: null, challenge: null, offline: false });
}

export async function refreshSession() {
  if (refreshInFlight) return refreshInFlight;
  refreshInFlight = (async () => {
    const current = useAuth.getState().session;
    if (!current) throw new Error('Sign-in required.');
    try {
      // Never automatically retry this request: refresh tokens rotate on acceptance.
      const response = await api.post<TokenResponse>('/api/auth/refresh', { refreshToken: current.refreshToken });
      if (response.data.profile.teacherId !== current.teacherId) throw new Error('Account mismatch.');
      await acceptTokens(response.data);
    } catch (error) {
      if (isAxiosError(error) && error.response?.status === 401) await clearAccount();
      throw error;
    }
  })();
  try { await refreshInFlight; }
  finally { refreshInFlight = undefined; }
}

export async function authorized<T>(configuration: AxiosRequestConfig): Promise<T> {
  let current = useAuth.getState().session;
  if (!current) throw new Error('Sign-in required.');
  if (Date.parse(current.accessExpiresAt) <= Date.now() + 30_000) {
    await refreshSession();
    current = useAuth.getState().session!;
  }
  try {
    const response = await api.request<T>({ ...configuration, headers: { ...configuration.headers, Authorization: `Bearer ${current.accessToken}` } });
    useAuth.setState({ offline: false });
    return response.data;
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 401) {
      await refreshSession();
      const replacement = useAuth.getState().session;
      const response = await api.request<T>({ ...configuration, headers: { ...configuration.headers, Authorization: `Bearer ${replacement!.accessToken}` } });
      return response.data;
    }
    throw error;
  }
}

export async function bootstrap() {
  useAuth.setState({ ready: false, startupError: null });
  try {
    const session = await readSession();
    if (!session) return;
    const profile = await readProfile(session.teacherId);
    useAuth.setState({ session, profile });
    try {
      const fresh = await authorized<Profile>({ url: '/api/me' });
      const latest = useAuth.getState().session;
      if (!latest || fresh.teacherId !== latest.teacherId) throw new Error('Account mismatch.');
      const verified = { ...latest, lastVerifiedAt: Date.now() };
      await saveSession(verified);
      await saveProfile(fresh);
      useAuth.setState({ session: verified, profile: fresh, offline: false });
    } catch (error) {
      const current = useAuth.getState().session;
      if (isAxiosError(error) && (!error.response || error.response.status >= 500)
        && current && profile && canUseCachedAccount(current)) {
        useAuth.setState({ offline: true });
      } else if (isAxiosError(error) && error.response?.status === 401) {
        await clearAccount();
      } else {
        // Preserve the vault on network/storage trouble, but block access and allow retry.
        useAuth.setState({ profile: null, startupError: 'Reconnect to verify your account and continue.' });
      }
    }
  } catch {
    useAuth.setState({ startupError: 'Could not open your saved account. Please try again.' });
  } finally { useAuth.setState({ ready: true }); }
}

export async function requestCode(phone: string) {
  const { data } = await api.post<Omit<Challenge, 'phone'>>('/api/auth/otp/request', { phone });
  useAuth.setState({ challenge: { ...data, phone } });
}

export async function verifyCode(code: string) {
  const challenge = useAuth.getState().challenge;
  if (!challenge) throw new Error('Request a sign-in code first.');
  const { data } = await api.post<TokenResponse>('/api/auth/otp/verify', { challengeId: challenge.challengeId, code });
  await acceptTokens(data);
}

export async function updateInstitute(name: string, language: Profile['language']) {
  const profile = await authorized<Profile>({ method: 'PUT', url: '/api/institute', data: { name, language } });
  await saveProfile(profile);
  useAuth.setState({ profile });
}

export async function logout() {
  // Require server confirmation so a device session is actually revoked.
  await authorized<void>({ method: 'POST', url: '/api/auth/logout' });
  await clearAccount();
}

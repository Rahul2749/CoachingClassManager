import * as SecureStore from 'expo-secure-store';
import type { Tokens } from '../api/types';

const KEY = 'coaching.session.v1';
export type StoredSession = Tokens & { teacherId: string; lastVerifiedAt: number };
const storageOptions = { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY };

export async function readSession(): Promise<StoredSession | null> {
  const data = await SecureStore.getItemAsync(KEY, storageOptions);
  return data ? JSON.parse(data) as StoredSession : null;
}
export async function saveSession(session: StoredSession) {
  await SecureStore.setItemAsync(KEY, JSON.stringify(session), storageOptions);
}
export async function deleteSession() {
  await SecureStore.deleteItemAsync(KEY, storageOptions);
}

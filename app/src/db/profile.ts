import * as SQLite from 'expo-sqlite';
import type { Profile } from '../api/types';

let database: Promise<SQLite.SQLiteDatabase> | undefined;

async function open() {
  database ??= (async () => {
    const db = await SQLite.openDatabaseAsync('coaching.db');
    await db.execAsync('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;');
    const version = await db.getFirstAsync<{ user_version: number }>('PRAGMA user_version;');
    if ((version?.user_version ?? 0) < 1) {
      await db.withExclusiveTransactionAsync(async (tx) => {
        await tx.execAsync(`
          CREATE TABLE account_profile (
            teacher_id TEXT PRIMARY KEY NOT NULL,
            profile_json TEXT NOT NULL
          );
          PRAGMA user_version = 1;
        `);
      });
    }
    return db;
  })();
  try { return await database; }
  catch (error) { database = undefined; throw error; }
}

export async function saveProfile(profile: Profile) {
  const db = await open();
  await db.runAsync(
    'INSERT INTO account_profile (teacher_id, profile_json) VALUES (?, ?) ON CONFLICT(teacher_id) DO UPDATE SET profile_json = excluded.profile_json',
    profile.teacherId, JSON.stringify(profile),
  );
}

export async function readProfile(teacherId: string): Promise<Profile | null> {
  const db = await open();
  const row = await db.getFirstAsync<{ profile_json: string }>(
    'SELECT profile_json FROM account_profile WHERE teacher_id = ?', teacherId,
  );
  return row ? JSON.parse(row.profile_json) as Profile : null;
}

export async function clearProfile(teacherId: string) {
  const db = await open();
  await db.runAsync('DELETE FROM account_profile WHERE teacher_id = ?', teacherId);
}

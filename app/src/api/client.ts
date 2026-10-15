import { create, isAxiosError } from 'axios';

const baseURL = process.env.EXPO_PUBLIC_API_URL ?? 'http://10.0.2.2:5080';
if (!__DEV__ && !baseURL.startsWith('https://')) {
  throw new Error('Release builds require an HTTPS API URL.');
}

export const api = create({ baseURL, timeout: 12_000 });

export function errorMessage(error: unknown): string {
  if (isAxiosError(error)) {
    if (!error.response) return 'Could not connect. Check your internet connection and try again.';
    if (error.response.status === 429) return 'Too many attempts. Please wait and try again.';
    const title: unknown = error.response.data?.title;
    if (typeof title === 'string' && error.response.status < 500) return title;
    return 'The service is temporarily unavailable. Please try again.';
  }
  return 'Something went wrong. Please try again.';
}

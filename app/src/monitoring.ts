import * as Sentry from '@sentry/react-native';

let initialized = false;
export function initializeMonitoring() {
  if (initialized) return;
  initialized = true;
  const dsn = process.env.EXPO_PUBLIC_SENTRY_DSN;
  Sentry.init({
    dsn, enabled: !!dsn && !__DEV__, sendDefaultPii: false,
    tracesSampleRate: 0,
    beforeBreadcrumb: () => null,
    beforeSend(event) {
      delete event.user;
      delete event.request;
      delete event.extra;
      const redact = (value: string) => value.replace(/\+?91[6-9]\d{9}/g, '[phone]')
        .replace(/Bearer\s+\S+/gi, '[authorization]');
      if (event.message) event.message = redact(event.message);
      event.exception?.values?.forEach((exception) => {
        if (exception.value) exception.value = redact(exception.value);
      });
      return event;
    },
  });
}

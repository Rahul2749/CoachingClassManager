import type { ConfigContext, ExpoConfig } from 'expo/config';

export default ({ config }: ConfigContext): ExpoConfig => {
  const development = process.env.APP_VARIANT === 'development';
  return {
    ...config,
    name: development ? 'Coaching Manager Dev' : 'Coaching Class Manager',
    slug: 'coaching-class-manager',
    scheme: 'coachingmanager',
    android: {
      ...config.android,
      package: development ? 'com.coachingclassmanager.app.dev' : 'com.coachingclassmanager.app',
      allowBackup: false,
    },
    plugins: [
      'expo-router', 'expo-secure-store', 'expo-sqlite',
      ['expo-build-properties', { android: { usesCleartextTraffic: development } }],
    ],
  };
};

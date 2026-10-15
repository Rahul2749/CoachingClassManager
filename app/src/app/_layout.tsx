import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { ActivityIndicator, AppState } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { bootstrap, useAuth } from '../auth/store';
import { Button, ErrorText, Heading, Screen } from '../components/ui';
import '../i18n';
import { initializeMonitoring } from '../monitoring';

initializeMonitoring();
const queryClient = new QueryClient({ defaultOptions: {
  mutations: { retry: false }, queries: { retry: 1, staleTime: 30_000 },
} });

export default function RootLayout() {
  const ready = useAuth((s) => s.ready);
  const error = useAuth((s) => s.startupError);
  useEffect(() => {
    void bootstrap();
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active' && useAuth.getState().session) void bootstrap();
    });
    return () => subscription.remove();
  }, []);
  return <SafeAreaProvider><QueryClientProvider client={queryClient}>
    <StatusBar style="dark" />
    {!ready ? <Screen><ActivityIndicator accessibilityLabel="Opening your account" size="large" /></Screen>
      : error ? <Screen><Heading title="Let’s reconnect." /><ErrorText message={error} />
        <Button title="Try again" onPress={() => { void bootstrap(); }} /></Screen>
      : <Stack screenOptions={{ headerShown: false }} />}
  </QueryClientProvider></SafeAreaProvider>;
}

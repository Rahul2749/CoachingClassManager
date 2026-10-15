import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Redirect, router } from 'expo-router';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Text, View } from 'react-native';
import { errorMessage } from '../api/client';
import { bootstrap, logout, useAuth } from '../auth/store';
import { Button, ErrorText, Heading, Screen, styles } from '../components/ui';

export default function Home() {
  const profile = useAuth((s) => s.profile);
  const offline = useAuth((s) => s.offline);
  const { t, i18n } = useTranslation();
  const client = useQueryClient();
  const signOut = useMutation({ mutationFn: logout, onSuccess: () => { client.clear(); router.replace('/sign-in'); } });
  useEffect(() => { if (profile) void i18n.changeLanguage(profile.language); }, [profile, i18n]);
  if (!profile) return <Redirect href="/sign-in" />;
  if (!profile.instituteName) return <Redirect href="/setup" />;
  return <Screen>
    <Heading title={profile.instituteName} detail={t('welcome')} />
    {offline ? <View style={styles.card}><Text style={styles.label}>You’re viewing saved account details.</Text>
      <Text style={styles.detail}>Reconnect to update your institute or sign out. Cached access lasts up to seven days after account verification.</Text>
      <Button title="Reconnect" secondary onPress={() => { void bootstrap(); }} /></View> : null}
    <View style={styles.card}><Text style={styles.label}>{t('account')}</Text><Text style={styles.detail}>{profile.phone}</Text>
      <Text style={styles.detail}>Your institute setup is saved.</Text></View>
    <View style={styles.card}><Text style={styles.label}>{t('next')}</Text>
      <Text style={styles.detail}>Attendance, fees, and parent reminders will follow in the next phases.</Text></View>
    <Button title="Edit institute" secondary disabled={offline} onPress={() => router.push('/setup')} />
    <ErrorText message={signOut.error ? errorMessage(signOut.error) : null} />
    <Button title={t('signOut')} secondary busy={signOut.isPending} disabled={offline} onPress={() => signOut.mutate()} />
  </Screen>;
}

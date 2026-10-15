import { useMutation } from '@tanstack/react-query';
import { Redirect, router } from 'expo-router';
import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Text, View } from 'react-native';
import { z } from 'zod';
import { errorMessage } from '../api/client';
import { requestCode, useAuth, verifyCode } from '../auth/store';
import { Button, ErrorText, Field, Heading, Screen, styles } from '../components/ui';

const codeSchema = z.string().regex(/^\d{6}$/, 'Enter the six-digit code.');

export default function Verify() {
  const challenge = useAuth((s) => s.challenge);
  const profile = useAuth((s) => s.profile);
  const [seconds, setSeconds] = useState(challenge?.resendAfterSeconds ?? 60);
  const { control, handleSubmit, reset } = useForm({ defaultValues: { code: '' } });
  const verify = useMutation({ mutationFn: ({ code }: { code: string }) => verifyCode(code), onSuccess: () => router.replace('/') });
  const resend = useMutation({ mutationFn: () => requestCode(challenge!.phone), onSuccess: () => {
    setSeconds(useAuth.getState().challenge?.resendAfterSeconds ?? 60); reset(); verify.reset();
  } });
  useEffect(() => {
    const timer = setInterval(() => setSeconds((value) => Math.max(0, value - 1)), 1000);
    return () => clearInterval(timer);
  }, []);
  if (profile) return <Redirect href="/" />;
  if (!challenge) return <Redirect href="/sign-in" />;
  return <Screen>
    <Heading title="Check your phone." detail={`Enter the sign-in code for ${challenge.phone}. It expires after five minutes.`} />
    {__DEV__ && challenge.developmentCode ? <View style={styles.card}>
      <Text style={styles.label}>Development code: {challenge.developmentCode}</Text>
      <Text style={styles.detail}>Local testing only. No SMS was sent.</Text>
    </View> : null}
    <Controller control={control} name="code" rules={{ validate: (value) => {
      const result = codeSchema.safeParse(value);
      return result.success || result.error.issues[0].message;
    } }} render={({ field, fieldState }) => <Field label="Six-digit code" placeholder="000000"
      keyboardType="number-pad" autoComplete="sms-otp" maxLength={6} value={field.value}
      onChangeText={(value) => { field.onChange(value.replace(/\D/g, '')); verify.reset(); }}
      onBlur={field.onBlur} error={fieldState.error?.message} />} />
    <ErrorText message={verify.error ? errorMessage(verify.error) : resend.error ? errorMessage(resend.error) : null} />
    <Button title="Verify and continue" busy={verify.isPending} disabled={resend.isPending}
      onPress={handleSubmit((values) => verify.mutate(values))} />
    <Button title={seconds > 0 ? `Resend in ${seconds}s` : 'Resend code'} secondary
      disabled={seconds > 0 || verify.isPending} busy={resend.isPending} onPress={() => resend.mutate()} />
    <Button title="Use a different number" secondary disabled={verify.isPending || resend.isPending} onPress={() => {
      useAuth.setState({ challenge: null }); router.replace('/sign-in');
    }} />
  </Screen>;
}

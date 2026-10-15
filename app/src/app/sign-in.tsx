import { useMutation } from '@tanstack/react-query';
import { Redirect, router } from 'expo-router';
import { Controller, useForm } from 'react-hook-form';
import { Text } from 'react-native';
import { z } from 'zod';
import { errorMessage } from '../api/client';
import { requestCode, useAuth } from '../auth/store';
import { Button, ErrorText, Field, Heading, Screen, styles } from '../components/ui';

const phoneSchema = z.string().regex(/^[6-9]\d{9}$/, 'Enter a valid 10-digit mobile number.');

export default function SignIn() {
  const profile = useAuth((s) => s.profile);
  const { control, handleSubmit } = useForm({ defaultValues: { phone: '' } });
  const mutation = useMutation({
    mutationFn: ({ phone }: { phone: string }) => requestCode(`+91${phone}`),
    onSuccess: () => router.replace('/verify'),
  });
  if (profile) return <Redirect href="/" />;
  return <Screen>
    <Heading title="Your classroom, together." detail="A simpler way to manage your tuition class. Start with your mobile number." />
    <Controller control={control} name="phone" rules={{ validate: (value) => {
      const result = phoneSchema.safeParse(value);
      return result.success || result.error.issues[0].message;
    } }} render={({ field, fieldState }) => <Field label="Mobile number · +91" placeholder="10-digit mobile number"
      keyboardType="phone-pad" autoComplete="tel-national" maxLength={10} value={field.value}
      onChangeText={(value) => { field.onChange(value.replace(/\D/g, '')); mutation.reset(); }}
      onBlur={field.onBlur} error={fieldState.error?.message} />} />
    <ErrorText message={mutation.error ? errorMessage(mutation.error) : null} />
    <Button title="Get sign-in code" busy={mutation.isPending} onPress={handleSubmit((values) => mutation.mutate(values))} />
    <Text style={styles.detail}>Internet is needed to sign in. Your mobile number identifies your teacher account.</Text>
  </Screen>;
}

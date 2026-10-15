import { useMutation } from '@tanstack/react-query';
import { Redirect, router } from 'expo-router';
import { Controller, useForm } from 'react-hook-form';
import { Pressable, Text, View } from 'react-native';
import { z } from 'zod';
import { errorMessage } from '../api/client';
import type { Profile } from '../api/types';
import { updateInstitute, useAuth } from '../auth/store';
import { Button, colors, ErrorText, Field, Heading, Screen, styles } from '../components/ui';

const nameSchema = z.string().trim().min(2, 'Use at least two characters.').max(120, 'Use at most 120 characters.');
const languages: { code: Profile['language']; name: string }[] = [
  { code: 'en', name: 'English' }, { code: 'hi', name: 'हिंदी' }, { code: 'mr', name: 'मराठी' },
];

export default function Setup() {
  const profile = useAuth((s) => s.profile);
  const { control, handleSubmit } = useForm({ defaultValues: {
    name: profile?.instituteName ?? '', language: profile?.language ?? 'en',
  } });
  const mutation = useMutation({
    mutationFn: ({ name, language }: { name: string; language: Profile['language'] }) => updateInstitute(name, language),
    onSuccess: () => router.replace('/home'),
  });
  if (!profile) return <Redirect href="/sign-in" />;
  return <Screen>
    <Heading title="Make it your class." detail="Add your institute name and choose your preferred language." />
    <Controller control={control} name="name" rules={{ validate: (value) => {
      const result = nameSchema.safeParse(value);
      return result.success || result.error.issues[0].message;
    } }} render={({ field, fieldState }) => <Field label="Institute name" placeholder="e.g. Bright Coaching Classes"
      autoCapitalize="words" maxLength={120} value={field.value} onChangeText={field.onChange}
      onBlur={field.onBlur} error={fieldState.error?.message} />} />
    <Controller control={control} name="language" render={({ field }) => <View style={{ gap: 10 }}>
      <Text style={styles.label}>Preferred language</Text>
      {languages.map((language) => <Pressable key={language.code} accessibilityRole="radio"
        accessibilityState={{ checked: field.value === language.code }} onPress={() => field.onChange(language.code)}
        style={[styles.input, field.value === language.code && { borderColor: colors.green, borderWidth: 2 }]}>
        <Text style={styles.label}>{language.name}{field.value === language.code ? '  ✓' : ''}</Text>
      </Pressable>)}
      <Text style={styles.detail}>We’ll save your preference. Full translations are coming before launch.</Text>
    </View>} />
    <ErrorText message={mutation.error ? errorMessage(mutation.error) : null} />
    <Button title="Save institute" busy={mutation.isPending} onPress={handleSubmit((values) => mutation.mutate(values))} />
  </Screen>;
}

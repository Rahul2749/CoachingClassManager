import type { PropsWithChildren } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

export const colors = { ink: '#12352E', muted: '#60756D', paper: '#F5F7F2', green: '#22634C', border: '#D9E2D8', danger: '#A53333' };

export function Screen({ children }: PropsWithChildren) {
  return <SafeAreaView style={styles.safe}>
    <KeyboardAvoidingView style={styles.safe} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">{children}</ScrollView>
    </KeyboardAvoidingView>
  </SafeAreaView>;
}

export function Heading({ title, detail }: { title: string; detail?: string }) {
  return <View style={{ gap: 12 }}>
    <Text style={styles.eyebrow}>COACHING CLASS MANAGER</Text>
    <Text accessibilityRole="header" style={styles.title}>{title}</Text>
    {detail ? <Text style={styles.detail}>{detail}</Text> : null}
  </View>;
}

export function Field({ label, error, ...props }: TextInputProps & { label: string; error?: string }) {
  return <View style={{ gap: 8 }}>
    <Text style={styles.label}>{label}</Text>
    <TextInput accessibilityLabel={label} placeholderTextColor={colors.muted} style={styles.input} {...props} />
    {error ? <ErrorText message={error} /> : null}
  </View>;
}

export function Button({ title, onPress, busy = false, secondary = false, disabled = false }: {
  title: string; onPress: () => void; busy?: boolean; secondary?: boolean; disabled?: boolean;
}) {
  return <Pressable accessibilityRole="button" accessibilityState={{ disabled: disabled || busy }}
    disabled={disabled || busy} onPress={onPress}
    style={({ pressed }) => [styles.button, secondary && styles.secondary, (pressed || disabled || busy) && { opacity: 0.65 }]}>
    {busy ? <ActivityIndicator color={secondary ? colors.green : 'white'} />
      : <Text style={[styles.buttonText, secondary && { color: colors.green }]}>{title}</Text>}
  </Pressable>;
}

export function ErrorText({ message }: { message: string | null }) {
  return message ? <Text accessibilityRole="alert" style={styles.error}>{message}</Text> : null;
}

export const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.paper },
  content: { flexGrow: 1, padding: 24, paddingTop: 40, gap: 24, width: '100%', maxWidth: 540, alignSelf: 'center' },
  eyebrow: { fontSize: 11, fontWeight: '700', letterSpacing: 2, color: colors.green },
  title: { fontSize: 34, fontWeight: '700', lineHeight: 42, color: colors.ink },
  detail: { fontSize: 16, lineHeight: 25, color: colors.muted },
  label: { fontSize: 14, color: colors.ink, fontWeight: '600' },
  input: { minHeight: 56, borderWidth: 1, borderColor: colors.border, borderRadius: 14, backgroundColor: 'white', padding: 16, fontSize: 17, color: colors.ink },
  button: { minHeight: 56, borderRadius: 14, backgroundColor: colors.green, alignItems: 'center', justifyContent: 'center', padding: 16 },
  buttonText: { color: 'white', fontSize: 16, fontWeight: '700' },
  secondary: { backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.border },
  error: { color: colors.danger, fontSize: 14, lineHeight: 22 },
  card: { padding: 22, backgroundColor: 'white', borderRadius: 18, borderWidth: 1, borderColor: colors.border, gap: 12 },
});

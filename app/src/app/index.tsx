import { Redirect } from 'expo-router';
import { useAuth } from '../auth/store';

export default function Index() {
  const profile = useAuth((s) => s.profile);
  return <Redirect href={!profile ? '/sign-in' : profile.instituteName ? '/home' : '/setup'} />;
}

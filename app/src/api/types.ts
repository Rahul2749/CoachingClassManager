export type Profile = {
  teacherId: string;
  instituteId: string;
  phone: string;
  instituteName: string;
  language: 'en' | 'hi' | 'mr';
};

export type Tokens = {
  accessToken: string;
  refreshToken: string;
  accessExpiresAt: string;
  refreshExpiresAt: string;
};

export type TokenResponse = Tokens & { profile: Profile };
export type Challenge = {
  challengeId: string;
  expiresAt: string;
  resendAfterSeconds: number;
  developmentCode?: string;
  phone: string;
};

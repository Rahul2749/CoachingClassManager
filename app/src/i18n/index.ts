import { createInstance } from 'i18next';
import { initReactI18next } from 'react-i18next';

const i18n = createInstance();
void i18n.use(initReactI18next).init({
  lng: 'en', fallbackLng: 'en', interpolation: { escapeValue: false },
  resources: {
    en: { translation: { welcome: 'Your classroom, together.', home: 'Your institute', signOut: 'Sign out', account: 'Account', next: 'Students and batches are coming next.' } },
    hi: { translation: { welcome: 'आपकी कक्षा, एक साथ।', home: 'आपकी संस्था', signOut: 'साइन आउट', account: 'खाता', next: 'अगले चरण में विद्यार्थी और बैच जुड़ेंगे।' } },
    mr: { translation: { welcome: 'तुमचा वर्ग, एकत्र.', home: 'तुमची संस्था', signOut: 'साइन आउट', account: 'खाते', next: 'पुढील टप्प्यात विद्यार्थी आणि बॅच जोडले जातील.' } },
  },
});

export default i18n;

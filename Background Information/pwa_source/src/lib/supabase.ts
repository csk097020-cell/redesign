import { createClient } from '@supabase/supabase-js';

// Initialize Supabase client with error handling
const supabaseUrl = 'https://wnumxvfvabqpgwenweff.supabase.co';
const supabaseKey = 'sb_publishable_Yqh2E7dPyGKB6y84MX8iBg_CvSe-Ti0';

const supabase = createClient(supabaseUrl, supabaseKey, {
  auth: {
    persistSession: true,
    autoRefreshToken: true,
  },
  global: {
    fetch: (url, options = {}) => {
      return fetch(url, {
        ...options,
        signal: AbortSignal.timeout(10000), // 10 second timeout
      }).catch(err => {
        console.warn('Supabase fetch error (falling back to local storage):', err.message);
        throw err;
      });
    }
  }
});

export { supabase };

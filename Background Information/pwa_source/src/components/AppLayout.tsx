import React, { useState, useEffect } from 'react';
import LandingHero from './LandingHero';
import HowItWorks from './HowItWorks';
import Features from './Features';
import Testimonials from './Testimonials';
import FAQ from './FAQ';
import Footer from './Footer';
import AuthModal from './AuthModal';
import Dashboard from './Dashboard';
import { supabase } from '@/lib/supabase';

const AppLayout: React.FC = () => {
  const [showAuthModal, setShowAuthModal] = useState(false);
  const [user, setUser] = useState<any>(null);
  const [showDemoVideo, setShowDemoVideo] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Check for existing session
    checkUser();
    
    // Listen for auth changes
    const { data: { subscription } } = supabase.auth.onAuthStateChange((_event, session) => {
      setUser(session?.user || null);
    });

    return () => subscription.unsubscribe();
  }, []);

  const checkUser = async () => {
    try {
      const { data: { user } } = await supabase.auth.getUser();
      setUser(user);
    } catch (error) {
      console.error('Error checking user:', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    // Check for auth modal trigger
    const checkAuthModal = () => {
      const authModal = document.getElementById('auth-modal');
      if (authModal && !authModal.classList.contains('hidden')) {
        setShowAuthModal(true);
        authModal.classList.add('hidden');
      }
    };

    // Check for demo video trigger
    const checkDemoVideo = () => {
      const demoVideo = document.getElementById('demo-video');
      if (demoVideo && !demoVideo.classList.contains('hidden')) {
        setShowDemoVideo(true);
        demoVideo.classList.add('hidden');
      }
    };

    const interval = setInterval(() => {
      checkAuthModal();
      checkDemoVideo();
    }, 100);

    return () => clearInterval(interval);
  }, []);

  const handleAuthSuccess = (userData: any) => {
    setUser(userData);
    setShowAuthModal(false);
  };

  const handleLogout = () => {
    setUser(null);
  };

  // Show loading state
  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-coral-500"></div>
      </div>
    );
  }

  // If user is logged in, show dashboard
  if (user) {
    return <Dashboard user={user} onLogout={handleLogout} />;
  }
  // Show landing page
  return (
    <div className="min-h-screen">
      {/* Hidden triggers */}
      <div id="auth-modal" className="hidden"></div>
      <div id="demo-video" className="hidden"></div>

      {/* Landing Page */}
      <LandingHero />
      <HowItWorks />
      <Features />
      <Testimonials />
      <FAQ />
      <Footer />

      {/* Auth Modal */}
      {showAuthModal && (
        <AuthModal 
          onClose={() => setShowAuthModal(false)}
          onSuccess={handleAuthSuccess}
        />
      )}

      {/* Demo Video Modal */}
      {showDemoVideo && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-4xl overflow-hidden">
            <div className="relative aspect-video bg-gray-900">
              <button
                onClick={() => setShowDemoVideo(false)}
                className="absolute top-4 right-4 z-10 p-2 bg-white/10 backdrop-blur rounded-full hover:bg-white/20 transition-colors"
              >
                <svg className="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
              
              <div className="absolute inset-0 flex items-center justify-center">
                <div className="text-center text-white">
                  <svg className="w-20 h-20 mx-auto mb-4 text-white/50" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM9.555 7.168A1 1 0 008 8v4a1 1 0 001.555.832l3-2a1 1 0 000-1.664l-3-2z" clipRule="evenodd" />
                  </svg>
                  <p className="text-xl">Demo Video Coming Soon</p>
                  <p className="text-white/60 mt-2">Experience Momentary Momentos in action</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AppLayout;
import React from 'react';
import { ChevronDown, Play } from 'lucide-react';
import HourglassLogo from './HourglassLogo';
const LandingHero: React.FC = () => {
  const scrollToFeatures = () => {
    document.getElementById('features')?.scrollIntoView({
      behavior: 'smooth'
    });
  };
  const handleGetStarted = () => {
    document.getElementById('auth-modal')?.classList.remove('hidden');
  };
  return <section className="relative min-h-screen flex items-center justify-center overflow-hidden">
      {/* Background Image with Overlay */}
      <div className="absolute inset-0 z-0">
        <img src="https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927940819_ae302e25.webp" alt="Memory background" className="w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-b from-black/30 via-transparent to-black/50"></div>
      </div>

      {/* Content */}
      <div className="relative z-10 text-center px-4 sm:px-6 lg:px-8 max-w-5xl mx-auto">
        <div className="animate-fade-in-up">
          {/* Hourglass Logo */}
          <div className="flex justify-center mb-6">
            <HourglassLogo className="w-20 h-20 text-coral-400 animate-pulse" />
          </div>
          
          <h1 className="text-5xl sm:text-6xl lg:text-7xl font-bold text-white mb-6" data-mixed-content="true" data-mixed-content="true">

            Your life,{' '}
            <span className="bg-clip-text bg-gradient-to-r from-orange-400 via-red-500 to-pink-500 bg-cover bg-center text-purple-500">
              10 seconds
            </span>{' '}
            at a time
          </h1>
          <p className="text-xl sm:text-2xl text-white/90 mb-8 max-w-2xl mx-auto">
            Capture. Tag. Relive. Transform fleeting moments into lasting memories with Momentary Momentos' intelligent video diary.
          </p>
          
          <div className="flex flex-col sm:flex-row gap-4 justify-center mb-12">
            <button onClick={handleGetStarted} className="px-8 py-4 bg-gradient-to-r from-coral-500 to-coral-600 text-white rounded-full font-semibold text-lg hover:from-coral-600 hover:to-coral-700 transform hover:scale-105 transition-all duration-200 shadow-xl">
              Start Capturing Memories
            </button>
            <button onClick={() => document.getElementById('demo-video')?.classList.remove('hidden')} className="px-8 py-4 bg-white/10 backdrop-blur-md text-white rounded-full font-semibold text-lg hover:bg-white/20 transform hover:scale-105 transition-all duration-200 border border-white/30 flex items-center justify-center gap-2">
              <Play className="w-5 h-5" />
              Watch Demo
            </button>
          </div>

          <div className="flex justify-center gap-8 text-white/80">
            <div className="text-center">
              <div className="text-3xl font-bold">10K+</div>
              <div className="text-sm">Active Users</div>
            </div>
            <div className="text-center">
              <div className="text-3xl font-bold">1M+</div>
              <div className="text-sm">Memories Saved</div>
            </div>
            <div className="text-center">
              <div className="text-3xl font-bold">4.9★</div>
              <div className="text-sm">User Rating</div>
            </div>
          </div>
        </div>

        {/* Scroll Indicator */}
        <button onClick={scrollToFeatures} className="absolute bottom-8 left-1/2 transform -translate-x-1/2 animate-bounce">
          <ChevronDown className="w-8 h-8 text-white/60" />
        </button>
      </div>
    </section>;
};
export default LandingHero;
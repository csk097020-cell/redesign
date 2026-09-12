import React from 'react';
import { Brain, Shield, Cloud, Sparkles, Heart, Clock } from 'lucide-react';

const Features: React.FC = () => {
  const features = [
    {
      icon: Brain,
      title: 'Smart Memory Capture',
      description: 'AI-powered recording that captures the perfect 10 seconds every time',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927948848_02a1e137.webp'
    },
    {
      icon: Sparkles,
      title: 'Intelligent Playback',
      description: 'Algorithm learns your preferences to surface memories that matter most',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927950667_2b1fb97c.webp'
    },
    {
      icon: Heart,
      title: 'Personalized Tag System',
      description: 'Custom categories and smart suggestions make organizing effortless',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927952365_3af4f86e.webp'
    },
    {
      icon: Cloud,
      title: 'Secure Cloud Storage',
      description: 'Your memories safely backed up and synced across all devices',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927954550_34fc0fda.webp'
    },
    {
      icon: Shield,
      title: 'Privacy First',
      description: 'End-to-end encryption ensures your memories stay yours alone',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927956274_4099ff42.webp'
    },
    {
      icon: Clock,
      title: 'Time Capsule Mode',
      description: 'Schedule memories to resurface on special dates and anniversaries',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927958004_3d367bd6.webp'
    }
  ];

  return (
    <section id="features" className="py-20 bg-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-4xl sm:text-5xl font-bold text-gray-900 mb-4">
            Features That{' '}
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-coral-500 to-purple-500">
              Matter
            </span>
          </h2>
          <p className="text-xl text-gray-600 max-w-2xl mx-auto">
            Every feature designed to make capturing and reliving memories magical
          </p>
        </div>

        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
          {features.map((feature, index) => (
            <div 
              key={index}
              className="group bg-gradient-to-br from-white to-cream-50 rounded-2xl p-6 shadow-md hover:shadow-xl transition-all duration-300 border border-gray-100"
            >
              <div className="mb-4 h-48 rounded-xl overflow-hidden">
                <img 
                  src={feature.image}
                  alt={feature.title}
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />
              </div>
              
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gradient-to-br from-coral-100 to-purple-100 rounded-xl flex items-center justify-center flex-shrink-0 group-hover:scale-110 transition-transform">
                  <feature.icon className="w-6 h-6 text-coral-600" />
                </div>
                
                <div>
                  <h3 className="text-xl font-bold text-gray-900 mb-2">
                    {feature.title}
                  </h3>
                  <p className="text-gray-600 leading-relaxed">
                    {feature.description}
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
};

export default Features;
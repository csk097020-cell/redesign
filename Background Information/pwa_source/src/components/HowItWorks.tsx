import React from 'react';
import { Camera, Tag, Play } from 'lucide-react';

const HowItWorks: React.FC = () => {
  const steps = [
    {
      icon: Camera,
      title: 'Capture',
      description: 'Record 10-second video memories with a single tap. Our smart camera captures the perfect moment every time.',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927964699_e35ab4d1.webp'
    },
    {
      icon: Tag,
      title: 'Tag',
      description: 'Organize your memories with intelligent tags. Family, travel, celebrations - find any memory instantly.',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927966461_bd2d8d03.webp'
    },
    {
      icon: Play,
      title: 'Relive',
      description: 'Let Momentary Momentos surprise you with perfectly timed memories. Our AI learns what makes you smile.',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927968319_74986751.webp'
    }
  ];

  return (
    <section className="py-20 bg-gradient-to-b from-white to-cream-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-4xl sm:text-5xl font-bold text-gray-900 mb-4">
            How It Works
          </h2>
          <p className="text-xl text-gray-600 max-w-2xl mx-auto">
            Three simple steps to transform your daily moments into a lifetime of memories
          </p>
        </div>

        <div className="grid md:grid-cols-3 gap-8">
          {steps.map((step, index) => (
            <div 
              key={index}
              className="group relative bg-white rounded-2xl shadow-lg overflow-hidden hover:shadow-2xl transition-all duration-300 transform hover:-translate-y-2"
            >
              <div className="aspect-video overflow-hidden">
                <img 
                  src={step.image}
                  alt={step.title}
                  className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-500"
                />
              </div>
              
              <div className="p-8">
                <div className="flex items-center gap-4 mb-4">
                  <div className="w-12 h-12 bg-gradient-to-br from-coral-400 to-purple-400 rounded-full flex items-center justify-center">
                    <step.icon className="w-6 h-6 text-white" />
                  </div>
                  <div className="text-3xl font-bold text-coral-500">
                    {String(index + 1).padStart(2, '0')}
                  </div>
                </div>
                
                <h3 className="text-2xl font-bold text-gray-900 mb-3">
                  {step.title}
                </h3>
                <p className="text-gray-600 leading-relaxed">
                  {step.description}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
};

export default HowItWorks;
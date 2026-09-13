import React, { useState } from 'react';
import { ChevronLeft, ChevronRight, Star } from 'lucide-react';

const Testimonials: React.FC = () => {
  const [currentIndex, setCurrentIndex] = useState(0);
  
  const testimonials = [
    {
      name: 'Sarah Johnson',
      role: 'Mother of Two',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927974615_ca9954e8.webp',
      content: 'Momentary Momentos has transformed how I capture my kids growing up. The 10-second limit forces me to focus on what really matters, and the surprise playback always brings tears of joy.',
      rating: 5
    },
    {
      name: 'Michael Chen',
      role: 'Travel Blogger',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927976342_975d8763.webp',
      content: 'As someone always on the go, Momentary Momentos is perfect. Quick captures, smart organization, and those random memory replays during long flights are absolutely priceless.',
      rating: 5
    },
    {
      name: 'Emily Rodriguez',
      role: 'College Student',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927978085_95e17c3d.webp',
      content: 'This app helped me document my entire college journey. Looking back at random memories from freshman year while studying for finals was exactly what I needed.',
      rating: 5
    },
    {
      name: 'David Thompson',
      role: 'Entrepreneur',
      image: 'https://d64gsuwffb70l.cloudfront.net/68e65e53d61797aeea515f1d_1759927979823_4378b3f6.webp',
      content: 'In the hustle of building a company, Momentary Momentos reminds me to celebrate small wins. The AI knows exactly when I need a boost - it\'s like having a personal happiness curator.',
      rating: 5
    }
  ];

  const nextTestimonial = () => {
    setCurrentIndex((prev) => (prev + 1) % testimonials.length);
  };

  const prevTestimonial = () => {
    setCurrentIndex((prev) => (prev - 1 + testimonials.length) % testimonials.length);
  };

  return (
    <section className="py-20 bg-gradient-to-b from-cream-50 to-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-4xl sm:text-5xl font-bold text-gray-900 mb-4">
            Loved by{' '}
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-coral-500 to-purple-500">
              Thousands
            </span>
          </h2>
          <p className="text-xl text-gray-600 max-w-2xl mx-auto">
            Real stories from people whose lives have been enriched by Momentary Momentos
          </p>
        </div>

        <div className="relative max-w-4xl mx-auto">
          <div className="bg-white rounded-3xl shadow-xl p-8 md:p-12">
            <div className="flex flex-col md:flex-row items-center gap-8">
              <img 
                src={testimonials[currentIndex].image}
                alt={testimonials[currentIndex].name}
                className="w-32 h-32 rounded-full object-cover shadow-lg"
              />
              
              <div className="flex-1 text-center md:text-left">
                <div className="flex justify-center md:justify-start mb-4">
                  {[...Array(testimonials[currentIndex].rating)].map((_, i) => (
                    <Star key={i} className="w-5 h-5 text-yellow-400 fill-current" />
                  ))}
                </div>
                
                <p className="text-lg text-gray-700 mb-6 italic">
                  "{testimonials[currentIndex].content}"
                </p>
                
                <div>
                  <div className="font-bold text-gray-900">
                    {testimonials[currentIndex].name}
                  </div>
                  <div className="text-gray-600">
                    {testimonials[currentIndex].role}
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="flex justify-center gap-4 mt-8">
            <button 
              onClick={prevTestimonial}
              className="p-3 bg-white rounded-full shadow-md hover:shadow-lg transition-shadow"
            >
              <ChevronLeft className="w-6 h-6 text-gray-600" />
            </button>
            
            <div className="flex gap-2 items-center">
              {testimonials.map((_, index) => (
                <div 
                  key={index}
                  className={`h-2 rounded-full transition-all duration-300 ${
                    index === currentIndex 
                      ? 'w-8 bg-gradient-to-r from-coral-500 to-purple-500' 
                      : 'w-2 bg-gray-300'
                  }`}
                />
              ))}
            </div>
            
            <button 
              onClick={nextTestimonial}
              className="p-3 bg-white rounded-full shadow-md hover:shadow-lg transition-shadow"
            >
              <ChevronRight className="w-6 h-6 text-gray-600" />
            </button>
          </div>
        </div>
      </div>
    </section>
  );
};

export default Testimonials;
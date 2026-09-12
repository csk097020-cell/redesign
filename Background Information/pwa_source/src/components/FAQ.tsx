import React, { useState } from 'react';
import { Plus, Minus } from 'lucide-react';

const FAQ: React.FC = () => {
  const [openIndex, setOpenIndex] = useState<number | null>(0);
  
  const faqs = [
    {
      question: 'How does the 10-second limit work?',
      answer: 'Our smart capture system automatically records exactly 10 seconds of video. This constraint helps you focus on capturing the essence of each moment without overthinking. You can preview and retake if needed before saving.'
    },
    {
      question: 'Is my data secure and private?',
      answer: 'Absolutely. We use end-to-end encryption for all your memories. Your videos are stored securely in the cloud and only you have access to them. We never analyze your personal content, and memories can only be accessed through authenticated in-app reliving.'
    },
    {
      question: 'How does the intelligent playback algorithm work?',
      answer: 'Our AI learns from your interactions - which memories you favorite, skip, or delete. Over time, it understands your preferences and surfaces memories that are most meaningful to you at the perfect moments.'
    },
    {
      question: 'Can I export or download my memories?',
      answer: 'Yes! You can download individual memories to your device at any time. We believe your memories belong to you, so we make it easy to keep local copies. Downloaded memories can be shared manually through your preferred apps.'
    },
    {
      question: 'How much storage do I get?',
      answer: 'Free accounts include 5GB of storage (approximately 500 memories). Premium plans offer unlimited storage, advanced tagging features, and priority AI processing for just $4.99/month.'
    },
    {
      question: 'Why can\'t I browse all my memories?',
      answer: 'Momentary Momentos is designed around surprise and intentional reliving. Instead of scrolling through archives, memories resurface naturally through our intelligent algorithm. This preserves the emotional impact of rediscovering forgotten moments.'
    }
  ];


  const toggleFAQ = (index: number) => {
    setOpenIndex(openIndex === index ? null : index);
  };

  return (
    <section className="py-20 bg-white">
      <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-4xl sm:text-5xl font-bold text-gray-900 mb-4">
            Frequently Asked Questions
          </h2>
          <p className="text-xl text-gray-600">
            Everything you need to know about Momentary Momentos
          </p>
        </div>

        <div className="space-y-4">
          {faqs.map((faq, index) => (
            <div 
              key={index}
              className="bg-gradient-to-br from-white to-cream-50 rounded-2xl shadow-md overflow-hidden transition-all duration-300 hover:shadow-lg"
            >
              <button
                onClick={() => toggleFAQ(index)}
                className="w-full px-6 py-5 flex items-center justify-between text-left"
              >
                <h3 className="text-lg font-semibold text-gray-900 pr-4">
                  {faq.question}
                </h3>
                <div className="flex-shrink-0">
                  {openIndex === index ? (
                    <Minus className="w-5 h-5 text-coral-500" />
                  ) : (
                    <Plus className="w-5 h-5 text-coral-500" />
                  )}
                </div>
              </button>
              
              <div className={`transition-all duration-300 ${
                openIndex === index ? 'max-h-96' : 'max-h-0'
              } overflow-hidden`}>
                <div className="px-6 pb-5">
                  <p className="text-gray-600 leading-relaxed">
                    {faq.answer}
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

export default FAQ;

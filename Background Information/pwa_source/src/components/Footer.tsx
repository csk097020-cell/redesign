import React from 'react';
import { Heart, Twitter, Instagram, Facebook, Youtube, Mail, Phone, MapPin } from 'lucide-react';
import HourglassLogo from './HourglassLogo';

const Footer: React.FC = () => {
  const currentYear = new Date().getFullYear();
  const footerLinks = {
    product: [{
      name: 'Features',
      href: '#features'
    }, {
      name: 'How It Works',
      href: '#how-it-works'
    }, {
      name: 'Pricing',
      href: '#pricing'
    }, {
      name: 'Download',
      href: '#download'
    }],
    company: [{
      name: 'About Us',
      href: '#about'
    }, {
      name: 'Careers',
      href: '#careers'
    }, {
      name: 'Blog',
      href: '#blog'
    }, {
      name: 'Press Kit',
      href: '#press'
    }],
    support: [{
      name: 'Help Center',
      href: '#help'
    }, {
      name: 'Contact',
      href: '#contact'
    }, {
      name: 'Privacy Policy',
      href: '#privacy'
    }, {
      name: 'Terms of Service',
      href: '#terms'
    }],
    community: [{
      name: 'User Stories',
      href: '#stories'
    }, {
      name: 'Tips',
      href: '#tips'
    }, {
      name: 'Developers',
      href: '#developers'
    }, {
      name: 'Affiliates',
      href: '#affiliates'
    }]
  };
  const socialLinks = [{
    icon: Twitter,
    href: '#',
    label: 'Twitter'
  }, {
    icon: Instagram,
    href: '#',
    label: 'Instagram'
  }, {
    icon: Facebook,
    href: '#',
    label: 'Facebook'
  }, {
    icon: Youtube,
    href: '#',
    label: 'YouTube'
  }];
  return <footer className="bg-gradient-to-b from-gray-900 to-black text-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        {/* Main Footer Content */}
        <div className="grid grid-cols-2 md:grid-cols-6 gap-8 mb-12">
          {/* Brand Section */}
          <div className="col-span-2">
            <div className="flex items-center gap-2 mb-4">
              <HourglassLogo className="w-8 h-8 text-coral-500" />

              <span className="text-2xl font-bold">Momentary Momentos</span>
            </div>
            <p className="text-gray-400 mb-6">
              Capture. Tag. Relive. Transform your daily moments into lasting memories.
            </p>
            <div className="flex gap-4">
              {socialLinks.map((social, index) => <a key={index} href={social.href} aria-label={social.label} className="w-10 h-10 bg-white/10 rounded-full flex items-center justify-center hover:bg-coral-500 transition-colors">
                  <social.icon className="w-5 h-5" />
                </a>)}
            </div>
          </div>

          {/* Links Sections */}
          <div>
            <h3 className="font-semibold mb-4 text-gray-300">Product</h3>
            <ul className="space-y-2">
              {footerLinks.product.map((link, index) => <li key={index}>
                  <a href={link.href} className="text-gray-400 hover:text-coral-400 transition-colors">
                    {link.name}
                  </a>
                </li>)}
            </ul>
          </div>

          <div>
            <h3 className="font-semibold mb-4 text-gray-300">Company</h3>
            <ul className="space-y-2">
              {footerLinks.company.map((link, index) => <li key={index}>
                  <a href={link.href} className="text-gray-400 hover:text-coral-400 transition-colors">
                    {link.name}
                  </a>
                </li>)}
            </ul>
          </div>

          <div>
            <h3 className="font-semibold mb-4 text-gray-300">Support</h3>
            <ul className="space-y-2">
              {footerLinks.support.map((link, index) => <li key={index}>
                  <a href={link.href} className="text-gray-400 hover:text-coral-400 transition-colors">
                    {link.name}
                  </a>
                </li>)}
            </ul>
          </div>

          <div>
            <h3 className="font-semibold mb-4 text-gray-300">Community</h3>
            <ul className="space-y-2">
              {footerLinks.community.map((link, index) => <li key={index}>
                  <a href={link.href} className="text-gray-400 hover:text-coral-400 transition-colors">
                    {link.name}
                  </a>
                </li>)}
            </ul>
          </div>
        </div>

        {/* Contact Info */}
        <div className="border-t border-gray-800 pt-8 mb-8">
          <div className="flex flex-col md:flex-row justify-between items-center gap-4">
            <div className="flex flex-col md:flex-row gap-6 text-gray-400">
              <div className="flex items-center gap-2">
                <Mail className="w-4 h-4" />
                <span>hello@momentarymoments.app</span>
              </div>
              <div className="flex items-center gap-2">
                <Phone className="w-4 h-4" />
                <span>1-800-MOMENTS</span>
              </div>
              <div className="flex items-center gap-2">
                <MapPin className="w-4 h-4" />
                <span>San Francisco, CA</span>
              </div>
            </div>
          </div>
        </div>

        {/* Bottom Bar */}
        <div className="border-t border-gray-800 pt-8">
          <div className="flex flex-col md:flex-row justify-between items-center gap-4">
            <p className="text-gray-400 text-sm" data-mixed-content="true">
              © {currentYear} Momentary Momentos. All rights reserved.
            </p>
            <p className="text-gray-400 text-sm flex items-center gap-1">
              Made with <Heart className="w-4 h-4 text-coral-500" /> for memory keepers
            </p>
          </div>
        </div>
      </div>
    </footer>;
};
export default Footer;

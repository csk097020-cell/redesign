import React, { useState } from 'react';
import { X, Mail, Lock, User, Eye, EyeOff, Loader } from 'lucide-react';
import { supabase } from '@/lib/supabase';
import { useToast } from '@/hooks/use-toast';

interface AuthModalProps {
  onClose: () => void;
  onSuccess: (user: any) => void;
}

const AuthModal: React.FC<AuthModalProps> = ({ onClose, onSuccess }) => {
  const [isSignUp, setIsSignUp] = useState(true);
  const [isForgotPassword, setIsForgotPassword] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [formData, setFormData] = useState({
    email: '',
    password: '',
    name: ''
  });
  const [errors, setErrors] = useState<any>({});
  const { toast } = useToast();


  const validateForm = () => {
    const newErrors: any = {};
    if (!formData.email) newErrors.email = 'Email is required';
    else if (!/\S+@\S+\.\S+/.test(formData.email)) newErrors.email = 'Invalid email';
    if (!formData.password) newErrors.password = 'Password is required';
    else if (formData.password.length < 6) newErrors.password = 'Password must be at least 6 characters';
    if (isSignUp && !formData.name) newErrors.name = 'Name is required';
    return newErrors;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (isForgotPassword) {
      if (!formData.email) {
        setErrors({ email: 'Email is required' });
        return;
      }
      
      setIsLoading(true);
      try {
        const { error } = await supabase.auth.resetPasswordForEmail(formData.email, {
          redirectTo: `${window.location.origin}/reset-password`
        });
        
        if (error) throw error;
        
        toast({
          title: "Reset email sent!",
          description: "Check your inbox for password reset instructions"
        });
        setIsForgotPassword(false);
      } catch (error: any) {
        toast({
          title: "Error",
          description: error.message,
          variant: "destructive"
        });
      } finally {
        setIsLoading(false);
      }
      return;
    }
    
    const newErrors = validateForm();
    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }
    
    setIsLoading(true);
    
    try {
      if (isSignUp) {
        const { data, error } = await supabase.auth.signUp({
          email: formData.email,
          password: formData.password,
          options: {
            data: {
              name: formData.name
            }
          }
        });
        
        if (error) throw error;
        
        if (data.user) {
          toast({
            title: "Check your email!",
            description: "We've sent you a confirmation link. Please verify your email before signing in."
          });
          setIsSignUp(false); // Switch to sign-in mode
          setFormData({ email: formData.email, password: '', name: '' }); // Keep email, clear password
        }
      } else {
        const { data, error } = await supabase.auth.signInWithPassword({
          email: formData.email,
          password: formData.password
        });
        
        if (error) {
          // Check if it's an email not confirmed error
          if (error.message.includes('Email not confirmed')) {
            toast({
              title: "Email not verified",
              description: "Please check your inbox and confirm your email before signing in.",
              variant: "destructive"
            });
          } else {
            throw error;
          }
          return;
        }
        
        if (data.user) {
          // Double check email is confirmed
          if (!data.user.email_confirmed_at) {
            toast({
              title: "Email not verified",
              description: "Please check your inbox and confirm your email before signing in.",
              variant: "destructive"
            });
            await supabase.auth.signOut();
            return;
          }
          
          toast({
            title: "Welcome back!",
            description: "Continue your memory journey"
          });
          onSuccess(data.user);
        }
      }

    } catch (error: any) {
      console.error('Auth error:', error);
      toast({
        title: "Authentication Failed",
        description: error.message || "Please check your credentials and try again",
        variant: "destructive"
      });
    } finally {
      setIsLoading(false);
    }
  };


  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
    setErrors({ ...errors, [e.target.name]: '' });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
      <div className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden animate-fade-in-up">
        <div className="relative p-8">
          <button
            onClick={onClose}
            className="absolute top-4 right-4 p-2 rounded-full hover:bg-gray-100 transition-colors"
            disabled={isLoading}
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>

          <div className="text-center mb-8">
            <h2 className="text-3xl font-bold text-gray-900 mb-2">
              {isForgotPassword ? 'Reset Password' : isSignUp ? 'Create Account' : 'Welcome Back'}
            </h2>
            <p className="text-gray-600">
              {isForgotPassword ? 'Enter your email to receive reset instructions' : isSignUp ? 'Start capturing your memories' : 'Continue your memory journey'}
            </p>
          </div>


          <form onSubmit={handleSubmit} className="space-y-4">
            {isSignUp && !isForgotPassword && (
              <div>
                <div className="relative">
                  <User className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                  <input
                    type="text"
                    name="name"
                    value={formData.name}
                    onChange={handleInputChange}
                    placeholder="Full Name"
                    disabled={isLoading}
                    className={`w-full pl-10 pr-4 py-3 border rounded-xl focus:outline-none focus:ring-2 focus:ring-coral-500 ${
                      errors.name ? 'border-red-500' : 'border-gray-300'
                    } ${isLoading ? 'bg-gray-50' : ''}`}
                  />
                </div>
                {errors.name && <p className="text-red-500 text-sm mt-1">{errors.name}</p>}
              </div>
            )}

            <div>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                <input
                  type="email"
                  name="email"
                  value={formData.email}
                  onChange={handleInputChange}
                  placeholder="Email Address"
                  disabled={isLoading}
                  className={`w-full pl-10 pr-4 py-3 border rounded-xl focus:outline-none focus:ring-2 focus:ring-coral-500 ${
                    errors.email ? 'border-red-500' : 'border-gray-300'
                  } ${isLoading ? 'bg-gray-50' : ''}`}
                />
              </div>
              {errors.email && <p className="text-red-500 text-sm mt-1">{errors.email}</p>}
            </div>

            {!isForgotPassword && (
              <div>
                <div className="relative">
                  <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                  <input
                    type={showPassword ? 'text' : 'password'}
                    name="password"
                    value={formData.password}
                    onChange={handleInputChange}
                    placeholder="Password"
                    disabled={isLoading}
                    className={`w-full pl-10 pr-12 py-3 border rounded-xl focus:outline-none focus:ring-2 focus:ring-coral-500 ${
                      errors.password ? 'border-red-500' : 'border-gray-300'
                    } ${isLoading ? 'bg-gray-50' : ''}`}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 p-1"
                    disabled={isLoading}
                  >
                    {showPassword ? (
                      <EyeOff className="w-5 h-5 text-gray-400" />
                    ) : (
                      <Eye className="w-5 h-5 text-gray-400" />
                    )}
                  </button>
                </div>
                {errors.password && <p className="text-red-500 text-sm mt-1">{errors.password}</p>}
              </div>
            )}

            {!isSignUp && !isForgotPassword && (
              <div className="text-right">
                <button
                  type="button"
                  onClick={() => setIsForgotPassword(true)}
                  disabled={isLoading}
                  className="text-sm text-coral-500 hover:text-coral-600 disabled:opacity-50"
                >
                  Forgot password?
                </button>
              </div>
            )}

            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 bg-gradient-to-r from-coral-500 to-coral-600 text-white rounded-xl font-semibold hover:from-coral-600 hover:to-coral-700 transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
            >
              {isLoading && <Loader className="w-5 h-5 animate-spin" />}
              {isForgotPassword ? 'Send Reset Link' : isSignUp ? 'Create Account' : 'Sign In'}
            </button>
          </form>

          <div className="mt-6 text-center">
            {isForgotPassword ? (
              <button
                type="button"
                onClick={() => {
                  setIsForgotPassword(false);
                  setErrors({});
                }}
                disabled={isLoading}
                className="text-coral-500 font-semibold hover:text-coral-600 disabled:opacity-50"
              >
                Back to Sign In
              </button>
            ) : (
              <p className="text-gray-600">
                {isSignUp ? 'Already have an account?' : "Don't have an account?"}{' '}
                <button
                  type="button"
                  onClick={() => {
                    setIsSignUp(!isSignUp);
                    setErrors({});
                  }}
                  disabled={isLoading}
                  className="text-coral-500 font-semibold hover:text-coral-600 disabled:opacity-50"
                >
                  {isSignUp ? 'Sign In' : 'Sign Up'}
                </button>
              </p>
            )}
          </div>

        </div>
      </div>
    </div>
  );
};

export default AuthModal;
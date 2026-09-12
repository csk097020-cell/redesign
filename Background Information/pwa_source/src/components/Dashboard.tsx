import React, { useState, useEffect } from 'react';
import { Camera, Play, Heart, Film, HardDrive, LogOut, Grid, TrendingUp, Loader, X, Shield, Trash2, Upload, Download } from 'lucide-react';
import HourglassLogo from './HourglassLogo';

import MemoryCapture from './MemoryCapture';
import MemoryPlayback from './MemoryPlayback';
import AdminPanel from './AdminPanel';
import ProfileEditModal from './ProfileEditModal';
import UploadMemory from './UploadMemory';

import { supabase } from '@/lib/supabase';
import { useToast } from '@/hooks/use-toast';
import { getEmotionTagColor } from '@/config/emotionTags';

interface DashboardProps {
  user: any;
  onLogout: () => void;
}

interface TagInfo {
  id: string;
  name: string;
  color: string;
}

// Helper function to determine text color based on background brightness
const getContrastTextColor = (hexColor: string): string => {
  const hex = hexColor.replace('#', '');
  const r = parseInt(hex.substr(0, 2), 16);
  const g = parseInt(hex.substr(2, 2), 16);
  const b = parseInt(hex.substr(4, 2), 16);
  const brightness = (r * 299 + g * 587 + b * 114) / 1000;
  return brightness > 155 ? '#1F2937' : '#FFFFFF';
};

const Dashboard: React.FC<DashboardProps> = ({ user, onLogout }) => {
  const [activeView, setActiveView] = useState<'dashboard' | 'capture' | 'relive' | 'admin' | 'upload'>('dashboard');
  const [isAdmin, setIsAdmin] = useState(false);
  const [showCategoriesModal, setShowCategoriesModal] = useState(false);
  const [showProfileModal, setShowProfileModal] = useState(false);
  const [userProfile, setUserProfile] = useState<any>(null);
  const [isDownloading, setIsDownloading] = useState(false);

  const [memories, setMemories] = useState<any[]>([]);
  const [stats, setStats] = useState({
    total: 0,
    favorites: 0,
    storage: '0 MB',
    thisWeek: 0,
    trend: '+0%',
    categories: 0
  });
  const [recentMemories, setRecentMemories] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedMemory, setSelectedMemory] = useState<any>(null);
  const [tagData, setTagData] = useState<Record<string, TagInfo>>({});
  const { toast } = useToast();


  useEffect(() => {
    if (user) {
      loadUserData();
    }
  }, [user]);

  const loadUserData = async () => {
    try {
      setIsLoading(true);
      
      // Fetch all tags to create name and color mapping (including inactive ones for historical memories)
      try {
        const { data: tagsData } = await supabase
          .from('tags')
          .select('id, name, color');
        
        if (tagsData) {
          const tagMapping: Record<string, TagInfo> = {};
          tagsData.forEach(tag => {
            // Use database color, fallback to emotion tag config color
            const color = tag.color || getEmotionTagColor(tag.name) || '#3B82F6';
            tagMapping[tag.id] = { id: tag.id, name: tag.name, color };
          });
          setTagData(tagMapping);
        }
      } catch (err) {
        console.warn('Could not fetch tags');
      }

      
      // Check admin status
      try {
        console.log('Checking admin status for user ID:', user.id);
        console.log('User email:', user.email);
        
        const { data: profileData, error: profileError } = await supabase
          .from('profiles')
          .select('is_admin')
          .eq('id', user.id)
          .maybeSingle();
        
        console.log('Profile data returned:', profileData);
        console.log('Profile error:', profileError);
        
        if (profileError) {
          console.warn('Error checking admin status:', profileError);
        }
        
        const adminStatus = profileData?.is_admin || false;
        setIsAdmin(adminStatus);
        console.log('Setting isAdmin to:', adminStatus);
        
        if (adminStatus) {
          console.log('USER IS ADMIN - Button should be visible!');
        }
      } catch (err) {
        console.warn('Could not check admin status:', err);
      }
      
      // Try to fetch from database, but gracefully handle if tables don't exist
      try {
        const { data: memoriesData, error: memoriesError } = await supabase
          .from('memories')
          .select('*')
          .eq('user_id', user.id)
          .order('created_at', { ascending: false });
        
        if (memoriesError) {
          console.warn('Database not set up yet, using local storage');
          loadFromLocalStorage();
          return;
        }
        
        // Process memories
        const processedMemories = memoriesData || [];
        setMemories(processedMemories);
        // Show only a limited glimpse of recent memories (max 4) to preserve surprise-based design
        setRecentMemories(processedMemories.slice(0, 4));
        calculateStats(processedMemories);
        
      } catch (dbError) {
        console.warn('Database error, using local storage:', dbError);
        loadFromLocalStorage();
      }
      
    } catch (error) {
      console.error('Error loading user data:', error);
      loadFromLocalStorage();
    } finally {
      setIsLoading(false);
    }
  };

  const loadFromLocalStorage = () => {
    const stored = localStorage.getItem(`memories_${user.id}`);
    const localMemories = stored ? JSON.parse(stored) : [];
    setMemories(localMemories);
    // Show only a limited glimpse of recent memories (max 4) to preserve surprise-based design
    setRecentMemories(localMemories.slice(0, 4));
    calculateStats(localMemories);
  };

  const calculateStats = (memoriesData: any[]) => {
    const total = memoriesData.length;
    const favorites = memoriesData.filter(m => m.is_favorite).length;
    
    const oneWeekAgo = new Date();
    oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
    const thisWeek = memoriesData.filter(m => 
      new Date(m.created_at) > oneWeekAgo
    ).length;
    
    const twoWeeksAgo = new Date();
    twoWeeksAgo.setDate(twoWeeksAgo.getDate() - 14);
    const lastWeek = memoriesData.filter(m => {
      const date = new Date(m.created_at);
      return date > twoWeeksAgo && date <= oneWeekAgo;
    }).length;
    
    const trend = lastWeek > 0 
      ? Math.round(((thisWeek - lastWeek) / lastWeek) * 100)
      : thisWeek > 0 ? 100 : 0;
    
    const uniqueTags = new Set();
    memoriesData.forEach(m => {
      if (m.tags && Array.isArray(m.tags)) {
        m.tags.forEach((tag: string) => uniqueTags.add(tag));
      }
    });
    
    const storageInMB = total * 2.5;
    const storageString = storageInMB > 1000 
      ? `${(storageInMB / 1000).toFixed(1)} GB`
      : `${storageInMB.toFixed(0)} MB`;
    
    setStats({
      total,
      favorites,
      storage: storageString,
      thisWeek,
      trend: trend >= 0 ? `+${trend}%` : `${trend}%`,
      categories: uniqueTags.size
    });
  };

  const handleMemorySave = async (memory: any) => {
    await loadUserData(); // Reload data after saving
    setActiveView('dashboard');
    toast({
      title: "Success!",
      description: "Your memory has been saved"
    });
  };

  const toggleFavorite = async (memoryId: string, currentFavoriteState: boolean, e: React.MouseEvent) => {
    e.stopPropagation();
    
    const newFavoriteState = !currentFavoriteState;
    
    try {
      const { error } = await supabase
        .from('memories')
        .update({ is_favorite: newFavoriteState })
        .eq('id', memoryId);
      
      if (error) {
        const stored = localStorage.getItem(`memories_${user.id}`);
        if (stored) {
          const localMemories = JSON.parse(stored);
          const updatedMemories = localMemories.map((m: any) => 
            m.id === memoryId ? { ...m, is_favorite: newFavoriteState } : m
          );
          localStorage.setItem(`memories_${user.id}`, JSON.stringify(updatedMemories));
        }
      }
      
      setMemories(prev => prev.map(m => 
        m.id === memoryId ? { ...m, is_favorite: newFavoriteState } : m
      ));
      setRecentMemories(prev => prev.map(m => 
        m.id === memoryId ? { ...m, is_favorite: newFavoriteState } : m
      ));
      
      const updatedMemories = memories.map(m => 
        m.id === memoryId ? { ...m, is_favorite: newFavoriteState } : m
      );
      calculateStats(updatedMemories);
      
      toast({
        title: newFavoriteState ? "Added to favorites" : "Removed from favorites",
        description: newFavoriteState ? "Memory saved to your favorites" : "Memory removed from favorites"
      });
      
    } catch (error) {
      console.error('Error toggling favorite:', error);
      toast({
        title: "Error",
        description: "Failed to update favorite status",
        variant: "destructive"
      });
    }
  };

  const handleDeleteMemory = async (memoryId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    
    if (!confirm('Are you sure you want to delete this memory? This action cannot be undone.')) {
      return;
    }
    
    try {
      const { error } = await supabase
        .from('memories')
        .delete()
        .eq('id', memoryId);
      
      if (error) {
        const stored = localStorage.getItem(`memories_${user.id}`);
        if (stored) {
          const localMemories = JSON.parse(stored);
          const updatedMemories = localMemories.filter((m: any) => m.id !== memoryId);
          localStorage.setItem(`memories_${user.id}`, JSON.stringify(updatedMemories));
        }
      }
      
      const updatedMemories = memories.filter(m => m.id !== memoryId);
      setMemories(updatedMemories);
      // Maintain limited glimpse - only show up to 4 recent memories
      setRecentMemories(updatedMemories.slice(0, 4));
      calculateStats(updatedMemories);
      
      toast({
        title: "Memory deleted",
        description: "Your memory has been permanently deleted"
      });
      
    } catch (error) {
      console.error('Error deleting memory:', error);
      toast({
        title: "Error",
        description: "Failed to delete memory",
        variant: "destructive"
      });
    }
  };

  // Download memory video - downloads the original file directly (no recompression, no watermark)
  const handleDownloadMemory = async (memory: any, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    if (!memory?.video_url) return;
    
    setIsDownloading(true);
    
    try {
      const response = await fetch(memory.video_url);
      if (!response.ok) throw new Error('Failed to fetch video');
      
      const blob = await response.blob();
      
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      
      // Generate filename based on memory date and tags
      const date = memory.created_at 
        ? new Date(memory.created_at).toISOString().split('T')[0]
        : new Date().toISOString().split('T')[0];
      const memoryTagNames = memory.tags
        ?.map((tagId: string) => tagData[tagId]?.name)
        .filter(Boolean)
        .slice(0, 2)
        .join('-') || 'memory';
      
      link.download = `momento-${memoryTagNames}-${date}.mp4`;
      
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      
      window.URL.revokeObjectURL(url);
      
      toast({ 
        title: "Memory saved", 
        description: "Your memory has been downloaded to your device" 
      });
    } catch (error) {
      console.error('Error downloading memory:', error);
      toast({
        title: "Download failed",
        description: "Unable to download this memory. Please try again.",
        variant: "destructive"
      });
    } finally {
      setIsDownloading(false);
    }
  };

  // Get tag display info with color
  const getTagDisplayInfo = (tagId: string): { name: string; color: string } => {
    const tag = tagData[tagId];
    if (tag) {
      return { name: tag.name, color: tag.color };
    }
    // Fallback for custom tags
    const emotionColor = getEmotionTagColor(tagId);
    return { name: tagId, color: emotionColor || '#3B82F6' };
  };

  const getUserCategories = () => {
    const categoryMap = new Map<string, number>();
    memories.forEach(m => {
      if (m.tags && Array.isArray(m.tags)) {
        m.tags.forEach((tagId: string) => {
          categoryMap.set(tagId, (categoryMap.get(tagId) || 0) + 1);
        });
      }
    });
    return Array.from(categoryMap.entries()).map(([tagId, count]) => {
      const { name, color } = getTagDisplayInfo(tagId);
      return { id: tagId, name, color, count };
    });
  };

  const handleLogout = async () => {
    await supabase.auth.signOut();
    onLogout();
  };

  // Render different views
  if (activeView === 'capture') {
    return <MemoryCapture onSave={handleMemorySave} onBack={() => setActiveView('dashboard')} user={user} />;
  }

  if (activeView === 'upload') {
    return <UploadMemory onSave={handleMemorySave} onBack={() => setActiveView('dashboard')} user={user} />;
  }

  if (activeView === 'relive') {
    return <MemoryPlayback memories={memories} onBack={() => setActiveView('dashboard')} />;
  }

  if (activeView === 'admin') {
    return <AdminPanel onBack={() => setActiveView('dashboard')} />;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-cream-50 to-white">
      {/* Header */}
      <header className="bg-white shadow-sm border-b border-gray-100">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <HourglassLogo className="w-8 h-8 text-coral-500" />
              <span className="hidden sm:inline text-2xl font-bold text-gray-900">Momentary Momentos</span>

            </div>
            
            <div className="flex items-center gap-4">
              <button 
                onClick={() => setShowProfileModal(true)}
                className="text-gray-600 hover:text-coral-500 transition-colors cursor-pointer"
              >
                Welcome, <span className="font-medium underline decoration-dotted">{user.email?.split('@')[0]}</span>
              </button>

              {isAdmin && (
                <button 
                  onClick={() => setActiveView('admin')}
                  className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
                  title="Admin Panel"
                >
                  <Shield className="w-5 h-5" />
                  <span className="font-medium">Admin</span>
                </button>
              )}

              <button 
                onClick={handleLogout}
                className="p-2 rounded-lg hover:bg-gray-100 transition-colors"
              >
                <LogOut className="w-5 h-5 text-gray-600" />
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {isLoading ? (
          <div className="flex justify-center items-center h-64">
            <Loader className="w-8 h-8 text-coral-500 animate-spin" />
          </div>
        ) : (
          <>
            {/* Stats Cards */}
            <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-8">
              <div className="bg-gradient-to-br from-coral-50 to-coral-100 rounded-2xl p-6 shadow-md">
                <div className="flex items-center justify-between mb-2">
                  <Film className="w-8 h-8 text-coral-500" />
                  <span className={`text-xs font-semibold ${
                    stats.trend.startsWith('+') ? 'text-green-500' : 'text-red-500'
                  }`}>{stats.trend}</span>
                </div>
                <div className="text-2xl font-bold text-gray-900">{stats.total}</div>
                <div className="text-sm text-gray-600">Total Memories</div>
              </div>
              
              <div className="bg-gradient-to-br from-pink-50 to-pink-100 rounded-2xl p-6 shadow-md">
                <Heart className="w-8 h-8 text-pink-500 mb-2" />
                <div className="text-2xl font-bold text-gray-900">{stats.favorites}</div>
                <div className="text-sm text-gray-600">Favorites</div>
              </div>
              
              <div className="bg-gradient-to-br from-blue-50 to-blue-100 rounded-2xl p-6 shadow-md">
                <HardDrive className="w-8 h-8 text-blue-500 mb-2" />
                <div className="text-2xl font-bold text-gray-900">{stats.storage}</div>
                <div className="text-sm text-gray-600">Storage Used</div>
              </div>
              
              <div className="bg-gradient-to-br from-green-50 to-green-100 rounded-2xl p-6 shadow-md">
                <TrendingUp className="w-8 h-8 text-green-500 mb-2" />
                <div className="text-2xl font-bold text-gray-900">{stats.thisWeek}</div>
                <div className="text-sm text-gray-600">This Week</div>
              </div>
              
              <button 
                onClick={() => setShowCategoriesModal(true)}
                className="bg-gradient-to-br from-purple-50 to-purple-100 rounded-2xl p-6 shadow-md hover:shadow-lg transition-shadow cursor-pointer"
              >
                <Grid className="w-8 h-8 text-purple-500 mb-2" />
                <div className="text-2xl font-bold text-gray-900">{stats.categories}</div>
                <div className="text-sm text-gray-600">Categories</div>
              </button>
            </div>

            {/* Action Cards */}
            <div className="grid md:grid-cols-3 gap-6 mb-8">
              <button
                onClick={() => setActiveView('capture')}
                className="group bg-gradient-to-br from-coral-500 to-coral-600 rounded-3xl p-8 shadow-xl hover:shadow-2xl transition-all duration-300 transform hover:scale-105"
              >
                <div className="flex flex-col items-center text-white">
                  <div className="w-20 h-20 bg-white/20 backdrop-blur rounded-full flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                    <Camera className="w-10 h-10" />
                  </div>
                  <h3 className="text-2xl font-bold mb-2">Capture Memory</h3>
                  <p className="text-white/90 text-center">
                    Record a new 10-second moment
                  </p>
                </div>
              </button>

              <button
                onClick={() => setActiveView('upload')}
                className="group bg-gradient-to-br from-teal-500 to-teal-600 rounded-3xl p-8 shadow-xl hover:shadow-2xl transition-all duration-300 transform hover:scale-105"
              >
                <div className="flex flex-col items-center text-white">
                  <div className="w-20 h-20 bg-white/20 backdrop-blur rounded-full flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                    <Upload className="w-10 h-10" />
                  </div>
                  <h3 className="text-2xl font-bold mb-2">Upload Memory</h3>
                  <p className="text-white/90 text-center">
                    Add a past video from your device
                  </p>
                </div>
              </button>

              <button
                onClick={() => setActiveView('relive')}
                className="group bg-gradient-to-br from-purple-500 to-purple-600 rounded-3xl p-8 shadow-xl hover:shadow-2xl transition-all duration-300 transform hover:scale-105"
                disabled={memories.length === 0}
              >
                <div className="flex flex-col items-center text-white">
                  <div className="w-20 h-20 bg-white/20 backdrop-blur rounded-full flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                    <Play className="w-10 h-10" />
                  </div>
                  <h3 className="text-2xl font-bold mb-2">Relive Memory</h3>
                  <p className="text-white/90 text-center">
                    {memories.length === 0 
                      ? "Capture your first memory to start"
                      : "Surprise yourself with a memory"
                    }
                  </p>
                </div>
              </button>
            </div>

            {/* Recent Memories Preview - Limited glimpse only, no "View All" to preserve surprise-based design */}
            {recentMemories.length > 0 && (
              <div className="mt-8">
                <div className="flex items-center justify-between mb-6">
                  <h2 className="text-2xl font-bold text-gray-900">Recent Memories</h2>
                  <p className="text-sm text-gray-500 italic">
                    Relive to rediscover your moments
                  </p>
                </div>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  {recentMemories.map((memory) => {
                    const firstTagInfo = memory.tags && memory.tags[0] ? getTagDisplayInfo(memory.tags[0]) : null;
                    return (
                      <button
                        key={memory.id}
                        onClick={() => setSelectedMemory(memory)}
                        className="relative group cursor-pointer"
                      >
                        <div className="bg-gray-200 rounded-xl aspect-square overflow-hidden">
                          <video
                            src={memory.video_url}
                            className="w-full h-full object-cover"
                            muted
                            playsInline
                          />
                        </div>
                        <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity rounded-xl flex items-center justify-center">
                          <Play className="w-12 h-12 text-white" />
                        </div>
                        
                        <button
                          onClick={(e) => toggleFavorite(memory.id, memory.is_favorite, e)}
                          className="absolute top-2 right-2 p-2 bg-black/50 backdrop-blur rounded-full hover:bg-black/70 transition-all z-10"
                        >
                          <Heart 
                            className={`w-5 h-5 ${
                              memory.is_favorite 
                                ? 'fill-pink-500 text-pink-500' 
                                : 'text-white'
                            }`}
                          />
                        </button>
                        
                        {firstTagInfo && (
                          <div 
                            className="absolute bottom-2 left-2 px-2 py-1 backdrop-blur rounded text-xs font-medium"
                            style={{ 
                              backgroundColor: firstTagInfo.color,
                              color: getContrastTextColor(firstTagInfo.color)
                            }}
                          >
                            {firstTagInfo.name}
                          </div>
                        )}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Empty State */}
            {memories.length === 0 && (
              <div className="mt-12 text-center py-12">
                <Film className="w-16 h-16 text-gray-300 mx-auto mb-4" />
                <h3 className="text-xl font-semibold text-gray-600 mb-2">No memories yet</h3>
                <p className="text-gray-500">Start capturing or upload your first memory!</p>
              </div>
            )}
          </>
        )}
      </div>

      {/* Video Playback Modal */}
      {selectedMemory && (
        <div 
          className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4"
          onClick={() => setSelectedMemory(null)}
        >
          <div 
            className="relative max-w-2xl w-full bg-black rounded-2xl overflow-hidden"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Close button */}
            <button
              onClick={() => setSelectedMemory(null)}
              className="absolute top-4 right-4 z-10 p-2 bg-black/50 backdrop-blur rounded-full hover:bg-black/70 transition-colors"
            >
              <X className="w-6 h-6 text-white" />
            </button>
            
            {/* Download button - The sole outward-facing option */}
            <button
              onClick={(e) => handleDownloadMemory(selectedMemory, e)}
              disabled={isDownloading}
              className="absolute top-4 left-4 z-10 p-2 bg-emerald-500 backdrop-blur rounded-full hover:bg-emerald-600 transition-colors disabled:opacity-50"
              title="Download memory to your device"
            >
              {isDownloading ? (
                <Loader className="w-6 h-6 text-white animate-spin" />
              ) : (
                <Download className="w-6 h-6 text-white" />
              )}
            </button>
            
            <video
              src={selectedMemory.video_url}
              className="w-full"
              controls
              autoPlay
              playsInline
            />
            
            {/* Tags and Download bar */}
            <div className="p-4 bg-gray-900">
              <div className="flex items-center justify-between gap-4">
                <div className="flex flex-wrap gap-2 flex-1">
                  {selectedMemory.tags && selectedMemory.tags.map((tagId: string, index: number) => {
                    const { name, color } = getTagDisplayInfo(tagId);
                    return (
                      <span
                        key={index}
                        className="px-3 py-1 rounded-full text-sm font-medium"
                        style={{ 
                          backgroundColor: color,
                          color: getContrastTextColor(color)
                        }}
                      >
                        {name}
                      </span>
                    );
                  })}
                </div>
                
                {/* Download button in footer - The sole outward-facing option */}
                <button
                  onClick={(e) => handleDownloadMemory(selectedMemory, e)}
                  disabled={isDownloading}
                  className="flex items-center gap-2 px-4 py-2 bg-emerald-500 text-white rounded-lg hover:bg-emerald-600 transition-colors text-sm font-medium whitespace-nowrap disabled:opacity-50"
                >
                  {isDownloading ? (
                    <Loader className="w-4 h-4 animate-spin" />
                  ) : (
                    <Download className="w-4 h-4" />
                  )}
                  <span>Download</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Categories Modal */}
      {showCategoriesModal && (
        <div 
          className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4"
          onClick={() => setShowCategoriesModal(false)}
        >
          <div 
            className="relative max-w-2xl w-full bg-white rounded-2xl overflow-hidden"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 border-b border-gray-200">
              <div className="flex items-center justify-between">
                <h2 className="text-2xl font-bold text-gray-900">Your Categories</h2>
                <button
                  onClick={() => setShowCategoriesModal(false)}
                  className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                >
                  <X className="w-6 h-6 text-gray-600" />
                </button>
              </div>
            </div>
            
            <div className="p-6 max-h-96 overflow-y-auto">
              {getUserCategories().length === 0 ? (
                <div className="text-center py-8">
                  <Grid className="w-12 h-12 text-gray-300 mx-auto mb-3" />
                  <p className="text-gray-500">No categories yet. Start capturing memories with tags!</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {getUserCategories().map((category) => (
                    <div
                      key={category.id}
                      className="flex items-center justify-between p-4 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <div 
                          className="w-10 h-10 rounded-full flex items-center justify-center"
                          style={{ backgroundColor: category.color }}
                        >
                          <Grid 
                            className="w-5 h-5" 
                            style={{ color: getContrastTextColor(category.color) }}
                          />
                        </div>
                        <span className="font-medium text-gray-900">{category.name}</span>
                      </div>
                      <span 
                        className="px-3 py-1 rounded-full text-sm font-semibold"
                        style={{ 
                          backgroundColor: category.color,
                          color: getContrastTextColor(category.color)
                        }}
                      >
                        {category.count} {category.count === 1 ? 'memory' : 'memories'}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Profile Edit Modal */}
      {showProfileModal && (
        <ProfileEditModal
          user={user}
          onClose={() => setShowProfileModal(false)}
          onUpdate={loadUserData}
        />
      )}
    </div>
  );
};

export default Dashboard;

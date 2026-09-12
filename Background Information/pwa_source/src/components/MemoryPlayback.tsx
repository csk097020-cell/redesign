import React, { useState, useEffect, useRef, useMemo } from 'react';
import { X, Heart, Trash2, ThumbsDown, Tag, Download, Shuffle, Play, Pause, Loader } from 'lucide-react';
import { supabase } from '@/lib/supabase';
import { useToast } from '@/hooks/use-toast';
import TagSelector from './TagSelector';

interface MemoryPlaybackProps {
  memories: any[];
  onBack: () => void;
}

interface TagData {
  name: string;
  color: string;
}

const MemoryPlayback: React.FC<MemoryPlaybackProps> = ({ memories, onBack }) => {
  const [currentMemory, setCurrentMemory] = useState<any>(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [showPreview, setShowPreview] = useState(true);
  const [memoryWeights, setMemoryWeights] = useState<{[key: string]: number}>({});
  const [isLoading, setIsLoading] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const [touchStart, setTouchStart] = useState<number | null>(null);
  const [touchEnd, setTouchEnd] = useState<number | null>(null);
  const [tagData, setTagData] = useState<{[key: string]: TagData}>({});
  const [showRetagModal, setShowRetagModal] = useState(false);
  const videoRef = useRef<HTMLVideoElement>(null);

  const { toast } = useToast();

  // Minimum swipe distance (in px)
  const minSwipeDistance = 50;

  useEffect(() => {
    if (memories.length > 0) {
      fetchTagData();
      loadMemoryWeights();
      selectRandomMemory();
    }
  }, [memories]);

  const fetchTagData = async () => {
    try {
      const { data: { user } } = await supabase.auth.getUser();
      
      // Fetch all tags (default and user-specific) with colors
      const { data, error } = await supabase
        .from('tags')
        .select('id, name, color')
        .or(`user_id.is.null${user ? `,user_id.eq.${user.id}` : ''}`);

      if (error) throw error;
      
      // Create lookup map for tag ID -> tag data (name and color)
      const tagMap: {[key: string]: TagData} = {};
      data?.forEach(tag => {
        tagMap[tag.id] = {
          name: tag.name,
          color: tag.color || '#9333ea' // Default purple if no color
        };
      });
      setTagData(tagMap);
    } catch (error) {
      console.error('Error fetching tag data:', error);
    }
  };

  // Helper function to convert hex to RGB
  const hexToRgb = (hex: string): { r: number; g: number; b: number } | null => {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? {
      r: parseInt(result[1], 16),
      g: parseInt(result[2], 16),
      b: parseInt(result[3], 16)
    } : null;
  };

  // Helper function to lighten a color
  const lightenColor = (hex: string, percent: number): string => {
    const rgb = hexToRgb(hex);
    if (!rgb) return hex;
    
    const lighten = (value: number) => Math.min(255, Math.floor(value + (255 - value) * percent));
    return `rgb(${lighten(rgb.r)}, ${lighten(rgb.g)}, ${lighten(rgb.b)})`;
  };

  // Helper function to darken a color
  const darkenColor = (hex: string, percent: number): string => {
    const rgb = hexToRgb(hex);
    if (!rgb) return hex;
    
    const darken = (value: number) => Math.max(0, Math.floor(value * (1 - percent)));
    return `rgb(${darken(rgb.r)}, ${darken(rgb.g)}, ${darken(rgb.b)})`;
  };

  // Generate dynamic gradient based on memory tags
  const generateTagGradient = useMemo(() => {
    if (!currentMemory?.tags || currentMemory.tags.length === 0) {
      // Default gradient if no tags
      return 'linear-gradient(135deg, rgba(147, 51, 234, 0.9) 0%, rgba(236, 72, 153, 0.9) 100%)';
    }

    const tagColors = currentMemory.tags
      .map((tagId: string) => tagData[tagId]?.color)
      .filter((color: string | undefined): color is string => !!color);

    if (tagColors.length === 0) {
      // Default gradient if no valid colors found
      return 'linear-gradient(135deg, rgba(147, 51, 234, 0.9) 0%, rgba(236, 72, 153, 0.9) 100%)';
    }

    if (tagColors.length === 1) {
      // Single tag: create a soft gradient from lighter to darker shade
      const baseColor = tagColors[0];
      const lightColor = lightenColor(baseColor, 0.3);
      const darkColor = darkenColor(baseColor, 0.2);
      return `linear-gradient(135deg, ${lightColor} 0%, ${baseColor} 50%, ${darkColor} 100%)`;
    }

    // Multiple tags: blend all colors smoothly
    const gradientStops = tagColors.map((color: string, index: number) => {
      const position = (index / (tagColors.length - 1)) * 100;
      const rgb = hexToRgb(color);
      if (rgb) {
        return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, 0.85) ${position}%`;
      }
      return `${color} ${position}%`;
    });

    return `linear-gradient(135deg, ${gradientStops.join(', ')})`;
  }, [currentMemory?.tags, tagData]);

  // Format date for display - returns both relative and absolute formats
  const formatRecordedDate = (dateString: string): { relative: string; absolute: string } => {
    const date = new Date(dateString);
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - date.getTime());
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    // Absolute format: "March 12, 2024"
    const absoluteDate = date.toLocaleDateString('en-US', {
      month: 'long',
      day: 'numeric',
      year: 'numeric'
    });
    
    // Relative format
    let relativeDate: string;
    if (diffDays === 0) {
      relativeDate = 'Today';
    } else if (diffDays === 1) {
      relativeDate = 'Yesterday';
    } else if (diffDays < 7) {
      relativeDate = `${diffDays} days ago`;
    } else if (diffDays < 30) {
      const weeks = Math.floor(diffDays / 7);
      relativeDate = `${weeks} week${weeks > 1 ? 's' : ''} ago`;
    } else if (diffDays < 365) {
      const months = Math.floor(diffDays / 30);
      relativeDate = `${months} month${months > 1 ? 's' : ''} ago`;
    } else {
      const years = Math.floor(diffDays / 365);
      relativeDate = `${years} year${years > 1 ? 's' : ''} ago`;
    }
    
    return { relative: relativeDate, absolute: absoluteDate };
  };

  const loadMemoryWeights = async () => {
    // Load weights from memory_metrics table
    const weights: {[key: string]: number} = {};
    memories.forEach(memory => {
      const metrics = memory.memory_metrics?.[0];
      if (metrics) {
        weights[memory.id] = metrics.weight || 1;
      } else {
        weights[memory.id] = 1;
      }
    });
    setMemoryWeights(weights);
  };

  const selectRandomMemory = () => {
    if (memories.length === 0) return;
    
    const weightedMemories = memories.flatMap(m => 
      Array(Math.max(1, memoryWeights[m.id] || 1)).fill(m)
    );
    const randomIndex = Math.floor(Math.random() * weightedMemories.length);
    setCurrentMemory(weightedMemories[randomIndex]);
    setShowPreview(true);
    setIsPlaying(false);
  };

  const goToNextMemory = () => {
    if (memories.length === 0) return;
    const nextIndex = (currentIndex + 1) % memories.length;
    setCurrentIndex(nextIndex);
    setCurrentMemory(memories[nextIndex]);
    setShowPreview(true);
    setIsPlaying(false);
  };

  const goToPreviousMemory = () => {
    if (memories.length === 0) return;
    const prevIndex = currentIndex === 0 ? memories.length - 1 : currentIndex - 1;
    setCurrentIndex(prevIndex);
    setCurrentMemory(memories[prevIndex]);
    setShowPreview(true);
    setIsPlaying(false);
  };

  const onTouchStart = (e: React.TouchEvent) => {
    setTouchEnd(null);
    setTouchStart(e.targetTouches[0].clientX);
  };

  const onTouchMove = (e: React.TouchEvent) => {
    setTouchEnd(e.targetTouches[0].clientX);
  };

  const onTouchEnd = () => {
    if (!touchStart || !touchEnd) return;
    
    const distance = touchStart - touchEnd;
    const isLeftSwipe = distance > minSwipeDistance;
    const isRightSwipe = distance < -minSwipeDistance;
    
    if (isLeftSwipe) {
      goToNextMemory();
    }
    if (isRightSwipe) {
      goToPreviousMemory();
    }
  };

  // Download memory video - downloads the original file directly (no recompression, no watermark)
  const handleDownloadMemory = async () => {
    if (!currentMemory?.video_url) return;
    
    setIsDownloading(true);
    
    try {
      // Fetch the video file
      const response = await fetch(currentMemory.video_url);
      if (!response.ok) throw new Error('Failed to fetch video');
      
      const blob = await response.blob();
      
      // Create a download link
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      
      // Generate filename based on memory date and tags
      const date = currentMemory.created_at 
        ? new Date(currentMemory.created_at).toISOString().split('T')[0]
        : new Date().toISOString().split('T')[0];
      const memoryTagNames = currentMemory.tags
        ?.map((tagId: string) => tagData[tagId]?.name)
        .filter(Boolean)
        .slice(0, 2)
        .join('-') || 'memory';
      
      link.download = `momento-${memoryTagNames}-${date}.mp4`;
      
      // Trigger download
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      
      // Clean up the blob URL
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

  const handleAction = async (action: string) => {
    if (!currentMemory) return;
    
    setIsLoading(true);
    
    try {
      const { data: { user } } = await supabase.auth.getUser();
      if (!user) throw new Error('User not authenticated');
      
      switch(action) {
        case 'favorite':
          // Update weight and favorite count
          await supabase
            .from('memory_metrics')
            .upsert({
              memory_id: currentMemory.id,
              user_id: user.id,
              weight: (memoryWeights[currentMemory.id] || 1) + 2,
              favorite_count: (currentMemory.memory_metrics?.[0]?.favorite_count || 0) + 1
            });
          setMemoryWeights(prev => ({ ...prev, [currentMemory.id]: (prev[currentMemory.id] || 1) + 2 }));
          toast({ title: "Added to favorites!", description: "You'll see this memory more often" });
          break;
          
        case 'delete':
          // Delete the memory
          await supabase
            .from('memories')
            .delete()
            .eq('id', currentMemory.id);
          
          // Remove from local memories array
          const index = memories.findIndex(m => m.id === currentMemory.id);
          if (index > -1) {
            memories.splice(index, 1);
          }
          
          toast({ title: "Memory deleted", description: "This memory has been removed" });
          break;
          
        case 'seeLess':
          // Decrease weight
          const newWeight = Math.max(0, (memoryWeights[currentMemory.id] || 1) - 1);
          await supabase
            .from('memory_metrics')
            .upsert({
              memory_id: currentMemory.id,
              user_id: user.id,
              weight: newWeight,
              skip_count: (currentMemory.memory_metrics?.[0]?.skip_count || 0) + 1
            });
          setMemoryWeights(prev => ({ ...prev, [currentMemory.id]: newWeight }));
          toast({ title: "Got it!", description: "You'll see this memory less often" });
          break;

        case 'retag':
          // Open tag selector modal
          setIsLoading(false);
          setShowRetagModal(true);
          return; // Don't load next memory yet
      }
      
      // Load next memory after a short delay
      setTimeout(() => selectRandomMemory(), 500);
      
    } catch (error) {
      console.error('Error handling action:', error);
      toast({
        title: "Error",
        description: "Failed to update memory",
        variant: "destructive"
      });
    } finally {
      setIsLoading(false);
    }
  };

  const startPlayback = () => {
    setShowPreview(false);
    setIsPlaying(true);
    if (videoRef.current) {
      videoRef.current.play();
    }
  };

  const togglePlayback = () => {
    if (videoRef.current) {
      if (isPlaying) {
        videoRef.current.pause();
      } else {
        videoRef.current.play();
      }
      setIsPlaying(!isPlaying);
    }
  };

  const handleRetagSave = async (newTags: string[]) => {
    if (!currentMemory) return;
    
    try {
      const { error } = await supabase
        .from('memories')
        .update({ tags: newTags })
        .eq('id', currentMemory.id);
      
      if (error) throw error;
      
      // Update local memory
      currentMemory.tags = newTags;
      
      toast({ 
        title: "Tags updated!", 
        description: "Memory has been retagged successfully" 
      });
      
      setShowRetagModal(false);
      setTimeout(() => selectRandomMemory(), 500);
    } catch (error) {
      console.error('Error updating tags:', error);
      toast({
        title: "Error",
        description: "Failed to update tags",
        variant: "destructive"
      });
    }
  };

  if (memories.length === 0) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-blue-50 to-purple-50 flex items-center justify-center p-4">
        <div className="text-center">
          <p className="text-gray-800 text-xl mb-4">No memories to relive yet</p>
          <button
            onClick={onBack}
            className="px-6 py-3 bg-coral-500 text-white rounded-full font-semibold hover:bg-coral-600 transition-colors"
          >
            Go Back
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-purple-50 flex items-center justify-center p-4">
      <div className="w-full max-w-2xl">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <button
            onClick={onBack}
            className="p-3 bg-gray-800/80 backdrop-blur rounded-full hover:bg-gray-900 transition-colors"
          >
            <X className="w-6 h-6 text-white" />
          </button>
          
          <h1 className="text-2xl font-bold text-gray-800">Relive Memory</h1>
          
          <button
            onClick={selectRandomMemory}
            className="p-3 bg-gray-800/80 backdrop-blur rounded-full hover:bg-gray-900 transition-colors"
          >
            <Shuffle className="w-6 h-6 text-white" />
          </button>
        </div>

        {/* Memory Player */}
        <div 
          className="relative bg-black rounded-3xl overflow-hidden aspect-[9/16] max-h-[60vh] mx-auto"
          onTouchStart={onTouchStart}
          onTouchMove={onTouchMove}
          onTouchEnd={onTouchEnd}
        >
          {showPreview && currentMemory ? (
            <>
              {/* Preview Overlay with Dynamic Tag-Based Gradient */}
              <div 
                className="absolute inset-0 flex items-center justify-center transition-all duration-500"
                style={{ background: generateTagGradient }}
              >
                {/* Subtle overlay for text legibility */}
                <div className="absolute inset-0 bg-black/20" />
                
                <div className="relative text-center text-white p-8 z-10">
                  <p className="text-lg mb-2 opacity-90 font-medium">You're about to relive:</p>
                  
                  {/* Tag Pills */}
                  <div className="flex flex-wrap justify-center gap-2 mb-6">
                    {currentMemory.tags?.map((tagId: string, i: number) => {
                      const tag = tagData[tagId];
                      return (
                        <span 
                          key={i} 
                          className="px-3 py-1 bg-white/25 backdrop-blur-sm rounded-full text-sm font-medium shadow-sm"
                          style={{
                            borderColor: tag?.color ? `${tag.color}80` : 'rgba(255,255,255,0.3)',
                            borderWidth: '1px'
                          }}
                        >
                          {tag?.name || tagId}
                        </span>
                      );
                    })}
                  </div>
                  
                  {/* Recorded Date - Enhanced Display */}
                  {currentMemory.created_at && (
                    <div className="mb-8">
                      <p className="text-base opacity-90 font-medium">
                        Recorded {formatRecordedDate(currentMemory.created_at).absolute}
                      </p>
                      <p className="text-sm opacity-70 mt-1">
                        ({formatRecordedDate(currentMemory.created_at).relative})
                      </p>
                    </div>
                  )}
                  
                  {/* Play Button */}
                  <button
                    onClick={startPlayback}
                    className="w-20 h-20 bg-white/25 backdrop-blur-sm rounded-full flex items-center justify-center mx-auto hover:bg-white/35 transition-all duration-300 shadow-lg hover:scale-105 active:scale-95"
                  >
                    <Play className="w-10 h-10 text-white ml-1 drop-shadow-md" />
                  </button>
                </div>
              </div>
            </>
          ) : currentMemory ? (
            <>
              {/* Video Playback */}
              <video
                ref={videoRef}
                src={currentMemory.video_url}
                autoPlay
                loop
                playsInline
                className="absolute inset-0 w-full h-full object-cover"
                onEnded={() => setIsPlaying(false)}
              />
              
              {/* Playback Controls */}
              <div className="absolute bottom-0 left-0 right-0 p-4">
                <button
                  onClick={togglePlayback}
                  className="absolute bottom-4 right-4 p-3 bg-white/20 backdrop-blur rounded-full hover:bg-white/30 transition-colors"
                >
                  {isPlaying ? (
                    <Pause className="w-6 h-6 text-white" />
                  ) : (
                    <Play className="w-6 h-6 text-white" />
                  )}
                </button>
              </div>
            </>
          ) : (
            <div className="absolute inset-0 flex items-center justify-center">
              <Loader className="w-8 h-8 text-white animate-spin" />
            </div>
          )}
        </div>

        {/* Action Buttons - Download is the sole outward-facing option */}
        {!showPreview && currentMemory && (
          <div className="flex justify-center gap-3 mt-8">
            <button
              onClick={() => handleAction('favorite')}
              disabled={isLoading}
              className="p-4 bg-pink-500/20 backdrop-blur rounded-full hover:bg-pink-500/30 transition-colors group disabled:opacity-50"
              title="Favorite (+2 weight)"
            >
              <Heart className="w-6 h-6 text-pink-400 group-hover:scale-110 transition-transform" />
            </button>
            
            <button
              onClick={() => handleAction('delete')}
              disabled={isLoading}
              className="p-4 bg-red-500/20 backdrop-blur rounded-full hover:bg-red-500/30 transition-colors group disabled:opacity-50"
              title="Delete"
            >
              <Trash2 className="w-6 h-6 text-red-400 group-hover:scale-110 transition-transform" />
            </button>
            
            <button
              onClick={() => handleAction('seeLess')}
              disabled={isLoading}
              className="p-4 bg-orange-500/20 backdrop-blur rounded-full hover:bg-orange-500/30 transition-colors group disabled:opacity-50"
              title="See Less (-1 weight)"
            >
              <ThumbsDown className="w-6 h-6 text-orange-400 group-hover:scale-110 transition-transform" />
            </button>
            
            <button
              onClick={() => handleAction('retag')}
              disabled={isLoading}
              className="p-4 bg-blue-500/20 backdrop-blur rounded-full hover:bg-blue-500/30 transition-colors group disabled:opacity-50"
              title="Retag"
            >
              <Tag className="w-6 h-6 text-blue-400 group-hover:scale-110 transition-transform" />
            </button>
            
            {/* Download Memory - The sole outward-facing option for saving/sharing */}
            <button
              onClick={handleDownloadMemory}
              disabled={isDownloading}
              className="p-4 bg-emerald-500/20 backdrop-blur rounded-full hover:bg-emerald-500/30 transition-colors group disabled:opacity-50"
              title="Download Memory - Save to your device"
            >
              {isDownloading ? (
                <Loader className="w-6 h-6 text-emerald-400 animate-spin" />
              ) : (
                <Download className="w-6 h-6 text-emerald-400 group-hover:scale-110 transition-transform" />
              )}
            </button>
          </div>
        )}

        {/* Action Labels */}
        {!showPreview && currentMemory && (
          <div className="flex justify-center gap-3 mt-4 text-xs text-gray-600">
            <span className="w-14 text-center">Favorite</span>
            <span className="w-14 text-center">Delete</span>
            <span className="w-14 text-center">See Less</span>
            <span className="w-14 text-center">Retag</span>
            <span className="w-14 text-center">Download</span>
          </div>
        )}
      </div>

      {/* Retag Modal */}
      {showRetagModal && currentMemory && (
        <div className="fixed inset-0 z-50">
          <TagSelector
            onSelect={handleRetagSave}
            onBack={() => setShowRetagModal(false)}
            buttonText="Done"
            initialTags={currentMemory.tags || []}
          />
        </div>
      )}
    </div>
  );
};

export default MemoryPlayback;

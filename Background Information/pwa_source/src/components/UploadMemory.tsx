import React, { useState, useRef, useEffect } from 'react';
import { Upload, X, Check, RotateCcw, Loader, AlertCircle, Play, Pause } from 'lucide-react';
import TagSelector from './TagSelector';
import VideoTrimmer from './VideoTrimmer';
import { supabase } from '@/lib/supabase';
import { useToast } from '@/hooks/use-toast';
import { formatFileSize } from '@/utils/videoHelpers';

interface UploadMemoryProps {
  onSave: (memory: any) => void;
  onBack: () => void;
  user: any;
}

const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
const MIN_DURATION = 10; // Minimum 10 seconds required
const CLIP_DURATION = 10; // 10 seconds

const UploadMemory: React.FC<UploadMemoryProps> = ({ onSave, onBack, user }) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [videoUrl, setVideoUrl] = useState<string | null>(null);
  const [videoDuration, setVideoDuration] = useState<number>(0);
  const [trimmedBlob, setTrimmedBlob] = useState<Blob | null>(null);
  const [trimmedUrl, setTrimmedUrl] = useState<string | null>(null);
  const [showTagSelector, setShowTagSelector] = useState(false);
  const [showTrimmer, setShowTrimmer] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [durationLoaded, setDurationLoaded] = useState(false);
  
  const fileInputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const previewVideoRef = useRef<HTMLVideoElement>(null);
  
  const { toast } = useToast();

  // Cleanup URLs on unmount
  useEffect(() => {
    return () => {
      if (videoUrl) URL.revokeObjectURL(videoUrl);
      if (trimmedUrl) URL.revokeObjectURL(trimmedUrl);
    };
  }, []);

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Reset state
    setError(null);
    setTrimmedBlob(null);
    if (trimmedUrl) URL.revokeObjectURL(trimmedUrl);
    setTrimmedUrl(null);
    setShowPreview(false);
    setShowTrimmer(false);
    setDurationLoaded(false);
    setVideoDuration(0);

    // Validate file type
    if (!file.type.startsWith('video/')) {
      setError('Please select a video file');
      return;
    }

    // Validate file size
    if (file.size > MAX_FILE_SIZE) {
      setError(`File size must be less than ${formatFileSize(MAX_FILE_SIZE)}`);
      return;
    }

    // Cleanup previous URL
    if (videoUrl) URL.revokeObjectURL(videoUrl);

    const url = URL.createObjectURL(file);
    setSelectedFile(file);
    setVideoUrl(url);
  };

  const handleVideoLoaded = () => {
    if (videoRef.current) {
      const duration = videoRef.current.duration;
      setVideoDuration(duration);
      setDurationLoaded(true);
      
      // Check if video is too short
      if (duration < MIN_DURATION) {
        setError(`Video must be at least ${MIN_DURATION} seconds long. Your video is ${duration.toFixed(1)} seconds.`);
        toast({
          title: "Video Too Short",
          description: `Please select a video that is at least ${MIN_DURATION} seconds long.`,
          variant: "destructive"
        });
      } else if (duration <= CLIP_DURATION) {
        // Video is exactly 10 seconds or slightly more - can use as-is
        toast({
          title: "Video Ready",
          description: `Your ${duration.toFixed(1)}s video is ready to save`
        });
      } else {
        // Video needs trimming
        toast({
          title: "Select Your Clip",
          description: `Choose which ${CLIP_DURATION} seconds to keep from your ${duration.toFixed(1)}s video`
        });
      }
    }
  };

  const handleContinue = () => {
    if (!selectedFile || !videoRef.current) return;
    
    const duration = videoRef.current.duration;
    
    // Check minimum duration
    if (duration < MIN_DURATION) {
      setError(`Video must be at least ${MIN_DURATION} seconds long.`);
      return;
    }
    
    // If video is short enough, skip trimming
    if (duration <= CLIP_DURATION) {
      setTrimmedBlob(selectedFile);
      const url = URL.createObjectURL(selectedFile);
      setTrimmedUrl(url);
      setShowPreview(true);
    } else {
      // Show trimmer for longer videos
      setShowTrimmer(true);
    }
  };

  const handleTrimComplete = (blob: Blob) => {
    setTrimmedBlob(blob);
    const url = URL.createObjectURL(blob);
    if (trimmedUrl) URL.revokeObjectURL(trimmedUrl);
    setTrimmedUrl(url);
    setShowTrimmer(false);
    setShowPreview(true);
    
    toast({
      title: "Video Trimmed!",
      description: "Your 10-second clip is ready to save"
    });
  };

  const confirmVideo = () => {
    setShowTagSelector(true);
  };

  const handleTagsSelected = async (tags: string[]) => {
    if (!trimmedBlob || !user) return;
    
    setIsLoading(true);
    
    try {
      // Generate unique filename
      const timestamp = Date.now();
      const extension = trimmedBlob.type.includes('mp4') ? 'mp4' : 'webm';
      const videoFileName = `${user.id}/${timestamp}.${extension}`;
      
      // Upload video to Supabase storage
      const { data: videoData, error: videoError } = await supabase.storage
        .from('user-videos')
        .upload(videoFileName, trimmedBlob, {
          contentType: trimmedBlob.type,
          upsert: false
        });

      if (videoError) throw videoError;

      // Get public URL for the video
      const { data: { publicUrl } } = supabase.storage
        .from('user-videos')
        .getPublicUrl(videoFileName);

      // Save memory to database
      const { data: memoryData, error: memoryError } = await supabase
        .from('memories')
        .insert({
          user_id: user.id,
          title: tags.length > 0 ? `${tags[0]} Memory` : 'Uploaded Memory',
          video_url: publicUrl,
          tags: tags,
          is_favorite: false
        })
        .select()
        .single();

      if (memoryError) throw memoryError;

      toast({
        title: "Memory Uploaded!",
        description: "Your past memory has been saved successfully"
      });
      
      // Cleanup
      if (videoUrl) URL.revokeObjectURL(videoUrl);
      if (trimmedUrl) URL.revokeObjectURL(trimmedUrl);
      
      onSave(memoryData);
      
    } catch (error: any) {
      console.error('Error saving memory:', error);
      toast({
        title: "Upload Failed",
        description: error.message || "Failed to upload memory. Please try again.",
        variant: "destructive"
      });
    } finally {
      setIsLoading(false);
    }
  };

  const resetUpload = () => {
    if (videoUrl) URL.revokeObjectURL(videoUrl);
    if (trimmedUrl) URL.revokeObjectURL(trimmedUrl);
    setSelectedFile(null);
    setVideoUrl(null);
    setTrimmedBlob(null);
    setTrimmedUrl(null);
    setShowPreview(false);
    setShowTrimmer(false);
    setError(null);
    setVideoDuration(0);
    setDurationLoaded(false);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const togglePlayPause = () => {
    const video = showPreview ? previewVideoRef.current : videoRef.current;
    if (video) {
      if (video.paused) {
        video.play();
        setIsPlaying(true);
      } else {
        video.pause();
        setIsPlaying(false);
      }
    }
  };

  // Show tag selector
  if (showTagSelector && trimmedBlob) {
    return <TagSelector onSelect={handleTagsSelected} onBack={() => setShowTagSelector(false)} />;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-900 to-black flex items-center justify-center p-4">
      <div className="w-full max-w-2xl">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <button
            onClick={onBack}
            className="p-3 bg-white/10 backdrop-blur rounded-full hover:bg-white/20 transition-colors"
          >
            <X className="w-6 h-6 text-white" />
          </button>
          
          <h1 className="text-2xl font-bold text-white">Upload Past Memory</h1>
          
          <div className="w-12" /> {/* Spacer for alignment */}
        </div>

        {/* Upload Area */}
        {!selectedFile && (
          <div className="bg-white/5 backdrop-blur border-2 border-dashed border-white/20 rounded-3xl p-12 text-center">
            <input
              ref={fileInputRef}
              type="file"
              accept="video/*"
              onChange={handleFileSelect}
              className="hidden"
            />
            
            <div 
              onClick={() => fileInputRef.current?.click()}
              className="cursor-pointer"
            >
              <div className="w-24 h-24 bg-gradient-to-br from-teal-500 to-teal-600 rounded-full flex items-center justify-center mx-auto mb-6 hover:scale-110 transition-transform">
                <Upload className="w-12 h-12 text-white" />
              </div>
              
              <h2 className="text-xl font-semibold text-white mb-2">
                Select a Video
              </h2>
              <p className="text-white/60 mb-4">
                Choose a video from your device to upload as a memory
              </p>
              <p className="text-white/40 text-sm">
                Max file size: {formatFileSize(MAX_FILE_SIZE)} • Minimum {MIN_DURATION}s required
              </p>
            </div>
          </div>
        )}

        {/* Error Display */}
        {error && (
          <div className="mt-4 p-4 bg-red-500/20 border border-red-500/50 rounded-xl flex items-center gap-3">
            <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0" />
            <p className="text-red-300">{error}</p>
          </div>
        )}

        {/* Video Selection Preview (before trimming) */}
        {selectedFile && !showTrimmer && !showPreview && (
          <div className="space-y-6">
            <div className="relative bg-black rounded-3xl overflow-hidden aspect-video">
              <video
                ref={videoRef}
                src={videoUrl || undefined}
                onLoadedMetadata={handleVideoLoaded}
                onClick={togglePlayPause}
                className="w-full h-full object-contain cursor-pointer"
                playsInline
              />
              
              {/* Play/Pause Overlay */}
              <div 
                onClick={togglePlayPause}
                className="absolute inset-0 flex items-center justify-center bg-black/30 opacity-0 hover:opacity-100 transition-opacity cursor-pointer"
              >
                {isPlaying ? (
                  <Pause className="w-16 h-16 text-white" />
                ) : (
                  <Play className="w-16 h-16 text-white" />
                )}
              </div>
            </div>

            {/* Video Info */}
            <div className="bg-white/5 backdrop-blur rounded-xl p-4">
              <div className="flex items-center justify-between text-sm">
                <span className="text-white/60">File:</span>
                <span className="text-white truncate max-w-[200px]">{selectedFile.name}</span>
              </div>
              <div className="flex items-center justify-between text-sm mt-2">
                <span className="text-white/60">Size:</span>
                <span className="text-white">{formatFileSize(selectedFile.size)}</span>
              </div>
              <div className="flex items-center justify-between text-sm mt-2">
                <span className="text-white/60">Duration:</span>
                <span className="text-white">
                  {durationLoaded ? (
                    <>
                      {videoDuration.toFixed(1)}s 
                      {videoDuration < MIN_DURATION && (
                        <span className="text-red-400 ml-2">
                          (too short - need at least {MIN_DURATION}s)
                        </span>
                      )}
                      {videoDuration > CLIP_DURATION && (
                        <span className="text-yellow-400 ml-2">
                          (will select {CLIP_DURATION}s)
                        </span>
                      )}
                    </>
                  ) : (
                    <span className="text-white/40">Loading...</span>
                  )}
                </span>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex justify-center gap-4">
              <button
                onClick={resetUpload}
                className="px-6 py-3 bg-white/20 backdrop-blur text-white rounded-full font-semibold hover:bg-white/30 transition-colors flex items-center gap-2"
              >
                <RotateCcw className="w-5 h-5" />
                Choose Different
              </button>
              <button
                onClick={handleContinue}
                className="px-6 py-3 bg-gradient-to-r from-teal-500 to-teal-600 text-white rounded-full font-semibold hover:from-teal-600 hover:to-teal-700 transition-colors flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                disabled={!durationLoaded || videoDuration < MIN_DURATION}
              >
                <Check className="w-5 h-5" />
                {videoDuration > CLIP_DURATION ? 'Select Clip' : 'Continue'}
              </button>
            </div>
          </div>
        )}

        {/* Video Trimmer */}
        {showTrimmer && selectedFile && videoUrl && (
          <VideoTrimmer
            videoFile={selectedFile}
            videoUrl={videoUrl}
            videoDuration={videoDuration}
            onTrimComplete={handleTrimComplete}
            onChooseDifferent={resetUpload}
          />
        )}

        {/* Trimmed Preview */}
        {showPreview && trimmedUrl && (
          <div className="space-y-6">
            <div className="relative bg-black rounded-3xl overflow-hidden aspect-video">
              <video
                ref={previewVideoRef}
                src={trimmedUrl}
                autoPlay
                loop
                playsInline
                className="w-full h-full object-contain"
              />
              
              {/* Duration Badge */}
              <div className="absolute top-4 left-4 px-3 py-1 bg-black/50 backdrop-blur rounded-full">
                <span className="text-white text-sm font-medium">
                  {CLIP_DURATION}s clip
                </span>
              </div>

              {/* Ready Badge */}
              <div className="absolute top-4 right-4 px-3 py-1 bg-green-500/80 backdrop-blur rounded-full">
                <span className="text-white text-sm font-medium flex items-center gap-1">
                  <Check className="w-4 h-4" />
                  Ready
                </span>
              </div>
            </div>

            {/* Preview Controls */}
            <div className="flex justify-center gap-4">
              <button
                onClick={resetUpload}
                className="px-6 py-3 bg-white/20 backdrop-blur text-white rounded-full font-semibold hover:bg-white/30 transition-colors flex items-center gap-2"
                disabled={isLoading}
              >
                <RotateCcw className="w-5 h-5" />
                Choose Different
              </button>
              <button
                onClick={confirmVideo}
                className="px-6 py-3 bg-gradient-to-r from-teal-500 to-teal-600 text-white rounded-full font-semibold hover:from-teal-600 hover:to-teal-700 transition-colors flex items-center gap-2"
                disabled={isLoading}
              >
                {isLoading ? (
                  <Loader className="w-5 h-5 animate-spin" />
                ) : (
                  <Check className="w-5 h-5" />
                )}
                Add Tags & Save
              </button>
            </div>
          </div>
        )}

        {/* Instructions */}
        {!selectedFile && (
          <div className="mt-8 text-center">
            <p className="text-white/40 text-sm">
              Supported formats: MP4, WebM, MOV, and other common video formats
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export default UploadMemory;

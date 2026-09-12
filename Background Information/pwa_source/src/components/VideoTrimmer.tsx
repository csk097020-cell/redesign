import React, { useState, useRef, useEffect, useCallback } from 'react';
import { X, Play, Pause, Loader, Scissors, RotateCcw, AlertCircle } from 'lucide-react';
import { FFmpeg } from '@ffmpeg/ffmpeg';
import { toBlobURL, fetchFile } from '@ffmpeg/util';
import { Slider } from '@/components/ui/slider';

interface VideoTrimmerProps {
  videoFile: File;
  videoUrl: string;
  videoDuration: number;
  onTrimComplete: (trimmedBlob: Blob) => void;
  onChooseDifferent: () => void;
}

const CLIP_DURATION = 10; // Fixed 10-second clip

const VideoTrimmer: React.FC<VideoTrimmerProps> = ({
  videoFile,
  videoUrl,
  videoDuration,
  onTrimComplete,
  onChooseDifferent,
}) => {
  const [startTime, setStartTime] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [isTrimming, setIsTrimming] = useState(false);
  const [ffmpegLoaded, setFfmpegLoaded] = useState(false);
  const [ffmpegLoading, setFfmpegLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [progress, setProgress] = useState(0);
  
  const videoRef = useRef<HTMLVideoElement>(null);
  const ffmpegRef = useRef<FFmpeg | null>(null);
  const playbackIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Calculate max start time (duration - 10 seconds)
  const maxStartTime = Math.max(0, videoDuration - CLIP_DURATION);

  // Format time as MM:SS
  const formatTime = (seconds: number): string => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  // Format time with decimals for precise display
  const formatTimeDetailed = (seconds: number): string => {
    const mins = Math.floor(seconds / 60);
    const secs = (seconds % 60).toFixed(1);
    return `${mins}:${secs.padStart(4, '0')}`;
  };

  // Load FFmpeg
  const loadFFmpeg = async () => {
    if (ffmpegRef.current && ffmpegLoaded) return;
    
    setFfmpegLoading(true);
    setError(null);
    
    try {
      const ffmpeg = new FFmpeg();
      ffmpegRef.current = ffmpeg;
      
      ffmpeg.on('progress', ({ progress }) => {
        setProgress(Math.round(progress * 100));
      });
      
      const baseURL = 'https://unpkg.com/@ffmpeg/core@0.12.6/dist/esm';
      
      await ffmpeg.load({
        coreURL: await toBlobURL(`${baseURL}/ffmpeg-core.js`, 'text/javascript'),
        wasmURL: await toBlobURL(`${baseURL}/ffmpeg-core.wasm`, 'application/wasm'),
      });
      
      setFfmpegLoaded(true);
    } catch (err: any) {
      console.error('FFmpeg load error:', err);
      setError('Failed to load video processor. Please try again.');
    } finally {
      setFfmpegLoading(false);
    }
  };

  // Load FFmpeg on mount
  useEffect(() => {
    loadFFmpeg();
    
    return () => {
      if (playbackIntervalRef.current) {
        clearInterval(playbackIntervalRef.current);
      }
    };
  }, []);

  // Update video playback position when slider changes
  useEffect(() => {
    if (videoRef.current && !isPlaying) {
      videoRef.current.currentTime = startTime;
    }
  }, [startTime]);

  // Handle playback loop within the selected 10-second window
  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    const handleTimeUpdate = () => {
      if (video.currentTime >= startTime + CLIP_DURATION) {
        video.currentTime = startTime;
      }
    };

    video.addEventListener('timeupdate', handleTimeUpdate);
    return () => video.removeEventListener('timeupdate', handleTimeUpdate);
  }, [startTime]);

  const handleSliderChange = (value: number[]) => {
    const newStartTime = value[0];
    setStartTime(newStartTime);
    
    if (videoRef.current) {
      videoRef.current.currentTime = newStartTime;
    }
  };

  const togglePlayPause = () => {
    const video = videoRef.current;
    if (!video) return;

    if (isPlaying) {
      video.pause();
      setIsPlaying(false);
    } else {
      video.currentTime = startTime;
      video.play();
      setIsPlaying(true);
    }
  };

  const handleVideoEnded = () => {
    if (videoRef.current) {
      videoRef.current.currentTime = startTime;
      videoRef.current.play();
    }
  };

  const handleTrim = async () => {
    if (!ffmpegRef.current || !ffmpegLoaded) {
      setError('Video processor not ready. Please wait...');
      await loadFFmpeg();
      return;
    }

    setIsTrimming(true);
    setError(null);
    setProgress(0);

    try {
      const ffmpeg = ffmpegRef.current;
      
      // Get file extension
      const extension = videoFile.name.split('.').pop()?.toLowerCase() || 'mp4';
      const inputFileName = `input.${extension}`;
      const outputFileName = 'output.mp4';

      // Write input file to FFmpeg virtual filesystem
      await ffmpeg.writeFile(inputFileName, await fetchFile(videoFile));

      // Run FFmpeg trim command
      // -ss: start time, -t: duration, -c:v copy -c:a copy for fast copy (no re-encoding)
      // If copy doesn't work well, fall back to re-encoding
      await ffmpeg.exec([
        '-ss', startTime.toFixed(2),
        '-i', inputFileName,
        '-t', CLIP_DURATION.toString(),
        '-c:v', 'libx264',
        '-preset', 'ultrafast',
        '-c:a', 'aac',
        '-strict', 'experimental',
        outputFileName
      ]);

      // Read the output file
      const data = await ffmpeg.readFile(outputFileName);
      
      // Create blob from the output
      const trimmedBlob = new Blob([data], { type: 'video/mp4' });
      
      // Cleanup
      await ffmpeg.deleteFile(inputFileName);
      await ffmpeg.deleteFile(outputFileName);

      onTrimComplete(trimmedBlob);
    } catch (err: any) {
      console.error('Trim error:', err);
      setError('Failed to trim video. Please try a different file or format.');
    } finally {
      setIsTrimming(false);
      setProgress(0);
    }
  };

  return (
    <div className="space-y-6">
      {/* Video Preview */}
      <div className="relative bg-black rounded-3xl overflow-hidden aspect-video">
        <video
          ref={videoRef}
          src={videoUrl}
          onClick={togglePlayPause}
          onEnded={handleVideoEnded}
          className="w-full h-full object-contain cursor-pointer"
          playsInline
          muted={false}
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

        {/* Time Display Badge */}
        <div className="absolute top-4 left-4 px-3 py-1 bg-black/60 backdrop-blur rounded-full">
          <span className="text-white text-sm font-medium">
            {formatTimeDetailed(startTime)} - {formatTimeDetailed(startTime + CLIP_DURATION)}
          </span>
        </div>

        {/* Duration Badge */}
        <div className="absolute top-4 right-4 px-3 py-1 bg-teal-500/80 backdrop-blur rounded-full">
          <span className="text-white text-sm font-medium">
            {CLIP_DURATION}s clip
          </span>
        </div>
      </div>

      {/* Trim Slider Section */}
      <div className="bg-white/5 backdrop-blur rounded-2xl p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-white font-semibold flex items-center gap-2">
            <Scissors className="w-5 h-5 text-teal-400" />
            Select 10-Second Window
          </h3>
          <span className="text-white/60 text-sm">
            Total: {formatTime(videoDuration)}
          </span>
        </div>

        {/* Timeline Slider */}
        <div className="space-y-3">
          <Slider
            value={[startTime]}
            onValueChange={handleSliderChange}
            max={maxStartTime}
            min={0}
            step={0.1}
            className="w-full"
            disabled={isTrimming}
          />
          
          {/* Time Labels */}
          <div className="flex justify-between text-sm text-white/60">
            <span>{formatTime(0)}</span>
            <span className="text-teal-400 font-medium">
              Start: {formatTimeDetailed(startTime)}
            </span>
            <span>{formatTime(videoDuration)}</span>
          </div>
        </div>

        {/* Visual Timeline */}
        <div className="relative h-8 bg-white/10 rounded-lg overflow-hidden">
          {/* Full timeline */}
          <div className="absolute inset-0 bg-gradient-to-r from-white/5 to-white/10" />
          
          {/* Selected window highlight */}
          <div 
            className="absolute top-0 bottom-0 bg-teal-500/40 border-l-2 border-r-2 border-teal-400"
            style={{
              left: `${(startTime / videoDuration) * 100}%`,
              width: `${(CLIP_DURATION / videoDuration) * 100}%`,
            }}
          />
          
          {/* Start marker */}
          <div 
            className="absolute top-0 bottom-0 w-1 bg-teal-400"
            style={{ left: `${(startTime / videoDuration) * 100}%` }}
          />
          
          {/* End marker */}
          <div 
            className="absolute top-0 bottom-0 w-1 bg-teal-400"
            style={{ left: `${((startTime + CLIP_DURATION) / videoDuration) * 100}%` }}
          />
        </div>

        {/* Info Text */}
        <p className="text-white/50 text-sm text-center">
          Drag the slider to choose which 10 seconds to keep
        </p>
      </div>

      {/* Error Display */}
      {error && (
        <div className="p-4 bg-red-500/20 border border-red-500/50 rounded-xl flex items-center gap-3">
          <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0" />
          <p className="text-red-300">{error}</p>
        </div>
      )}

      {/* FFmpeg Loading State */}
      {ffmpegLoading && (
        <div className="p-4 bg-teal-500/20 border border-teal-500/50 rounded-xl flex items-center gap-3">
          <Loader className="w-5 h-5 text-teal-400 animate-spin" />
          <p className="text-teal-300">Loading video processor...</p>
        </div>
      )}

      {/* Trimming Progress */}
      {isTrimming && (
        <div className="p-4 bg-teal-500/20 border border-teal-500/50 rounded-xl space-y-3">
          <div className="flex items-center gap-3">
            <Loader className="w-5 h-5 text-teal-400 animate-spin" />
            <p className="text-teal-300">Trimming video... {progress}%</p>
          </div>
          <div className="w-full bg-white/10 rounded-full h-2">
            <div 
              className="bg-teal-500 h-2 rounded-full transition-all duration-300"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex justify-center gap-4">
        <button
          onClick={onChooseDifferent}
          className="px-6 py-3 bg-white/20 backdrop-blur text-white rounded-full font-semibold hover:bg-white/30 transition-colors flex items-center gap-2"
          disabled={isTrimming}
        >
          <RotateCcw className="w-5 h-5" />
          Choose Different
        </button>
        <button
          onClick={handleTrim}
          className="px-6 py-3 bg-gradient-to-r from-teal-500 to-teal-600 text-white rounded-full font-semibold hover:from-teal-600 hover:to-teal-700 transition-colors flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
          disabled={isTrimming || ffmpegLoading}
        >
          {isTrimming ? (
            <>
              <Loader className="w-5 h-5 animate-spin" />
              Trimming...
            </>
          ) : (
            <>
              <Scissors className="w-5 h-5" />
              Trim & Continue
            </>
          )}
        </button>
      </div>
    </div>
  );
};

export default VideoTrimmer;

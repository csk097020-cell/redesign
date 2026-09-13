import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Camera, RotateCcw, Check, X, Loader, AlertCircle, Mic, MicOff, SwitchCamera } from 'lucide-react';
import TagSelector from './TagSelector';
import { supabase } from '@/lib/supabase';
import { useToast } from '@/hooks/use-toast';
import { getSupportedMimeType, getVideoConstraints, isWebRTCSupported, formatFileSize } from '@/utils/videoHelpers';

interface MemoryCaptureProps {
  onSave: (memory: any) => void;
  onBack: () => void;
  user: any;
}


const MemoryCapture: React.FC<MemoryCaptureProps> = ({ onSave, onBack, user }) => {

  const [isRecording, setIsRecording] = useState(false);
  const [countdown, setCountdown] = useState(10);
  const [showPreview, setShowPreview] = useState(false);
  const [showTagSelector, setShowTagSelector] = useState(false);
  const [recordedBlob, setRecordedBlob] = useState<Blob | null>(null);
  const [recordedUrl, setRecordedUrl] = useState<string | null>(null);
  const [cameraPermission, setCameraPermission] = useState<'prompt' | 'granted' | 'denied'>('prompt');
  const [isLoading, setIsLoading] = useState(false);
  const [audioEnabled, setAudioEnabled] = useState(true);
  const [cameraReady, setCameraReady] = useState(false);
  const [isMobile] = useState(/iPhone|iPad|iPod|Android/i.test(navigator.userAgent));
  const [facingMode, setFacingMode] = useState<'user' | 'environment'>(
    /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) ? 'environment' : 'user'
  );

  
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const animationFrameRef = useRef<number | null>(null);

  
  const { toast } = useToast();

  // Initialize camera on mount
  useEffect(() => {
    initializeCamera();
    return () => {
      stopCamera();
    };
  }, []);

  // Countdown timer
  useEffect(() => {
    if (isRecording && countdown > 0) {
      const timer = setTimeout(() => setCountdown(countdown - 1), 1000);
      return () => clearTimeout(timer);
    } else if (isRecording && countdown === 0) {
      stopRecording();
    }
  }, [isRecording, countdown]);

  const initializeCamera = async () => {
    if (!isWebRTCSupported()) {
      setCameraPermission('denied');
      toast({
        title: "Browser Not Supported",
        description: "Your browser doesn't support video recording. Please use a modern browser.",
        variant: "destructive"
      });
      return;
    }

    try {
      const constraints = getVideoConstraints(facingMode);
      const stream = await navigator.mediaDevices.getUserMedia({
        video: constraints,
        audio: audioEnabled
      });
      
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        videoRef.current.muted = true;
      }
      setCameraPermission('granted');
      setCameraReady(true);
    } catch (error) {
      console.error('Camera access error:', error);
      setCameraPermission('denied');
      toast({
        title: "Camera Access Required",
        description: "Please allow camera access to record memories",
        variant: "destructive"
      });
    }
  };

  const stopCamera = () => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(track => track.stop());
      streamRef.current = null;
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
  };

  const switchCamera = async () => {
    stopCamera();
    setFacingMode(prev => prev === 'user' ? 'environment' : 'user');
    setCameraReady(false);
    
    try {
      const constraints = getVideoConstraints(facingMode === 'user' ? 'environment' : 'user');
      const stream = await navigator.mediaDevices.getUserMedia({
        video: constraints,
        audio: audioEnabled
      });
      
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        videoRef.current.muted = true;
      }
      setCameraReady(true);
    } catch (error) {
      console.error('Camera switch error:', error);
      // Fallback to original camera if switch fails
      setFacingMode('user');
      initializeCamera();
    }
  };

  const toggleAudio = () => {
    if (streamRef.current) {
      const audioTracks = streamRef.current.getAudioTracks();
      audioTracks.forEach(track => {
        track.enabled = !audioEnabled;
      });
      setAudioEnabled(!audioEnabled);
    }
  };

  const startRecording = async () => {
    if (!streamRef.current) {
      await initializeCamera();
    }
    
    if (!streamRef.current || !videoRef.current) return;
    
    chunksRef.current = [];
    
    // Set recording state first
    setIsRecording(true);
    setCountdown(10);
    
    // Determine if we should flip the video (only for front camera)
    const shouldFlip = facingMode === 'user';
    
    // Create canvas for processing video
    const canvas = document.createElement('canvas');
    const video = videoRef.current;
    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;
    canvasRef.current = canvas;
    
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    
    let isDrawing = true;
    
    // Function to draw video frame (flipped or normal)
    const drawFrame = () => {
      if (!isDrawing) return;
      
      ctx.save();
      if (shouldFlip) {
        ctx.scale(-1, 1);
        ctx.drawImage(video, -canvas.width, 0, canvas.width, canvas.height);
      } else {
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
      }
      ctx.restore();
      
      animationFrameRef.current = requestAnimationFrame(drawFrame);
    };
    
    // Start drawing frames
    drawFrame();
    
    // Get stream from canvas
    const canvasStream = canvas.captureStream(30); // 30 fps
    
    // Add audio track from original stream if audio is enabled
    if (audioEnabled) {
      const audioTracks = streamRef.current.getAudioTracks();
      audioTracks.forEach(track => canvasStream.addTrack(track));
    }
    
    const mimeType = getSupportedMimeType();
    const options: MediaRecorderOptions = {
      mimeType,
      videoBitsPerSecond: isMobile ? 1500000 : 2500000
    };
    
    try {
      const mediaRecorder = new MediaRecorder(canvasStream, options);
      
      mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          chunksRef.current.push(event.data);
        }
      };
      
      mediaRecorder.onstop = () => {
        // Stop animation frame
        isDrawing = false;
        if (animationFrameRef.current) {
          cancelAnimationFrame(animationFrameRef.current);
          animationFrameRef.current = null;
        }
        
        const blob = new Blob(chunksRef.current, { type: mimeType });
        setRecordedBlob(blob);
        const url = URL.createObjectURL(blob);
        setRecordedUrl(url);
        setShowPreview(true);
        stopCamera();
        
        // Show file size in console for debugging
        console.log('Recorded video size:', formatFileSize(blob.size));
      };
      
      mediaRecorderRef.current = mediaRecorder;
      mediaRecorder.start(1000); // Capture data every second
    } catch (error) {
      console.error('Failed to start recording:', error);
      toast({
        title: "Recording Failed",
        description: "Unable to start recording. Please try again.",
        variant: "destructive"
      });
      setIsRecording(false);
    }
  };




  const stopRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.stop();
      setIsRecording(false);
    }
  };

  const retake = () => {
    setShowPreview(false);
    setRecordedBlob(null);
    if (recordedUrl) {
      URL.revokeObjectURL(recordedUrl);
      setRecordedUrl(null);
    }
    setCountdown(10);
    initializeCamera();
  };

  const confirmVideo = () => {
    setShowTagSelector(true);
  };

  const handleTagsSelected = async (tags: string[]) => {
    if (!recordedBlob || !user) return;
    
    setIsLoading(true);
    
    try {
      // Generate unique filename
      const timestamp = Date.now();
      const videoFileName = `${user.id}/${timestamp}.webm`;
      
      // Upload video to Supabase storage
      const { data: videoData, error: videoError } = await supabase.storage
        .from('user-videos')
        .upload(videoFileName, recordedBlob, {
          contentType: recordedBlob.type,
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
          title: tags.length > 0 ? `${tags[0]} Memory` : 'Untitled Memory',
          video_url: publicUrl,
          tags: tags,
          is_favorite: false
        })
        .select()
        .single();

      if (memoryError) throw memoryError;

      toast({
        title: "Memory Saved!",
        description: "Your memory has been captured successfully"
      });
      
      if (recordedUrl) URL.revokeObjectURL(recordedUrl);
      onSave(memoryData);
      
    } catch (error: any) {
      console.error('Error saving memory:', error);
      toast({
        title: "Save Failed",
        description: error.message || "Failed to save memory. Please try again.",
        variant: "destructive"
      });
    } finally {
      setIsLoading(false);
    }
  };



  if (showTagSelector && recordedBlob) {
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
          
          <h1 className="text-2xl font-bold text-white">Capture Memory</h1>
          
          <button
            onClick={toggleAudio}
            className="p-3 bg-white/10 backdrop-blur rounded-full hover:bg-white/20 transition-colors"
            disabled={showPreview}
          >
            {audioEnabled ? (
              <Mic className="w-6 h-6 text-white" />
            ) : (
              <MicOff className="w-6 h-6 text-white" />
            )}
          </button>
        </div>

        {/* Camera View */}
        <div className="relative bg-black rounded-3xl overflow-hidden aspect-[9/16] max-h-[70vh] mx-auto">
          {!showPreview ? (
            <>
              {/* Live Camera Feed */}
              <video
                ref={videoRef}
                autoPlay
                playsInline
                muted
                className="absolute inset-0 w-full h-full object-cover"
                style={{ transform: facingMode === 'user' ? 'scaleX(-1)' : 'none' }}
              />





              
              {/* Camera Permission Denied */}
              {cameraPermission === 'denied' && (
                <div className="absolute inset-0 bg-gray-900 flex flex-col items-center justify-center p-8">
                  <AlertCircle className="w-16 h-16 text-red-500 mb-4" />
                  <p className="text-white text-center mb-2">Camera access denied</p>
                  <p className="text-white/60 text-sm text-center">
                    Please enable camera access in your browser settings to record memories
                  </p>
                </div>
              )}
              
              
              {/* Switch Camera Button (Mobile Only) */}
              {isMobile && cameraReady && !isRecording && (
                <button
                  onClick={switchCamera}
                  className="absolute top-4 right-4 p-3 bg-white/10 backdrop-blur rounded-full hover:bg-white/20 transition-colors"
                >
                  <SwitchCamera className="w-5 h-5 text-white" />
                </button>
              )}
              
              {/* Recording Indicator */}
              {isRecording && (
                <div className="absolute top-4 left-4 flex items-center gap-2 bg-red-600 px-3 py-1 rounded-full">
                  <div className="w-2 h-2 bg-white rounded-full animate-pulse"></div>
                  <span className="text-white font-semibold">REC</span>
                </div>
              )}
              
              {/* Countdown */}
              {isRecording && (
                <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2">
                  <div className="text-8xl font-bold text-white animate-pulse drop-shadow-2xl">
                    {countdown}
                  </div>
                </div>
              )}
              
              {/* Progress Bar */}
              {isRecording && (
                <div className="absolute bottom-0 left-0 right-0 h-1 bg-white/20">
                  <div 
                    className="h-full bg-coral-500 transition-all duration-1000"
                    style={{ width: `${((10 - countdown) / 10) * 100}%` }}
                  />
                </div>
              )}
            </>
          ) : (
            <>
              {/* Video Preview */}
              {recordedUrl && (
                <video
                  src={recordedUrl}
                  autoPlay
                  loop
                  playsInline
                  className="absolute inset-0 w-full h-full object-cover"
                />
              )}

              
              {/* Preview Controls */}
              <div className="absolute bottom-0 left-0 right-0 p-6 bg-gradient-to-t from-black/80 to-transparent">
                <div className="flex justify-center gap-4">
                  <button
                    onClick={retake}
                    className="px-6 py-3 bg-white/20 backdrop-blur text-white rounded-full font-semibold hover:bg-white/30 transition-colors flex items-center gap-2"
                    disabled={isLoading}
                  >
                    <RotateCcw className="w-5 h-5" />
                    Retake
                  </button>
                  <button
                    onClick={confirmVideo}
                    className="px-6 py-3 bg-gradient-to-r from-coral-500 to-coral-600 text-white rounded-full font-semibold hover:from-coral-600 hover:to-coral-700 transition-colors flex items-center gap-2"
                    disabled={isLoading}
                  >
                    {isLoading ? (
                      <Loader className="w-5 h-5 animate-spin" />
                    ) : (
                      <Check className="w-5 h-5" />
                    )}
                    Use This
                  </button>
                </div>
              </div>
            </>
          )}
        </div>

        {/* Record Button */}
        {!showPreview && !isRecording && cameraReady && (
          <div className="flex justify-center mt-8">
            <button
              onClick={startRecording}
              className="w-20 h-20 bg-gradient-to-r from-coral-500 to-coral-600 rounded-full flex items-center justify-center hover:from-coral-600 hover:to-coral-700 transition-all duration-200 shadow-2xl transform hover:scale-110"
            >
              <div className="w-16 h-16 bg-white rounded-full"></div>
            </button>
          </div>
        )}

        {/* Instructions */}
        {!showPreview && !isRecording && cameraReady && (
          <p className="text-center text-white/60 mt-4">
            Tap to start recording your 10-second memory
          </p>
        )}
        
        {/* Loading Camera */}
        {!cameraReady && cameraPermission !== 'denied' && !showPreview && (
          <div className="flex justify-center mt-8">
            <Loader className="w-8 h-8 text-white animate-spin" />
          </div>
        )}
      </div>
    </div>
  );
};

export default MemoryCapture;
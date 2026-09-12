export const getSupportedMimeType = (): string => {
  const types = [
    'video/webm;codecs=vp9,opus',
    'video/webm;codecs=vp8,opus',
    'video/webm;codecs=vp9',
    'video/webm;codecs=vp8',
    'video/webm',
    'video/mp4',
  ];

  for (const type of types) {
    if (MediaRecorder.isTypeSupported(type)) {
      return type;
    }
  }
  
  return 'video/webm'; // fallback
};

export const getVideoConstraints = (facingMode: 'user' | 'environment' = 'user') => {
  const isMobile = /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);
  
  return {
    facingMode,
    width: isMobile ? { ideal: 720 } : { ideal: 1280 },
    height: isMobile ? { ideal: 1280 } : { ideal: 720 },
    aspectRatio: isMobile ? 9/16 : 16/9,
    frameRate: { ideal: 30, max: 30 }
  };
};

export const compressVideo = async (blob: Blob): Promise<Blob> => {
  // For now, return the original blob
  // In production, you might want to use a video compression library
  return blob;
};

export const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 Bytes';
  
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
};

export const checkMediaPermissions = async (): Promise<{
  camera: boolean;
  microphone: boolean;
}> => {
  try {
    const cameraPermission = await navigator.permissions.query({ name: 'camera' as PermissionName });
    const microphonePermission = await navigator.permissions.query({ name: 'microphone' as PermissionName });
    
    return {
      camera: cameraPermission.state === 'granted',
      microphone: microphonePermission.state === 'granted'
    };
  } catch (error) {
    // Fallback for browsers that don't support permissions API
    return {
      camera: false,
      microphone: false
    };
  }
};

export const isWebRTCSupported = (): boolean => {
  return !!(
    navigator.mediaDevices &&
    navigator.mediaDevices.getUserMedia &&
    window.MediaRecorder
  );
};
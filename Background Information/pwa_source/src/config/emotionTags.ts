/**
 * Centralized configuration for emotion-based tags
 * Each emotion tag has a consistent color for visual representation
 */

export interface EmotionTag {
  name: string;
  color: string;
  icon: string;
}

export const EMOTION_TAGS: Record<string, EmotionTag> = {
  // Category-based tags
  adventure: {
    name: 'Adventure',
    color: '#FF7043',
    icon: '🏔️',
  },
  celebrations: {
    name: 'Celebrations',
    color: '#FFB74D',
    icon: '🎉',
  },
  family: {
    name: 'Family',
    color: '#F48FB1',
    icon: '👨‍👩‍👧‍👦',
  },
  friends: {
    name: 'Friends',
    color: '#4FC3F7',
    icon: '👥',
  },
  pets: {
    name: 'Pets',
    color: '#A1887F',
    icon: '🐾',
  },
  travel: {
    name: 'Travel',
    color: '#4DB6AC',
    icon: '✈️',
  },
  work: {
    name: 'Work',
    color: '#90A4AE',
    icon: '💼',
  },
  // Emotion-based tags
  happy: {
    name: 'Happy',
    color: '#FFD54F',
    icon: '😊',
  },
  sad: {
    name: 'Sad',
    color: '#90CAF9',
    icon: '😢',
  },
  grateful: {
    name: 'Grateful',
    color: '#81C784',
    icon: '🙏',
  },
  hurt: {
    name: 'Hurt',
    color: '#EF9A9A',
    icon: '💔',
  },
  awe: {
    name: 'Awe',
    color: '#B39DDB',
    icon: '✨',
  },
  // Additional emotion tags
  scared: {
    name: 'Scared',
    color: '#78909C',
    icon: '😨',
  },
  joy: {
    name: 'Joy',
    color: '#FFF176',
    icon: '🌟',
  },
  grief: {
    name: 'Grief',
    color: '#B0BEC5',
    icon: '🕊️',
  },
  excited: {
    name: 'Excited',
    color: '#FFAB91',
    icon: '🤩',
  },
};

// Array format for easy iteration
export const EMOTION_TAGS_LIST: EmotionTag[] = Object.values(EMOTION_TAGS);

// Get emotion tag by name (case-insensitive)
export const getEmotionTagByName = (name: string): EmotionTag | undefined => {
  const key = name.toLowerCase();
  return EMOTION_TAGS[key];
};

// Get color for a tag name (returns undefined if not an emotion tag)
export const getEmotionTagColor = (name: string): string | undefined => {
  const tag = getEmotionTagByName(name);
  return tag?.color;
};

// Check if a tag name is an emotion tag
export const isEmotionTag = (name: string): boolean => {
  return name.toLowerCase() in EMOTION_TAGS;
};

// Get icon for a tag name
export const getEmotionTagIcon = (name: string): string | undefined => {
  const tag = getEmotionTagByName(name);
  return tag?.icon;
};

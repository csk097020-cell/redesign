import React, { useState, useEffect } from 'react';
import { Check, X, Plus } from 'lucide-react';
import { supabase } from '@/lib/supabase';
import { toast } from 'sonner';
import { getEmotionTagColor } from '@/config/emotionTags';

interface TagSelectorProps {
  onSelect: (tags: string[]) => void;
  onBack: () => void;
  buttonText?: string;
  initialTags?: string[];
}

interface Tag {
  id: string;
  name: string;
  color: string;
  icon: string;
}

const TagSelector: React.FC<TagSelectorProps> = ({ onSelect, onBack, buttonText = 'Save Memory', initialTags = [] }) => {
  const [selectedTags, setSelectedTags] = useState<string[]>(initialTags);
  const [customTag, setCustomTag] = useState('');
  const [availableTags, setAvailableTags] = useState<Tag[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchActiveTags();
  }, []);

  const fetchActiveTags = async () => {
    try {
      const { data: { user } } = await supabase.auth.getUser();
      
      // Fetch both default tags (user_id is null) and user-specific tags
      const { data, error } = await supabase
        .from('tags')
        .select('*')
        .or(`user_id.is.null${user ? `,user_id.eq.${user.id}` : ''}`)
        .order('name');

      if (error) throw error;
      setAvailableTags(data || []);
    } catch (error) {
      console.error('Error fetching tags:', error);
      toast.error('Failed to load tags');
    } finally {
      setLoading(false);
    }
  };

  const toggleTag = (tagId: string) => {
    setSelectedTags(prev =>
      prev.includes(tagId) 
        ? prev.filter(t => t !== tagId)
        : [...prev, tagId]
    );
  };

  const addCustomTag = () => {
    if (customTag && !selectedTags.includes(customTag)) {
      setSelectedTags([...selectedTags, customTag]);
      setCustomTag('');
    }
  };

  const handleSave = () => {
    if (selectedTags.length > 0) {
      onSelect(selectedTags);
    }
  };

  // Get tag color - prioritize database color, fallback to emotion tag config
  const getTagColor = (tag: Tag): string => {
    // First check if it's in the database with a color
    if (tag.color) {
      return tag.color;
    }
    // Fallback to emotion tag config
    const emotionColor = getEmotionTagColor(tag.name);
    return emotionColor || '#3B82F6'; // Default blue if no color found
  };

  // Get display info for a selected tag
  const getSelectedTagInfo = (tagId: string): { name: string; color: string } => {
    const tag = availableTags.find(t => t.id === tagId);
    if (tag) {
      return { name: tag.name, color: getTagColor(tag) };
    }
    // For custom tags, check if it matches an emotion tag name
    const emotionColor = getEmotionTagColor(tagId);
    return { name: tagId, color: emotionColor || '#F97316' }; // Coral color for custom tags
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-cream-50 to-white flex items-center justify-center p-4">
      <div className="w-full max-w-2xl bg-white rounded-3xl shadow-2xl p-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between mb-3">
          <button onClick={onBack} className="p-1.5 rounded-lg hover:bg-gray-100 transition-colors">
            <X className="w-4 h-4 text-gray-600" />
          </button>
          <h2 className="text-lg font-bold text-gray-900">Tag Your Memory</h2>
          <div className="w-7"></div>
        </div>

        {loading ? (
          <div className="text-center py-6 text-sm">Loading tags...</div>
        ) : (
          <>
            <div className="grid grid-cols-3 md:grid-cols-4 gap-2 mb-4">
              {availableTags.map(tag => {
                const tagColor = getTagColor(tag);
                return (
                  <button
                    key={tag.id}
                    onClick={() => toggleTag(tag.id)}
                    className={`relative p-2 rounded-lg border-2 transition-all ${
                      selectedTags.includes(tag.id) ? 'border-coral-500 bg-coral-50' : 'border-gray-200 hover:border-gray-300'
                    }`}
                  >
                    <div 
                      className="w-8 h-8 rounded-lg flex items-center justify-center mb-1 mx-auto text-base" 
                      style={{ backgroundColor: tagColor }}
                    >
                      {tag.icon}
                    </div>
                    <p className="font-semibold text-xs text-gray-900 truncate">{tag.name}</p>
                    {selectedTags.includes(tag.id) && (
                      <div className="absolute top-1 right-1 w-4 h-4 bg-coral-500 rounded-full flex items-center justify-center">
                        <Check className="w-2.5 h-2.5 text-white" />
                      </div>
                    )}
                  </button>
                );
              })}
            </div>

            <div className="mb-3">
              <label className="block text-xs font-medium text-gray-700 mb-1.5">Add Custom Tag</label>
              <div className="flex gap-2">
                <input type="text" value={customTag} onChange={(e) => setCustomTag(e.target.value)}
                  onKeyPress={(e) => e.key === 'Enter' && addCustomTag()}
                  placeholder="Enter custom tag..."
                  className="flex-1 px-2.5 py-1.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-coral-500 text-xs"
                />
                <button onClick={addCustomTag} className="px-2.5 py-1.5 bg-gray-100 rounded-lg hover:bg-gray-200">
                  <Plus className="w-3.5 h-3.5 text-gray-600" />
                </button>
              </div>
            </div>

            {selectedTags.length > 0 && (
              <div className="mb-3">
                <p className="text-xs font-medium text-gray-700 mb-1.5">Selected Tags:</p>
                <div className="flex flex-wrap gap-1.5">
                  {selectedTags.map(tagId => {
                    const { name, color } = getSelectedTagInfo(tagId);
                    // Calculate text color based on background brightness
                    const isLightColor = (hexColor: string) => {
                      const hex = hexColor.replace('#', '');
                      const r = parseInt(hex.substr(0, 2), 16);
                      const g = parseInt(hex.substr(2, 2), 16);
                      const b = parseInt(hex.substr(4, 2), 16);
                      const brightness = (r * 299 + g * 587 + b * 114) / 1000;
                      return brightness > 155;
                    };
                    const textColor = isLightColor(color) ? '#1F2937' : '#FFFFFF';
                    
                    return (
                      <span 
                        key={tagId} 
                        className="px-2 py-0.5 rounded-full text-xs font-medium"
                        style={{ backgroundColor: color, color: textColor }}
                      >
                        {name}
                      </span>
                    );
                  })}
                </div>
              </div>
            )}

            <div className="flex gap-2">
              <button onClick={onBack} className="flex-1 py-2 bg-gray-100 text-gray-700 rounded-lg font-semibold hover:bg-gray-200 text-xs">
                Cancel
              </button>
              <button onClick={handleSave} disabled={selectedTags.length === 0}
                className={`flex-1 py-2 rounded-lg font-semibold text-xs ${selectedTags.length > 0 ? 'bg-gradient-to-r from-coral-500 to-coral-600 text-white hover:from-coral-600 hover:to-coral-700' : 'bg-gray-200 text-gray-400 cursor-not-allowed'}`}
              >
                {buttonText}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default TagSelector;

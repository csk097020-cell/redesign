import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { supabase } from '@/lib/supabase';
import { toast } from 'sonner';

interface Tag {
  id: string;
  name: string;
  color: string;
  icon: string;
  created_at: string;
}

interface TagManagementProps {
  tags: Tag[];
  onRefresh: () => void;
}

const TagManagement: React.FC<TagManagementProps> = ({ tags, onRefresh }) => {
  const [showAddTag, setShowAddTag] = useState(false);
  const [newTag, setNewTag] = useState({ name: '', color: '#3B82F6', icon: '🏷️' });
  const [editingTag, setEditingTag] = useState<Tag | null>(null);

  const handleAddTag = async () => {
    if (!newTag.name.trim()) {
      toast.error('Tag name is required');
      return;
    }

    try {
      const { error } = await supabase
        .from('tags')
        .insert([{ name: newTag.name, color: newTag.color, icon: newTag.icon }]);

      if (error) throw error;
      toast.success('Tag created successfully');
      setNewTag({ name: '', color: '#3B82F6', icon: '🏷️' });
      setShowAddTag(false);
      onRefresh();
    } catch (error) {
      toast.error('Failed to create tag');
      console.error(error);
    }
  };

  const handleUpdateTag = async () => {
    if (!editingTag) return;

    try {
      const { error } = await supabase
        .from('tags')
        .update({ name: editingTag.name, color: editingTag.color, icon: editingTag.icon })
        .eq('id', editingTag.id);

      if (error) throw error;
      toast.success('Tag updated successfully');
      setEditingTag(null);
      onRefresh();
    } catch (error) {
      toast.error('Failed to update tag');
      console.error(error);
    }
  };

  const handleDeleteTag = async (tagId: string) => {
    if (!confirm('Are you sure you want to delete this tag?')) return;

    try {
      const { error } = await supabase.from('tags').delete().eq('id', tagId);
      if (error) throw error;
      toast.success('Tag deleted successfully');
      onRefresh();
    } catch (error) {
      toast.error('Failed to delete tag');
      console.error(error);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-3">
          <h3 className="text-lg font-semibold">Manage Tags</h3>
          <Badge variant="secondary">{tags.length} Total</Badge>
        </div>
        <Button onClick={() => setShowAddTag(!showAddTag)}>
          {showAddTag ? 'Cancel' : 'Add New Tag'}
        </Button>
      </div>

      {showAddTag && (
        <Card className="p-4">
          <div className="space-y-3">
            <Input placeholder="Tag name" value={newTag.name} onChange={(e) => setNewTag({ ...newTag, name: e.target.value })} />
            <div className="flex gap-3">
              <Input type="color" value={newTag.color} onChange={(e) => setNewTag({ ...newTag, color: e.target.value })} className="w-20" />
              <Input placeholder="Icon" value={newTag.icon} onChange={(e) => setNewTag({ ...newTag, icon: e.target.value })} className="w-20" />
              <Button onClick={handleAddTag} className="flex-1">Create</Button>
            </div>
          </div>
        </Card>
      )}

      {tags.length === 0 ? (
        <Card className="p-8 text-center"><p className="text-gray-500">No tags found.</p></Card>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          {tags.map((tag) => (
            <Card key={tag.id} className="p-4">
              {editingTag?.id === tag.id ? (
                <div className="space-y-3">
                  <Input value={editingTag.name} onChange={(e) => setEditingTag({ ...editingTag, name: e.target.value })} />
                  <div className="flex gap-2">
                    <Input type="color" value={editingTag.color} onChange={(e) => setEditingTag({ ...editingTag, color: e.target.value })} className="w-16" />
                    <Input value={editingTag.icon} onChange={(e) => setEditingTag({ ...editingTag, icon: e.target.value })} className="w-16" />
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm" onClick={handleUpdateTag}>Save</Button>
                    <Button size="sm" variant="outline" onClick={() => setEditingTag(null)}>Cancel</Button>
                  </div>
                </div>
              ) : (
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-lg flex items-center justify-center text-lg" style={{ backgroundColor: tag.color }}>{tag.icon}</div>
                    <h4 className="font-semibold">{tag.name}</h4>
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm" variant="outline" onClick={() => setEditingTag(tag)}>Edit</Button>
                    <Button size="sm" variant="destructive" onClick={() => handleDeleteTag(tag.id)}>Delete</Button>
                  </div>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};

export default TagManagement;

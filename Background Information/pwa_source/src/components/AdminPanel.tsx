import React, { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import SystemStats from './admin/SystemStats';
import UserManagement from './admin/UserManagement';
import TagManagement from './admin/TagManagement';
import { supabase } from '@/lib/supabase';
import { toast } from 'sonner';

interface AdminPanelProps {
  onBack: () => void;
}

const AdminPanel: React.FC<AdminPanelProps> = ({ onBack }) => {
  const [stats, setStats] = useState({
    totalUsers: 0,
    totalMemories: 0,
    totalTags: 0,
    activeUsers: 0,
  });
  const [users, setUsers] = useState([]);
  const [tags, setTags] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      // Call edge function to fetch users with admin privileges
      const { data: usersResponse, error: usersError } = await supabase.functions.invoke('list-users');
      
      if (usersError) throw usersError;

      const usersData = usersResponse?.users || [];
      setUsers(usersData);

      // Fetch memories for stats
      const { data: memories, error: memoriesError } = await supabase
        .from('memories')
        .select('user_id');

      if (memoriesError) throw memoriesError;

      // Set stats
      setStats({
        totalUsers: usersData.length,
        totalMemories: memories?.length || 0,
        totalTags: 0,
        activeUsers: usersData.length,
      });

      // Load tags
      const { data: tagsData } = await supabase
        .from('tags')
        .select('*')
        .order('name');

      setTags(tagsData || []);
      setStats(prev => ({ ...prev, totalTags: tagsData?.length || 0 }));
    } catch (error) {
      console.error('Error loading admin data:', error);
      toast.error('Failed to load admin data');
    } finally {
      setLoading(false);
    }
  };


  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="bg-white border-b">
        <div className="max-w-7xl mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <Button variant="ghost" onClick={onBack}>←</Button>
              <h1 className="text-2xl font-bold">Admin Panel</h1>
            </div>
            <Button onClick={loadData} variant="outline">Refresh</Button>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 py-8">
        <div className="mb-8">
          <SystemStats stats={stats} />
        </div>

        <Tabs defaultValue="users" className="space-y-6">
          <TabsList>
            <TabsTrigger value="users">Users</TabsTrigger>
            <TabsTrigger value="tags">Tags</TabsTrigger>
          </TabsList>

          <TabsContent value="users">
            <UserManagement users={users} onRefresh={loadData} />
          </TabsContent>

          <TabsContent value="tags">
            <TagManagement tags={tags} onRefresh={loadData} />
          </TabsContent>

        </Tabs>

      </div>
    </div>
  );
};

export default AdminPanel;

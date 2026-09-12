import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { supabase } from '@/lib/supabase';
import { toast } from 'sonner';

interface User {
  id: string;
  email: string;
  full_name: string;
  is_admin: boolean;
  created_at: string;
  memory_count: number;
}

interface UserManagementProps {
  users: User[];
  onRefresh: () => void;
}

const UserManagement: React.FC<UserManagementProps> = ({ users, onRefresh }) => {
  const [searchTerm, setSearchTerm] = useState('');

  const filteredUsers = users.filter(user =>
    user.email?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    user.full_name?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const toggleAdmin = async (userId: string, currentStatus: boolean) => {
    try {
      const { error } = await supabase
        .from('profiles')
        .update({ is_admin: !currentStatus })
        .eq('id', userId);

      if (error) throw error;
      toast.success(`Admin status updated successfully`);
      onRefresh();
    } catch (error) {
      toast.error('Failed to update admin status');
      console.error(error);
    }
  };

  const deleteUser = async (userId: string) => {
    if (!confirm('Are you sure you want to delete this user? This will also delete all their memories.')) return;
    
    try {
      const { error } = await supabase
        .from('profiles')
        .delete()
        .eq('id', userId);

      if (error) throw error;
      toast.success('User deleted successfully');
      onRefresh();
    } catch (error) {
      toast.error('Failed to delete user');
      console.error(error);
    }
  };

  if (users.length === 0) {
    return (
      <Card className="p-8 text-center">
        <p className="text-gray-500 text-lg mb-2">No users found in the system.</p>
        <p className="text-gray-400 text-sm mb-4">Users will appear here once they sign up.</p>
        <p className="text-xs text-gray-400 font-mono">Check browser console for debugging info</p>
      </Card>
    );
  }


  return (
    <div className="space-y-4">
      <div className="flex gap-4 items-center justify-between">
        <Input
          placeholder="Search users by email or name..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="flex-1"
        />
        <Badge variant="secondary">{users.length} Total Users</Badge>
      </div>

      {filteredUsers.length === 0 ? (
        <Card className="p-8 text-center">
          <p className="text-gray-500">No users match your search.</p>
        </Card>
      ) : (
        <div className="space-y-3">
          {filteredUsers.map((user) => (
            <Card key={user.id} className="p-4">
              <div className="flex items-center justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-semibold text-gray-900">{user.full_name || 'No name'}</h3>
                    {user.is_admin && <Badge variant="secondary">Admin</Badge>}
                  </div>
                  <p className="text-sm text-gray-600">{user.email}</p>
                  <p className="text-xs text-gray-500 mt-1">
                    {user.memory_count} memories • Joined {new Date(user.created_at).toLocaleDateString()}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => toggleAdmin(user.id, user.is_admin)}
                  >
                    {user.is_admin ? 'Remove Admin' : 'Make Admin'}
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={() => deleteUser(user.id)}
                  >
                    Delete
                  </Button>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};

export default UserManagement;

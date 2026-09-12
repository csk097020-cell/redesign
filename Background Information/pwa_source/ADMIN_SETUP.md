# Admin Panel Setup Instructions

## Making a User an Admin

The admin panel button will only appear for users who have `is_admin = true` in their profile.

### Steps to Set Up Admin Access:

1. **Run the Migration** (if not already done):
   - The migration file `supabase/migrations/20240107_add_is_admin_column.sql` adds the `is_admin` column to the profiles table.

2. **Set a User as Admin via Supabase Dashboard**:
   - Go to your Supabase project dashboard
   - Navigate to the SQL Editor
   - Run this query (replace `USER_ID_HERE` with the actual user ID):
   
   ```sql
   UPDATE profiles SET is_admin = true WHERE id = 'USER_ID_HERE';
   ```

3. **Find Your User ID**:
   - Option A: Check the `auth.users` table in Supabase
   - Option B: Log in to your app and check the browser console for the user object
   - Option C: Run this query to see all users:
   
   ```sql
   SELECT id, email FROM auth.users;
   ```

4. **Verify Admin Status**:
   - Log out and log back in
   - You should now see a shield icon button in the top right corner of the dashboard
   - Click it to access the admin panel

### Troubleshooting:

- **Button still not visible?** 
  - Check browser console for errors
  - Verify the profiles table has the is_admin column
  - Make sure you're logged in with the correct user account
  
- **Profile doesn't exist?**
  - Create a profile entry manually:
  ```sql
  INSERT INTO profiles (id, is_admin) 
  VALUES ('USER_ID_HERE', true)
  ON CONFLICT (id) DO UPDATE SET is_admin = true;
  ```

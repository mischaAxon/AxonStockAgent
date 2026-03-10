import { useAdminUsers, useUpdateUser } from '../hooks/useApi';

const roleColors: Record<string, string> = {
  admin: 'bg-axon-600/20 text-axon-400',
  user: 'bg-gray-700/50 text-gray-400',
};

export default function AdminUsersPage() {
  const { data, isLoading, error } = useAdminUsers();
  const updateUser = useUpdateUser();

  const users = data?.data ?? [];

  function handleRoleToggle(id: string, currentRole: string) {
    const newRole = currentRole === 'admin' ? 'user' : 'admin';
    updateUser.mutate({ id, role: newRole });
  }

  function handleToggleActive(id: string, currentActive: boolean) {
    updateUser.mutate({ id, isActive: !currentActive });
  }

  return (
    <div>
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-white">Gebruikers</h1>
        <p className="text-gray-400 text-sm mt-1">Beheer gebruikersaccounts en rollen</p>
      </div>

      {isLoading && (
        <div className="text-gray-400 text-sm">Laden...</div>
      )}

      {error && (
        <div className="bg-red-500/10 border border-red-500/30 rounded-lg px-4 py-3 text-sm text-red-400">
          Fout bij laden: {error.message}
        </div>
      )}

      {!isLoading && !error && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-800 text-left">
                <th className="px-6 py-4 text-gray-400 font-medium">Naam</th>
                <th className="px-6 py-4 text-gray-400 font-medium">E-mail</th>
                <th className="px-6 py-4 text-gray-400 font-medium">Rol</th>
                <th className="px-6 py-4 text-gray-400 font-medium">Status</th>
                <th className="px-6 py-4 text-gray-400 font-medium">Laatst ingelogd</th>
                <th className="px-6 py-4 text-gray-400 font-medium">Acties</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {users.map((user: {
                id: string;
                email: string;
                displayName: string;
                role: string;
                isActive: boolean;
                createdAt: string;
                lastLoginAt: string | null;
              }) => (
                <tr key={user.id} className="hover:bg-gray-800/30 transition-colors">
                  <td className="px-6 py-4 text-white font-medium">{user.displayName || '—'}</td>
                  <td className="px-6 py-4 text-gray-300">{user.email}</td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${roleColors[user.role] ?? roleColors.user}`}>
                      {user.role}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                      user.isActive
                        ? 'bg-green-500/10 text-green-400'
                        : 'bg-red-500/10 text-red-400'
                    }`}>
                      {user.isActive ? 'Actief' : 'Inactief'}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-gray-400">
                    {user.lastLoginAt
                      ? new Date(user.lastLoginAt).toLocaleString('nl-NL')
                      : 'Nooit'}
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => handleRoleToggle(user.id, user.role)}
                        disabled={updateUser.isPending}
                        className="text-xs px-3 py-1.5 rounded-md bg-gray-800 hover:bg-gray-700 text-gray-300 transition-colors disabled:opacity-50"
                      >
                        {user.role === 'admin' ? 'Maak user' : 'Maak admin'}
                      </button>
                      <button
                        onClick={() => handleToggleActive(user.id, user.isActive)}
                        disabled={updateUser.isPending}
                        className={`text-xs px-3 py-1.5 rounded-md transition-colors disabled:opacity-50 ${
                          user.isActive
                            ? 'bg-red-500/10 hover:bg-red-500/20 text-red-400'
                            : 'bg-green-500/10 hover:bg-green-500/20 text-green-400'
                        }`}
                      >
                        {user.isActive ? 'Deactiveer' : 'Activeer'}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {users.length === 0 && (
            <div className="px-6 py-12 text-center text-gray-500 text-sm">Geen gebruikers gevonden</div>
          )}
        </div>
      )}
    </div>
  );
}

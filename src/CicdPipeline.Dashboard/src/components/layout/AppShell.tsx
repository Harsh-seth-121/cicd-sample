import { Link, Outlet } from 'react-router';
import { HealthIndicator } from './HealthIndicator';

export function AppShell() {
  return (
    <div className="min-h-screen bg-gray-50">
      <header className="border-b border-gray-200 bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
          <Link to="/" className="text-lg font-semibold text-gray-900">
            CI/CD Pipeline Dashboard
          </Link>
          <HealthIndicator />
        </div>
      </header>
      <main className="mx-auto max-w-7xl px-6 py-8">
        <Outlet />
      </main>
    </div>
  );
}

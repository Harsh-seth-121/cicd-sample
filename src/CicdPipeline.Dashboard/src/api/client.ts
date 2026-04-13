const BASE = '';

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`${res.status}: ${text}`);
  }
  return res.json();
}

export async function apiFetchText(path: string): Promise<string> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`${res.status}: ${res.statusText}`);
  return res.text();
}

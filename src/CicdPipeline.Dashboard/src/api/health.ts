import { apiFetchText } from './client';

export async function checkHealth(): Promise<boolean> {
  try {
    await apiFetchText('/health');
    return true;
  } catch {
    return false;
  }
}

export async function checkTemporalHealth(): Promise<boolean> {
  try {
    await apiFetchText('/health/temporal');
    return true;
  } catch {
    return false;
  }
}

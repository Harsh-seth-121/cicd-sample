import { apiFetch } from './client';
import type { StartPipelineResponse, WebhookPayload } from './types';

export function triggerPipeline(payload: WebhookPayload): Promise<StartPipelineResponse> {
  return apiFetch('/api/webhooks/generic', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

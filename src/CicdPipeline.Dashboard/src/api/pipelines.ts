import { apiFetch } from './client';
import type {
  PipelineListItem,
  PipelineStageStatus,
  SignalRequest,
  ResumeRequest,
} from './types';

export function listPipelines(params?: {
  repository?: string;
  status?: string;
}): Promise<PipelineListItem[]> {
  const qs = new URLSearchParams();
  if (params?.repository) qs.set('repository', params.repository);
  if (params?.status) qs.set('status', params.status);
  const query = qs.toString();
  return apiFetch(`/api/ops/pipelines${query ? `?${query}` : ''}`);
}

export function getPipelineStatus(workflowId: string): Promise<PipelineStageStatus> {
  return apiFetch(`/api/ops/pipelines/${encodeURIComponent(workflowId)}/status`);
}

export function pausePipeline(workflowId: string, reason: string) {
  const body: SignalRequest = { reason };
  return apiFetch(`/api/ops/pipelines/${encodeURIComponent(workflowId)}/pause`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function cancelPipeline(workflowId: string, reason: string) {
  const body: SignalRequest = { reason };
  return apiFetch(`/api/ops/pipelines/${encodeURIComponent(workflowId)}/cancel`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function resumePipeline(workflowId: string, operatorId: string, reason?: string) {
  const body: ResumeRequest = { operatorId, reason };
  return apiFetch(`/api/ops/pipelines/${encodeURIComponent(workflowId)}/resume`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

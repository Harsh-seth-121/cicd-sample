export type PipelineStatus =
  | 'Received'
  | 'Validating'
  | 'Building'
  | 'Testing'
  | 'Scanning'
  | 'Versioning'
  | 'Publishing'
  | 'DeployingDev'
  | 'VerifyingDev'
  | 'DeployingQa'
  | 'VerifyingQa'
  | 'Succeeded'
  | 'Failed'
  | 'Cancelled'
  | 'Paused'
  | 'Skipped';

export interface FailureEvidence {
  stage: string;
  reason: string;
  occurredAt: string;
  diagnosticData: Record<string, string>;
}

export interface PipelineStageStatus {
  pipelineId: string;
  status: PipelineStatus;
  currentStage: string;
  isPaused: boolean;
  isCancelled: boolean;
  lastUpdated: string;
  failures: FailureEvidence[];
}

export interface PipelineListItem {
  id: string;
  runId: string;
  status: string;
  startTime: string;
  closeTime: string | null;
}

export interface StartPipelineResponse {
  workflowId: string;
  runId: string;
}

export interface WebhookPayload {
  repository: string;
  commitSha: string;
  ref: string;
  eventType: string;
  senderLogin?: string;
}

export interface SignalRequest {
  reason: string;
}

export interface ResumeRequest {
  operatorId: string;
  reason?: string;
}

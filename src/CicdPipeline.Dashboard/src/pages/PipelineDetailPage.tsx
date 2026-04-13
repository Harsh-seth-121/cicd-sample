import { useParams, Link } from 'react-router';
import { usePipelineStatus } from '../hooks/usePipelineStatus';
import { PipelineDetail } from '../components/pipeline-detail/PipelineDetail';

export function PipelineDetailPage() {
  const { workflowId } = useParams<{ workflowId: string }>();
  const { data, isLoading, error } = usePipelineStatus(workflowId!);

  return (
    <div className="space-y-4">
      <Link to="/" className="text-sm text-blue-600 hover:underline">
        &larr; Back to pipelines
      </Link>

      {isLoading && (
        <div className="py-12 text-center text-gray-500">Loading pipeline status...</div>
      )}

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Failed to load pipeline: {error.message}
        </div>
      )}

      {data && <PipelineDetail data={data} />}
    </div>
  );
}

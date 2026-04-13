import type { FailureEvidence } from '../../api/types';

export function FailurePanel({ failures }: { failures: FailureEvidence[] }) {
  if (failures.length === 0) return null;

  return (
    <div className="rounded-lg border border-red-200 bg-red-50 p-4">
      <h3 className="mb-3 text-sm font-semibold text-red-800">Failure Evidence</h3>
      <div className="space-y-3">
        {failures.map((f, i) => (
          <div key={i} className="rounded bg-white p-3 shadow-sm">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-red-700">{f.stage}</span>
              <span className="text-xs text-gray-500">
                {new Date(f.occurredAt).toLocaleString()}
              </span>
            </div>
            <p className="mt-1 text-sm text-gray-700">{f.reason}</p>
            {Object.keys(f.diagnosticData).length > 0 && (
              <details className="mt-2">
                <summary className="cursor-pointer text-xs text-gray-500">
                  Diagnostic data
                </summary>
                <pre className="mt-1 overflow-x-auto rounded bg-gray-100 p-2 text-xs">
                  {JSON.stringify(f.diagnosticData, null, 2)}
                </pre>
              </details>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

export default function ScoreBar({ score, width }: { score: number; width?: string }) {
  const pct = Math.round(score * 100);
  const color = score >= 0.6 ? 'bg-green-500' : score >= 0.3 ? 'bg-amber-500' : 'bg-red-500';
  return (
    <div className="flex items-center gap-2">
      <div className={`${width ?? 'flex-1'} h-1.5 bg-gray-700 rounded-full overflow-hidden`}>
        <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="font-mono text-xs text-gray-300 w-8 text-right">{pct}%</span>
    </div>
  );
}

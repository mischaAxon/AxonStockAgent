export default function VerdictBadge({ verdict }: { verdict: string }) {
  const styles: Record<string, string> = {
    BUY:     'bg-green-500/20 text-green-400',
    SELL:    'bg-red-500/20 text-red-400',
    SQUEEZE: 'bg-amber-500/20 text-amber-400',
  };
  return (
    <span className={`px-2 py-0.5 rounded text-xs font-bold ${styles[verdict] ?? 'bg-gray-700 text-gray-300'}`}>
      {verdict}
    </span>
  );
}

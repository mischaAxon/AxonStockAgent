interface PillarProps {
  label: string;
  score: number | null;
  color: string;
  textColor: string;
}

function Pillar({ label, score, color, textColor }: PillarProps) {
  const available = score != null;
  const pct = available ? Math.round(score * 100) : 0;

  return (
    <div className="flex items-center gap-2">
      <span className={`text-[10px] font-mono w-10 ${available ? textColor : 'text-gray-600'}`}>
        {label}
      </span>
      <div className="flex-1 h-1.5 bg-gray-800 rounded-full overflow-hidden">
        {available && (
          <div
            className={`h-full rounded-full transition-all ${color}`}
            style={{ width: `${pct}%` }}
          />
        )}
      </div>
      <span className={`text-[10px] font-mono w-7 text-right ${available ? 'text-gray-300' : 'text-gray-700'}`}>
        {available ? pct : '—'}
      </span>
    </div>
  );
}

interface PillarScoreBarProps {
  techScore: number;
  sentimentScore: number | null;
  claudeConfidence: number | null;
  fundamentalsScore: number | null;
}

export default function PillarScoreBar({ techScore, sentimentScore, claudeConfidence, fundamentalsScore }: PillarScoreBarProps) {
  return (
    <div className="space-y-1">
      <Pillar label="Tech"  score={techScore}         color="bg-blue-500"   textColor="text-blue-400" />
      <Pillar label="Fund"  score={fundamentalsScore}  color="bg-green-500"  textColor="text-green-400" />
      <Pillar label="Sent"  score={sentimentScore}     color="bg-amber-500"  textColor="text-amber-400" />
      <Pillar label="AI"    score={claudeConfidence}   color="bg-purple-500" textColor="text-purple-400" />
    </div>
  );
}

/** Compacte variant: vier gekleurde dots voor gebruik in tiles */
export function PillarDots({
  techScore,
  sentimentScore,
  claudeConfidence,
  fundamentalsScore,
}: PillarScoreBarProps) {
  const dots = [
    { score: techScore,         color: 'bg-blue-400',   label: 'T' },
    { score: fundamentalsScore, color: 'bg-green-400',  label: 'F' },
    { score: sentimentScore,    color: 'bg-amber-400',  label: 'S' },
    { score: claudeConfidence,  color: 'bg-purple-400', label: 'A' },
  ];

  return (
    <div
      className="flex items-center gap-px mt-0.5"
      title={dots.map(d => `${d.label}: ${d.score != null ? Math.round(d.score * 100) + '%' : 'n/a'}`).join(' | ')}
    >
      {dots.map(d => (
        <span
          key={d.label}
          className={`w-1 h-1 rounded-full ${d.color}`}
          style={{ opacity: d.score != null ? Math.max(0.2, d.score) : 0.1 }}
        />
      ))}
    </div>
  );
}

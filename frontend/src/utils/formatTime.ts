export function relativeTime(dateStr: string): string {
  const now = Date.now();
  const date = new Date(dateStr).getTime();
  const diffMs = now - date;
  const diffMin = Math.floor(diffMs / 60000);
  const diffHr = Math.floor(diffMs / 3600000);
  const diffDay = Math.floor(diffMs / 86400000);

  if (diffMin < 1) return 'zojuist';
  if (diffMin < 60) return `${diffMin}m geleden`;
  if (diffHr < 24) return `${diffHr}u geleden`;
  if (diffDay === 1) return 'gisteren';
  if (diffDay < 7) return `${diffDay}d geleden`;
  return new Date(dateStr).toLocaleDateString('nl-NL');
}

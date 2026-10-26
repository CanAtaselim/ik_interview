import { Tag } from 'primereact/tag';

/**
 * Bir durumu renkli etiketle ve her zaman metniyle gösterir (yalnızca renge güvenilmez).
 * @param {{ value: string, map: Record<string, { label: string, severity: string }> }} props
 */
export default function StatusTag({ value, map }) {
  const status = map[value] ?? { label: value, severity: 'secondary' };

  return <Tag value={status.label} severity={status.severity} />;
}

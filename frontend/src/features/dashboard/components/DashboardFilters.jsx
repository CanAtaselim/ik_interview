import { Calendar } from 'primereact/calendar';
import { Dropdown } from 'primereact/dropdown';
import { SelectButton } from 'primereact/selectbutton';
import { useMemo } from 'react';
import DictionarySelect from '@/shared/components/DictionarySelect';
import { useOpenPositions } from '@/shared/recruitment/queries';
import '../charts/charts.css';

const RANGES = [
  { label: 'Son 30 gün', value: '30' },
  { label: 'Son 90 gün', value: '90' },
  { label: 'Bu yıl', value: 'year' },
  { label: 'Özel', value: 'custom' },
];

const pad = (value) => String(value).padStart(2, '0');

/** @param {Date} date "yyyy-MM-dd" (yerel gün) */
export const toIsoDate = (date) =>
  `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

const fromIsoDate = (iso) => {
  const [year, month, day] = iso.split('-').map(Number);
  return new Date(year, month - 1, day);
};

/**
 * Seçili dönem seçeneğini tarih aralığına çevirir. Özel aralık adres çubuğundaki from/to değerlerinden gelir.
 * @param {string} range @param {string | null} from @param {string | null} to @param {Date} [today]
 * @returns {{ from: string, to: string }}
 */
export function resolveRange(range, from, to, today = new Date()) {
  const end = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  if (range === 'custom' && from && to) return { from, to };
  if (range === 'year')
    return { from: toIsoDate(new Date(end.getFullYear(), 0, 1)), to: toIsoDate(end) };

  const days = range === '30' ? 30 : 90;
  const start = new Date(end);
  start.setDate(start.getDate() - (days - 1));
  return { from: toIsoDate(start), to: toIsoDate(end) };
}

/**
 * Dashboard filtre çubuğu: dönem, birim, pozisyon. Değerler adres çubuğunda tutulur (bağlantı paylaşılabilir).
 * @param {{ range: string, from: string | null, to: string | null, departmentId: string | null,
 *   positionId: string | null, onChange: (changes: Record<string, string | null>) => void }} props
 */
export default function DashboardFilters({ range, from, to, departmentId, positionId, onChange }) {
  const { data: positions = [] } = useOpenPositions();
  const positionOptions = useMemo(
    () =>
      positions
        .filter((position) => !departmentId || position.departmentId === departmentId)
        .map((position) => ({
          value: position.id,
          label: `${position.title} · ${position.departmentName}`,
        })),
    [positions, departmentId],
  );

  return (
    <div className="dashboard-filters" role="group" aria-label="Dashboard filtreleri">
      <SelectButton
        value={range}
        options={RANGES}
        allowEmpty={false}
        aria-label="Dönem"
        onChange={(event) =>
          event.value &&
          onChange({
            range: event.value,
            ...(event.value === 'custom' ? {} : { from: null, to: null }),
          })
        }
      />
      {range === 'custom' && (
        <Calendar
          selectionMode="range"
          value={from && to ? [fromIsoDate(from), fromIsoDate(to)] : null}
          dateFormat="dd.mm.yy"
          placeholder="Tarih aralığı seçin"
          maxDate={new Date()}
          showIcon
          readOnlyInput
          aria-label="Tarih aralığı"
          onChange={(event) => {
            const [start, end] = event.value ?? [];
            if (start && end) onChange({ from: toIsoDate(start), to: toIsoDate(end) });
          }}
        />
      )}
      <DictionarySelect
        type="Department"
        value={departmentId}
        placeholder="Tüm birimler"
        showClear
        onChange={(id) => onChange({ departmentId: id, positionId: null })}
      />
      <Dropdown
        value={positionId}
        options={positionOptions}
        placeholder="Tüm pozisyonlar"
        emptyMessage="Açık pozisyon yok."
        aria-label="Pozisyon"
        showClear
        filter
        filterInputAutoFocus
        onChange={(event) => onChange({ positionId: event.value ?? null })}
      />
    </div>
  );
}

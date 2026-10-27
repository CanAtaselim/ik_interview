import { Dropdown } from 'primereact/dropdown';
import { useDictionary } from '@/shared/hooks/useDictionary';

/**
 * Bir sözlükten tek öğe seçtiren açılır liste.
 * @param {{ type: string, value: string | null, onChange: (id: string | null) => void,
 *   inputId?: string, placeholder?: string, invalid?: boolean, showClear?: boolean,
 *   disabled?: boolean, onBlur?: () => void, valueBy?: 'id' | 'name' }} props
 * `valueBy="name"` seçilen öğenin kimliği yerine adını değer olarak taşır (ör. pozisyon adı).
 */
export default function DictionarySelect({
  type,
  value,
  onChange,
  inputId,
  placeholder = 'Seçin',
  invalid = false,
  showClear = false,
  disabled = false,
  onBlur,
  valueBy = 'id',
}) {
  const { data: loaded = [], isLoading } = useDictionary(type);
  // Listeden sonradan kaldırılmış veya pasif yapılmış bir değer de seçili görünsün.
  const items =
    valueBy === 'name' && value && !loaded.some((item) => item.name === value)
      ? [{ id: value, name: value }, ...loaded]
      : loaded;

  return (
    <Dropdown
      inputId={inputId}
      value={value || null}
      options={items}
      optionLabel="name"
      optionValue={valueBy}
      onChange={(event) => onChange(event.value ?? null)}
      onBlur={onBlur}
      placeholder={isLoading ? 'Yükleniyor…' : placeholder}
      emptyMessage="Bu listede henüz seçenek yok."
      invalid={invalid}
      showClear={showClear}
      disabled={disabled || isLoading}
      filter
      filterInputAutoFocus
    />
  );
}

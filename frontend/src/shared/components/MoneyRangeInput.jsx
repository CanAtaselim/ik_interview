import { InputNumber } from 'primereact/inputnumber';
import { SelectButton } from 'primereact/selectbutton';
import './MoneyRangeInput.css';

const NET_OPTIONS = [
  { label: 'Net', value: true },
  { label: 'Brüt', value: false },
];

/**
 * Tutar aralığı girişi (ör. 100.000 – 130.000 ₺ net). Alt değer tek başına da geçerlidir; üst değer
 * alt değer girilmeden açılmaz. Alt değer silinirse üst değer de temizlenir.
 * @param {{ value: { amount: number, currency: string, isNet: boolean } | null, max: number | null,
 *   onChange: (value: object | null) => void, onMaxChange: (max: number | null) => void,
 *   inputId?: string }} props
 */
export default function MoneyRangeInput({ value, max, onChange, onMaxChange, inputId }) {
  const isNet = value?.isNet ?? true;
  const isInverted = value && max !== null && max !== undefined && max < value.amount;

  return (
    <div className="money-range">
      <div className="money-range__fields">
        <InputNumber
          inputId={inputId}
          value={value?.amount ?? null}
          onValueChange={(event) => {
            if (event.value === null) {
              onChange(null);
              onMaxChange(null);
              return;
            }
            onChange({ amount: event.value, currency: 'TRY', isNet });
          }}
          min={0}
          locale="tr-TR"
          maxFractionDigits={2}
          suffix=" ₺"
          placeholder="En az"
          aria-label="Ücret beklentisi, en az"
        />
        <span className="money-range__dash" aria-hidden="true">
          –
        </span>
        <InputNumber
          inputId={inputId ? `${inputId}-max` : undefined}
          value={max ?? null}
          onValueChange={(event) => onMaxChange(event.value ?? null)}
          min={0}
          locale="tr-TR"
          maxFractionDigits={2}
          suffix=" ₺"
          placeholder="En çok (isteğe bağlı)"
          aria-label="Ücret beklentisi, en çok"
          disabled={!value}
          invalid={Boolean(isInverted)}
        />
        <SelectButton
          value={isNet}
          options={NET_OPTIONS}
          allowEmpty={false}
          disabled={!value}
          aria-label="Net mi brüt mü"
          onChange={(event) => onChange({ ...value, isNet: event.value })}
        />
      </div>
      {isInverted && (
        <small className="form-field__error" role="alert">
          Üst değer alt değerden küçük olamaz.
        </small>
      )}
    </div>
  );
}

import { InputNumber } from 'primereact/inputnumber';
import { SelectButton } from 'primereact/selectbutton';
import './MoneyInput.css';

const NET_OPTIONS = [
  { label: 'Net', value: true },
  { label: 'Brüt', value: false },
];

/**
 * Para tutarı girişi: tutar + net/brüt. Tutar boşsa değer `null` olur.
 * @param {{ value: { amount: number, currency: string, isNet: boolean } | null,
 *   onChange: (value: { amount: number, currency: string, isNet: boolean } | null) => void,
 *   inputId?: string }} props
 */
export default function MoneyInput({ value, onChange, inputId }) {
  const isNet = value?.isNet ?? true;

  return (
    <div className="money-input">
      <InputNumber
        inputId={inputId}
        value={value?.amount ?? null}
        onValueChange={(event) =>
          onChange(event.value === null ? null : { amount: event.value, currency: 'TRY', isNet })
        }
        min={0}
        locale="tr-TR"
        maxFractionDigits={2}
        suffix=" ₺"
        placeholder="Tutar"
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
  );
}

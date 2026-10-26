import { AutoComplete } from 'primereact/autocomplete';
import { useState } from 'react';
import { suggestUniversities } from '../universitySearch';

/**
 * Okul adı: Türkiye'deki üniversitelerden öneri çıkar ama listeden seçmek zorunlu değildir;
 * listede olmayan okul serbestçe yazılır.
 * @param {{ value: string, onChange: (value: string) => void, inputId?: string }} props
 */
export default function SchoolInput({ value, onChange, inputId }) {
  const [suggestions, setSuggestions] = useState([]);

  return (
    <AutoComplete
      inputId={inputId}
      value={value ?? ''}
      suggestions={suggestions}
      completeMethod={(event) => setSuggestions(suggestUniversities(event.query))}
      onChange={(event) => onChange(typeof event.value === 'string' ? event.value : '')}
      forceSelection={false}
      placeholder="Okul (listeden seçin veya yazın)"
      aria-label="Okul"
      autoHighlight
    />
  );
}

import { useEffect, useState } from 'react';

/**
 * Değer değişmeyi bıraktıktan `delay` ms sonra güncellenen kopyasını döner.
 * @template T @param {T} value @param {number} [delay] @returns {T}
 */
export function useDebounce(value, delay = 300) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);

  return debounced;
}

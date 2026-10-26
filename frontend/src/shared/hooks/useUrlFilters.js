import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { useDebounce } from './useDebounce';

/**
 * Liste sayfalarının filtrelerini, sayfa numarasını ve arama metnini adres çubuğunda tutar;
 * böylece bağlantı paylaşılabilir ve geri tuşu beklendiği gibi çalışır. Arama kutusu yazmayı
 * bıraktıktan sonra adrese (ve dolayısıyla sorguya) yansır.
 */
export function useUrlFilters() {
  const [params, setParams] = useSearchParams();
  const search = params.get('q') ?? '';
  const [searchText, setSearchText] = useState(search);
  const debouncedSearch = useDebounce(searchText);

  /**
   * Verilen parametreleri günceller; boş değerler adresten çıkarılır.
   * @param {Record<string, string | number | null | undefined>} changes
   * @param {{ replace?: boolean }} [options]
   */
  const updateParams = (changes, { replace = false } = {}) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        Object.entries(changes).forEach(([key, value]) => {
          if (value === null || value === undefined || value === '') next.delete(key);
          else next.set(key, value);
        });
        return next;
      },
      { replace },
    );

  useEffect(() => {
    if (debouncedSearch !== search)
      updateParams({ q: debouncedSearch, page: null }, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- yalnızca geciktirilmiş arama metni değişince
  }, [debouncedSearch]);

  return {
    params,
    search,
    searchText,
    setSearchText,
    page: Number(params.get('page')) || 1,
    updateParams,
  };
}

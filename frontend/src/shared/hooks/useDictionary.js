import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getDictionaryAdminItems, getDictionaryItems } from '@/shared/api/dictionaries';

export const dictionaryKeys = {
  all: ['dictionaries'],
  list: (type, includeInactive = false) => ['dictionaries', type, { includeInactive }],
};

/**
 * Bir sözlüğün öğeleri. Seçim listeleri yalnızca aktif öğeleri alır; sözlükler seyrek
 * değiştiği için uzun süre önbellekte kalır.
 * @param {string} type @param {{ includeInactive?: boolean }} [options]
 */
export function useDictionary(type, { includeInactive = false } = {}) {
  return useQuery({
    queryKey: dictionaryKeys.list(type, includeInactive),
    queryFn: () => getDictionaryItems(type, { includeInactive }),
    staleTime: 5 * 60_000,
  });
}

/**
 * Yönetim ekranının sayfalı listesi. Kayıt/güncelleme/silme sonrası `dictionaryKeys.all`
 * geçersiz kılınınca bu liste de yenilenir.
 * @param {string} type @param {{ search?: string, isActive?: boolean, page?: number, pageSize?: number }} filters
 */
export function useDictionaryAdminItems(type, filters) {
  return useQuery({
    queryKey: [...dictionaryKeys.all, 'admin', type, filters],
    queryFn: () => getDictionaryAdminItems(type, filters),
    placeholderData: keepPreviousData,
  });
}

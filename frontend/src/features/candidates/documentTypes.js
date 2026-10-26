import { useDictionary } from '@/shared/hooks/useDictionary';

/** Varsayılan dosya türünün sözlükteki adı; aday listesindeki CV simgesi de bu türü açar. */
export const CV_TYPE_NAME = 'CV';

/**
 * Seçilmiş dosya türü; seçim yoksa "CV" (o da yoksa listenin ilki).
 * @param {string | null} selectedId
 * @returns {{ typeId: string | null, hasTypes: boolean }}
 */
export function useDocumentType(selectedId) {
  const { data: types = [] } = useDictionary('DocumentType');
  const fallback = types.find((item) => item.name === CV_TYPE_NAME) ?? types[0];
  return { typeId: selectedId ?? fallback?.id ?? null, hasTypes: types.length > 0 };
}

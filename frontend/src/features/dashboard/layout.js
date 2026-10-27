/**
 * Dashboard kart düzeni: her grid için kartların sırası. Yalnızca kimlik sırası saklanır;
 * kart genişlikleri (altıda bir birimle) ayrıca saklanır; içerik koddan gelir. @typedef {{ grids?: Record<string, string[]>, widths?: Record<string, number> }} DashboardLayout
 */

/**
 * Kayıtlı sırayı koddaki kartlara uygular. Kayıtta olmayan (sonradan eklenmiş) kartlar sona,
 * kaldırılmış kartların kimlikleri yok sayılır; böylece yeni kart eklemek eski düzeni bozmaz.
 * @param {string[]} defaultIds koddaki varsayılan sıra @param {string[] | undefined} savedIds
 */
export function applyOrder(defaultIds, savedIds) {
  if (!savedIds?.length) return defaultIds;
  const known = new Set(defaultIds);
  const ordered = [...new Set(savedIds)].filter((id) => known.has(id));
  const placed = new Set(ordered);
  return [...ordered, ...defaultIds.filter((id) => !placed.has(id))];
}

/** Izgara altı sütunludur; kart genişliği sütun sayısıdır: 1/3, 1/2, 2/3, tam satır. */
export const CARD_WIDTHS = [
  { value: 2, label: '1/3', title: 'Dar (satırın üçte biri)' },
  { value: 3, label: '1/2', title: 'Yarım (iki kart yan yana)' },
  { value: 4, label: '2/3', title: 'Geniş (satırın üçte ikisi)' },
  { value: 6, label: 'Tam', title: 'Tam satır' },
];

/** Kayıtlı genişlik geçerli değilse kartın varsayılan genişliği kullanılır. @param {number} defaultWidth @param {unknown} saved */
export function resolveWidth(defaultWidth, saved) {
  return CARD_WIDTHS.some((option) => option.value === saved)
    ? /** @type {number} */ (saved)
    : defaultWidth;
}

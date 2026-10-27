import { createContext, useContext } from 'react';

/**
 * Kart düzeni bağlamı. `orderFor` bir grid'in kartlarını kayıtlı sırayla döner, `setOrder` yeni sırayı kaydeder.
 * Bağlam yoksa (ör. testte) kartlar koddaki sırayla, düzenlenemez biçimde görünür.
 * @type {import('react').Context<{ editing: boolean, orderFor: (gridId: string, ids: string[]) => string[], setOrder: (gridId: string, ids: string[]) => void, widthFor: (cardId: string, defaultWidth: number) => number, setWidth: (cardId: string, width: number) => void }>}
 */
export const DashboardLayoutContext = createContext({
  editing: false,
  orderFor: (_gridId, ids) => ids,
  setOrder: () => {},
  widthFor: (_cardId, defaultWidth) => defaultWidth,
  setWidth: () => {},
});

export const useDashboardLayoutContext = () => useContext(DashboardLayoutContext);

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

/** Kullanıcının arayüz tercihleri; tarayıcıda saklanır. */
export const useUiStore = create(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
    }),
    { name: 'recruitment-ui' },
  ),
);

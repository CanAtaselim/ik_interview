import { useEffect, useRef, useState } from 'react';

/**
 * Değer değişmeyi bıraktıktan `delay` ms sonra kendiliğinden kaydeder. Bileşen ekrandan
 * kalkarken bekleyen değişiklik varsa beklemeden kaydedilir; yazılan hiçbir şey kaybolmaz.
 *
 * @template T
 * @param {{ value: T, save: (value: T) => Promise<unknown>, delay?: number }} options
 *   `value` JSON'a çevrilebilir olmalı; değişip değişmediği JSON karşılaştırmasıyla anlaşılır.
 * @returns {{ status: 'idle' | 'saving' | 'saved' | 'error', savedAt: Date | null, error: unknown }}
 */
export function useAutosave({ value, save, delay = 2000 }) {
  const [state, setState] = useState({ status: 'idle', savedAt: null, error: null });
  const serialized = JSON.stringify(value);
  const lastSaved = useRef(serialized);
  const pending = useRef(null);
  const saveRef = useRef(save);

  useEffect(() => {
    saveRef.current = save;
  });

  useEffect(() => {
    if (serialized === lastSaved.current) return undefined;

    pending.current = serialized;
    const timer = setTimeout(async () => {
      pending.current = null;
      setState((current) => ({ ...current, status: 'saving' }));
      try {
        await saveRef.current(JSON.parse(serialized));
        lastSaved.current = serialized;
        setState({ status: 'saved', savedAt: new Date(), error: null });
      } catch (error) {
        setState({ status: 'error', savedAt: null, error });
      }
    }, delay);

    return () => clearTimeout(timer);
  }, [serialized, delay]);

  // Ekrandan kalkarken (sekme değişimi, sayfadan çıkış) bekleyen değişikliği hemen gönder.
  useEffect(
    () => () => {
      if (pending.current) saveRef.current(JSON.parse(pending.current)).catch(() => {});
    },
    [],
  );

  return state;
}

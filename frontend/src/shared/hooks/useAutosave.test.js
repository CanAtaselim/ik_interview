import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAutosave } from './useAutosave';

describe('useAutosave', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const setup = (save, initial = { note: '' }) =>
    renderHook(({ value }) => useAutosave({ value, save, delay: 2000 }), {
      initialProps: { value: initial },
    });

  it('başlangıç değerini kaydetmez', async () => {
    const save = vi.fn().mockResolvedValue();
    const { result } = setup(save);

    await act(() => vi.advanceTimersByTimeAsync(5000));

    expect(save).not.toHaveBeenCalled();
    expect(result.current.status).toBe('idle');
  });

  it('yazma durduktan sonra yalnızca son değeri bir kez kaydeder', async () => {
    const save = vi.fn().mockResolvedValue();
    const { result, rerender } = setup(save);

    rerender({ value: { note: 'a' } });
    await act(() => vi.advanceTimersByTimeAsync(1000));
    rerender({ value: { note: 'ab' } });
    await act(() => vi.advanceTimersByTimeAsync(1999));
    expect(save).not.toHaveBeenCalled();

    await act(() => vi.advanceTimersByTimeAsync(1));

    expect(save).toHaveBeenCalledTimes(1);
    expect(save).toHaveBeenCalledWith({ note: 'ab' });
    expect(result.current.status).toBe('saved');
    expect(result.current.savedAt).toBeInstanceOf(Date);
  });

  it('kaydedilen değere geri dönülürse yeniden kaydetmez', async () => {
    const save = vi.fn().mockResolvedValue();
    const { rerender } = setup(save);

    rerender({ value: { note: 'a' } });
    await act(() => vi.advanceTimersByTimeAsync(2000));
    rerender({ value: { note: 'ab' } });
    rerender({ value: { note: 'a' } });
    await act(() => vi.advanceTimersByTimeAsync(5000));

    expect(save).toHaveBeenCalledTimes(1);
  });

  it('kayıt başarısız olursa hatayı bildirir ve sonraki değişiklikte yeniden dener', async () => {
    const save = vi.fn().mockRejectedValueOnce(new Error('ağ hatası')).mockResolvedValue();
    const { result, rerender } = setup(save);

    rerender({ value: { note: 'a' } });
    await act(() => vi.advanceTimersByTimeAsync(2000));
    expect(result.current.status).toBe('error');
    expect(result.current.error.message).toBe('ağ hatası');

    rerender({ value: { note: 'ab' } });
    await act(() => vi.advanceTimersByTimeAsync(2000));

    expect(save).toHaveBeenLastCalledWith({ note: 'ab' });
    expect(result.current.status).toBe('saved');
  });

  it('ekrandan kalkarken bekleyen değişikliği beklemeden kaydeder', () => {
    const save = vi.fn().mockResolvedValue();
    const { rerender, unmount } = setup(save);

    rerender({ value: { note: 'yarım kalan not' } });
    unmount();

    expect(save).toHaveBeenCalledTimes(1);
    expect(save).toHaveBeenCalledWith({ note: 'yarım kalan not' });
  });

  it('bekleyen değişiklik yoksa ekrandan kalkarken kaydetmez', async () => {
    const save = vi.fn().mockResolvedValue();
    const { rerender, unmount } = setup(save);

    rerender({ value: { note: 'a' } });
    await act(() => vi.advanceTimersByTimeAsync(2000));
    unmount();

    expect(save).toHaveBeenCalledTimes(1);
  });
});

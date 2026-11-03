import { describe, expect, it } from 'vitest';
import { fileNameFrom } from './downloadFile';

describe('fileNameFrom', () => {
  it('Türkçe karakterli filename* değerini çözer', () => {
    expect(
      fileNameFrom(
        "attachment; filename=Aday-Zehra_Ko_-20261027.pdf; filename*=UTF-8''Aday-Zehra_Ko%C3%A7-20261027.pdf",
      ),
    ).toBe('Aday-Zehra_Koç-20261027.pdf');
  });

  it('yalnızca düz filename varsa onu kullanır', () => {
    expect(fileNameFrom('attachment; filename="rapor.xlsx"')).toBe('rapor.xlsx');
    expect(fileNameFrom('attachment; filename=rapor.xlsx')).toBe('rapor.xlsx');
  });

  it('başlık yoksa ya da bozuksa varsayılan ad verir', () => {
    expect(fileNameFrom(undefined)).toBe('rapor');
    expect(fileNameFrom("attachment; filename*=UTF-8''%E0%A4%A")).toBe('rapor');
  });
});

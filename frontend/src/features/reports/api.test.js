import { beforeEach, describe, expect, it, vi } from 'vitest';
import { downloadFile } from '@/shared/utils/downloadFile';
import { downloadListReport } from './api';

vi.mock('@/shared/utils/downloadFile', () => ({ downloadFile: vi.fn() }));

describe('downloadListReport', () => {
  beforeEach(() => vi.clearAllMocks());

  it('sends boolean filters as text so "false" survives the empty-value filter', () => {
    downloadListReport('candidates', { blacklisted: false, inTalentPool: true, city: 'Ankara' });

    expect(downloadFile).toHaveBeenCalledWith('/reports/lists/candidates', {
      blacklisted: 'false',
      inTalentPool: 'true',
      city: 'Ankara',
    });
  });

  it('passes unset filters through untouched', () => {
    downloadListReport('positions', { status: undefined, search: 'dev' });

    expect(downloadFile).toHaveBeenCalledWith('/reports/lists/positions', {
      status: undefined,
      search: 'dev',
    });
  });
});

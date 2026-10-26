import { AxiosError } from 'axios';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from '@/shared/auth/authStore';
import { authHttp, http, refreshSession } from './http';

const user = { id: '1', username: 'ikuzman', displayName: 'Mehmet Demir', roles: ['HrSpecialist'] };

function respond(config, status, data = null) {
  const response = { status, data, headers: {}, config, statusText: '' };
  if (status < 400) return Promise.resolve(response);
  return Promise.reject(new AxiosError('İstek başarısız', undefined, config, null, response));
}

/** API'yi taklit eder: yalnızca "gecerli" token'ı kabul eder. */
function fakeApi() {
  const calls = { api: [], refresh: 0 };

  http.defaults.adapter = (config) => {
    const token = config.headers.Authorization;
    calls.api.push(token);
    return respond(config, token === 'Bearer gecerli' ? 200 : 401, { ok: true });
  };

  return calls;
}

describe('http', () => {
  const originalAdapters = { http: http.defaults.adapter, auth: authHttp.defaults.adapter };

  beforeEach(() => {
    useAuthStore.setState({ status: 'authenticated', accessToken: 'eskimis', user });
  });

  afterEach(() => {
    http.defaults.adapter = originalAdapters.http;
    authHttp.defaults.adapter = originalAdapters.auth;
  });

  it('401 alan eşzamanlı istekler için oturumu bir kez yeniler ve hepsini tekrar dener', async () => {
    const calls = fakeApi();
    authHttp.defaults.adapter = (config) => {
      calls.refresh += 1;
      return respond(config, 200, { accessToken: 'gecerli', user });
    };

    const responses = await Promise.all([http.get('/a'), http.get('/b'), http.get('/c')]);

    expect(responses.map((response) => response.status)).toEqual([200, 200, 200]);
    expect(calls.refresh).toBe(1);
    expect(calls.api.filter((token) => token === 'Bearer gecerli')).toHaveLength(3);
    expect(useAuthStore.getState().accessToken).toBe('gecerli');
  });

  it('yenileme başarısız olursa oturumu temizler ve ilk hatayı iletir', async () => {
    fakeApi();
    authHttp.defaults.adapter = (config) => respond(config, 401);

    await expect(http.get('/a')).rejects.toMatchObject({ response: { status: 401 } });

    expect(useAuthStore.getState()).toMatchObject({
      status: 'anonymous',
      accessToken: null,
      user: null,
    });
  });

  it('yenilemeden sonra da 401 dönen isteği sonsuza kadar tekrar denemez', async () => {
    const calls = fakeApi();
    authHttp.defaults.adapter = (config) => {
      calls.refresh += 1;
      return respond(config, 200, { accessToken: 'hala-gecersiz', user });
    };

    await expect(http.get('/a')).rejects.toMatchObject({ response: { status: 401 } });

    expect(calls.refresh).toBe(1);
    expect(calls.api).toHaveLength(2);
  });

  it('401 dışındaki hatalarda oturumu yenilemez', async () => {
    const calls = { refresh: 0 };
    http.defaults.adapter = (config) => respond(config, 403);
    authHttp.defaults.adapter = (config) => {
      calls.refresh += 1;
      return respond(config, 200, { accessToken: 'gecerli', user });
    };

    await expect(http.get('/a')).rejects.toMatchObject({ response: { status: 403 } });

    expect(calls.refresh).toBe(0);
  });

  it('refreshSession eşzamanlı çağrılarda tek istek gönderir', async () => {
    let refreshCalls = 0;
    authHttp.defaults.adapter = (config) => {
      refreshCalls += 1;
      return respond(config, 200, { accessToken: 'gecerli', user });
    };

    await Promise.all([refreshSession(), refreshSession()]);

    expect(refreshCalls).toBe(1);
  });
});

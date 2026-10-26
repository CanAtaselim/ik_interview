// Demo veri betiklerinin ortak yardımcıları. Yalnızca geliştirme ortamı (Docker, sahte AD) içindir.
import { execFileSync } from 'node:child_process';

export const APP = process.env.APP_URL ?? 'http://localhost:8080';
export const API = `${APP}/api/v1`;
export const PASSWORD = 'Test123!';
export const DB_CONTAINER = process.env.DB_CONTAINER ?? 'recruitment-postgres';
export const API_CONTAINER = process.env.API_CONTAINER ?? 'recruitment-api';

export const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Verilen kullanıcıyla giriş yapar; istekleri onun adına yapan küçük bir istemci döner. */
export async function login(username) {
  const response = await fetch(`${API}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password: PASSWORD }),
  });
  if (!response.ok) {
    throw new Error(
      `${username} ile giriş yapılamadı (${response.status}). Uygulama açık mı ve Development/sahte AD ile mi çalışıyor?`,
    );
  }
  const session = await response.json();
  const headers = { Authorization: `Bearer ${session.accessToken}` };

  const call = async (method, path, body, { form } = {}) => {
    const init = { method, headers: { ...headers } };
    if (form) init.body = form;
    else if (body !== undefined) {
      init.headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(body);
    }
    const result = await fetch(API + path, init);
    const text = await result.text();
    if (!result.ok) throw new Error(`${username}: ${method} ${path} → ${result.status} ${text.slice(0, 300)}`);
    return text ? JSON.parse(text) : null;
  };

  return {
    id: session.user.id,
    name: session.user.displayName,
    get: (path) => call('GET', path),
    post: (path, body) => call('POST', path, body ?? {}),
    put: (path, body) => call('PUT', path, body),
    upload: (path, form) => call('POST', path, undefined, { form }),
  };
}

/** SQL'i veritabanı konteynerinde çalıştırır (yalnızca tarih geri alma ve temizlik için). */
export function sql(statement) {
  return execFileSync(
    'docker',
    ['exec', '-i', DB_CONTAINER, 'psql', '-U', 'recruitment', '-d', 'recruitment', '-v', 'ON_ERROR_STOP=1', '-At'],
    { input: statement, encoding: 'utf8' },
  ).trim();
}

export const quote = (value) => `'${String(value).replace(/'/g, "''")}'`;
export const uuidList = (ids) => (ids.length ? ids.map(quote).join(',') : `'00000000-0000-0000-0000-000000000000'`);

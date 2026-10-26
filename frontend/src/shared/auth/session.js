import { refreshSession } from '@/shared/api/http';

let bootstrapPromise = null;

/**
 * Uygulama açılışında bir kez çalışır: refresh cookie'si varsa oturumu geri yükler.
 * Kök rotanın loader'ıdır; bitene kadar hiçbir sayfa render edilmez, böylece korumalı
 * sayfalar oturum durumu belli olmadan girişe yönlendirmez.
 */
export function bootstrapSession() {
  bootstrapPromise ??= refreshSession().then(
    () => null,
    () => null,
  );
  return bootstrapPromise;
}

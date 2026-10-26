import StatusPage from './StatusPage';

/** Beklenmeyen bir render veya loader hatasında router'ın gösterdiği sayfa. */
export default function ErrorPage() {
  return (
    <StatusPage
      icon="pi pi-exclamation-triangle"
      title="Beklenmeyen bir hata oluştu"
      description="Sayfayı yenilemeyi deneyin. Sorun sürerse sistem yöneticinize haber verin."
    />
  );
}

import StatusPage from './StatusPage';

export default function ForbiddenPage() {
  return (
    <StatusPage
      icon="pi pi-lock"
      title="Bu sayfaya erişim yetkiniz yok"
      description="Erişmeniz gerektiğini düşünüyorsanız İK ekibiyle iletişime geçin."
    />
  );
}

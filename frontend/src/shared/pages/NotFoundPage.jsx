import StatusPage from './StatusPage';

export default function NotFoundPage() {
  return (
    <StatusPage
      icon="pi pi-compass"
      title="Aradığınız sayfa bulunamadı"
      description="Adres hatalı olabilir veya sayfa taşınmış olabilir."
    />
  );
}

import { addLocale, locale } from 'primereact/api';

/** PrimeReact bileşenlerinin (takvim, tablo filtresi, onay pencereleri) Türkçe metinleri. */
export function setupPrimeLocale() {
  addLocale('tr', {
    firstDayOfWeek: 1,
    dayNames: ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'],
    dayNamesShort: ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'],
    dayNamesMin: ['Pz', 'Pt', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct'],
    monthNames: [
      'Ocak',
      'Şubat',
      'Mart',
      'Nisan',
      'Mayıs',
      'Haziran',
      'Temmuz',
      'Ağustos',
      'Eylül',
      'Ekim',
      'Kasım',
      'Aralık',
    ],
    monthNamesShort: [
      'Oca',
      'Şub',
      'Mar',
      'Nis',
      'May',
      'Haz',
      'Tem',
      'Ağu',
      'Eyl',
      'Eki',
      'Kas',
      'Ara',
    ],
    today: 'Bugün',
    clear: 'Temizle',
    apply: 'Uygula',
    accept: 'Evet',
    reject: 'Hayır',
    choose: 'Seç',
    upload: 'Yükle',
    cancel: 'Vazgeç',
    close: 'Kapat',
    dateFormat: 'dd.mm.yy',
    weekHeader: 'Hf',
    emptyMessage: 'Kayıt bulunamadı',
    emptyFilterMessage: 'Sonuç bulunamadı',
    emptySearchMessage: 'Sonuç bulunamadı',
    emptySelectionMessage: 'Seçim yapılmadı',
    showPassword: 'Şifreyi göster',
    hidePassword: 'Şifreyi gizle',
  });

  locale('tr');
}

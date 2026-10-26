import { TURKEY_UNIVERSITIES } from '@/shared/data/turkeyUniversities';

/** Türkçe harfleri ve aksanları düz harfe indirir: "İstanbul Üniversitesi" → "istanbul universitesi". */
export function normalizeSearch(text) {
  return text
    .toLocaleLowerCase('tr')
    .replace(/ı/g, 'i')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .trim();
}

// Sık kullanılan kısaltmalar: "ODTÜ" yazan Orta Doğu Teknik Üniversitesi'ni bulur.
const ALIASES = {
  odtu: 'Orta Doğu Teknik Üniversitesi',
  metu: 'Orta Doğu Teknik Üniversitesi',
  itu: 'İstanbul Teknik Üniversitesi',
  ytu: 'Yıldız Teknik Üniversitesi',
  gtu: 'Gebze Teknik Üniversitesi',
  ktu: 'Karadeniz Teknik Üniversitesi',
  gsu: 'Galatasaray Üniversitesi',
  msgsu: 'Mimar Sinan Güzel Sanatlar Üniversitesi',
};

const INDEX = TURKEY_UNIVERSITIES.map((name) => ({ name, key: normalizeSearch(name) }));

/**
 * Okul adı önerileri: yazılan her kelimeyi içeren üniversiteler; adın başıyla eşleşenler önce gelir.
 * Liste yalnızca öneridir, listede olmayan okul da yazılabilir.
 * @param {string} query @param {number} [limit]
 * @returns {string[]}
 */
export function suggestUniversities(query, limit = 12) {
  const words = normalizeSearch(query ?? '')
    .split(/\s+/)
    .filter(Boolean);
  if (words.length === 0) return [];

  const alias = ALIASES[words.join('')];
  const matches = INDEX.filter((item) => words.every((word) => item.key.includes(word)))
    .sort((a, b) => Number(!a.key.startsWith(words[0])) - Number(!b.key.startsWith(words[0])))
    .map((item) => item.name);

  return [...new Set([...(alias ? [alias] : []), ...matches])].slice(0, limit);
}

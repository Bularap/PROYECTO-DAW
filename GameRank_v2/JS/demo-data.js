/* ===== Modo demo: juegos cargados a mano (sin API) =====
   Agregá, editá o borrá objetos de DEMO_GAMES para probar la grilla.
   Imágenes: dejá background_image en null para usar una portada generada,
   o poné una ruta propia, ej: 'imagenes/kcd.jpg'. */
const DEMO_GAMES = [
  { id: 1,  name: 'Kingdom Come: Deliverance', genres: ['RPG', 'Acción'],       released: '2018-02-13', rating: 4.2, ratings_count: 5400 },
  { id: 2,  name: 'Cyberpunk 2077',            genres: ['RPG', 'Acción'],       released: '2020-12-10', rating: 4.1, ratings_count: 9100 },
  { id: 3,  name: 'The Witcher 3',             genres: ['RPG', 'Aventura'],     released: '2015-05-19', rating: 4.7, ratings_count: 12000 },
  { id: 4,  name: 'Hollow Knight',             genres: ['Plataformas', 'Aventura'], released: '2017-02-24', rating: 4.5, ratings_count: 7300 },
  { id: 5,  name: 'Celeste',                   genres: ['Plataformas', 'Indie'],released: '2018-01-25', rating: 4.4, ratings_count: 4800 },
  { id: 6,  name: 'Hades',                     genres: ['Acción', 'Indie'],     released: '2020-09-17', rating: 4.6, ratings_count: 6900 },
  { id: 7,  name: 'Stardew Valley',            genres: ['Simulación', 'Indie'], released: '2016-02-26', rating: 4.5, ratings_count: 8200 },
  { id: 8,  name: 'Elden Ring',                genres: ['RPG', 'Acción'],       released: '2022-02-25', rating: 4.4, ratings_count: 11000 },
  { id: 9,  name: 'Portal 2',                  genres: ['Puzzle', 'Aventura'],  released: '2011-04-19', rating: 4.6, ratings_count: 10500 },
  { id: 10, name: 'Dark Souls III',            genres: ['RPG', 'Acción'],       released: '2016-03-24', rating: 4.3, ratings_count: 6000 },
  { id: 11, name: 'Disco Elysium',             genres: ['RPG', 'Indie'],        released: '2019-10-15', rating: 4.5, ratings_count: 3900 },
  { id: 12, name: 'Hotline Miami',             genres: ['Acción', 'Indie'],     released: '2012-10-23', rating: 4.2, ratings_count: 3500 },
  { id: 13, name: 'Civilization VI',           genres: ['Estrategia'],          released: '2016-10-21', rating: 4.0, ratings_count: 4100 },
  { id: 14, name: 'Forza Horizon 5',           genres: ['Carreras'],            released: '2021-11-09', rating: 4.3, ratings_count: 3300 }
];

const slug = s => s.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\s+/g, '-');

/** Genera una portada SVG (data URI) para juegos sin imagen. */
function portadaDemo(nombre, id) {
  const hue = (id * 47) % 360;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="320" height="200"><rect width="320" height="200" fill="hsl(${hue},35%,22%)"/><text x="160" y="105" fill="#e9e6df" font-family="Georgia,serif" font-size="20" text-anchor="middle">${nombre.replace(/&/g, '&amp;').replace(/</g, '&lt;')}</text></svg>`;
  return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
}

/** Imita las respuestas de RAWG: /genres y /games (search, genres, ordering, page, page_size). */
async function demoRawg(endpoint, p = {}) {
  await new Promise(r => setTimeout(r, 300));          // simula latencia de red
  if (endpoint === 'genres') {
    const nombres = [...new Set(DEMO_GAMES.flatMap(g => g.genres))].sort();
    return { results: nombres.map(n => ({ name: n, slug: slug(n) })) };
  }
  let list = DEMO_GAMES.map(g => ({
    ...g,
    genres: g.genres.map(n => ({ name: n, slug: slug(n) })),
    background_image: g.background_image || portadaDemo(g.name, g.id)
  }));
  if (p.search) list = list.filter(g => g.name.toLowerCase().includes(p.search.toLowerCase()));
  if (p.genres) list = list.filter(g => g.genres.some(x => x.slug === p.genres));
  const orden = {
    '-rating': (a, b) => b.rating - a.rating,
    '-released': (a, b) => b.released.localeCompare(a.released),
    'name': (a, b) => a.name.localeCompare(b.name),
    '-added': (a, b) => b.ratings_count - a.ratings_count
  }[p.ordering || '-added'];
  list.sort(orden);
  const size = Number(p.page_size) || 12, page = Number(p.page) || 1;
  return { results: list.slice((page - 1) * size, page * size), next: list.length > page * size ? 'siguiente' : null };
}

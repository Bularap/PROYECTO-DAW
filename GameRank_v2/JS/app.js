/* ===== GameRank – utilidades compartidas ===== */
const RAWG_KEY = 'PEGA_AQUI_TU_API_KEY';   // https://rawg.io/apidocs (gratis)
const USE_DEMO = true;   // true = juegos de js/demo-data.js (sin API) · false = RAWG real
const RAWG_URL = 'https://api.rawg.io/api';

const $ = (sel, root = document) => root.querySelector(sel);

/** Crea elementos del DOM de forma segura (textContent, nunca innerHTML con datos externos). */
function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v === null || v === false) continue;      // atributos booleanos: false/null = no se agrega
    if (v === true) node.setAttribute(k, '');
    else if (k === 'class') node.className = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v);
  }
  node.append(...children);
  return node;
}

/** Llama a la RAWG API con manejo de errores. */
async function rawg(endpoint, params = {}) {
  if (USE_DEMO) return demoRawg(endpoint, params);
  const url = new URL(`${RAWG_URL}/${endpoint}`);
  url.search = new URLSearchParams({ key: RAWG_KEY, ...params });
  let res;
  try { res = await fetch(url); }
  catch { throw new Error('No hay conexión con el servidor. Revisá tu internet.'); }
  if (res.status === 401) throw new Error('API key inválida. Revisá RAWG_KEY en js/app.js.');
  if (res.status === 429) throw new Error('Demasiadas consultas. Esperá un momento.');
  if (!res.ok) throw new Error(`Error del servidor (${res.status}).`);
  return res.json();
}

const stars = n => '★'.repeat(n) + '☆'.repeat(5 - n);
const debounce = (fn, ms = 400) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

/** Muestra un mensaje de estado (cargando / error / vacío) dentro de un contenedor. */
function showStatus(container, text, type = '') {
  container.replaceChildren(el('div', { class: `status ${type}`, role: type === 'error' ? 'alert' : 'status' }, text));
}

/* Menú hamburguesa (móvil) */
document.addEventListener('DOMContentLoaded', () => {
  const btn = $('.nav-toggle'), nav = $('.nav');
  if (!btn) return;
  btn.addEventListener('click', () => {
    const open = nav.classList.toggle('open');
    btn.setAttribute('aria-expanded', open);
  });
});

/* ===== Almacén de reviews =====
   Hoy usa localStorage para poder demostrar el DOM sin backend.
   Para el TP final: reemplazar el cuerpo de cada método por fetch() a tu API
   (POST/GET/DELETE /api/reviews) que guarde en SQL Server. La interfaz no cambia. */
const ReviewStore = {
  KEY: 'gamerank_reviews',
  async all() { return JSON.parse(localStorage.getItem(this.KEY) || '[]'); },
  async add(r) { const list = await this.all(); list.unshift({ ...r, id: Date.now(), fecha: new Date().toISOString() }); localStorage.setItem(this.KEY, JSON.stringify(list)); },
  async remove(id) { const list = (await this.all()).filter(r => r.id !== id); localStorage.setItem(this.KEY, JSON.stringify(list)); }
};

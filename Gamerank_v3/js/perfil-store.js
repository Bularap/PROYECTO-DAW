/* ===== Perfil, cuenta y biblioteca – almacenes + utilidades =====
   Misma idea que ReviewStore/WishlistStore de app.js: con USE_DEMO = true usan localStorage;
   con USE_DEMO = false llaman a la API (que ejecuta los procedimientos de /database).
   Las formas de datos replican los result sets de sp_ObtenerPerfil, para que el cambio no toque la interfaz. */
const API_URL = '/api';
const ESTADOS_DB = ['Jugado', 'Quiero jugarlo', 'Abandonado'];      // = tabla EstadoLista (ids 1, 2, 3)
const ID_ESTADO = Object.fromEntries(ESTADOS_DB.map((n, i) => [n, i + 1]));

async function req(ruta, metodo = 'GET', body) {
  const h = { 'Content-Type': 'application/json' };
  const t = localStorage.getItem('token'); if (t) h.Authorization = 'Bearer ' + t;
  let r;
  try { r = await fetch(API_URL + ruta, { method: metodo, headers: h, body: body && JSON.stringify(body) }); }
  catch { throw Object.assign(new Error('No hay conexión con el servidor. Revisá tu internet.'), { status: 0 }); }
  const d = await r.json().catch(() => ({}));
  if (!r.ok) throw Object.assign(new Error(d.mensaje || `Error del servidor (${r.status}).`), { status: r.status });
  return d;
}

const CuentaStore = {
  KEY: 'gamerank_cuenta',
  async get() {
    if (!USE_DEMO) return req('/me');
    return JSON.parse(localStorage.getItem(this.KEY) || JSON.stringify({
      nombreUsuario: 'jugador1', email: 'jugador@mail.com', avatarUrl: '', perfilPublico: true, mostrarListas: true, fechaRegistro: new Date().toISOString() }));
  },
  async guardar(datos) {                                   // PUT /api/me  (NombreUsuario, AvatarUrl, privacidad)
    if (!USE_DEMO) return req('/me', 'PUT', datos);
    localStorage.setItem(this.KEY, JSON.stringify({ ...(await this.get()), ...datos }));
  },
  async cambiarEmail(email, password) { if (!USE_DEMO) return req('/me/email', 'PUT', { email, password }); await this.guardar({ email }); },
  async cambiarPassword(actual, nueva) { if (!USE_DEMO) return req('/me/password', 'PUT', { actual, nueva }); },
  async eliminar() {                                       // derecho de supresión → sp_EliminarUsuario
    if (!USE_DEMO) return req('/me', 'DELETE');
    ['gamerank_cuenta', 'gamerank_reviews', 'gamerank_wishlist'].forEach(k => localStorage.removeItem(k));
  }
};

const PerfilStore = {
  async obtener(id) {                                      // GET /api/usuarios/{id}/perfil  → sp_ObtenerPerfil
    if (!USE_DEMO) return req('/usuarios/' + (id || 'me') + '/perfil');
    const c = await CuentaStore.get(), rs = await ReviewStore.all(), ws = await WishlistStore.all();
    const prom = rs.length ? rs.reduce((s, r) => s + r.puntaje, 0) / rs.length : null;
    return {
      esPropio: true,
      usuario: { NombreUsuario: c.nombreUsuario, AvatarUrl: c.avatarUrl, FechaRegistro: c.fechaRegistro,
                 PromedioOtorgado: prom, CantidadCalificaciones: rs.length, CantidadResenas: rs.length },
      estados: ESTADOS_DB.map((n, i) => ({ EstadoListaId: i + 1, Nombre: n, Cantidad: ws.filter(x => x.estado === n).length })),
      calificaciones: rs.map(r => ({ JuegoId: r.juegoId ?? r.id, Titulo: r.juego, PortadaUrl: '', Puntuacion: r.puntaje, Fecha: r.fecha })),
      resenas: rs.map(r => ({ ResenaId: r.id, JuegoId: r.juegoId ?? r.id, Titulo: r.juego, Puntuacion: r.puntaje, Contenido: r.texto, FechaPublicacion: r.fecha, Editada: false }))
    };
  },
  async eliminarResena(id) { if (!USE_DEMO) return req('/resenas/' + id, 'DELETE'); return ReviewStore.remove(id); }   // sp_EliminarResena
};

const BibliotecaStore = {                                  // items: { id, name, background_image, genres, released, rating, estado }
  async all() { return USE_DEMO ? WishlistStore.all() : req('/biblioteca'); },
  async setEstado(id, estado) { return USE_DEMO ? WishlistStore.setEstado(id, estado) : req('/biblioteca/' + id, 'PUT', { estadoListaId: ID_ESTADO[estado] }); },  // sp_AsignarEstadoBiblioteca
  async quitar(id) { return USE_DEMO ? WishlistStore.remove(id) : req('/biblioteca/' + id, 'DELETE'); }                                                   // sp_QuitarDeBiblioteca
};

/* ----- Utilidades de interfaz ----- */
function cabecera(actual) {
  const links = [['index.html', 'Juegos'], ['reviews.html', 'Reviews'], ['biblioteca.html', 'Mi biblioteca'], ['perfil.html', 'Perfil']];
  document.body.prepend(el('header', { class: 'site-header' }, el('div', { class: 'container' },
    el('a', { class: 'logo', href: 'index.html' }, 'Game', el('span', {}, 'Rank')),
    el('button', { class: 'nav-toggle', 'aria-expanded': 'false', 'aria-controls': 'nav' }, 'Menú'),
    el('nav', { class: 'nav', id: 'nav', 'aria-label': 'Principal' },
      ...links.map(([h, t]) => el('a', { href: h, 'aria-current': h === actual ? 'page' : null }, t))))));
}
function pie() {
  document.body.append(el('footer', { class: 'site-footer' }, el('div', { class: 'container' },
    el('p', {}, 'Datos de videojuegos provistos por ', el('a', { href: 'https://rawg.io', target: '_blank', rel: 'noopener' }, 'RAWG'), '.'),
    el('p', { class: 'privacy' }, 'Aviso de privacidad (Ley 25.326): los datos que ingreses se usan solo para gestionar tu cuenta, tus reseñas y tu lista de juegos. Podés acceder, rectificar o suprimir tus datos desde Configuración. La AAIP es el órgano de control de la ley.'))));
}
function aviso(texto, ok = false) { const a = $('#aviso'); a.textContent = texto; a.classList.toggle('ok', ok); }
function errorCampo(id, texto) {                           // marca el campo y muestra el error; devuelve true si es válido
  const i = $('#' + id); i.setAttribute('aria-invalid', String(!!texto)); $('#e-' + id).textContent = texto || ''; return !texto;
}
function campo(id, etiqueta, tipo, extra = {}) {
  return el('div', { class: 'field' }, el('label', { for: id }, etiqueta), el('input', { id, type: tipo, ...extra }), el('p', { class: 'field-error', id: 'e-' + id }));
}
function avatar(url, nombre, grande) {
  return el('div', { class: 'avatar' + (grande ? ' grande' : ''), 'aria-hidden': 'true' },
    url ? el('img', { src: url, alt: '', onerror: e => e.target.replaceWith((nombre || '?')[0].toUpperCase()) }) : (nombre || '?')[0].toUpperCase());
}
const EMAIL_OK = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;           // misma regla que CK_Usuario_Email
const USER_OK = /^[A-Za-z0-9_]{3,30}$/;                     // NombreUsuario NVARCHAR(30)
const PASS_OK = /^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/;
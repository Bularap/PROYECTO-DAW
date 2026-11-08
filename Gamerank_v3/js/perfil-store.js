async function req(ruta, metodo = 'GET', body) {
  return GameRankAuth.request(ruta, { method: metodo, body });
}

const CuentaStore = {
  validar(account, owner) {
    if (account?.usuarioId !== owner.usuarioId || typeof account.nombreUsuario !== 'string'
      || typeof account.email !== 'string' || !(account.avatarUrl === null || typeof account.avatarUrl === 'string')
      || !account.avatares || typeof account.avatares !== 'object' || Array.isArray(account.avatares)
      || !Object.entries(account.avatares).every(([id, url]) => id && typeof url === 'string' && /^https?:\/\//.test(url))) {
      throw GameRankApi.createError('El servidor devolvió datos de cuenta inesperados.', 0, 'response');
    }
    return account;
  },
  async get() {
    const requestedToken = GameRankAuth.token();
    await GameRankAuth.ready;
    const user = GameRankAuth.user();
    const token = GameRankAuth.token();
    if (!user || !token || requestedToken !== token) throw GameRankApi.createError('Ingresá para consultar tu cuenta.', 401, 'session');
    const [account, profile] = await Promise.all([GameRankAuth.request('/auth/cuenta'), GameRankData.profile()]);
    if (GameRankAuth.user()?.usuarioId !== user.usuarioId || GameRankAuth.token() !== token || profile.usuarioId !== user.usuarioId) {
      throw GameRankApi.createError('La sesión cambió. Volvé a cargar tu cuenta.', 401, 'session');
    }
    return { ...this.validar(account, user), rol: user.rol, fechaRegistro: profile.fechaRegistro };
  },
  async guardar(nombreUsuario, avatarId) {
    const requestedToken = GameRankAuth.token();
    await GameRankAuth.ready;
    const user = GameRankAuth.user();
    if (!user || !requestedToken || requestedToken !== GameRankAuth.token()) throw GameRankApi.createError('La sesión cambió. Volvé a cargar tu perfil.', 401, 'session');
    const account = await GameRankAuth.request('/auth/perfil', { method: 'PUT', body: { nombreUsuario, avatarId } });
    if (GameRankAuth.token() !== requestedToken || GameRankAuth.user()?.usuarioId !== user.usuarioId) throw GameRankApi.createError('La sesión cambió. Volvé a cargar tu perfil.', 401, 'session');
    return this.validar(account, user);
  },
  async cambiarEmail() { throw GameRankApi.createError('El servidor todavía no permite cambiar el correo.', 0, 'unavailable'); },
  cambiarPassword: (passwordActual, passwordNueva) => GameRankAuth.changePassword(passwordActual, passwordNueva),
  eliminar: password => GameRankAuth.deleteAccount(password)
};

const PerfilStore = {
  obtener: id => GameRankData.profile(id),
  eliminarResena: id => GameRankData.removeReview(id)
};

const BibliotecaStore = {
  all: () => GameRankData.library(),
  estados: () => GameRankData.states(),
  setEstado: (id, estadoListaId) => GameRankData.setState(id, estadoListaId),
  quitar: id => GameRankData.remove(id)
};

/* ----- Utilidades de interfaz ----- */
function cabecera(actual) {
  const links = [['index.html', 'Juegos'], ['reviews.html', 'Reviews'], ['biblioteca.html', 'Mi biblioteca'], ['perfil.html', 'Perfil']];
  document.body.prepend(el('header', { class: 'site-header' }, el('div', { class: 'container' },
    el('a', { class: 'logo', href: 'index.html' }, 'Game', el('span', {}, 'Rank')),
    el('button', { class: 'nav-toggle', 'aria-expanded': 'false', 'aria-controls': 'nav' }, 'Menú'),
    el('nav', { class: 'nav', id: 'nav', 'aria-label': 'Principal' },
      ...links.map(([h, t]) => el('a', { href: h, 'aria-current': h === actual ? 'page' : null }, t)),
      el('div', { class: 'auth-nav', id: 'auth-nav' })))));
  document.querySelector('.site-header').after(el('p', { class: 'container session-status', id: 'session-status', role: 'status', hidden: true }));
}
function pie() {
  document.body.append(el('footer', { class: 'site-footer' }, el('div', { class: 'container' },
    el('p', {}, 'Datos de videojuegos provistos por ', el('a', { href: 'https://store.steampowered.com', target: '_blank', rel: 'noopener' }, 'Steam'), '.'),
    el('p', { class: 'privacy' }, el('a', { href: 'reviews.html#privacidad' }, 'Aviso de privacidad'), '. Podés editar tu nombre y avatar desde tu perfil, cambiar la contraseña y solicitar la baja desde Configuración. El cambio de correo y las preferencias de privacidad todavía no están disponibles.'))));
}
function aviso(texto, ok = false) { const a = $('#aviso'); a.textContent = texto; a.classList.toggle('ok', ok); }
function errorCampo(id, texto) {                           // marca el campo y muestra el error; devuelve true si es válido
  const i = $('#' + id); i.setAttribute('aria-invalid', String(!!texto)); $('#e-' + id).textContent = texto || ''; return !texto;
}
function campo(id, etiqueta, tipo, extra = {}) {
  return el('div', { class: 'field' }, el('label', { for: id }, etiqueta), el('input', { id, type: tipo, 'aria-describedby': 'e-' + id, ...extra }), el('p', { class: 'field-error', id: 'e-' + id }));
}
function avatar(url, nombre, grande) {
  return el('div', { class: 'avatar' + (grande ? ' grande' : ''), 'aria-hidden': 'true' },
    url ? el('img', { src: url, alt: '', onerror: e => e.target.replaceWith((nombre || '?')[0].toUpperCase()) }) : (nombre || '?')[0].toUpperCase());
}
const EMAIL_OK = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;           // misma regla que CK_Usuario_Email
const USER_OK = /^[A-Za-z0-9_]{3,30}$/;                     // NombreUsuario NVARCHAR(30)
const PASS_OK = /^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/;

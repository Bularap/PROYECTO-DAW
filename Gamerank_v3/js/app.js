const $ = (sel, root = document) => root.querySelector(sel);

function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(props)) {
    if (v === null || v === false) continue;
    if (v === true) node.setAttribute(k, '');
    else if (k === 'class') node.className = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v);
  }
  node.append(...children);
  return node;
}

const stars = n => '★'.repeat(n) + '☆'.repeat(5 - n);
const debounce = (fn, ms = 400) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

function showStatus(container, text, type = '') {
  container.replaceChildren(el('div', { class: `status ${type}`, role: type === 'error' ? 'alert' : 'status' }, text));
}

function avisoTemporal(nodo, texto, ms = 3500) {
  nodo.textContent = texto;
  clearTimeout(nodo._temporizador);
  nodo._temporizador = setTimeout(() => { if (nodo.textContent === texto) nodo.textContent = ''; }, ms);
}

const PAGINAS = [
  ['index.html', 'Juegos'],
  ['reviews.html', 'Reviews'],
  ['biblioteca.html', 'Mi biblioteca'],
  ['diario.html', 'Diario'],
  ['perfil.html', 'Perfil']
];
const PAGINA_DEL_MENU = { 'juego.html': 'index.html','editar-perfil.html': 'perfil.html', 'configuracion.html': 'perfil.html', '': 'index.html' };

function sesionRecordada() {
  try {
    if (!sessionStorage.getItem('gamerank_token')) return null;
    return JSON.parse(sessionStorage.getItem('gamerank_token_usuario'));
  } catch { return null; }
}

function accesos(usuario, actual) {
  if (usuario?.nombreUsuario) {
    return [el('a', { class: 'session-user', href: 'perfil.html' }, usuario.nombreUsuario),
      el('button', { type: 'button', class: 'btn btn-ghost' }, 'Cerrar sesión')];
  }
  return [el('a', { href: 'login.html', 'aria-current': actual === 'login.html' ? 'page' : null }, 'Ingresar'),
    el('a', { href: 'register.html', 'aria-current': actual === 'register.html' ? 'page' : null }, 'Crear cuenta')];
}

function cabecera() {
  const archivo = location.pathname.split('/').pop();
  const actual = PAGINA_DEL_MENU[archivo] || archivo;
  const usuario = sesionRecordada();
  const esAdmin = ['Administrador', 'SuperAdministrador'].includes(usuario?.rol);
  const boton = el('button', { class: 'nav-toggle', type: 'button', 'aria-expanded': 'false', 'aria-controls': 'nav' }, 'Menú');
  const nav = el('nav', { class: 'nav', id: 'nav', 'aria-label': 'Principal' },
    ...PAGINAS.map(([href, texto]) => el('a', { href, 'aria-current': href === actual ? 'page' : null }, texto)),
    el('a', { href: 'admin.html', id: 'nav-admin', hidden: !esAdmin, 'aria-current': actual === 'admin.html' ? 'page' : null }, 'Administración'),
    el('div', { class: 'auth-nav', id: 'auth-nav' }, ...accesos(usuario, archivo)));
  boton.addEventListener('click', () => boton.setAttribute('aria-expanded', String(nav.classList.toggle('open'))));
  document.body.prepend(el('header', { class: 'site-header' },
    el('div', { class: 'container' }, el('a', { class: 'logo', href: 'index.html' }, 'Game', el('span', {}, 'Rank')), boton, nav)),
    el('p', { class: 'container session-status', id: 'session-status', role: 'status', hidden: true }));
}

cabecera();

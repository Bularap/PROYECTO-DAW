(() => {
  pie();
  const main = $('#main');
  main.append(el('div', { class: 'page-head' }, el('h1', {}, 'Mi biblioteca'),
    el('p', { class: 'meta' }, 'Tus juegos y sus estados actuales. Cambiá el estado o quitalos cuando quieras.')),
    el('p', { id: 'aviso-bib', class: 'aviso-flotante', role: 'status', 'aria-live': 'polite' }),
    el('div', { class: 'tabs', id: 'tabs', role: 'group', 'aria-label': 'Filtrar por estado' }),
    el('div', { id: 'lista', class: 'grid', 'aria-live': 'polite' }));
  const list = $('#lista');
  const tabs = $('#tabs');
  const notice = $('#aviso-bib');
  const fallback = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="440" height="275"><rect width="100%" height="100%" fill="#1f2630"/><text x="50%" y="50%" text-anchor="middle" fill="#9aa4b2" font-family="sans-serif" font-size="22">Sin portada</text></svg>');
  let states = [];
  let games = [];
  let filter = 0;
  let version = 0;
  const date = value => GameRankData.date(value)?.toLocaleDateString('es-AR') || 'Fecha no disponible';

  function render() {
    tabs.replaceChildren(...[{ estadoListaId: 0, nombre: 'Todos' }, ...states].map(state => {
      const count = state.estadoListaId ? games.filter(game => game.estadoListaId === state.estadoListaId).length : games.length;
      return el('button', { type: 'button', class: 'tab', 'aria-pressed': String(filter === state.estadoListaId),
        onclick: () => { filter = state.estadoListaId; render(); } }, `${state.nombre} (${count})`);
    }));
    const items = filter ? games.filter(game => game.estadoListaId === filter) : games;
    if (items.length) list.replaceChildren(...items.map(card));
    else showStatus(list, games.length ? 'No tenés juegos en este estado.' : 'Tu biblioteca está vacía. Agregá juegos desde la sección Juegos con el botón «+ Agregar».');
  }

  function card(game) {
    const cover = el('img', { src: game.portadaUrl || fallback, alt: `Portada de ${game.titulo}`, loading: 'lazy' });
    cover.addEventListener('error', () => { cover.src = fallback; }, { once: true });
    const select = el('select', { id: `estado-${game.juegoId}` }, ...states.map(state =>
      el('option', { value: state.estadoListaId, selected: state.estadoListaId === game.estadoListaId }, state.nombre)));
    const remove = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Quitar');
    async function mutate(action) {
      const owner = GameRankAuth.token();
      select.disabled = true;
      remove.disabled = true;
      try {
        await action();
        if (GameRankAuth.token() !== owner) return;
        avisoTemporal(notice, 'Biblioteca actualizada.');
        await load();
        $(`#estado-${game.juegoId}`)?.focus();
      } catch (error) {
        if (GameRankAuth.token() !== owner) return;
        select.value = String(game.estadoListaId);
        avisoTemporal(notice, error.message, 8000);
      } finally { select.disabled = false; remove.disabled = false; }
    }
    select.addEventListener('change', () => mutate(() => BibliotecaStore.setEstado(game.juegoId, Number(select.value))));
    remove.addEventListener('click', () => {
      if (confirm('¿Quitar este juego de tu biblioteca?')) mutate(() => BibliotecaStore.quitar(game.juegoId));
    });
    return el('article', { class: 'card' }, cover, el('div', { class: 'card-body' }, el('h3', {}, game.titulo),
      el('p', { class: 'meta' }, `Última actualización en biblioteca: ${date(game.fecha)}`),
      el('label', { class: 'sr-only', for: `estado-${game.juegoId}` }, `Estado de ${game.titulo}`), select,
      el('div', { class: 'card-actions' }, el('a', { class: 'btn', href: `reviews.html?juegoId=${game.juegoId}` }, 'Escribir reseña'), remove)));
  }

  async function load() {
    const current = ++version;
    games = [];
    tabs.replaceChildren();
    showStatus(list, 'Cargando tu biblioteca…');
    list.setAttribute('aria-busy', 'true');
    try {
      const result = await Promise.all([BibliotecaStore.all(), BibliotecaStore.estados()]);
      if (current !== version) return;
      [games, states] = result;
      render();
    } catch (error) {
      if (current !== version) return;
      showStatus(list, error.message, 'error');
      list.firstChild.append(' ', error.status === 401 ? el('a', { class: 'btn', href: 'login.html' }, 'Ingresar')
        : el('button', { class: 'btn btn-ghost', type: 'button', onclick: load }, 'Reintentar'));
    } finally { if (current === version) list.removeAttribute('aria-busy'); }
  }

  window.addEventListener('gamerank:sessionchange', () => { games = []; filter = 0; notice.textContent = ''; load(); });
  GameRankAuth.ready.then(load);
  window.addEventListener('pageshow', event => { if (event.persisted) load(); });
})();

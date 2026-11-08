(() => {
  const list = $('#lista');
  const tabs = $('#tabs');
  const notice = $('#wishlist-aviso');
  const fallback = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="440" height="275"><rect width="100%" height="100%" fill="#1f2630"/><text x="50%" y="50%" text-anchor="middle" fill="#9aa4b2" font-family="sans-serif" font-size="22">Sin portada</text></svg>');
  let states = [];
  let games = [];
  let filter = 0;
  let version = 0;

  function message(text, error = false) {
    notice.textContent = text;
    notice.className = error ? 'field-error' : 'meta';
  }

  function render() {
    tabs.replaceChildren(...[{ estadoListaId: 0, nombre: 'Todos' }, ...states].map(state => {
      const count = state.estadoListaId ? games.filter(game => game.estadoListaId === state.estadoListaId).length : games.length;
      return el('button', {
        type: 'button', class: 'tab', 'aria-pressed': String(filter === state.estadoListaId),
        onclick: () => { filter = state.estadoListaId; render(); }
      }, `${state.nombre} (${count})`);
    }));
    const items = filter ? games.filter(game => game.estadoListaId === filter) : games;
    if (items.length) list.replaceChildren(...items.map(card));
    else showStatus(list, games.length ? 'No tenés juegos en este estado.' : 'Tu lista está vacía. Agregá juegos desde la sección Juegos.');
  }

  function card(game) {
    const cover = el('img', { src: game.portadaUrl || fallback, alt: `Portada de ${game.titulo}`, loading: 'lazy' });
    cover.addEventListener('error', () => { cover.src = fallback; }, { once: true });
    const select = el('select', { id: `estado-${game.juegoId}` }, ...states.map(state =>
      el('option', { value: state.estadoListaId, selected: state.estadoListaId === game.estadoListaId }, state.nombre)));
    const remove = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Quitar');
    async function mutate(action) {
      const owner = GameRankAuth.user()?.usuarioId;
      select.disabled = true;
      remove.disabled = true;
      try {
        await action();
        if (GameRankAuth.user()?.usuarioId !== owner) return;
        message('Lista actualizada.');
        await load();
        $(`#estado-${game.juegoId}`)?.focus();
      } catch (error) {
        if (GameRankAuth.user()?.usuarioId !== owner) return;
        select.value = String(game.estadoListaId);
        message(error.message, true);
      } finally {
        select.disabled = false;
        remove.disabled = false;
      }
    }
    select.addEventListener('change', () => mutate(() => GameRankData.setState(game.juegoId, Number(select.value))));
    remove.addEventListener('click', () => {
      if (confirm('¿Quitar este juego de tu lista?')) mutate(() => GameRankData.remove(game.juegoId));
    });
    return el('article', { class: 'card' }, cover,
      el('div', { class: 'card-body' },
        el('h3', {}, game.titulo),
        el('p', { class: 'meta' }, `En tu lista desde ${new Date(game.fecha).toLocaleDateString('es-AR')}`),
        el('label', { class: 'sr-only', for: `estado-${game.juegoId}` }, `Estado de ${game.titulo}`), select,
        el('div', { class: 'card-actions' },
          el('a', { class: 'btn', href: `reviews.html?juegoId=${game.juegoId}` }, 'Escribir reseña'), remove)));
  }

  async function load() {
    const current = ++version;
    games = [];
    tabs.replaceChildren();
    showStatus(list, 'Cargando tu lista…');
    list.setAttribute('aria-busy', 'true');
    try {
      const data = await Promise.all([GameRankData.states(), GameRankData.library()]);
      if (current !== version) return;
      [states, games] = data;
      render();
    } catch (error) {
      if (current !== version) return;
      showStatus(list, error.message, 'error');
      if (error.status === 401) list.firstChild.append(' ', el('a', { class: 'btn', href: 'login.html' }, 'Ingresar'));
      else list.firstChild.append(' ', el('button', { type: 'button', class: 'btn btn-ghost', onclick: load }, 'Reintentar'));
    } finally {
      if (current === version) list.removeAttribute('aria-busy');
    }
  }

  window.addEventListener('gamerank:sessionchange', () => { games = []; filter = 0; message(''); load(); });
  GameRankAuth.ready.then(load);
  window.addEventListener('pageshow', event => { if (event.persisted) load(); });
})();

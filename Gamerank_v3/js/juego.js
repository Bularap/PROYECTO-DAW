(() => {
  const container = $('#ficha');
  const notice = $('#ficha-aviso');
  const reviewsSection = $('#ficha-resenas');
  const list = $('#lista');
  const pager = $('#resenas-pager');
  const pageSize = 10;
  const fallbackCover = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="440" height="275"><rect width="100%" height="100%" fill="#1f2630"/><text x="50%" y="50%" text-anchor="middle" fill="#9aa4b2" font-family="sans-serif" font-size="22">Sin portada</text></svg>');
  const id = Number(new URLSearchParams(location.search).get('juegoId'));
  let game = null;
  let states = [];
  let current = null;
  let page = 1;
  let feedVersion = 0;
  let stateVersion = 0;

  function names(items) {
    return Array.isArray(items) ? items.map(item => item.nombre).filter(Boolean).join(', ') : '';
  }

  function formatDate(value) {
    return value ? value.split('T')[0].split('-').reverse().join('/') : '';
  }

  function dataRow(label, value) {
    return value ? [el('dt', {}, label), el('dd', {}, value)] : [];
  }

  function ratingBlock() {
    if (!(game.cantidadCalificaciones > 0) || typeof game.promedioCalificacion !== 'number') {
      return el('p', { class: 'meta' }, 'Todavía no tiene calificaciones en GameRank.');
    }
    return el('p', { class: 'ficha-puntaje' },
      el('span', { class: 'stars', 'aria-hidden': 'true' }, stars(Math.round(game.promedioCalificacion))),
      el('strong', {}, ` ${game.promedioCalificacion.toFixed(1)}/5`),
      el('span', { class: 'meta' }, ` · ${game.cantidadCalificaciones} ${game.cantidadCalificaciones === 1 ? 'calificación' : 'calificaciones'}`));
  }

  function librarySelect() {
    const user = GameRankAuth.user();
    if (!user) return el('a', { class: 'btn btn-ghost', href: 'login.html' }, 'Ingresá para guardarlo en tu biblioteca');
    const selected = current?.estadoListaId || null;
    const select = el('select', { class: 'estado-select', 'aria-label': `Estado de ${game.titulo} en mi biblioteca`, disabled: !current || !states.length },
      el('option', { value: '', disabled: true, selected: !selected }, current ? '+ Agregar a mi biblioteca' : 'Cargando…'),
      ...states.map(state => el('option', { value: state.estadoListaId, selected: state.estadoListaId === selected }, state.nombre)));
    if (selected) select.append(el('option', { value: 'remove' }, 'Quitar de la biblioteca'));
    select.addEventListener('change', async () => {
      const chosen = select.value;
      select.disabled = true;
      try {
        if (chosen === 'remove') await GameRankData.remove(game.juegoId);
        else await GameRankData.setState(game.juegoId, Number(chosen));
        current = { ...current, estadoListaId: chosen === 'remove' ? null : Number(chosen) };
        avisoTemporal(notice, chosen === 'remove' ? 'Juego quitado de tu biblioteca.' : 'Biblioteca actualizada.');
      } catch (error) {
        avisoTemporal(notice, error.message, 8000);
      }
      renderActions();
    });
    return select;
  }

  function renderActions() {
    const actions = $('#ficha-acciones');
    if (!actions) return;
    const own = current?.puntuacion ? el('span', { class: 'meta' }, `Tu puntuación: ${stars(current.puntuacion)}`) : null;
    actions.replaceChildren(librarySelect(), ...(own ? [own] : []));
  }

  function render() {
    const cover = el('img', { class: 'ficha-portada', src: game.portadaUrl || fallbackCover, alt: `Portada de ${game.titulo}` });
    cover.addEventListener('error', () => { cover.src = fallbackCover; }, { once: true });
    const details = el('dl', { class: 'ficha-datos' },
      ...dataRow('Lanzamiento', formatDate(game.fechaLanzamiento) || 'Fecha no disponible'),
      ...dataRow('Géneros', names(game.generos)),
      ...dataRow('Plataformas', names(game.plataformas)),
      ...dataRow('Desarrolladora', names(game.desarrolladoras)),
      ...dataRow('Metacritic', game.metacritic ? `${game.metacritic}/100` : ''));
    const steamLink = Number.isInteger(game.steamAppId) && game.steamAppId > 0
      ? el('a', { class: 'btn btn-ghost', href: `https://store.steampowered.com/app/${game.steamAppId}/`, target: '_blank', rel: 'noopener' }, 'Ver en Steam')
      : null;
    container.replaceChildren(el('article', { class: 'ficha' }, cover,
      el('div', { class: 'ficha-info' },
        el('h1', {}, game.titulo),
        ratingBlock(),
        el('p', { class: 'ficha-sinopsis' }, game.sinopsis || 'Steam no informa una sinopsis para este juego.'),
        details,
        el('div', { class: 'card-actions', id: 'ficha-acciones' }),
        el('div', { class: 'card-actions' },
          el('a', { class: 'btn', href: `reviews.html?juegoId=${game.juegoId}` }, 'Reseñar y puntuar'),
          ...(steamLink ? [steamLink] : [])))));
    renderActions();
  }

  async function loadPersonal() {
    const version = ++stateVersion;
    current = null;
    renderActions();
    if (!GameRankAuth.user()) return;
    try {
      const data = await GameRankData.state(game.juegoId);
      if (version !== stateVersion) return;
      current = data || {};
    } catch (error) {
      if (version !== stateVersion) return;
      current = {};
      avisoTemporal(notice, `No pudimos consultar tu biblioteca: ${error.message}`, 8000);
    }
    renderActions();
  }

  function review(item) {
    const score = item.puntuacion == null ? 'Sin calificar' : `${stars(item.puntuacion)} · ${item.puntuacion}/5`;
    const date = GameRankData.date(item.fechaPublicacion);
    return el('article', { class: 'review' },
      el('div', { class: 'stars' }, score),
      el('p', {}, item.contenido),
      el('p', { class: 'meta' }, `${item.nombreUsuario}${date ? ` · ${date.toLocaleDateString('es-AR')}` : ''}${item.editada ? ' · Editada' : ''}`),
      GameRankReportes.control(item));
  }

  async function loadReviews() {
    const version = ++feedVersion;
    pager.replaceChildren();
    showStatus(list, 'Cargando reseñas…');
    try {
      const data = await GameRankApi.request(`/juegos/${game.juegoId}/resenas?pagina=${page}&tamanioPagina=${pageSize}`, { authenticated: false });
      if (version !== feedVersion) return;
      if (!Array.isArray(data?.resenas) || !Number.isInteger(data.total)) throw GameRankApi.createError('El servidor devolvió datos inesperados.', 0, 'response');
      if (data.resenas.length) list.replaceChildren(...data.resenas.map(review));
      else showStatus(list, 'Todavía nadie reseñó este juego. ¡Sé el primero!');
      if (data.total > pageSize) pager.replaceChildren(
        el('button', { type: 'button', class: 'btn btn-ghost', disabled: page <= 1, onclick: () => { page--; loadReviews(); } }, 'Anterior'),
        el('span', { class: 'meta' }, `Página ${page} de ${Math.ceil(data.total / pageSize)}`),
        el('button', { type: 'button', class: 'btn btn-ghost', disabled: page * pageSize >= data.total, onclick: () => { page++; loadReviews(); } }, 'Siguiente'));
    } catch (error) {
      if (version !== feedVersion) return;
      showStatus(list, error.message, 'error');
      list.firstChild.append(' ', el('button', { type: 'button', class: 'btn btn-ghost', onclick: loadReviews }, 'Reintentar'));
    }
  }

  async function load() {
    if (!Number.isInteger(id) || id < 1) {
      showStatus(container, 'El juego solicitado no es válido.', 'error');
      return;
    }
    container.setAttribute('aria-busy', 'true');
    showStatus(container, 'Cargando ficha del juego…');
    try {
      const data = await GameRankData.game(id);
      if (!Number.isInteger(data?.juegoId) || typeof data.titulo !== 'string') throw GameRankApi.createError('El servidor devolvió datos inesperados.', 0, 'response');
      game = data;
      document.title = `${game.titulo} · GameRank`;
      $('#escribir').href = `reviews.html?juegoId=${game.juegoId}`;
      render();
      reviewsSection.hidden = false;
      loadReviews();
      GameRankData.states().then(data => { states = data; renderActions(); }).catch(() => {});
      await GameRankAuth.ready;
      loadPersonal();
    } catch (error) {
      showStatus(container, error.status === 404 ? 'No encontramos ese juego en el catálogo.' : error.message, 'error');
      if (error.status !== 404) container.firstChild.append(' ', el('button', { type: 'button', class: 'btn btn-ghost', onclick: load }, 'Reintentar'));
    } finally {
      container.removeAttribute('aria-busy');
    }
  }

  window.addEventListener('gamerank:sessionchange', () => { if (game) { loadPersonal(); loadReviews(); } });
  load();
})();

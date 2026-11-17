(() => {
  const results = $('#resultados');
  const pager = $('#pager');
  const notice = $('#catalogo-aviso');
  const personalNotice = $('#personal-aviso');
  const genreSelect = $('#genero');
  const orderSelect = $('#orden');
  const search = $('#q');
  const pageSize = 12;
  const fallbackCover = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="440" height="275"><rect width="100%" height="100%" fill="#1f2630"/><text x="50%" y="50%" text-anchor="middle" fill="#9aa4b2" font-family="sans-serif" font-size="22">Sin portada</text></svg>');
  let page = 1;
  let hasMore = true;
  let loading = false;
  let searchVersion = 0;
  let games = [];
  let states = [];
  let saved = new Map();
  let personalUserId = null;
  let personalVersion = 0;
  let personalLoading = false;

  function catalogError() {
    return GameRankApi.createError('El servidor devolvió datos de catálogo inesperados.', 0, 'response');
  }

  function showNotice(container, message, error = false) {
    if (message) showStatus(container, message, error ? 'error' : '');
    else container.replaceChildren();
  }

  function renderGames() {
    if (games.length) results.replaceChildren(...games.map(card));
  }

  function librarySelect(game) {
    const selected = saved.get(game.juegoId);
    const select = el('select', {
      class: 'estado-select',
      'aria-label': `Estado de ${game.titulo} en mi biblioteca`,
      disabled: personalLoading || states.length === 0
    },
    el('option', { value: '', disabled: true, selected: !selected }, personalLoading ? 'Cargando…' : '+ Agregar'),
    ...states.map(state => el('option', {
      value: state.estadoListaId,
      selected: state.estadoListaId === selected
    }, state.nombre)));
    if (selected) select.append(el('option', { value: 'remove' }, 'Quitar de la biblioteca'));
    select.addEventListener('change', async () => {
      const user = GameRankAuth.user();
      if (!user) {
        location.href = 'login.html';
        return;
      }
      const previous = saved.get(game.juegoId);
      const chosen = select.value;
      select.disabled = true;
      personalNotice.textContent = '';
      try {
        await GameRankAuth.request(`/juegos/${game.juegoId}/biblioteca`, chosen === 'remove'
          ? { method: 'DELETE' }
          : { method: 'PUT', body: { estadoListaId: Number(chosen) } });
        if (GameRankAuth.user()?.usuarioId !== user.usuarioId) return;
        if (chosen === 'remove') saved.delete(game.juegoId);
        else saved.set(game.juegoId, Number(chosen));
        avisoTemporal(personalNotice, chosen === 'remove' ? 'Juego quitado de tu biblioteca.' : 'Biblioteca actualizada.');
        renderGames();
      } catch (error) {
        select.value = previous === undefined ? '' : String(previous);
        avisoTemporal(personalNotice, error.message, 8000);
        select.disabled = false;
      }
    });
    return select;
  }

  function card(game) {
    const genres = game.generos.map(genre => genre.nombre).slice(0, 2).join(', ') || 'Sin género';
    const release = game.fechaLanzamiento
      ? game.fechaLanzamiento.split('T')[0].split('-').reverse().join('/')
      : 'Fecha no disponible';
    const cover = el('img', { src: game.portadaUrl || fallbackCover, alt: `Portada de ${game.titulo}`, loading: 'lazy' });
    cover.addEventListener('error', () => { cover.src = fallbackCover; }, { once: true });
    const rated = game.cantidadCalificaciones > 0 && typeof game.promedioCalificacion === 'number';
    const rating = rated ? el('p', { class: 'meta' },
      el('span', { class: 'stars', 'aria-hidden': 'true' }, stars(Math.round(game.promedioCalificacion))),
      ` ${game.promedioCalificacion.toFixed(1)}/5 (${game.cantidadCalificaciones})`)
      : el('p', { class: 'meta' }, 'Sin calificaciones');
    const detail = `juego.html?juegoId=${game.juegoId}`;
    return el('article', { class: 'card' }, el('a', { href: detail, tabindex: '-1', 'aria-hidden': 'true' }, cover),
      el('div', { class: 'card-body' },
        el('h3', {}, el('a', { class: 'card-link', href: detail }, game.titulo)),
        el('p', { class: 'meta' }, `${genres} · ${release}`), rating,
        el('div', { class: 'card-actions' },
          el('a', { class: 'btn', href: `reviews.html?juegoId=${game.juegoId}`, title: 'Escribir o editar una reseña de este juego' }, 'Reseñas'),
          librarySelect(game))));
  }

  async function loadGenres() {
    genreSelect.disabled = true;
    try {
      const data = await GameRankApi.request('/generos', { authenticated: false });
      if (!Array.isArray(data) || data.some(genre => typeof genre.slug !== 'string' || typeof genre.nombre !== 'string')) throw catalogError();
      genreSelect.append(...data.map(genre => el('option', { value: genre.slug }, genre.nombre)));
      genreSelect.disabled = false;
    } catch (error) {
      genreSelect.options[0].textContent = 'Géneros no disponibles';
      genreSelect.title = error.message;
    }
  }

  async function loadStates() {
    try {
      const data = await GameRankApi.request('/estados-lista', { authenticated: false });
      if (!Array.isArray(data) || data.some(state => !Number.isInteger(state.estadoListaId) || typeof state.nombre !== 'string')) throw catalogError();
      states = data;
      renderGames();
    } catch (error) {
      avisoTemporal(personalNotice, `El catálogo sigue disponible. No pudimos cargar los estados de tu lista: ${error.message}`, 8000);
    }
  }

  async function syncPersonalLibrary() {
    const user = GameRankAuth.user();
    const userId = user?.usuarioId || 0;
    if (personalUserId === userId) return;
    personalUserId = userId;
    const version = ++personalVersion;
    saved = new Map();
    personalLoading = !!user;
    personalNotice.textContent = '';
    renderGames();
    if (!user) return;
    try {
      const data = await GameRankAuth.request(`/usuarios/${userId}/biblioteca`);
      if (!Array.isArray(data) || data.some(item => !Number.isInteger(item.juegoId) || !Number.isInteger(item.estadoListaId))) throw catalogError();
      if (version !== personalVersion) return;
      saved = new Map(data.map(item => [item.juegoId, item.estadoListaId]));
    } catch (error) {
      if (version === personalVersion) avisoTemporal(personalNotice, `El catálogo sigue disponible. No pudimos consultar tu biblioteca: ${error.message}`, 8000);
    } finally {
      if (version === personalVersion) {
        personalLoading = false;
        renderGames();
      }
    }
  }

  function validPage(data) {
    return Array.isArray(data?.juegos) && Number.isInteger(data.total) && data.total >= 0
      && Number.isInteger(data.pagina) && data.pagina >= 1 && Number.isInteger(data.tamanioPagina) && data.tamanioPagina >= 1
      && !data.juegos.some(game => !Number.isInteger(game.juegoId) || typeof game.titulo !== 'string'
        || !Array.isArray(game.generos) || game.generos.some(genre => typeof genre.nombre !== 'string')
        || (game.promedioCalificacion !== null && (typeof game.promedioCalificacion !== 'number' || game.promedioCalificacion < 1 || game.promedioCalificacion > 5)));
  }

  function goToSearch() {
    window.scrollTo({ top: 0 });
    search.focus({ preventScroll: true });
  }

  function showEnd() {
    pager.replaceChildren(el('p', { class: 'meta fin-catalogo', role: 'status' },
      el('button', { type: 'button', class: 'btn-link', onclick: goToSearch }, search.value.trim() ? 'Buscá otro juego' : 'Buscá un juego'),
      ' para ver más resultados.'));
  }

  function checkEnd() {
    if (hasMore && !loading && pager.getBoundingClientRect().top < innerHeight + 600) loadPage();
  }

  async function loadPage() {
    const version = searchVersion;
    const first = page === 1;
    loading = true;
    if (first) {
      results.setAttribute('aria-busy', 'true');
      showStatus(results, 'Cargando juegos…');
    } else {
      pager.replaceChildren(el('p', { class: 'meta', role: 'status' }, 'Cargando más juegos…'));
    }
    const query = search.value.trim();
    const params = new URLSearchParams({ orden: orderSelect.value, pagina: page, tamanioPagina: pageSize });
    if (query) params.set('texto', query);
    if (genreSelect.value) params.set('generos', genreSelect.value);
    try {
      const data = await GameRankApi.request(`/juegos?${params}`, { authenticated: false, timeout: 30000 });
      if (version !== searchVersion) return;
      if (!validPage(data)) throw catalogError();
      const known = new Set(games.map(game => game.juegoId));
      const fresh = data.juegos.filter(game => !known.has(game.juegoId));
      games.push(...fresh);
      if (data.parcial) showNotice(notice, 'Steam no está disponible. Mostramos los resultados guardados en GAME RANK; podrían faltar juegos.');
      hasMore = data.juegos.length > 0 && page * data.tamanioPagina < data.total;
      if (first && !games.length) {
        showStatus(results, data.parcial
          ? 'No hay resultados guardados para esta búsqueda. Probá más tarde o cambiá los filtros.'
          : 'No encontramos juegos con esos filtros. Probá otra búsqueda.');
        pager.replaceChildren();
        return;
      }
      if (first) renderGames();
      else results.append(...fresh.map(card));
      page++;
      if (hasMore) pager.replaceChildren();
      else showEnd();
    } catch (error) {
      if (version !== searchVersion) return;
      hasMore = false;
      const retry = el('button', { type: 'button', class: 'btn btn-ghost', onclick: () => { hasMore = true; loadPage(); } }, 'Reintentar');
      if (first) {
        showStatus(results, error.message, 'error');
        results.firstChild.append(' ', retry);
      } else {
        showStatus(pager, error.message, 'error');
        pager.firstChild.append(' ', retry);
      }
    } finally {
      if (version === searchVersion) {
        loading = false;
        results.removeAttribute('aria-busy');
        setTimeout(checkEnd);
      }
    }
  }

  function findGames() {
    searchVersion++;
    page = 1;
    games = [];
    hasMore = true;
    loading = false;
    pager.replaceChildren();
    showNotice(notice, '');
    const query = search.value.trim();
    $('#titulo-lista').textContent = query ? `Resultados para “${query}”` : orderSelect.selectedOptions[0].textContent;
    loadPage();
  }

  window.addEventListener('scroll', checkEnd, { passive: true });
  window.addEventListener('resize', checkEnd);

  $('#filtros').addEventListener('submit', event => {
    event.preventDefault();
    findGames();
  });
  search.addEventListener('input', debounce(findGames));
  genreSelect.addEventListener('change', findGames);
  orderSelect.addEventListener('change', findGames);
  window.addEventListener('gamerank:sessionchange', syncPersonalLibrary);
  GameRankAuth.ready.then(syncPersonalLibrary);
  loadGenres();
  loadStates();
  findGames();
})();

(() => {
  const entries = $('#entradas');
  const list = $('#lista-perfil');
  const tabs = $('#tabs-lista');
  const notice = $('#diario-aviso');
  let profile = null;
  let games = [];
  let states = [];
  let filter = 0;
  let version = 0;
  const date = value => GameRankData.date(value)?.toLocaleDateString('es-AR') || 'Fecha no disponible';

  function renderStats() {
    const stats = [[profile.cantidadResenas, 'Reseñas'],
      [profile.promedioOtorgado == null ? '–' : Number(profile.promedioOtorgado).toFixed(1), 'Puntaje promedio'],
      [profile.cantidadCalificaciones, 'Juegos calificados'], [games.length, 'En mi biblioteca']];
    $('#stats').replaceChildren(...stats.map(([count, title]) => el('div', { class: 'stat' }, el('strong', {}, String(count)), el('span', {}, title))));
    $('#dist').replaceChildren(...[5, 4, 3, 2, 1].map(score => {
      const count = profile.calificaciones.filter(game => game.puntuacion === score).length;
      const width = profile.calificaciones.length ? count / profile.calificaciones.length * 100 : 0;
      return el('div', { class: 'dist-row' }, el('span', { class: 'stars' }, `${score}★`),
        el('div', { class: 'dist-bar', role: 'img', 'aria-label': `${count} calificaciones de ${score} estrellas` },
          el('i', { style: `width:${width}%` })), el('span', {}, String(count)));
    }));
  }

  function renderEntries() {
    if (!profile) return;
    const activity = [
      ...profile.resenas.map(review => ({ kind: 'Reseña publicada', title: review.titulo, date: review.fechaPublicacion,
        text: `${review.contenido}${review.editada ? ' (editada; fecha de edición no disponible)' : ''}`, score: null })),
      ...profile.calificaciones.map(game => ({ kind: 'Última calificación', title: game.titulo, date: game.fecha,
        text: 'Puntuación actual del juego.', score: game.puntuacion })),
      ...games.map(game => ({ kind: 'Última actualización de lista', title: game.titulo, date: game.fecha,
        text: `Estado actual: ${game.estado}. Esta fecha no indica cuándo jugaste o terminaste el juego.`, score: null }))
    ];
    if (!activity.length) return showStatus(entries, 'Todavía no hay reseñas, calificaciones ni juegos en tu lista.');
    const mode = $('#orden').value;
    const stamp = value => GameRankData.date(value)?.getTime() ?? -Infinity;
    activity.sort((a, b) => {
      if (mode !== 'recientes') {
        if (a.score == null && b.score != null) return 1;
        if (a.score != null && b.score == null) return -1;
        if (a.score != null && b.score != null && a.score !== b.score) return mode === 'mejor' ? b.score - a.score : a.score - b.score;
      }
      const left = stamp(a.date), right = stamp(b.date);
      return left === right ? 0 : right > left ? 1 : -1;
    });
    const nodes = [];
    let previousMonth = '';
    for (const item of activity) {
      const time = GameRankData.date(item.date);
      const month = time?.toLocaleDateString('es-AR', { month: 'long', year: 'numeric' }) || 'Sin fecha disponible';
      if (mode === 'recientes' && month !== previousMonth) {
        previousMonth = month;
        nodes.push(el('h3', { class: 'month' }, month));
      }
      const row = el('article', { class: 'entry' },
        el('div', { class: 'day', 'aria-hidden': 'true' }, time ? String(time.getDate()) : '–'),
        el('div', {}, el('h3', {}, item.title), el('p', { class: 'meta' }, `${item.kind} · ${date(item.date)}`), el('p', {}, item.text)));
      if (item.score != null) row.append(el('span', { class: 'stars', role: 'img', 'aria-label': `${item.score} de 5 estrellas` }, stars(item.score)));
      nodes.push(row);
    }
    entries.replaceChildren(...nodes);
  }

  function renderList() {
    tabs.replaceChildren(...[{ estadoListaId: 0, nombre: 'Todos' }, ...states].map(state => {
      const count = state.estadoListaId ? games.filter(game => game.estadoListaId === state.estadoListaId).length : games.length;
      return el('button', { class: 'tab', type: 'button', 'aria-pressed': String(filter === state.estadoListaId),
        onclick: () => { filter = state.estadoListaId; renderList(); } }, `${state.nombre} (${count})`);
    }));
    const items = filter ? games.filter(game => game.estadoListaId === filter) : games;
    if (!items.length) return showStatus(list, games.length ? 'No hay juegos en este estado.' : 'No hay juegos en tu lista todavía.');
    list.replaceChildren(...items.map(game => {
      const card = el('article', { class: 'card' });
      if (game.portadaUrl) {
        const cover = el('img', { src: game.portadaUrl, alt: `Portada de ${game.titulo}`, loading: 'lazy' });
        cover.addEventListener('error', () => cover.replaceWith(el('p', { class: 'meta' }, 'Portada no disponible')), { once: true });
        card.append(cover);
      }
      card.append(el('div', { class: 'card-body' }, el('h3', {}, game.titulo),
        el('p', { class: 'meta' }, `Última actualización: ${date(game.fecha)}`),
        el('span', { class: 'badge', 'data-estado': game.estado }, game.estado)));
      return card;
    }));
  }

  async function load() {
    const current = ++version;
    profile = null;
    games = [];
    $('#stats').replaceChildren();
    $('#dist').replaceChildren();
    tabs.replaceChildren();
    list.replaceChildren();
    notice.replaceChildren();
    showStatus(entries, 'Cargando tu actividad…');
    entries.setAttribute('aria-busy', 'true');
    try {
      const result = await Promise.all([GameRankData.profile(), GameRankData.library(), GameRankData.states()]);
      if (current !== version) return;
      [profile, games, states] = result;
      $('#diario-avatar').replaceWith(Object.assign(avatar(profile.avatarUrl, profile.nombreUsuario), { id: 'diario-avatar' }));
      renderStats();
      renderEntries();
      renderList();
    } catch (error) {
      if (current !== version) return;
      showStatus(entries, error.message, 'error');
      entries.firstChild.append(' ', error.status === 401 ? el('a', { class: 'btn', href: 'login.html' }, 'Ingresar')
        : el('button', { type: 'button', class: 'btn btn-ghost', onclick: load }, 'Reintentar'));
    } finally { if (current === version) entries.removeAttribute('aria-busy'); }
  }

  $('#orden').addEventListener('change', renderEntries);
  window.addEventListener('gamerank:sessionchange', () => { filter = 0; load(); });
  GameRankAuth.ready.then(load);
  window.addEventListener('pageshow', event => { if (event.persisted) load(); });
})();

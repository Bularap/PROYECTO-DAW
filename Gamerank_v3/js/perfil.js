(() => {
  cabecera('perfil.html');
  pie();
  const main = $('#main');
  const requested = new URLSearchParams(location.search).get('id');
  const profileId = requested === null ? null : /^\d+$/.test(requested) ? Number(requested) : NaN;
  let version = 0;
  const date = value => GameRankData.date(value)?.toLocaleDateString('es-AR') || 'Fecha no disponible';

  function rating(value) {
    return value == null ? el('span', { class: 'meta' }, 'Sin calificación')
      : el('span', { class: 'stars', role: 'img', 'aria-label': `${value} de 5 estrellas` }, stars(value));
  }

  function reviewRow(review, own) {
    const row = el('article', { class: 'review' }, el('h3', {}, review.titulo),
      el('p', { class: 'meta' }, rating(review.puntuacion), ` · ${date(review.fechaPublicacion)}${review.editada ? ' (editada)' : ''}`),
      el('p', {}, review.contenido));
    if (own) {
      const remove = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Eliminar');
      remove.addEventListener('click', async () => {
        if (!confirm('¿Eliminar esta reseña? No se puede deshacer.')) return;
        const owner = GameRankAuth.token();
        remove.disabled = true;
        try {
          await PerfilStore.eliminarResena(review.resenaId);
          if (owner === GameRankAuth.token()) await load();
        } catch (error) {
          if (owner !== GameRankAuth.token()) return;
          showStatus($('#perfil-aviso'), error.message, 'error');
        } finally { remove.disabled = false; }
      });
      row.append(el('div', { class: 'review-actions' },
        el('a', { class: 'btn btn-ghost', href: `reviews.html?juegoId=${review.juegoId}` }, 'Editar'), remove));
    }
    return row;
  }

  function render(profile) {
    const own = profile.usuarioId === GameRankAuth.user()?.usuarioId;
    const stats = [[profile.promedioOtorgado == null ? '–' : Number(profile.promedioOtorgado).toFixed(1), 'Promedio de notas'],
      [profile.cantidadCalificaciones, 'Juegos calificados'], [profile.cantidadResenas, 'Reseñas'],
      ...profile.listas.map(state => [state.cantidad, state.nombre])];
    const heading = el('div', {}, el('h1', {}, profile.nombreUsuario),
      el('p', { class: 'meta', style: 'margin:0' }, `Miembro desde ${date(profile.fechaRegistro)}`));
    if (own) heading.append(el('div', { class: 'row-btns' }, el('a', { class: 'btn', href: 'editar-perfil.html' }, 'Editar perfil'),
      el('a', { class: 'btn btn-ghost', href: 'configuracion.html' }, 'Configuración')));
    const games = el('div', { class: 'grid compact', id: 'calificados' });
    const reviews = el('div', { id: 'resenas' });
    main.replaceChildren(el('div', { class: 'profile-head' }, avatar(profile.avatarUrl, profile.nombreUsuario, true), heading),
      el('div', { id: 'perfil-aviso', 'aria-live': 'polite' }),
      el('section', { class: 'stats', 'aria-label': 'Estadísticas' }, ...stats.map(([n, text]) =>
        el('div', { class: 'stat' }, el('strong', {}, String(n)), el('span', {}, text)))),
      el('h2', {}, 'Juegos calificados'), games, el('h2', {}, 'Reseñas publicadas'), reviews);
    if (profile.calificaciones.length) games.replaceChildren(...profile.calificaciones.map(game => {
      const card = el('article', { class: 'card' });
      if (game.portadaUrl) {
        const cover = el('img', { src: game.portadaUrl, alt: `Portada de ${game.titulo}`, loading: 'lazy' });
        cover.addEventListener('error', () => cover.replaceWith(el('p', { class: 'meta' }, 'Portada no disponible')), { once: true });
        card.append(cover);
      }
      card.append(el('div', { class: 'card-body' }, el('h3', {}, game.titulo), rating(game.puntuacion),
        el('p', { class: 'meta' }, `Última calificación: ${date(game.fecha)}`)));
      return card;
    }));
    else showStatus(games, 'Todavía no hay juegos calificados.');
    if (profile.resenas.length) reviews.replaceChildren(...profile.resenas.map(review => reviewRow(review, own)));
    else showStatus(reviews, 'Sin reseñas publicadas.');
  }

  async function load() {
    const current = ++version;
    showStatus(main, 'Cargando perfil…');
    main.setAttribute('aria-busy', 'true');
    try {
      const profile = await PerfilStore.obtener(profileId);
      if (current === version) render(profile);
    } catch (error) {
      if (current !== version) return;
      showStatus(main, error.status === 404 ? 'Perfil no disponible.' : error.message, 'error');
      main.firstChild.append(' ', error.status === 401 ? el('a', { class: 'btn', href: 'login.html' }, 'Ingresar')
        : el('button', { class: 'btn btn-ghost', type: 'button', onclick: load }, 'Reintentar'));
    } finally { if (current === version) main.removeAttribute('aria-busy'); }
  }

  window.addEventListener('gamerank:sessionchange', load);
  GameRankAuth.ready.then(load);
  window.addEventListener('pageshow', event => { if (event.persisted) load(); });
})();

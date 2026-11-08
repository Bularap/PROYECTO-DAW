const GameRankData = (() => {
  function check(value, condition) {
    if (!condition) throw GameRankApi.createError('El servidor devolvió datos inesperados.', 0, 'response');
    return value;
  }

  async function identity() {
    await GameRankAuth.ready;
    const user = GameRankAuth.user();
    if (!user) throw GameRankApi.createError('Ingresá para gestionar tus reseñas y tu lista.', 401, 'session');
    return { id: user.usuarioId, token: GameRankAuth.token() };
  }

  async function personal(path, options = {}) {
    const owner = await identity();
    if (GameRankAuth.user()?.usuarioId !== owner.id || GameRankAuth.token() !== owner.token) {
      throw GameRankApi.createError('La sesión cambió. Volvé a cargar tus datos.', 401, 'session');
    }
    const result = await GameRankApi.request(typeof path === 'function' ? path(owner.id) : path, options);
    if (GameRankAuth.user()?.usuarioId !== owner.id || GameRankAuth.token() !== owner.token) {
      throw GameRankApi.createError('La sesión cambió. Volvé a cargar tus datos.', 401, 'session');
    }
    return result;
  }

  async function search(text) {
    const data = await GameRankApi.request(`/juegos?${new URLSearchParams({ texto: text, tamanioPagina: 8, orden: 'TITULO' })}`, { authenticated: false, timeout: 30000 });
    return check(data, Array.isArray(data?.juegos));
  }

  async function states() {
    const data = await GameRankApi.request('/estados-lista', { authenticated: false });
    return check(data, Array.isArray(data) && data.every(s => Number.isInteger(s.estadoListaId) && typeof s.nombre === 'string'));
  }

  async function library() {
    const data = await personal(id => `/usuarios/${id}/biblioteca`);
    return check(data, Array.isArray(data) && data.every(g => Number.isInteger(g.juegoId) && typeof g.titulo === 'string'));
  }

  async function ownReviews() {
    const data = await personal(id => `/usuarios/${id}`);
    return check(data?.resenas, Array.isArray(data?.resenas));
  }

  async function profile(id) {
    if (id != null && (!Number.isInteger(id) || id < 1)) {
      throw GameRankApi.createError('El perfil solicitado no es válido.', 404, 'profile');
    }
    const data = await personal(owner => `/usuarios/${id ?? owner}`);
    return check(data, Number.isInteger(data?.usuarioId) && typeof data.nombreUsuario === 'string'
      && Array.isArray(data.listas) && Array.isArray(data.calificaciones) && Array.isArray(data.resenas)
      && Number.isInteger(data.cantidadCalificaciones) && Number.isInteger(data.cantidadResenas)
      && data.listas.every(state => Number.isInteger(state.estadoListaId) && typeof state.nombre === 'string' && Number.isInteger(state.cantidad))
      && data.calificaciones.every(game => Number.isInteger(game.juegoId) && typeof game.titulo === 'string' && Number.isInteger(game.puntuacion) && game.puntuacion >= 1 && game.puntuacion <= 5)
      && data.resenas.every(review => Number.isInteger(review.resenaId) && Number.isInteger(review.juegoId)
        && typeof review.titulo === 'string' && typeof review.contenido === 'string'
        && (review.puntuacion == null || Number.isInteger(review.puntuacion) && review.puntuacion >= 1 && review.puntuacion <= 5)));
  }

  function date(value) {
    if (typeof value !== 'string' || !value) return null;
    const normalized = /[zZ]$|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`;
    const result = new Date(normalized);
    return Number.isFinite(result.getTime()) ? result : null;
  }

  async function community(page) {
    const data = await GameRankApi.request(`/resenas?pagina=${page}&tamanioPagina=10`, { authenticated: false });
    return check(data, Array.isArray(data?.resenas) && Number.isInteger(data.total));
  }

  return {
    search, states, library, ownReviews, community, profile, date,
    game: id => GameRankApi.request(`/juegos/${id}`, { authenticated: false, timeout: 30000 }),
    state: id => personal(`/juegos/${id}/mi-estado`),
    saveReview: (gameId, reviewId, contenido) => personal(reviewId ? `/resenas/${reviewId}` : `/juegos/${gameId}/resenas`, { method: reviewId ? 'PUT' : 'POST', body: { contenido } }),
    removeReview: id => personal(`/resenas/${id}`, { method: 'DELETE' }),
    rate: (id, puntuacion) => personal(`/juegos/${id}/calificacion`, { method: 'PUT', body: { puntuacion } }),
    unrate: id => personal(`/juegos/${id}/calificacion`, { method: 'DELETE' }),
    setState: (id, estadoListaId) => personal(`/juegos/${id}/biblioteca`, { method: 'PUT', body: { estadoListaId } }),
    remove: id => personal(`/juegos/${id}/biblioteca`, { method: 'DELETE' })
  };
})();

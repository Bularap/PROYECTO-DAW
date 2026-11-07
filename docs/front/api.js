const API_URL = 'http://localhost:5251/api';

const Sesion = {
  KEY: 'gamerank_sesion',
  actual() { return JSON.parse(localStorage.getItem(this.KEY) || 'null'); },
  token() { return this.actual()?.token || null; },
  usuario() { return this.actual()?.usuario || null; },
  guardar(sesion) { localStorage.setItem(this.KEY, JSON.stringify(sesion)); },
  cerrar() { localStorage.removeItem(this.KEY); }
};

async function api(ruta, { metodo = 'GET', cuerpo } = {}) {
  const cabeceras = {};
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json';
  if (Sesion.token()) cabeceras.Authorization = `Bearer ${Sesion.token()}`;

  let res;
  try {
    res = await fetch(API_URL + ruta, { method: metodo, headers: cabeceras, body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo) });
  } catch {
    throw Object.assign(new Error('No hay conexión con el servidor de GameRank.'), { estado: 0 });
  }

  const texto = await res.text();
  const datos = texto ? JSON.parse(texto) : null;
  if (res.status === 401 && Sesion.token()) Sesion.cerrar();
  if (!res.ok) throw Object.assign(new Error(datos?.mensaje || `Error del servidor (${res.status}).`), { estado: res.status });
  return datos;
}

const consulta = params => {
  const limpios = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== '');
  return limpios.length ? '?' + new URLSearchParams(limpios) : '';
};

const GameRankApi = {
  async registrar(email, nombreUsuario, password, aceptaPrivacidad) {
    const sesion = await api('/auth/registro', { metodo: 'POST', cuerpo: { email, nombreUsuario, password, aceptaPrivacidad } });
    Sesion.guardar(sesion);
    return sesion.usuario;
  },
  async login(email, password) {
    const sesion = await api('/auth/login', { metodo: 'POST', cuerpo: { email, password } });
    Sesion.guardar(sesion);
    return sesion.usuario;
  },
  async logout() {
    try { await api('/auth/logout', { metodo: 'POST' }); } finally { Sesion.cerrar(); }
  },
  yo: () => api('/auth/yo'),

  generos: () => api('/generos'),
  plataformas: () => api('/plataformas'),
  estadosLista: () => api('/estados-lista'),
  juegos: filtros => api('/juegos' + consulta(filtros)),
  ficha: id => api(`/juegos/${id}`),
  miEstado: id => api(`/juegos/${id}/mi-estado`),

  calificar: (id, puntuacion) => api(`/juegos/${id}/calificacion`, { metodo: 'PUT', cuerpo: { puntuacion } }),
  quitarCalificacion: id => api(`/juegos/${id}/calificacion`, { metodo: 'DELETE' }),
  feed: (id, pagina = 1) => api(`/juegos/${id}/resenas` + consulta({ pagina })),
  publicarResena: (id, contenido) => api(`/juegos/${id}/resenas`, { metodo: 'POST', cuerpo: { contenido } }),
  editarResena: (resenaId, contenido) => api(`/resenas/${resenaId}`, { metodo: 'PUT', cuerpo: { contenido } }),
  eliminarResena: resenaId => api(`/resenas/${resenaId}`, { metodo: 'DELETE' }),
  reportarResena: (resenaId, motivo, comentario) => api(`/resenas/${resenaId}/reportes`, { metodo: 'POST', cuerpo: { motivo, comentario } }),

  ponerEnLista: (id, estadoListaId) => api(`/juegos/${id}/biblioteca`, { metodo: 'PUT', cuerpo: { estadoListaId } }),
  quitarDeLista: id => api(`/juegos/${id}/biblioteca`, { metodo: 'DELETE' }),
  perfil: usuarioId => api(`/usuarios/${usuarioId}`),
  biblioteca: (usuarioId, estadoListaId) => api(`/usuarios/${usuarioId}/biblioteca` + consulta({ estadoListaId }))
};

const ORDEN_API = { '-added': 'puntuacion', '-rating': 'puntuacion', '-released': 'lanzamiento', name: 'titulo' };

const comoTarjeta = j => ({
  id: j.juegoId,
  name: j.titulo,
  background_image: j.portadaUrl,
  genres: (j.generos || []).map(g => ({ name: g.nombre, slug: g.slug })),
  released: j.fechaLanzamiento,
  rating: j.promedioCalificacion || 0,
  ratings_count: j.cantidadCalificaciones
});

async function buscarJuegos({ page = 1, page_size = 12, search, genres, ordering } = {}) {
  const datos = await GameRankApi.juegos({ texto: search, generos: genres, orden: ORDEN_API[ordering], pagina: page, tamanioPagina: page_size });
  return { results: datos.juegos.map(comoTarjeta), next: page * page_size < datos.total, parcial: datos.parcial };
}

async function listarGeneros() {
  const generos = await GameRankApi.generos();
  return { results: generos.map(g => ({ name: g.nombre, slug: g.slug })) };
}

let estadosCache = null;
async function idDeEstado(nombre) {
  estadosCache ??= await GameRankApi.estadosLista();
  const estado = estadosCache.find(e => e.nombre === nombre);
  if (!estado) throw new Error(`El estado "${nombre}" no existe en el servidor.`);
  return estado.estadoListaId;
}

function usuarioActual() {
  const usuario = Sesion.usuario();
  if (!usuario) throw Object.assign(new Error('Tenés que iniciar sesión.'), { estado: 401 });
  return usuario;
}

const ReviewStore = {
  async all() {
    const { usuarioId } = usuarioActual();
    const [perfil, lista] = await Promise.all([GameRankApi.perfil(usuarioId), GameRankApi.biblioteca(usuarioId)]);
    const estados = new Map(lista.map(x => [x.juegoId, x.estado]));
    return perfil.resenas.map(r => ({
      id: r.resenaId, juegoId: r.juegoId, juego: r.titulo, puntaje: r.puntuacion || 0,
      estado: estados.get(r.juegoId) || '', texto: r.contenido, fecha: r.fechaPublicacion, editada: r.editada
    }));
  },
  async add(r) {
    usuarioActual();
    if (!r.juegoId) throw new Error('Elegí un juego de la lista de sugerencias.');
    const estadoListaId = r.estado ? await idDeEstado(r.estado) : null;
    await GameRankApi.publicarResena(r.juegoId, r.texto);
    await GameRankApi.calificar(r.juegoId, r.puntaje);
    if (estadoListaId) await GameRankApi.ponerEnLista(r.juegoId, estadoListaId);
  },
  async remove(id) { await GameRankApi.eliminarResena(id); }
};

const WishlistStore = {
  async all() {
    const lista = await GameRankApi.biblioteca(usuarioActual().usuarioId);
    return lista.map(x => ({
      id: x.juegoId, name: x.titulo, background_image: x.portadaUrl, estado: x.estado, fecha: x.fecha,
      genres: [], released: null, rating: 0
    }));
  },
  async add(g, estado) { await GameRankApi.ponerEnLista(g.id, await idDeEstado(estado)); },
  async setEstado(id, estado) { await GameRankApi.ponerEnLista(id, await idDeEstado(estado)); },
  async remove(id) { await GameRankApi.quitarDeLista(id); }
};

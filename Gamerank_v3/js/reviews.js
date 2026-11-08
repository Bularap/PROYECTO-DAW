(() => {
  const form = $('#form-review');
  const input = $('#juego');
  const list = $('#lista');
  const notice = $('#review-aviso');
  const pager = $('#review-pager');
  let gameId = null;
  let reviewId = null;
  let suggestions = [];
  let page = 1;
  let feedVersion = 0;
  let selectionVersion = 0;
  let searchVersion = 0;
  let busy = false;

  function message(text, error = false) {
    notice.textContent = text;
    notice.className = error ? 'field-error' : 'meta';
  }

  function setBusy(value) {
    busy = value;
    form.setAttribute('aria-busy', String(value));
    form.querySelectorAll('input, textarea, select, button').forEach(control => { control.disabled = value; });
  }

  function rating() {
    return Number(form.querySelector('input[name="puntaje"]:checked')?.value) || null;
  }

  function fillRating(value) {
    form.querySelectorAll('input[name="puntaje"]').forEach(control => { control.checked = Number(control.value) === value; });
  }

  function reset() {
    selectionVersion++;
    form.reset();
    gameId = null;
    reviewId = null;
    $('#publicar').textContent = 'Publicar reseña';
    $('#cancelar').hidden = true;
    ['juego', 'puntaje', 'texto', 'acepto'].forEach(key => { $(`#err-${key}`).textContent = ''; });
  }

  async function selectGame(game, content) {
    const version = ++selectionVersion;
    gameId = game.juegoId;
    input.value = game.titulo;
    reviewId = null;
    $('#texto').value = content || '';
    fillRating(null);
    $('#estado').value = '';
    $('#publicar').textContent = 'Publicar reseña';
    $('#cancelar').hidden = true;
    if (!GameRankAuth.user()) return;
    setBusy(true);
    message('Consultando tu reseña y puntuación…');
    try {
      const state = await GameRankData.state(gameId);
      if (version !== selectionVersion) return;
      reviewId = state.resenaId;
      fillRating(state.puntuacion);
      $('#estado').value = state.estadoListaId || '';
      $('#texto').value = content ?? state.resenaContenido ?? '';
      $('#publicar').textContent = reviewId ? 'Guardar cambios' : 'Publicar reseña';
      $('#cancelar').hidden = !reviewId;
      message(reviewId ? 'Ya reseñaste este juego. Podés editar la reseña existente.' : '');
    } catch (error) {
      if (version === selectionVersion) message(error.message, true);
    } finally {
      if (version === selectionVersion) setBusy(false);
    }
  }

  const suggest = debounce(async () => {
    const query = input.value.trim();
    const version = ++searchVersion;
    if (query.length < 2) return;
    try {
      const data = await GameRankData.search(query);
      if (version !== searchVersion || input.value.trim() !== query) return;
      suggestions = data.juegos;
      $('#sugerencias').replaceChildren(...suggestions.map(game => el('option', { value: game.titulo })));
      const selected = suggestions.find(game => game.titulo === input.value);
      if (selected && gameId !== selected.juegoId) await selectGame(selected);
      else message(data.parcial ? 'Steam no está disponible; las sugerencias pueden estar incompletas.' : 'Elegí un juego de las sugerencias.');
    } catch (error) {
      if (version === searchVersion) message(error.message, true);
    }
  }, 350);

  input.addEventListener('input', () => {
    if (busy) return;
    searchVersion++;
    selectionVersion++;
    gameId = null;
    reviewId = null;
    $('#publicar').textContent = 'Publicar reseña';
    $('#cancelar').hidden = true;
    const selected = suggestions.find(game => game.titulo === input.value);
    if (selected) selectGame(selected);
    else suggest();
  });

  function validate(review = false) {
    const errors = {};
    if (!gameId) errors.juego = 'Elegí un juego real de las sugerencias.';
    if (!GameRankAuth.user()) errors.juego = 'Ingresá para guardar tus datos.';
    if (review && ($('#texto').value.trim().length < 10 || $('#texto').value.trim().length > 2000)) errors.texto = 'La reseña debe tener entre 10 y 2000 caracteres.';
    if (review && !$('#acepto').checked) errors.acepto = 'Debés aceptar la política de privacidad.';
    ['juego', 'puntaje', 'texto', 'acepto'].forEach(key => { $(`#err-${key}`).textContent = errors[key] || ''; });
    return !Object.keys(errors).length;
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (busy || !validate(true)) return;
    setBusy(true);
    const saved = [];
    let step = 'reseña';
    try {
      const review = await GameRankData.saveReview(gameId, reviewId, $('#texto').value.trim());
      reviewId = review.resenaId;
      $('#publicar').textContent = 'Guardar cambios';
      $('#cancelar').hidden = false;
      saved.push('Reseña guardada');
      if (rating()) {
        step = 'puntuación';
        await GameRankData.rate(gameId, rating());
        saved.push('puntuación guardada');
      }
      if ($('#estado').value) {
        step = 'estado de tu lista';
        await GameRankData.setState(gameId, Number($('#estado').value));
        saved.push('lista actualizada');
      }
      message(`${saved.join('; ')}. Las operaciones terminaron correctamente.`);
    } catch (error) {
      message(saved.length
        ? `${saved.join('; ')}. No se pudo guardar ${step}: ${error.message} Lo anterior sigue guardado; podés reintentar sin crear otra reseña.`
        : (error.code === 'network' || error.code === 'timeout' ? `${error.message} No pudimos confirmar si la reseña se guardó. Revisá Mis reseñas antes de volver a publicarla.` : error.message), true);
    } finally {
      setBusy(false);
      loadFeed();
    }
  });

  async function changeRating(remove) {
    if (busy || !validate()) return;
    if (!remove && !rating()) {
      $('#err-puntaje').textContent = 'Elegí una puntuación de 1 a 5.';
      return;
    }
    setBusy(true);
    try {
      if (remove) {
        await GameRankData.unrate(gameId);
        fillRating(null);
      } else await GameRankData.rate(gameId, rating());
      message(remove ? 'Puntuación eliminada. Tu reseña y lista no cambiaron.' : 'Puntuación guardada. Tu reseña y lista no cambiaron.');
      loadFeed();
    } catch (error) { message(error.message, true); }
    finally { setBusy(false); }
  }

  $('#guardar-puntaje').addEventListener('click', () => changeRating(false));
  $('#quitar-puntaje').addEventListener('click', () => changeRating(true));
  $('#cancelar').addEventListener('click', () => { reset(); message(''); });

  async function removeReview(review, button) {
    if (busy) return;
    if (!confirm('¿Eliminar esta reseña? Tu puntuación y lista no se eliminarán.')) return;
    setBusy(true);
    button.disabled = true;
    try {
      await GameRankData.removeReview(review.resenaId);
      if (reviewId === review.resenaId) reset();
      message('Reseña eliminada.');
      loadFeed();
    } catch (error) { message(error.message, true); }
    finally { button.disabled = false; setBusy(false); }
  }

  function article(review, own) {
    const actions = el('div', { class: 'review-actions' });
    if (own) {
      const edit = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Editar');
      const remove = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Eliminar');
      edit.addEventListener('click', () => { if (!busy) selectGame({ juegoId: review.juegoId, titulo: review.titulo }, review.contenido); });
      remove.addEventListener('click', () => removeReview(review, remove));
      actions.append(edit, remove);
    }
    const score = review.puntuacion == null ? 'Sin calificar' : `${stars(review.puntuacion)} · ${review.puntuacion}/5`;
    return el('article', { class: 'review' },
      el('h3', {}, review.titulo || 'Juego'), el('div', { class: 'stars' }, score),
      el('p', {}, review.contenido),
      el('p', { class: 'meta' }, `${review.nombreUsuario || GameRankAuth.user()?.nombreUsuario || ''} · ${new Date(review.fechaPublicacion).toLocaleDateString('es-AR')}${review.editada ? ' · Editada' : ''}`), actions);
  }

  async function loadFeed() {
    const version = ++feedVersion;
    const own = $('#alcance').value === 'propias';
    const userId = GameRankAuth.user()?.usuarioId;
    pager.replaceChildren();
    showStatus(list, 'Cargando reseñas…');
    try {
      const data = own ? { resenas: await GameRankData.ownReviews() } : await GameRankData.community(page);
      if (version !== feedVersion || userId !== GameRankAuth.user()?.usuarioId) return;
      const score = Number($('#filtro-puntaje').value);
      const items = score ? data.resenas.filter(review => review.puntuacion === score) : data.resenas;
      if (items.length) list.replaceChildren(...items.map(review => article(review, own || review.usuarioId === userId)));
      else showStatus(list, score ? 'No hay reseñas con esa puntuación en estos resultados.' : 'Todavía no hay reseñas para mostrar.');
      if (!own) pager.replaceChildren(
        el('button', { type: 'button', class: 'btn btn-ghost', disabled: page <= 1, onclick: () => { page--; loadFeed(); } }, 'Anterior'),
        el('span', { class: 'meta' }, `Página ${page} · ${data.total} reseñas. El filtro de estrellas se aplica a esta página.`),
        el('button', { type: 'button', class: 'btn btn-ghost', disabled: page * 10 >= data.total, onclick: () => { page++; loadFeed(); } }, 'Siguiente'));
    } catch (error) {
      if (version !== feedVersion) return;
      showStatus(list, error.message, 'error');
      list.firstChild.append(' ', el('button', { type: 'button', class: 'btn btn-ghost', onclick: loadFeed }, 'Reintentar'));
    }
  }

  $('#alcance').addEventListener('change', () => { page = 1; loadFeed(); });
  $('#filtro-puntaje').addEventListener('change', loadFeed);
  window.addEventListener('gamerank:sessionchange', () => { reset(); setBusy(false); message(''); loadFeed(); });

  async function initialize() {
    try {
      const states = await GameRankData.states();
      $('#estado').append(...states.map(state => el('option', { value: state.estadoListaId }, state.nombre)));
    } catch (error) { message(`No se pudieron cargar los estados de tu lista. Podés guardar reseña y puntuación. ${error.message}`, true); }
    await GameRankAuth.ready;
    loadFeed();
    const id = Number(new URLSearchParams(location.search).get('juegoId'));
    if (Number.isInteger(id) && id > 0) {
      try { await selectGame(await GameRankData.game(id)); }
      catch (error) { message(error.message, true); }
    }
  }
  initialize();
})();

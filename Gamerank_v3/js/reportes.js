const GameRankReportes = (() => {
  const MOTIVOS = [['SPAM', 'Spam o publicidad'], ['OFENSIVO', 'Contenido ofensivo'], ['SPOILER', 'Spoiler sin aviso'], ['OTRO', 'Otro motivo']];
  const enviados = new Set();
  let secuencia = 0;

  function puedeReportar(resena) {
    const usuario = GameRankAuth.user();
    return !!usuario && Number.isInteger(resena?.resenaId) && resena.usuarioId !== usuario.usuarioId;
  }

  function control(resena) {
    const contenedor = el('div', { class: 'report' });
    if (!puedeReportar(resena)) return contenedor;
    const id = `reporte-${resena.resenaId}-${++secuencia}`;
    const estado = el('p', { class: 'meta', id: `${id}-estado`, 'aria-live': 'polite' });
    const boton = el('button', { type: 'button', class: 'btn-link', 'aria-expanded': 'false', 'aria-controls': `${id}-form` }, 'Reportar');
    if (enviados.has(resena.resenaId)) {
      boton.disabled = true;
      boton.textContent = 'Reportada';
    }

    const motivo = el('select', { id: `${id}-motivo`, required: true },
      el('option', { value: '' }, 'Elegí un motivo'), ...MOTIVOS.map(([valor, texto]) => el('option', { value: valor }, texto)));
    const comentario = el('textarea', { id: `${id}-comentario`, maxlength: '300', rows: '2', 'aria-describedby': `${id}-ayuda` });
    const error = el('p', { class: 'field-error', id: `${id}-error` });
    const enviar = el('button', { type: 'submit', class: 'btn' }, 'Enviar reporte');
    const cancelar = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Cancelar');
    const form = el('form', { class: 'report-form', id: `${id}-form`, hidden: true, novalidate: true },
      el('div', { class: 'field' }, el('label', { for: motivo.id }, 'Motivo del reporte'), motivo),
      el('div', { class: 'field' }, el('label', { for: comentario.id }, 'Comentario (opcional)'), comentario,
        el('p', { class: 'meta', id: `${id}-ayuda` }, 'Hasta 300 caracteres. Lo verán solo los moderadores.')),
      error,
      el('div', { class: 'review-actions' }, enviar, cancelar));

    function abrir(abierto) {
      form.hidden = !abierto;
      boton.setAttribute('aria-expanded', String(abierto));
      if (abierto) { estado.textContent = ''; motivo.focus(); }
      else { form.reset(); error.textContent = ''; motivo.removeAttribute('aria-invalid'); }
    }

    boton.addEventListener('click', () => abrir(form.hidden));
    cancelar.addEventListener('click', () => { abrir(false); boton.focus(); });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      if (!motivo.value) {
        error.textContent = 'Elegí el motivo del reporte.';
        motivo.setAttribute('aria-invalid', 'true');
        motivo.setAttribute('aria-describedby', error.id);
        motivo.focus();
        return;
      }
      motivo.removeAttribute('aria-invalid');
      error.textContent = '';
      form.setAttribute('aria-busy', 'true');
      form.querySelectorAll('select, textarea, button').forEach(c => { c.disabled = true; });
      try {
        await GameRankData.report(resena.resenaId, motivo.value, comentario.value.trim());
        marcarEnviado('Reporte enviado. Un moderador lo va a revisar.');
      } catch (falla) {
        if (falla.status === 409) marcarEnviado('Ya habías reportado esta reseña.');
        else error.textContent = falla.message;
      } finally {
        form.removeAttribute('aria-busy');
        form.querySelectorAll('select, textarea, button').forEach(c => { c.disabled = false; });
      }
    });

    function marcarEnviado(texto) {
      enviados.add(resena.resenaId);
      abrir(false);
      boton.disabled = true;
      boton.textContent = 'Reportada';
      estado.textContent = texto;
    }

    contenedor.append(boton, form, estado);
    return contenedor;
  }

  return { control };
})();

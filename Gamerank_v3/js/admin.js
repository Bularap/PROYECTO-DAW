(() => {
  const contenido = $('#contenido');
  const secciones = $('#secciones');
  const ADMIN = 'Administrador';
  const SUPER = 'SuperAdministrador';
  const SECCIONES = [
    { id: 'moderacion', titulo: 'Moderación', roles: [ADMIN, SUPER], render: moderacion },
    { id: 'bloqueados', titulo: 'Cuentas bloqueadas', roles: [ADMIN, SUPER], render: bloqueados },
    { id: 'usuarios', titulo: 'Usuarios y roles', roles: [SUPER], render: usuarios },
    { id: 'auditoria', titulo: 'Auditoría', roles: [SUPER], render: auditoria },
    { id: 'respaldos', titulo: 'Respaldos', roles: [SUPER], render: respaldos }
  ];
  let actual = null;
  let version = 0;

  const fecha = valor => valor ? new Date(valor).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' }) : '–';
  const api = (ruta, opciones) => GameRankAuth.request(ruta, opciones);
  const query = parametros => new URLSearchParams(Object.entries(parametros).filter(([, v]) => v !== '' && v != null)).toString();

  function aviso(nodo, texto, error = false) {
    nodo.textContent = texto;
    nodo.className = error ? 'aviso' : 'aviso ok';
  }

  function paginador(datos, pagina, irA) {
    const paginas = Math.max(1, Math.ceil(datos.total / datos.tamanioPagina));
    return el('div', { class: 'toolbar', style: 'justify-content:center' },
      el('button', { type: 'button', class: 'btn btn-ghost', disabled: pagina <= 1, onclick: () => irA(pagina - 1) }, 'Anterior'),
      el('span', { class: 'meta', style: 'align-self:center' }, `Página ${pagina} de ${paginas} · ${datos.total} registros`),
      el('button', { type: 'button', class: 'btn btn-ghost', disabled: pagina >= paginas, onclick: () => irA(pagina + 1) }, 'Siguiente'));
  }

  function tabla(columnas, filas) {
    return el('div', { class: 'tabla-admin' }, el('table', {},
      el('thead', {}, el('tr', {}, ...columnas.map(c => el('th', { scope: 'col' }, c)))),
      el('tbody', {}, ...filas)));
  }

  async function cargar(contenedor, tarea) {
    const v = version;
    showStatus(contenedor, 'Cargando…');
    try {
      const resultado = await tarea();
      if (v === version) contenedor.replaceChildren(...[resultado].flat());
    } catch (error) {
      if (v !== version) return;
      showStatus(contenedor, error.message, 'error');
      contenedor.firstChild.append(' ', el('button', { type: 'button', class: 'btn btn-ghost', onclick: () => cargar(contenedor, tarea) }, 'Reintentar'));
    }
  }

  function moderacion() {
    let pagina = 1;
    const filtro = el('select', { id: 'filtro-moderacion' },
      el('option', { value: 'REPORTADAS' }, 'Reportadas'),
      el('option', { value: 'RECIENTES' }, 'Recientes'),
      el('option', { value: 'OCULTAS' }, 'Ocultas'));
    const motivo = el('input', { type: 'text', id: 'motivo-moderacion', maxlength: '300', placeholder: 'Obligatorio para ocultar, eliminar o bloquear' });
    const estado = el('p', { class: 'aviso', role: 'status' });
    const lista = el('div', { id: 'lista-moderacion' });

    async function accion(boton, tarea, exito) {
      boton.disabled = true;
      try {
        await tarea(motivo.value.trim());
        aviso(estado, exito);
        motivo.value = '';
        listar();
      } catch (error) {
        aviso(estado, error.message, true);
        boton.disabled = false;
      }
    }

    function fila(r) {
      const oculta = r.estado === 'O';
      const visibilidad = el('button', { type: 'button', class: 'btn btn-ghost' }, oculta ? 'Mostrar' : 'Ocultar');
      visibilidad.addEventListener('click', () => accion(visibilidad,
        m => api(`/moderacion/resenas/${r.resenaId}`, { method: 'PUT', body: { accion: oculta ? 'MOSTRAR' : 'OCULTAR', motivo: m } }),
        oculta ? 'La reseña vuelve a estar visible.' : 'Reseña ocultada.'));
      const eliminar = el('button', { type: 'button', class: 'btn btn-danger' }, 'Eliminar');
      eliminar.addEventListener('click', () => accion(eliminar,
        m => api(`/moderacion/resenas/${r.resenaId}?${query({ motivo: m })}`, { method: 'DELETE' }), 'Reseña eliminada.'));
      const bloqueo = el('button', { type: 'button', class: 'btn btn-ghost' }, r.autorBloqueado ? 'Desbloquear autor' : 'Bloquear autor');
      bloqueo.addEventListener('click', () => accion(bloqueo,
        m => api(`/moderacion/usuarios/${r.usuarioId}/bloqueo`, { method: 'PUT', body: { bloquear: !r.autorBloqueado, motivo: m } }),
        r.autorBloqueado ? `Se desbloqueó a ${r.nombreUsuario}.` : `Se bloqueó a ${r.nombreUsuario}.`));
      const etiquetas = [oculta ? 'Oculta' : 'Visible', r.autorBloqueado ? 'Autor bloqueado' : null,
        r.cantidadReportes ? `${r.cantidadReportes} reporte${r.cantidadReportes > 1 ? 's' : ''}` : null].filter(Boolean);
      return el('article', { class: 'review' },
        el('h3', {}, r.titulo),
        el('p', { class: 'meta' }, `${r.nombreUsuario} · ${fecha(r.fechaPublicacion)}`),
        el('p', {}, r.contenido),
        el('p', { class: 'meta' }, ...etiquetas.map(t => el('span', { class: 'badge', style: 'margin-right:.4rem' }, t)),
          r.motivos ? `Motivos: ${r.motivos}` : ''),
        el('div', { class: 'review-actions', style: 'flex-wrap:wrap' }, visibilidad, eliminar, bloqueo));
    }

    function listar() {
      cargar(lista, async () => {
        const datos = await api(`/moderacion/resenas?${query({ filtro: filtro.value, pagina, tamanioPagina: 10 })}`);
        if (!datos.items.length) return el('div', { class: 'status' }, 'No hay reseñas para este filtro.');
        return [...datos.items.map(fila), paginador(datos, pagina, p => { pagina = p; listar(); })];
      });
    }

    filtro.addEventListener('change', () => { pagina = 1; listar(); });
    listar();
    return el('section', { class: 'panel' },
      el('div', { class: 'admin-controles' },
        el('div', { class: 'field' }, el('label', { for: 'filtro-moderacion' }, 'Mostrar'), filtro),
        el('div', { class: 'field', style: 'flex:2 1 280px' }, el('label', { for: 'motivo-moderacion' }, 'Motivo de la acción'), motivo)),
      estado, lista);
  }

  function bloqueados() {
    let pagina = 1;
    const estado = el('p', { class: 'aviso', role: 'status' });
    const lista = el('div');

    function listar() {
      cargar(lista, async () => {
        const datos = await api(`/moderacion/usuarios-bloqueados?${query({ pagina, tamanioPagina: 20 })}`);
        if (!datos.items.length) return el('div', { class: 'status' }, 'No hay cuentas bloqueadas.');
        const filas = datos.items.map(u => {
          const boton = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Desbloquear');
          boton.addEventListener('click', async () => {
            boton.disabled = true;
            try {
              await api(`/moderacion/usuarios/${u.usuarioId}/bloqueo`, { method: 'PUT', body: { bloquear: false } });
              aviso(estado, `Se desbloqueó a ${u.nombreUsuario}.`);
              listar();
            } catch (error) {
              aviso(estado, error.message, true);
              boton.disabled = false;
            }
          });
          return el('tr', {}, el('td', {}, u.nombreUsuario), el('td', {}, u.rol), el('td', {}, fecha(u.fechaRegistro)), el('td', {}, boton));
        });
        return [tabla(['Usuario', 'Rol', 'Registro', ''], filas), paginador(datos, pagina, p => { pagina = p; listar(); })];
      });
    }

    listar();
    return el('section', { class: 'panel' }, estado, lista);
  }

  function usuarios() {
    let pagina = 1;
    let roles = [];
    const texto = el('input', { type: 'search', id: 'buscar-usuario', maxlength: '100', placeholder: 'Nombre o correo' });
    const estado = el('p', { class: 'aviso', role: 'status' });
    const lista = el('div');
    const propio = GameRankAuth.user()?.usuarioId;

    function fila(u) {
      const selector = el('select', { 'aria-label': `Rol de ${u.nombreUsuario}`, disabled: u.usuarioId === propio },
        ...roles.map(r => el('option', { value: r.rolId, selected: r.rolId === u.rolId }, r.nombre)));
      selector.addEventListener('change', async () => {
        selector.disabled = true;
        try {
          await api(`/admin/usuarios/${u.usuarioId}/rol`, { method: 'PUT', body: { rolId: Number(selector.value) } });
          aviso(estado, `Rol de ${u.nombreUsuario} actualizado.`);
          u.rolId = Number(selector.value);
        } catch (error) {
          aviso(estado, error.message, true);
          selector.value = String(u.rolId);
        } finally { selector.disabled = false; }
      });
      return el('tr', {}, el('td', {}, u.nombreUsuario), el('td', {}, u.email || '–'), el('td', {}, selector),
        el('td', {}, u.bloqueado ? 'Bloqueada' : 'Activa'), el('td', {}, fecha(u.ultimoLogin)));
    }

    function listar() {
      cargar(lista, async () => {
        if (!roles.length) roles = await api('/admin/roles');
        const datos = await api(`/admin/usuarios?${query({ texto: texto.value.trim(), pagina, tamanioPagina: 20 })}`);
        if (!datos.items.length) return el('div', { class: 'status' }, 'No se encontraron usuarios.');
        return [tabla(['Usuario', 'Correo', 'Rol', 'Cuenta', 'Último ingreso'], datos.items.map(fila)),
          paginador(datos, pagina, p => { pagina = p; listar(); })];
      });
    }

    texto.addEventListener('input', debounce(() => { pagina = 1; listar(); }));
    listar();
    return el('section', { class: 'panel' },
      el('div', { class: 'field' }, el('label', { for: 'buscar-usuario' }, 'Buscar usuario'), texto),
      el('p', { class: 'meta' }, 'No podés cambiar tu propio rol.'), estado, lista);
  }

  function auditoria() {
    let pagina = 1;
    const lista = el('div');

    function listar() {
      cargar(lista, async () => {
        const datos = await api(`/admin/auditoria?${query({ pagina, tamanioPagina: 20 })}`);
        if (!datos.items.length) return el('div', { class: 'status' }, 'Todavía no hay acciones registradas.');
        const filas = datos.items.map(a => el('tr', {},
          el('td', {}, fecha(a.fecha)), el('td', {}, a.actor || '–'), el('td', {}, a.accion),
          el('td', {}, `${a.entidadTipo} #${a.entidadId ?? '–'}`), el('td', {}, a.motivo || a.detalle || '–')));
        return [tabla(['Fecha', 'Responsable', 'Acción', 'Entidad', 'Motivo'], filas), paginador(datos, pagina, p => { pagina = p; listar(); })];
      });
    }

    listar();
    return el('section', { class: 'panel' }, lista);
  }

  function respaldos() {
    let pagina = 1;
    const tipo = el('select', { id: 'tipo-respaldo' },
      el('option', { value: 'COMPLETO' }, 'Completo'),
      el('option', { value: 'DIFERENCIAL' }, 'Diferencial'),
      el('option', { value: 'LOG' }, 'Log de transacciones'),
      el('option', { value: 'EXPORTACION' }, 'Exportación CSV'));
    const generar = el('button', { type: 'submit', class: 'btn' }, 'Generar respaldo');
    const confirmacion = el('input', { type: 'text', id: 'confirmar-restauracion', autocomplete: 'off', placeholder: 'RESTAURAR' });
    const estado = el('p', { class: 'aviso', role: 'status' });
    const lista = el('div');
    const tamanio = bytes => bytes == null ? '–' : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / 1048576).toFixed(1)} MB`;

    async function descargar(r, boton) {
      boton.disabled = true;
      try {
        const respuesta = await fetch(`${GameRankApi.baseUrl}/admin/respaldos/${r.respaldoId}/descarga`, { headers: { Authorization: `Bearer ${GameRankAuth.token()}` } });
        if (!respuesta.ok) {
          const datos = await respuesta.json().catch(() => null);
          throw new Error(datos?.mensaje || 'No se pudo descargar el respaldo.');
        }
        const url = URL.createObjectURL(await respuesta.blob());
        el('a', { href: url, download: r.nombreArchivo }).click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      } catch (error) {
        aviso(estado, error.message, true);
      } finally { boton.disabled = false; }
    }

    async function restaurar(r, boton) {
      if (confirmacion.value.trim() !== 'RESTAURAR') {
        aviso(estado, 'Escribí RESTAURAR en el campo de confirmación para restaurar un respaldo.', true);
        confirmacion.focus();
        return;
      }
      boton.disabled = true;
      aviso(estado, 'Restaurando la base. Puede tardar unos segundos…');
      try {
        await api(`/admin/respaldos/${r.respaldoId}/restauracion`, { method: 'POST', body: { confirmacion: 'RESTAURAR' }, timeout: 120000 });
        confirmacion.value = '';
        aviso(estado, `Se restauró ${r.nombreArchivo}. Antes se generó un respaldo de seguridad del estado anterior.`);
        listar();
      } catch (error) {
        aviso(estado, error.message, true);
        boton.disabled = false;
      }
    }

    function fila(r) {
      const disponible = r.estado === 'OK' || r.estado === 'RESTAURADO';
      const acciones = el('div', { class: 'review-actions' });
      if (disponible) {
        const bajar = el('button', { type: 'button', class: 'btn btn-ghost' }, 'Descargar');
        bajar.addEventListener('click', () => descargar(r, bajar));
        acciones.append(bajar);
        if (r.tipo !== 'EXPORTACION') {
          const volver = el('button', { type: 'button', class: 'btn btn-danger' }, 'Restaurar');
          volver.addEventListener('click', () => restaurar(r, volver));
          acciones.append(volver);
        }
      }
      return el('tr', {}, el('td', {}, fecha(r.fechaInicio)), el('td', {}, r.tipo), el('td', {}, r.estado),
        el('td', {}, tamanio(r.tamanioBytes)), el('td', {}, r.nombreUsuario), el('td', {}, r.mensajeError ? el('span', { class: 'meta' }, r.mensajeError) : acciones));
    }

    function listar() {
      cargar(lista, async () => {
        const datos = await api(`/admin/respaldos?${query({ pagina, tamanioPagina: 10 })}`);
        if (!datos.items.length) return el('div', { class: 'status' }, 'Todavía no se generaron respaldos.');
        return [tabla(['Fecha', 'Tipo', 'Estado', 'Tamaño', 'Generado por', ''], datos.items.map(fila)),
          paginador(datos, pagina, p => { pagina = p; listar(); })];
      });
    }

    const formulario = el('form', { class: 'admin-controles' },
      el('div', { class: 'field' }, el('label', { for: 'tipo-respaldo' }, 'Tipo de respaldo'), tipo),
      el('div', { class: 'field', style: 'align-self:end' }, generar));
    formulario.addEventListener('submit', async event => {
      event.preventDefault();
      generar.disabled = true;
      aviso(estado, 'Generando respaldo…');
      try {
        await api('/admin/respaldos', { method: 'POST', body: { tipo: tipo.value }, timeout: 120000 });
        aviso(estado, 'Respaldo generado correctamente.');
        pagina = 1;
        listar();
      } catch (error) {
        aviso(estado, error.message, true);
      } finally { generar.disabled = false; }
    });

    listar();
    return el('section', { class: 'panel' }, formulario,
      el('div', { class: 'field' }, el('label', { for: 'confirmar-restauracion' }, 'Confirmación para restaurar'), confirmacion,
        el('p', { class: 'meta' }, 'Restaurar reemplaza la base actual. Escribí RESTAURAR y después elegí el respaldo.')),
      estado, lista);
  }

  function mostrar(id) {
    const rol = GameRankAuth.user()?.rol;
    const disponibles = SECCIONES.filter(s => s.roles.includes(rol));
    actual = disponibles.find(s => s.id === id) || disponibles[0];
    version++;
    secciones.replaceChildren(...disponibles.map(s => el('button', {
      type: 'button', class: 'tab', role: 'tab', 'aria-selected': String(s === actual), 'aria-pressed': String(s === actual),
      onclick: () => { history.replaceState(null, '', `#${s.id}`); mostrar(s.id); }
    }, s.titulo)));
    contenido.replaceChildren(actual.render());
  }

  function iniciar() {
    const usuario = GameRankAuth.user();
    if (!usuario) {
      secciones.replaceChildren();
      showStatus(contenido, 'Ingresá con una cuenta de administración para usar esta sección.');
      contenido.firstChild.append(' ', el('a', { class: 'btn', href: 'login.html' }, 'Ingresar'));
      return;
    }
    if (![ADMIN, SUPER].includes(usuario.rol)) {
      secciones.replaceChildren();
      showStatus(contenido, 'Tu cuenta no tiene permisos de administración.', 'error');
      return;
    }
    mostrar(location.hash.slice(1));
  }

  window.addEventListener('hashchange', iniciar);
  window.addEventListener('gamerank:sessionchange', iniciar);
  GameRankAuth.ready.then(iniciar);
})();

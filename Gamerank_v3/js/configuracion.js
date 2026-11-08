(() => {
  cabecera('perfil.html');
  pie();
  const main = $('#main');
  let version = 0;

  function panel(title, ...children) {
    return el('section', { class: 'panel form-panel' }, el('h2', {}, title), ...children);
  }

  function render(account) {
    const owner = GameRankAuth.token();
    const status = el('p', { id: 'aviso', class: 'aviso', role: 'alert', tabindex: '-1' });
    const logout = el('button', { class: 'btn btn-ghost', type: 'button', id: 'cerrar-sesion' }, 'Cerrar sesión');
    const password = campo('password-baja', 'Contraseña actual', 'password', { autocomplete: 'current-password', required: true });
    const acceptance = el('input', { type: 'checkbox', id: 'confirmar-baja', required: true, 'aria-describedby': 'e-confirmar-baja' });
    const submit = el('button', { class: 'btn btn-danger', type: 'submit', id: 'eliminar-cuenta' }, 'Eliminar mi cuenta');
    const deletion = el('form', { id: 'baja-form', novalidate: true }, password,
      el('label', { class: 'check', for: 'confirmar-baja' }, acceptance, 'Entiendo que la baja es permanente y elimina mi cuenta, reseñas, calificaciones y listas.'),
      el('p', { class: 'field-error', id: 'e-confirmar-baja' }), submit);
    const privacy = el('a', { href: 'reviews.html#privacidad' }, 'Leer el aviso de privacidad');
    const passwordSubmit = el('button', { class: 'btn', type: 'submit', id: 'guardar-password' }, 'Cambiar contraseña');
    const passwordStatus = el('p', { class: 'aviso', role: 'alert', id: 'password-aviso', tabindex: '-1' });
    const passwordForm = el('form', { id: 'password-form', method: 'post', novalidate: true },
      campo('password-actual', 'Contraseña actual', 'password', { autocomplete: 'current-password', required: true }),
      campo('password-nueva', 'Nueva contraseña', 'password', { autocomplete: 'new-password', required: true, 'aria-describedby': 'e-password-nueva password-ayuda' }),
      campo('password-confirmacion', 'Confirmar nueva contraseña', 'password', { autocomplete: 'new-password', required: true }),
      el('p', { id: 'password-ayuda', class: 'meta' }, 'Usá al menos 8 caracteres, una letra y un número, y hasta 72 bytes UTF-8. Se conservan los espacios.'),
      el('p', { class: 'meta' }, 'El cambio cierra todas tus sesiones. Después deberás ingresar con la nueva contraseña.'), passwordStatus, passwordSubmit);
    const registered = GameRankData.date(account.fechaRegistro)?.toLocaleDateString('es-AR') || 'Fecha no disponible';
    main.replaceChildren(el('h1', { style: 'margin-top:1.5rem' }, 'Configuración'), status,
      panel('Tu cuenta', el('p', {}, `Usuario: ${account.nombreUsuario}`), el('p', {}, `Rol: ${account.rol}`),
        el('p', {}, `Miembro desde: ${registered}`),
        el('div', { class: 'row-btns' }, el('a', { class: 'btn btn-ghost', href: 'perfil.html' }, 'Ver mi perfil'),
          el('a', { class: 'btn btn-ghost', href: 'editar-perfil.html' }, 'Edición de perfil'), logout)),
      panel('Correo electrónico', campo('email', 'Correo', 'email', { disabled: true, value: account.email }),
        el('p', { class: 'meta' }, 'Este es tu correo de cuenta; no se publica en tu perfil. El cambio de correo todavía no está disponible.'),
        el('button', { class: 'btn', type: 'button', disabled: true }, 'Guardar correo (no disponible)')),
      panel('Contraseña', passwordForm),
      panel('Privacidad', el('p', { class: 'meta' }, 'No hay preferencias de visibilidad configurables en el servidor. Otros usuarios autenticados pueden consultar perfiles y bibliotecas.'),
        el('p', { class: 'meta' }, 'Podés editar tu nombre y avatar desde Edición de perfil. El cambio de correo y las preferencias no están disponibles. La aceptación del aviso al registrarse no acredita por sí sola cumplimiento legal.'),
        privacy, el('div', { class: 'row-btns' }, el('button', { class: 'btn', type: 'button', disabled: true }, 'Guardar privacidad (no disponible)'))),
      panel('Eliminar cuenta', el('p', {}, 'La baja elimina tu cuenta y sus reseñas, calificaciones, listas y sesiones del servidor. Es permanente. El servidor puede rechazarla si existen registros de auditoría o respaldos vinculados.'),
        el('p', { class: 'meta' }, 'La baja no borra datos demo antiguos guardados en este navegador ni acredita la eliminación de copias de seguridad existentes.'), deletion));

    let changingPassword = false;
    passwordForm.addEventListener('submit', async event => {
      event.preventDefault();
      if (changingPassword || owner !== GameRankAuth.token()) return;
      passwordStatus.textContent = '';
      const actual = $('#password-actual');
      const nueva = $('#password-nueva');
      const confirmation = $('#password-confirmacion');
      const characters = nueva.value.split('');
      const validActual = errorCampo(actual.id, actual.value ? '' : 'Ingresá tu contraseña actual.');
      const validNew = errorCampo(nueva.id, nueva.value.length >= 8 && new TextEncoder().encode(nueva.value).length <= 72
        && characters.some(character => /\p{L}/u.test(character)) && characters.some(character => /\p{Nd}/u.test(character))
        ? '' : 'Usá al menos 8 caracteres, una letra y un número, y hasta 72 bytes UTF-8.');
      const validConfirmation = errorCampo(confirmation.id, confirmation.value && confirmation.value === nueva.value ? '' : 'Las contraseñas no coinciden.');
      if (!validActual || !validNew || !validConfirmation) {
        passwordForm.querySelector('[aria-invalid="true"]').focus();
        return;
      }
      changingPassword = true;
      const controls = Array.from(passwordForm.querySelectorAll('input, button'));
      controls.forEach(control => { control.disabled = true; });
      submit.disabled = true;
      logout.disabled = true;
      passwordSubmit.textContent = 'Cambiando contraseña…';
      passwordForm.setAttribute('aria-busy', 'true');
      try {
        await CuentaStore.cambiarPassword(actual.value, nueva.value);
        if (!GameRankAuth.user()) location.replace('login.html');
      } catch (error) {
        if (owner !== GameRankAuth.token()) return;
        passwordStatus.textContent = error.status === 403 ? 'La contraseña actual es incorrecta. La contraseña no cambió.'
          : error.status === 429 ? 'Demasiados intentos. Esperá un minuto antes de volver a intentar.'
          : error.status === 0 ? `${error.message} No pudimos confirmar el cambio. Verificá tus credenciales antes de reintentar.` : error.message;
        passwordStatus.focus();
      } finally {
        actual.value = '';
        nueva.value = '';
        confirmation.value = '';
        controls.forEach(control => { control.disabled = false; });
        submit.disabled = false;
        logout.disabled = false;
        passwordSubmit.textContent = 'Cambiar contraseña';
        passwordForm.removeAttribute('aria-busy');
        changingPassword = false;
      }
    });

    logout.addEventListener('click', async () => {
      if (owner !== GameRankAuth.token()) return;
      logout.disabled = true;
      status.textContent = '';
      try {
        await GameRankAuth.logout();
        if (!GameRankAuth.user()) location.href = 'index.html';
      } catch (error) {
        if (owner === GameRankAuth.token()) { status.textContent = error.message; status.focus(); }
      } finally { logout.disabled = false; }
    });

    let sending = false;
    deletion.addEventListener('submit', async event => {
      event.preventDefault();
      if (sending || owner !== GameRankAuth.token()) return;
      status.textContent = '';
      const input = $('#password-baja');
      const validPassword = errorCampo('password-baja', input.value ? '' : 'Ingresá tu contraseña actual.');
      acceptance.setAttribute('aria-invalid', String(!acceptance.checked));
      $('#e-confirmar-baja').textContent = acceptance.checked ? '' : 'Confirmá que comprendés la eliminación permanente.';
      if (!validPassword || !acceptance.checked) {
        (validPassword ? acceptance : input).focus();
        return;
      }
      if (!confirm('¿Eliminar definitivamente tu cuenta y sus reseñas, calificaciones y listas? No se puede deshacer.')) {
        input.value = '';
        return;
      }
      sending = true;
      deletion.setAttribute('aria-busy', 'true');
      submit.disabled = true;
      logout.disabled = true;
      input.disabled = true;
      acceptance.disabled = true;
      submit.textContent = 'Eliminando cuenta…';
      try {
        await CuentaStore.eliminar(input.value);
        if (!GameRankAuth.user()) location.href = 'index.html';
      } catch (error) {
        if (owner !== GameRankAuth.token()) return;
        status.textContent = error.status === 403 ? 'La contraseña es incorrecta. La cuenta no fue eliminada.'
          : error.status === 409 ? 'El servidor rechazó la baja por registros de auditoría o respaldos vinculados. Consultá al equipo responsable.'
          : error.status === 0 ? `${error.message} No se pudo confirmar la baja; verificá el estado de tu cuenta antes de reintentar.` : error.message;
        status.focus();
      } finally {
        input.value = '';
        acceptance.checked = false;
        input.disabled = false;
        acceptance.disabled = false;
        submit.disabled = false;
        logout.disabled = false;
        submit.textContent = 'Eliminar mi cuenta';
        deletion.removeAttribute('aria-busy');
        sending = false;
      }
    });
  }

  async function load() {
    const current = ++version;
    showStatus(main, 'Cargando tu cuenta…');
    main.setAttribute('aria-busy', 'true');
    try {
      const account = await CuentaStore.get();
      if (current === version) render(account);
    } catch (error) {
      if (current !== version) return;
      showStatus(main, error.message, 'error');
      main.firstChild.append(' ', error.status === 401 ? el('a', { class: 'btn', href: 'login.html' }, 'Ingresar')
        : el('button', { class: 'btn btn-ghost', type: 'button', onclick: load }, 'Reintentar'));
    } finally { if (current === version) main.removeAttribute('aria-busy'); }
  }

  window.addEventListener('gamerank:sessionchange', load);
  GameRankAuth.ready.then(load);
  window.addEventListener('pageshow', event => { if (event.persisted) load(); });
})();

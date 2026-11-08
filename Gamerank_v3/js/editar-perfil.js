(() => {
  cabecera('perfil.html');
  pie();
  const main = $('#main');
  let version = 0;
  let savingToken = null;

  function render(account, message = '', failed = false) {
    const owner = GameRankAuth.token();
    const status = el('p', { id: 'aviso', class: `aviso${failed ? '' : ' ok'}`, role: 'status', tabindex: '-1' }, message);
    const name = campo('nombreUsuario', 'Nombre de usuario', 'text', { value: account.nombreUsuario, autocomplete: 'username', required: true, maxlength: 30 });
    const preview = el('div', { id: 'previa' }, avatar(account.avatarUrl, account.nombreUsuario, true));
    const options = el('div', { class: 'row-btns' });
    const currentAvatar = account.avatarUrl === null ? '' : Object.entries(account.avatares).find(([, url]) => url === account.avatarUrl)?.[0];
    const entries = [['', null], ...Object.entries(account.avatares)];
    for (const [id, url] of entries) {
      const input = el('input', { type: 'radio', name: 'avatarId', value: id, id: `avatar-${id || 'ninguno'}`, checked: id === currentAvatar });
      input.addEventListener('change', () => preview.replaceChildren(avatar(url, $('#nombreUsuario').value, true)));
      options.append(el('label', { class: 'check', for: input.id }, input,
        el('span', {}, avatar(url, account.nombreUsuario, false), id ? id[0].toUpperCase() + id.slice(1) : 'Sin avatar')));
    }
    const submit = el('button', { class: 'btn', type: 'submit', id: 'guardar' }, 'Guardar cambios');
    const form = el('form', { id: 'perfil-form', method: 'post', novalidate: true }, name,
      el('fieldset', { style: 'border:0;padding:0;margin:0' }, el('legend', {}, 'Avatar'), options),
      el('p', { class: 'meta' }, 'Elegí un avatar predefinido o quitá el actual. No se admiten archivos ni direcciones externas.'),
      el('div', { class: 'row-btns' }, submit, el('a', { class: 'btn btn-ghost', href: 'perfil.html' }, 'Volver al perfil'),
        el('a', { class: 'btn btn-ghost', href: 'configuracion.html' }, 'Configuración')));
    main.replaceChildren(el('section', { class: 'panel form-panel' }, el('h1', { style: 'font-size:1.6rem' }, 'Editar perfil'), preview, status, form,
      el('p', { class: 'privacy' }, el('a', { href: 'reviews.html#privacidad' }, 'Leer el aviso de privacidad'), '. Podés rectificar tu nombre y avatar. El cambio de correo y las preferencias todavía no están disponibles.')));
    let sending = false;
    form.addEventListener('submit', async event => {
      event.preventDefault();
      if (sending || owner !== GameRankAuth.token()) return;
      status.textContent = '';
      const nombreUsuario = $('#nombreUsuario').value.trim();
      if (!errorCampo('nombreUsuario', nombreUsuario.length >= 3 && nombreUsuario.length <= 30 ? '' : 'El nombre de usuario debe tener entre 3 y 30 caracteres.')) {
        $('#nombreUsuario').focus();
        return;
      }
      const choice = form.querySelector('input[name="avatarId"]:checked');
      if (!choice || choice.value && !Object.hasOwn(account.avatares, choice.value)) {
        status.textContent = 'Seleccioná un avatar disponible o la opción Sin avatar.';
        status.classList.remove('ok');
        status.focus();
        return;
      }
      sending = true;
      savingToken = owner;
      form.setAttribute('aria-busy', 'true');
      const controls = Array.from(form.querySelectorAll('input, button'));
      controls.forEach(control => { control.disabled = true; });
      submit.textContent = 'Guardando…';
      let saved = null;
      try {
        saved = await CuentaStore.guardar(nombreUsuario, choice.value || null);
        await GameRankAuth.validateSession(true);
        if (owner !== GameRankAuth.token()) return;
        render(saved, 'Nombre y avatar guardados correctamente.');
      } catch (error) {
        if (owner !== GameRankAuth.token()) return;
        status.classList.remove('ok');
        status.textContent = saved ? 'Los cambios se guardaron, pero no pudimos actualizar la sesión visible. Recargá la página.'
          : error.status === 409 ? 'Ese nombre de usuario ya está en uso. Elegí otro.'
          : error.status === 0 ? `${error.message} No se pudo confirmar el resultado; recargá para comprobar tu perfil antes de reintentar.` : error.message;
        status.focus();
      } finally {
        controls.forEach(control => { control.disabled = false; });
        submit.textContent = 'Guardar cambios';
        form.removeAttribute('aria-busy');
        sending = false;
        savingToken = null;
      }
    });
  }

  async function load() {
    const current = ++version;
    showStatus(main, 'Cargando tu perfil…');
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

  window.addEventListener('gamerank:sessionchange', () => { if (!savingToken || savingToken !== GameRankAuth.token()) load(); });
  GameRankAuth.ready.then(load);
  window.addEventListener('pageshow', event => { if (event.persisted) load(); });
})();

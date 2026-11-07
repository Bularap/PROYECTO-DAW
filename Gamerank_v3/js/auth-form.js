(() => {
  const form = document.querySelector('#auth-form');
  const isRegister = document.body.dataset.authPage === 'register';
  const submit = form.querySelector('button[type="submit"]');
  const status = document.querySelector('#form-status');
  const fields = Array.from(form.querySelectorAll('input'));
  let submitting = false;

  function showError(field, message) {
    document.querySelector(`#err-${field.id}`).textContent = message;
    field.setAttribute('aria-invalid', message ? 'true' : 'false');
    return !message;
  }

  function validate() {
    let valid = true;
    for (const field of fields) {
      let message = '';
      const value = field.type === 'password' ? field.value : field.value.trim();
      if (field.type === 'checkbox') {
        if (!field.checked) message = 'Tenés que aceptar la política de privacidad para crear tu cuenta.';
      } else if (!value) {
        message = 'Completá este campo.';
      } else if (field.id === 'email') {
        if (value.length > 255 || !/^[^\s@]+@[^\s@.][^\s@]*\.[^\s@.]+$/.test(value) || field.validity.typeMismatch) {
          message = 'Ingresá un correo electrónico válido, por ejemplo nombre@correo.com.';
        }
      } else if (field.id === 'nombreUsuario' && (value.length < 3 || value.length > 30)) {
        message = 'El nombre de usuario debe tener entre 3 y 30 caracteres.';
      } else if (field.id === 'password' && isRegister) {
        const characters = value.split('');
        if (value.length < 8 || new TextEncoder().encode(value).length > 72
          || !characters.some(character => /\p{L}/u.test(character))
          || !characters.some(character => /\p{Nd}/u.test(character))) {
          message = 'Usá al menos 8 caracteres, una letra y un número, y hasta 72 bytes UTF-8.';
        }
      } else if (field.id === 'confirmPassword' && value !== form.elements.password.value) {
        message = 'Las contraseñas no coinciden.';
      }
      if (!showError(field, message)) valid = false;
    }
    return valid;
  }

  for (const field of fields) {
    field.addEventListener('input', () => {
      showError(field, '');
      status.textContent = '';
    });
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submitting) return;
    status.textContent = '';
    if (!validate()) {
      status.textContent = 'Revisá los campos marcados antes de continuar.';
      form.querySelector('[aria-invalid="true"]').focus();
      return;
    }
    submitting = true;
    submit.disabled = true;
    submit.textContent = isRegister ? 'Creando cuenta…' : 'Ingresando…';
    form.setAttribute('aria-busy', 'true');
    try {
      await GameRankAuth.ready;
      const body = {
        email: form.elements.email.value.trim(),
        password: form.elements.password.value
      };
      if (isRegister) {
        body.nombreUsuario = form.elements.nombreUsuario.value.trim();
        body.aceptaPrivacidad = form.elements.aceptaPrivacidad.checked;
      }
      const data = await GameRankAuth.request(isRegister ? 'registro' : 'login', { body });
      GameRankAuth.startSession(data);
      form.reset();
    } catch (error) {
      status.textContent = error.message;
      status.focus();
    } finally {
      submitting = false;
      submit.disabled = false;
      submit.textContent = isRegister ? 'Crear cuenta' : 'Ingresar';
      form.removeAttribute('aria-busy');
    }
  });
})();

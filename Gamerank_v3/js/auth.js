const GameRankAuth = (() => {
  const storageKey = 'gamerank_token';
  let currentUser = null;
  let validation = null;
  let logoutRequest = null;
  let deletionRequest = null;
  let passwordRequest = null;

  function showMessage(message) {
    const status = document.querySelector('#session-status');
    if (!status) return;
    status.textContent = message;
    status.hidden = !message;
  }

  function token() {
    try {
      return sessionStorage.getItem(storageKey);
    } catch {
      showMessage('Habilitá el almacenamiento del navegador para iniciar sesión.');
      return null;
    }
  }

  function user() {
    return currentUser ? { ...currentUser } : null;
  }

  function showNavigation() {
    const nav = document.querySelector('#auth-nav');
    if (!nav) return;
    if (!currentUser) {
      const login = document.createElement('a');
      login.href = 'login.html';
      login.textContent = 'Ingresar';
      const register = document.createElement('a');
      register.href = 'register.html';
      register.textContent = 'Crear cuenta';
      if (document.body.dataset.authPage === 'login') login.setAttribute('aria-current', 'page');
      if (document.body.dataset.authPage === 'register') register.setAttribute('aria-current', 'page');
      nav.replaceChildren(login, register);
      return;
    }
    const name = document.createElement('a');
    name.className = 'session-user';
    name.href = 'perfil.html';
    name.textContent = currentUser.nombreUsuario;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'btn btn-ghost';
    button.textContent = 'Cerrar sesión';
    button.addEventListener('click', async () => {
      button.disabled = true;
      button.textContent = 'Cerrando sesión…';
      showMessage('');
      try {
        await logout();
        showMessage('Sesión cerrada.');
      } catch (error) {
        showMessage(`${error.message} No se pudo cerrar la sesión; volvé a intentarlo.`);
        button.disabled = false;
        button.textContent = 'Cerrar sesión';
      }
    });
    nav.replaceChildren(name, button);
  }

  function updateUser(value) {
    currentUser = value ? { usuarioId: value.usuarioId, nombreUsuario: value.nombreUsuario, rol: value.rol } : null;
    showNavigation();
    window.dispatchEvent(new CustomEvent('gamerank:sessionchange', { detail: user() }));
  }

  function clearSession() {
    try {
      sessionStorage.removeItem(storageKey);
    } catch {
      throw GameRankApi.createError('No se pudo quitar la sesión del navegador. Habilitá el almacenamiento e intentá nuevamente.', 0, 'storage');
    }
    updateUser(null);
  }

  function validUser(value) {
    return Number.isInteger(value?.usuarioId) && value.usuarioId > 0
      && typeof value.nombreUsuario === 'string' && value.nombreUsuario.length > 0
      && typeof value.rol === 'string';
  }

  function startSession(data) {
    if (typeof data?.token !== 'string' || !data.token || !validUser(data.usuario)) {
      throw GameRankApi.createError('La respuesta del servidor no contiene una sesión válida. Intentá ingresar nuevamente.', 0, 'response');
    }
    try {
      sessionStorage.setItem(storageKey, data.token);
    } catch {
      throw GameRankApi.createError('No se pudo guardar la sesión. Habilitá el almacenamiento del navegador e intentá ingresar.', 0, 'storage');
    }
    updateUser(data.usuario);
    showMessage('');
    return user();
  }

  async function authenticate(path, body) {
    await ready;
    const data = await GameRankApi.request(path, { method: 'POST', body, authenticated: false });
    return startSession(data);
  }

  const login = body => authenticate('/auth/login', body);
  const register = body => authenticate('/auth/registro', body);

  function validateSession(refresh = false) {
    if (validation) return refresh ? validation.catch(() => {}).then(() => validateSession()) : validation;
    const sessionToken = token();
    if (!sessionToken) {
      updateUser(null);
      return Promise.resolve(null);
    }
    const nav = document.querySelector('#auth-nav');
    nav?.setAttribute('aria-busy', 'true');
    validation = (async () => {
      try {
        const value = await GameRankApi.request('/auth/yo');
        if (!validUser(value)) throw GameRankApi.createError('No pudimos verificar la sesión: la respuesta del servidor no es válida.', 0, 'response');
        if (token() !== sessionToken) return user();
        updateUser(value);
        showMessage('');
        if (document.body.dataset.authPage) location.replace('index.html');
        return user();
      } catch (error) {
        if (token() === sessionToken && (error.status === 401 || error.status === 403)) clearSession();
        showMessage(error.status === 401 || error.status === 403
          ? 'Tu sesión venció o no está disponible. Volvé a ingresar.'
          : `${error.message} No pudimos verificar tu sesión.`);
        throw error;
      } finally {
        validation = null;
        nav?.removeAttribute('aria-busy');
      }
    })();
    return validation;
  }

  async function request(path, options = {}) {
    await ready;
    return GameRankApi.request(path, options);
  }

  function logout() {
    if (logoutRequest) return logoutRequest;
    logoutRequest = (async () => {
      await ready;
      await GameRankApi.request('/auth/logout', { method: 'POST' });
      clearSession();
    })().finally(() => { logoutRequest = null; });
    return logoutRequest;
  }

  function deleteAccount(password) {
    if (deletionRequest) return deletionRequest;
    const requestedToken = token();
    deletionRequest = (async () => {
      await ready;
      const owner = user();
      const sessionToken = token();
      if (sessionToken !== requestedToken) throw GameRankApi.createError('La sesión cambió. Volvé a confirmar la baja desde tu cuenta.', 401, 'session');
      if (!owner || !sessionToken) throw GameRankApi.createError('Ingresá para eliminar tu cuenta.', 401, 'session');
      if (typeof password !== 'string' || !password) throw GameRankApi.createError('Ingresá tu contraseña actual.', 400, 'validation');
      const result = await GameRankApi.request('/auth/baja', { method: 'POST', body: { password } });
      if (result !== null) throw GameRankApi.createError('El servidor no confirmó la baja. Verificá el estado de tu cuenta antes de reintentar.', 0, 'response');
      if (token() !== sessionToken || user()?.usuarioId !== owner.usuarioId) {
        throw GameRankApi.createError('La sesión cambió durante la solicitud. La cuenta anterior fue eliminada; la sesión actual se conserva.', 0, 'session');
      }
      clearSession();
    })().finally(() => { deletionRequest = null; });
    return deletionRequest;
  }

  function changePassword(passwordActual, passwordNueva) {
    if (passwordRequest) return passwordRequest;
    const requestedToken = token();
    passwordRequest = (async () => {
      await ready;
      const owner = user();
      if (!owner || !requestedToken || token() !== requestedToken) throw GameRankApi.createError('La sesión cambió. Volvé a ingresar antes de cambiar la contraseña.', 401, 'session');
      if (typeof passwordActual !== 'string' || !passwordActual || typeof passwordNueva !== 'string' || !passwordNueva) throw GameRankApi.createError('Completá la contraseña actual y la nueva.', 400, 'validation');
      const result = await GameRankApi.request('/auth/password', { method: 'PUT', body: { passwordActual, passwordNueva } });
      if (result !== null) throw GameRankApi.createError('El servidor no confirmó el cambio. Verificá tus credenciales antes de reintentar.', 0, 'response');
      if (token() !== requestedToken || user()?.usuarioId !== owner.usuarioId) throw GameRankApi.createError('La contraseña de la cuenta anterior cambió. La sesión actual se conserva.', 0, 'session');
      clearSession();
    })().finally(() => { passwordRequest = null; });
    return passwordRequest;
  }

  GameRankApi.configureAuth({
    getToken: token,
    onUnauthorized: expiredToken => {
      if (token() !== expiredToken) return;
      clearSession();
      showMessage('Tu sesión venció. Volvé a ingresar.');
    }
  });

  const ready = new Promise(resolve => {
    const initialize = async () => {
      showNavigation();
      try { await validateSession(); } catch {}
      resolve(user());
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
    else initialize();
  });

  window.addEventListener('pageshow', event => {
    if (event.persisted) validateSession().catch(() => {});
  });

  return { token, user, login, register, logout, deleteAccount, changePassword, validateSession, request, startSession, ready };
})();

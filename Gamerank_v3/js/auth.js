const GameRankAuth = (() => {
  const apiUrl = 'http://localhost:5251/api/auth';
  const storageKey = 'gamerank_token';
  const nav = document.querySelector('#auth-nav');
  const status = document.querySelector('#session-status');

  function showMessage(message) {
    status.textContent = message;
    status.hidden = !message;
  }

  async function request(endpoint, { body, token } = {}) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);
    const headers = {};
    if (body) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;
    try {
      const response = await fetch(`${apiUrl}/${endpoint}`, {
        method: endpoint === 'yo' ? 'GET' : 'POST',
        headers,
        body: body ? JSON.stringify(body) : undefined,
        signal: controller.signal
      });
      const data = response.status === 204 ? null : await response.json().catch(() => null);
      if (!response.ok) {
        let message = 'No pudimos completar la solicitud. Intentá nuevamente.';
        if (response.status < 500 && typeof data?.mensaje === 'string') message = data.mensaje;
        if (endpoint === 'login' && response.status === 401) message = 'Correo o contraseña incorrectos.';
        if (response.status === 403) message = 'La cuenta se encuentra suspendida.';
        if (endpoint === 'registro' && response.status === 409) {
          message = data?.mensaje || 'El correo o el nombre de usuario ya se encuentra registrado.';
        }
        const error = new Error(message);
        error.status = response.status;
        throw error;
      }
      return data;
    } catch (error) {
      if (error.status) throw error;
      throw new Error(error.name === 'AbortError'
        ? 'El servidor tardó demasiado en responder. Intentá nuevamente.'
        : 'No pudimos conectar con el servidor. Revisá la conexión e intentá nuevamente.');
    } finally {
      clearTimeout(timeout);
    }
  }

  function showNavigation(user) {
    if (!user) {
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
    const name = document.createElement('span');
    name.className = 'session-user';
    name.textContent = user.nombreUsuario;
    const logout = document.createElement('button');
    logout.type = 'button';
    logout.className = 'btn btn-ghost';
    logout.textContent = 'Cerrar sesión';
    logout.addEventListener('click', async () => {
      logout.disabled = true;
      logout.textContent = 'Cerrando sesión…';
      showMessage('');
      try {
        await request('logout', { token: sessionStorage.getItem(storageKey) });
        sessionStorage.removeItem(storageKey);
        showNavigation(null);
        showMessage('Sesión cerrada.');
      } catch (error) {
        showMessage(`${error.message} No se pudo cerrar la sesión; volvé a intentarlo.`);
        logout.disabled = false;
        logout.textContent = 'Cerrar sesión';
      }
    });
    nav.replaceChildren(name, logout);
  }

  function startSession(data) {
    if (typeof data?.token !== 'string' || !data.token || typeof data.usuario?.nombreUsuario !== 'string') {
      throw new Error('La respuesta del servidor no contiene una sesión válida. Intentá ingresar nuevamente.');
    }
    try {
      sessionStorage.setItem(storageKey, data.token);
    } catch {
      throw new Error('No se pudo guardar la sesión. Habilitá el almacenamiento del navegador e intentá ingresar.');
    }
    showNavigation(data.usuario);
    location.replace('index.html');
  }

  async function restoreSession() {
    let token;
    try {
      token = sessionStorage.getItem(storageKey);
    } catch {
      showMessage('Habilitá el almacenamiento del navegador para iniciar sesión.');
      return;
    }
    if (!token) return;
    nav.setAttribute('aria-busy', 'true');
    try {
      const user = await request('yo', { token });
      if (typeof user?.nombreUsuario !== 'string') throw new Error('No pudimos verificar la sesión. Intentá ingresar nuevamente.');
      showNavigation(user);
      if (document.body.dataset.authPage) location.replace('index.html');
    } catch (error) {
      if (error.status === 401 || error.status === 403) {
        sessionStorage.removeItem(storageKey);
        showNavigation(null);
        showMessage(error.status === 403 ? 'La cuenta se encuentra suspendida.' : 'Tu sesión venció. Volvé a ingresar.');
      } else {
        showMessage(`${error.message} No pudimos verificar tu sesión.`);
      }
    } finally {
      nav.removeAttribute('aria-busy');
    }
  }

  showNavigation(null);
  const ready = restoreSession();
  window.addEventListener('pageshow', event => {
    if (event.persisted) {
      showNavigation(null);
      restoreSession();
    }
  });
  return { request, startSession, ready };
})();

const GameRankApi = (() => {
  const baseUrl = 'http://localhost:5251/api';
  let getToken = () => null;
  let onUnauthorized = () => {};

  function createError(message, status = 0, code = 'http') {
    return Object.assign(new Error(message), { status, code });
  }

  function configureAuth(handlers) {
    getToken = handlers.getToken;
    onUnauthorized = handlers.onUnauthorized;
  }

  async function request(path, { method = 'GET', body, authenticated = true, timeout = 10000 } = {}) {
    if (typeof path !== 'string' || !path.startsWith('/') || path.startsWith('//') || path.includes('://')) {
      throw createError('La ruta de la API no es válida.', 0, 'request');
    }
    const token = authenticated ? getToken() : null;
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeout);
    try {
      const response = await fetch(`${baseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal
      });
      if (response.status === 401 && token) onUnauthorized(token);
      if (response.status === 204) return null;
      const text = await response.text();
      let data = null;
      try {
        data = text ? JSON.parse(text) : null;
      } catch {
        if (response.ok) throw createError('El servidor devolvió una respuesta inesperada.', response.status, 'response');
      }
      if (!response.ok) {
        let message = 'No pudimos completar la solicitud. Intentá nuevamente.';
        if (response.status < 500 && typeof data?.mensaje === 'string') message = data.mensaje;
        if (response.status === 401 && path === '/auth/login') message = 'Correo o contraseña incorrectos.';
        else if (response.status === 401) message = 'Tu sesión venció o no es válida. Volvé a ingresar.';
        if (response.status === 403 && path === '/auth/login') message = 'La cuenta se encuentra suspendida.';
        if (response.status === 409 && path === '/auth/registro' && !data?.mensaje) {
          message = 'El correo o el nombre de usuario ya se encuentra registrado.';
        }
        throw createError(message, response.status);
      }
      if (data === null) throw createError('El servidor devolvió una respuesta inesperada.', response.status, 'response');
      return data;
    } catch (error) {
      if (typeof error.status === 'number') throw error;
      throw createError(error.name === 'AbortError'
        ? 'El servidor tardó demasiado en responder. Intentá nuevamente.'
        : 'No pudimos conectar con el servidor. Revisá la conexión e intentá nuevamente.',
      0, error.name === 'AbortError' ? 'timeout' : 'network');
    } finally {
      clearTimeout(timer);
    }
  }

  return { baseUrl, request, configureAuth, createError };
})();

# Conectar el front con la API

`api.js` reemplaza los datos de demo y el `localStorage` por la API del backend. Devuelve los juegos con la misma forma que usa hoy el front (`name`, `background_image`, `genres`, `released`, `rating`, `ratings_count`), así que cada página cambia pocas líneas.

Se probó desde `http://localhost:5500` contra la API: búsqueda, registro, reseñas, lista, feed, logout y mensajes de error.

## Antes de empezar

1. Levantar la API (`dotnet run --project backend/GameRank.API`). Queda en `http://localhost:5251`.
2. Servir el front en el puerto 5500, que es el que la API acepta por CORS. Por ejemplo, con Live Server o con:

```bash
python -m http.server 5500 --bind 127.0.0.1 --directory Gamerank_v3
```

3. Copiar `api.js` a `Gamerank_v3/js/api.js`.

## Cambios comunes a todas las páginas

1. Sacar de `js/app.js` lo que ahora resuelve `api.js`: `RAWG_KEY`, `USE_DEMO`, `RAWG_URL`, la función `rawg()`, `ReviewStore` y `WishlistStore`. Si quedan los dos `ReviewStore`, el navegador da error por declarar dos veces la misma constante.
2. En cada página, reemplazar la línea de `demo-data.js` por la de `api.js`, después de `app.js`:

```html
<script src="js/app.js"></script>
<script src="js/api.js"></script>
```

3. Cambiar en el pie de cada página el crédito de RAWG por Steam.

## Por página

### index.html

| Hoy | Con la API |
|---|---|
| `await rawg('genres')` | `await listarGeneros()` |
| `await rawg('games', { page, page_size: 12, search: q, genres: ..., ordering: ... })` | `await buscarJuegos({ page, page_size: 12, search: q, genres: ..., ordering: ... })` |

`buscarJuegos` devuelve `{ results, next, parcial }`. Si `parcial` es `true`, Steam no respondió y conviene mostrar un aviso de resultados parciales (CU04).

Los valores del selector de orden siguen funcionando. "Más populares" ordena por puntuación, porque la API no tiene un orden por popularidad.

El selector "+ Mi lista" necesita sesión iniciada. Si no la hay, `WishlistStore` lanza un error con `estado: 401`.

### reviews.html

- El autocompletado pasa de `rawg('games', { search: q, page_size: 6 })` a `buscarJuegos({ search: q, page_size: 6 })`.
- La reseña necesita el `juegoId`, así que el usuario tiene que elegir el juego de las sugerencias. Si no lo hace, `ReviewStore.add` avisa con "Elegí un juego de la lista de sugerencias".
- `ReviewStore.add` publica la reseña, guarda la puntuación y, si se eligió un estado, agrega el juego a la lista. Si la reseña falla, por ejemplo porque ya existe una para ese juego, no cambia nada más.
- `ReviewStore.all` devuelve las reseñas del usuario en sesión. Para la lista "Reseñas de la comunidad", usar `GameRankApi.resenasRecientes(pagina)`, que devuelve las reseñas recientes de todos los juegos, con `titulo`, `nombreUsuario`, `puntuacion`, `contenido` y `fechaPublicacion`.
- El campo de texto tiene `maxlength="1000"`, pero la API acepta hasta 2000 caracteres. Conviene alinearlos.

### wishlist.html y diario.html

Funcionan con los mismos métodos de `ReviewStore` y `WishlistStore`. Los juegos de la lista llegan sin géneros, fecha ni puntuación, porque el endpoint de biblioteca no los devuelve.

## Lo que falta en el front

**Registro e inicio de sesión.** No hay pantalla todavía, y casi todo lo que guarda datos la necesita. Un ejemplo con un formulario de login:

```js
$('#form-login').addEventListener('submit', async e => {
  e.preventDefault();
  try {
    await GameRankApi.login($('#email').value.trim(), $('#password').value);
    location.href = 'index.html';
  } catch (error) {
    $('#err-login').textContent = error.message;
  }
});
```

El registro es igual con `GameRankApi.registrar(email, nombreUsuario, password, aceptaPrivacidad)`. El checkbox de privacidad es obligatorio (Ley 25.326). Para cerrar sesión: `await GameRankApi.logout()`. Para saber quién está logueado: `Sesion.usuario()`.

**Errores.** Todos los errores tienen `message` listo para mostrar y `estado` con el código HTTP. Con `estado === 401` conviene llevar al usuario al login.

**Ficha del juego.** `GameRankApi.ficha(id)` devuelve sinopsis, géneros, plataformas y desarrolladoras. `GameRankApi.feed(id)` devuelve las reseñas de la comunidad para ese juego (CU12).

## Decisiones pendientes del grupo

- **Estados de la lista.** El front usa "Quiero jugarlo", "En pausa" y "Abandonado". La base y el documento técnico usan "Jugado", "Quiero jugarlo" y "Abandonado". Con "En pausa", `api.js` responde que el estado no existe. Hay que cambiar `ESTADOS_LISTA` en el front o agregar "En pausa" en la base.

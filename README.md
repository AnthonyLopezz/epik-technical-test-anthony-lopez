# Prueba Técnica para Epik: CRUD de personas en .NET

API REST hecha con ASP.NET Core (.NET 10) y SQLite para la tabla `Persona`. Incluye una interfaz web
sencilla que se sirve desde la misma API para poder probar facilmente. El listado de mujeres se consulta desde la vista `VW_Mujeres`.

## Requisitos

Solo se necesita el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0):

```powershell
winget install Microsoft.DotNet.SDK.10
```

Si no tienes permisos de administrador (puede pasar para una instalación rápida):

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
.\dotnet-install.ps1 -Channel 10.0
```

## Cómo ejecutarlo

```powershell
cd CrudNet
dotnet run --project src/Epik.Crud.Api
```

Luego abre:

- http://localhost:5080 para la interfaz web.
- http://localhost:5080/swagger para probar los endpoints (también hay un enlace en la interfaz).

La primera vez se crea `epik.db` (tabla y vista) junto al proyecto. Para cargar los datos de ejemplo de
`BaseDeDatos/seed.sql`, arranca con el argumento `seed`. Se puede repetir sin problema, porque usa
`INSERT OR IGNORE` y no duplica ni sobrescribe registros:

```powershell
dotnet run --project src/Epik.Crud.Api -- seed
```

También puedes usar la base exportada, que ya trae los datos, cambiando la cadena de conexión en
`appsettings.json`:

```json
"Epik": "Data Source=../../../BaseDeDatos/epik.db"
```

## Interfaz web

Desde la página se puede:

- Ver el listado paginado, con filtro para mostrar todas las personas o solo mujeres.
- Buscar por identificación.
- Registrar una persona.
- Editar la edad o eliminar un registro (pide confirmación antes de borrar).

Los formularios validan cada campo antes de enviar, y los mensajes del servidor se muestran como
notificaciones. 

## Endpoints

| Acción | Método y ruta | Cuerpo | Respuestas |
|---|---|---|---|
| Insertar | `POST /api/personas` | `{ "identificacion": "2001", "nombres": "Sofía", "apellidos": "Ramírez", "edad": 30, "genero": "Femenino" }` | 201, 400, 409 si ya existe |
| Listar todas | `GET /api/personas?page=1&pageSize=10` | | 200, 400 |
| Buscar por identificación | `GET /api/personas/{identificacion}` | | 200, 404 |
| Listar mujeres (vista `VW_Mujeres`) | `GET /api/personas/mujeres?page=1&pageSize=10` | | 200, 400 |
| Actualizar edad | `PATCH /api/personas/{identificacion}/edad` | `{ "edad": 31 }` | 200, 400, 404 |
| Eliminar | `DELETE /api/personas/{identificacion}` | | 200, 404 |

### Respuestas

Todas las respuestas, de éxito o de error, tienen la misma forma. El código HTTP indica el resultado:

```json
{ "success": true, "message": "Persona encontrada.",
  "data": { "identificacion": "2001", "nombres": "Jane", "apellidos": "Doe", "edad": 30, "genero": "Femenino" } }
```

```json
{ "success": false, "message": "No se encontró una persona con esa identificación.", "data": null }
```

Los listados incluyen además `pagination`. `page` empieza en 1 y `pageSize` va de 1 a 100 (por defecto 1
y 10). Un valor fuera de rango responde 400.

```json
{ "success": true, "message": "Listado de personas.", "data": [ ... ],
  "pagination": { "page": 1, "pageSize": 10, "totalItems": 23, "totalPages": 3 } }
```

### Validaciones

| Campo | Regla |
|---|---|
| `identificacion` | Obligatoria, solo números, máximo 20 caracteres |
| `nombres`, `apellidos` | Obligatorios, solo letras (con tildes y ñ; espacio, `-` y `'` entre palabras), máximo 100 caracteres |
| `edad` | Entero entre 0 y 150 |
| `genero` | `Masculino` o `Femenino` |

Antes de guardar, el servidor normaliza nombres y apellidos: quita espacios sobrantes, unifica la forma
en que se guardan las tildes (Unicode NFC) y pone mayúscula inicial en cada palabra (`jANE  doe-SMITH` queda como `Jane Doe-Smith`). Como lo hace en todas las
palabras, "de la Cruz" queda "De La Cruz".

Un error de validación responde 400:

```json
{ "success": false, "message": "El campo Nombres admite solo letras.", "data": null }
```

Un JSON mal formado, un género que no existe o algo como `page=abc` también responden 400. Si ocurre un
error inesperado, la respuesta es 500 con un mensaje genérico y el detalle solo queda en el log.

## Tests

```powershell
cd CrudNet
dotnet test
```

Son 38 pruebas con xUnit. Para ver el resultado de cada una:
`dotnet test --logger "console;verbosity=normal"`.

- `PersonServiceTests` son pruebas unitarias de las reglas de negocio, con un repositorio en memoria.
  Revisan campos obligatorios, formatos, capitalización, rango de edad, paginación, género, identificación
  duplicada y los casos en que el registro no existe.
- `PersonApiTests` son pruebas de integración: levantan la API en memoria contra una base SQLite temporal
  que se borra al terminar. Revisan los códigos HTTP y el formato de respuesta de cada endpoint, la
  paginación, que el update solo cambie la edad, que la vista devuelva solo mujeres, que el JSON mantenga
  los nombres en español, que se sirva la interfaz y que estén los encabezados de seguridad.

## Base de datos

`BaseDeDatos/epik.db` contiene la tabla, la vista y los datos de ejemplo. Se puede abrir con
[DB Browser for SQLite](https://sqlitebrowser.org/) o con la extensión SQLite Viewer de VS Code.

## Estructura

```
BaseDeDatos/
  schema.sql        tabla Persona y vista VW_Mujeres (la API usa este mismo script al iniciar)
  seed.sql          datos de ejemplo
  epik.db           base exportada
CrudNet/
  EpikCrud.sln
  src/Epik.Crud.Api/
    Domain/         entidad Person y enum Gender
    Application/    interfaces, reglas de negocio, excepciones y paginación
    Infrastructure/ repositorio SQLite (ADO.NET) e inicialización de la base
    Api/            endpoints, formato de respuesta y manejo de errores
    wwwroot/        interfaz web (index.html, styles.css, app.js y vendor/ con Notyf)
    Program.cs      configuración y arranque
  tests/Epik.Crud.Tests/
    PersonServiceTests.cs
    PersonApiTests.cs
```

## Notas técnicas

- La API está en un solo proyecto con tres capas: `Api`, `Application` e `Infrastructure`. `Application`
  solo conoce interfaces, así que cambiar de motor de base de datos implica escribir otra
  implementación de `IPersonRepository`.
- Las reglas de validación están en el servicio. El front aplica las mismas para avisar antes de enviar,
  y la tabla tiene `CHECK` sobre edad y género como última barrera.
- Se usó SQLite para que el proyecto corra sin instalar un servidor de base de datos.
- Seguridad:
  - Todas las consultas SQL son parametrizadas.
  - Nombres e identificación solo aceptan letras o números, así que caracteres como `<`, `>` o `;` se
    rechazan con 400, y el front ni siquiera deja escribirlos.
  - El front inserta los datos como texto (`textContent`). Lo que se muestra en las notificaciones de
    Notyf, que usa `innerHTML`, se escapa antes.
  - Cada respuesta lleva `Content-Security-Policy` (`default-src 'self'`, por eso el JS y el CSS van en
    archivos aparte), `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff` y
    `Referrer-Policy: no-referrer`. Swagger UI queda fuera de la CSP porque necesita scripts en línea.
- El front no necesita build: es HTML, CSS y JavaScript. La única librería es
  [Notyf](https://github.com/caroso1222/notyf) 3.10 (MIT) para las notificaciones, guardada en
  `wwwroot/vendor/` para que funcione sin internet.

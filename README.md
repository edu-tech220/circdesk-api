# CircDesk API

API REST para la circulación de una biblioteca (catálogo, préstamos, reservas y multas),
construida con ASP.NET Core 8, EF Core y SQL Server.

## Qué resuelve

Una biblioteca necesita algo más que un CRUD de libros: tiene que decidir, con reglas claras,
quién puede llevarse un ejemplar, qué pasa cuando no queda ninguno disponible, cómo se reparte
un libro entre varios socios que lo esperan y qué consecuencia tiene devolverlo tarde. CircDesk
modela ese flujo de **circulación** (circulation desk) de punta a punta:

- **Catálogo**: libros con N ejemplares físicos (`Copy`), cada uno con su propio estado
  (`Available`, `OnLoan`, `Reserved`, `Lost`).
- **Préstamos**: un socio pide prestado un libro; el sistema asigna un ejemplar disponible,
  calcula la fecha de vencimiento y controla cuántos préstamos activos puede tener a la vez.
- **Renovaciones**: con límite de renovaciones por préstamo, bloqueadas si el préstamo ya está
  vencido o si otro socio está esperando el libro.
- **Reservas (holds)**: cuando no hay ejemplares disponibles, el socio se anota en una cola FIFO.
  Al devolverse un ejemplar, se reserva automáticamente para el primero en la fila, con una
  ventana de tiempo límite para recogerlo antes de pasar al siguiente.
- **Multas**: devolver tarde genera una multa por día de atraso (con tope máximo). Si un socio
  acumula multas pendientes por encima de un umbral, se le bloquean nuevos préstamos hasta que
  pague.

Todo esto corre bajo concurrencia: dos socios pueden intentar llevarse el último ejemplar al
mismo tiempo, y solo uno debe ganar.

## Arquitectura

Solución de dos proyectos:

```
CircDesk.sln
├── src/CircDesk.Api          API ASP.NET Core 8 (Minimal Hosting + Controllers)
└── tests/CircDesk.Api.Tests  Pruebas xUnit (WebApplicationFactory + SQLite)
```

Dentro de `CircDesk.Api`:

- **Domain**: entidades EF Core puras (`Book`, `Copy`, `Member`, `Loan`, `Hold`, `Fine`) y enums.
  `LoanStatus` solo persiste `Active`/`Returned`; "vencido" se deriva comparando `DueDate` contra
  la hora actual al momento de responder, así no hace falta un job que vaya marcando préstamos
  como vencidos.
- **Data**: `CircDeskDbContext` con la configuración Fluent API (índices, relaciones, precisión
  decimal). Las entidades con control de concurrencia implementan `IHasRowVersion`; el propio
  `DbContext` les asigna un nuevo token (`Guid.NewGuid().ToByteArray()`) en cada `SaveChanges`
  sobre una entidad modificada, en vez de depender de una columna `rowversion` nativa de SQL
  Server — así el mismo modelo funciona igual en SQL Server (producción) y SQLite (pruebas).
- **Services**: la lógica de negocio real vive aquí, no en los controllers.
  - `LoanService.BorrowAsync` valida bloqueos, multas pendientes y límite de préstamos activos,
    y reclama un ejemplar con un bucle de reintento ante conflictos de concurrencia optimista
    (`DbUpdateConcurrencyException`) — si dos socios compiten por el mismo ejemplar, el perdedor
    reintenta con el siguiente ejemplar disponible o recibe un 409 si ya no queda ninguno.
  - `HoldService` administra la cola de reservas por libro y expone `ReleaseCopyAsync`, usado
    tanto al devolver un préstamo como al expirar o cancelar una reserva, para no duplicar la
    lógica de "a quién le toca el siguiente ejemplar".
  - `FineService` cobra y permite pagar multas.
- **BackgroundServices**: `HoldExpirationService` expira periódicamente las reservas `Ready` que
  superaron su ventana de recogida y empuja la cola al siguiente socio (se desactiva en el
  entorno `Testing` para que las pruebas controlen la expiración de forma determinista llamando
  directamente a `IHoldService.ExpireDueHoldsAsync`).
  - **Auth**: JWT Bearer con claims de rol, y hashing de contraseña con PBKDF2 (salteado,
  100k iteraciones, comparación en tiempo constante).
- **Common**: `DomainException` y sus subclases (`NotFoundException`, `ConflictException`,
  `ForbiddenException`, `ValidationException`, `InvalidCredentialsException`) se traducen a
  `ProblemDetails` (RFC 7807) mediante `CircDeskExceptionHandler` (`IExceptionHandler` de
  ASP.NET Core 8), así los controllers no tienen bloques `try/catch` repetidos.

## Decisiones técnicas

- **Concurrencia optimista portable**: en vez de una columna `rowversion` de SQL Server (que no
  existe en SQLite), el control de concurrencia se implementa en código vía `IHasRowVersion` +
  `IsConcurrencyToken()`. Mismo comportamiento en ambos motores, y las pruebas de carrera corren
  sin necesitar SQL Server.
- **Sin job para "vencido"**: `LoanStatus` no tiene un valor `Overdue` persistido; se calcula al
  vuelo comparando `DueDate` con `DateTime.UtcNow`. Menos estado que mantener sincronizado.
- **Multas sumadas en memoria**: SQLite no soporta `SUM()` sobre `decimal` vía LINQ
  (`SqliteQueryableAggregateMethodTranslator` lo rechaza). Los montos pendientes se traen con
  `.Select(...).ToListAsync()` y se suman en el cliente — el conjunto es pequeño (multas por
  socio) y el código queda igual en ambos proveedores.
- **Rate limiting solo en auth**: `/api/auth/*` usa un `FixedWindowLimiter` (50 req/min) para
  mitigar fuerza bruta sobre login/registro sin afectar el resto de la API.
- **JWT options resueltas de forma perezosa**: `JwtBearerOptions` se configura vía
  `AddOptions<JwtBearerOptions>().Configure<IOptions<JwtOptions>>(...)` en lugar de leer
  `IConfiguration` una sola vez al arrancar. Esto evita depender del orden exacto en que
  `WebApplicationFactory` inyecta su configuración de prueba, y es el patrón recomendado para
  que la config pueda cambiar sin reiniciar el proceso.

## Modelo de dominio (resumen)

| Entidad | Rol |
|---|---|
| `Book` | Título, autor, ISBN; agrupa N `Copy`. |
| `Copy` | Ejemplar físico con estado y token de concurrencia. |
| `Member` | Socio (o bibliotecario) con credenciales y rol. |
| `Loan` | Préstamo de un `Copy` a un `Member`, con fechas y contador de renovaciones. |
| `Hold` | Reserva en cola para un `Book`; pasa de `Queued` → `Ready` → `Fulfilled`/`Expired`. |
| `Fine` | Multa asociada a un `Loan` vencido, con estado `Pending`/`Paid`. |

## Reglas de negocio implementadas

- Máximo de préstamos activos simultáneos por socio (configurable).
- No se puede pedir prestado el mismo libro dos veces mientras el préstamo anterior siga activo.
- Un socio con multas pendientes por encima del umbral no puede pedir más préstamos.
- Las renovaciones tienen un límite, se bloquean si el préstamo está vencido, y se bloquean si
  hay otros socios esperando ese libro en la cola de reservas.
- Las reservas solo se pueden crear cuando **no** hay ejemplares disponibles (si los hay, se pide
  el préstamo directamente).
- Al devolver un ejemplar, se promueve automáticamente a la reserva más antigua de la cola; si no
  hay nadie esperando, el ejemplar vuelve a estar disponible.
- Las reservas `Ready` expiran tras una ventana de tiempo configurable y ceden el turno al
  siguiente en la cola.
- Las multas se calculan por día de atraso con un tope máximo por préstamo.

## Endpoints principales

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | — | Registro de un socio nuevo. |
| POST | `/api/auth/login` | — | Login, devuelve JWT. |
| GET | `/api/books` | autenticado | Catálogo paginado, con búsqueda por título/autor. |
| GET | `/api/books/{id}` | autenticado | Detalle de un libro. |
| POST | `/api/books` | Librarian | Alta de libro (con N ejemplares iniciales). |
| POST | `/api/books/{id}/copies` | Librarian | Agregar un ejemplar a un libro existente. |
| POST | `/api/loans` | autenticado | Pedir prestado un libro. |
| POST | `/api/loans/{id}/return` | Librarian | Registrar la devolución de un préstamo. |
| POST | `/api/loans/{id}/renew` | autenticado (dueño) | Renovar un préstamo propio. |
| GET | `/api/loans/mine` | autenticado | Préstamos propios, paginados. |
| GET | `/api/loans` | Librarian | Todos los préstamos, filtrables por socio/vencidos. |
| POST | `/api/holds` | autenticado | Crear una reserva. |
| DELETE | `/api/holds/{id}` | autenticado (dueño) | Cancelar una reserva. |
| GET | `/api/holds/mine` | autenticado | Reservas propias, paginadas. |
| GET | `/api/fines/mine` | autenticado | Multas propias, paginadas. |
| POST | `/api/fines/{id}/pay` | autenticado (dueño) | Pagar una multa. |

## Cómo correrlo

### Con Docker (API + SQL Server)

```bash
docker compose up --build
```

La API queda disponible en `http://localhost:8080`, con Swagger en `/swagger` (solo en
`Development`; en este compose corre en `Production`, así que se puede probar directo con
`curl`/Postman contra `http://localhost:8080/api/...`).

### Localmente, sin Docker

Necesitás una instancia de SQL Server accesible (o cambiar `UseSqlite` a `true` en
`appsettings.json` para probar rápido sin instalar nada):

```bash
cd src/CircDesk.Api
dotnet run
```

Al arrancar (fuera del entorno `Testing`), la API aplica las migraciones de EF Core
automáticamente contra la base configurada en `ConnectionStrings:DefaultConnection`.

## Cómo probarlo

```bash
dotnet test
```

Las pruebas no requieren SQL Server ni Docker: cada clase de prueba levanta la API en memoria
con `WebApplicationFactory<Program>` contra una base SQLite en memoria (modo compartido, una
conexión por `DbContext` como en producción, para que las pruebas de concurrencia disparen una
carrera real). Cubren:

- Autenticación (registro, login, rutas protegidas).
- Catálogo (alta de libros/ejemplares, búsqueda, paginación, permisos por rol).
- Flujo de préstamo completo: pedir, devolver, renovar, límites y multas por atraso.
- Concurrencia: dos socios compitiendo por el último ejemplar de un libro.
- Reservas: cola FIFO, promoción automática al devolver, expiración y cascada al siguiente.
- Rate limiting en los endpoints de autenticación.

## Generar una nueva migración

El repo incluye un manifiesto de herramientas locales (`.config/dotnet-tools.json`) con
`dotnet-ef`:

```bash
dotnet tool restore
cd src/CircDesk.Api
dotnet ef migrations add NombreDeLaMigracion -o Migrations
```

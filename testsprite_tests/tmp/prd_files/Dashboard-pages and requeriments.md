# CarStore — Dashboard: páginas, entidades, acciones, reglas de negocio y flujos

> **Propósito**: especificación funcional completa del panel administrativo (`app/(dashboard)/`) de CarStore. Cubre qué se controla en cada pantalla, qué entidades se tocan, qué acciones se pueden ejecutar, qué reglas de negocio se aplican (con `file:line` al backend cuando aplica), y los happy paths de extremo a extremo.
>
> **Stack**: Next.js 14 (App Router), TypeScript, Tailwind v4, shadcn/ui, React Query, Zustand, RHF + Zod, JWT custom.
>
> **Backend de referencia**: `CarStore-BackEnd/src/` (Clean Architecture — `Domain → Application → Infrastructure → Web.Api`).

---

## 0. Mapa global de páginas del dashboard

| Ruta | Archivo | Render | Propósito |
|---|---|---|---|
| `/auth/login` | `app/(dashboard)/auth/login/page.tsx:5` | Client | Login JWT |
| `/auth/registro` | `app/(dashboard)/auth/registro/page.tsx:5` | Client | Alta de cuenta |
| `/auth/recuperar` | `app/(dashboard)/auth/recuperar/page.tsx:5` | Client | Recuperación de contraseña |
| `/auth/verificar-email` | `app/(dashboard)/auth/verificar-email/page.tsx:12` | Client (Suspense) | Verificación de email (stub) |
| `/dashboard` | `app/(dashboard)/dashboard/page.tsx:24` | Client | Overview: KPIs, gráficos, ventas recientes |
| `/dashboard/inventario` | `app/(dashboard)/dashboard/inventario/page.tsx:5` | Server → Suspense | Listado de vehículos (tabla + KPIs) |
| `/dashboard/inventario/[id]` | `app/(dashboard)/dashboard/inventario/[id]/page.tsx:35` | Client | Detalle de vehículo + imágenes + tareas de reacondicionamiento |
| `/dashboard/inventario/nuevo` | `app/(dashboard)/dashboard/inventario/nuevo/page.tsx:7` | Server | Stub — redirige a `/inventario` (la creación real es modal) |
| `/dashboard/ventas` | `app/(dashboard)/dashboard/ventas/page.tsx:5` | Server | Listado de ventas + KPIs |
| `/dashboard/ventas/nuevo` | `app/(dashboard)/dashboard/ventas/nuevo/page.tsx:14` | Client | Form de alta de venta |
| `/dashboard/ventas/[id]` | `app/(dashboard)/dashboard/ventas/[id]/page.tsx:14` | Client | Detalle de venta |
| `/dashboard/clientes` | `app/(dashboard)/dashboard/clientes/page.tsx:7` | Server | Tabs: Pipeline Kanban (Leads) + Tabla de Clientes |
| `/dashboard/clientes/nuevo` | `app/(dashboard)/dashboard/clientes/nuevo/page.tsx:11` | Client | Alta de cliente (no conversión de lead) |
| `/dashboard/clientes/[id]` | `app/(dashboard)/dashboard/clientes/[id]/page.tsx:7` | Server | Detalle de cliente |
| `/dashboard/clientes/[id]/editar` | `app/(dashboard)/dashboard/clientes/[id]/editar/page.tsx:11` | Client | Edición de cliente |
| `/dashboard/cotizaciones` | `app/(dashboard)/dashboard/cotizaciones/page.tsx:5` | Server | Listado de cotizaciones (Pending/Accepted/Rejected/Expired) |
| `/dashboard/cotizaciones/[id]` | `app/(dashboard)/dashboard/cotizaciones/[id]/page.tsx:15` | Client | Detalle + acciones Accept/Reject |
| `/dashboard/agenda` | `app/(dashboard)/dashboard/agenda/page.tsx:19` | Server → Suspense | Calendario FullCalendar de turnos (TestDrive/Service/Delivery) |
| `/dashboard/finanzas` | `app/(dashboard)/dashboard/finanzas/page.tsx:5` | Server | Caja: transacciones, categorías, summary (admin-only) |
| `/dashboard/reports` | `app/(dashboard)/dashboard/reports/page.tsx:11` | Client (dynamic, ssr:false) | Reportes |
| `/dashboard/configuracion` | `app/(dashboard)/dashboard/configuracion/page.tsx:5` | Server | Tabs: Ajustes del dealer, Usuarios, Roles/Permisos, Perfil |

**Sidebar / nav** (`lib/navigation.ts:19` + `AppSidebar.tsx:33`):

| Grupo | Ítem | URL | Rol mínimo |
|---|---|---|---|
| General | Dashboard | `/dashboard` | empleado |
| Gestión | Inventario | `/dashboard/inventario` | empleado |
| Gestión | Ventas | `/dashboard/ventas` | empleado |
| Gestión | Clientes | `/dashboard/clientes` | empleado |
| Gestión | Cotizaciones | `/dashboard/cotizaciones` | empleado |
| Gestión | Agenda | `/dashboard/agenda` | empleado |
| Análisis | Finanzas | `/dashboard/finanzas` | **admin** |
| Análisis | Reportes | `/dashboard/reports` | empleado |
| Configuración | Ajustes | `/dashboard/configuracion` | empleado |

Roles (`lib/auth/route-roles.ts:5`): `invitado=0 < cliente=1 < empleado=2 < admin=3`.

> **Nota**: el filtro de nav es client-side. Hoy NO hay `middleware.ts` (gap conocido), así que un usuario con link directo puede acceder a páginas cuyo rol no le corresponde — el backend igual responde 403 por permiso.

---

## 1. Autenticación

### 1.1 Entidades controladas

- `User` — `Domain/Users/User.cs:6-96`: `Email`, `FirstName`, `LastName`, `PasswordHash`, `Role` (Admin/Empleado/Cliente/Invitado), `IsActive`, `Phone?`, `CreatedAt`.
- `UserPermission` — `Domain/Users/UserPermission.cs:5-26`: `(UserId, Permission)` con índice único.
- `DealerSettings` — relevante para branding del login (`Domain/DealerSettings/DealerSettings.cs:10-140`).

### 1.2 Acciones

| Acción | Endpoint | Hook/servicio FE |
|---|---|---|
| Login | `POST /api/v1/users/login` | `authService.login()` → guarda en `useGlobalStore` |
| Registro | `POST /api/v1/users/register` | `authService.register()` |
| Verificar email (stub) | `POST /api/v1/users/verify-email` | `authService.verifyEmail()` |
| Reenviar verificación (stub) | `POST /api/v1/users/resend-verification` | `authService.resendVerification()` |
| Reset password (stub) | `POST /api/v1/users/password-reset` | `authService.resetPassword()` |

### 1.3 Happy path — Login

1. El usuario entra a `/auth/login` (o es redirigido tras 401).
2. Completa email + password (validación local con Zod en `LoginForm.tsx:43`).
3. `authService.login()` → `POST /api/v1/users/login` (`Web.Api/Endpoints/Users/Login.cs:15`).
4. Backend (`Users/Login/LoginUserCommandHandler.cs`):
   - Bypassea el filtro de tenant para validar (login cross-tenant).
   - Verifica el hash.
   - Emite JWT con claims `sub`, `role`, `dealer_id`.
5. FE: decodifica el JWT con `jose.decodeJwt` (`authService.ts:50`), enriquece con `GET /users/{id}`.
6. `useGlobalStore.setAuth(token, user)` (`globalStore.ts:107-119`):
   - Persiste en `localStorage` clave `global-app-storage`.
   - Setea cookie `carstore-auth` (preparada para futuro middleware SSR).
   - Setea header `Authorization: Bearer …` en el Axios client.
7. `router.push('/dashboard')`.

### 1.4 Reglas de negocio

- **Login con token expirado**: 401 → interceptor Axios (`services/apiClient.ts:99`) dispatcha `auth:expired` y limpia el store. `AuthProvider` (`providers/AuthProvider.tsx`) detecta el evento y muestra el spinner de "Cargando…" hasta que se desautentica.
- **Email único global** (`Infrastructure/Database/Configurations/UserConfiguration.cs:17`).
- **Verificación de email, resend y password-reset son STUBS**: devuelven 200 pero no procesan realmente (marcados como tales en `Web.Api/Endpoints/Users/*`).
- **No hay rate limit visible en el front**; el back sí rate-limita login, resend y password-reset.

### 1.5 Estados a contemplar

- Formulario con errores inline (Zod).
- Loading state en el botón.
- 401 con mensaje "Credenciales inválidas".
- Cuenta inactiva (`User.IsActive = false`).
- Email no verificado (futuro, hoy stub).

---

## 2. Dashboard — Overview (`/dashboard`)

### 2.1 Entidades controladas

- KPIs agregados: ventas, leads, inventario, clientes (resumen).
- `Sale` (resumen + hoy).
- `Lead` (conteos por estado).
- `Car` (stats de inventario).
- `Client` (stats).

### 2.2 Acciones visibles

| Acción | Componente | Endpoint |
|---|---|---|
| Ver KPIs | `DashboardKPIGrid` |汇总 de `useDashboard()` (combina `useSales`, `useLeads`, `useCars`, `useClients`) |
| Ver gráficos de tendencia | `DashboardCharts` (feature flag `NEXT_PUBLIC_ENABLE_TREND_CHARTS`) | `ActualSalesChart`, `SalesTrendChart` |
| Acciones rápidas | `QuickActions` | Links a Inventario, Ventas, Clientes, Cotizaciones, Agenda |
| Ventas recientes | `RecentSalesList` | `GET /api/v1/sales` (top N) |

### 2.3 Happy path

1. Usuario autenticado entra a `/dashboard`.
2. `useDashboard()` dispara en paralelo las queries necesarias.
3. Mientras cargan: skeletons (`LoadingCard`, `LoadingTable`).
4. Al resolver: saludo personalizado (`useCurrentUser()`) + grid de KPIs + gráficos + lista de últimas ventas.
5. Si hay error: `ErrorState` por card.
6. Click en una venta reciente → navega a `/dashboard/ventas/[id]`.

### 2.4 Reglas de negocio

- Saludo usa `currentUser.firstName`.
- Feature flag de gráficos: si está apagado, oculta `DashboardCharts`.
- Datos siempre filtrados por tenant actual (`dealer_id` del JWT).

---

## 3. Inventario (`/dashboard/inventario`)

### 3.1 Entidades controladas

- `Car` (aggregate root) — `Domain/Cars/Car.cs:8-232`:
  - `MarcaId`/`ModeloId`, `Color` (enum), `CarType` (TypeCar: Sedan/Coupe/Hatchback/SUV/Pickup/Minivan).
  - `CarStatus` (StatusCar: New/Used/Certified).
  - `ServiceCar` (StatusServiceCar: Service/EnVenta/Vendido/Disponible/NoDisponible).
  - `FuelType`, `Transmission`, `Featured`, `CantidadPuertas`, `CantidadAsientos`, `Cilindrada`, `Kilometraje`, `Anio`.
  - `Patente` (value object, formato argentino — Mercosur / pre-1995 / moto), única **global**.
  - `Price` (Money), `PurchaseCost?` (Money).
  - `Descripcion` (≤255), `DealerId`, timestamps.
- `CarImage` — `Domain/Cars/CarImages.cs:12-104`: dual mode `ImageUrl` o `ObjectKey` (MinIO), `IsCover`, `DisplayOrder`. **UN solo cover por auto** (índice parcial Postgres).
- `ReconditioningTask` — `Domain/Cars/ReconditioningTask.cs:11-80`: descripción, costo (Money), status (Pending/InProgress/Completed).
- `Marca` / `Modelo` — catálogo compartido.

### 3.2 Acciones

**Listado**:

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar vehículos | `GET /api/v1/cars` | `cars:read` |
| Stats de inventario | `GET /api/v1/cars/stats/inventory` | `cars:read` |
| Imágenes primarias (grid) | `GET /api/v1/cars/primary-images` | `cars:read` |

**Detalle** (`/dashboard/inventario/[id]`):

| Acción | Endpoint | Permiso |
|---|---|---|
| Ver vehículo | `GET /api/v1/cars/{id}` | `cars:read` |
| Ver imágenes | `GET /api/v1/cars/{carId}/images` | `cars:read` |
| Ver tareas de reacondicionamiento | `GET /api/v1/cars/{id}/reconditioning` | `cars:read` |
| Ver costo total de propiedad | computado `Car.GetTotalCostOfOwnership()` | derivado |

**Edición / creación**:

| Acción | Endpoint | Permiso |
|---|---|---|
| Crear vehículo | `POST /api/v1/cars` | `cars:create` |
| Editar vehículo | `PUT /api/v1/cars/{id}` | `cars:update` |
| Eliminar vehículo (cascadea MinIO) | `DELETE /api/v1/cars/{id}` | `cars:delete` |

**Imágenes** (modal/dropzone en el detalle):

| Acción | Endpoint | Permiso |
|---|---|---|
| Subir imagen directa | `POST /api/v1/cars/{carId}/images` | `cars:update` |
| Obtener URL prefirmada de upload | `POST /api/v1/cars/{carId}/images/upload-url` | `cars:update` |
| Confirmar upload prefirmado | `POST /api/v1/cars/{carId}/images/confirm` | `cars:update` |
| Eliminar imagen (blob + DB) | `DELETE /api/v1/cars/{carId}/images/{imageId}` | `cars:update` |
| Reordenar imágenes | `PUT /api/v1/cars/{carId}/images/reorder` | `cars:update` |
| Marcar como cover | `PATCH /api/v1/cars/{carId}/images/{imageId}/cover` | `cars:update` |
| Set primary (legacy alias) | `PUT /api/v1/cars/{carId}/images/{imageId}/make-primary` | `cars:update` |

**Reacondicionamiento**:

| Acción | Endpoint | Permiso |
|---|---|---|
| Agregar tarea | `POST /api/v1/cars/{id}/reconditioning` | `cars:update` |
| Completar tarea | `PATCH /api/v1/cars/{carId}/reconditioning/{taskId}/complete` | `cars:update` |

**Admin**:

| Acción | Endpoint | Permiso |
|---|---|---|
| Backfill cover pre-Phase-2 | `POST /api/v1/admin/backfill/pre-phase2-images` | `admin:backfill` |
| Regenerar URLs SAS (legacy) | `POST /api/v1/cars/regenerate-image-urls` | `cars:update` |

### 3.3 Happy path — Crear vehículo

1. Usuario abre `/dashboard/inventario`, click en "Nuevo vehículo" → abre `VehicleFormModal`.
2. Completa: Marca (select, cached), Modelo (select dependiente), Color, Tipo, Condición (New/Used/Certified), Estado de servicio (default `EnVenta`), Combustible, Transmisión, Puertas (2-5), Asientos, Cilindrada, Kilometraje, Año, Patente (validada), Precio, Costo de compra (opcional), Descripción, Featured.
3. Submit → `useCreateCar()` → `POST /api/v1/cars`:
   - Backend valida patente única global (`CreateCarCommandHandler.cs:29-31` con `IgnoreQueryFilters`).
   - Resuelve Marca/Modelo por cache.
   - Crea entidad, dispara `NewCarDomainEvent`.
4. Toast de éxito + invalidación de `['cars']` + el nuevo auto aparece en la tabla.

### 3.4 Happy path — Subir imágenes

1. En el detalle, abrir `VehicleImageUploader` (`react-dropzone`).
2. Drag-and-drop hasta 10 imágenes (5MB c/u, JPEG/PNG/WebP — `imageFileSchema`).
3. FE genera presigned POST → `POST /cars/{id}/images/upload-url` → `POST /cars/{id}/images/confirm`.
4. Backend (`UploadCarImageCommandHandler.cs:55`): la **primera imagen se hace cover automáticamente**.
5. `VehicleImagePreviewGrid` re-renderiza. Reordenar con drag, marcar cover con un click.

### 3.5 Happy path — Tarea de reacondicionamiento

1. En el detalle, sección "Reacondicionamiento" → `ReconditioningChecklist`.
2. "Agregar tarea" → modal con `addReconditioningTaskSchema` (descripción 1-500, costo >0, currency).
3. `POST /cars/{id}/reconditioning` → `ReconditioningTask` en `Pending`.
4. Click en "Completar" → `PATCH /cars/{id}/reconditioning/{taskId}/complete`:
   - Backend dispara `ReconditioningTaskCompletedDomainEvent` → consumido por `ReconditioningTaskCompletedHandler.cs` que escribe un egreso en el ledger financiero (idempotente por `TaskId`).
5. La tarea pasa a `Completed`, se ve en "Costo total de propiedad" recalculado.

### 3.6 Reglas de negocio críticas

- **Patente única global** entre todos los tenants (`CreateCarCommandHandler.cs:29-31`).
- **Cover único por auto** enforced por índice parcial Postgres `ux_car_images_car_id_is_cover` (`ApplicationDbContext.cs:68-72`); el handler demota la cover anterior antes de promover una nueva.
- **Límite de imágenes por auto**: `MaxImagesPerCar` (consultar `Application/Cars/Commands/UploadCarImage/UploadCarImageCommandHandler.cs:34-38`).
- **Borrar auto = borrar blobs primero** (REQ-VMS-5, `DeleteCarCommandHandler.cs:32-48`).
- **Borrar imagen = borrar blob primero, después fila** (REQ-VMS-6, `DeleteCarImageCommandHandler.cs:38-66`).
- **Crear venta requiere** `ServiceCar == Disponible` (`CreateSaleCommandHandler.cs:34-37`); al vender, `Car.MarkAsSold()` se ejecuta en el mismo `SaveChangesAsync` que la venta.
- **`GetTotalCostOfOwnership`**: `PurchaseCost + sum(tareas Completed)`. Si hay mismatch de currency, lanza `DomainException` (`Money.cs:24-30`). Precedencia de currency: `PurchaseCost.Currency` → primera tarea completada → `"USD"`.
- **Reacondicionamiento — transición**: `Create → Start → Complete`. `Start` bloqueado si ya está `Completed`. `Complete` es idempotente.
- **Las URLs de MinIO expiran en 15 min** (presigned).

### 3.7 Estados y errores a manejar

- Patente duplicada → 409 con mensaje.
- Exceder límite de imágenes → toast "Máximo N imágenes por vehículo".
- Borrar auto con tareas pendientes → confirmar en dialog.
- Reacondicionar tarea sin descripción → Zod.
- Costo ≤ 0 → Zod.
- Año fuera de `[1900, currentYear+1]` → Zod (`createVehicleSchema`).
- VIN (si se pidiera) con longitud ≠ 17 → Zod.

---

## 4. Leads — Pipeline Kanban (`/dashboard/clientes` → tab "Pipeline")

### 4.1 Entidades controladas

- `Lead` — `Domain/Leads/Lead.cs:7-162`:
  - `ClientName`, `Email`, `Phone`, `Status` (LeadStatus: Nuevo/Contactado/Demostracion/Negociacion/Ganado/Perdido/Archivado), `AssignedAgentId?`, `Notes?`, `Source` (Web/Portal/Referral/Otro).
  - CRM: `InterestedVehicleId?`, `ConvertedClientId?`, `LossReason?` (Precio/Financiacion/ComproEnOtra/Desistio/Otro).
  - `CreatedAt`, `DealerId`.
- `LeadStatus` enum: `Nuevo=0, Contactado=1, Demostracion=2, Negociacion=3, Ganado=4, Perdido=5, Archivado=6`.
- `LeadSource` enum: Web/Portal/Referral/Otro.
- `LeadLossReason` enum: Precio/Financiacion/ComproEnOtra/Desistio/Otro.

### 4.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar leads (filtro por status opcional) | `GET /api/v1/leads?status=…` | `leads:read` |
| Detalle de lead | `GET /api/v1/leads/{id}` | `leads:read` |
| Crear lead | `POST /api/v1/leads` | `leads:create` |
| Cambiar status | `PATCH /api/v1/leads/{id}/status` | `leads:update` |
| Editar notas | `PATCH /api/v1/leads/{id}/notes` | `leads:update` |
| Asignar agente | `PATCH /api/v1/leads/{id}/agent` | `leads:update` |
| Vincular vehículo de interés | `PATCH /api/v1/leads/{id}/vehicle` | `leads:update` |
| Convertir a cliente (manual) | `POST /api/v1/leads/{id}/convert` | `leads:update` |
| Archivar (soft) | `DELETE /api/v1/leads/{id}` | `leads:update` |

### 4.3 Happy path — Crear lead desde la web pública

1. Visitante completa formulario en landing → `POST /api/v1/quotes/inquiry` (`CreateInquiryCommandHandler.cs`).
2. Backend crea/actualiza cliente + crea lead en status `Nuevo` + asigna agente por round-robin (`DealerSettings.LastAssignedAgentIndex` se incrementa).
3. Lead aparece en la columna "Nuevo" del Kanban del dashboard.

### 4.4 Happy path — Pipeline Kanban

1. Vendedor abre `/dashboard/clientes` tab "Pipeline".
2. Ve columnas: Nuevo / Contactado / Demostración / Negociación / Ganado / Perdido (Archivado está oculto por filtro).
3. Drag & drop (`@dnd-kit`) mueve un card de "Nuevo" → "Contactado":
   - `useUpdateLeadStatus()` → `PATCH /leads/{id}/status`.
   - Backend `UpdateStatus` (`Lead.cs:73-112`) valida:
     - **Secuencial**: solo se permite `Status + 1` o saltar a `Perdido`/`Archivado` desde cualquier punto. No se puede `Nuevo → Demostracion`.
     - **`Ganado` y `Archivado` son terminales** (no se puede cambiar después, salvo `ForceStatus`).
     - **`Nuevo → Contactado` requiere `notes`** (no se puede sin nota).
     - **`→ Demostracion` requiere `InterestedVehicleId`**.
     - **`→ Perdido` requiere `lossReason`**.
4. Si la transición es válida → card se acomoda en la nueva columna. Si no → toast con el error exacto (mensajes en español) y el card vuelve a su origen.

### 4.5 Happy path — Convertir lead a cliente

Dos caminos:

**A) Manual**: vendedor click "Convertir" → `POST /leads/{id}/convert` (`ConvertLeadToClientCommandHandler.cs`):
- Crea `Client` a partir del lead.
- Reasigna quotes y appointments que estuvieran con el lead al nuevo cliente.
- Marca el lead como `ConvertedClientId`.

**B) Automático**: al aceptar una cotización del lead (`QuoteAcceptedDomainEvent` → `CreateClientFromLeadOnQuoteAcceptedHandler.cs`) el cliente se crea solo, forzando el status del lead con `Lead.ForceStatus` (bypass de la regla secuencial).

### 4.6 Reglas de negocio críticas

- **Pipeline secuencial** — sin skips (excepto `Perdido`/`Archivado`).
- **Filtro de query global** excluye `Archivado` (`LeadConfiguration.cs:71`), por eso no aparecen en el Kanban.
- **Round-robin de asignación** automático al crear.
- **Auto-creación de cliente** al aceptar cotización: el handler usa `ForceStatus` para saltar la regla.
- **`MarkConverted`** es el estado lógico, pero el filtro no lo oculta; el lead queda visible como referencia histórica.

### 4.7 Estados y errores a manejar

- Drag & drop sin pasar la validación del backend → revertir posición + toast.
- Intentar archivar un lead ya archivado → idempotente (`Archive()` solo si no está archivado).
- Crear lead sin email/teléfono → validación Zod (`createLeadSchema`).
- Asignar agente inexistente → 404.

---

## 5. Clientes (`/dashboard/clientes` → tab "Tabla de Clientes")

### 5.1 Entidades controladas

- `Client` — `Domain/Clients/Client.cs:9-115`:
  - `FirstName`, `LastName`, `DNI` (único), `Email`, `Phone`, `Address`, `City?`, `ZipCode?`, `Notes?`.
  - `Status` (Active/Inactive), `Type` (Individual/Corporate).
  - `OriginLeadId?` (si fue convertido desde lead).
  - `Sales` (colección), timestamps.

### 5.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar clientes | `GET /api/v1/clients` | `clients:read` |
| Stats (total, recent30d, active) | `GET /api/v1/clients/stats` | `clients:read` |
| Top clientes por revenue | `GET /api/v1/clients/top?limit=…` | `clients:read` |
| Clientes recientes | `GET /api/v1/clients/recent?limit=…` | `clients:read` |
| Incompletos (auto-creados por newsletter) | `GET /api/v1/clients/incomplete` | `clients:read` |
| Búsqueda | `GET /api/v1/clients/search?q=…` | `clients:read` |
| Detalle | `GET /api/v1/clients/{id}` | `clients:read` |
| Crear | `POST /api/v1/clients` | `clients:create` |
| Editar | `PUT /api/v1/clients/{id}` | `clients:update` |
| Eliminar (hard) | `DELETE /api/v1/clients/{id}` | `clients:delete` |

### 5.3 Happy path — Crear cliente

1. `/dashboard/clientes/nuevo` → `ClientForm`.
2. Completa: nombre, apellido, DNI, email, teléfono, dirección, ciudad (opcional), CP (opcional), tipo (Individual/Corporate), notas.
3. FE hace split `name → firstName/lastName` (`page.tsx:11`).
4. Submit → `POST /clients` → el cliente aparece en la tabla.

### 5.4 Happy path — Editar cliente

1. Tab "Tabla de Clientes" → fila → "Editar" → `/dashboard/clientes/[id]/editar`.
2. `useClient(id)` carga el detalle; `useUpdateClient()` persiste.
3. Acciones disponibles también: **Activar / Desactivar** (idempotente), **Eliminar** (hard delete con confirmación).

### 5.5 Happy path — Ver detalle

`/dashboard/clientes/[id]`:
- Datos personales.
- Ventas asociadas.
- Documentos (DNI/Título) — ver `DocumentUploader`.
- Historial.

### 5.6 Reglas de negocio

- **DNI único** global.
- **Eliminación hard** (no soft). Confirmar en dialog porque rompe historial.
- **Newsletter → cliente incompleto**: cuando alguien se suscribe, se crea un `Client` con `DNI` placeholder (formato `n` + 33 chars). El listado `GetIncompleteClientsQuery` los agrupa.
- **Conversión desde lead** preserva `OriginLeadId` (trazabilidad).

### 5.7 Estados y errores a manejar

- DNI duplicado → 409.
- Email inválido → Zod (`Email` value object).
- Eliminar cliente con ventas → confirmar (no hay cascade).
- Búsqueda case-insensitive, top 50.

---

## 6. Cotizaciones (`/dashboard/cotizaciones`)

### 6.1 Entidades controladas

- `Quote` — `Domain/Quotes/Quote.cs:11-157`:
  - `CarId` (required), `ClientId?` o `LeadId?` (**exactamente uno, no ambos, no ninguno**).
  - `ProposedPrice` (Money), `PaymentMethod` (Contado/Financiado/Permuta/Mixto).
  - `Status` (Pending/Accepted/Rejected/Expired), `ValidUntil`, `Comments`, timestamps.

### 6.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar cotizaciones | `GET /api/v1/quotes` | `quotes:read` |
| Detalle | `GET /api/v1/quotes/{id}` | `quotes:read` |
| Vencidas | `GET /api/v1/quotes/expired` | `quotes:read` |
| Mis cotizaciones (cliente) | `GET /api/v1/quotes/my` | auth |
| Crear | `POST /api/v1/quotes` | `quotes:create` |
| Editar (solo Pending) | `PUT /api/v1/quotes/{id}` | `quotes:update` |
| Eliminar (solo Pending) | `DELETE /api/v1/quotes/{id}` | `quotes:delete` |
| Aceptar | `POST /api/v1/quotes/{id}/accept` | `quotes:accept` |
| Rechazar (con razón) | `POST /api/v1/quotes/{id}/reject` | `quotes:reject` |
| Inquiry pública | `POST /api/v1/quotes/inquiry` | anónimo |

### 6.3 Happy path — Crear cotización

1. Vendedor abre `QuotesPage` → "Nueva cotización" → `QuoteFormModal`.
2. Selecciona: vehículo, cliente **o** lead (no ambos), precio propuesto, método de pago, `ValidUntil` (futuro), comentarios.
3. `POST /quotes`:
   - Backend valida `ValidUntil > now` (`CreateQuoteCommandHandler.cs:22-25`).
   - Valida `Client XOR Lead` (`Quote.cs:42-45`).
4. Cotización en status `Pending`.

### 6.4 Happy path — Aceptar cotización

1. Vendedor abre `/dashboard/cotizaciones/[id]`.
2. Click "Aceptar" → `POST /quotes/{id}/accept`:
   - Backend valida `Status == Pending` y `ValidUntil >= now` (`Quote.cs:113-118`).
   - Status → `Accepted`.
   - Emite `QuoteAcceptedDomainEvent` → consumido por `CreateClientFromLeadOnQuoteAcceptedHandler.cs` que crea un `Client` si el quote estaba atado a un lead (auto-conversión).
3. UI muestra el nuevo cliente creado (si aplica).

### 6.5 Happy path — Rechazar cotización

1. Click "Rechazar" → pedir razón.
2. `POST /quotes/{id}/reject` con `reason` no vacío.
3. Status → `Rejected`.

### 6.6 Reglas de negocio

- **Edit / Delete solo si Pending** (luego son inmutables).
- **Accept solo si Pending AND `ValidUntil >= now`**.
- **Expire automático**: job background cada 5 min (`MarkExpiredQuotesJob.cs:1-35`) pasa a `Expired` las que estén vencidas.
- **Inquiry pública** (`CreateInquiryCommandHandler.cs`): crea/actualiza cliente + crea quote, o solo cliente si no hay `carId`.
- **Cotización aceptada desde un lead → crea cliente automáticamente**.

### 6.7 Estados y errores a manejar

- ValidUntil en el pasado al crear → Zod (frontend) + validación backend.
- Cliente y lead al mismo tiempo → 400.
- Aceptar quote vencida → 409 "Cotización vencida".
- Editar quote Accepted/Rejected/Expired → 409.

---

## 7. Ventas (`/dashboard/ventas`)

### 7.1 Entidades controladas

- `Sale` — `Domain/Sales/Sale.cs:12-109`:
  - `CarId`, `ClientId`, `QuoteId?`, `LeadId?`, `FinalPrice` (Money), `Status` (Pending/Completed/Cancelled), `PaymentMethod` (Cash/CreditCard/DebitCard/BankTransfer/Other), `ContractNumber`, `SaleDate`, `Comments`.
  - Reglas: `Complete` solo desde `Pending`. `Cancel(reason)` solo desde `Pending` y con `reason` no vacío. `Update` solo si `Pending`.

### 7.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar | `GET /api/v1/sales` | `sales:read` |
| Detalle | `GET /api/v1/sales/{id}` | `sales:read` |
| Hoy | `GET /api/v1/sales/today` | `sales:read` |
| Summary | `GET /api/v1/sales/summary` | `sales:read` |
| Crear | `POST /api/v1/sales` | `sales:create` |
| Editar | `PUT /api/v1/sales/{id}` | `sales:update` |
| Eliminar (hard) | `DELETE /api/v1/sales/{id}` | `sales:delete` |

### 7.3 Happy path — Crear venta

1. `/dashboard/ventas/nuevo` (`page.tsx:14`) carga en paralelo `clients` + `cars` (solo `Disponible`).
2. Vendedor selecciona: cliente (required), vehículo, método de pago, precio final, número de contrato, fecha, comentarios.
3. `saleService.createSale()` (`hooks/useSales.ts`) → `POST /sales`:
   - Backend valida `ServiceCar == Disponible` (`CreateSaleCommandHandler.cs:34-37`).
   - Llama `car.MarkAsSold()` + `sale.Complete()` en un único `SaveChangesAsync` (atomicidad, `CreateSaleCommandHandler.cs:73`).
   - Emite `SaleCompletedDomainEvent` y `CarSoldDomainEvent`.
4. Toast de éxito → redirect a `/dashboard/ventas`.

### 7.4 Happy path — Cancelar venta

1. En `/dashboard/ventas/[id]`, si la venta está `Pending` o `Completed`, click "Cancelar" → pedir razón.
2. `PUT /sales/{id}` con `status: Cancelled` y `reason`.
3. `Sale.Cancel(reason)` valida que sea `Pending` y que `reason` no esté vacío.

> **Nota**: la cancelación NO devuelve el auto a `Disponible` automáticamente. Si querés liberar el auto, hay que hacerlo por separado (potencial gap de producto).

### 7.5 Reglas de negocio

- **Vender requiere `ServiceCar == Disponible`**.
- **`MarkAsSold` y `Complete` en una sola transacción** (outbox events en la misma SaveChanges).
- **Money > 0** enforced por `Money` value object.
- **No se puede vender un auto que esté `Vendido` o `EnVenta` con tareas pendientes**.

### 7.6 Estados y errores a manejar

- Vehículo no disponible → 409 "El vehículo no está disponible para la venta".
- Cancelar sin razón → 400.
- Editar venta Completed/Cancelled → 409.
- Eliminar venta con documentos asociados → confirmar (cascade configurable, ver Documentos).

---

## 8. Agenda (`/dashboard/agenda`)

### 8.1 Entidades controladas

- `Appointment` — `Domain/Appointments/Appointment.cs:13-94`:
  - `VehicleId` (required), `ClientId?` o `LeadId?` (al menos uno), `AgentId` (required), `StartDateTime`, `EndDateTime` (estrictamente mayor), `Type` (TestDrive=0, Service=1, Delivery=2), `Notes?`.

### 8.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar por rango | `GET /api/v1/appointments?from=…&to=…` | `appointments:read` |
| Crear | `POST /api/v1/appointments` | `appointments:create` |
| Reprogramar | `PUT /api/v1/appointments/{id}` | `appointments:update` |
| Eliminar (hard) | `DELETE /api/v1/appointments/{id}` | `appointments:delete` |

### 8.3 Happy path — Agendar test drive

1. Vendedor abre `/dashboard/agenda` → `AgendaCalendar` (FullCalendar).
2. Click en slot libre → `createAppointmentSchema` (RHF + Zod):
   - Vehículo (required).
   - Cliente o Lead (al menos uno, refine cross-field).
   - Agente (default = usuario actual).
   - Inicio + Fin (`end > start`).
   - Tipo (TestDrive / Service / Delivery).
   - Notas (≤2000).
3. `useCreateAppointment()` → `POST /appointments`:
   - Backend chequea overlap half-open: `existing.Start < new.End AND existing.End > new.Start` por `(dealerId, VehicleId OR AgentId)`.
   - Si hay conflicto → 409 Conflict.
4. Evento aparece en el calendario.

### 8.4 Happy path — Reprogramar

1. Drag & drop de un evento → actualiza `start/end`.
2. `PUT /appointments/{id}` con el mismo control de overlap, **excluyendo el row que se está moviendo** (`RescheduleAppointmentCommandHandler.cs:32-37`).

### 8.5 Reglas de negocio

- **`End > Start`** enforced en dominio y Zod.
- **Conflicto por vehículo O por agente** (no por cliente).
- **Hard delete** (no soft) — intencional.
- **Vistas guardadas en `globalStore.agendaView`** (default `timeGridWeek`).

### 8.6 Estados y errores a manejar

- Conflicto de horario → 409 con detalle del evento en colisión.
- Reprogramar a un slot ocupado → revertir drag + toast.
- Falta vehículo o agente → 400.

---

## 9. Finanzas (`/dashboard/finanzas`)

> **Acceso restringido**: solo `admin` (sidebar lo oculta para `empleado`).

### 9.1 Entidades controladas

- `FinancialTransaction` — `Domain/Financial/Transaction.cs:11-159`:
  - `Type` (Income/Expense), `Amount` (Money), `Description`, `PaymentMethod` (Cash/CreditCard/DebitCard/BankTransfer/Other), `ReferenceNumber?`, `TransactionDate`.
  - FKs opcionales: `CategoryId` (required), `CarId?`, `ClientId?`, `SaleId?`.
- `TransactionCategory` — `Domain/Financial/Attributes/TransactionCategory.cs:5-26`: `Name`, `Description`, `Type`. **Compartida entre tenants**.

### 9.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar transacciones | `GET /api/v1/financial` | `financial:read` |
| Summary (Income/Expenses/Balance/Count) | `GET /api/v1/financial/summary` | `financial:read` |
| Crear transacción | `POST /api/v1/financial` | `financial:create` |
| Editar | `PUT /api/v1/financial/{id}` | `financial:update` |
| Eliminar (hard) | `DELETE /api/v1/financial/{id}` | `financial:delete` |
| Listar categorías | `GET /api/v1/financial/categories` | `financial:read` |
| Crear categoría | `POST /api/v1/financial/categories` | `financial:create` |
| Editar categoría | `PUT /api/v1/financial/categories/{id}` | `financial:update` |
| Eliminar categoría | `DELETE /api/v1/financial/categories/{id}` | `financial:delete` |

### 9.3 Happy path — Cargar egreso de reconditioning

1. Este flujo es **automático** y no requiere acción del usuario: cuando se completa una `ReconditioningTask`, `ReconditioningTaskCompletedHandler.cs` escribe un egreso en el ledger vía `IFinancialLedgerService` (idempotente por `TaskId`).
2. La transacción aparece en `/dashboard/finanzas` con la descripción, monto y moneda.

### 9.4 Happy path — Cargar ingreso manual (venta cancelada, otros)

1. Admin abre `/dashboard/finanzas` → "Nueva transacción".
2. Completa: tipo (Income/Expense), monto, descripción, método de pago, referencia, fecha, categoría, opcionalmente vincular auto/cliente/venta.
3. `POST /financial` → aparece en el listado y suma al `summary`.

### 9.5 Happy path — Summary

`GET /financial/summary` devuelve `{ TotalIncome, TotalExpenses, Balance, EntryCount }`. El dashboard lo muestra como KPIs arriba de la tabla.

### 9.6 Reglas de negocio

- **Money > 0**.
- **`CategoryId` requerido** (no se puede crear transacción huérfana).
- **Las categorías son globales** (compartidas entre dealers); modificarlas afecta a todos.
- **Vinculaciones opcionales** pero, si se pasan, deben existir (validación de FK).
- **Idempotencia del ledger** en el path de reconditioning: no duplica gastos si se reintenta el handler.

### 9.7 Estados y errores a manejar

- CategoryId inexistente → 400.
- Monto ≤ 0 → Zod.
- Eliminar categoría con transacciones asociadas → confirmar (puede dejar transacciones huérfanas según el cascade configurado).

---

## 10. Documentos (`/dashboard/clientes/[id]`)

### 10.1 Entidades controladas

- `Document` — `Domain/Documents/Document.cs:7-126`:
  - `ClientId?`, `Type` (DNI/Titulo/Other), `OcrStatus` (Pending/Processing/Verified/Discrepancy).
  - `BlobName`, `FileName`, `ContentType`, `ParsedData?` (JSON con `OcrExtractedData`: FullName, DocumentNumber, IssueDate, VehicleTitleNumber, VehicleIdentifier), `OcrRawJson?`.
  - `UploadedAtUtc`, `VerifiedAtUtc?`, `DiscrepancyNotes?`.

### 10.2 Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Subir + verificar (OCR) | `POST /api/v1/documents/ocr-upload` | `documents:create` |
| Download URL (SAS 15min) | `GET /api/v1/documents/{id}/download-url` | `documents:read` |
| Legacy upload | `POST /api/documents/upload` | auth |
| Legacy verify | `POST /api/documents/{id}/verify` | auth |
| Legacy listar por cliente | `GET /api/documents/client/{clientId}` | auth |

### 10.3 Happy path — Verificar DNI con OCR

1. En el detalle del cliente, `DocumentUploader` arrastra PDF/JPEG/JPG/PNG.
2. `POST /documents/ocr-upload` (content-type whitelist en `UploadAndVerifyDocumentCommandHandler.cs:17-23`).
3. Backend:
   - Sube a blob.
   - Llama Azure Document Intelligence.
   - Compara `parsed.DocumentNumber` (case-insensitive trim) con `Client.DNI`.
   - Si match → `MarkAsVerified(data)`.
   - Si no match → `MarkAsFailed(data, discrepancyNotes)`.
4. UI muestra el badge correspondiente (`DocumentVerificationPanel`).

### 10.4 Reglas de negocio

- **Whitelist de MIME**: pdf, jpeg, jpg, png.
- **Discrepancia** registrada con notas.
- **URLs SAS expiran en 15 min**.

### 10.5 Estados y errores a manejar

- Content-type inválido → 415.
- Discrepancia con DNI → badge amarillo + notas.
- OCR timeout / Azure caído → 500 + retry manual.

---

## 11. Configuración (`/dashboard/configuracion`)

Tabs: **Ajustes del dealer / Usuarios / Roles y Permisos / Perfil**.

### 11.1 Ajustes del dealer

#### Entidades
- `DealerSettings` — `Domain/DealerSettings/DealerSettings.cs:10-140`:
  - Identidad: `DealerName`, `ContactEmail`, `HostName?`, `CustomDomain?`, `Address?`, `PhoneNumber?`.
  - Redes sociales.
  - `InterestRateTna?` (TNA usada por el simulador de financiación).
  - Visual: `LogoUrl?`, `PrimaryColor?`, `SecondaryColor?` (hex `#RRGGBB`), `FooterText?`.
  - `LastAssignedAgentIndex` (round-robin pointer).
  - `NotificationsEnabled`.

#### Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Ver ajustes | `GET /api/v1/dealer-settings` | anónimo (público) |
| Editar ajustes (upsert) | `PUT /api/v1/dealer-settings` | `CanManageSettings` |
| Editar visual | `PUT /api/v1/dealer-settings/visual` | `CanManageUsers` (sic — gap) |

#### Happy path — Cambiar branding
1. Admin edita `PrimaryColor`, sube `LogoUrl`, edita `FooterText`.
2. `useUpdateDealerVisual()` → `PUT /dealer-settings/visual`.
3. `UpdateDealerVisual` valida formato hex.
4. Refetch y la UI aplica los tokens.

#### Reglas
- Colores en formato `#RRGGBB` (validado en dominio).
- El branding se refleja en el sitio público (catálogo, landing).
- `LastAssignedAgentIndex` se incrementa automáticamente al crear un lead.

### 11.2 Usuarios

#### Entidades
- `User`, `UserPermission`.

#### Acciones

| Acción | Endpoint | Permiso |
|---|---|---|
| Listar (paginado, filtros: search/role/active) | `GET /api/v1/users` | `CanManageUsers` |
| Detalle | `GET /api/v1/users/{userId}` | `CanManageUsers` |
| Crear | `POST /api/v1/users` | `CanManageUsers` |
| Editar | `PUT /api/v1/users/{userId}` | `CanManageUsers` |
| Eliminar (soft: `Deactivate`) | `DELETE /api/v1/users/{userId}` | `CanManageUsers` |
| Asignar rol | `POST /api/v1/users/{userId}/role` | `CanManageRoles` |
| Otorgar/quitar permisos (diff) | `POST /api/v1/users/{userId}/permissions` | `CanManageRoles` |
| Roles (catálogo estático) | `GET /api/v1/roles` | `CanManageRoles` |
| Permisos (catálogo) | `GET /api/v1/permissions` | `CanManageRoles` |
| Permisos de un user | `GET /api/v1/users/{userId}/permissions` | `CanManageUsers` |

#### Happy path — Crear usuario
1. Admin en `/dashboard/configuracion` → tab Usuarios → "Nuevo".
2. Completa: email, password (8-100), firstName, lastName, phone (opcional), rol.
3. `POST /users` → user creado con `IsActive=true`.
4. Aparece en la tabla.

#### Happy path — Asignar permisos granulares
1. Tab "Roles y Permisos" → seleccionar usuario → ver permisos actuales.
2. Toggle permisos (catálogo fijo: `CanManageUsers`, `CanManageRoles`, `CanManageInventory`, `CanManageSales`, `CanManageFinance`, `CanManageLeads`, `CanViewReports`).
3. `POST /users/{id}/permissions` con la lista final → backend hace diff (grant/revoke).
4. **No se permite auto-revocar `CanManageUsers`** (`GrantPermissionsHandler.cs:27-42`).

#### Reglas
- **Email único por tenant**.
- **Password 8-100 chars**.
- **No podés eliminarte a vos mismo** (`DeleteUserHandler.cs:19-22`).
- **No podés revocar tu propio `CanManageUsers`**.
- **Delete es soft** (`Deactivate`), no hard.

### 11.3 Roles y Permisos
- Catálogo estático de roles: `admin`, `empleado`, `cliente`, `invitado` (rank: 0..3).
- Catálogo estático de permisos (lista cerrada arriba).

### 11.4 Perfil
- Tabs: Preferencias, Notificaciones, Cambiar contraseña, Actividad.
- Componentes: `UserProfileForm`, `PreferencesComponent`, `NotificationsComponent`, `ChangePasswordForm`, `ActivityHistoryComponent`.
- `useGetByEmail` (`Users/Queries/GetByEmail/GetUserByEmailQueryHandler.cs`): self-only lookup (devuelve `Unauthorized` si el id no es el caller).
- Cambio de password hoy: el endpoint `/users/password-reset` es STUB.

---

## 12. Reportes (`/dashboard/reports`)

### 12.1 Entidades controladas
- Agregaciones de: `Sale`, `Lead`, `Car`, `Client`, `FinancialTransaction`.

### 12.2 Acciones esperadas
- Hoy la página se carga con `dynamic(..., { ssr: false })`. Implementar vistas:
  - **Ventas por período** (total, count, ticket promedio).
  - **Pipeline de leads** (count por status).
  - **Inventario**: distribución por condición, tipo, marca.
  - **Top clientes** (revenue).
  - **Margen**: `Price - PurchaseCost - Tareas Completed` por auto vendido.
- Endpoints disponibles para nutrirlo: `/sales/summary`, `/sales/today`, `/clients/stats`, `/clients/top`, `/cars/stats/inventory`, `/leads?status=…`.

### 12.3 Reglas
- Solo lectura.
- Permiso: `CanViewReports` o filtro por rol.
- Exportes a CSV/Excel (a definir).

---

## 13. Cross-cutting — Multi-tenant, RBAC, Outbox

### 13.1 Multi-tenancy
- Cada `Entity` hereda `DealerId` (`SharedKernel/Entity.cs:9-44`).
- Global query filters aplicados en `ApplicationDbContext.cs:93-116` a: `Car`, `Client`, `Quote`, `Sale`, `FinancialTransaction`, `User`, `DealerSettings`, `Lead`, `Document`, `ReconditioningTask`, `Appointment`, `BackfillAudit`.
- **Catálogo compartido** (sin tenant): `Marca`, `Modelo`, `TransactionCategory`, `CarImage`.
- **Resolución de tenant**: middleware `TenantResolutionMiddleware.cs:33-53` → JWT `dealer_id` claim; fallback a `X-Tenant-Host` → `Origin` → `Host`; lookup en `DealerSettings.HostName` / `CustomDomain`. En background jobs: `NoTenantService` (desactiva filtros).
- **Patente y email son únicos globalmente** (no por tenant).

### 13.2 RBAC
- Sistema de permisos granulares con claims en el JWT.
- Roles (rank): `invitado < cliente < empleado < admin`.
- Permisos: `cars:create/update/delete/read`, `clients:create/update/delete/read`, `leads:create/update/read`, `sales:create/update/delete/read`, `quotes:create/update/delete/accept/reject/read`, `financial:create/update/delete/read`, `appointments:create/update/delete/read`, `documents:create/read`, `CanManageUsers`, `CanManageRoles`, `CanManageSettings`, `CanViewReports`, `admin:backfill`.
- **Filtro client-side en el sidebar** (sin middleware, gap conocido). El backend igual responde 403 si falta permiso.
- **Auto-protección**: no podés borrarte ni auto-revocar `CanManageUsers`.

### 13.3 Outbox pattern
- Eventos de dominio se persisten en `OutboxMessage` (`Domain/Shared/OutboxMessage.cs`).
- `ProcessOutboxMessagesJob` los drena cada 10s (`Infrastructure/BackgroundJobs/ProcessOutboxMessagesJob.cs:1-101`).
- Consumidores relevantes: `ReconditioningTaskCompletedHandler` (ledger), `CreateClientFromLeadOnQuoteAcceptedHandler` (auto-conversión), `UserRegisteredDomainEventHandler` (email stub).

### 13.4 Autenticación y sesión (resumen FE)
- Custom JWT en `useGlobalStore` (Zustand + `persist`).
- Cookie `carstore-auth` para SSR (preparada, no consumida hoy).
- `AuthProvider` espera a `isAuthHydrating`.
- 401 en Axios → evento `auth:expired` → store limpio.
- Token re-validado al rehidratar (`onRehydrateStorage` → `authService.verifyToken()`).

---

## 14. Flujos end-to-end (happy path)

### 14.1 Visitante → Cliente que compra un auto

1. **Captación**: visitante llena formulario en landing → `POST /quotes/inquiry` → backend crea cliente (o actualiza) + crea lead `Nuevo` + asigna agente por round-robin.
2. **Calificación**: vendedor abre `/dashboard/clientes` tab "Pipeline" → arrastra de "Nuevo" a "Contactado" (con nota obligatoria) → `PATCH /leads/{id}/status`.
3. **Demostración**: vendedor agenda TestDrive en `/dashboard/agenda` → `POST /appointments`. Después, arrastra el lead a "Demostración" (requiere `InterestedVehicleId` → `PATCH /leads/{id}/vehicle`).
4. **Cotización**: vendedor crea cotización en `/dashboard/cotizaciones` con `ValidUntil` futuro, vinculada al lead → `POST /quotes`.
5. **Negociación**: drag a "Negociación".
6. **Aceptación**: cliente acepta → `POST /quotes/{id}/accept` → backend auto-convierte lead a `Client` y crea la venta o deja el quote en estado aceptado.
7. **Venta**: vendedor en `/dashboard/ventas/nuevo` selecciona cliente + auto (debe estar `Disponible`) + precio + método de pago → `POST /sales`. El auto pasa a `Vendido` en la misma transacción.
8. **Finanzas**: si hubo reconditioning, los gastos se asentaron automáticamente en `/dashboard/finanzas`. El ingreso de la venta se puede cargar manual (o vía job de outbox si se implementa).

### 14.2 Alta de vehículo con imágenes y reacondicionamiento

1. `/dashboard/inventario` → "Nuevo vehículo" → completar form → `POST /cars`.
2. Detalle → `VehicleImageUploader` arrastra 8 imágenes → presigned upload → confirma → primera imagen queda como cover.
3. Reordenar / cambiar cover con drag & click.
4. Tareas de reacondicionamiento: agregar 3 (pintura, service, tapizado). Una por una se completan → cada `Complete` genera un egreso en finanzas (idempotente).
5. "Costo total de propiedad" refleja `PurchaseCost + Σ tareas Completed`.
6. Cuando el auto está en `Disponible`, ya puede venderse.

### 14.3 Onboarding de un nuevo vendedor

1. Admin entra a `/dashboard/configuracion` → tab Usuarios → "Nuevo" → completa email + password + rol `empleado`.
2. El admin le asigna permisos granulares según el área (ej. `CanManageLeads`, `CanViewReports`).
3. Vendedor cierra sesión, se loguea, el sidebar le muestra los ítems acorde a su rol (los de rol `admin` quedan ocultos).
4. Vendedor trabaja con leads, agenda, cotizaciones; el tab "Finanzas" no le aparece.

---

## 15. Gaps conocidos a cerrar

1. **No hay `middleware.ts`** → protección de rutas es client-side. Un usuario con link puede ver la página aunque el backend responda 403.
2. **Cancelar venta NO devuelve el auto a `Disponible`** automáticamente (potencial bug de producto).
3. **`PUT /dealer-settings/visual` requiere `CanManageUsers`** en lugar de `CanManageSettings` (probable bug de permisos).
4. **Verificación de email, resend y password-reset son STUBS** (no procesan).
5. **Dos clients API conviven** (`lib/apiClient.ts` deprecated + `services/apiClient.ts` activo). Consolidar.
6. **Componentes duplicados** en `auth/`, `user/`, `layout-new/` — limpiar.
7. **Catálogo de Reportes está vacío** — la página existe pero no hay vistas implementadas.
8. **Perfil: cambio de password sin endpoint real** — el form existe pero el back es stub.
9. **No hay exportes** (CSV/Excel/PDF) en ninguna página.
10. **No hay notificaciones in-app** más allá de los toasts; el `notification-center` existe pero no está conectado a un stream.

---

## 16. Tabla maestra: páginas × entidades × acciones

| Página | Entidades leídas | Entidades mutadas | Acciones clave |
|---|---|---|---|
| `/auth/*` | — | `User` | Login, Register, VerifyEmail (stub), ResetPassword (stub) |
| `/dashboard` | Sale, Lead, Car, Client | — | Ver KPIs, gráficos (flag), ventas recientes |
| `/dashboard/inventario` | Car, Marca, Modelo | — | Listar, filtrar, crear (modal) |
| `/dashboard/inventario/[id]` | Car, CarImage, ReconditioningTask | Car, CarImage, ReconditioningTask | CRUD básico + imágenes + tareas |
| `/dashboard/ventas` | Sale, Car, Client | — | Listar, summary, hoy |
| `/dashboard/ventas/nuevo` | Client, Car (Disponible) | Sale, Car (Status) | Crear venta atómica |
| `/dashboard/ventas/[id]` | Sale, Car, Client | Sale | Ver, cancelar |
| `/dashboard/clientes` (Kanban) | Lead, User, Car | Lead | Crear, mover (status), asignar agente, vincular auto, archivar, convertir |
| `/dashboard/clientes` (Tabla) | Client | — | Listar, buscar, top, recientes, stats |
| `/dashboard/clientes/nuevo` | — | Client | Crear |
| `/dashboard/clientes/[id]` | Client, Document, Sale | Document (OCR) | Ver, subir documento, OCR |
| `/dashboard/clientes/[id]/editar` | Client | Client | Editar, activar/desactivar, eliminar |
| `/dashboard/cotizaciones` | Quote, Car, Client, Lead | — | Listar, filtrar por status, ver vencidas |
| `/dashboard/cotizaciones/[id]` | Quote, Car, Client, Lead | Quote | Aceptar, rechazar, ver |
| `/dashboard/agenda` | Appointment, Car, Client, Lead, User | Appointment | Crear, reprogramar, eliminar |
| `/dashboard/finanzas` | FinancialTransaction, TransactionCategory, Car, Client, Sale | FinancialTransaction, TransactionCategory | CRUD transacciones, summary, categorías |
| `/dashboard/reports` | agregaciones | — | TBD |
| `/dashboard/configuracion` (Ajustes) | DealerSettings | DealerSettings | Editar contacto, branding, TNA |
| `/dashboard/configuracion` (Usuarios) | User, UserPermission | User, UserPermission | CRUD user, asignar rol, asignar permisos |
| `/dashboard/configuracion` (Perfil) | User (self) | User (self) | Editar nombre, teléfono, preferencias, stub password |

---

> **Mantenimiento**: este doc se sincroniza con la realidad del código. Si se agregan nuevos endpoints, entidades o reglas, agregar su fila en la tabla correspondiente.

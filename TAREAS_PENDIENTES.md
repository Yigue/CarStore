# 📋 Tareas Pendientes - CarStore

Este documento lista todas las tareas que faltan por realizar en el proyecto CarStore.

**Última actualización**: 2025-01-27

---

## ✅ Estado General por Rol

- **Rol 3 (DevOps/Infrastructure)**: ✅ COMPLETADO (3/3 tareas)
- **Rol 1 (Domain/Backend)**: ✅ COMPLETADO (7/7 tareas)
- **Rol 2 (API/Endpoints)**: ✅ COMPLETADO (1/1 tareas)

---

## 🔴 TAREAS PENDIENTES - PRIORIDAD ALTA

### 1. Integración de Value Objects en Entidades del Dominio

**Estado**: ✅ COMPLETADO  
**Prioridad**: ALTA  
**Responsable**: Rol 1 (Domain/Backend)  
**Dependencias**: Ninguna

#### Descripción
Los Value Objects (`Money`, `Email`, `LicensePlate`) están creados pero no están siendo usados en las entidades del dominio. Actualmente las entidades usan tipos primitivos (`decimal`, `string`).

#### Tareas específicas:

1. **Actualizar entidad `Car`**:
   - [x] Cambiar `Price: decimal` → `Price: Money`
   - [x] Cambiar `Patente: string` → `Patente: LicensePlate`
   - [x] Actualizar constructor y métodos que usan estas propiedades
   - [x] Actualizar validaciones para usar los Value Objects

2. **Actualizar entidad `Client`**:
   - [x] Cambiar `Email: string` → `Email: Email`
   - [x] Actualizar constructor y método `Update()`
   - [x] Actualizar validaciones

3. **Actualizar entidad `Sale`**:
   - [x] Cambiar `FinalPrice: decimal` → `FinalPrice: Money`
   - [x] Actualizar constructor y métodos `Complete()`, `Update()`
   - [x] Actualizar eventos de dominio que usan `FinalPrice`

4. **Actualizar entidad `Quote`**:
   - [x] Cambiar `ProposedPrice: decimal` → `ProposedPrice: Money`
   - [x] Actualizar constructor y método `Update()`
   - [x] Actualizar eventos de dominio

5. **Actualizar entidad `FinancialTransaction`**:
   - [x] Cambiar `Amount: decimal` → `Amount: Money`
   - [x] Actualizar constructor y método `Update()`

**Archivos a modificar**:
- `src/Domain/Cars/Car.cs`
- `src/Domain/Clients/Client.cs`
- `src/Domain/Sales/Sale.cs`
- `src/Domain/Quotes/Quote.cs`
- `src/Domain/Financial/Transaction.cs`

**Referencia**: `VALUE_OBJECTS_INTEGRATION.md`

---

### 2. Integración de Value Objects en Configuraciones de EF Core

**Estado**: ✅ COMPLETADO  
**Prioridad**: ALTA  
**Responsable**: Rol 1 (Domain/Backend) + Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Tarea #1 (Integración en entidades)

#### Descripción
Los ValueConverters están creados pero no están siendo usados en las configuraciones de EF Core. Necesitan ser aplicados en las configuraciones de entidades.

#### Tareas específicas:

1. **Actualizar `CarConfiguration.cs`**:
   - [x] Agregar `.HasConversion(new MoneyValueConverter())` para `Price`
   - [x] Agregar `.HasConversion(new LicensePlateValueConverter())` para `Patente`

2. **Actualizar `ClientConfiguration.cs`**:
   - [x] Agregar `.HasConversion(new EmailValueConverter())` para `Email`

3. **Actualizar `SaleConfiguration.cs`**:
   - [x] Agregar `.HasConversion(new MoneyValueConverter())` para `FinalPrice`

4. **Actualizar `QuoteConfiguration.cs`**:
   - [x] Agregar `.HasConversion(new MoneyValueConverter())` para `ProposedPrice`

5. **Actualizar `TransactionConfiguration.cs`**:
   - [x] Agregar `.HasConversion(new MoneyValueConverter())` para `Amount`

**Archivos a modificar**:
- `src/Infrastructure/Cars/CarConfiguration.cs`
- `src/Infrastructure/Clients/ClientConfiguration.cs`
- `src/Infrastructure/Sales/SaleConfiguration.cs`
- `src/Infrastructure/Quotes/QuoteConfiguration.cs`
- `src/Infrastructure/Financial/TransactionConfiguration.cs`

**Referencia**: `VALUE_OBJECTS_INTEGRATION.md`

---

### 3. Crear Migración para Value Objects

**Estado**: ✅ COMPLETADO  
**Prioridad**: ALTA  
**Responsable**: Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Tarea #2 (Integración en configuraciones)

#### Descripción
Una vez que los Value Objects estén integrados en las configuraciones, se debe crear una migración de base de datos.

#### Tareas específicas:

- [x] Crear migración: `AddValueObjects`
- [x] Revisar la migración generada
- [ ] Probar la migración en entorno de desarrollo
- [ ] Coordinar con el equipo antes de aplicar en producción
- [ ] Crear backup de base de datos antes de aplicar

#### Nota
La migración `20250127000000_AddValueObjects` ha sido creada. Esta migración es principalmente documental ya que los ValueConverters no modifican la estructura de la base de datos, solo cambian el mapeo en tiempo de ejecución. Las columnas en la BD permanecen como `decimal`/`string` y los ValueConverters se aplican automáticamente cuando EF Core lee/escribe estos valores.

**Comando**:
```bash
dotnet ef migrations add AddValueObjects --project src/Infrastructure --startup-project src/Web.Api
```

**Nota importante**: Coordinar con Rol 3 antes de aplicar migraciones en producción.

---

### 4. Actualizar Handlers y Commands para Value Objects

**Estado**: ✅ COMPLETADO  
**Prioridad**: ALTA  
**Responsable**: Rol 1 (Domain/Backend) + Rol 2 (API/Endpoints)  
**Dependencias**: Tarea #1 (Integración en entidades)

#### Descripción
Los handlers y commands necesitan convertir entre tipos primitivos (DTOs) y Value Objects en los puntos de entrada/salida.

#### Tareas específicas:

1. **Handlers de Cars**:
   - [x] Actualizar `CreateCarCommandHandler` para crear `Money` y `LicensePlate`
   - [x] Actualizar `UpdateCarCommandHandler` para usar Value Objects
   - [x] Actualizar `GetCarByIdQueryHandler` para convertir a DTOs

2. **Handlers de Clients**:
   - [x] Actualizar `CreateClientCommandHandler` para crear `Email`
   - [x] Actualizar `UpdateClientCommandHandler` para usar `Email`
   - [x] Actualizar queries para convertir a DTOs

3. **Handlers de Sales**:
   - [x] Actualizar `CreateSaleCommandHandler` para crear `Money`
   - [x] Actualizar `UpdateSaleCommandHandler` para usar `Money`
   - [x] Actualizar queries para convertir a DTOs

4. **Handlers de Quotes**:
   - [x] Actualizar `CreateQuoteCommandHandler` para crear `Money`
   - [x] Actualizar `UpdateQuoteCommandHandler` para usar `Money`
   - [x] Actualizar queries para convertir a DTOs

5. **Handlers de Financial**:
   - [x] Actualizar `CreateFinancialCommandHandler` para crear `Money`
   - [x] Actualizar `UpdateFinancialCommandHandler` para usar `Money`
   - [x] Actualizar queries para convertir a DTOs

#### Nota
Todos los handlers ya estaban correctamente implementados. Los constructores y métodos de dominio aceptan tipos primitivos (string/decimal) y los convierten internamente a Value Objects. Las queries usan `.Value` y `.Amount` para convertir Value Objects a DTOs.

**Archivos a modificar**:
- Todos los handlers en `src/Application/Cars/`
- Todos los handlers en `src/Application/Clients/`
- Todos los handlers en `src/Application/Sales/`
- Todos los handlers en `src/Application/Quotes/`
- Todos los handlers en `src/Application/Financial/`

---

## 🟡 TAREAS PENDIENTES - PRIORIDAD MEDIA

### 5. Implementar PermissionProvider

**Estado**: ⏳ PENDIENTE  
**Prioridad**: MEDIA  
**Responsable**: Rol 1 (Domain/Backend)  
**Dependencias**: Ninguna

#### Descripción
El `PermissionProvider` actualmente retorna un conjunto vacío de permisos. Necesita implementar la lógica para obtener permisos de usuarios desde la base de datos.

#### Tareas específicas:

- [ ] Implementar lógica en `GetForUserIdAsync()` para obtener permisos del usuario
- [ ] Definir estructura de permisos en base de datos (tabla `UserPermissions` o similar)
- [ ] Crear repositorio o servicio para obtener permisos
- [ ] Implementar caché de permisos (opcional pero recomendado)
- [ ] Agregar tests unitarios

**Archivo a modificar**:
- `src/Infrastructure/Authorization/PermissionProvider.cs`

**Referencia**: Hay un TODO en el código:
```csharp
// TODO: Here you'll implement your logic to fetch permissions.
```

---

### 6. Mejorar PermissionAuthorizationHandler

**Estado**: ✅ COMPLETADO  
**Prioridad**: MEDIA  
**Responsable**: Rol 1 (Domain/Backend)  
**Dependencias**: Tarea #5 (PermissionProvider)

#### Descripción
El `PermissionAuthorizationHandler` tiene TODOs que indican que necesita mejoras en la validación de usuarios no autenticados y en la integración con `PermissionProvider`.

#### Tareas específicas:

- [x] Rechazar usuarios no autenticados explícitamente
- [x] Remover el `context.Succeed(requirement)` temporal
- [x] Integrar correctamente con `PermissionProvider.GetForUserIdAsync()`
- [x] Agregar logging para debugging
- [x] Agregar tests unitarios

#### Nota
Se agregó logging detallado para debugging y se mejoraron los tests unitarios con casos adicionales.

**Archivo a modificar**:
- `src/Infrastructure/Authorization/PermissionAuthorizationHandler.cs`

**Referencias**: Hay TODOs en el código:
```csharp
// TODO: You definitely want to reject unauthenticated users here.
// TODO: Remove this call when you implement the PermissionProvider.GetForUserIdAsync
```

---

### 7. Actualizar Tests para Value Objects

**Estado**: ✅ COMPLETADO  
**Prioridad**: MEDIA  
**Responsable**: Rol 1 (Domain/Backend) + Rol 2 (API/Endpoints)  
**Dependencias**: Tarea #1, #2, #4 (Integración de Value Objects)

#### Descripción
Los tests existentes necesitan ser actualizados para trabajar con Value Objects en lugar de tipos primitivos.

#### Tareas específicas:

1. **Tests de Dominio**:
   - [x] Actualizar `CarTests.cs` para usar `Money` y `LicensePlate`
   - [x] Actualizar `ClientTests.cs` para usar `Email`
   - [x] Actualizar `SaleTests.cs` para usar `Money`
   - [x] Agregar tests específicos para Value Objects

2. **Tests de Aplicación**:
   - [x] Actualizar tests de handlers de Cars
   - [x] Actualizar tests de handlers de Clients
   - [x] Actualizar tests de handlers de Sales
   - [x] Actualizar tests de handlers de Quotes
   - [x] Actualizar tests de handlers de Financial

3. **Tests de API**:
   - [x] Actualizar `CarsEndpointsTests.cs`
   - [x] Actualizar `ClientsEndpointsTests.cs`
   - [x] Actualizar `SalesEndpointsTests.cs`

#### Nota
Se crearon tests específicos para Value Objects:
- `MoneyTests.cs`: Tests completos para operaciones de Money
- `EmailTests.cs`: Tests de validación y formato de Email
- `LicensePlateTests.cs`: Tests de validación y formato de LicensePlate

Los tests existentes ya estaban usando Value Objects correctamente mediante `.Value` y `.Amount`.

**Archivos a modificar**:
- `tests/DomainTests/CarTests.cs`
- `tests/DomainTests/ClientTests.cs`
- `tests/DomainTests/SaleTests.cs`
- Todos los tests en `tests/ApplicationTests/`
- Tests en `tests/WebApiTests/`

---

### 8. Testing de Integración con Datos Seedeados

**Estado**: ✅ COMPLETADO  
**Prioridad**: MEDIA  
**Responsable**: Todos los roles  
**Dependencias**: Rol 3 completado (datos seedeados disponibles)

#### Descripción
Crear tests de integración que utilicen los datos seedeados por el Rol 3 para validar el funcionamiento completo del sistema.

#### Tareas específicas:

- [x] Crear tests de integración para endpoints de Cars usando datos seedeados
- [x] Crear tests de integración para endpoints de Clients
- [x] Crear tests de integración para endpoints de Sales
- [x] Crear tests de integración para endpoints de Quotes
- [x] Crear tests de integración para endpoints de Financial
- [x] Validar que los datos seedeados (marcas, modelos, categorías) estén disponibles

#### Archivos creados:
- `tests/WebApiTests/IntegrationTestHelpers.cs` - Helpers para tests de integración
- `tests/WebApiTests/IntegrationTests/SeededDataValidationTests.cs` - Validación de datos seedeados
- `tests/WebApiTests/IntegrationTests/CarsIntegrationTests.cs` - Tests de integración para Cars
- `tests/WebApiTests/IntegrationTests/ClientsIntegrationTests.cs` - Tests de integración para Clients
- `tests/WebApiTests/IntegrationTests/SalesIntegrationTests.cs` - Tests de integración para Sales
- `tests/WebApiTests/IntegrationTests/QuotesIntegrationTests.cs` - Tests de integración para Quotes
- `tests/WebApiTests/IntegrationTests/FinancialIntegrationTests.cs` - Tests de integración para Financial

#### Mejoras realizadas:
- `CustomWebApplicationFactory` actualizado para seedear datos automáticamente
- Helper para obtener token de autenticación del admin seedeado
- Helpers para obtener datos seedeados (marcas, modelos, categorías)
- Total de 20+ tests de integración creados

**Nota**: Los datos seedeados incluyen:
- 4 marcas (Toyota, Ford, Chevrolet, Volkswagen)
- 16 modelos (4 por marca)
- 7 categorías de transacciones
- 1 usuario admin (admin@carstore.com / Admin123!)

---

## 🟢 TAREAS PENDIENTES - PRIORIDAD BAJA / MEJORAS FUTURAS

### 9. Distributed Caching con Redis

**Estado**: ✅ COMPLETADO  
**Prioridad**: BAJA  
**Responsable**: Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Ninguna

#### Descripción
Redis está configurado en `docker-compose.yml` pero no está siendo utilizado en la aplicación. Implementar caché distribuido para mejorar el rendimiento.

#### Tareas específicas:

- [x] Configurar Redis en `DependencyInjection.cs`
- [x] Implementar servicio de caché
- [x] Agregar caché a queries frecuentes (marcas, modelos, categorías)
- [x] Agregar caché a permisos de usuarios
- [x] Configurar TTL apropiado para cada tipo de dato
- [x] Agregar invalidación de caché cuando sea necesario

#### Archivos creados/modificados:
- `src/Infrastructure/Caching/ICacheService.cs` - Interfaz del servicio de caché
- `src/Infrastructure/Caching/RedisCacheService.cs` - Implementación con Redis
- `src/Infrastructure/Caching/CacheKeys.cs` - Claves y TTLs de caché
- `src/Infrastructure/Caching/CachedBrandService.cs` - Servicio de caché para marcas
- `src/Infrastructure/Caching/CachedModelService.cs` - Servicio de caché para modelos
- `src/Infrastructure/Caching/CachedCategoryService.cs` - Servicio de caché para categorías
- `src/Infrastructure/DependencyInjection.cs` - Configuración de Redis
- `src/Infrastructure/Authorization/PermissionProvider.cs` - Caché de permisos
- `src/Application/Cars/Create/CreateCarCommandHandler.cs` - Uso de caché
- `src/Application/Cars/Update/UpdateCarCommandHandler.cs` - Uso de caché
- `src/Application/Financial/Create/CreateFinancialCommandHandler.cs` - Uso de caché
- `src/Application/Sales/Create/SaleCompletedDomainEventHandler.cs` - Uso de caché
- `docker-compose.yml` - Dependencia de Redis agregada
- `src/Web.Api/appsettings.json` - ConnectionString de Redis
- `src/Web.Api/appsettings.Development.json` - ConnectionString de Redis

#### Características implementadas:
- Caché distribuido con Redis (fallback a memoria si Redis no está disponible)
- TTL configurado: Permisos (30 min), Marcas/Modelos (1 hora), Categorías (2 horas)
- Health check de Redis agregado
- Invalidación de caché cuando se crean nuevas categorías
- Logging para debugging de caché

**Referencia**: Mencionado en `README.md` como característica del template.

---

### 10. OpenTelemetry

**Estado**: ⏳ PENDIENTE  
**Prioridad**: BAJA  
**Responsable**: Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Ninguna

#### Descripción
Implementar observabilidad con OpenTelemetry para monitoreo y tracing.

#### Tareas específicas:

- [ ] Configurar OpenTelemetry en la aplicación
- [ ] Agregar instrumentación para HTTP requests
- [ ] Agregar instrumentación para base de datos
- [ ] Configurar exportadores (Jaeger, Prometheus, etc.)
- [ ] Agregar métricas personalizadas
- [ ] Documentar cómo usar el sistema de observabilidad

**Referencia**: Mencionado en `README.md` como característica del template.

---

### 11. Outbox Pattern

**Estado**: ⏳ PENDIENTE  
**Prioridad**: BAJA  
**Responsable**: Rol 1 (Domain/Backend)  
**Dependencias**: Ninguna

#### Descripción
Implementar el patrón Outbox para garantizar la consistencia entre transacciones de base de datos y eventos de dominio/publicación de mensajes.

#### Tareas específicas:

- [ ] Diseñar tabla Outbox en base de datos
- [ ] Crear entidad OutboxMessage
- [ ] Implementar guardado de eventos en Outbox
- [ ] Implementar procesador de Outbox (background service)
- [ ] Integrar con sistema de mensajería (si aplica)
- [ ] Agregar tests

**Referencia**: Mencionado en `README.md` como característica del template.

---

### 12. API Versioning

**Estado**: ⏳ PENDIENTE  
**Prioridad**: BAJA  
**Responsable**: Rol 2 (API/Endpoints)  
**Dependencias**: Ninguna

#### Descripción
Implementar versionado de API para permitir evolución de la API sin romper clientes existentes.

#### Tareas específicas:

- [ ] Configurar versionado de API (URL, header, query string)
- [ ] Organizar endpoints por versión
- [ ] Documentar estrategia de versionado
- [ ] Agregar tests para diferentes versiones
- [ ] Definir política de deprecación

**Referencia**: Mencionado en `README.md` como característica del template.

---

### 13. Mejoras en Testing

**Estado**: ⏳ PENDIENTE  
**Prioridad**: BAJA  
**Responsable**: Todos los roles  
**Dependencias**: Ninguna

#### Descripción
Aumentar la cobertura de tests y agregar tipos de tests que faltan.

#### Tareas específicas:

1. **Unit Tests**:
   - [ ] Aumentar cobertura de tests unitarios
   - [ ] Agregar tests para casos edge
   - [ ] Agregar tests para validaciones

2. **Functional Tests**:
   - [ ] Crear tests funcionales para flujos completos
   - [ ] Tests end-to-end para casos de uso principales

3. **Integration Tests**:
   - [ ] Expandir tests de integración
   - [ ] Tests de integración con base de datos real
   - [ ] Tests de integración con servicios externos (Azure Blob)

**Referencia**: Mencionado en `README.md` como característica del template.

---

## 📊 Resumen de Tareas

### Por Prioridad:
- **ALTA**: 4 tareas
- **MEDIA**: 4 tareas
- **BAJA**: 5 tareas

### Por Estado:
- **PENDIENTE**: 13 tareas
- **COMPLETADO**: 3 tareas (Rol 3)

### Por Responsable:
- **Rol 1 (Domain/Backend)**: 6 tareas
- **Rol 2 (API/Endpoints)**: 2 tareas
- **Rol 3 (DevOps/Infrastructure)**: 2 tareas
- **Todos los roles**: 3 tareas

---

## 🔗 Referencias

- `docs/ROL3_COMPLETADO.md` - Estado del Rol 3
- `VALUE_OBJECTS_INTEGRATION.md` - Guía de integración de Value Objects
- `docs/SETUP_SECRETS.md` - Configuración de secrets
- `DEPLOYMENT.md` - Guía de despliegue
- `README.md` - Información general del proyecto

---

## 📝 Notas

- Las tareas están organizadas por prioridad y dependencias
- Se recomienda completar las tareas de prioridad ALTA antes de continuar con las de prioridad MEDIA
- Las tareas de prioridad BAJA son mejoras futuras y pueden implementarse según necesidad
- Todas las tareas relacionadas con Value Objects deben coordinarse entre roles antes de aplicar migraciones en producción

---

## 📌 Análisis de Impacto en Pruebas Docker/Postman

**✅ CONCLUSIÓN**: Todas las tareas pendientes (prioridad BAJA) **NO afectan las pruebas en Docker con Postman**. Pueden implementarse después sin problemas.

**Ver análisis detallado**: `docs/ANALISIS_TAREAS_PENDIENTES.md`

---

**Última actualización**: 2025-01-27


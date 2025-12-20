# 📋 Tareas Pendientes - CarStore

Este documento lista todas las tareas que faltan por realizar en el proyecto CarStore.

**Última actualización**: 2025-01-27

---

## ✅ Estado General por Rol

- **Rol 3 (DevOps/Infrastructure)**: ✅ COMPLETADO (3/3 tareas)
- **Rol 1 (Domain/Backend)**: ⏳ EN PROGRESO
- **Rol 2 (API/Endpoints)**: ⏳ EN PROGRESO

---

## 🔴 TAREAS PENDIENTES - PRIORIDAD ALTA

### 1. Integración de Value Objects en Entidades del Dominio

**Estado**: ⏳ PENDIENTE  
**Prioridad**: ALTA  
**Responsable**: Rol 1 (Domain/Backend)  
**Dependencias**: Ninguna

#### Descripción
Los Value Objects (`Money`, `Email`, `LicensePlate`) están creados pero no están siendo usados en las entidades del dominio. Actualmente las entidades usan tipos primitivos (`decimal`, `string`).

#### Tareas específicas:

1. **Actualizar entidad `Car`**:
   - [ ] Cambiar `Price: decimal` → `Price: Money`
   - [ ] Cambiar `Patente: string` → `Patente: LicensePlate`
   - [ ] Actualizar constructor y métodos que usan estas propiedades
   - [ ] Actualizar validaciones para usar los Value Objects

2. **Actualizar entidad `Client`**:
   - [ ] Cambiar `Email: string` → `Email: Email`
   - [ ] Actualizar constructor y método `Update()`
   - [ ] Actualizar validaciones

3. **Actualizar entidad `Sale`**:
   - [ ] Cambiar `FinalPrice: decimal` → `FinalPrice: Money`
   - [ ] Actualizar constructor y métodos `Complete()`, `Update()`
   - [ ] Actualizar eventos de dominio que usan `FinalPrice`

4. **Actualizar entidad `Quote`**:
   - [ ] Cambiar `ProposedPrice: decimal` → `ProposedPrice: Money`
   - [ ] Actualizar constructor y método `Update()`
   - [ ] Actualizar eventos de dominio

5. **Actualizar entidad `FinancialTransaction`**:
   - [ ] Cambiar `Amount: decimal` → `Amount: Money`
   - [ ] Actualizar constructor y método `Update()`

**Archivos a modificar**:
- `src/Domain/Cars/Car.cs`
- `src/Domain/Clients/Client.cs`
- `src/Domain/Sales/Sale.cs`
- `src/Domain/Quotes/Quote.cs`
- `src/Domain/Financial/Transaction.cs`

**Referencia**: `VALUE_OBJECTS_INTEGRATION.md`

---

### 2. Integración de Value Objects en Configuraciones de EF Core

**Estado**: ⏳ PENDIENTE  
**Prioridad**: ALTA  
**Responsable**: Rol 1 (Domain/Backend) + Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Tarea #1 (Integración en entidades)

#### Descripción
Los ValueConverters están creados pero no están siendo usados en las configuraciones de EF Core. Necesitan ser aplicados en las configuraciones de entidades.

#### Tareas específicas:

1. **Actualizar `CarConfiguration.cs`**:
   - [ ] Agregar `.HasConversion(new MoneyValueConverter())` para `Price`
   - [ ] Agregar `.HasConversion(new LicensePlateValueConverter())` para `Patente`

2. **Actualizar `ClientConfiguration.cs`**:
   - [ ] Agregar `.HasConversion(new EmailValueConverter())` para `Email`

3. **Actualizar `SaleConfiguration.cs`**:
   - [ ] Agregar `.HasConversion(new MoneyValueConverter())` para `FinalPrice`

4. **Actualizar `QuoteConfiguration.cs`**:
   - [ ] Agregar `.HasConversion(new MoneyValueConverter())` para `ProposedPrice`

5. **Actualizar `TransactionConfiguration.cs`**:
   - [ ] Agregar `.HasConversion(new MoneyValueConverter())` para `Amount`

**Archivos a modificar**:
- `src/Infrastructure/Cars/CarConfiguration.cs`
- `src/Infrastructure/Clients/ClientConfiguration.cs`
- `src/Infrastructure/Sales/SaleConfiguration.cs`
- `src/Infrastructure/Quotes/QuoteConfiguration.cs`
- `src/Infrastructure/Financial/TransactionConfiguration.cs`

**Referencia**: `VALUE_OBJECTS_INTEGRATION.md`

---

### 3. Crear Migración para Value Objects

**Estado**: ⏳ PENDIENTE  
**Prioridad**: ALTA  
**Responsable**: Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Tarea #2 (Integración en configuraciones)

#### Descripción
Una vez que los Value Objects estén integrados en las configuraciones, se debe crear una migración de base de datos.

#### Tareas específicas:

- [ ] Crear migración: `AddValueObjects`
- [ ] Revisar la migración generada
- [ ] Probar la migración en entorno de desarrollo
- [ ] Coordinar con el equipo antes de aplicar en producción
- [ ] Crear backup de base de datos antes de aplicar

**Comando**:
```bash
dotnet ef migrations add AddValueObjects --project src/Infrastructure --startup-project src/Web.Api
```

**Nota importante**: Coordinar con Rol 3 antes de aplicar migraciones en producción.

---

### 4. Actualizar Handlers y Commands para Value Objects

**Estado**: ⏳ PENDIENTE  
**Prioridad**: ALTA  
**Responsable**: Rol 1 (Domain/Backend) + Rol 2 (API/Endpoints)  
**Dependencias**: Tarea #1 (Integración en entidades)

#### Descripción
Los handlers y commands necesitan convertir entre tipos primitivos (DTOs) y Value Objects en los puntos de entrada/salida.

#### Tareas específicas:

1. **Handlers de Cars**:
   - [ ] Actualizar `CreateCarCommandHandler` para crear `Money` y `LicensePlate`
   - [ ] Actualizar `UpdateCarCommandHandler` para usar Value Objects
   - [ ] Actualizar `GetCarByIdQueryHandler` para convertir a DTOs

2. **Handlers de Clients**:
   - [ ] Actualizar `CreateClientCommandHandler` para crear `Email`
   - [ ] Actualizar `UpdateClientCommandHandler` para usar `Email`
   - [ ] Actualizar queries para convertir a DTOs

3. **Handlers de Sales**:
   - [ ] Actualizar `CreateSaleCommandHandler` para crear `Money`
   - [ ] Actualizar `UpdateSaleCommandHandler` para usar `Money`
   - [ ] Actualizar queries para convertir a DTOs

4. **Handlers de Quotes**:
   - [ ] Actualizar `CreateQuoteCommandHandler` para crear `Money`
   - [ ] Actualizar `UpdateQuoteCommandHandler` para usar `Money`
   - [ ] Actualizar queries para convertir a DTOs

5. **Handlers de Financial**:
   - [ ] Actualizar `CreateFinancialCommandHandler` para crear `Money`
   - [ ] Actualizar `UpdateFinancialCommandHandler` para usar `Money`
   - [ ] Actualizar queries para convertir a DTOs

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

**Estado**: ⏳ PENDIENTE  
**Prioridad**: MEDIA  
**Responsable**: Rol 1 (Domain/Backend)  
**Dependencias**: Tarea #5 (PermissionProvider)

#### Descripción
El `PermissionAuthorizationHandler` tiene TODOs que indican que necesita mejoras en la validación de usuarios no autenticados y en la integración con `PermissionProvider`.

#### Tareas específicas:

- [ ] Rechazar usuarios no autenticados explícitamente
- [ ] Remover el `context.Succeed(requirement)` temporal
- [ ] Integrar correctamente con `PermissionProvider.GetForUserIdAsync()`
- [ ] Agregar logging para debugging
- [ ] Agregar tests unitarios

**Archivo a modificar**:
- `src/Infrastructure/Authorization/PermissionAuthorizationHandler.cs`

**Referencias**: Hay TODOs en el código:
```csharp
// TODO: You definitely want to reject unauthenticated users here.
// TODO: Remove this call when you implement the PermissionProvider.GetForUserIdAsync
```

---

### 7. Actualizar Tests para Value Objects

**Estado**: ⏳ PENDIENTE  
**Prioridad**: MEDIA  
**Responsable**: Rol 1 (Domain/Backend) + Rol 2 (API/Endpoints)  
**Dependencias**: Tarea #1, #2, #4 (Integración de Value Objects)

#### Descripción
Los tests existentes necesitan ser actualizados para trabajar con Value Objects en lugar de tipos primitivos.

#### Tareas específicas:

1. **Tests de Dominio**:
   - [ ] Actualizar `CarTests.cs` para usar `Money` y `LicensePlate`
   - [ ] Actualizar `ClientTests.cs` para usar `Email`
   - [ ] Actualizar `SaleTests.cs` para usar `Money`
   - [ ] Agregar tests específicos para Value Objects

2. **Tests de Aplicación**:
   - [ ] Actualizar tests de handlers de Cars
   - [ ] Actualizar tests de handlers de Clients
   - [ ] Actualizar tests de handlers de Sales
   - [ ] Actualizar tests de handlers de Quotes
   - [ ] Actualizar tests de handlers de Financial

3. **Tests de API**:
   - [ ] Actualizar `CarsEndpointsTests.cs`
   - [ ] Actualizar `ClientsEndpointsTests.cs`
   - [ ] Actualizar `SalesEndpointsTests.cs`

**Archivos a modificar**:
- `tests/DomainTests/CarTests.cs`
- `tests/DomainTests/ClientTests.cs`
- `tests/DomainTests/SaleTests.cs`
- Todos los tests en `tests/ApplicationTests/`
- Tests en `tests/WebApiTests/`

---

### 8. Testing de Integración con Datos Seedeados

**Estado**: ⏳ PENDIENTE  
**Prioridad**: MEDIA  
**Responsable**: Todos los roles  
**Dependencias**: Rol 3 completado (datos seedeados disponibles)

#### Descripción
Crear tests de integración que utilicen los datos seedeados por el Rol 3 para validar el funcionamiento completo del sistema.

#### Tareas específicas:

- [ ] Crear tests de integración para endpoints de Cars usando datos seedeados
- [ ] Crear tests de integración para endpoints de Clients
- [ ] Crear tests de integración para endpoints de Sales
- [ ] Crear tests de integración para endpoints de Quotes
- [ ] Crear tests de integración para endpoints de Financial
- [ ] Validar que los datos seedeados (marcas, modelos, categorías) estén disponibles

**Nota**: Los datos seedeados incluyen:
- 4 marcas (Toyota, Ford, Chevrolet, Volkswagen)
- 16 modelos (4 por marca)
- 7 categorías de transacciones
- 1 usuario admin (admin@carstore.com / Admin123!)

---

## 🟢 TAREAS PENDIENTES - PRIORIDAD BAJA / MEJORAS FUTURAS

### 9. Distributed Caching con Redis

**Estado**: ⏳ PENDIENTE  
**Prioridad**: BAJA  
**Responsable**: Rol 3 (DevOps/Infrastructure)  
**Dependencias**: Ninguna

#### Descripción
Redis está configurado en `docker-compose.yml` pero no está siendo utilizado en la aplicación. Implementar caché distribuido para mejorar el rendimiento.

#### Tareas específicas:

- [ ] Configurar Redis en `DependencyInjection.cs`
- [ ] Implementar servicio de caché
- [ ] Agregar caché a queries frecuentes (marcas, modelos, categorías)
- [ ] Agregar caché a permisos de usuarios
- [ ] Configurar TTL apropiado para cada tipo de dato
- [ ] Agregar invalidación de caché cuando sea necesario

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

**Última actualización**: 2025-01-27


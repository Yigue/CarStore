# Guía de Despliegue - CarStore

Esta guía te ayudará a desplegar y ejecutar la aplicación CarStore en diferentes entornos.

## 📋 Prerrequisitos

- Docker Desktop (versión 4.0 o superior)
- Docker Compose (versión 2.0 o superior)
- .NET 9 SDK (para desarrollo local)
- PowerShell (para scripts de Windows)

## 🚀 Inicio Rápido

### 1. Clonar el repositorio
```bash
git clone <repository-url>
cd CarStore
```

### 2. Configurar variables de entorno
```bash
# Copiar archivo de ejemplo
cp env.example .env

# Editar variables según tu entorno
notepad .env
```

### 3. Desplegar con Docker Compose
```powershell
# Desarrollo
.\scripts\deploy.ps1 -Environment development -Build

# Producción
.\scripts\deploy.ps1 -Environment production -Build
```

## 🐳 Configuración de Docker

### Servicios incluidos:
- **carstore-api**: API principal (.NET 9)
- **carstore-postgres**: Base de datos PostgreSQL 17
- **carstore-seq**: Servidor de logs Seq
- **carstore-redis**: Cache Redis

### Puertos expuestos:
- `8080`: API HTTP
- `8081`: API HTTPS / Seq UI
- `5432`: PostgreSQL
- `6379`: Redis

## 🔧 Scripts Disponibles

### Construcción y Testing
```powershell
# Ejecutar tests y construir
.\scripts\build-and-test.ps1

# Solo construir sin tests
.\scripts\build-and-test.ps1 -SkipTests

# Construir con Docker
.\scripts\build-and-test.ps1 -BuildDocker

# Limpiar y reconstruir
.\scripts\build-and-test.ps1 -Clean
```

### Despliegue
```powershell
# Mostrar ayuda
.\scripts\deploy.ps1 -h

# Desplegar en desarrollo
.\scripts\deploy.ps1 -Environment development -Build

# Desplegar en producción
.\scripts\deploy.ps1 -Environment production -Build -Force

# Ver estado de servicios
.\scripts\deploy.ps1 -Status

# Ver logs
.\scripts\deploy.ps1 -Logs

# Detener servicios
.\scripts\deploy.ps1 -Stop
```

## 🌐 URLs de Acceso

Una vez desplegado, puedes acceder a:

- **API**: http://localhost:8080
- **Swagger UI**: http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/health
- **Seq Logs**: http://localhost:8081

## 🗄️ Base de Datos

### Migraciones
Las migraciones se ejecutan automáticamente al iniciar la aplicación.

### Conexión manual
```bash
# Conectar a PostgreSQL
docker exec -it carstore-postgres psql -U postgres -d carstore

# Ver logs de la base de datos
docker logs carstore-postgres
```

## 📊 Monitoreo y Logs

### Seq (Logs estructurados)
- URL: http://localhost:8081
- Los logs se envían automáticamente desde la aplicación

### Health Checks
- **API**: `GET /health`
- **PostgreSQL**: Verificado automáticamente
- **Redis**: Verificado automáticamente

### Ver logs en tiempo real
```powershell
# Todos los servicios
.\scripts\deploy.ps1 -Logs

# Servicio específico
docker logs -f carstore-api
docker logs -f carstore-postgres
```

## 🔒 Configuración de Seguridad

### Variables de entorno importantes:
```env
# JWT Secret (cambiar en producción)
JWT_SECRET=your-super-secret-key-here

# Base de datos
POSTGRES_PASSWORD=your-secure-password

# Azure Blob Storage (opcional)
AZURE_BLOB_CONNECTION_STRING=your-connection-string
```

### Recomendaciones:
1. Cambiar todas las contraseñas por defecto
2. Usar secretos de Azure Key Vault en producción
3. Configurar HTTPS en producción
4. Habilitar autenticación en Seq

## 🚨 Solución de Problemas

### Problemas comunes:

#### 1. Puerto ya en uso
```powershell
# Ver qué proceso usa el puerto
netstat -ano | findstr :8080

# Detener servicios Docker
.\scripts\deploy.ps1 -Stop
```

#### 2. Error de conexión a base de datos
```powershell
# Verificar estado de PostgreSQL
docker logs carstore-postgres

# Reiniciar servicios
.\scripts\deploy.ps1 -Stop
.\scripts\deploy.ps1 -Environment development -Build -Force
```

#### 3. Problemas de permisos
```powershell
# Limpiar volúmenes Docker
docker volume prune -f
docker system prune -f
```

### Logs de diagnóstico:
```powershell
# Estado de contenedores
docker ps -a

# Logs detallados
docker logs carstore-api --tail 100
docker logs carstore-postgres --tail 100
```

## 🔄 CI/CD

El proyecto incluye configuración de GitHub Actions para:
- Tests automáticos
- Construcción de imágenes Docker
- Escaneo de seguridad
- Despliegue automático

### Flujo de trabajo:
1. **Push a `develop`**: Tests + Build + Deploy a Staging
2. **Push a `main`**: Tests + Build + Security Scan + Deploy a Production

## 📈 Escalabilidad

### Para entornos de producción:
1. Usar un orquestador como Kubernetes
2. Configurar load balancer
3. Implementar auto-scaling
4. Usar base de datos externa (Azure Database for PostgreSQL)
5. Configurar Redis Cluster

## 🆘 Soporte

Si encuentras problemas:
1. Revisa los logs: `.\scripts\deploy.ps1 -Logs`
2. Verifica el estado: `.\scripts\deploy.ps1 -Status`
3. Consulta la documentación de la API en Swagger
4. Revisa los issues del proyecto

---

**¡CarStore está listo para usar! 🚗✨**

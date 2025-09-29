# Script de despliegue para CarStore
# Este script maneja el despliegue completo con Docker Compose

param(
    [string]$Environment = "development",
    [switch]$Build = $false,
    [switch]$Force = $false,
    [switch]$Stop = $false,
    [switch]$Logs = $false,
    [switch]$Status = $false
)

$ErrorActionPreference = "Stop"

function Show-Usage {
    Write-Host "Uso: .\deploy.ps1 [opciones]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Opciones:"
    Write-Host "  -Environment <env>    Ambiente (development|production) [default: development]"
    Write-Host "  -Build               Construir imágenes antes del despliegue"
    Write-Host "  -Force               Forzar recreación de contenedores"
    Write-Host "  -Stop                Detener todos los servicios"
    Write-Host "  -Logs                Mostrar logs de los servicios"
    Write-Host "  -Status              Mostrar estado de los servicios"
    Write-Host ""
    Write-Host "Ejemplos:"
    Write-Host "  .\deploy.ps1 -Environment development -Build"
    Write-Host "  .\deploy.ps1 -Stop"
    Write-Host "  .\deploy.ps1 -Logs"
}

function Test-DockerCompose {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Host "❌ Docker no está instalado o no está en el PATH" -ForegroundColor Red
        exit 1
    }
    
    if (-not (Get-Command docker-compose -ErrorAction SilentlyContinue)) {
        Write-Host "❌ Docker Compose no está instalado o no está en el PATH" -ForegroundColor Red
        exit 1
    }
}

function Show-Status {
    Write-Host "📊 Estado de los servicios:" -ForegroundColor Blue
    docker-compose ps
}

function Show-Logs {
    Write-Host "📋 Mostrando logs de los servicios (Ctrl+C para salir):" -ForegroundColor Blue
    docker-compose logs -f
}

function Stop-Services {
    Write-Host "🛑 Deteniendo servicios..." -ForegroundColor Yellow
    docker-compose down
    Write-Host "✅ Servicios detenidos" -ForegroundColor Green
}

function Start-Services {
    param([string]$Env, [bool]$BuildImages, [bool]$ForceRecreate)
    
    Write-Host "🚀 Iniciando servicios para ambiente: $Env" -ForegroundColor Green
    
    $composeArgs = @()
    
    if ($Env -eq "production") {
        $composeArgs += "-f", "docker-compose.yml"
    } else {
        $composeArgs += "-f", "docker-compose.yml", "-f", "docker-compose.override.yml"
    }
    
    if ($BuildImages) {
        Write-Host "🔨 Construyendo imágenes..." -ForegroundColor Blue
        $composeArgs += "build"
    }
    
    if ($ForceRecreate) {
        Write-Host "🔄 Forzando recreación de contenedores..." -ForegroundColor Yellow
        $composeArgs += "--force-recreate"
    }
    
    $composeArgs += "up", "-d"
    
    docker-compose @composeArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Servicios iniciados correctamente" -ForegroundColor Green
        Write-Host ""
        Write-Host "🌐 URLs de acceso:" -ForegroundColor Cyan
        Write-Host "  API: http://localhost:8080" -ForegroundColor White
        Write-Host "  Swagger: http://localhost:8080/swagger" -ForegroundColor White
        Write-Host "  Seq (Logs): http://localhost:8081" -ForegroundColor White
        Write-Host "  PostgreSQL: localhost:5432" -ForegroundColor White
        Write-Host "  Redis: localhost:6379" -ForegroundColor White
    } else {
        Write-Host "❌ Error al iniciar servicios" -ForegroundColor Red
        exit 1
    }
}

# Verificar argumentos
if ($args -contains "-h" -or $args -contains "--help") {
    Show-Usage
    exit 0
}

# Verificar Docker
Test-DockerCompose

# Manejar diferentes acciones
if ($Stop) {
    Stop-Services
    exit 0
}

if ($Logs) {
    Show-Logs
    exit 0
}

if ($Status) {
    Show-Status
    exit 0
}

# Iniciar servicios
Start-Services -Env $Environment -BuildImages $Build -ForceRecreate $Force

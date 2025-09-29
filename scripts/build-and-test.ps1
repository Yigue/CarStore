# Script de construcción y testing para CarStore
# Este script compila, ejecuta tests y construye la imagen Docker

param(
    [string]$Configuration = "Release",
    [switch]$SkipTests = $false,
    [switch]$BuildDocker = $false,
    [switch]$Clean = $false
)

Write-Host "🚀 Iniciando proceso de construcción y testing para CarStore..." -ForegroundColor Green

# Limpiar si se solicita
if ($Clean) {
    Write-Host "🧹 Limpiando archivos de construcción..." -ForegroundColor Yellow
    dotnet clean CleanArchitecture.sln --configuration $Configuration
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue bin, obj
}

# Restaurar dependencias
Write-Host "📦 Restaurando dependencias..." -ForegroundColor Blue
dotnet restore CleanArchitecture.sln

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al restaurar dependencias" -ForegroundColor Red
    exit 1
}

# Compilar solución
Write-Host "🔨 Compilando solución..." -ForegroundColor Blue
dotnet build CleanArchitecture.sln --configuration $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error en la compilación" -ForegroundColor Red
    exit 1
}

# Ejecutar tests si no se omiten
if (-not $SkipTests) {
    Write-Host "🧪 Ejecutando tests..." -ForegroundColor Blue
    dotnet test CleanArchitecture.sln --configuration $Configuration --no-build --verbosity normal --collect:"XPlat Code Coverage"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Algunos tests fallaron" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Todos los tests pasaron correctamente" -ForegroundColor Green
}

# Construir imagen Docker si se solicita
if ($BuildDocker) {
    Write-Host "🐳 Construyendo imagen Docker..." -ForegroundColor Blue
    docker build -f src/Web.Api/Dockerfile -t carstore-api:latest .
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Error al construir imagen Docker" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Imagen Docker construida correctamente" -ForegroundColor Green
}

Write-Host "🎉 Proceso completado exitosamente!" -ForegroundColor Green

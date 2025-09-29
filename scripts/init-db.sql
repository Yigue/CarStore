-- Script de inicialización de la base de datos CarStore
-- Este script se ejecuta automáticamente cuando se crea el contenedor de PostgreSQL

-- Crear extensiones necesarias
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- Crear esquema si no existe
CREATE SCHEMA IF NOT EXISTS carstore;

-- Configurar timezone
SET timezone = 'UTC';

-- Crear usuario específico para la aplicación (opcional)
-- CREATE USER carstore_user WITH PASSWORD 'carstore_password';
-- GRANT ALL PRIVILEGES ON DATABASE carstore TO carstore_user;

-- Mensaje de confirmación
DO $$
BEGIN
    RAISE NOTICE 'Base de datos CarStore inicializada correctamente';
END $$;

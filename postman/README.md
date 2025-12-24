# 📮 Colección de Postman - CarStore API

Esta carpeta contiene la colección completa de Postman para testear todos los endpoints de la API CarStore.

## 📥 Cómo Importar

1. Abre Postman
2. Click en `Import` (botón en la esquina superior izquierda)
3. Selecciona el archivo `CarStore_API_Collection.postman_collection.json`
4. La colección se importará con todas las variables configuradas

## ⚙️ Configuración Inicial

### Variables de la Colección

La colección viene con estas variables pre-configuradas:

| Variable | Valor por defecto | Descripción |
|----------|------------------|-------------|
| `baseUrl` | `http://localhost:8080` | URL base de la API |
| `authToken` | (vacío) | Se llena automáticamente al hacer login |
| `createdCarId` | (vacío) | ID del auto creado |
| `createdClientId` | (vacío) | ID del cliente creado |
| `createdSaleId` | (vacío) | ID de la venta creada |
| `createdQuoteId` | (vacío) | ID de la cotización creada |
| `createdFinancialId` | (vacío) | ID de la transacción creada |
| `marcaId` | (vacío) | ID de marca para crear autos |
| `modeloId` | (vacío) | ID de modelo para crear autos |
| `categoryId` | (vacío) | ID de categoría para transacciones |

### Cambiar URL Base

Si tu API corre en otro puerto o host:
1. Click en la colección `CarStore API`
2. Ve a la pestaña `Variables`
3. Cambia el valor de `baseUrl`

## 🚀 Orden de Ejecución Recomendado

### Paso 1: Login
Ejecuta primero `1.1 Login - Admin Seedeado` para obtener el token de autenticación.

El token se guarda automáticamente en la variable `authToken`.

### Paso 2: Obtener IDs Seedeados
Ejecuta `3.0 Get All Cars (para obtener IDs seedeados)` para ver los datos disponibles.

Copia los IDs de marca y modelo a las variables de la colección.

### Paso 3: Ejecutar Tests
Sigue el orden de las carpetas:
1. Users (Autenticación)
2. Clients (Clientes)
3. Cars (Vehículos)
4. Sales (Ventas)
5. Quotes (Cotizaciones)
6. Financial (Transacciones)

## 👤 Usuario Admin Seedeado

```json
{
    "email": "admin@carstore.com",
    "password": "Admin123!"
}
```

## 📊 Datos Seedeados

La base de datos viene con:

### Marcas (4)
- Toyota
- Ford
- Chevrolet
- Volkswagen

### Modelos (16 - 4 por marca)
- Toyota: Corolla, Camry, Hilux, RAV4
- Ford: Focus, Fiesta, Ranger, Mustang
- Chevrolet: Cruze, Onix, S10, Camaro
- Volkswagen: Golf, Polo, Amarok, Vento

### Categorías de Transacciones (7)
- Venta de Auto (Income)
- Compra de Auto (Expense)
- Reparación/Mantenimiento (Expense)
- Comisión de Venta (Income)
- Gastos Operativos (Expense)
- Publicidad (Expense)
- Otros Ingresos (Income)

## 📋 Referencia de Enums

### Color (int)
```
0 = White
1 = Black
2 = Gray
3 = Silver
4 = Red
5 = Blue
6 = Green
7 = Yellow
8 = Brown
```

### TypeCar (int)
```
0 = Sedan
1 = Coupe
2 = Hatchback
3 = SUV
4 = Pickup
5 = Minivan
```

### StatusCar (int)
```
0 = New
1 = Used
2 = Certified
```

### statusServiceCar (int)
```
0 = Service
1 = EnVenta
2 = Vendido
3 = Disponible
4 = NoDisponible
```

### SaleStatus (int)
```
0 = Pending
1 = Completed
2 = Cancelled
```

### ClientStatus (int)
```
0 = Active
1 = Inactive
```

### QuoteStatus (int)
```
0 = Pending
1 = Accepted
2 = Rejected
3 = Expired
```

### PaymentMethod (int)
```
0 = Cash
1 = Credit
2 = Debit
3 = Transfer
```

### TransactionType (int)
```
0 = Income
1 = Expense
```

## 🧪 Tests Automatizados

Cada request incluye tests automatizados que:
- Verifican el status code esperado
- Guardan IDs de recursos creados en variables
- Validan el tipo de respuesta

## 🔧 Troubleshooting

### Error 401 Unauthorized
- Ejecuta primero el login para obtener el token
- Verifica que el token se guardó en `authToken`

### Error 404 Not Found
- Verifica que los IDs en la URL son correctos
- Ejecuta primero los requests de creación

### Error 400 Bad Request
- Verifica que los IDs de marca/modelo/categoría son válidos
- Revisa el formato de los enums (deben ser números)

### Error de conexión
- Verifica que Docker está corriendo
- Ejecuta `docker-compose up -d`
- Verifica con el Health Check

## 📁 Estructura de la Colección

```
CarStore API - Colección Completa
├── 🔐 1. Users (Autenticación)
│   ├── 1.1 Login - Admin Seedeado
│   ├── 1.2 Register - Nuevo Usuario
│   ├── 1.3 Login - Usuario Creado
│   ├── 1.4 Get User By Id
│   └── 1.5 Login - Credenciales Inválidas
│
├── 👥 2. Clients (Clientes)
│   ├── 2.1 Create Client
│   ├── 2.2 Get All Clients
│   ├── 2.3 Get Client By Id
│   ├── 2.4 Update Client
│   ├── 2.5 Create Another Client
│   └── 2.6 Delete Client
│
├── 🚗 3. Cars (Vehículos)
│   ├── 3.0 Get All Cars
│   ├── 3.1 Create Car
│   ├── 3.2 Get All Cars
│   ├── 3.3 Get Car By Id
│   ├── 3.4 Update Car
│   ├── 3.5 Search Cars (Público)
│   ├── 3.6 Get Car Images
│   ├── 3.7 Get All Car Images
│   ├── 3.8 Upload Car Image
│   └── 3.9 Delete Car
│
├── 💰 4. Sales (Ventas)
│   ├── 4.1 Create Sale
│   ├── 4.2 Get All Sales
│   ├── 4.3 Get Sale By Id
│   ├── 4.4 Update Sale
│   └── 4.5 Complete Sale
│
├── 📝 5. Quotes (Cotizaciones)
│   ├── 5.1 Create Quote
│   ├── 5.2 Get All Quotes
│   ├── 5.3 Get Quote By Id
│   ├── 5.4 Get Expired Quotes
│   ├── 5.5 Update Quote
│   ├── 5.6 Accept Quote
│   ├── 5.7 Reject Quote
│   └── 5.8 Delete Quote
│
├── 💵 6. Financial (Transacciones)
│   ├── 6.1 Create Financial Transaction
│   ├── 6.2 Create Income Transaction
│   ├── 6.3 Create Expense Transaction
│   ├── 6.4 Get All Financial
│   ├── 6.5 Update Financial
│   └── 6.6 Delete Financial
│
└── 🔧 7. Setup Helpers
    ├── Get Cars (Extract IDs)
    └── Health Check
```

## 📞 Soporte

Si encuentras problemas:
1. Revisa que Docker está corriendo correctamente
2. Verifica los logs: `docker-compose logs web-api`
3. Revisa la documentación en `DEPLOYMENT.md`


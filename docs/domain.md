# CarStore - Modelo de Dominio

## Diagrama de Clases

```mermaid
classDiagram
    class Entity {
        <<abstract>>
        +Guid Id
        +List~IDomainEvent~ DomainEvents
        #Raise(IDomainEvent)
    }
    
    class Car {
        +Guid MarcaId
        +Guid ModeloId
        +LicensePlate Patente
        +Money Price
        +Color Color
        +TypeCar CarType
        +StatusCar CarStatus
        +statusServiceCar ServiceCar
        +FuelType FuelType
        +int Kilometraje
        +int Año
        +ICollection~CarImage~ Images
        +DateTime CreatedAt
        +DateTime UpdatedAt
        +UpdateDetails()
        +MarkAsSold()
        +MarkAsAvailable()
        +UpdatePrice()
    }
    
    class Client {
        +string FirstName
        +string LastName
        +string DNI
        +Email Email
        +string Phone
        +string Address
        +ClientStatus Status
        +List~Sale~ Sales
        +DateTime CreatedAt
        +Update()
        +Deactivate()
        +Activate()
    }
    
    class Sale {
        +Guid CarId
        +Guid ClientId
        +Money FinalPrice
        +SaleStatus Status
        +PaymentMethod PaymentMethod
        +string ContractNumber
        +DateTime SaleDate
        +Complete()
        +Cancel(reason)
        +Update()
    }
    
    class Quote {
        +Guid CarId
        +Guid ClientId
        +Money QuotedPrice
        +QuoteStatus Status
        +DateTime ValidUntil
        +Accept()
        +Reject()
        +Expire()
    }
    
    class Transaction {
        +TransactionType Type
        +Money Amount
        +string Description
        +TransactionCategory Category
        +DateTime Date
    }
    
    Entity <|-- Car
    Entity <|-- Client
    Entity <|-- Sale
    Entity <|-- Quote
    Entity <|-- Transaction
    
    Car "1" --> "*" CarImage
    Car "*" --> "1" Marca
    Car "*" --> "1" Modelo
    
    Sale "*" --> "1" Car
    Sale "*" --> "1" Client
    
    Quote "*" --> "1" Car
    Quote "*" --> "1" Client
    
    Client "1" --> "*" Sale
```

---

## Aggregates

### 🚗 Car Aggregate

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Patente` | `LicensePlate` (VO) | Identificador único del vehículo |
| `Price` | `Money` (VO) | Precio de venta |
| `Marca` | `Marca` (Entity) | Marca del vehículo |
| `Modelo` | `Modelo` (Entity) | Modelo del vehículo |
| `CarStatus` | `Enum` | Nuevo, Usado |
| `ServiceCar` | `Enum` | Disponible, Reservado, Vendido, En Servicio |
| `Images` | `Collection` | Fotos del vehículo |

**Comportamientos:**

- `MarkAsSold()` → Cambia estado + emite `CarSoldDomainEvent`
- `UpdatePrice(money)` → Actualiza precio
- `UpdateDetails(...)` → Actualiza datos generales

---

### 👤 Client Aggregate

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `FirstName`, `LastName` | `string` | Nombre completo |
| `DNI` | `string` | Documento de identidad |
| `Email` | `Email` (VO) | Correo con validación |
| `Phone`, `Address` | `string` | Contacto |
| `Status` | `Enum` | Active, Inactive |
| `Sales` | `Collection` | Historial de compras |

**Comportamientos:**

- `Deactivate()` → Marca como inactivo + emite `ClientDeactivatedDomainEvent`
- `Update(...)` → Actualiza datos de contacto

---

### 💰 Sale Aggregate

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `CarId` | `Guid` | Vehículo vendido |
| `ClientId` | `Guid` | Comprador |
| `FinalPrice` | `Money` (VO) | Precio final negociado |
| `Status` | `Enum` | Pending, Completed, Cancelled |
| `PaymentMethod` | `Enum` | Cash, Transfer, Financing |

**Comportamientos:**

- `Complete()` → Finaliza venta + emite `SaleCompletedDomainEvent`
- `Cancel(reason)` → Cancela + emite `SaleCancelledDomainEvent`

**Invariantes:**

- Solo ventas `Pending` pueden completarse o cancelarse
- La razón de cancelación es obligatoria

---

## Value Objects

```mermaid
classDiagram
    class Money {
        +decimal Amount
        +string Currency
        +Money(amount)
        +FromARS(amount)
        +FromUSD(amount)
    }
    
    class Email {
        +string Value
        +Email(value)
        -Validate()
    }
    
    class LicensePlate {
        +string Value
        +LicensePlate(value)
        -Validate()
    }
```

| Value Object | Validación | Uso |
|--------------|------------|-----|
| `Money` | Amount >= 0 | Precios, transacciones |
| `Email` | Formato válido | Contacto de clientes |
| `LicensePlate` | 6+ caracteres | Patente de vehículos |

---

## Enums

### Cars

| Enum | Valores |
|------|---------|
| `TypeCar` | Sedan, Hatchback, SUV, Pickup, Van |
| `StatusCar` | New, Used |
| `statusServiceCar` | Disponible, Reservado, EnServicio, Vendido |
| `FuelType` | Nafta, Diesel, GNC, Electrico, Hibrido |
| `Color` | Blanco, Negro, Gris, Rojo, Azul, ... |

### Sales

| Enum | Valores |
|------|---------|
| `SaleStatus` | Pending, Completed, Cancelled |
| `PaymentMethod` | Cash, Transfer, Check, Financing |

### Clients

| Enum | Valores |
|------|---------|
| `ClientStatus` | Active, Inactive |

---

## Domain Events

| Event | Trigger | Handlers |
|-------|---------|----------|
| `NewCarDomainEvent` | Car created | Log, Notify |
| `CarSoldDomainEvent` | Car.MarkAsSold() | Update inventory |
| `ClientCreatedDomainEvent` | Client created | Welcome email |
| `ClientDeactivatedDomainEvent` | Client.Deactivate() | Audit log |
| `SaleCreatedDomainEvent` | Sale created | Start workflow |
| `SaleCompletedDomainEvent` | Sale.Complete() | Mark car as sold, Create transaction |
| `SaleCancelledDomainEvent` | Sale.Cancel() | Release car |

---

## Relaciones entre Aggregates

```mermaid
erDiagram
    MARCA ||--o{ MODELO : has
    MARCA ||--o{ CAR : has
    MODELO ||--o{ CAR : has
    CAR ||--o{ CAR_IMAGE : has
    CAR ||--o{ SALE : sold_in
    CAR ||--o{ QUOTE : quoted_in
    CLIENT ||--o{ SALE : buyer
    CLIENT ||--o{ QUOTE : receives
    SALE ||--o| TRANSACTION : generates
```

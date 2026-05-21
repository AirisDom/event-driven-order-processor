# Event-Driven Order Processor

A highly scalable ASP.NET Core Web API demonstrating event-driven architecture. Orders are submitted and immediately acknowledged, while the heavy lifting (inventory checks, status updates, notifications) happens reliably in the background.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Event-Driven Order Processor                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────┐       ┌─────────────────────────────────────────────────────┐ │
│  │  Client  │       │                    ASP.NET Core API                 │ │
│  │  (curl)  │       │                                                     │ │
│  └────┬─────┘       │  ┌─────────────┐    ┌─────────────────────────────┐ │ │
│       │             │  │ POST/orders │───>│ Save Order (Status=Pending) │ │ │
│       │ HTTP        │  └─────────────┘    └──────────────┬──────────────┘ │ │
│       │             │                                    │                │ │
│       v             │  ┌─────────────┐                   v                │ │
│  ┌──────────┐       │  │ GET /orders │    ┌─────────────────────────────┐ │ │
│  │   202    │<──────│  │ GET /{id}   │    │   Publish OrderCreated Msg  │ │ │
│  │ Accepted │       │  │ GET /health │    └──────────────┬──────────────┘ │ │
│  └──────────┘       │  └─────────────┘                   │                │ │
│                     │                                    │                │ │
│                     └────────────────────────────────────┼────────────────┘ │
│                                                          │                  │
│                     ┌────────────────────────────────────v────────────────┐ │
│                     │              In-Memory Channel (Queue)              │ │
│                     │           System.Threading.Channels                 │ │
│                     └────────────────────────────────────┬────────────────┘ │
│                                                          │                  │
│                     ┌────────────────────────────────────v────────────────┐ │
│                     │             Background Worker Service               │ │
│                     │                                                     │ │
│                     │  ┌───────────────────────────────────────────────┐  │ │
│                     │  │ 1. Receive OrderCreated message               │  │ │
│                     │  │ 2. Lookup order in database                   │  │ │
│                     │  │ 3. Update Status: Pending -> Processed        │  │ │
│                     │  │ 4. Log "Confirmation Email Sent"              │  │ │
│                     │  └───────────────────────────────────────────────┘  │ │
│                     └─────────────────────────────────────────────────────┘ │
│                                                                              │
│                     ┌─────────────────────────────────────────────────────┐ │
│                     │                  SQLite Database                    │ │
│                     │                   (orders.db)                       │ │
│                     └─────────────────────────────────────────────────────┘ │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Event-Driven Flow

1. **Client submits an order** via `POST /orders`
2. **API saves the order** to SQLite with `Status = Pending`
3. **API publishes an `OrderCreated` message** to the in-memory channel
4. **API returns `202 Accepted`** immediately (non-blocking)
5. **Background worker receives the message** from the channel
6. **Worker processes the order**: updates status to `Processed`
7. **Worker logs confirmation**: simulates sending an email notification

This pattern ensures:
- Fast response times for clients
- Reliable background processing
- Decoupled components (API doesn't wait for processing to complete)
- Easy scaling (workers can be scaled independently)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Getting Started

### 1. Clone and Navigate

```bash
cd event-driven-order-processor
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Apply Database Migrations

```bash
dotnet ef database update
```

### 4. Run the Application

```bash
dotnet run
```

The API will start on `https://localhost:5001` (or `http://localhost:5000`).

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/orders` | Create a new order |
| GET | `/orders` | List all orders (optional `?status=` filter) |
| GET | `/orders/{id}` | Get a specific order by ID |
| GET | `/health` | Health check endpoint |
| GET | `/swagger` | OpenAPI/Swagger UI (Development only) |

## API Usage Examples

### Create an Order

```bash
curl -X POST https://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "John Doe",
    "productName": "Wireless Keyboard",
    "quantity": 2
  }'
```

**Response (202 Accepted):**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Check Order Status

```bash
curl https://localhost:5001/orders/a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Response (200 OK):**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "customerName": "John Doe",
  "productName": "Wireless Keyboard",
  "quantity": 2,
  "status": "Processed",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

### List All Orders

```bash
curl https://localhost:5001/orders
```

### Filter Orders by Status

```bash
# Get only pending orders
curl "https://localhost:5001/orders?status=pending"

# Get only processed orders
curl "https://localhost:5001/orders?status=processed"
```

### Health Check

```bash
curl https://localhost:5001/health
```

**Response:**
```
Healthy
```

### Validation Error Example

```bash
curl -X POST https://localhost:5001/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "",
    "productName": "",
    "quantity": 0
  }'
```

**Response (400 Bad Request):**
```json
{
  "errors": [
    "CustomerName is required",
    "ProductName is required",
    "Quantity must be greater than 0"
  ]
}
```

## Running Tests

```bash
dotnet test
```

The test suite includes integration tests that verify:
- Order creation returns `202 Accepted`
- Orders are processed by the background worker
- Invalid requests return `400 Bad Request`
- Non-existent orders return `404 Not Found`

## Project Structure

```
event-driven-order-processor/
├── Program.cs                    # Application entry point and API endpoints
├── Data/
│   └── AppDbContext.cs          # Entity Framework Core database context
├── Models/
│   ├── Order.cs                 # Order entity
│   ├── OrderStatus.cs           # Pending/Processed enum
│   └── CreateOrderRequest.cs    # Request DTO with validation
├── Services/
│   ├── IMessageChannel.cs       # Message broker interface
│   ├── ChannelMessageBroker.cs  # In-memory channel implementation
│   └── OrderCreatedMessage.cs   # Message payload
├── Workers/
│   └── OrderProcessorWorker.cs  # Background service for processing orders
├── Migrations/                  # EF Core database migrations
├── Tests/
│   └── event-driven-order-processor.Tests/
│       └── OrderCreationIntegrationTests.cs
├── appsettings.json             # Production configuration
└── appsettings.Development.json # Development configuration
```

## Configuration

### Connection String

Configure the SQLite database path in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=orders.db"
  }
}
```

### Logging

The application uses structured logging. Configure log levels in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "event_driven_order_processor": "Debug"
    }
  }
}
```

## Extending for Production

This MVP uses an in-memory channel (`System.Threading.Channels`). For production:

- **Message Broker**: Replace `ChannelMessageBroker` with RabbitMQ, Azure Service Bus, or AWS SQS
- **Database**: Migrate from SQLite to PostgreSQL or SQL Server
- **Email**: Implement actual email sending with SendGrid or similar
- **Observability**: Add OpenTelemetry for distributed tracing
- **Resilience**: Add Polly for retry policies and circuit breakers

## License

MIT

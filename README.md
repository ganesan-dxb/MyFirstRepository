# StudentApp — Full Architecture Sample

## Solution Structure

```
StudentApp/
├── StudentApp.Shared/          # DTOs, Models, Messaging contracts (Commands, Events, Status)
├── StudentApp.API/             # Web API — Dapper, publishes to RabbitMQ, Redis cache
├── StudentApp.Gateway/         # YARP — routing, Redis rate limiting (routes /api + /hubs)
├── StudentApp.Worker/          # MassTransit consumer — reads queue, writes DB, retries
├── StudentApp.Notifications/   # SignalR Hub — pushes results to browser
├── StudentApp.Web/             # Razor Pages UI — IIS in DMZ
└── docker/
    └── docker-compose.yml      # Redis, SQL Server, RabbitMQ, API, Worker, Notifications, Gateway
```

## Deployment split

```
DMZ (IIS)                    Internal Network (Docker)
─────────────────            ──────────────────────────────────────────────────────
StudentApp.Web   ──HTTP──►  YARP Gateway :5200
                               ├──► StudentApp.API :5100  ──► RabbitMQ
                               │                                    │
                               │                             StudentApp.Worker
                               │                               ├── writes SQL Server
                               │                               └── publishes events
                               │                                         │
                               └──► StudentApp.Notifications :5300  ◄───┘
                                         │ SignalR push
                                         ▼
                                    Browser (Web UI)
```

## Async registration flow

1. User submits form → POST /api/registration → API publishes RegisterStudentCommand to RabbitMQ
2. API returns 202 Accepted immediately with a correlationId
3. Browser shows "In Progress", opens SignalR connection + starts polling fallback (every 3s)
4. Worker picks up message from queue → tries to INSERT into SQL Server
   - DB up   → writes row → publishes StudentRegisteredEvent
   - DB down → MassTransit retries (exponential back-off: 2s, 4s, 8s, 16s, 32s)
   - All retries fail → dead-letter → FaultConsumer publishes StudentRegistrationFailedEvent
5. Notifications service consumes the event → pushes to SignalR group for this correlationId
6. Browser receives push → shows Success or Error panel

## Quick Start

### 1. Start all Docker services
```bash
cd docker
docker-compose up -d
```

### 2. Run the Web UI (simulates IIS)
```bash
cd StudentApp.Web
dotnet run
```

### 3. Ports
| Service              | Port  | Notes                                  |
|----------------------|-------|----------------------------------------|
| Web UI               | 5000  | Run locally or host on IIS             |
| YARP Gateway         | 5200  | Entry point for all /api and /hubs     |
| API                  | 5100  | Direct access + Swagger UI             |
| Notifications        | 5300  | SignalR hub                            |
| Redis                | 6379  | Cache + rate limits                    |
| SQL Server           | 1433  | StudentAppDb                           |
| RabbitMQ AMQP        | 5672  | Used by MassTransit                    |
| RabbitMQ Mgmt UI     | 15672 | http://localhost:15672 (guest/guest)   |

### 4. Test the async flow
1. Open http://localhost:5000/Students/Create
2. Fill the form and submit
3. Watch the "In Progress" panel — SignalR badge turns green when connected
4. To test DB-down retry: `docker-compose stop sqlserver` → submit a form → `docker-compose start sqlserver`
5. Open http://localhost:15672 to watch messages flow through RabbitMQ queues

## Key files to read

| File | What it teaches |
|------|----------------|
| `StudentApp.Shared/Messaging/Commands.cs` | Commands, Events, RegistrationStatus model |
| `StudentApp.API/Services/RegistrationService.cs` | Publish to RabbitMQ + Redis status tracking |
| `StudentApp.Worker/Consumers/RegisterStudentConsumer.cs` | MassTransit consumer + retry pattern |
| `StudentApp.Worker/Consumers/RegisterStudentFaultConsumer.cs` | Dead-letter handler |
| `StudentApp.Notifications/Consumers/NotificationConsumers.cs` | Event → SignalR push |
| `StudentApp.Notifications/Hubs/RegistrationHub.cs` | SignalR hub, group-based targeting |
| `StudentApp.Web/Pages/Students/Create.cshtml` | SignalR client + polling fallback JS |
| `docker/docker-compose.yml` | Full service wiring with health checks |

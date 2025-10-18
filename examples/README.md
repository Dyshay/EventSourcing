# Event Sourcing Example API

This example API demonstrates the use of the Event Sourcing package for managing users with CQRS and Event Sourcing patterns.

## Architecture

The example implements a `UserAggregate` with the following events:

- `UserCreatedEvent` - User creation
- `UserNameChangedEvent` - Name change
- `UserEmailChangedEvent` - Email change
- `UserActivatedEvent` - Activation
- `UserDeactivatedEvent` - Deactivation

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for MongoDB)

## Quick Start

### 1. Start MongoDB with Docker

```bash
cd examples
docker-compose up -d
```

This starts a MongoDB container on port 27017.

### 2. Run the API

```bash
cd examples/EventSourcing.Example.Api
dotnet run
```

The API will be available at `https://localhost:5001` (or the port shown in the console).

### 3. Access Swagger UI

Open your browser and navigate to: `https://localhost:5001/swagger`

## API Endpoints

### Create a User

```http
POST /api/users
Content-Type: application/json

{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "deactivationReason": null,
  "version": 1
}
```

### Get a User

```http
GET /api/users/{id}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "deactivationReason": null,
  "version": 1
}
```

### Update User Name

```http
PUT /api/users/{id}/name
Content-Type: application/json

{
  "firstName": "Jane",
  "lastName": "Doe"
}
```

### Update User Email

```http
PUT /api/users/{id}/email
Content-Type: application/json

{
  "email": "jane.doe@example.com"
}
```

### Activate a User

```http
POST /api/users/{id}/activate
```

### Deactivate a User

```http
POST /api/users/{id}/deactivate
Content-Type: application/json

{
  "reason": "Account suspended for policy violation"
}
```

### Get User Event History

```http
GET /api/users/{id}/events
```

**Response (200 OK):**
```json
[
  {
    "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "eventType": "UserCreatedEvent",
    "timestamp": "2024-01-15T10:30:00Z",
    "data": {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "john.doe@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "eventId": "...",
      "timestamp": "2024-01-15T10:30:00Z",
      "eventType": "UserCreatedEvent"
    }
  },
  {
    "eventId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "eventType": "UserNameChangedEvent",
    "timestamp": "2024-01-15T11:00:00Z",
    "data": {
      "firstName": "Jane",
      "lastName": "Doe",
      "eventId": "...",
      "timestamp": "2024-01-15T11:00:00Z",
      "eventType": "UserNameChangedEvent"
    }
  }
]
```

**Useful for:**
- Audit trail for a specific user
- View complete modification history
- Debug a specific case

### Get All Events (All Users)

```http
GET /api/events/users
```

**Useful for:**
- Complete audit trail
- Event replay
- Building projections
- Historical analysis

### Get Events Since a Date

```http
GET /api/events/users/since?since=2024-01-15T00:00:00Z
```

**Useful for:**
- Incremental processing
- Updating projections
- System synchronization

## Testing with cURL

### Create a User

```bash
curl -X POST https://localhost:5001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

### Get a User

```bash
curl https://localhost:5001/api/users/{user-id}
```

### Update Name

```bash
curl -X PUT https://localhost:5001/api/users/{user-id}/name \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "lastName": "Smith"
  }'
```

## Verify Events in MongoDB

```bash
# Connect to MongoDB
docker exec -it eventsourcing-mongodb mongosh

# Use the database
use EventSourcingExample

# View all user events
db.useraggregate_events.find().pretty()

# View snapshots
db.useraggregate_snapshots.find().pretty()
```

## Project Structure

```
EventSourcing.Example.Api/
├── Controllers/
│   └── UsersController.cs      # REST API endpoints
├── Domain/
│   ├── UserAggregate.cs        # Aggregate with business logic
│   └── Events/                 # Domain events
│       ├── UserCreatedEvent.cs
│       ├── UserNameChangedEvent.cs
│       ├── UserEmailChangedEvent.cs
│       ├── UserActivatedEvent.cs
│       └── UserDeactivatedEvent.cs
├── Models/                     # DTOs for requests/responses
│   ├── CreateUserRequest.cs
│   ├── UpdateUserNameRequest.cs
│   ├── UpdateUserEmailRequest.cs
│   ├── DeactivateUserRequest.cs
│   └── UserResponse.cs
└── Program.cs                  # Event Sourcing configuration
```

## Event Sourcing Concepts Demonstrated

### 1. **Aggregate**
The `UserAggregate` encapsulates business logic and maintains consistency.

### 2. **Events**
Each action generates an immutable event stored in MongoDB:
- Collection: `useraggregate_events`
- Events are never modified or deleted

### 3. **State Reconstruction**
The current state is reconstructed by replaying all events from the beginning (or from the last snapshot).

### 4. **Snapshots**
For performance optimization, a snapshot is created every 10 events (configurable in `Program.cs`).

### 5. **Optimistic Concurrency**
The aggregate version number prevents concurrency conflicts.

## Advanced Configuration

### Modify Snapshot Frequency

In `Program.cs`:

```csharp
// Snapshot every 5 events
config.SnapshotEvery(5);

// Or time-based snapshot (every 5 minutes)
config.SnapshotEvery(TimeSpan.FromMinutes(5));

// Or custom strategy
config.SnapshotWhen((aggregate, eventCount, lastSnapshot) => {
    return eventCount >= 20 ||
           (lastSnapshot.HasValue && DateTime.UtcNow - lastSnapshot.Value > TimeSpan.FromHours(1));
});
```

### Add Projections

Projections allow you to create optimized views for reads (CQRS).

```csharp
// In Program.cs
config.AddProjection<UserListProjection>();
```

### Add External Publisher

To publish events to a message broker (RabbitMQ, Kafka, etc.):

```csharp
// In Program.cs
config.AddEventPublisher<RabbitMQPublisher>();
```

## Stop the Environment

```bash
# Stop MongoDB
docker-compose down

# Remove data (warning: data loss!)
docker-compose down -v
```

## Troubleshooting

### MongoDB Connection Error

Verify that MongoDB is running:
```bash
docker ps
```

You should see an `eventsourcing-mongodb` container running.

### Port Already in Use

If port 27017 is already in use, modify `docker-compose.yml`:
```yaml
ports:
  - "27018:27017"  # Use port 27018 locally
```

And update `appsettings.json`:
```json
"ConnectionStrings": {
  "MongoDB": "mongodb://localhost:27018"
}
```

## Learn More

- [Event Sourcing Pattern](https://martinfowler.com/eaaDev/EventSourcing.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Domain-Driven Design](https://www.domainlanguage.com/ddd/)

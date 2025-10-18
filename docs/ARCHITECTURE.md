# Architecture Complète : Event Sourcing + State Machines + MediatR

## Vue d'Ensemble

Cette bibliothèque combine trois patterns puissants :

1. **Event Sourcing** : Stocke tous les changements comme événements immuables
2. **State Machines** : Garantit des transitions d'état valides
3. **MediatR** : Pattern CQRS avec séparation commandes/queries

## Architecture Visuelle

```
┌────────────────────────────────────────────────────────────────────┐
│                          API Layer (HTTP)                          │
│  Controllers exposent des endpoints REST pour Commands & Queries  │
└────────────────────┬───────────────────────────────────────────────┘
                     │
                     │ Send(command) / Send(query)
                     ▼
┌────────────────────────────────────────────────────────────────────┐
│                          MediatR Layer                             │
│              Routes Commands/Queries to Handlers                   │
└────────────────────┬───────────────────────────────────────────────┘
                     │
         ┌───────────┴───────────┐
         │                       │
         ▼                       ▼
┌─────────────────┐    ┌──────────────────┐
│ Command Handler │    │  Query Handler   │
│  (Write Side)   │    │   (Read Side)    │
└────────┬────────┘    └────────┬─────────┘
         │                      │
         │ Load/Save            │ Load
         ▼                      ▼
┌────────────────────────────────────────────┐
│      Aggregate Repository                  │
│  - GetByIdAsync (loads from events)        │
│  - SaveAsync (appends new events)          │
└────────────┬───────────────────────────────┘
             │
             │ Uses
             ▼
┌────────────────────────────────────────────┐
│        Domain Aggregates                   │
│  ┌──────────────────────────────────┐      │
│  │   Business Logic (Commands)      │      │
│  │   - CreateOrder()                │      │
│  │   - Ship()                       │      │
│  │   - Cancel()                     │      │
│  └──────────┬───────────────────────┘      │
│             │ Raises Events                 │
│             ▼                               │
│  ┌──────────────────────────────────┐      │
│  │   Event Handlers (Apply)         │      │
│  │   - Apply(OrderCreated)          │      │
│  │   - Apply(OrderShipped)          │      │
│  └──────────┬───────────────────────┘      │
│             │ Updates State via             │
│             ▼                               │
│  ┌──────────────────────────────────┐      │
│  │    State Machine                 │      │
│  │  - Validates transitions          │      │
│  │  - Publishes notifications        │      │
│  │  - Tracks current/previous state  │      │
│  └──────────────────────────────────┘      │
└────────────┬───────────────────────────────┘
             │
             │ Persists
             ▼
┌────────────────────────────────────────────┐
│         Storage Layer (MongoDB)            │
│  ┌───────────────┐   ┌─────────────────┐  │
│  │  Event Store  │   │ Snapshot Store  │  │
│  │  - Events     │   │  - Snapshots    │  │
│  │  - Append only│   │  - Point-in-time│  │
│  └───────────────┘   └─────────────────┘  │
└────────────────────────────────────────────┘
             │
             │ Publishes
             ▼
┌────────────────────────────────────────────┐
│       Notification Handlers                │
│  - OrderShippedNotificationHandler         │
│    → Send email                            │
│  - OrderCancelledRefundHandler             │
│    → Process refund                        │
│  - OrderStateAnalyticsHandler              │
│    → Track metrics                         │
└────────────────────────────────────────────┘
```

## Flux de Données

### Commande (Write) : Ship an Order

```
1. HTTP Request
   POST /api/orders/123/ship
   Body: { "shippingAddress": "...", "trackingNumber": "..." }

2. Controller
   var command = new ShipOrderCommand(orderId, address, tracking);
   await _mediator.Send(command);

3. MediatR
   Routes to ShipOrderCommandHandler

4. Command Handler
   - var order = await _repository.GetByIdAsync(orderId);
   - order.Ship(address, tracking);  ← Business logic
   - await _repository.SaveAsync(order);

5. Repository (GetByIdAsync)
   - Load latest snapshot (if exists)
   - Load events since snapshot
   - Replay events to reconstruct state

6. Aggregate (Ship method)
   - Validate business rules
   - Check if Items.Count > 0
   - State machine validates transition Pending → Shipped
   - RaiseEvent(new OrderShippedEvent(...))

7. Repository (SaveAsync)
   - Append events to Event Store
   - Publish events to Event Bus
   - Check if snapshot needed
   - Create snapshot if threshold reached

8. State Machine (TransitionToAsync)
   - Execute OnExit hooks (Pending state)
   - Change state to Shipped
   - Execute OnEnter hooks (Shipped state)
   - Publish StateTransitionNotification via MediatR

9. Notification Handlers (Async)
   - OrderShippedNotificationHandler
     → Send confirmation email
     → Update tracking system
   - OrderStateAnalyticsHandler
     → Log to analytics platform

10. HTTP Response
    200 OK
    { "aggregateId": "123", "version": 5 }
```

### Query (Read) : Get Order

```
1. HTTP Request
   GET /api/orders/123

2. Controller
   var query = new GetOrderQuery(orderId);
   var result = await _mediator.Send(query);

3. MediatR
   Routes to GetOrderQueryHandler

4. Query Handler
   - var order = await _repository.GetByIdAsync(orderId);
   - Map to DTO: new OrderDto(...)
   - return dto;

5. Repository (GetByIdAsync)
   - Load from snapshot + events (same as command)

6. HTTP Response
   200 OK
   {
     "id": "123",
     "status": "Shipped",
     "total": 99.99,
     "items": [...]
   }
```

## Composants Clés

### 1. Commands (EventSourcing.Core/CQRS/Commands.cs)

```csharp
public abstract record Command<TResult> : IRequest<TResult>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
```

**Responsabilité** : Exprime l'**intention** de changer l'état

### 2. Queries (EventSourcing.Core/CQRS/Queries.cs)

```csharp
public abstract record Query<TResult> : IRequest<TResult>
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
}
```

**Responsabilité** : Récupère des données **sans** modifier l'état

### 3. Aggregates (EventSourcing.Core/AggregateBase.cs)

```csharp
public abstract class AggregateBase<TId> : IAggregate<TId>
{
    public void RaiseEvent(IEvent @event);
    public void LoadFromHistory(IEnumerable<IEvent> events);
}
```

**Responsabilité** :
- Logique métier
- Validation des invariants
- Émission d'événements

### 4. State Machine (EventSourcing.Core/StateMachine/StateMachine.cs)

```csharp
public class StateMachine<TState>
{
    public void TransitionTo(TState newState);
    public bool CanTransitionTo(TState newState);
    public IEnumerable<TState> GetAllowedTransitions();
}
```

**Responsabilité** :
- Valide les transitions
- Exécute les hooks OnEnter/OnExit
- Publie des notifications (avec MediatR)

### 5. Event Store (EventSourcing.MongoDB/MongoEventStore.cs)

```csharp
public interface IEventStore
{
    Task AppendEventsAsync(...);
    Task<IEnumerable<IEvent>> GetEventsAsync(...);
}
```

**Responsabilité** :
- Stockage append-only des événements
- Récupération des événements par aggregate
- Optimistic concurrency control

### 6. Snapshot Store (EventSourcing.MongoDB/MongoSnapshotStore.cs)

```csharp
public interface ISnapshotStore
{
    Task SaveSnapshotAsync<TId, TAggregate>(...);
    Task<Snapshot<TAggregate>?> GetLatestSnapshotAsync<TId, TAggregate>(...);
}
```

**Responsabilité** :
- Stockage de snapshots pour performance
- Récupération du dernier snapshot
- Évite de rejouer tous les événements

### 7. Repository (EventSourcing.Core/AggregateRepository.cs)

```csharp
public class AggregateRepository<TAggregate, TId> : IAggregateRepository<TAggregate, TId>
{
    public async Task<TAggregate> GetByIdAsync(TId aggregateId);
    public async Task SaveAsync(TAggregate aggregate);
}
```

**Responsabilité** :
- Orchestre Event Store + Snapshot Store
- Hydrate les aggregates (snapshot + events)
- Gère la création de snapshots

## Patterns et Principes

### CQRS (Command Query Responsibility Segregation)

```
┌─────────────────────────────────┐
│        Write Side               │
│  Commands → Handlers → Aggregate│
│  Focus: Consistency, Validation │
└─────────────────────────────────┘

┌─────────────────────────────────┐
│        Read Side                │
│  Queries → Handlers → DTOs      │
│  Focus: Performance, Projections│
└─────────────────────────────────┘
```

### Event Sourcing

```
État Actuel = f(Tous les Événements)

Events:
1. OrderCreated
2. ItemAdded
3. ItemAdded
4. OrderShipped

Replay → État final : Order { Status: Shipped, Items: 2 }
```

### State Machine

```
        ┌─────────┐
        │ Pending │
        └────┬────┘
             │
    ┌────────┴────────┐
    │                 │
    ▼                 ▼
┌─────────┐      ┌───────────┐
│ Shipped │      │ Cancelled │
└─────────┘      └───────────┘

Transitions autorisées définies explicitement
```

### Mediator Pattern (MediatR)

```
Request (Command/Query)
    ↓
Mediator (routes)
    ↓
Handler (execute)
    ↓
Response
```

## Avantages de l'Architecture

### 1. **Audit Trail Complet**
Tous les changements sont enregistrés comme événements

### 2. **Time Travel**
Replay des événements pour voir l'état à n'importe quel moment

### 3. **Validation Robuste**
State machines empêchent les transitions invalides

### 4. **Séparation des Responsabilités**
- Commands : Écriture
- Queries : Lecture
- Aggregates : Logique métier
- Handlers : Orchestration
- Notifications : Side effects

### 5. **Scalabilité**
- Read/Write peuvent scaler indépendamment
- Event Store peut être partitionné
- Notifications asynchrones

### 6. **Testabilité**
Chaque composant est testable indépendamment

### 7. **UI Dynamique**
Query pour savoir quelles actions sont possibles (basé sur state machine)

## Collections MongoDB

```
Database: EventSourcingDb

Collections:
- useraggregate_events
  { aggregateId, version, eventType, data, timestamp }

- useraggregate_snapshots
  { aggregateId, version, data, timestamp }

- orderaggregate_events
  { aggregateId, version, eventType, data, timestamp }

- orderaggregate_snapshots
  { aggregateId, version, data, timestamp }

- sagas
  { sagaId, sagaName, data, status, currentStepIndex }
```

## Performance

### Snapshot Strategy

```
Sans snapshots:
  Load Order avec 10,000 événements = ~2 secondes

Avec snapshots (tous les 100 événements):
  Load snapshot (version 9900) + 100 événements = ~50ms
```

### CQRS Read Models

Pour améliorer encore la performance :

```
Write Side: Aggregate → Events
Read Side: Projections → Read Models optimisés

Exemples:
- OrderSummaryProjection (pour listes)
- OrderDetailsProjection (pour détails)
- CustomerOrdersProjection (par customer)
```

## Prochaines Étapes

1. **Projections** : Créer des read models optimisés
2. **Sagas** : Orchestrer des workflows multi-aggregates
3. **Event Versioning** : Gérer l'évolution du schéma
4. **Outbox Pattern** : Garantir la publication d'événements
5. **Process Managers** : Workflows complexes

## Documentation

- [Quick Start MediatR](./MEDIATR_QUICKSTART.md)
- [MediatR Integration Complète](./MEDIATR_INTEGRATION.md)
- [State Machines](./STATE_MACHINES.md)
- [CLAUDE.md](../CLAUDE.md) - Vue d'ensemble du projet

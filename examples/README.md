# Event Sourcing Example API

Cette API d'exemple démontre l'utilisation du package Event Sourcing pour gérer des utilisateurs avec un pattern CQRS et Event Sourcing.

## Architecture

L'exemple implémente un agrégat `UserAggregate` avec les événements suivants :

- `UserCreatedEvent` - Création d'un utilisateur
- `UserNameChangedEvent` - Changement de nom
- `UserEmailChangedEvent` - Changement d'email
- `UserActivatedEvent` - Activation
- `UserDeactivatedEvent` - Désactivation

## Prérequis

- .NET 9.0 SDK
- Docker et Docker Compose (pour MongoDB)

## Démarrage rapide

### 1. Démarrer MongoDB avec Docker

```bash
cd examples
docker-compose up -d
```

Cela démarre un conteneur MongoDB sur le port 27017.

### 2. Lancer l'API

```bash
cd examples/EventSourcing.Example.Api
dotnet run
```

L'API sera disponible sur `https://localhost:5001` (ou le port indiqué dans la console).

### 3. Accéder à Swagger UI

Ouvrez votre navigateur et accédez à : `https://localhost:5001/swagger`

## Endpoints API

### Créer un utilisateur

```http
POST /api/users
Content-Type: application/json

{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Réponse (201 Created):**
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

### Obtenir un utilisateur

```http
GET /api/users/{id}
```

**Réponse (200 OK):**
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

### Modifier le nom

```http
PUT /api/users/{id}/name
Content-Type: application/json

{
  "firstName": "Jane",
  "lastName": "Doe"
}
```

### Modifier l'email

```http
PUT /api/users/{id}/email
Content-Type: application/json

{
  "email": "jane.doe@example.com"
}
```

### Activer un utilisateur

```http
POST /api/users/{id}/activate
```

### Désactiver un utilisateur

```http
POST /api/users/{id}/deactivate
Content-Type: application/json

{
  "reason": "Account suspended for policy violation"
}
```

### Récupérer l'historique des événements d'un utilisateur

```http
GET /api/users/{id}/events
```

**Réponse (200 OK):**
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

**Utile pour :**
- Audit trail d'un utilisateur spécifique
- Voir l'historique complet des modifications
- Debug d'un cas spécifique

### Récupérer tous les événements (tous les utilisateurs)

```http
GET /api/events/users
```

**Utile pour :**
- Audit trail complet
- Event replay
- Construction de projections
- Analyse historique

### Récupérer les événements depuis une date

```http
GET /api/events/users/since?since=2024-01-15T00:00:00Z
```

**Utile pour :**
- Traitement incrémental
- Mise à jour de projections
- Synchronisation de systèmes

## Test avec cURL

### Créer un utilisateur

```bash
curl -X POST https://localhost:5001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

### Obtenir un utilisateur

```bash
curl https://localhost:5001/api/users/{user-id}
```

### Modifier le nom

```bash
curl -X PUT https://localhost:5001/api/users/{user-id}/name \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "lastName": "Smith"
  }'
```

## Vérifier les événements dans MongoDB

```bash
# Se connecter à MongoDB
docker exec -it eventsourcing-mongodb mongosh

# Utiliser la base de données
use EventSourcingExample

# Voir tous les événements d'un utilisateur
db.useraggregate_events.find().pretty()

# Voir les snapshots
db.useraggregate_snapshots.find().pretty()
```

## Structure du projet

```
EventSourcing.Example.Api/
├── Controllers/
│   └── UsersController.cs      # Endpoints API REST
├── Domain/
│   ├── UserAggregate.cs        # Agrégat avec logique métier
│   └── Events/                 # Événements du domaine
│       ├── UserCreatedEvent.cs
│       ├── UserNameChangedEvent.cs
│       ├── UserEmailChangedEvent.cs
│       ├── UserActivatedEvent.cs
│       └── UserDeactivatedEvent.cs
├── Models/                     # DTOs pour les requêtes/réponses
│   ├── CreateUserRequest.cs
│   ├── UpdateUserNameRequest.cs
│   ├── UpdateUserEmailRequest.cs
│   ├── DeactivateUserRequest.cs
│   └── UserResponse.cs
└── Program.cs                  # Configuration Event Sourcing
```

## Concepts Event Sourcing démontrés

### 1. **Agrégat**
L'agrégat `UserAggregate` encapsule la logique métier et maintient la cohérence.

### 2. **Événements**
Chaque action génère un événement immutable stocké dans MongoDB :
- Collection : `useraggregate_events`
- Les événements ne sont jamais modifiés ou supprimés

### 3. **Reconstruction d'état**
L'état actuel est reconstruit en rejouant tous les événements depuis le début (ou depuis le dernier snapshot).

### 4. **Snapshots**
Pour optimiser les performances, un snapshot est créé tous les 10 événements (configurable dans `Program.cs`).

### 5. **Concurrence optimiste**
Le numéro de version de l'agrégat empêche les conflits de concurrence.

## Configuration avancée

### Modifier la fréquence des snapshots

Dans `Program.cs`:

```csharp
// Snapshot tous les 5 événements
config.SnapshotEvery(5);

// Ou snapshot basé sur le temps (toutes les 5 minutes)
config.SnapshotEvery(TimeSpan.FromMinutes(5));

// Ou stratégie personnalisée
config.SnapshotWhen((aggregate, eventCount, lastSnapshot) => {
    return eventCount >= 20 ||
           (lastSnapshot.HasValue && DateTime.UtcNow - lastSnapshot.Value > TimeSpan.FromHours(1));
});
```

### Ajouter des projections

Les projections permettent de créer des vues optimisées pour les lectures (CQRS).

```csharp
// Dans Program.cs
config.AddProjection<UserListProjection>();
```

### Ajouter un publisher externe

Pour publier les événements vers un message broker (RabbitMQ, Kafka, etc.):

```csharp
// Dans Program.cs
config.AddEventPublisher<RabbitMQPublisher>();
```

## Arrêter l'environnement

```bash
# Arrêter MongoDB
docker-compose down

# Supprimer les données (attention : perte de données !)
docker-compose down -v
```

## Troubleshooting

### Erreur de connexion MongoDB

Vérifiez que MongoDB est bien démarré :
```bash
docker ps
```

Vous devriez voir un conteneur `eventsourcing-mongodb` en cours d'exécution.

### Port déjà utilisé

Si le port 27017 est déjà utilisé, modifiez le `docker-compose.yml` :
```yaml
ports:
  - "27018:27017"  # Utiliser le port 27018 localement
```

Et mettez à jour `appsettings.json` :
```json
"ConnectionStrings": {
  "MongoDB": "mongodb://localhost:27018"
}
```

## En savoir plus

- [Event Sourcing Pattern](https://martinfowler.com/eaaDev/EventSourcing.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Domain-Driven Design](https://www.domainlanguage.com/ddd/)

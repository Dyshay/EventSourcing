# HTTP Test Cases for Event Sourcing API

This file contains comprehensive HTTP test cases for the Event Sourcing Example API using the `.http` file format.

## Prerequisites

### Using VS Code
1. Install the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
2. Open `EventSourcing.Example.Api.http`
3. Click "Send Request" above any request

### Using JetBrains Rider / IntelliJ
1. Built-in support - no extension needed
2. Open `EventSourcing.Example.Api.http`
3. Click the green play button next to any request

### Using Visual Studio 2022+
1. Built-in support since VS 2022
2. Open `EventSourcing.Example.Api.http`
3. Click "Send Request" button

## Running the API

Before running tests, start the API:

```bash
cd examples/EventSourcing.Example.Api
dotnet run
```

The API will start on `http://localhost:5147` (configured in the .http file).

## API Endpoints

### User Management

| Method | Endpoint | Body | Description |
|--------|----------|------|-------------|
| POST | `/api/users` | `{ email, firstName, lastName }` | Create new user |
| GET | `/api/users/{id}` | - | Get user by ID |
| PUT | `/api/users/{id}/name` | `{ firstName, lastName }` | Update user name |
| PUT | `/api/users/{id}/email` | `{ email }` | Update user email |
| POST | `/api/users/{id}/activate` | - | Activate user |
| POST | `/api/users/{id}/deactivate` | `{ reason }` | Deactivate user with reason |
| GET | `/api/users/{id}/events` | - | Get user event history |

### Event Queries

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/users` | Get all user events |
| GET | `/api/events/users/since?since={timestamp}` | Get events since timestamp |
| GET | `/api/events/users/kind/{kind}` | Filter by single event kind |
| GET | `/api/events/users/kinds?kinds={kinds}` | Filter by multiple kinds |

## Request/Response Models

### CreateUserRequest
```json
{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

### UpdateUserNameRequest
```json
{
  "firstName": "John",
  "lastName": "Updated"
}
```

### UpdateUserEmailRequest
```json
{
  "email": "newemail@example.com"
}
```

### DeactivateUserRequest
```json
{
  "reason": "Account suspended"
}
```

### UserResponse
```json
{
  "id": "guid",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "deactivationReason": null,
  "version": 5
}
```

## Test Categories

### 1. User Management (Lines 8-78)
Basic CRUD operations:
- ✅ Create users (POST /api/users)
- ✅ Get user by ID (GET /api/users/{id})
- ✅ Update name (PUT /api/users/{id}/name)
- ✅ Update email (PUT /api/users/{id}/email)
- ✅ Activate user (POST /api/users/{id}/activate)
- ✅ Deactivate user (POST /api/users/{id}/deactivate)
- ✅ 404 handling for non-existent users

**Usage:**
1. Run "Create a new user" first
2. The `@userId` variable will auto-capture the returned ID
3. Run other operations using that ID

### 2. Event History - User Specific (Lines 80-86)
Get event history for a specific user:
- ✅ GET /api/users/{id}/events

Shows the complete event stream for one user (created, namechanged, emailchanged, activated, deactivated).

### 3. Event Queries - Global (Lines 88-104)
Query events across all users:
- ✅ Get all events (GET /api/events/users)
- ✅ Get events since timestamp (GET /api/events/users/since?since={timestamp})
- ✅ Get events from last hour

**Use Case:** Building projections or audit trails.

### 4. Event Filtering by Kind (Lines 106-136)
Filter events by event kind:
- ✅ Get "user.created" events only
- ✅ Get "user.namechanged" events only
- ✅ Get "user.emailchanged" events only
- ✅ Get "user.activated" events only
- ✅ Get "user.deactivated" events only
- ✅ Get multiple kinds at once (e.g., created + namechanged)

**Use Case:** Analytics, reporting, or selective event replay.

### 5. Validation Tests (Lines 138-205)
Test error handling:
- ❌ Empty email (should fail)
- ❌ Empty first name (should fail)
- ❌ Empty last name (should fail)
- ❌ Invalid email format (should fail)
- ❌ Empty new name in update (should fail)
- ❌ Empty new email in update (should fail)
- ❌ Empty deactivation reason (should fail)

**Expected:** HTTP 400 Bad Request responses.

### 6. Bulk Testing (Lines 207-263)
Create multiple users quickly:
- ✅ Creates 5 users (Alice, Charlie, David, Emma, Frank)

**Use Case:** Populate test data for event queries.

### 7. Complete User Lifecycle Test (Lines 265-327)
End-to-end test following a user through their entire lifecycle:
1. Create user
2. Get user (verify creation)
3. Update user name
4. Change email
5. Deactivate user with reason
6. Get events (verify 4 events)
7. Verify user state (should be inactive)
8. Reactivate user
9. Verify user is active
10. Get final events (should have 5 events total)

**Run sequentially** - each step depends on the previous one.

### 8. Performance Testing (Lines 329-396)
Test rapid operations and event accumulation:
- Create 1 user
- Perform 5 rapid name changes
- Verify 6 total events (1 created + 5 namechanged)
- Verify final state matches last change

**Use Case:** Test snapshot behavior (configured to snapshot every 10 events).

### 9. Activation / Deactivation Flow (Lines 398-439)
Test user activation lifecycle:
- Create user (starts active by default)
- Deactivate with reason
- Verify deactivated state
- Reactivate
- Deactivate again with different reason
- View activation history

### 10. Swagger/OpenAPI (Lines 441-450)
Access API documentation:
- ✅ Swagger UI (interactive docs)
- ✅ OpenAPI JSON schema

## Variables

The .http file uses variables for easy testing:

```http
@baseUrl = http://localhost:5147
@userId = {{createUser.response.body.$.id}}
@lifecycleUserId = {{lifecycleUser.response.body.$.id}}
@perfUserId = {{perfUser.response.body.$.id}}
@activationUserId = {{activationUser.response.body.$.id}}
@oneHourAgo = {{$datetime rfc1123 -1 h}}
```

### Named Requests
Requests with `# @name variableName` can be referenced by other requests:

```http
# @name createUser
POST {{baseUrl}}/api/users
...

# Later, use the response
@userId = {{createUser.response.body.$.id}}
GET {{baseUrl}}/api/users/{{userId}}
```

## Testing Workflow

### Quick Start
1. Run "Create a new user" → Get ID
2. Run "Get user by ID" → Verify
3. Run "Update user name" → Update name
4. Run "Get user by ID" → Verify name changed
5. Run "Get all events for a specific user" → See event history

### Full Test Suite
1. **User Management** - Create 3 users
2. **Bulk Testing** - Create 5 more users (8 total)
3. **Event Queries** - Query all events (should have 8 created events)
4. **Event Filtering** - Filter by "user.created" (should return 8)
5. **Lifecycle Test** - Run all 10 steps sequentially
6. **Performance Test** - Run all performance tests
7. **Activation Flow** - Test activation/deactivation cycles
8. **Event Queries** - Verify total event count

### Event Sourcing Concepts to Observe

**1. Complete Event History**
```http
GET {{baseUrl}}/api/users/{{userId}}/events
```
Shows every state change as an immutable event.

**2. State Reconstruction**
```http
GET {{baseUrl}}/api/users/{{userId}}
```
Current state is rebuilt by replaying all events.

**3. Audit Trail**
```http
GET {{baseUrl}}/api/events/users
```
Perfect audit log - events are never deleted, only users are deactivated.

**4. Event Kinds for Analytics**
```http
GET {{baseUrl}}/api/events/users/kinds?kinds=user.created,user.deactivated
```
Analyze patterns (e.g., "How many users were deactivated this month?").

**5. Time Travel**
```http
GET {{baseUrl}}/api/events/users/since?since=2024-01-01T00:00:00Z
```
Reconstruct state at any point in time.

## Expected Event Kinds

Based on the User aggregate, you'll see these event kinds:

- `user.created` - New user created
- `user.namechanged` - User name changed
- `user.emailchanged` - User email changed
- `user.activated` - User activated
- `user.deactivated` - User deactivated with reason

## MongoDB Verification

While the API is running, you can inspect MongoDB directly:

```bash
mongosh

use EventSourcingExample

# View all events
db.useraggregate_events.find().pretty()

# View snapshots (created every 10 events)
db.useraggregate_snapshots.find().pretty()

# Count events by kind
db.useraggregate_events.aggregate([
  { $group: { _id: "$kind", count: { $sum: 1 } } }
])

# View user activation/deactivation events
db.useraggregate_events.find({
  kind: { $in: ["user.activated", "user.deactivated"] }
}).pretty()
```

## Important Differences from Typical CRUD

### No DELETE endpoint
This API uses **soft deletion** via deactivation:
- ❌ `DELETE /api/users/{id}` does NOT exist
- ✅ Use `POST /api/users/{id}/deactivate` instead with a reason

Events are NEVER deleted - this is fundamental to event sourcing.

### Activation State
Users have an `isActive` boolean:
- Created users start as `active` by default
- `Deactivate` sets `isActive = false` and records a reason
- `Activate` sets `isActive = true` and clears the reason
- All state changes create events for the audit trail

### Version for Concurrency
The `version` field in UserResponse tracks:
- Event count for this aggregate
- Used for optimistic concurrency control
- Increments with each event (created, namechanged, etc.)

## Troubleshooting

### "Connection refused"
- Ensure the API is running: `dotnet run`
- Check the port matches: `http://localhost:5147`

### "404 Not Found"
- Verify the endpoint exists in Swagger UI
- Check if the user ID is valid
- Remember: There is NO DELETE endpoint

### Variables not working
- Ensure you run named requests first (e.g., `# @name createUser`)
- VS Code: May need to install REST Client extension
- Rider/VS: Built-in support

### MongoDB Connection Issues
- Ensure MongoDB is running on `localhost:27017`
- Check connection string in `appsettings.json`

### "User already deactivated" or similar
- Check user state with GET /api/users/{id}
- Review the event history to see current state
- Remember: Deactivation is idempotent (can deactivate multiple times)

## Tips

1. **Run sequentially**: The "Complete User Lifecycle Test" section should be run in order
2. **Use variables**: Named requests auto-populate IDs for you
3. **Check responses**: Look at event counts, timestamps, and kinds
4. **Compare states**: Get user before and after operations to see state changes
5. **Explore Swagger**: Use Swagger UI for interactive testing with schema validation
6. **Deactivation vs Deletion**: Use deactivate instead of delete to maintain audit trail

## Next Steps

After testing the API:
1. Build a read model / projection using the event queries
2. Implement CQRS with separate read/write models
3. Add more aggregate types (Orders, Products, etc.)
4. Implement event handlers for side effects (send emails, notifications)
5. Add event versioning for schema evolution
6. Implement sagas for long-running workflows

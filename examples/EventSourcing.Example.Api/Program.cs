using EventSourcing.Example.Api.Domain;
using EventSourcing.Example.Api.Infrastructure;
using EventSourcing.MongoDB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EventSourcing Example API", Version = "v1" });
});

// Add MediatR for CQRS and reactive workflows
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Configure Event Sourcing with MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration.GetValue<string>("MongoDB:DatabaseName") ?? "EventSourcingExample";

builder.Services.AddEventSourcing(config =>
{
    config.UseMongoDB(mongoConnectionString, mongoDatabaseName)
          .RegisterEventsFromAssembly(typeof(Program).Assembly) // Register all event types from this assembly
          .InitializeMongoDB("UserAggregate", "OrderAggregate"); // Initialize indexes for both aggregates

    // Snapshot every 10 events
    config.SnapshotEvery(10);

    // Optional: Add projections
    // config.AddProjection<UserListProjection>();

    // Infrastructure bridge: Convert domain events → MediatR notifications
    config.AddEventPublisher<MediatREventPublisher>();
});

// Register aggregate repositories
builder.Services.AddAggregateRepository<UserAggregate, Guid>();
builder.Services.AddAggregateRepository<OrderAggregate, Guid>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

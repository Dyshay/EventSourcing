using EventSourcing.CQRS.Configuration;
using EventSourcing.CQRS.DependencyInjection;
using EventSourcing.Example.Api.Application.Cqrs.Commands;
using EventSourcing.Example.Api.Application.Cqrs.Validators;
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
    c.SwaggerDoc("v2", new() { Title = "EventSourcing Example API (CQRS)", Version = "v2" });
});

// Add MediatR for CQRS and reactive workflows (Original approach - still supported)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Add new CQRS Framework (Alternative to MediatR)
//
// PERFORMANCE TUNING OPTIONS:
//
// Option 1: Default Configuration (All features enabled)
// - Audit trail tracking: ON
// - Logging: ON
// - Query caching: ON
// Best for: Development, debugging, and production with full observability
//
// Option 2: High Performance Configuration
// - Audit trail tracking: OFF (saves ~150-200 ns per command)
// - Logging: OFF (saves ~50-100 ns per operation)
// - Query caching: ON
// Best for: High-throughput scenarios, microservices, APIs
//
// Option 3: Custom Configuration
// - Fine-tune each feature individually
//

// EXAMPLE: Default configuration (recommended for most cases)
builder.Services.AddCqrs(
    cqrs =>
    {
        // Register all handlers from the assembly
        cqrs.AddHandlersFromAssembly(typeof(Program).Assembly)
            // Enable query caching
            .WithQueryCaching()
            // Enable command logging
            .WithCommandLogging()
            // Enable performance metrics
            .WithCommandMetrics()
            // Enable command validation
            .WithCommandValidation()
            // Enable retry with exponential backoff
            .WithCommandRetry(maxRetries: 3, retryDelay: TimeSpan.FromMilliseconds(100));

        // Register validators
        cqrs.AddCommandValidator<CreateOrderCqrsCommand, CreateOrderCqrsCommandValidator>()
            .AddCommandValidator<AddOrderItemCqrsCommand, AddOrderItemCqrsCommandValidator>()
            .AddCommandValidator<ShipOrderCqrsCommand, ShipOrderCqrsCommandValidator>()
            .AddCommandValidator<CancelOrderCqrsCommand, CancelOrderCqrsCommandValidator>();

        // Optional: Configure RabbitMQ event streaming
        // Uncomment to enable RabbitMQ event publishing
        /*
        cqrs.WithRabbitMQEventStreaming(options =>
        {
            options.HostName = "localhost";
            options.Port = 5672;
            options.UserName = "guest";
            options.Password = "guest";
            options.ExchangeName = "events";
        });
        */
    },
    options: CqrsOptions.Default() // Use default configuration
);

// ALTERNATIVE CONFIGURATIONS (uncomment to use):
//
// High Performance Mode (35-40% faster):
// options: CqrsOptions.HighPerformance()
//
// Custom Configuration:
// options: CqrsOptions.Custom(
//     enableAuditTrail: true,    // Keep audit trail for compliance
//     enableLogging: false,       // Disable logging for performance
//     enableQueryCache: true      // Keep cache for read performance
// )

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

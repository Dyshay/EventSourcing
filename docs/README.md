# Event Sourcing Documentation

Complete guide to using the Event Sourcing library for .NET.

## 🚀 Quick Start Guides

Start here if you're new to the library:

### [MediatR Quick Start](MEDIATR_QUICKSTART.md)
Get started with CQRS using MediatR in just a few minutes. Learn how to:
- Define Commands and Queries
- Create Handlers
- React to state changes with notifications
- Build dynamic UIs

**Start with this if you're building a CQRS application.**

### [State Machines](STATE_MACHINES.md)
Learn how to implement state machines in your aggregates. Covers:
- Built-in `StateMachine<TState>` (recommended)
- Integration with MediatR
- Stateless library integration
- State Pattern approach
- Best practices and testing

**Start with this if you need to manage complex state transitions.**

## 📚 Comprehensive Guides

Deep dive into specific topics:

### [MediatR Integration](MEDIATR_INTEGRATION.md)
Complete guide to building CQRS applications with MediatR. Includes:
- Commands: Write operations
- Queries: Read operations
- Notification Handlers: Reactive workflows
- Complete architecture diagrams
- Advanced patterns (validation pipelines, idempotency)
- Real-world examples

**300+ lines of detailed documentation with code samples.**

### [Architecture Overview](ARCHITECTURE.md)
Comprehensive architecture documentation covering:
- Complete system architecture with diagrams
- Data flow for commands and queries
- All key components explained
- Patterns and principles (CQRS, Event Sourcing, State Machines)
- Performance considerations
- MongoDB collections and indexes

**Your reference guide for understanding how everything fits together.**

### [Event Versioning & Upcasting](EVENT_VERSIONING.md)
Evolve your event schemas over time without breaking existing data:
- Automatic event upcasting
- Version strategies
- Schema evolution patterns
- Migration guides

### [Creating Custom Providers](CUSTOM_PROVIDERS.md)
Build your own storage provider for any database:
- Provider pattern overview
- Step-by-step implementation guide
- SQL Server example
- Testing strategies

## 🛠️ Operations & Testing

Guides for running tests and deploying:

### [Testing Guide](TESTING.md)
Everything you need to run tests locally and in CI:
- Local MongoDB setup
- Running tests
- CI/CD integration
- GitHub Actions configuration

### [GitHub Secrets Configuration](GITHUB_SECRETS.md)
Configure secrets for CI/CD pipelines:
- MongoDB Atlas setup
- GitHub Actions secrets
- Environment variables

### [Release Process](../.github/RELEASE.md)
How to create and publish releases:
- Version tagging
- NuGet package publishing
- CI/CD automation

## 📖 API Documentation

### [Main README](../README.md)
The main documentation covering:
- Quick start
- Installation
- Core concepts
- API examples
- Best practices
- Troubleshooting

### [Example Application](../examples/EventSourcing.Example.Api/)
Complete working example with:
- User and Order aggregates
- REST API endpoints
- Saga examples
- HTTP test files

## 🎯 Documentation by Use Case

### I want to build a CQRS application
1. Start with [MediatR Quick Start](MEDIATR_QUICKSTART.md)
2. Read [MediatR Integration](MEDIATR_INTEGRATION.md) for advanced patterns
3. Review [Architecture Overview](ARCHITECTURE.md) to understand the full picture
4. Check [Example Application](../examples/EventSourcing.Example.Api/) for real code

### I need to manage complex state transitions
1. Start with [State Machines](STATE_MACHINES.md)
2. If using MediatR, see the integration section in [MediatR Integration](MEDIATR_INTEGRATION.md)
3. Look at `OrderAggregateWithStateMachine` in examples

### I'm evolving my event schema
1. Read [Event Versioning & Upcasting](EVENT_VERSIONING.md)
2. See examples in `tests/EventSourcing.Tests/EventVersioning/`

### I want to use a different database (not MongoDB)
1. Read [Creating Custom Providers](CUSTOM_PROVIDERS.md)
2. Study the MongoDB implementation as reference
3. Implement `IEventStore` and `ISnapshotStore`

### I'm setting up CI/CD
1. Start with [Testing Guide](TESTING.md)
2. Configure secrets with [GitHub Secrets Configuration](GITHUB_SECRETS.md)
3. Review workflows in `.github/workflows/`

## 📦 Package Structure

```
EventSourcing/
├── src/
│   ├── EventSourcing.Abstractions/    # Public interfaces
│   ├── EventSourcing.Core/            # Core implementation + CQRS + State Machines
│   └── EventSourcing.MongoDB/         # MongoDB storage provider
├── tests/
│   └── EventSourcing.Tests/           # 184 tests covering all features
├── examples/
│   └── EventSourcing.Example.Api/     # Complete working example
└── docs/                              # THIS DIRECTORY
    ├── README.md                      # This file
    ├── MEDIATR_QUICKSTART.md          # Quick start guide
    ├── MEDIATR_INTEGRATION.md         # Complete MediatR guide
    ├── STATE_MACHINES.md              # State machine documentation
    ├── ARCHITECTURE.md                # Architecture overview
    ├── EVENT_VERSIONING.md            # Event versioning guide
    ├── CUSTOM_PROVIDERS.md            # Provider implementation guide
    ├── TESTING.md                     # Testing guide
    └── GITHUB_SECRETS.md              # CI/CD secrets configuration
```

## 🆘 Getting Help

- **Questions?** Check the [Main README](../README.md) troubleshooting section
- **Issues?** [GitHub Issues](https://github.com/Dyshay/EventSourcing/issues)
- **Discussions?** [GitHub Discussions](https://github.com/Dyshay/EventSourcing/discussions)
- **Examples?** See `examples/EventSourcing.Example.Api/`

## 🔄 Recent Additions

### State Machines (Latest)
- Built-in `StateMachine<TState>` for managing state transitions
- `StateMachineWithMediatr<TState>` for reactive workflows
- Complete documentation with examples
- 14 comprehensive tests

### MediatR Integration (Latest)
- Full CQRS support with Commands and Queries
- Notification handlers for reactive workflows
- Base classes for easy implementation
- Comprehensive documentation

### Event Versioning
- Automatic event upcasting
- `IEventUpcaster` interface
- Support for schema evolution

### Saga Pattern
- Long-running process orchestration
- Automatic compensation on failure
- MongoDB persistence

---

**All documentation is in English and regularly updated.**

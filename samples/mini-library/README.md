# Mini Library Sample

A small demonstration C# project for testing **graphify-dotnet** knowledge graph extraction.

## Purpose

This sample demonstrates common architectural patterns that create interesting knowledge graphs:

- **Repository Pattern**: Generic `IRepository<T>` interface with concrete `UserRepository` implementation
- **Service Layer**: `UserService` orchestrates business logic and calls the repository
- **Dependency Injection**: `ServiceCollectionExtensions` provides DI registration methods
- **Entity Model**: Simple `User` entity with validation logic

## Structure

```
src/
├── IRepository.cs              # Generic repository interface
├── User.cs                     # User entity model
├── UserRepository.cs           # In-memory repository implementation
├── UserService.cs              # Service layer with business logic
└── ServiceCollectionExtensions.cs  # DI configuration
```

## How to Use with graphify-dotnet

From the repository root, run:

```bash
dotnet run --project src/Graphify.Cli/Graphify.Cli.csproj run samples/mini-library
```

This will:
1. Detect all `.cs` files in the sample
2. Extract entities, methods, and relationships using AST parsing
3. Build a knowledge graph showing the repository pattern structure
4. Generate multiple export formats (JSON, HTML, report, etc.)

## Expected Graph Structure

The knowledge graph should reveal:

- **Interfaces**: `IRepository<T>`
- **Classes**: `User`, `UserRepository`, `UserService`, `ServiceCollectionExtensions`
- **Relationships**:
  - `UserRepository` implements `IRepository<User>`
  - `UserService` depends on `IRepository<User>`
  - `ServiceCollectionExtensions` registers both repository and service
  - Method calls between service and repository
- **Communities**: Likely 2-3 communities (data layer, service layer, infrastructure)

## Key Patterns Demonstrated

1. **Interface/Implementation**: Abstract repository contract with concrete implementation
2. **Generic Constraints**: `IRepository<T> where T : class`
3. **Dependency Injection**: Constructor injection in `UserService`
4. **Extension Methods**: Fluent API for service registration
5. **Async/Await**: Task-based async patterns throughout

This is a realistic mini-codebase that mimics production patterns while being small enough to analyze quickly.

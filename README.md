# CassandraORM.NET

A comprehensive Object-Relational Mapping (ORM) library for Apache Cassandra in .NET, inspired by Entity Framework's functionality and design patterns. Build production-ready applications with Cassandra using familiar .NET patterns and powerful features.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/Czechu7/CassandraORM.NET)
[![NuGet](https://img.shields.io/badge/nuget-v1.0.1-blue)](https://www.nuget.org/packages/CassandraORM.NET/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## Installation

```bash
dotnet add package CassandraORM.NET
```
## 🚀 Features

### ✅ Core Functionality
- **Entity Framework-like API**: Familiar DbContext and DbSet patterns
- **Generic Entity Mapping**: C# class-based data models with attributes
- **Full CRUD Operations**: Complete Create, Read, Update, Delete support
- **Async/Await Support**: Modern asynchronous programming patterns
- **Change Tracking**: Automatic entity state management
- **Batch Operations**: Efficient bulk operations for better performance
- **Connection Management**: Robust connection handling and pooling

### ✅ Advanced Data Types
- **User-Defined Types (UDTs)**: Full support for custom data types
- **Collections**: Native List, Set, and Map type support
- **Complex Objects**: Nested UDTs and collection combinations
- **Type Safety**: Compile-time type checking for all operations

### ✅ Query & Schema
- **LINQ Support**: Write queries using familiar LINQ syntax
- **Async Enumeration**: IAsyncEnumerable support for streaming results
- **Primary Key Queries**: Efficient lookups by partition and clustering keys
- **Materialized Views**: Automated view creation and management
- **Schema Generation**: Automatic table and index creation
- **Migration Support**: Schema versioning and evolution

### ✅ Production-Ready Features
- **Health Checks**: Built-in cluster health monitoring
- **Retry Policies**: Configurable retry logic with exponential backoff
- **Performance Metrics**: Comprehensive operation monitoring and analytics
- **Logging Integration**: Full Microsoft.Extensions.Logging support
- **Documentation Generation**: Automatic schema and API documentation
- **Error Handling**: Robust exception handling and recovery

## Quick Start


### Basic Usage

```csharp
using CassandraOrm.Core;
using CassandraOrm.Mapping;

// Define your entity
[Table("users")]
public class User
{
    [PartitionKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("full_name")]
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [ClusteringKey]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Create your DbContext
public class MyDbContext : CassandraDbContext
{
    public CassandraDbSet<User> Users { get; set; } = null!;

    public MyDbContext() : base(new CassandraConfiguration
    {
        ContactPoints = "127.0.0.1",
        Port = 9042,
        Keyspace = "my_keyspace",
        AutoCreateKeyspace = true
    })
    {
    }
}

// Use the context
using var context = new MyDbContext();
await context.EnsureCreatedAsync();

// Create a user
var user = new User
{
    Name = "John Doe",
    Email = "john@example.com"
};

context.Users.Add(user);
await context.SaveChangesAsync();

## 📚 Documentation

- **[Complete Usage Guide](USAGE_GUIDE.md)** - Comprehensive guide with examples
- **[API Documentation](docs/)** - Auto-generated API documentation
- **[Real-World Example](Examples/RealWorldExample.cs)** - Social media platform demo
- **[Migration Guide](docs/migrations.md)** - Schema evolution best practices

## 🎯 Quick Start

### Installation

```bash
dotnet add package CassandraORM.NET
```

### Basic Example

```csharp
using CassandraOrm.Core;
using CassandraOrm.Mapping;

// Define your entity
[Table("users")]
public class User
{
    [PartitionKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("full_name")]
    public string Name { get; set; } = string.Empty;

    [Index]
    public string Email { get; set; } = string.Empty;

    [ClusteringKey]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Create your DbContext
public class AppDbContext : CassandraDbContext
{
    public CassandraDbSet<User> Users { get; set; } = null!;

    public AppDbContext(CassandraConfiguration config) : base(config) { }
}

// Use the context
var config = new CassandraConfiguration
{
    ContactPoints = new[] { "127.0.0.1" },
    Port = 9042,
    Keyspace = "my_app",
    AutoCreateKeyspace = true
};

using var context = new AppDbContext(config);
await context.EnsureCreatedAsync();

// CRUD operations
var user = new User { Name = "John Doe", Email = "john@example.com" };
await context.Users.AddAsync(user);
await context.SaveChangesAsync();

var foundUser = await context.Users.FindAsync(user.Id);
var allUsers = await context.Users.ToListAsync();
```

### Advanced Features Example

```csharp
// User-Defined Types
[UserDefinedType("address")]
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

// Collections and UDTs
[Table("user_profiles")]
public class UserProfile
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    public Address HomeAddress { get; set; } = new();
    public List<string> Interests { get; set; } = new();
    public HashSet<string> Skills { get; set; } = new();
    public Dictionary<string, string> SocialLinks { get; set; } = new();
}

// Health checks and retry policies
var contextWithRetry = context.WithRetry(logger);
await contextWithRetry.SaveChangesAsync(maxRetries: 3);

var healthCheck = new CassandraHealthCheck(cluster, logger);
var isHealthy = await healthCheck.CheckHealthAsync();

// Performance monitoring
var metrics = new CassandraMetrics(logger);
var summary = metrics.GetSummary();
Console.WriteLine($"Success rate: {summary.SuccessRate:P2}");
```

## 🔧 Configuration

```csharp
var config = new CassandraConfiguration
{
    ContactPoints = new[] { "node1", "node2", "node3" },
    Port = 9042,
    Keyspace = "production_app",
    Username = "app_user",
    Password = "secure_password",
    AutoCreateKeyspace = true,
    ReplicationFactor = 3,
    ConnectionTimeout = 30000,
    QueryTimeout = 30000
};
```

## 🧪 Testing

Comprehensive test coverage with 55+ unit tests and integration tests:

```bash
# Run unit tests (no dependencies)
dotnet test CassandraOrm.Tests

# Run integration tests (requires Docker)
dotnet test CassandraOrm.IntegrationTests

# Run all tests
dotnet test
```

## 📊 Performance & Monitoring

Built-in performance monitoring and health checks:

```csharp
// Performance metrics
var metrics = new CassandraMetrics(logger);
metrics.LogSummary(); // Logs performance statistics

// Health monitoring
var healthCheck = new CassandraHealthCheck(cluster);
var result = await healthCheck.CheckHealthAsync();

// Retry policies with exponential backoff
var retryPolicy = new CassandraRetryPolicy(logger);
await retryPolicy.ExecuteWithRetryAsync(
    () => context.SaveChangesAsync(),
    maxRetries: 3);
```

## 🏗️ Entity Mapping Examples

### Complete Entity with All Features

```csharp
[Table("social_posts")]
public class SocialPost
{
    [PartitionKey]
    public Guid PostId { get; set; }

    [ClusteringKey(Order = 0, Descending = true)]
    public DateTime CreatedAt { get; set; }

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Index]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    // Collections
    public List<string> Tags { get; set; } = new();
    public HashSet<Guid> Likes { get; set; } = new();

    // UDT
    public PostMetadata Metadata { get; set; } = new();

    [NotMapped]
    public string ComputedProperty => $"{Title} by {AuthorId}";
}
```

## 🚀 Requirements

- .NET 8.0 or later
- Apache Cassandra 3.0+ or DataStax Enterprise 6.0+
- CassandraCSharpDriver 3.19.0+

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎯 Roadmap

### ✅ Completed
- Core CRUD operations with LINQ support
- User-Defined Types (UDTs) and collections
- Materialized views management
- Health checks and retry policies
- Performance monitoring and metrics
- Comprehensive documentation generation
- Real-world example application

### 🔄 In Progress
- Advanced LINQ-to-CQL optimizations
- Prepared statement caching
- Advanced indexing options

### 📋 Planned
- GraphQL integration
- Entity Framework Core provider
- Visual Studio tooling
- Performance profiler integration
- [ ] NuGet package publishing

## Support

For questions, issues, or feature requests, please use the GitHub issue tracker.

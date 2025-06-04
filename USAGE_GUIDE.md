# CassandraORM.NET - Complete Usage Guide

This guide demonstrates the complete functionality of CassandraORM.NET with practical examples.

## Table of Contents

1. [Installation](#installation)
2. [Basic Setup](#basic-setup)
3. [Entity Definition](#entity-definition)
4. [Database Context](#database-context)
5. [CRUD Operations](#crud-operations)
6. [Advanced Features](#advanced-features)
7. [Performance Monitoring](#performance-monitoring)
8. [Health Checks](#health-checks)
9. [Retry Policies](#retry-policies)
10. [Schema Documentation](#schema-documentation)

## Installation

```bash
dotnet add package CassandraORM.NET
```

## Basic Setup

### Configuration

```csharp
using CassandraOrm.Configuration;

var config = new CassandraConfiguration
{
    ContactPoints = new[] { "127.0.0.1" },
    Port = 9042,
    Keyspace = "my_app",
    AutoCreateKeyspace = true,
    ReplicationFactor = 1,
    ConnectionTimeout = 30000,
    QueryTimeout = 30000
};
```

## Entity Definition

### Simple Entity

```csharp
using CassandraOrm.Mapping;

[Table("users")]
public class User
{
    [PartitionKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("full_name")]
    public string Name { get; set; } = string.Empty;

    [Index]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}
```

### Advanced Entity with Collections and UDTs

```csharp
using CassandraOrm.Mapping;
using CassandraOrm.UDT;

// User-Defined Type
[UserDefinedType("address")]
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

[Table("profiles")]
public class UserProfile
{
    [PartitionKey]
    public Guid UserId { get; set; }

    [ClusteringKey(Order = 0)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string Bio { get; set; } = string.Empty;

    // UDT property
    public Address HomeAddress { get; set; } = new();

    // Collection properties
    public List<string> Interests { get; set; } = new();
    public HashSet<string> Skills { get; set; } = new();
    public Dictionary<string, string> SocialLinks { get; set; } = new();
}
```

## Database Context

```csharp
using CassandraOrm.Core;
using Microsoft.Extensions.Logging;

public class AppDbContext : CassandraDbContext
{
    public CassandraDbSet<User> Users { get; set; } = null!;
    public CassandraDbSet<UserProfile> Profiles { get; set; } = null!;

    public AppDbContext(CassandraConfiguration config, ILogger logger)
        : base(config)
    {
    }
}
```

### Context Usage

```csharp
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<Program>();

using var context = new AppDbContext(config, logger);

// Create database schema
await context.EnsureCreatedAsync();
```

## CRUD Operations

### Create (Insert)

```csharp
// Single entity
var user = new User
{
    Name = "John Doe",
    Email = "john@example.com"
};

await context.Users.AddAsync(user);
await context.SaveChangesAsync();

// Bulk insert
var users = new List<User>
{
    new User { Name = "Alice", Email = "alice@example.com" },
    new User { Name = "Bob", Email = "bob@example.com" }
};

await context.Users.AddRangeAsync(users);
await context.SaveChangesAsync();
```

### Read (Query)

```csharp
// Get by primary key
var user = await context.Users.FindAsync(userId);

// LINQ queries
var activeUsers = await context.Users
    .Where(u => u.IsActive)
    .ToListAsync();

// Async enumeration
await foreach (var user in context.Users.Where(u => u.IsActive))
{
    Console.WriteLine($"User: {user.Name}");
}

// Complex queries
var recentUsers = await context.Users
    .Where(u => u.CreatedAt > DateTime.UtcNow.AddDays(-30))
    .Where(u => u.IsActive)
    .OrderByDescending(u => u.CreatedAt)
    .Take(10)
    .ToListAsync();
```

### Update

```csharp
var user = await context.Users.FindAsync(userId);
if (user != null)
{
    user.Name = "Updated Name";
    user.Email = "updated@example.com";
    
    context.Users.Update(user);
    await context.SaveChangesAsync();
}
```

### Delete

```csharp
// Delete by entity
var user = await context.Users.FindAsync(userId);
if (user != null)
{
    context.Users.Remove(user);
    await context.SaveChangesAsync();
}

// Bulk delete
var usersToDelete = await context.Users
    .Where(u => !u.IsActive)
    .ToListAsync();

context.Users.RemoveRange(usersToDelete);
await context.SaveChangesAsync();
```

## Advanced Features

### Working with User-Defined Types

```csharp
var profile = new UserProfile
{
    UserId = userId,
    Bio = "Software Developer",
    HomeAddress = new Address
    {
        Street = "123 Main St",
        City = "San Francisco",
        Country = "USA",
        PostalCode = "94105"
    },
    Interests = new List<string> { "programming", "music", "travel" },
    Skills = new HashSet<string> { "C#", "Cassandra", "Docker" },
    SocialLinks = new Dictionary<string, string>
    {
        ["linkedin"] = "linkedin.com/in/johndoe",
        ["github"] = "github.com/johndoe"
    }
};

await context.Profiles.AddAsync(profile);
await context.SaveChangesAsync();
```

### Materialized Views

```csharp
// Define materialized view entity
[MaterializedView("users_by_email", "users")]
public class UserByEmail
{
    [PartitionKey]
    public string Email { get; set; } = string.Empty;

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Add to context
public CassandraDbSet<UserByEmail> UsersByEmail { get; set; } = null!;

// Create materialized views
await context.CreateViewsAsync();

// Query materialized view
var userByEmail = await context.UsersByEmail
    .Where(u => u.Email == "john@example.com")
    .FirstOrDefaultAsync();
```

## Performance Monitoring

```csharp
using CassandraOrm.Core;

// Create metrics collector
var metrics = new CassandraMetrics(logger);

// Record operations manually
using (var timer = new OperationTimer(metrics, "SELECT", "users"))
{
    try
    {
        var users = await context.Users.ToListAsync();
        // Operation succeeded
    }
    catch (Exception ex)
    {
        timer.MarkFailure(ex.Message);
        throw;
    }
}

// Get performance summary
var summary = metrics.GetSummary();
Console.WriteLine($"Total operations: {summary.TotalOperations}");
Console.WriteLine($"Average response time: {summary.AverageResponseTime.TotalMilliseconds}ms");
Console.WriteLine($"Success rate: {summary.SuccessRate:P2}");

// Log summary
metrics.LogSummary();
```

## Health Checks

```csharp
using CassandraOrm.Core;

// Create health check
var healthCheck = new CassandraHealthCheck(cluster, logger);

// Quick health check
var result = await healthCheck.CheckHealthAsync(TimeSpan.FromSeconds(5));
Console.WriteLine($"Healthy: {result.IsHealthy}");
Console.WriteLine($"Response time: {result.ResponseTime.TotalMilliseconds}ms");

if (!result.IsHealthy)
{
    Console.WriteLine($"Error: {result.Message}");
}

// Wait for cluster to become available
var isAvailable = await healthCheck.WaitForClusterAsync(
    maxWaitTime: TimeSpan.FromMinutes(2),
    retryInterval: TimeSpan.FromSeconds(5));

if (isAvailable)
{
    Console.WriteLine("Cassandra cluster is now available!");
}
```

## Retry Policies

```csharp
using CassandraOrm.Core;

// Use retry policy with context
var contextWithRetry = context.WithRetry(logger);

// Operations with automatic retry
await contextWithRetry.SaveChangesAsync(maxRetries: 3);
await contextWithRetry.EnsureCreatedAsync(maxRetries: 5);

// Manual retry policy
var retryPolicy = new CassandraRetryPolicy(logger);

var users = await retryPolicy.ExecuteWithRetryAsync(
    async () => await context.Users.ToListAsync(),
    maxRetries: 3,
    baseDelay: TimeSpan.FromSeconds(1),
    maxDelay: TimeSpan.FromSeconds(10));
```

## Schema Documentation

```csharp
using CassandraOrm.Documentation;
using System.Reflection;

// Generate documentation for all entities in assembly
var assembly = Assembly.GetExecutingAssembly();

// Generate markdown documentation
var markdown = DocumentationGenerator.GenerateMarkdownDocumentation(assembly);
await File.WriteAllTextAsync("EntityDocumentation.md", markdown);

// Generate CQL schema
var cqlSchema = DocumentationGenerator.GenerateCqlSchema(assembly);
await File.WriteAllTextAsync("Schema.cql", cqlSchema);

// Save all documentation to directory
await DocumentationGenerator.SaveDocumentationAsync("./docs", assembly);
```

## Best Practices

### 1. Connection Management

```csharp
// Use dependency injection
services.AddSingleton<CassandraConfiguration>(config);
services.AddScoped<AppDbContext>();

// Or use using statements for short-lived contexts
using var context = new AppDbContext(config, logger);
```

### 2. Error Handling

```csharp
try
{
    await context.SaveChangesAsync();
}
catch (TimeoutException ex)
{
    logger.LogWarning("Operation timed out: {Message}", ex.Message);
    // Implement retry logic
}
catch (Exception ex)
{
    logger.LogError(ex, "Database operation failed");
    throw;
}
```

### 3. Performance Optimization

```csharp
// Use async enumeration for large datasets
await foreach (var user in context.Users.AsAsyncEnumerable())
{
    // Process one at a time without loading all into memory
    await ProcessUser(user);
}

// Use pagination for large result sets
var pageSize = 100;
var lastToken = "";

do
{
    var (users, nextToken) = await GetPagedUsers(context, pageSize, lastToken);
    
    foreach (var user in users)
    {
        await ProcessUser(user);
    }
    
    lastToken = nextToken;
} while (!string.IsNullOrEmpty(lastToken));
```

### 4. Schema Evolution

```csharp
// Use migrations for schema changes
public class AddUserProfileMigration : CassandraMigration
{
    public override string Version => "1.1.0";

    public override async Task UpAsync(ISession session)
    {
        await session.ExecuteAsync(new SimpleStatement(@"
            ALTER TABLE users 
            ADD profile_data text"));
    }

    public override async Task DownAsync(ISession session)
    {
        await session.ExecuteAsync(new SimpleStatement(@"
            ALTER TABLE users 
            DROP profile_data"));
    }
}
```

## Complete Example Application

See the `Examples/RealWorldExample.cs` file for a comprehensive example that demonstrates:

- Social media platform with users, posts, and comments
- User-defined types for contact information
- Collections for interests and social links
- Materialized views for efficient queries
- Advanced LINQ queries
- Bulk operations
- Error handling and logging

This example showcases all the features of CassandraORM.NET in a realistic scenario.

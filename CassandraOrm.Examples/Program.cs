using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CassandraOrm.Core;
using CassandraOrm.Configuration;
using CassandraOrm.Mapping;
using Microsoft.Extensions.Logging;

namespace CassandraOrm.Examples;

/// <summary>
/// Practical example demonstrating basic CassandraORM.NET usage
/// This example can run without requiring a Cassandra instance (shows setup)
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("CassandraORM.NET - Basic Usage Example");
        Console.WriteLine("=====================================");

        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<Program>();

        // Show configuration example
        ShowConfigurationExample(logger);
        
        // Show entity definition example
        ShowEntityDefinitionExample(logger);
        
        // Show context setup example
        ShowContextSetupExample(logger);
        
        // Show query examples (without execution)
        ShowQueryExamples(logger);

        Console.WriteLine("\nExample completed! To run with actual Cassandra:");
        Console.WriteLine("1. Start Cassandra locally on port 9042");
        Console.WriteLine("2. Uncomment the live execution section below");
    }

    private static void ShowConfigurationExample(ILogger logger)
    {
        logger.LogInformation("=== Configuration Example ===");
        
        var config = new CassandraConfiguration
        {
            ContactPoints = new[] { "127.0.0.1" },
            Port = 9042,
            Keyspace = "example_keyspace",
            AutoCreateKeyspace = true,
            ReplicationFactor = 1,
            ConnectionTimeout = 30000,
            QueryTimeout = 30000
        };

        Console.WriteLine($"✓ Configuration created for keyspace: {config.Keyspace}");
        Console.WriteLine($"  Contact points: {string.Join(", ", config.ContactPoints)}");
        Console.WriteLine($"  Port: {config.Port}");
    }

    private static void ShowEntityDefinitionExample(ILogger logger)
    {
        logger.LogInformation("=== Entity Definition Example ===");
        
        Console.WriteLine("✓ User entity with attributes:");
        Console.WriteLine("  [Table(\"users\")] - Maps to 'users' table");
        Console.WriteLine("  [PartitionKey] UserId - Primary partition key");
        Console.WriteLine("  [Column(\"full_name\")] Name - Custom column mapping");
        Console.WriteLine("  [Index] Email - Secondary index");
    }

    private static void ShowContextSetupExample(ILogger logger)
    {
        logger.LogInformation("=== DbContext Setup Example ===");
        
        Console.WriteLine("✓ BlogContext extends CassandraDbContext");
        Console.WriteLine("  DbSet<User> Users - Entity set for CRUD operations");
        Console.WriteLine("  DbSet<Post> Posts - Another entity set");
        Console.WriteLine("  EnsureCreatedAsync() - Creates schema automatically");
    }

    private static void ShowQueryExamples(ILogger logger)
    {
        logger.LogInformation("=== Query Examples ===");
        
        Console.WriteLine("✓ LINQ-style queries:");
        Console.WriteLine("  await context.Users.Where(u => u.IsActive).ToListAsync()");
        Console.WriteLine("  await context.Posts.FirstOrDefaultAsync(p => p.Id == postId)");
        Console.WriteLine("  await context.Users.AddAsync(newUser)");
        Console.WriteLine("  await context.SaveChangesAsync()");
    }

    // Uncomment and modify this section to run with actual Cassandra
    /*
    private static async Task LiveExample(ILogger logger)
    {
        var config = new CassandraConfiguration
        {
            ContactPoints = new[] { "127.0.0.1" },
            Port = 9042,
            Keyspace = "blog_example",
            AutoCreateKeyspace = true
        };

        using var context = new BlogContext(config, logger);
        
        // Create schema
        await context.EnsureCreatedAsync();
        
        // Create a user
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Name = "John Doe",
            Email = "john@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        
        // Query users
        var activeUsers = await context.Users
            .Where(u => u.IsActive)
            .ToListAsync();
            
        logger.LogInformation($"Found {activeUsers.Count} active users");
    }
    */
}

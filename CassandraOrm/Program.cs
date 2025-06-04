// Main namespace exports for easier usage
global using CassandraOrm.Core;
global using CassandraOrm.Configuration;
global using CassandraOrm.Mapping;
global using CassandraOrm.Migrations;
global using CassandraOrm.Extensions;

using System.Runtime.CompilerServices;

// Make internal methods visible to the test assembly
[assembly: InternalsVisibleTo("CassandraOrm.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // For Moq

namespace CassandraOrm;

/// <summary>
/// CassandraORM.NET - Entity Framework-like ORM for Apache Cassandra
/// 
/// This library provides a familiar Entity Framework-style API for working with Apache Cassandra,
/// including support for LINQ queries, migrations, and dependency injection.
/// 
/// Key Features:
/// - Entity Framework-like DbContext pattern
/// - LINQ query support with translation to CQL
/// - Automatic entity mapping with attributes
/// - Database migrations system
/// - Dependency injection integration
/// - Async/await support throughout
/// - Connection pooling and session management
/// 
/// Usage:
/// 1. Define your entities with [Table], [PartitionKey], [ClusteringKey] attributes
/// 2. Create a context class inheriting from CassandraDbContext
/// 3. Register the context with dependency injection using AddCassandraContext()
/// 4. Use LINQ queries and SaveChanges() just like Entity Framework
/// 
/// Example:
/// [Table("users")]
/// public class User
/// {
///     [PartitionKey]
///     public Guid Id { get; set; }
///     
///     public string Username { get; set; }
///     public string Email { get; set; }
/// }
/// 
/// public class MyContext : CassandraDbContext
/// {
///     public CassandraDbSet&lt;User&gt; Users { get; set; }
/// }
/// 
/// // In Startup.cs or Program.cs:
/// services.AddCassandraContext&lt;MyContext&gt;(config =&gt;
/// {
///     config.ContactPoints = new[] { "localhost" };
///     config.Keyspace = "myapp";
/// });
/// </summary>
public static class CassandraOrm
{
    /// <summary>
    /// Gets the version of CassandraORM.NET.
    /// </summary>
    public static Version Version => new(1, 0, 0);

    /// <summary>
    /// Gets the name of the library.
    /// </summary>
    public static string Name => "CassandraORM.NET";

    /// <summary>
    /// Gets the description of the library.
    /// </summary>
    public static string Description => "Entity Framework-like ORM for Apache Cassandra";
}

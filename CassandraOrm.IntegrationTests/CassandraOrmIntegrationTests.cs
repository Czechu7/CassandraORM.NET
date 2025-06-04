using CassandraOrm.Core;
using CassandraOrm.Mapping;
using FluentAssertions;
using Testcontainers.Cassandra;
using Xunit;

namespace CassandraOrm.IntegrationTests;

/// <summary>
/// Integration tests for the CassandraORM library using Testcontainers.
/// These tests verify the library works with a real Cassandra instance.
/// </summary>
public class CassandraOrmIntegrationTests : IAsyncLifetime
{
    private readonly CassandraContainer _cassandraContainer = new CassandraBuilder()
        .WithImage("cassandra:4.1")
        .WithPortBinding(9042, true)
        .Build();

    private TestDbContext? _context;

    [Table("users")]
    public class User
    {
        [PartitionKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("full_name")]
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }    public class TestDbContext : CassandraDbContext
    {
        public CassandraDbSet<User> Users { get; set; } = null!;        public TestDbContext(string contactPoint, int port) : base(new Configuration.CassandraConfiguration
        {
            ContactPoints = new[] { contactPoint },
            Port = port,
            Keyspace = "test_keyspace",
            AutoCreateKeyspace = true
        })
        {
        }
    }public async Task InitializeAsync()
    {
        await _cassandraContainer.StartAsync();
        
        var contactPoint = _cassandraContainer.Hostname;
        var port = _cassandraContainer.GetMappedPublicPort(9042);
        
        _context = new TestDbContext(contactPoint, port);
        
        // Wait a bit for Cassandra to be ready
        await Task.Delay(5000);
        
        // Create the schema
        await _context.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
        
        await _cassandraContainer.DisposeAsync();
    }

    [Fact]
    public async Task BasicCrudOperations_ShouldWork()
    {
        // Arrange
        var user = new User
        {
            Name = "John Doe",
            Email = "john.doe@example.com"
        };

        // Act & Assert - Create
        _context!.Users.Add(user);
        var savedCount = await _context.SaveChangesAsync();
        savedCount.Should().Be(1);

        // Act & Assert - Read
        var foundUser = await _context.Users.FindAsync(user.Id);
        foundUser.Should().NotBeNull();
        foundUser!.Name.Should().Be("John Doe");
        foundUser.Email.Should().Be("john.doe@example.com");

        // Act & Assert - Update
        foundUser.Name = "Jane Doe";
        _context.Users.Update(foundUser);
        var updatedCount = await _context.SaveChangesAsync();
        updatedCount.Should().Be(1);

        // Verify update
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.Name.Should().Be("Jane Doe");

        // Act & Assert - Delete
        _context.Users.Remove(updatedUser);
        var deletedCount = await _context.SaveChangesAsync();
        deletedCount.Should().Be(1);

        // Verify deletion
        var deletedUser = await _context.Users.FindAsync(user.Id);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task BulkOperations_ShouldWork()
    {
        // Arrange
        var users = new[]
        {
            new User { Name = "Alice", Email = "alice@example.com" },
            new User { Name = "Bob", Email = "bob@example.com" },
            new User { Name = "Charlie", Email = "charlie@example.com" }
        };

        // Act - Add multiple users
        _context!.Users.AddRange(users);
        var savedCount = await _context.SaveChangesAsync();

        // Assert
        savedCount.Should().Be(3);

        // Verify all users were saved
        foreach (var user in users)
        {
            var savedUser = await _context.Users.FindAsync(user.Id);
            savedUser.Should().NotBeNull();
            savedUser!.Name.Should().Be(user.Name);
        }

        // Clean up
        _context.Users.RemoveRange(users);
        await _context.SaveChangesAsync();
    }
}

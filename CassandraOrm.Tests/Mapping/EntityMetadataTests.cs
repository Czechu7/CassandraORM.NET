using CassandraOrm.Mapping;
using FluentAssertions;
using System.Reflection;

namespace CassandraOrm.Tests.Mapping;

public class EntityMetadataTests
{
    [Table("test_users")]
    public class TestUser
    {
        [PartitionKey(0)]
        public Guid Id { get; set; }
        
        [ClusteringKey(0)]
        public DateTime CreatedAt { get; set; }
        
        [Column("user_name")]
        public string Name { get; set; } = string.Empty;
        
        public string Email { get; set; } = string.Empty;
        
        [StaticColumn]
        public string CompanyName { get; set; } = string.Empty;
        
        [Counter]
        public long LoginCount { get; set; }
        
        [Index]
        public string Department { get; set; } = string.Empty;
        
        [NotMapped]
        public string TempData { get; set; } = string.Empty;
    }

    [Table("invalid_table")]
    public class InvalidEntity
    {
        // No partition key - should cause exception
        public string Name { get; set; } = string.Empty;
    }

    public class EntityWithoutTableAttribute
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Constructor_WithValidEntity_ShouldInitializeCorrectly()
    {
        // Act
        var metadata = new EntityMetadata(typeof(TestUser));

        // Assert
        metadata.EntityType.Should().Be(typeof(TestUser));
        metadata.TableName.Should().Be("test_users");
        metadata.Keyspace.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEntityWithoutTableAttribute_ShouldThrowException()
    {
        // Act & Assert
        Action act = () => new EntityMetadata(typeof(EntityWithoutTableAttribute));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must have a [Table] attribute*");
    }

    [Fact]
    public void Constructor_WithEntityWithoutPartitionKey_ShouldThrowException()
    {
        // Act & Assert
        Action act = () => new EntityMetadata(typeof(InvalidEntity));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must have at least one partition key*");
    }

    [Fact]
    public void PartitionKeys_ShouldReturnCorrectProperties()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.PartitionKeys.Should().HaveCount(1);
        metadata.PartitionKeys[0].Name.Should().Be("Id");
    }

    [Fact]
    public void ClusteringKeys_ShouldReturnCorrectProperties()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.ClusteringKeys.Should().HaveCount(1);
        metadata.ClusteringKeys[0].Name.Should().Be("CreatedAt");
    }

    [Fact]
    public void Properties_ShouldExcludeNotMappedProperties()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.Properties.Should().NotContain(p => p.Name == "TempData");
        metadata.Properties.Select(p => p.Name).Should().Contain(new[] 
        {
            "Id", "CreatedAt", "Name", "Email", "CompanyName", "LoginCount", "Department"
        });
    }

    [Fact]
    public void ColumnMappings_ShouldMapCorrectly()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act
        var nameProperty = typeof(TestUser).GetProperty("Name")!;
        var emailProperty = typeof(TestUser).GetProperty("Email")!;

        // Assert
        metadata.ColumnMappings[nameProperty].Should().Be("user_name");
        metadata.ColumnMappings[emailProperty].Should().Be("email"); // lowercase default
    }

    [Fact]
    public void StaticColumns_ShouldReturnCorrectProperties()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.StaticColumns.Should().HaveCount(1);
        metadata.StaticColumns[0].Name.Should().Be("CompanyName");
    }

    [Fact]
    public void CounterColumns_ShouldReturnCorrectProperties()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.CounterColumns.Should().HaveCount(1);
        metadata.CounterColumns[0].Name.Should().Be("LoginCount");
    }

    [Fact]
    public void IndexedProperties_ShouldReturnCorrectProperties()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.IndexedProperties.Should().HaveCount(1);
        metadata.IndexedProperties[0].Name.Should().Be("Department");
    }

    [Fact]
    public void GetPrimaryKeyValues_ShouldReturnCorrectValues()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));
        var user = new TestUser
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var keyValues = metadata.GetPrimaryKeyValues(user);

        // Assert
        keyValues.Should().HaveCount(2);
        keyValues[0].Should().Be(user.Id);
        keyValues[1].Should().Be(user.CreatedAt);
    }

    [Fact]
    public void GetColumnName_ShouldReturnCorrectName()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));
        var nameProperty = typeof(TestUser).GetProperty("Name")!;
        var emailProperty = typeof(TestUser).GetProperty("Email")!;

        // Act & Assert
        metadata.GetColumnName(nameProperty).Should().Be("user_name");
        metadata.GetColumnName(emailProperty).Should().Be("email");
    }

    [Fact]
    public void GetFullTableName_WithoutKeyspace_ShouldReturnTableName()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(TestUser));

        // Act & Assert
        metadata.GetFullTableName().Should().Be("test_users");
    }

    [Table("test_table", Keyspace = "test_keyspace")]
    public class EntityWithKeyspace
    {
        [PartitionKey(0)]
        public Guid Id { get; set; }
    }

    [Fact]
    public void GetFullTableName_WithKeyspace_ShouldReturnFullyQualifiedName()
    {
        // Arrange
        var metadata = new EntityMetadata(typeof(EntityWithKeyspace));

        // Act & Assert
        metadata.GetFullTableName().Should().Be("test_keyspace.test_table");
    }
}

using CassandraOrm.Mapping;
using FluentAssertions;

namespace CassandraOrm.Tests.Mapping;

public class EntityMetadataCacheTests
{
    [Table("cached_entity")]
    public class CachedEntity
    {
        [PartitionKey(0)]
        public Guid Id { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    [Table("another_entity")]
    public class AnotherEntity
    {
        [PartitionKey(0)]
        public int Id { get; set; }
    }

    [Fact]
    public void GetMetadata_ShouldReturnSameInstanceForSameType()
    {
        // Act
        var metadata1 = EntityMetadataCache.GetMetadata(typeof(CachedEntity));
        var metadata2 = EntityMetadataCache.GetMetadata(typeof(CachedEntity));

        // Assert
        metadata1.Should().BeSameAs(metadata2);
    }

    [Fact]
    public void GetMetadata_Generic_ShouldReturnCorrectMetadata()
    {
        // Act
        var metadata = EntityMetadataCache.GetMetadata<CachedEntity>();

        // Assert
        metadata.EntityType.Should().Be(typeof(CachedEntity));
        metadata.TableName.Should().Be("cached_entity");
    }

    [Fact]
    public void GetMetadata_DifferentTypes_ShouldReturnDifferentInstances()
    {
        // Act
        var metadata1 = EntityMetadataCache.GetMetadata<CachedEntity>();
        var metadata2 = EntityMetadataCache.GetMetadata<AnotherEntity>();

        // Assert
        metadata1.Should().NotBeSameAs(metadata2);
        metadata1.EntityType.Should().Be(typeof(CachedEntity));
        metadata2.EntityType.Should().Be(typeof(AnotherEntity));
    }

    [Fact]
    public void GetMetadata_ThreadSafety_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var tasks = new List<Task<EntityMetadata>>();
        const int taskCount = 10;

        // Act - Create multiple concurrent tasks accessing the same type
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(() => EntityMetadataCache.GetMetadata<CachedEntity>()));
        }

        var results = Task.WhenAll(tasks).Result;

        // Assert - All results should be the same instance
        var firstResult = results[0];
        results.Should().AllSatisfy(result => result.Should().BeSameAs(firstResult));
    }
}

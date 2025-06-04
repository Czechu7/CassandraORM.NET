using CassandraOrm.Core;
using CassandraOrm.Mapping;
using FluentAssertions;
using Moq;

namespace CassandraOrm.Tests.Core;

public class CassandraDbSetTests
{
    [Table("test_entities")]
    public class TestEntity
    {
        [PartitionKey(0)]
        public Guid Id { get; set; }
        
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
    }

    public class TestContext : CassandraDbContext
    {
        public CassandraDbSet<TestEntity> TestEntities { get; set; } = null!;
        
        public TestContext() : base()
        {
        }
    }

    [Fact]
    public void Constructor_WithValidContext_ShouldInitialize()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();

        // Act
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);

        // Assert
        dbSet.Should().NotBeNull();
        dbSet.ElementType.Should().Be(typeof(TestEntity));
        dbSet.Expression.Should().NotBeNull();
        dbSet.Provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowException()
    {
        // Act & Assert
        Action act = () => new CassandraDbSet<TestEntity>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_WithValidEntity_ShouldTrackEntity()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        dbSet.Add(entity);

        // Assert
        mockContext.Verify(c => c.TrackEntity(entity, EntityState.Added), Times.Once);
    }

    [Fact]
    public void Add_WithNullEntity_ShouldThrowException()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);

        // Act & Assert
        Action act = () => dbSet.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AddAsync_WithValidEntity_ShouldTrackEntity()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        await dbSet.AddAsync(entity);

        // Assert
        mockContext.Verify(c => c.TrackEntity(entity, EntityState.Added), Times.Once);
    }

    [Fact]
    public void AddRange_WithValidEntities_ShouldTrackAllEntities()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entities = new[]
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "Test1" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Test2" }
        };

        // Act
        dbSet.AddRange(entities);

        // Assert
        foreach (var entity in entities)
        {
            mockContext.Verify(c => c.TrackEntity(entity, EntityState.Added), Times.Once);
        }
    }

    [Fact]
    public void AddRange_WithNullEntities_ShouldThrowException()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);

        // Act & Assert
        Action act = () => dbSet.AddRange((IEnumerable<TestEntity>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_WithDetachedEntity_ShouldTrackAsModified()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        mockContext.Setup(c => c.GetEntityState(It.IsAny<TestEntity>()))
                   .Returns(EntityState.Detached);
        
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        dbSet.Update(entity);

        // Assert
        mockContext.Verify(c => c.TrackEntity(entity, EntityState.Modified), Times.Once);
    }

    [Fact]
    public void Update_WithNullEntity_ShouldThrowException()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);

        // Act & Assert
        Action act = () => dbSet.Update(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Remove_WithAddedEntity_ShouldDetachEntity()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        mockContext.Setup(c => c.GetEntityState(It.IsAny<TestEntity>()))
                   .Returns(EntityState.Added);
        
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        dbSet.Remove(entity);

        // Assert
        mockContext.Verify(c => c.TrackEntity(entity, EntityState.Detached), Times.Once);
    }

    [Fact]
    public void Remove_WithExistingEntity_ShouldMarkAsDeleted()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        mockContext.Setup(c => c.GetEntityState(It.IsAny<TestEntity>()))
                   .Returns(EntityState.Unchanged);
        
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        dbSet.Remove(entity);

        // Assert
        mockContext.Verify(c => c.TrackEntity(entity, EntityState.Deleted), Times.Once);
    }

    [Fact]
    public void Remove_WithNullEntity_ShouldThrowException()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);

        // Act & Assert
        Action act = () => dbSet.Remove(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveRange_WithValidEntities_ShouldRemoveAllEntities()
    {
        // Arrange
        var mockContext = new Mock<CassandraDbContext>();
        mockContext.Setup(c => c.GetEntityState(It.IsAny<TestEntity>()))
                   .Returns(EntityState.Unchanged);
        
        var dbSet = new CassandraDbSet<TestEntity>(mockContext.Object);
        var entities = new[]
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "Test1" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Test2" }
        };

        // Act
        dbSet.RemoveRange(entities);

        // Assert
        foreach (var entity in entities)
        {
            mockContext.Verify(c => c.TrackEntity(entity, EntityState.Deleted), Times.Once);
        }
    }
}

using CassandraOrm.Mapping;
using FluentAssertions;

namespace CassandraOrm.Tests.Mapping;

public class AttributesTests
{
    [Fact]
    public void TableAttribute_Constructor_ShouldSetProperties()
    {
        // Act
        var attribute = new TableAttribute("test_table");

        // Assert
        attribute.Name.Should().Be("test_table");
        attribute.Keyspace.Should().BeNull();
    }

    [Fact]
    public void TableAttribute_WithKeyspace_ShouldSetBothProperties()
    {
        // Act
        var attribute = new TableAttribute("test_table") { Keyspace = "test_keyspace" };

        // Assert
        attribute.Name.Should().Be("test_table");
        attribute.Keyspace.Should().Be("test_keyspace");
    }

    [Fact]
    public void TableAttribute_WithNullName_ShouldThrowException()
    {
        // Act & Assert
        Action act = () => new TableAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PartitionKeyAttribute_DefaultConstructor_ShouldSetDefaultOrder()
    {
        // Act
        var attribute = new PartitionKeyAttribute();

        // Assert
        attribute.Order.Should().Be(0);
    }

    [Fact]
    public void PartitionKeyAttribute_WithOrder_ShouldSetOrder()
    {
        // Act
        var attribute = new PartitionKeyAttribute(5);

        // Assert
        attribute.Order.Should().Be(5);
    }

    [Fact]
    public void ClusteringKeyAttribute_DefaultConstructor_ShouldSetDefaultValues()
    {
        // Act
        var attribute = new ClusteringKeyAttribute();

        // Assert
        attribute.Order.Should().Be(0);
        attribute.Descending.Should().BeFalse();
    }

    [Fact]
    public void ClusteringKeyAttribute_WithParameters_ShouldSetValues()
    {
        // Act
        var attribute = new ClusteringKeyAttribute(3, true);

        // Assert
        attribute.Order.Should().Be(3);
        attribute.Descending.Should().BeTrue();
    }

    [Fact]
    public void ColumnAttribute_Constructor_ShouldSetName()
    {
        // Act
        var attribute = new ColumnAttribute("column_name");

        // Assert
        attribute.Name.Should().Be("column_name");
        attribute.Type.Should().BeNull();
    }

    [Fact]
    public void ColumnAttribute_WithType_ShouldSetBothProperties()
    {
        // Act
        var attribute = new ColumnAttribute("column_name") { Type = "text" };

        // Assert
        attribute.Name.Should().Be("column_name");
        attribute.Type.Should().Be("text");
    }

    [Fact]
    public void ColumnAttribute_WithNullName_ShouldThrowException()
    {
        // Act & Assert
        Action act = () => new ColumnAttribute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StaticColumnAttribute_ShouldBeInstantiable()
    {
        // Act
        var attribute = new StaticColumnAttribute();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void CounterAttribute_ShouldBeInstantiable()
    {
        // Act
        var attribute = new CounterAttribute();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void IndexAttribute_DefaultConstructor_ShouldSetDefaultValues()
    {
        // Act
        var attribute = new IndexAttribute();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Name.Should().BeNull();
        attribute.Type.Should().Be(IndexType.Default);
    }

    [Fact]
    public void IndexAttribute_WithName_ShouldSetName()
    {
        // Act
        var attribute = new IndexAttribute("custom_index");

        // Assert
        attribute.Name.Should().Be("custom_index");
        attribute.Type.Should().Be(IndexType.Default);
    }

    [Fact]
    public void IndexAttribute_WithType_ShouldSetType()
    {
        // Act
        var attribute = new IndexAttribute { Type = IndexType.SASI };

        // Assert
        attribute.Type.Should().Be(IndexType.SASI);
    }

    [Fact]
    public void NotMappedAttribute_ShouldBeInstantiable()
    {
        // Act
        var attribute = new NotMappedAttribute();

        // Assert
        attribute.Should().NotBeNull();
    }
}

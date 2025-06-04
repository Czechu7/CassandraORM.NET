using CassandraOrm.Configuration;
using Cassandra;
using FluentAssertions;

namespace CassandraOrm.Tests.Configuration;

public class CassandraConfigurationTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new CassandraConfiguration();

        // Assert
        config.ContactPoints.Should().BeEquivalentTo(new[] { "127.0.0.1" });
        config.Port.Should().Be(9042);
        config.Keyspace.Should().BeNull();
        config.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.ConsistencyLevel.Should().Be(ConsistencyLevel.LocalQuorum);
        config.ReplicationFactor.Should().Be(1);
        config.UseNetworkTopologyStrategy.Should().BeFalse();
        config.ConnectionTimeout.Should().Be(5000);
        config.QueryTimeout.Should().Be(12000);
    }

    [Fact]
    public void ContactPoints_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();
        var contactPoints = new[] { "192.168.1.1", "192.168.1.2" };

        // Act
        config.ContactPoints = contactPoints;

        // Assert
        config.ContactPoints.Should().BeEquivalentTo(contactPoints);
    }

    [Fact]
    public void Port_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();

        // Act
        config.Port = 9043;

        // Assert
        config.Port.Should().Be(9043);
    }

    [Fact]
    public void Keyspace_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();

        // Act
        config.Keyspace = "test_keyspace";

        // Assert
        config.Keyspace.Should().Be("test_keyspace");
    }

    [Fact]
    public void Authentication_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();

        // Act
        config.Username = "testuser";
        config.Password = "testpass";

        // Assert
        config.Username.Should().Be("testuser");
        config.Password.Should().Be("testpass");
    }

    [Fact]
    public void ConsistencyLevel_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();

        // Act
        config.ConsistencyLevel = ConsistencyLevel.All;

        // Assert
        config.ConsistencyLevel.Should().Be(ConsistencyLevel.All);
    }

    [Fact]
    public void ReplicationConfiguration_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();

        // Act
        config.ReplicationFactor = 3;
        config.UseNetworkTopologyStrategy = true;

        // Assert
        config.ReplicationFactor.Should().Be(3);
        config.UseNetworkTopologyStrategy.Should().BeTrue();
    }

    [Fact]
    public void TimeoutConfiguration_ShouldBeSettable()
    {
        // Arrange
        var config = new CassandraConfiguration();

        // Act
        config.ConnectionTimeout = 10000;
        config.QueryTimeout = 20000;

        // Assert
        config.ConnectionTimeout.Should().Be(10000);
        config.QueryTimeout.Should().Be(20000);
    }
}

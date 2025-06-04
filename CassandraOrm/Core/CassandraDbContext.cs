using System.Collections.Concurrent;
using System.Reflection;
using Cassandra;
using CassandraOrm.Configuration;
using CassandraOrm.Mapping;
using CassandraOrm.UDT;
using CassandraOrm.MaterializedViews;
using CassandraOrm.Collections;
using Microsoft.Extensions.Logging;

namespace CassandraOrm.Core;

/// <summary>
/// The main entry point for interacting with a Cassandra database.
/// Similar to Entity Framework's DbContext.
/// </summary>
public abstract class CassandraDbContext : IAsyncDisposable, IDisposable
{
    private readonly CassandraConfiguration _configuration;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<Type, object> _entitySets = new();
    private readonly ConcurrentDictionary<object, EntityState> _entityStates = new();
    private readonly UdtRegistry _udtRegistry = new();
    private readonly MaterializedViewRegistry _viewRegistry = new();
    private readonly CollectionUpdateBuilder _collectionUpdates = new();
    private Cluster? _cluster;
    private ISession? _session;
    private bool _disposed;

    /// <summary>
    /// Gets the Cassandra session used by this context.
    /// </summary>
    public ISession Session => GetSession();

    /// <summary>
    /// Gets the UDT registry for managing User-Defined Types.
    /// </summary>
    public UdtRegistry UdtRegistry => _udtRegistry;

    /// <summary>
    /// Gets the materialized view registry for managing views.
    /// </summary>
    public MaterializedViewRegistry ViewRegistry => _viewRegistry;

    /// <summary>
    /// Gets the collection update builder for batch collection operations.
    /// </summary>
    public CollectionUpdateBuilder CollectionUpdates => _collectionUpdates;

    /// <summary>
    /// Gets the configuration used by this context.
    /// </summary>
    protected CassandraConfiguration Configuration => _configuration;

    /// <summary>
    /// Initializes a new instance of the CassandraDbContext with default configuration.
    /// </summary>
    protected CassandraDbContext() : this(new CassandraConfiguration())
    {
    }

    /// <summary>
    /// Initializes a new instance of the CassandraDbContext with the specified configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use for this context.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    protected CassandraDbContext(CassandraConfiguration configuration, ILogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        Initialize();
    }

    private void Initialize()
    {
        // Find all CassandraDbSet<T> properties and create instances
        var dbSetProperties = GetType().GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(CassandraDbSet<>));

        foreach (var property in dbSetProperties)
        {
            var entityType = property.PropertyType.GetGenericArguments()[0];
            var dbSetType = typeof(CassandraDbSet<>).MakeGenericType(entityType);
            var dbSet = Activator.CreateInstance(dbSetType, this);
            
            property.SetValue(this, dbSet);
            _entitySets[entityType] = dbSet!;
        }
    }

    private ISession GetSession()
    {
        if (_session == null)
        {
            var builder = Cluster.Builder()
                .AddContactPoints(_configuration.ContactPoints)
                .WithPort(_configuration.Port);

            if (!string.IsNullOrEmpty(_configuration.Username) && !string.IsNullOrEmpty(_configuration.Password))
            {
                builder.WithCredentials(_configuration.Username, _configuration.Password);
            }

            if (_configuration.RetryPolicy != null)
            {
                builder.WithRetryPolicy(_configuration.RetryPolicy);
            }

            if (_configuration.LoadBalancingPolicy != null)
            {
                builder.WithLoadBalancingPolicy(_configuration.LoadBalancingPolicy);
            }

            builder.WithSocketOptions(new SocketOptions()
                .SetConnectTimeoutMillis(_configuration.ConnectionTimeout)
                .SetReadTimeoutMillis(_configuration.QueryTimeout));

            _cluster = builder.Build();

            if (!string.IsNullOrEmpty(_configuration.Keyspace))
            {
                _session = _cluster.Connect(_configuration.Keyspace);
            }
            else
            {
                _session = _cluster.Connect();
            }

            _logger?.LogInformation("Connected to Cassandra cluster with keyspace: {Keyspace}", _configuration.Keyspace);
        }

        return _session;
    }

    /// <summary>
    /// Gets a DbSet for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The DbSet for the entity type.</returns>
    public CassandraDbSet<T> Set<T>() where T : class
    {
        var entityType = typeof(T);
        
        if (_entitySets.TryGetValue(entityType, out var existingSet))
        {
            return (CassandraDbSet<T>)existingSet;
        }

        var dbSet = new CassandraDbSet<T>(this);
        _entitySets[entityType] = dbSet;
        return dbSet;
    }    /// <summary>
    /// Executes a raw CQL statement.
    /// </summary>
    /// <param name="cql">The CQL statement to execute.</param>
    /// <param name="parameters">The parameters for the statement.</param>
    /// <returns>The result set.</returns>
    public RowSet ExecuteCql(string cql, params object[] parameters)
    {
        _logger?.LogDebug("Executing CQL: {Cql}", cql);
        
        if (parameters.Length > 0)
        {
            var prepared = Session.Prepare(cql);
            return Session.Execute(prepared.Bind(parameters));
        }
        
        return Session.Execute(new SimpleStatement(cql));
    }

    /// <summary>
    /// Asynchronously executes a raw CQL statement.
    /// </summary>
    /// <param name="cql">The CQL statement to execute.</param>
    /// <param name="parameters">The parameters for the statement.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result set.</returns>
    public async Task<RowSet> ExecuteCqlAsync(string cql, params object[] parameters)
    {
        _logger?.LogDebug("Executing CQL async: {Cql}", cql);
        
        if (parameters.Length > 0)
        {
            var prepared = await Session.PrepareAsync(cql).ConfigureAwait(false);
            return await Session.ExecuteAsync(prepared.Bind(parameters)).ConfigureAwait(false);
        }
        
        return await Session.ExecuteAsync(new SimpleStatement(cql)).ConfigureAwait(false);
    }    /// <summary>
    /// Tracks an entity for the specified state.
    /// </summary>
    /// <param name="entity">The entity to track.</param>
    /// <param name="state">The state to track the entity in.</param>
    internal virtual void TrackEntity(object entity, EntityState state)
    {
        _entityStates[entity] = state;
    }

    /// <summary>
    /// Gets the state of an entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The entity state.</returns>
    internal virtual EntityState GetEntityState(object entity)
    {
        return _entityStates.TryGetValue(entity, out var state) ? state : EntityState.Detached;
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public virtual int SaveChanges()
    {
        var changeCount = 0;
        var batch = new BatchStatement();

        foreach (var (entity, state) in _entityStates.ToList())
        {
            switch (state)
            {
                case EntityState.Added:
                    var insertStatement = GenerateInsertStatement(entity);
                    batch.Add(insertStatement);
                    changeCount++;
                    break;

                case EntityState.Modified:
                    var updateStatement = GenerateUpdateStatement(entity);
                    batch.Add(updateStatement);
                    changeCount++;
                    break;

                case EntityState.Deleted:
                    var deleteStatement = GenerateDeleteStatement(entity);
                    batch.Add(deleteStatement);
                    changeCount++;
                    break;
            }
        }        if (changeCount > 0)
        {
            Session.Execute(batch);
            
            // Clear tracked entities after successful save
            _entityStates.Clear();
        }

        _logger?.LogInformation("Saved {ChangeCount} changes to Cassandra", changeCount);
        return changeCount;
    }

    /// <summary>
    /// Asynchronously saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var changeCount = 0;
        var batch = new BatchStatement();

        foreach (var (entity, state) in _entityStates.ToList())
        {
            switch (state)
            {
                case EntityState.Added:
                    var insertStatement = GenerateInsertStatement(entity);
                    batch.Add(insertStatement);
                    changeCount++;
                    break;

                case EntityState.Modified:
                    var updateStatement = GenerateUpdateStatement(entity);
                    batch.Add(updateStatement);
                    changeCount++;
                    break;

                case EntityState.Deleted:
                    var deleteStatement = GenerateDeleteStatement(entity);
                    batch.Add(deleteStatement);
                    changeCount++;
                    break;
            }
        }        if (changeCount > 0)
        {
            await Session.ExecuteAsync(batch).ConfigureAwait(false);
            
            // Clear tracked entities after successful save
            _entityStates.Clear();
        }

        _logger?.LogInformation("Saved {ChangeCount} changes to Cassandra", changeCount);
        return changeCount;
    }

    private SimpleStatement GenerateInsertStatement(object entity)
    {
        var metadata = EntityMetadataCache.GetMetadata(entity.GetType());
        var columns = string.Join(", ", metadata.Properties.Select(p => metadata.GetColumnName(p)));
        var values = string.Join(", ", metadata.Properties.Select((_, i) => $"?"));
        var cql = $"INSERT INTO {metadata.GetFullTableName()} ({columns}) VALUES ({values})";
        
        var parameters = metadata.Properties.Select(p => p.GetValue(entity)).ToArray();
        return new SimpleStatement(cql, parameters);
    }

    private SimpleStatement GenerateUpdateStatement(object entity)
    {
        var metadata = EntityMetadataCache.GetMetadata(entity.GetType());
        var nonKeyProperties = metadata.Properties
            .Except(metadata.PartitionKeys)
            .Except(metadata.ClusteringKeys)
            .ToList();

        var setClauses = string.Join(", ", nonKeyProperties.Select(p => $"{metadata.GetColumnName(p)} = ?"));
        var whereClause = string.Join(" AND ", 
            metadata.PartitionKeys.Concat(metadata.ClusteringKeys)
                .Select(p => $"{metadata.GetColumnName(p)} = ?"));

        var cql = $"UPDATE {metadata.GetFullTableName()} SET {setClauses} WHERE {whereClause}";
        
        var setParameters = nonKeyProperties.Select(p => p.GetValue(entity));
        var whereParameters = metadata.PartitionKeys.Concat(metadata.ClusteringKeys).Select(p => p.GetValue(entity));
        var parameters = setParameters.Concat(whereParameters).ToArray();

        return new SimpleStatement(cql, parameters);
    }

    private SimpleStatement GenerateDeleteStatement(object entity)
    {
        var metadata = EntityMetadataCache.GetMetadata(entity.GetType());
        var whereClause = string.Join(" AND ", 
            metadata.PartitionKeys.Concat(metadata.ClusteringKeys)
                .Select(p => $"{metadata.GetColumnName(p)} = ?"));

        var cql = $"DELETE FROM {metadata.GetFullTableName()} WHERE {whereClause}";
        var parameters = metadata.PartitionKeys.Concat(metadata.ClusteringKeys).Select(p => p.GetValue(entity)).ToArray();

        return new SimpleStatement(cql, parameters);
    }

    /// <summary>
    /// Creates the database schema if it doesn't exist.
    /// </summary>
    public virtual void EnsureCreated()
    {
        // Create keyspace if it doesn't exist and auto-creation is enabled
        if (_configuration.AutoCreateKeyspace && !string.IsNullOrEmpty(_configuration.Keyspace))
        {
            CreateKeyspaceIfNotExists();
        }

        // Create tables for all registered entity types
        foreach (var entityType in _entitySets.Keys)
        {
            CreateTableIfNotExists(entityType);
        }
    }

    /// <summary>
    /// Asynchronously creates the database schema if it doesn't exist.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        // Create keyspace if it doesn't exist and auto-creation is enabled
        if (_configuration.AutoCreateKeyspace && !string.IsNullOrEmpty(_configuration.Keyspace))
        {
            await CreateKeyspaceIfNotExistsAsync().ConfigureAwait(false);
        }

        // Create tables for all registered entity types
        foreach (var entityType in _entitySets.Keys)
        {
            await CreateTableIfNotExistsAsync(entityType).ConfigureAwait(false);
        }
    }

    private void CreateKeyspaceIfNotExists()
    {
        var replicationStrategy = _configuration.UseNetworkTopologyStrategy
            ? $"'class': 'NetworkTopologyStrategy', {string.Join(", ", _configuration.DataCenterReplicationFactors.Select(kv => $"'{kv.Key}': {kv.Value}"))}"
            : $"'class': 'SimpleStrategy', 'replication_factor': {_configuration.ReplicationFactor}";        var cql = $@"
            CREATE KEYSPACE IF NOT EXISTS {_configuration.Keyspace}
            WITH REPLICATION = {{ {replicationStrategy} }}";

        Session.Execute(new SimpleStatement(cql));
        _logger?.LogInformation("Created keyspace: {Keyspace}", _configuration.Keyspace);
    }

    private async Task CreateKeyspaceIfNotExistsAsync()
    {
        var replicationStrategy = _configuration.UseNetworkTopologyStrategy
            ? $"'class': 'NetworkTopologyStrategy', {string.Join(", ", _configuration.DataCenterReplicationFactors.Select(kv => $"'{kv.Key}': {kv.Value}"))}"
            : $"'class': 'SimpleStrategy', 'replication_factor': {_configuration.ReplicationFactor}";        var cql = $@"
            CREATE KEYSPACE IF NOT EXISTS {_configuration.Keyspace}
            WITH REPLICATION = {{ {replicationStrategy} }}";

        await Session.ExecuteAsync(new SimpleStatement(cql)).ConfigureAwait(false);
        _logger?.LogInformation("Created keyspace: {Keyspace}", _configuration.Keyspace);
    }    private void CreateTableIfNotExists(Type entityType)
    {
        var metadata = EntityMetadataCache.GetMetadata(entityType);
        var cql = GenerateCreateTableCql(metadata);
        Session.Execute(new SimpleStatement(cql));
        _logger?.LogInformation("Created table: {TableName}", metadata.GetFullTableName());
    }    private async Task CreateTableIfNotExistsAsync(Type entityType)
    {
        var metadata = EntityMetadataCache.GetMetadata(entityType);
        var cql = GenerateCreateTableCql(metadata);
        await Session.ExecuteAsync(new SimpleStatement(cql)).ConfigureAwait(false);
        _logger?.LogInformation("Created table: {TableName}", metadata.GetFullTableName());
    }

    private string GenerateCreateTableCql(EntityMetadata metadata)
    {
        var columns = metadata.Properties.Select(p =>
        {
            var columnName = metadata.GetColumnName(p);
            var columnType = GetCassandraType(p.PropertyType);
            var isStatic = metadata.StaticColumns.Contains(p) ? " STATIC" : "";
            return $"{columnName} {columnType}{isStatic}";
        });

        var partitionKeyColumns = string.Join(", ", metadata.PartitionKeys.Select(p => metadata.GetColumnName(p)));
        var clusteringKeyColumns = metadata.ClusteringKeys.Any()
            ? ", " + string.Join(", ", metadata.ClusteringKeys.Select(p => metadata.GetColumnName(p)))
            : "";

        var primaryKey = metadata.PartitionKeys.Count == 1 && !metadata.ClusteringKeys.Any()
            ? partitionKeyColumns
            : $"({partitionKeyColumns}){clusteringKeyColumns}";

        var clusteringOrder = "";
        if (metadata.ClusteringKeys.Any())
        {
            var orderClauses = metadata.ClusteringKeys.Select(p =>
            {
                var columnName = metadata.GetColumnName(p);
                var clusteringAttr = p.GetCustomAttribute<ClusteringKeyAttribute>()!;
                var order = clusteringAttr.Descending ? "DESC" : "ASC";
                return $"{columnName} {order}";
            });
            clusteringOrder = $" WITH CLUSTERING ORDER BY ({string.Join(", ", orderClauses)})";
        }

        return $@"
            CREATE TABLE IF NOT EXISTS {metadata.GetFullTableName()} (
                {string.Join(",\n                ", columns)},
                PRIMARY KEY ({primaryKey})
            ){clusteringOrder}";
    }

    private string GetCassandraType(Type clrType)
    {
        // Handle nullable types
        if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            clrType = Nullable.GetUnderlyingType(clrType)!;
        }        return clrType.Name switch
        {
            nameof(String) => "text",
            nameof(Int32) => "int",
            nameof(Int64) => "bigint",
            nameof(Boolean) => "boolean",
            nameof(Double) => "double",
            nameof(Single) => "float",
            nameof(Decimal) => "decimal",
            nameof(Guid) => "uuid",
            nameof(DateTime) => "timestamp",
            nameof(DateTimeOffset) => "timestamp",
            nameof(TimeUuid) => "timeuuid",
            nameof(Byte) + "[]" => "blob",
            _ => "text" // Default fallback
        };
    }

    #region Advanced Features

    /// <summary>
    /// Registers a User-Defined Type in the context.
    /// </summary>
    public void RegisterUdt<T>() where T : class
    {
        _udtRegistry.RegisterUdt<T>();
    }

    /// <summary>
    /// Registers a materialized view in the context.
    /// </summary>
    public void RegisterView<TView, TBaseEntity>() 
        where TView : class 
        where TBaseEntity : class
    {
        _viewRegistry.RegisterView<TView, TBaseEntity>();
    }

    /// <summary>
    /// Creates all registered materialized views.
    /// </summary>
    public async Task CreateViewsAsync()
    {
        var viewManager = new MaterializedViewManager(_viewRegistry, Session);
        await viewManager.CreateAllViewsAsync();
    }

    /// <summary>
    /// Drops and recreates all registered materialized views.
    /// </summary>
    public async Task RefreshViewsAsync()
    {
        var viewManager = new MaterializedViewManager(_viewRegistry, Session);        foreach (var viewType in _viewRegistry.GetRegisteredViewTypes())
        {
            // Use reflection to call the generic method since RefreshViewAsync<T>() is generic
            var method = typeof(MaterializedViewManager).GetMethod("RefreshViewAsync")!;
            var genericMethod = method.MakeGenericMethod(viewType);
            await (Task)genericMethod.Invoke(viewManager, null)!;
        }
    }

    /// <summary>
    /// Executes collection updates using the collection update builder.
    /// </summary>
    public async Task<int> SaveCollectionChangesAsync<T>(T entity) where T : class
    {
        var updates = _collectionUpdates.GetUpdates();
        if (!updates.Any()) return 0;

        var metadata = EntityMetadataCache.GetMetadata(typeof(T));
        var whereClause = GenerateWhereClauseForEntity(entity, metadata);
        var changeCount = 0;

        foreach (var update in updates)
        {
            var columnName = metadata.Properties
                .FirstOrDefault(p => p.Name == update.PropertyName)?.Name?.ToLowerInvariant() 
                ?? update.PropertyName.ToLowerInvariant();

            var cql = update.Operation switch
            {
                CollectionOperation.ListAppend => $"UPDATE {metadata.GetFullTableName()} SET {columnName} = {columnName} + [?] WHERE {whereClause}",
                CollectionOperation.ListPrepend => $"UPDATE {metadata.GetFullTableName()} SET {columnName} = [?] + {columnName} WHERE {whereClause}",
                CollectionOperation.SetAdd => $"UPDATE {metadata.GetFullTableName()} SET {columnName} = {columnName} + {{?}} WHERE {whereClause}",
                CollectionOperation.SetRemove => $"UPDATE {metadata.GetFullTableName()} SET {columnName} = {columnName} - {{?}} WHERE {whereClause}",
                CollectionOperation.MapPut => $"UPDATE {metadata.GetFullTableName()} SET {columnName}[?] = ? WHERE {whereClause}",
                CollectionOperation.MapRemove => $"DELETE {columnName}[?] FROM {metadata.GetFullTableName()} WHERE {whereClause}",
                _ => throw new NotSupportedException($"Collection operation {update.Operation} is not supported.")
            };

            var parameters = new List<object>();
            if (update.Operation == CollectionOperation.MapPut)
            {
                parameters.Add(update.Key!);
                parameters.Add(update.Value!);
            }
            else if (update.Operation == CollectionOperation.MapRemove)
            {
                parameters.Add(update.Key!);
            }
            else
            {
                parameters.Add(update.Value!);
            }

            // Add WHERE clause parameters
            var keyValues = GetPrimaryKeyValues(entity, metadata);
            parameters.AddRange(keyValues);

            await Session.ExecuteAsync(new SimpleStatement(cql, parameters.ToArray()));
            changeCount++;
        }

        _collectionUpdates.Clear();
        return changeCount;
    }

    private string GenerateWhereClauseForEntity<T>(T entity, EntityMetadata metadata) where T : class
    {
        var keyProperties = metadata.PartitionKeys.Concat(metadata.ClusteringKeys);
        return string.Join(" AND ", keyProperties.Select(p => $"{metadata.GetColumnName(p)} = ?"));
    }

    private object[] GetPrimaryKeyValues<T>(T entity, EntityMetadata metadata) where T : class
    {
        var keyProperties = metadata.PartitionKeys.Concat(metadata.ClusteringKeys);
        return keyProperties.Select(p => p.GetValue(entity) ?? throw new InvalidOperationException($"Primary key property {p.Name} cannot be null")).ToArray();
    }

    /// <summary>
    /// Gets a DbSet for a materialized view.
    /// </summary>
    public CassandraDbSet<TView> View<TView>() where TView : class
    {
        var viewType = typeof(TView);
        
        if (!_viewRegistry.IsView(viewType))
        {
            throw new InvalidOperationException($"Type {viewType.Name} is not registered as a materialized view.");
        }

        if (_entitySets.TryGetValue(viewType, out var existingSet))
        {
            return (CassandraDbSet<TView>)existingSet;
        }

        var dbSet = new CassandraDbSet<TView>(this);
        _entitySets[viewType] = dbSet;
        return dbSet;
    }

    /// <summary>
    /// Registers all UDTs and views from the current assembly.
    /// </summary>
    public void RegisterTypesFromAssembly(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        
        // Register UDTs
        _udtRegistry.RegisterUdtsFromAssembly(assembly);
        
        // Register materialized views
        var viewTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MaterializedViewAttribute>() != null);

        foreach (var viewType in viewTypes)
        {
            // Find the base entity type (this is simplified - in real implementation would be more sophisticated)
            var baseTableName = viewType.GetCustomAttribute<MaterializedViewAttribute>()?.BaseTable;            var baseEntityType = assembly.GetTypes()
                .FirstOrDefault(t => t.GetCustomAttribute<TableAttribute>()?.Name == baseTableName);

            if (baseEntityType != null)
            {
                _viewRegistry.RegisterView(viewType, baseEntityType);
            }
        }
    }

    #endregion

    /// <summary>
    /// Releases the underlying Cassandra session and cluster.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this context.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _session?.Dispose();
                _cluster?.Dispose();
                _logger?.LogInformation("Disposed Cassandra context");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Asynchronously releases the underlying Cassandra session and cluster.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases the unmanaged resources used by this context.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_session != null)
        {
            await _session.ShutdownAsync().ConfigureAwait(false);
        }
        
        // Cluster doesn't have an async disposal method
        _cluster?.Dispose();
        _logger?.LogInformation("Disposed Cassandra context async");
    }
}

/// <summary>
/// Represents the state of an entity.
/// </summary>
public enum EntityState
{
    /// <summary>
    /// The entity is not being tracked by the context.
    /// </summary>
    Detached,

    /// <summary>
    /// The entity is being tracked and exists in the database. Its property values have not changed.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The entity is being tracked and exists in the database. Some of its property values have been modified.
    /// </summary>
    Modified,

    /// <summary>
    /// The entity is being tracked but does not yet exist in the database.
    /// </summary>
    Added,

    /// <summary>
    /// The entity is being tracked and exists in the database but has been marked for deletion.
    /// </summary>
    Deleted
}

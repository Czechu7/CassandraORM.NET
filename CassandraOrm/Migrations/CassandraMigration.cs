using Cassandra;
using CassandraOrm.Core;
using Microsoft.Extensions.Logging;

namespace CassandraOrm.Migrations;

/// <summary>
/// Base class for Cassandra migrations.
/// </summary>
public abstract class CassandraMigration
{
    /// <summary>
    /// Gets the version of the migration.
    /// </summary>
    public abstract long Version { get; }

    /// <summary>
    /// Gets the description of the migration.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Applies the migration to the database.
    /// </summary>
    /// <param name="session">The Cassandra session to use.</param>
    public abstract void Up(ISession session);

    /// <summary>
    /// Reverts the migration from the database.
    /// </summary>
    /// <param name="session">The Cassandra session to use.</param>
    public abstract void Down(ISession session);

    /// <summary>
    /// Asynchronously applies the migration to the database.
    /// </summary>
    /// <param name="session">The Cassandra session to use.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task UpAsync(ISession session)
    {
        Up(session);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously reverts the migration from the database.
    /// </summary>
    /// <param name="session">The Cassandra session to use.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task DownAsync(ISession session)
    {
        Down(session);
        return Task.CompletedTask;
    }    /// <summary>
    /// Executes a CQL statement during migration.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="cql">The CQL statement to execute.</param>
    protected void Execute(ISession session, string cql)
    {
        session.Execute(new SimpleStatement(cql));
    }

    /// <summary>
    /// Asynchronously executes a CQL statement during migration.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="cql">The CQL statement to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task ExecuteAsync(ISession session, string cql)
    {
        await session.ExecuteAsync(new SimpleStatement(cql)).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a table with the specified definition.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columns">The column definitions.</param>
    /// <param name="primaryKey">The primary key definition.</param>
    /// <param name="additionalOptions">Additional table options.</param>
    protected void CreateTable(ISession session, string tableName, string columns, string primaryKey, string? additionalOptions = null)
    {
        var cql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                {columns},
                PRIMARY KEY ({primaryKey})
            ){additionalOptions}";

        Execute(session, cql);
    }

    /// <summary>
    /// Drops a table.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="tableName">The name of the table to drop.</param>
    protected void DropTable(ISession session, string tableName)
    {
        Execute(session, $"DROP TABLE IF EXISTS {tableName}");
    }

    /// <summary>
    /// Adds a column to an existing table.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the new column.</param>
    /// <param name="columnType">The type of the new column.</param>
    protected void AddColumn(ISession session, string tableName, string columnName, string columnType)
    {
        Execute(session, $"ALTER TABLE {tableName} ADD {columnName} {columnType}");
    }

    /// <summary>
    /// Drops a column from an existing table.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column to drop.</param>
    protected void DropColumn(ISession session, string tableName, string columnName)
    {
        Execute(session, $"ALTER TABLE {tableName} DROP {columnName}");
    }

    /// <summary>
    /// Creates an index on a table column.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="columnName">The name of the column to index.</param>
    protected void CreateIndex(ISession session, string indexName, string tableName, string columnName)
    {
        Execute(session, $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableName} ({columnName})");
    }

    /// <summary>
    /// Drops an index.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="indexName">The name of the index to drop.</param>
    protected void DropIndex(ISession session, string indexName)
    {
        Execute(session, $"DROP INDEX IF EXISTS {indexName}");
    }

    /// <summary>
    /// Creates a user-defined type.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="fields">The field definitions.</param>
    protected void CreateType(ISession session, string typeName, string fields)
    {
        Execute(session, $"CREATE TYPE IF NOT EXISTS {typeName} ({fields})");
    }

    /// <summary>
    /// Drops a user-defined type.
    /// </summary>
    /// <param name="session">The Cassandra session.</param>
    /// <param name="typeName">The name of the type to drop.</param>
    protected void DropType(ISession session, string typeName)
    {
        Execute(session, $"DROP TYPE IF EXISTS {typeName}");
    }
}

/// <summary>
/// Manages database migrations for Cassandra.
/// </summary>
public class CassandraMigrationManager
{
    private readonly CassandraDbContext _context;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the CassandraMigrationManager class.
    /// </summary>
    /// <param name="context">The context to manage migrations for.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public CassandraMigrationManager(CassandraDbContext context, ILogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    /// <summary>
    /// Ensures that the migrations table exists.
    /// </summary>
    public void EnsureMigrationsTableExists()
    {        var cql = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version bigint PRIMARY KEY,
                description text,
                applied_at timestamp
            )";

        _context.Session.Execute(new SimpleStatement(cql));
        _logger?.LogInformation("Ensured schema_migrations table exists");
    }

    /// <summary>
    /// Asynchronously ensures that the migrations table exists.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EnsureMigrationsTableExistsAsync()
    {
        var cql = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version bigint PRIMARY KEY,
                description text,
                applied_at timestamp
            )";

        await _context.Session.ExecuteAsync(new SimpleStatement(cql)).ConfigureAwait(false);
        _logger?.LogInformation("Ensured schema_migrations table exists");
    }

    /// <summary>
    /// Gets all applied migration versions.
    /// </summary>
    /// <returns>A set of applied migration versions.</returns>
    public HashSet<long> GetAppliedMigrations()
    {
        EnsureMigrationsTableExists();

        var result = _context.Session.Execute(new SimpleStatement("SELECT version FROM schema_migrations"));
        return new HashSet<long>(result.Select(row => row.GetValue<long>("version")));
    }

    /// <summary>
    /// Asynchronously gets all applied migration versions.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a set of applied migration versions.</returns>
    public async Task<HashSet<long>> GetAppliedMigrationsAsync()
    {
        await EnsureMigrationsTableExistsAsync().ConfigureAwait(false);

        var result = await _context.Session.ExecuteAsync(new SimpleStatement("SELECT version FROM schema_migrations")).ConfigureAwait(false);
        return new HashSet<long>(result.Select(row => row.GetValue<long>("version")));
    }

    /// <summary>
    /// Applies all pending migrations.
    /// </summary>
    /// <param name="migrations">The migrations to apply.</param>
    public void MigrateUp(params CassandraMigration[] migrations)
    {
        var appliedMigrations = GetAppliedMigrations();
        var pendingMigrations = migrations
            .Where(m => !appliedMigrations.Contains(m.Version))
            .OrderBy(m => m.Version)
            .ToList();

        _logger?.LogInformation("Found {PendingCount} pending migrations", pendingMigrations.Count);

        foreach (var migration in pendingMigrations)
        {
            _logger?.LogInformation("Applying migration {Version}: {Description}", migration.Version, migration.Description);

            try
            {
                // Apply the migration
                migration.Up(_context.Session);

                // Record the migration
                RecordMigration(migration);

                _logger?.LogInformation("Successfully applied migration {Version}", migration.Version);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply migration {Version}: {Description}", migration.Version, migration.Description);
                throw;
            }
        }
    }

    /// <summary>
    /// Asynchronously applies all pending migrations.
    /// </summary>
    /// <param name="migrations">The migrations to apply.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task MigrateUpAsync(params CassandraMigration[] migrations)
    {
        var appliedMigrations = await GetAppliedMigrationsAsync().ConfigureAwait(false);
        var pendingMigrations = migrations
            .Where(m => !appliedMigrations.Contains(m.Version))
            .OrderBy(m => m.Version)
            .ToList();

        _logger?.LogInformation("Found {PendingCount} pending migrations", pendingMigrations.Count);

        foreach (var migration in pendingMigrations)
        {
            _logger?.LogInformation("Applying migration {Version}: {Description}", migration.Version, migration.Description);

            try
            {
                // Apply the migration
                await migration.UpAsync(_context.Session).ConfigureAwait(false);

                // Record the migration
                await RecordMigrationAsync(migration).ConfigureAwait(false);

                _logger?.LogInformation("Successfully applied migration {Version}", migration.Version);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to apply migration {Version}: {Description}", migration.Version, migration.Description);
                throw;
            }
        }
    }

    /// <summary>
    /// Reverts migrations down to the specified target version.
    /// </summary>
    /// <param name="targetVersion">The target version to migrate down to.</param>
    /// <param name="migrations">All available migrations.</param>
    public void MigrateDown(long targetVersion, params CassandraMigration[] migrations)
    {
        var appliedMigrations = GetAppliedMigrations();
        var migrationsToRevert = migrations
            .Where(m => appliedMigrations.Contains(m.Version) && m.Version > targetVersion)
            .OrderByDescending(m => m.Version)
            .ToList();

        _logger?.LogInformation("Found {RevertCount} migrations to revert", migrationsToRevert.Count);

        foreach (var migration in migrationsToRevert)
        {
            _logger?.LogInformation("Reverting migration {Version}: {Description}", migration.Version, migration.Description);

            try
            {
                // Revert the migration
                migration.Down(_context.Session);

                // Remove the migration record
                RemoveMigrationRecord(migration.Version);

                _logger?.LogInformation("Successfully reverted migration {Version}", migration.Version);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to revert migration {Version}: {Description}", migration.Version, migration.Description);
                throw;
            }
        }
    }

    /// <summary>
    /// Asynchronously reverts migrations down to the specified target version.
    /// </summary>
    /// <param name="targetVersion">The target version to migrate down to.</param>
    /// <param name="migrations">All available migrations.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task MigrateDownAsync(long targetVersion, params CassandraMigration[] migrations)
    {
        var appliedMigrations = await GetAppliedMigrationsAsync().ConfigureAwait(false);
        var migrationsToRevert = migrations
            .Where(m => appliedMigrations.Contains(m.Version) && m.Version > targetVersion)
            .OrderByDescending(m => m.Version)
            .ToList();

        _logger?.LogInformation("Found {RevertCount} migrations to revert", migrationsToRevert.Count);

        foreach (var migration in migrationsToRevert)
        {
            _logger?.LogInformation("Reverting migration {Version}: {Description}", migration.Version, migration.Description);

            try
            {
                // Revert the migration
                await migration.DownAsync(_context.Session).ConfigureAwait(false);

                // Remove the migration record
                await RemoveMigrationRecordAsync(migration.Version).ConfigureAwait(false);

                _logger?.LogInformation("Successfully reverted migration {Version}", migration.Version);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to revert migration {Version}: {Description}", migration.Version, migration.Description);
                throw;
            }
        }
    }

    private void RecordMigration(CassandraMigration migration)
    {
        var insert = _context.Session.Prepare(
            "INSERT INTO schema_migrations (version, description, applied_at) VALUES (?, ?, ?)");
        _context.Session.Execute(insert.Bind(migration.Version, migration.Description, DateTimeOffset.UtcNow));
    }

    private async Task RecordMigrationAsync(CassandraMigration migration)
    {
        var insert = await _context.Session.PrepareAsync(
            "INSERT INTO schema_migrations (version, description, applied_at) VALUES (?, ?, ?)").ConfigureAwait(false);
        await _context.Session.ExecuteAsync(insert.Bind(migration.Version, migration.Description, DateTimeOffset.UtcNow))
            .ConfigureAwait(false);
    }

    private void RemoveMigrationRecord(long version)
    {
        var delete = _context.Session.Prepare("DELETE FROM schema_migrations WHERE version = ?");
        _context.Session.Execute(delete.Bind(version));
    }

    private async Task RemoveMigrationRecordAsync(long version)
    {
        var delete = await _context.Session.PrepareAsync("DELETE FROM schema_migrations WHERE version = ?").ConfigureAwait(false);
        await _context.Session.ExecuteAsync(delete.Bind(version)).ConfigureAwait(false);
    }
}

/// <summary>
/// Attribute to mark a class as a migration with automatic version detection.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class MigrationAttribute : Attribute
{
    /// <summary>
    /// Gets the migration version.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the migration description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the MigrationAttribute class.
    /// </summary>
    /// <param name="version">The migration version (typically a timestamp like 20231201120000).</param>
    /// <param name="description">The migration description.</param>
    public MigrationAttribute(long version, string description)
    {
        Version = version;
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}

using System.Collections;
using System.Linq.Expressions;
using System.Text;
using Cassandra;
using CassandraOrm.Core;
using CassandraOrm.Mapping;

namespace CassandraOrm.Query;

/// <summary>
/// Provides functionality to convert LINQ expressions to CQL queries for Cassandra.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public class CassandraQueryProvider<T> : IQueryProvider where T : class
{
    private readonly CassandraDbContext _context;

    /// <summary>
    /// Initializes a new instance of the CassandraQueryProvider class.
    /// </summary>
    /// <param name="context">The context this provider belongs to.</param>
    public CassandraQueryProvider(CassandraDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Creates a query from the specified expression.
    /// </summary>
    /// <param name="expression">The expression to create a query from.</param>
    /// <returns>A query that represents the expression.</returns>
    public IQueryable CreateQuery(Expression expression)
    {
        Type elementType = GetElementType(expression.Type);
        Type queryType = typeof(CassandraQuery<>).MakeGenericType(elementType);        return (IQueryable)Activator.CreateInstance(queryType, this, expression)!;
    }    /// <summary>
    /// Creates a query for a specific type from the specified expression.
    /// </summary>
    /// <typeparam name="TElement">The type of elements to query.</typeparam>
    /// <param name="expression">The expression to create a query from.</param>
    /// <returns>A query that represents the expression.</returns>
    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
    {
        if (!typeof(TElement).IsClass)
        {
            throw new NotSupportedException($"Type {typeof(TElement).Name} must be a reference type");
        }
        
        Type queryType = typeof(CassandraQuery<>).MakeGenericType(typeof(TElement));
        return (IQueryable<TElement>)Activator.CreateInstance(queryType, this, expression)!;
    }

    /// <summary>
    /// Creates a query for a specific type from the specified expression (public method with constraint).
    /// </summary>
    /// <typeparam name="TElement">The type of elements to query.</typeparam>
    /// <param name="expression">The expression to create a query from.</param>
    /// <returns>A query that represents the expression.</returns>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) where TElement : class
    {
        return new CassandraQuery<TElement>((CassandraQueryProvider<TElement>)(object)this, expression);
    }

    /// <summary>
    /// Executes the specified expression.
    /// </summary>
    /// <param name="expression">The expression to execute.</param>
    /// <returns>The result of executing the expression.</returns>
    public object Execute(Expression expression)
    {
        return ExecuteInternal(expression);
    }

    /// <summary>
    /// Executes the specified expression for a specific type.
    /// </summary>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <param name="expression">The expression to execute.</param>
    /// <returns>The result of executing the expression.</returns>
    public TResult Execute<TResult>(Expression expression)
    {
        return (TResult)ExecuteInternal(expression);
    }

    /// <summary>
    /// Asynchronously executes the specified expression for a specific type.
    /// </summary>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <param name="expression">The expression to execute.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of executing the expression.</returns>
    public async Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteInternalAsync(expression, cancellationToken).ConfigureAwait(false);
        return (TResult)result;
    }

    private object ExecuteInternal(Expression expression)
    {
        var translator = new CqlQueryTranslator();
        var queryInfo = translator.Translate(expression);
        
        var result = _context.ExecuteCql(queryInfo.Cql, queryInfo.Parameters.ToArray());
        var metadata = EntityMetadataCache.GetMetadata<T>();

        return ProcessResult(result, queryInfo, metadata);
    }

    private async Task<object> ExecuteInternalAsync(Expression expression, CancellationToken cancellationToken)
    {
        var translator = new CqlQueryTranslator();
        var queryInfo = translator.Translate(expression);
        
        var result = await _context.ExecuteCqlAsync(queryInfo.Cql, queryInfo.Parameters.ToArray()).ConfigureAwait(false);
        var metadata = EntityMetadataCache.GetMetadata<T>();

        return ProcessResult(result, queryInfo, metadata);
    }

    private object ProcessResult(RowSet result, CqlQueryInfo queryInfo, EntityMetadata metadata)
    {
        var entities = result.Select(row => MapRowToEntity(row, metadata)).ToList();

        return queryInfo.ResultType switch
        {
            CqlResultType.Enumerable => entities,
            CqlResultType.Single => entities.Single(),
            CqlResultType.SingleOrDefault => entities.SingleOrDefault(),
            CqlResultType.First => entities.First(),
            CqlResultType.FirstOrDefault => entities.FirstOrDefault(),
            CqlResultType.Count => entities.Count,
            CqlResultType.Any => entities.Any(),
            _ => entities
        };
    }

    private T MapRowToEntity(Row row, EntityMetadata metadata)
    {
        var entity = Activator.CreateInstance<T>();        foreach (var property in metadata.Properties)
        {
            var columnName = metadata.GetColumnName(property);
            
            try
            {
                if (!row.IsNull(columnName))
                {
                    var value = row.GetValue(property.PropertyType, columnName);
                    property.SetValue(entity, value);
                }
            }
            catch (ArgumentException)
            {
                // Column doesn't exist in the result set, skip it
            }
        }

        // Track the entity as unchanged since it came from the database
        _context.TrackEntity(entity, EntityState.Unchanged);

        return entity;
    }

    private static Type GetElementType(Type type)
    {
        if (type.HasElementType)
        {
            return type.GetElementType()!;
        }

        if (type.IsGenericType)
        {
            foreach (Type arg in type.GetGenericArguments())
            {
                Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                if (ienum.IsAssignableFrom(type))
                {
                    return arg;
                }
            }
        }

        Type[] ifaces = type.GetInterfaces();
        if (ifaces.Length > 0)
        {
            foreach (Type iface in ifaces)
            {
                Type elementType = GetElementType(iface);
                if (elementType != typeof(object))
                {
                    return elementType;
                }
            }
        }

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            return GetElementType(type.BaseType);
        }

        return typeof(object);
    }
}

/// <summary>
/// Translates LINQ expressions to CQL.
/// </summary>
public class CqlQueryTranslator : ExpressionVisitor
{
    private readonly StringBuilder _cqlBuilder = new();
    private readonly List<object> _parameters = new();
    private EntityMetadata? _metadata;
    private CqlResultType _resultType = CqlResultType.Enumerable;
    private bool _isWhereClause = false;

    /// <summary>
    /// Translates the expression to CQL.
    /// </summary>
    /// <param name="expression">The expression to translate.</param>
    /// <returns>The translated CQL query information.</returns>
    public CqlQueryInfo Translate(Expression expression)
    {
        Visit(expression);

        return new CqlQueryInfo
        {
            Cql = _cqlBuilder.ToString(),
            Parameters = _parameters,
            ResultType = _resultType
        };
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case "Where":
                VisitWhere(node);
                break;
            case "Select":
                VisitSelect(node);
                break;
            case "OrderBy":
            case "OrderByDescending":
            case "ThenBy":
            case "ThenByDescending":
                VisitOrderBy(node);
                break;
            case "Take":
                VisitTake(node);
                break;
            case "First":
                _resultType = CqlResultType.First;
                Visit(node.Arguments[0]);
                _cqlBuilder.Append(" LIMIT 1");
                break;
            case "FirstOrDefault":
                _resultType = CqlResultType.FirstOrDefault;
                Visit(node.Arguments[0]);
                _cqlBuilder.Append(" LIMIT 1");
                break;
            case "Single":
                _resultType = CqlResultType.Single;
                Visit(node.Arguments[0]);
                _cqlBuilder.Append(" LIMIT 2"); // Get 2 to verify single
                break;
            case "SingleOrDefault":
                _resultType = CqlResultType.SingleOrDefault;
                Visit(node.Arguments[0]);
                _cqlBuilder.Append(" LIMIT 2"); // Get 2 to verify single
                break;
            case "Count":
                _resultType = CqlResultType.Count;
                _cqlBuilder.Append("SELECT COUNT(*) FROM ");
                VisitTableReference(node.Arguments[0]);
                if (node.Arguments.Count > 1)
                {
                    _cqlBuilder.Append(" WHERE ");
                    _isWhereClause = true;
                    Visit(node.Arguments[1]);
                    _isWhereClause = false;
                }
                break;
            case "Any":
                _resultType = CqlResultType.Any;
                _cqlBuilder.Append("SELECT * FROM ");
                VisitTableReference(node.Arguments[0]);
                if (node.Arguments.Count > 1)
                {
                    _cqlBuilder.Append(" WHERE ");
                    _isWhereClause = true;
                    Visit(node.Arguments[1]);
                    _isWhereClause = false;
                }
                _cqlBuilder.Append(" LIMIT 1");
                break;
            default:
                return base.VisitMethodCall(node);
        }

        return node;
    }

    private void VisitWhere(MethodCallExpression node)
    {
        if (_cqlBuilder.Length == 0)
        {
            _cqlBuilder.Append("SELECT * FROM ");
            VisitTableReference(node.Arguments[0]);
        }

        _cqlBuilder.Append(" WHERE ");
        _isWhereClause = true;
        
        if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
        {
            Visit(lambda.Body);
        }
        
        _isWhereClause = false;
    }

    private void VisitSelect(MethodCallExpression node)
    {
        // For now, we'll just support selecting the full entity
        // TODO: Implement projection support
        Visit(node.Arguments[0]);
    }

    private void VisitOrderBy(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);
        
        // Cassandra requires clustering columns for ORDER BY
        // This is a simplified implementation
        if (!_cqlBuilder.ToString().Contains("ORDER BY"))
        {
            _cqlBuilder.Append(" ORDER BY ");
        }
        else
        {
            _cqlBuilder.Append(", ");
        }

        if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
        {
            VisitOrderByExpression(lambda.Body);
        }

        if (node.Method.Name.EndsWith("Descending"))
        {
            _cqlBuilder.Append(" DESC");
        }
    }

    private void VisitTake(MethodCallExpression node)
    {
        Visit(node.Arguments[0]);
        
        if (node.Arguments[1] is ConstantExpression constant)
        {
            _cqlBuilder.Append($" LIMIT {constant.Value}");
        }
    }

    private void VisitTableReference(Expression expression)
    {
        // Extract the entity type from the expression
        Type entityType = GetEntityTypeFromExpression(expression);
        _metadata = EntityMetadataCache.GetMetadata(entityType);
        _cqlBuilder.Append(_metadata.GetFullTableName());
    }

    private void VisitOrderByExpression(Expression expression)
    {
        if (expression is MemberExpression member)
        {
            var columnName = _metadata?.GetColumnName(member.Member as System.Reflection.PropertyInfo);
            if (columnName != null)
            {
                _cqlBuilder.Append(columnName);
            }
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (_isWhereClause)
        {
            Visit(node.Left);
            
            var op = node.NodeType switch
            {
                ExpressionType.Equal => " = ",
                ExpressionType.NotEqual => " != ",
                ExpressionType.GreaterThan => " > ",
                ExpressionType.GreaterThanOrEqual => " >= ",
                ExpressionType.LessThan => " < ",
                ExpressionType.LessThanOrEqual => " <= ",
                ExpressionType.AndAlso => " AND ",
                ExpressionType.OrElse => " OR ",
                _ => throw new NotSupportedException($"Binary operator {node.NodeType} is not supported")
            };
            
            _cqlBuilder.Append(op);
            Visit(node.Right);
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (_isWhereClause && node.Member is System.Reflection.PropertyInfo property)
        {
            var columnName = _metadata?.GetColumnName(property);
            if (columnName != null)
            {
                _cqlBuilder.Append(columnName);
            }
        }

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (_isWhereClause && node.Value != null)
        {
            _parameters.Add(node.Value);
            _cqlBuilder.Append("?");
        }
        else if (node.Value is IQueryable)
        {
            // This is the root table reference, handle it appropriately
            if (_cqlBuilder.Length == 0)
            {
                _cqlBuilder.Append("SELECT * FROM ");
                Type entityType = GetEntityTypeFromQueryable(node.Value);
                _metadata = EntityMetadataCache.GetMetadata(entityType);
                _cqlBuilder.Append(_metadata.GetFullTableName());
            }
        }

        return node;
    }

    private Type GetEntityTypeFromExpression(Expression expression)
    {
        if (expression is ConstantExpression constant && constant.Value is IQueryable queryable)
        {
            return GetEntityTypeFromQueryable(queryable);
        }

        return expression.Type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
    }

    private Type GetEntityTypeFromQueryable(object queryable)
    {
        var queryableType = queryable.GetType();
        
        // Handle CassandraDbSet<T>
        if (queryableType.IsGenericType && queryableType.GetGenericTypeDefinition() == typeof(CassandraDbSet<>))
        {
            return queryableType.GetGenericArguments()[0];
        }

        // Handle other IQueryable implementations
        var interfaces = queryableType.GetInterfaces();
        var queryableInterface = interfaces.FirstOrDefault(i => 
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));

        return queryableInterface?.GetGenericArguments()[0] ?? typeof(object);
    }
}

/// <summary>
/// Contains information about a translated CQL query.
/// </summary>
public class CqlQueryInfo
{
    /// <summary>
    /// Gets or sets the CQL statement.
    /// </summary>
    public string Cql { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters for the query.
    /// </summary>
    public List<object> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the type of result expected.
    /// </summary>
    public CqlResultType ResultType { get; set; }
}

/// <summary>
/// Specifies the type of result expected from a CQL query.
/// </summary>
public enum CqlResultType
{
    /// <summary>
    /// Return an enumerable of results.
    /// </summary>
    Enumerable,

    /// <summary>
    /// Return a single result, throw if none or multiple.
    /// </summary>
    Single,

    /// <summary>
    /// Return a single result or default, throw if multiple.
    /// </summary>
    SingleOrDefault,

    /// <summary>
    /// Return the first result, throw if none.
    /// </summary>
    First,

    /// <summary>
    /// Return the first result or default.
    /// </summary>
    FirstOrDefault,

    /// <summary>
    /// Return the count of results.
    /// </summary>
    Count,

    /// <summary>
    /// Return whether any results exist.
    /// </summary>
    Any
}

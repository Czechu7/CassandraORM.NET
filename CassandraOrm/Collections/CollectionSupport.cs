using System.Collections;
using System.Reflection;
using CassandraOrm.Mapping;

namespace CassandraOrm.Collections;

/// <summary>
/// Marks a property as a Cassandra collection (List, Set, or Map)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CollectionAttribute : Attribute
{
    public CollectionType CollectionType { get; }
    public Type? KeyType { get; set; }
    public Type? ValueType { get; set; }

    public CollectionAttribute(CollectionType collectionType)
    {
        CollectionType = collectionType;
    }
}

/// <summary>
/// Types of Cassandra collections
/// </summary>
public enum CollectionType
{
    List,
    Set,
    Map
}

/// <summary>
/// Metadata for collection properties
/// </summary>
public class CollectionMetadata
{
    public string PropertyName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public CollectionType CollectionType { get; set; }
    public Type PropertyType { get; set; } = null!;
    public Type? ElementType { get; set; }
    public Type? KeyType { get; set; }
    public Type? ValueType { get; set; }
    public PropertyInfo PropertyInfo { get; set; } = null!;

    public static CollectionMetadata? Create(PropertyInfo property)
    {
        var collectionAttribute = property.GetCustomAttribute<CollectionAttribute>();
        if (collectionAttribute == null)
        {
            // Try to infer collection type from property type
            var propertyType = property.PropertyType;
            
            if (IsListType(propertyType))
            {
                return CreateForList(property, propertyType);
            }
            else if (IsSetType(propertyType))
            {
                return CreateForSet(property, propertyType);
            }
            else if (IsMapType(propertyType))
            {
                return CreateForMap(property, propertyType);
            }
            
            return null;
        }

        var metadata = new CollectionMetadata
        {
            PropertyName = property.Name,
            ColumnName = GetColumnName(property),
            CollectionType = collectionAttribute.CollectionType,
            PropertyType = property.PropertyType,
            PropertyInfo = property
        };

        // Determine element types
        switch (collectionAttribute.CollectionType)
        {
            case CollectionType.List:
            case CollectionType.Set:
                metadata.ElementType = collectionAttribute.ValueType ?? GetGenericArgument(property.PropertyType, 0);
                break;
            case CollectionType.Map:
                metadata.KeyType = collectionAttribute.KeyType ?? GetGenericArgument(property.PropertyType, 0);
                metadata.ValueType = collectionAttribute.ValueType ?? GetGenericArgument(property.PropertyType, 1);
                break;
        }

        return metadata;
    }

    private static CollectionMetadata CreateForList(PropertyInfo property, Type propertyType)
    {
        return new CollectionMetadata
        {
            PropertyName = property.Name,
            ColumnName = GetColumnName(property),
            CollectionType = CollectionType.List,
            PropertyType = propertyType,
            ElementType = GetGenericArgument(propertyType, 0),
            PropertyInfo = property
        };
    }

    private static CollectionMetadata CreateForSet(PropertyInfo property, Type propertyType)
    {
        return new CollectionMetadata
        {
            PropertyName = property.Name,
            ColumnName = GetColumnName(property),
            CollectionType = CollectionType.Set,
            PropertyType = propertyType,
            ElementType = GetGenericArgument(propertyType, 0),
            PropertyInfo = property
        };
    }

    private static CollectionMetadata CreateForMap(PropertyInfo property, Type propertyType)
    {
        return new CollectionMetadata
        {
            PropertyName = property.Name,
            ColumnName = GetColumnName(property),
            CollectionType = CollectionType.Map,
            PropertyType = propertyType,
            KeyType = GetGenericArgument(propertyType, 0),
            ValueType = GetGenericArgument(propertyType, 1),
            PropertyInfo = property
        };
    }    private static string GetColumnName(PropertyInfo property)
    {
        var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
        return columnAttribute?.Name ?? property.Name.ToLowerInvariant();
    }

    private static bool IsListType(Type type)
    {
        return type.IsGenericType && 
               (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    private static bool IsSetType(Type type)
    {
        return type.IsGenericType && 
               (type.GetGenericTypeDefinition() == typeof(HashSet<>) ||
                type.GetGenericTypeDefinition() == typeof(ISet<>));
    }

    private static bool IsMapType(Type type)
    {
        return type.IsGenericType && 
               (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    private static Type? GetGenericArgument(Type type, int index)
    {
        if (!type.IsGenericType) return null;
        var args = type.GetGenericArguments();
        return args.Length > index ? args[index] : null;
    }
}

/// <summary>
/// Collection operations for Cassandra
/// </summary>
public static class CollectionOperations
{
    /// <summary>
    /// Generates CQL for appending to a list
    /// </summary>
    public static string AppendToList(string columnName, object value)
    {
        return $"{columnName} = {columnName} + [?]";
    }

    /// <summary>
    /// Generates CQL for prepending to a list
    /// </summary>
    public static string PrependToList(string columnName, object value)
    {
        return $"{columnName} = [?] + {columnName}";
    }

    /// <summary>
    /// Generates CQL for removing from a list by value
    /// </summary>
    public static string RemoveFromList(string columnName, object value)
    {
        return $"{columnName} = {columnName} - [?]";
    }

    /// <summary>
    /// Generates CQL for adding to a set
    /// </summary>
    public static string AddToSet(string columnName, object value)
    {
        return $"{columnName} = {columnName} + {{?}}";
    }

    /// <summary>
    /// Generates CQL for removing from a set
    /// </summary>
    public static string RemoveFromSet(string columnName, object value)
    {
        return $"{columnName} = {columnName} - {{?}}";
    }

    /// <summary>
    /// Generates CQL for adding to a map
    /// </summary>
    public static string AddToMap(string columnName, object key, object value)
    {
        return $"{columnName}[?] = ?";
    }

    /// <summary>
    /// Generates CQL for removing from a map by key
    /// </summary>
    public static string RemoveFromMap(string columnName, object key)
    {
        return $"DELETE {columnName}[?]";
    }

    /// <summary>
    /// Generates CQL for updating an entire collection
    /// </summary>
    public static string UpdateCollection(string columnName, CollectionType collectionType)
    {
        return $"{columnName} = ?";
    }
}

/// <summary>
/// Helper for working with collection updates
/// </summary>
public class CollectionUpdateBuilder
{
    private readonly List<CollectionUpdate> _updates = new();

    /// <summary>
    /// Adds an append operation to a list
    /// </summary>
    public CollectionUpdateBuilder AppendToList<T>(string propertyName, T value)
    {
        _updates.Add(new CollectionUpdate
        {
            PropertyName = propertyName,
            Operation = CollectionOperation.ListAppend,
            Value = value
        });
        return this;
    }

    /// <summary>
    /// Adds a prepend operation to a list
    /// </summary>
    public CollectionUpdateBuilder PrependToList<T>(string propertyName, T value)
    {
        _updates.Add(new CollectionUpdate
        {
            PropertyName = propertyName,
            Operation = CollectionOperation.ListPrepend,
            Value = value
        });
        return this;
    }

    /// <summary>
    /// Adds an element to a set
    /// </summary>
    public CollectionUpdateBuilder AddToSet<T>(string propertyName, T value)
    {
        _updates.Add(new CollectionUpdate
        {
            PropertyName = propertyName,
            Operation = CollectionOperation.SetAdd,
            Value = value
        });
        return this;
    }

    /// <summary>
    /// Removes an element from a set
    /// </summary>
    public CollectionUpdateBuilder RemoveFromSet<T>(string propertyName, T value)
    {
        _updates.Add(new CollectionUpdate
        {
            PropertyName = propertyName,
            Operation = CollectionOperation.SetRemove,
            Value = value
        });
        return this;
    }

    /// <summary>
    /// Adds or updates a map entry
    /// </summary>
    public CollectionUpdateBuilder PutInMap<TKey, TValue>(string propertyName, TKey key, TValue value)
    {
        _updates.Add(new CollectionUpdate
        {
            PropertyName = propertyName,
            Operation = CollectionOperation.MapPut,
            Key = key,
            Value = value
        });
        return this;
    }

    /// <summary>
    /// Removes a key from a map
    /// </summary>
    public CollectionUpdateBuilder RemoveFromMap<TKey>(string propertyName, TKey key)
    {
        _updates.Add(new CollectionUpdate
        {
            PropertyName = propertyName,
            Operation = CollectionOperation.MapRemove,
            Key = key
        });
        return this;
    }

    /// <summary>
    /// Gets all collection updates
    /// </summary>
    public IReadOnlyList<CollectionUpdate> GetUpdates() => _updates.AsReadOnly();

    /// <summary>
    /// Clears all updates
    /// </summary>
    public void Clear() => _updates.Clear();
}

/// <summary>
/// Represents a collection update operation
/// </summary>
public class CollectionUpdate
{
    public string PropertyName { get; set; } = string.Empty;
    public CollectionOperation Operation { get; set; }
    public object? Key { get; set; }
    public object? Value { get; set; }
}

/// <summary>
/// Types of collection operations
/// </summary>
public enum CollectionOperation
{
    ListAppend,
    ListPrepend,
    ListRemove,
    SetAdd,
    SetRemove,
    MapPut,
    MapRemove,
    Replace
}

/// <summary>
/// Extension methods for collection support
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Creates a collection update builder for an entity
    /// </summary>
    public static CollectionUpdateBuilder UpdateCollections<T>(this T entity) where T : class
    {
        return new CollectionUpdateBuilder();
    }

    /// <summary>
    /// Checks if a type is a supported collection type
    /// </summary>
    public static bool IsSupportedCollectionType(this Type type)
    {
        if (!type.IsGenericType) return false;

        var genericType = type.GetGenericTypeDefinition();
        return genericType == typeof(List<>) ||
               genericType == typeof(IList<>) ||
               genericType == typeof(ICollection<>) ||
               genericType == typeof(HashSet<>) ||
               genericType == typeof(ISet<>) ||
               genericType == typeof(Dictionary<,>) ||
               genericType == typeof(IDictionary<,>);
    }    /// <summary>
    /// Gets the collection type for a property type
    /// </summary>
    public static CollectionType? GetCollectionType(this Type type)
    {
        if (!type.IsGenericType) return null;

        var genericType = type.GetGenericTypeDefinition();
        
        if (genericType == typeof(List<>) || 
            genericType == typeof(IList<>) || 
            genericType == typeof(ICollection<>))
        {
            return CollectionType.List;
        }
        
        if (genericType == typeof(HashSet<>) || 
            genericType == typeof(ISet<>))
        {
            return CollectionType.Set;
        }
        
        if (genericType == typeof(Dictionary<,>) || 
            genericType == typeof(IDictionary<,>))
        {
            return CollectionType.Map;
        }

        return null;
    }
}

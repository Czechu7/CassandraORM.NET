using System.Reflection;
using CassandraOrm.Mapping;

namespace CassandraOrm.UDT;

/// <summary>
/// Metadata for User-Defined Types
/// </summary>
public class UdtMetadata
{
    public string TypeName { get; set; } = string.Empty;
    public string? Keyspace { get; set; }
    public Type ClrType { get; set; } = null!;
    public List<UdtFieldMetadata> Fields { get; set; } = new();

    public static UdtMetadata Create(Type type)
    {
        var udtAttribute = type.GetCustomAttribute<UserDefinedTypeAttribute>();
        if (udtAttribute == null)
        {
            throw new InvalidOperationException($"Type {type.Name} must be marked with [UserDefinedType] attribute.");
        }

        var metadata = new UdtMetadata
        {
            TypeName = udtAttribute.Name, // Use Name property from existing attribute
            Keyspace = udtAttribute.Keyspace,
            ClrType = type
        };

        // Get all properties that can be mapped
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !p.GetCustomAttributes<NotMappedAttribute>().Any());

        foreach (var property in properties)
        {
            var fieldMetadata = new UdtFieldMetadata
            {
                PropertyName = property.Name,
                FieldName = GetFieldName(property),
                PropertyType = property.PropertyType,
                PropertyInfo = property
            };

            metadata.Fields.Add(fieldMetadata);
        }

        return metadata;
    }    private static string GetFieldName(PropertyInfo property)
    {
        var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
        return columnAttribute?.Name ?? property.Name.ToLowerInvariant();
    }
}

/// <summary>
/// Metadata for UDT fields
/// </summary>
public class UdtFieldMetadata
{
    public string PropertyName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public Type PropertyType { get; set; } = null!;
    public PropertyInfo PropertyInfo { get; set; } = null!;
}

/// <summary>
/// Registry for User-Defined Types
/// </summary>
public class UdtRegistry
{
    private readonly Dictionary<Type, UdtMetadata> _udtMetadataCache = new();
    private readonly Dictionary<string, Type> _udtTypesByName = new();

    /// <summary>
    /// Registers a UDT type
    /// </summary>
    public void RegisterUdt<T>() where T : class
    {
        RegisterUdt(typeof(T));
    }

    /// <summary>
    /// Registers a UDT type
    /// </summary>
    public void RegisterUdt(Type type)
    {
        if (_udtMetadataCache.ContainsKey(type))
            return;

        var metadata = UdtMetadata.Create(type);
        _udtMetadataCache[type] = metadata;
        _udtTypesByName[metadata.TypeName] = type;
    }

    /// <summary>
    /// Gets UDT metadata for a type
    /// </summary>
    public UdtMetadata? GetUdtMetadata(Type type)
    {
        return _udtMetadataCache.TryGetValue(type, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Gets UDT metadata by type name
    /// </summary>
    public UdtMetadata? GetUdtMetadata(string typeName)
    {
        if (_udtTypesByName.TryGetValue(typeName, out var type))
        {
            return GetUdtMetadata(type);
        }
        return null;
    }

    /// <summary>
    /// Checks if a type is a registered UDT
    /// </summary>
    public bool IsUdt(Type type)
    {
        return _udtMetadataCache.ContainsKey(type);
    }

    /// <summary>
    /// Gets all registered UDT types
    /// </summary>
    public IEnumerable<Type> GetRegisteredUdtTypes()
    {
        return _udtMetadataCache.Keys;
    }

    /// <summary>
    /// Clears all registered UDTs
    /// </summary>
    public void Clear()
    {
        _udtMetadataCache.Clear();
        _udtTypesByName.Clear();
    }
}

/// <summary>
/// UDT mapper for converting between .NET objects and Cassandra UDT values
/// </summary>
public static class UdtMapper
{
    /// <summary>
    /// Maps a .NET object to a Cassandra UDT
    /// </summary>
    public static object ToUdt<T>(T value, UdtMetadata metadata) where T : class
    {
        if (value == null) return null!;

        // This would use the Cassandra driver's UDT mapping functionality
        // For now, we'll return a dictionary representation
        var udtValues = new Dictionary<string, object?>();

        foreach (var field in metadata.Fields)
        {
            var propertyValue = field.PropertyInfo.GetValue(value);
            udtValues[field.FieldName] = propertyValue;
        }

        return udtValues;
    }

    /// <summary>
    /// Maps a Cassandra UDT to a .NET object
    /// </summary>
    public static T FromUdt<T>(object udtValue, UdtMetadata metadata) where T : class, new()
    {
        if (udtValue == null) return null!;

        var instance = new T();

        // This would use the Cassandra driver's UDT mapping functionality
        // For now, we'll assume udtValue is a dictionary
        if (udtValue is Dictionary<string, object?> udtDict)
        {
            foreach (var field in metadata.Fields)
            {
                if (udtDict.TryGetValue(field.FieldName, out var value))
                {
                    var convertedValue = ConvertValue(value, field.PropertyType);
                    field.PropertyInfo.SetValue(instance, convertedValue);
                }
            }
        }

        return instance;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsAssignableFrom(value.GetType())) return value;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        return Convert.ChangeType(value, targetType);
    }
}

/// <summary>
/// Extension methods for UDT support
/// </summary>
public static class UdtExtensions
{
    /// <summary>
    /// Registers UDTs for an assembly
    /// </summary>
    public static void RegisterUdtsFromAssembly(this UdtRegistry registry, Assembly assembly)
    {
        var udtTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<UserDefinedTypeAttribute>() != null);

        foreach (var type in udtTypes)
        {
            registry.RegisterUdt(type);
        }
    }    /// <summary>
    /// Registers UDTs from the calling assembly
    /// </summary>
    public static void RegisterUdtsFromCurrentAssembly(this UdtRegistry registry)
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        registry.RegisterUdtsFromAssembly(callingAssembly);
    }
}

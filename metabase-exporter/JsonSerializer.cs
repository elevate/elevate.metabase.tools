using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace metabase_exporter;

[AttributeUsage(AttributeTargets.Property)]
public class JsonPropertyAltAttribute(string mainPropertyName, params string[] altPropertyNames) : Attribute
{
    public IReadOnlyList<string> PropertyNames { get; } = new List<string> {mainPropertyName}.Concat(altPropertyNames).ToImmutableList();
}

/// <summary>
/// Custom contract resolver that supports both JsonProperty and JsonPropertyAlt attributes.
/// </summary>
public class AltNameContractResolver : DefaultContractResolver
{
    // written by Claude 3.7 Sonnet
    
    private readonly Dictionary<Type, Dictionary<string, JsonProperty>> _altNameMappings = 
        new Dictionary<Type, Dictionary<string, JsonProperty>>();
    
    // Override this to preserve property names
    protected override string ResolvePropertyName(string propertyName)
    {
        return propertyName;
    }
    
    protected override JsonContract CreateContract(Type objectType)
    {
        JsonContract contract = base.CreateContract(objectType);
        
        // For objects, we need to handle custom property resolution
        if (contract is JsonObjectContract objectContract)
        {
            // Store the original CreateDictionaryContract method to call later
            var originalReader = objectContract.ExtensionDataSetter;
            
            // Set up the resolution dictionary for this type
            if (!_altNameMappings.ContainsKey(objectType))
            {
                _altNameMappings[objectType] = new Dictionary<string, JsonProperty>();
            }
            
            // Set up extension data handler to catch unknown properties
            objectContract.ExtensionDataSetter = (o, key, value) =>
            {
                // Check if this unknown property is actually an alternate name
                if (_altNameMappings.TryGetValue(objectType, out var mappings) && 
                    mappings.TryGetValue(key.ToString(), out var property))
                {
                    // Handle type conversion between compatible numeric types
                    if (value != null && property.PropertyType != value.GetType())
                    {
                        value = ConvertValueToTargetType(value, property.PropertyType);
                    }
                    
                    // Set the value on the actual property
                    property.ValueProvider.SetValue(o, value);
                }
                else if (originalReader != null)
                {
                    // Fall back to original behavior
                    originalReader(o, key, value);
                }
            };
        }
        
        return contract;
    }
    
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
        
        // Clear any existing mapping for this type
        if (_altNameMappings.ContainsKey(type))
        {
            _altNameMappings[type].Clear();
        }
        else
        {
            _altNameMappings[type] = new Dictionary<string, JsonProperty>();
        }
        
        // Process JsonPropertyAlt attributes
        foreach (JsonProperty property in properties)
        {
            MemberInfo member = property.DeclaringType.GetMember(property.UnderlyingName).FirstOrDefault();
            if (member == null) continue;
            
            JsonPropertyAltAttribute altAttribute = member.GetCustomAttribute<JsonPropertyAltAttribute>(true);
            
            if (altAttribute != null && altAttribute.PropertyNames.Count > 0)
            {
                // Set the primary name for serialization (first name in the list)
                property.PropertyName = altAttribute.PropertyNames[0];
                
                // Register all alternative names for deserialization
                foreach (var altName in altAttribute.PropertyNames.Skip(1))
                {
                    _altNameMappings[type][altName] = property;
                }
            }
        }
        
        return properties;
    }
    
    /// <summary>
    /// Converts a value to the target type, handling numeric conversions.
    /// Does not swallow exceptions.
    /// </summary>
    private object ConvertValueToTargetType(object value, Type targetType)
    {
        // Handle numeric conversions
        if (IsNumericType(value.GetType()) && IsNumericType(targetType))
        {
            // Convert via Convert.ChangeType - will throw exceptions if conversion fails
            return Convert.ChangeType(value, targetType);
        }
        
        // For other types, attempt normal conversion
        if (value is IConvertible)
        {
            return Convert.ChangeType(value, targetType);
        }
        
        // Return the original value and let JSON.NET handle conversion or throw appropriate exceptions
        return value;
    }
    
    /// <summary>
    /// Determines if a type is numeric
    /// </summary>
    private bool IsNumericType(Type type)
    {
        if (type == null) return false;
        
        // Handle nullable types
        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType != null)
        {
            type = nullableType;
        }
        
        return type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal);
    }
}

public static class MetabaseJsonSerializer
{
    public static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        //Formatting = Formatting.Indented // don't set this, it will mess checksums
        ContractResolver = new AltNameContractResolver(),
    });

    public static readonly JsonSerializer IndentedSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        ContractResolver = new AltNameContractResolver(),
    });
    
}
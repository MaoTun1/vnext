namespace System.Dynamic;

/// <summary>
/// Extension methods and helper methods for working with dynamic objects and ExpandoObject
/// </summary>
public static class ExpandoExtensions
{
    public static bool HasProperty(this ExpandoObject obj, string propertyName)
    {
        return ((IDictionary<string, object>)obj).ContainsKey(propertyName);
    }
    
    /// <summary>
    /// Checks if a dynamic object has a specific property
    /// </summary>
    /// <param name="obj">The dynamic object to check</param>
    /// <param name="propertyName">The property name to check for</param>
    /// <returns>True if the property exists, false otherwise</returns>
    public static bool HasProperty(this object obj, string propertyName)
    {
        if (obj == null) return false;
        
        // Check if it's an ExpandoObject
        if (obj is ExpandoObject expando)
        {
            return ((IDictionary<string, object>)expando).ContainsKey(propertyName);
        }
        
        // Check if it's another IDictionary<string, object>
        if (obj is IDictionary<string, object> dictionary)
        {
            return dictionary.ContainsKey(propertyName);
        }
        
        // For other dynamic objects, try to access the property using reflection
        var objType = obj.GetType();
        return objType.GetProperty(propertyName) != null || 
               objType.GetField(propertyName) != null;
    }
    
    /// <summary>
    /// Gets a property value from a dynamic object safely
    /// </summary>
    /// <param name="obj">The dynamic object</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>The property value or null if not found</returns>
    public static object GetPropertyValue(this object obj, string propertyName)
    {
        if (obj == null) return null;
        
        // Check if it's an ExpandoObject
        if (obj is ExpandoObject expando)
        {
            var dict = (IDictionary<string, object>)expando;
            return dict.TryGetValue(propertyName, out var value) ? value : null;
        }
        
        // Check if it's another IDictionary<string, object>
        if (obj is IDictionary<string, object> dictionary)
        {
            return dictionary.TryGetValue(propertyName, out var value) ? value : null;
        }
        
        // For other dynamic objects, try to access the property using reflection
        var objType = obj.GetType();
        var property = objType.GetProperty(propertyName);
        if (property != null)
        {
            return property.GetValue(obj);
        }
        
        var field = objType.GetField(propertyName);
        if (field != null)
        {
            return field.GetValue(obj);
        }
        
        return null;
    }
}
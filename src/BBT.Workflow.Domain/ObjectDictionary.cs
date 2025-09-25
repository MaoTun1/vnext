namespace BBT.Workflow;

[Serializable]
public class ObjectDictionary : Dictionary<string, object?>
{
    public ObjectDictionary()
    {
    }

    public ObjectDictionary(Dictionary<string, object?> dictionary)
        : base(dictionary)
    {
    }
}

public interface IObjectDictionary
{
    public ObjectDictionary MetaData { get; }

    public void SetMetaData(ObjectDictionary data);

    public void AddOrUpdateData(string key, object? value)
    {
        if (!MetaData.TryAdd(key, value))
        {
            MetaData[key] = value;
        }
    }

    public void RemoveData(string key)
    {
        MetaData.Remove(key);
    }
}
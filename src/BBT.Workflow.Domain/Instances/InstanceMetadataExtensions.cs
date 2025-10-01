using System.Globalization;
using BBT.Workflow.Definitions;

namespace BBT.Workflow.Instances;

public static class InstanceMetadataExtensions
{
    public static SubFlowContractInfo ToSubFlowContractInfo(this Instance instance)
    {
        if (instance is null)
            throw new ArgumentNullException(nameof(instance));

        var md = instance.MetaData;

        return new SubFlowContractInfo
        {
            Id      = GetGuid(md, DomainConsts.MetaDataKeys.Id),
            Key     = GetString(md, DomainConsts.MetaDataKeys.Key),
            Domain  = GetString(md, DomainConsts.MetaDataKeys.Domain) ?? string.Empty,
            Flow    = GetString(md, DomainConsts.MetaDataKeys.Flow) ?? string.Empty,
            Version = GetString(md, DomainConsts.MetaDataKeys.Version),
            State   = GetString(md, DomainConsts.MetaDataKeys.State),
            SubType = GetString(md, DomainConsts.MetaDataKeys.FlowType) ?? string.Empty
        };
    }
    
    public static SubFlowContractInfo ToSubFlowContractInfo(this ObjectDictionary metaData)
    {
        return new SubFlowContractInfo
        {
            Id      = GetGuid(metaData, DomainConsts.MetaDataKeys.Id),
            Key     = GetString(metaData, DomainConsts.MetaDataKeys.Key),
            Domain  = GetString(metaData, DomainConsts.MetaDataKeys.Domain) ?? string.Empty,
            Flow    = GetString(metaData, DomainConsts.MetaDataKeys.Flow) ?? string.Empty,
            Version = GetString(metaData, DomainConsts.MetaDataKeys.Version),
            State   = GetString(metaData, DomainConsts.MetaDataKeys.State),
            SubType = GetString(metaData, DomainConsts.MetaDataKeys.FlowType) ?? string.Empty
        };
    }

    public static WorkflowType? ToFlowType(this Instance instance)
    {
        var md = instance.MetaData;
        var type = GetString(md, DomainConsts.MetaDataKeys.FlowType);
        return !string.IsNullOrEmpty(type) 
            ? WorkflowType.FromCode(type)
            : null;
    }
    
    private static string? GetString(ObjectDictionary md, string key)
    {
        if (!md.TryGetValue(key, out var raw) || raw is null)
            return null;

        return raw switch
        {
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => raw.ToString()
        };
    }

    private static Guid GetGuid(ObjectDictionary md, string key)
    {
        if (!md.TryGetValue(key, out var raw) || raw is null)
            return Guid.Empty;

        return raw switch
        {
            Guid g => g,
            string s when Guid.TryParse(s, out var g) => g,
            _ => Guid.Empty
        };
    }

    public static T? GetValue<T>(this Instance instance, string key)
    {
        if (instance.MetaData == null)
            return default;

        if (instance.MetaData.TryGetValue(key, out var value) && value is T typed)
            return typed;

        return default;
    }
}
using BBT.Workflow.Instances;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BBT.Workflow.Data.ValueConverters;

internal class InstanceStatusConverter() : ValueConverter<InstanceStatus, string>(status => status.Code,
    code => InstanceStatus.FromCode(code));

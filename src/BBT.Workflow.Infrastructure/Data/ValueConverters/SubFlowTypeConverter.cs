using BBT.Workflow.Definitions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BBT.Workflow.Data.ValueConverters;

internal class SubFlowTypeConverter() : ValueConverter<SubFlowType, string>(status => status.Code,
    code => SubFlowType.FromCode(code));

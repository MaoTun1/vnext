using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BBT.Workflow.Instances.DTOs;
public class SignalRRequest
{
    public string Id { get; set; }
    public string Source { get; set; }
    public string Type { get; set; }
    public string Subject { get; set; }
    public JsonElement? Data { get; set; }

}
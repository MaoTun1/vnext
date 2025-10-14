using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BBT.Workflow.Instances.DTOs;

    public class FunctionQueryParemeters
    {
        public string platform { get; set; } = string.Empty;
        public string? version { get; set; } = null;
        public string[]? extension { get; set; } = null;
        
    }

using System;
using System.Collections.Generic;

namespace IntelligentMutexExecutionEnvironment.Models
{
    [Serializable]
    public class ApplicationGroup
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public DateTime CreatedDate { get; set; }

        public ApplicationGroup()
        {
            CreatedDate = DateTime.Now;
        }

        public override string ToString()
        {
            return GroupName ?? "(unnamed)";
        }
    }
}

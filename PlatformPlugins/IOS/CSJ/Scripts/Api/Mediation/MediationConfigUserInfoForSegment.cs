using System.Collections.Generic;

namespace ByteDance.Union.Mediation
{
    public class MediationConfigUserInfoForSegment
    {
        public Dictionary<string, string> CustomInfos { get; set; }

        public string UserId { get; set; }

        public string Channel { get; set; }

        public string SubChannel { get; set; }

        public int Age { get; set; }

        public string Gender { get; set; }

        public string UserValueGroup { get; set; }
    }
}
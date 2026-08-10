using System;
using System.Collections.Generic;

namespace ByteDance.Union.Mediation
{
    public class MediationPreloadRequestInfo
    {

        public int AdType { set; get; }

        public AdSlot AdSlot { set; get; }

        public List<string> PrimeRitList { set; get; }

    }
}

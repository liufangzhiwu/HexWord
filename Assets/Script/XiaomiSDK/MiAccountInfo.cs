using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Xiaomi.GameSDK
{
    public class MiAccountInfo
    {
        public long uid;
        public String sessionId;
        public String nikename;

        public static MiAccountInfo parse(AndroidJavaObject parms) {
            if (parms == null) {
                return null;
            }
            MiAccountInfo miAccountInfo = new MiAccountInfo();

            miAccountInfo.uid = parms.Get<long>("uid");
            miAccountInfo.sessionId = parms.Get<String>("sessionId");
            miAccountInfo.nikename = parms.Get<String>("nikename");

            return miAccountInfo;
        }
    }
}

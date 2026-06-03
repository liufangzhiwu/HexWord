#if UNITY_huawei
using UnityEngine;

namespace Middleware
{
    
    public class HuaWeiAttribution : IAttribute
    {
        
     
        public void Init(float delay)
        {
            
            UnityTimer.Delay(delay, () =>
            {
                

            });
            

        }

        public void ReportConversion(int eventCode)
        {

        
        }
    }
}
#endif

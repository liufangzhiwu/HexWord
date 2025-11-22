using UnityEditor.Build;
using UnityEngine;

namespace Middleware
{
    public class BuildReportTool : IPostprocessBuildWithReport, IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        // build前
        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            Debug.Log("Build开始...");
        }

        // build完成后
        public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            Debug.Log("Build结束...");
        }
    }
}
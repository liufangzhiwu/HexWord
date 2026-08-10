namespace ByteDance.Union.Mediation
{
    /// <summary>
    /// 聚合的开屏自定义兜底
    /// </summary>
    public sealed class MediationSplashRequestInfo
    {
        public string AdnName { get; set; }
        
        public string AdnSlotId { get; set; }
        
        public string AppId { get; set; }
        
        public string Appkey { get; set; }
    }
}

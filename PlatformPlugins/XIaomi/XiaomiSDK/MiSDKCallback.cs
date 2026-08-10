namespace Xiaomi.GameSDK
{
    public interface IMiSDKLoginCallback
    {
        void FinishLoginProcess(int code, MiAccountInfo var2);
    }

    public interface IMiSDKPayCallback
    {
        void FinishPayProcess(int code);
    }

    public interface IMiSDKExitCallback
    {
        void OnExit(int code);
    }
}

using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Passport.Runtime;
using Unity.Passport.Runtime.Model;

namespace Unity.Passport.Runtime.UI
{
    public class PassportUI : MonoBehaviour
    {
        private static GameObject _uPassUI = null;
        private static PassportUIBase _uiController;

        public static async Task Init(PassportUIConfig config, Action<PassportEvent> callback)
        {
            if (_uPassUI != null) return;

            if (config.Theme == PassportUITheme.Dark)
            {
                var uiPrefab = Resources.Load<GameObject>("LoginPanelsV2");
                if (PassportUIBase.Instance != null)
                {
                    _uPassUI = PassportUIBase.Instance.gameObject;
                }
                else
                {
                    _uPassUI = Instantiate(uiPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                }

                _uiController = _uPassUI.GetComponent<PassportUIControllerDark>();
                _uiController.OnCallback += callback;

                // 配置
                _uiController.invokeLoginManually = config.InvokeLoginManually;
                _uiController.Config = config;

                await _uiController.Init();
            }
            else if (config.Theme == PassportUITheme.Light)
            {
                var uiPrefab = Resources.Load<GameObject>("LoginPanels");
                
                if (PassportUIBase.Instance != null)
                {
                    _uPassUI = PassportUIBase.Instance.gameObject;
                }
                else
                {
                    _uPassUI = Instantiate(uiPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                }
                _uiController = _uPassUI.GetComponent<PassportUIControllerLight>();
                _uiController.OnCallback += callback;

                // 配置
                _uPassUI.GetComponent<UIAdaptManager>().autoRotation = config.AutoRotation;
                
                _uiController.invokeLoginManually = config.InvokeLoginManually;
                _uiController.Config = config;
            
                await _uiController.Init();
            }
        }

        public static void Login()
        {
            if (_uPassUI == null)
            {
                Debug.LogError("UI 对象尚未初始化，请确保在 Login 之前调用 Init 进行初始化。");
                return;
            }

            _uiController.Login();
        }

        public static void Logout()
        {
            if (_uPassUI == null)
            {
                Debug.LogError("UI 对象尚未初始化，请确保在 Logout 之前调用 Init 进行初始化。");
                return;
            }

            _uiController.Logout();
        }
    }
}
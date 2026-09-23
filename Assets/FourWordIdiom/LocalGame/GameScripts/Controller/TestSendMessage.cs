// using System.Collections.Generic;
// using UnityEngine;
//
// public class TestSendMessage : MonoBehaviour
// {
//     private static List<string> _logs = new List<string>();
//     private static readonly object _lock = new object();
//
//     // ============================================================
//     // ★ 关键：Unity 启动时自动调用，不依赖场景挂载
//     // ============================================================
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
//     static void AutoBootstrap()
//     {
//         Debug.Log("[Test] ========== RuntimeInitializeOnLoadMethod 被调用 ==========");
//
//         // 动态创建 GameObject
//         var go = new GameObject("TestSendMessageRunner");
//         go.AddComponent<TestSendMessage>();
//         DontDestroyOnLoad(go);
//
//         Debug.Log("[Test] TestSendMessageRunner 已创建");
//     }
//
//     void Awake()
//     {
//         Debug.Log("[Test] Awake 被调用");
//
//         Application.logMessageReceived += (condition, stackTrace, type) =>
//         {
//             lock (_lock)
//             {
//                 _logs.Add($"[{type}] {condition}");
//                 if (_logs.Count > 25) _logs.RemoveAt(0);
//             }
//         };
//     }
//
//     void Start()
//     {
//         Debug.Log("[Test] Start 被调用，3 秒后自动测试");
//         Invoke(nameof(TestNow), 3f);
//     }
//
//     void OnGUI()
//     {
//         // 大按钮
//         if (GUI.Button(new Rect(50, 100, 500, 200), "测试 SendMessage", new GUIStyle(GUI.skin.button) { fontSize = 60 }))
//         {
//             TestNow();
//         }
//
//         // 显示日志
//         GUIStyle style = new GUIStyle(GUI.skin.label)
//         {
//             fontSize = 22,
//             normal = { textColor = Color.yellow }
//         };
//
//         lock (_lock)
//         {
//             for (int i = 0; i < _logs.Count; i++)
//             {
//                 GUI.Label(new Rect(20, 350 + i * 28, Screen.width - 40, 28), _logs[i], style);
//             }
//         }
//     }
//
//     void TestNow()
//     {
//         Debug.Log("[Test] ========== 开始测试 ==========");
//
//         var go = GameObject.Find("PushMessageReceiver");
//         if (go == null)
//         {
//             Debug.LogError("[Test] ❌ 找不到 PushMessageReceiver");
//
//             // 列出所有 GameObject
//             var all = FindObjectsOfType<GameObject>();
//             Debug.Log($"[Test] 场景共 {all.Length} 个 GameObject:");
//             foreach (var g in all)
//             {
//                 Debug.Log($"[Test]   - '{g.name}' active={g.activeInHierarchy}");
//             }
//             return;
//         }
//
//         Debug.Log($"[Test] ✅ 找到 GameObject: {go.name}");
//
//         var receiver = go.GetComponent<PushMessageReceiver>();
//         if (receiver == null)
//         {
//             Debug.LogError("[Test] ❌ GameObject 上没挂 PushMessageReceiver 脚本");
//             return;
//         }
//
//         Debug.Log("[Test] ✅ 脚本已挂载，调用方法");
//         receiver.OnSettingsClosedSignal("1");
//
//         Debug.Log("[Test] ========== 测试完成 ==========");
//     }
// }
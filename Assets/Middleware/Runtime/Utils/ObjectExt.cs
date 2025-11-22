using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;


namespace Middleware
{
    public static class ObjectExt
    {
        // 获取对象组建
        public static void DelScript<T>(this GameObject go) where T : Component
        {
            T t = go.GetComponent<T>();
            if (t != null)
            {
                GameObject.Destroy(t);
            }
        }

        /// <summary>
        /// 遍历删除子对象
        /// </summary>
        /// <param name="o"></param>
        public static void DestroyAllChild(this GameObject o)
        {
            if (null != o)
            {
                for (int i = o.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(o.transform.GetChild(i).gameObject);
                }
            }
        }

        public static T TryGetComponent<T>(this GameObject o) where T : Component
        {
            T t = o.GetComponent<T>();
            if (t == null)
                t = o.AddComponent<T>();
            return t;
        }
        
        private static readonly Random _rng = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

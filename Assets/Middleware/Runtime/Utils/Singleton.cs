using System;
using System.Collections;
using System.Diagnostics;

namespace Middleware
{
    public class Singleton<T> where T : new()
    {
        private static T _instance = default(T);
        private static Object ObjLock = new System.Object();

        protected Singleton()
        {
            Debug.Assert(_instance == null);
        }

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (ObjLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }
                return _instance;
            }
        }
        
        public virtual void Init()
        {
        }
    }
}
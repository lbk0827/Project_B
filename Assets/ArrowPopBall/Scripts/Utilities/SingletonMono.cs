using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Utilities
{
    /// <summary>
    /// SingletonMono의 static 필드 리셋 (Domain Reload 대응)
    /// [RuntimeInitializeOnLoadMethod]는 제네릭 클래스에서 사용 불가하므로
    /// 비제네릭 클래스에서 처리
    /// </summary>
    internal static class SingletonResetHelper
    {
        private static readonly System.Collections.Generic.List<System.Action> _resetActions = new();

        internal static void Register(System.Action resetAction)
        {
            _resetActions.Add(resetAction);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            foreach (var action in _resetActions)
                action?.Invoke();
        }
#endif
    }

    /// <summary>
    /// MonoBehaviour 싱글톤 베이스 클래스
    /// </summary>
    public class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T>
    {
        private static bool _shuttingDown = false;
        private static object _lock = new object();
        private static T _instance;

        static SingletonMono()
        {
            SingletonResetHelper.Register(() =>
            {
                _shuttingDown = false;
                _instance = null;
            });
        }

        public static T Instance
        {
            get
            {
                if (_shuttingDown)
                    return null;

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();
                        if (_instance == null)
                        {
                            var obj = new GameObject(typeof(T).Name + " (Singleton)");
                            _instance = obj.AddComponent<T>();
                            DontDestroyOnLoad(obj);
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
                OnAwake();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 씬 로드 시 shuttingDown 플래그 리셋
            _shuttingDown = false;
            OnSceneChange(scene);
        }

        private void OnApplicationQuit() => _shuttingDown = true;

        protected virtual void OnAwake() { }

        /// <summary>
        /// 씬 전환 시 호출 (자식 클래스에서 오버라이드 가능)
        /// </summary>
        protected virtual void OnSceneChange(Scene scene) { }
    }
}
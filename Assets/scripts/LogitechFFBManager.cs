using UnityEngine;

namespace Logitech
{
    [DefaultExecutionOrder(-1000)]
    public class LogitechFFBManager : MonoBehaviour
    {
        public static bool Initialized { get; private set; } = false;

        static LogitechFFBManager instance;

        void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            Initialized = LogitechGSDK.LogiSteeringInitialize(false);
            Debug.Log("[LogitechFFB] Initialized: " + Initialized);
        }

        void OnDestroy()
        {
            if (Initialized)
            {
                LogitechGSDK.LogiSteeringShutdown();
                Debug.Log("[LogitechFFB] Shutdown.");
                Initialized = false;
            }

            if (instance == this)
                instance = null;
        }

        void Update()
        {
            if (Initialized)
                LogitechGSDK.LogiUpdate();
        }
    }
}

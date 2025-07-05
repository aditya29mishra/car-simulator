using UnityEngine;
using VehiclePhysics;

namespace Logitech
{
    [DefaultExecutionOrder(-90)]
    public class LogitechWheelFFB_VPP : MonoBehaviour
    {
        [Tooltip("Drag the VPVehicleToolkit component here")]
        public VPVehicleToolkit vehicle;

        const int WHEEL = 0;
        const int SURFACE_FREQ = 75;

        private bool isInitialized = false;
        private bool isApplicationQuitting = false;

        void Awake()
        {
            InitializeSDK();
        }

        void InitializeSDK()
        {
            if (isInitialized || isApplicationQuitting) return;

            try
            {
                // Simple initialization without forcing shutdown first
                isInitialized = LogitechGSDK.LogiSteeringInitialize(false);
                Debug.Log("[Logitech FFB] SDK initialized: " + isInitialized);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Logitech FFB] SDK initialization failed: " + e.Message);
                isInitialized = false;
            }
        }

        void OnApplicationQuit()
        {
            isApplicationQuitting = true;
            CleanupSDK();
        }

        void OnDestroy()
        {
            if (!isApplicationQuitting)
            {
                CleanupSDK();
            }
        }

        void CleanupSDK()
        {
            if (isInitialized && !isApplicationQuitting)
            {
                try
                {
                    LogitechGSDK.LogiStopSurfaceEffect(WHEEL);
                    LogitechGSDK.LogiStopConstantForce(WHEEL);
                    LogitechGSDK.LogiSteeringShutdown();
                    Debug.Log("[Logitech FFB] SDK shut down.");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[Logitech FFB] SDK shutdown error: " + e.Message);
                }
                finally
                {
                    isInitialized = false;
                }
            }
        }

        void Update()
        {
            if (isInitialized && !isApplicationQuitting)
            {
                try
                {
                    LogitechGSDK.LogiUpdate();
                }
                catch (System.Exception)
                {
                    // Silently handle update errors
                    isInitialized = false;
                }
            }
        }

        void FixedUpdate()
        {
            if (!isInitialized || vehicle == null || isApplicationQuitting)
                return;

            try
            {
                if (!LogitechGSDK.LogiIsConnected(WHEEL))
                    return;

                float speedKph = vehicle.speedInKph;

                int spring = Mathf.Clamp(6 + (int)(speedKph * 0.6f), 0, 100);
                int saturation = 100;
                LogitechGSDK.LogiPlaySpringForce(WHEEL, 0, saturation, spring);

                int damper = Mathf.Clamp((int)(speedKph * 0.4f), 0, 80);
                LogitechGSDK.LogiPlayDamperForce(WHEEL, damper);

                float slip = Mathf.Abs(vehicle.lateralG) + Mathf.Abs(vehicle.longitudinalG);
                if (slip > 0.8f)
                {
                    int mag = Mathf.Clamp((int)(slip * 25f), 20, 100);
                    LogitechGSDK.LogiPlaySurfaceEffect(WHEEL, LogitechGSDK.LOGI_PERIODICTYPE_SINE, mag, SURFACE_FREQ);
                }
                else
                {
                    LogitechGSDK.LogiStopSurfaceEffect(WHEEL);
                }

                float impactG = Mathf.Abs(vehicle.verticalG);
                if (impactG > 3.0f)
                {
                    int force = Mathf.Clamp((int)(impactG * 30f), 0, 100);
                    LogitechGSDK.LogiPlayConstantForce(WHEEL, force);
                    CancelInvoke(nameof(StopConstantForce));
                    Invoke(nameof(StopConstantForce), 0.05f);
                }
            }
            catch (System.Exception)
            {
                // Silently handle errors and mark as uninitialized
                isInitialized = false;
            }
        }

        void StopConstantForce()
        {
            if (isInitialized && !isApplicationQuitting)
            {
                try
                {
                    LogitechGSDK.LogiStopConstantForce(WHEEL);
                }
                catch (System.Exception)
                {
                    // Silently handle errors
                }
            }
        }

#if UNITY_EDITOR
        // Simple editor cleanup that doesn't interfere with domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticData()
        {
            // This runs before domain reload, ensuring clean state
        }
#endif
    }
}
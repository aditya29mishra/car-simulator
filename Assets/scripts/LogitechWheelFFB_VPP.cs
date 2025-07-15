using UnityEngine;
using VehiclePhysics;
using System.Collections;
namespace Logitech
{
    [DefaultExecutionOrder(-90)]
    public class LogitechWheelFFB_VPP : MonoBehaviour
    {
        [Tooltip("Drag the VPVehicleToolkit component here")]
        public VPVehicleToolkit vehicle;

        const int WHEEL = 0;
        const int SURFACE_FREQ = 75;

        private static bool isInitialized = false;
        private bool isApplicationQuitting = false;
        IEnumerator Start()
        {
            yield return null; // Delay by 1 frame
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
            if (isInitialized)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticData()
        {
            isInitialized = false;
            Debug.Log("[Logitech] Static state reset on domain reload.");
        }
#endif

        void Update()
        {
            if (isApplicationQuitting) return;

            // Double-check actual SDK state
            if (!isInitialized || !LogitechGSDK.LogiIsConnected(WHEEL))
                return;

            try
            {
                if (!LogitechGSDK.LogiUpdate())
                {
                    Debug.LogWarning("[Logitech] LogiUpdate failed.");
                    isInitialized = false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Logitech] LogiUpdate crashed: " + e.Message);
                isInitialized = false;
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
    }
}
using UnityEngine;
using VehiclePhysics;           // VPP namespace

namespace Logitech
{
    [DefaultExecutionOrder(-100)]          // Initialise before everything else
    public class LogitechWheelFFB_VPP : MonoBehaviour
    {
        [Tooltip("Drag the VPVehicleToolkit component here")]
        public VPVehicleToolkit vehicle;

        // ------------------------------------------------------------------ Lifecycle
        void Awake()      => LogitechGSDK.LogiSteeringInitialize(false);
        void OnDestroy()  => LogitechGSDK.LogiSteeringShutdown();
        void Update()     => LogitechGSDK.LogiUpdate();        // must be called every frame

        // ------------------------------------------------------------------ Very simple FFB (vertical G spike)
        void FixedUpdate()
        {
            if (vehicle == null || !LogitechGSDK.LogiIsConnected(0))
                return;

            float impactG = Mathf.Abs(vehicle.verticalG);      // crude collision metric

            if (impactG > 3.0f)                                // tweak threshold
            {
                int force = Mathf.Clamp((int)(impactG * 30f), 0, 100);
                LogitechGSDK.LogiPlayConstantForce(0, force);
                Invoke(nameof(StopForce), 0.05f);
            }
        }

        void StopForce() => LogitechGSDK.LogiStopConstantForce(0);
    }
}

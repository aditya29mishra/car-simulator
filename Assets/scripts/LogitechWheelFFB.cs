using UnityEngine;
using EVP;
namespace Logitech 
{


    [DefaultExecutionOrder(-100)]           // Initialise before other scripts
    public class LogitechWheelFFB : MonoBehaviour
    {
        public VehicleController vehicle;    // drag the active vehicle prefab here

        void Awake()
        {
            LogitechGSDK.LogiSteeringInitialize(false);   // false = don’t ignore XInput devices
        }

        void OnEnable()
        {
            if (vehicle != null) vehicle.onImpact += OnVehicleImpact;
        }

        void OnDisable()
        {
            if (vehicle != null) vehicle.onImpact -= OnVehicleImpact;
        }

        void Update()
        {
             LogitechGSDK.LogiUpdate();

    if (LogitechGSDK.LogiIsConnected(0))
    {
        Debug.Log("Wheel Connected ✔");
        Debug.Log("Steering Axis: " + LogitechGSDK.LogiGetStateCSharp(0).lX);
    }
    else
    {
        Debug.LogWarning("Wheel NOT Connected ❌");
    }                  // **must** be called every frame
        }

        //--------------------------------- Force‑feedback hooks -----------------------------

        void OnVehicleImpact()
        {
            // Magnitude based on impact speed (0‑100)
            int force = Mathf.Clamp((int)(VehicleController.current.localImpactVelocity.magnitude * 20f), 0, 100);

            // 20 ms constant‑force “punch” then stop 40 ms later
            LogitechGSDK.LogiPlayConstantForce(0, force);
            Invoke(nameof(StopImpactForce), 0.04f);
        }

        void StopImpactForce() =>
           LogitechGSDK.LogiStopConstantForce(0);

        void OnDestroy() => LogitechGSDK.LogiSteeringShutdown();
    }
}

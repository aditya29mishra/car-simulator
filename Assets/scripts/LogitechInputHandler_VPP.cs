using UnityEngine;
using VehiclePhysics;

namespace Logitech
{
    [DefaultExecutionOrder(-50)]
    public class LogitechInputHandler_VPP : MonoBehaviour
    {
        [Tooltip("Drag the VPVehicleToolkit component here")]
        public VPVehicleToolkit vehicle;

        const int WHEEL = 0;

        static float NormaliseCenteredPedal(int raw)
        {
            // Maps from +32767 (released) to -32767 (pressed)
            return Mathf.Clamp01(Mathf.InverseLerp(32767f, -32767f, raw));
        }

        void Update()
        {
            if (vehicle == null || vehicle.vehicle == null) return;

            if (!LogitechGSDK.LogiUpdate() || !LogitechGSDK.LogiIsConnected(WHEEL))
                return;

            var s = LogitechGSDK.LogiGetStateCSharp(WHEEL);

            // 1. Steering
            float steer = s.lX / 32767f;
            VPVehicleToolkit.SetSteering(vehicle.vehicle, steer);

            // 2. Pedals
            float throttle = NormaliseCenteredPedal(s.lZ);    // Throttle: lZ
            float brake    = NormaliseCenteredPedal(s.lRz);   // Brake: lRz
            float clutch   = NormaliseCenteredPedal(s.lY);    // Clutch: lY

            VPVehicleToolkit.SetThrottle(vehicle.vehicle, throttle);
            VPVehicleToolkit.SetBrake   (vehicle.vehicle, brake);
            VPVehicleToolkit.SetClutch  (vehicle.vehicle, clutch);

            // 3. Handbrake – button 1
            bool hb = LogitechGSDK.LogiButtonIsPressed(WHEEL, 1);
            VPVehicleToolkit.SetHandbrake(vehicle.vehicle, hb ? 1f : 0f);

            // 4. Paddle shifters – buttons 5 & 4
            if (LogitechGSDK.LogiButtonTriggered(WHEEL, 5))
                vehicle.ShiftGearUp();
            if (LogitechGSDK.LogiButtonTriggered(WHEEL, 4))
                vehicle.ShiftGearDown();

            // 5. H-shifter – buttons 12-18
            if      (LogitechGSDK.LogiButtonIsPressed(WHEEL, 12)) vehicle.SetGear(1);
            else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 13)) vehicle.SetGear(2);
            else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 14)) vehicle.SetGear(3);
            else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 15)) vehicle.SetGear(4);
            else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 16)) vehicle.SetGear(5);
            else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 17)) vehicle.SetGear(-1);
            else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 18)) vehicle.SetGear(-1); // Reverse
            else                                                   vehicle.SetGear(0);  // Neutral

            // Debug
            Debug.Log($"[Input] Steer:{steer:F2}  Thr:{throttle:F2}  Brk:{brake:F2}  Clu:{clutch:F2}  Gear:{vehicle.engagedGear}");
        }
    }
}

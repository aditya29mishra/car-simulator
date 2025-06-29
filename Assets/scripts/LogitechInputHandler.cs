using UnityEngine;
using EVP;                 // Your vehicle namespace

namespace Logitech
{
    [DefaultExecutionOrder(-50)]          // Run before physics step
    public class LogitechInputHandler : MonoBehaviour
    {
        public VehicleController vehicle; // Drag your vehicle controller here

        const int WHEEL = 0;              // First wheel index

        void Update()
        {
            if (vehicle == null) return;

            // The SDK requires LogiUpdate every frame
            if (!LogitechGSDK.LogiUpdate() || !LogitechGSDK.LogiIsConnected(WHEEL))
                return;

            // Read full DirectInput state
            var state = LogitechGSDK.LogiGetStateCSharp(WHEEL);

            /* ───────────────────────────────────────────────────────────────
               1.  STEERING  (state.lX)   −32768  … 0 … +32767
            ─────────────────────────────────────────────────────────────── */
            float steering = state.lX / 32767f;         // Normalise to −1 … +1
            vehicle.steerInput = steering;

            /* ───────────────────────────────────────────────────────────────
               2.  PEDALS   lY = Accelerator,  lRz = Brake
               Range is usually 0 (pressed) … 65535 (released)
               We invert and normalise to 0…1
            ─────────────────────────────────────────────────────────────── */
            float accelRaw = 1f - (state.lY / 65535f);  // 0…1  (down=1)
            float brakeRaw = 1f - (state.lRz / 65535f); // 0…1  (down=1)

            vehicle.throttleInput = accelRaw;
            vehicle.brakeInput    = brakeRaw;

            /* ───────────────────────────────────────────────────────────────
               3.  HANDBRAKE  (example: wheel button 1)
            ─────────────────────────────────────────────────────────────── */
            bool handbrakeBtn = LogitechGSDK.LogiButtonIsPressed(WHEEL, 1); // change index if needed
            vehicle.handbrakeInput = handbrakeBtn ? 1f : 0f;

            /* ───────────────────────────────────────────────────────────────
               4.  OPTIONAL GEARS (wheel paddles)  buttons 4 & 5
            ─────────────────────────────────────────────────────────────── */
    
        }
    }
}

using UnityEngine;
using VehiclePhysics;

namespace Logitech
{
    [DefaultExecutionOrder(-50)]
    public class LogitechInputHandler_VPP : MonoBehaviour
    {
        [Tooltip("Drag the VPVehicleToolkit component here")]
        public VPVehicleToolkit vehicle;
        public bool enginestarted = false;
        [Tooltip("Sound to play when attempting gear shift without clutch pressed")]
        public AudioClip clutchWarningClip;
        private int lastValidGear = 0;
        private AudioSource _audio;
        [Header("Debug Settings")]
        [Tooltip("Enable comprehensive button debugging")]
        public bool enableButtonDebug = true;
        [Tooltip("Enable axis/pedal debugging")]
        public bool enableAxisDebug = false;
        [Tooltip("Only log when buttons change state")]
        public bool logOnlyChanges = true;
        private bool handbrakeToggled = false;
        private float clutch = 0.0f;
        private float clutchThreshold = 0.3f; // Threshold for clutch engagement
        const int WHEEL = 0;
        const int MAX_BUTTONS = 128; // Check up to 128 buttons

        // Track button states for change detection
        private bool[] previousButtonStates = new bool[MAX_BUTTONS];
        private bool[] currentButtonStates = new bool[MAX_BUTTONS];
        void Awake()
        {
            _audio = GetComponent<AudioSource>();
            if (clutchWarningClip != null && _audio == null)
                Debug.LogWarning("Attach an AudioSource to play clutch warning.");
        }

        static float NormaliseCenteredPedal(int raw)
        {
            // Maps from +32767 (released) to -32767 (pressed)
            return Mathf.Clamp01(Mathf.InverseLerp(32767f, -32767f, raw));
        }


        void Update()
        {
            if (vehicle == null || vehicle.vehicle == null) return;
            if (!LogitechGSDK.LogiUpdate() || !LogitechGSDK.LogiIsConnected(WHEEL)) return;

            var s = LogitechGSDK.LogiGetStateCSharp(WHEEL);

            //──────────────── BUTTON MAPPING ────────────────
            HandleButtons(s);

            //──────────────── AXES / PEDALS ────────────────
            float steer = s.lX / 32767f;
            float throttle = NormaliseCenteredPedal(s.lZ);
            float brake = NormaliseCenteredPedal(s.lRz);
            clutch = NormaliseCenteredPedal(s.lY);

            VPVehicleToolkit.SetSteering(vehicle.vehicle, steer);
            VPVehicleToolkit.SetThrottle(vehicle.vehicle, throttle);
            VPVehicleToolkit.SetBrake(vehicle.vehicle, brake);
            VPVehicleToolkit.SetClutch(vehicle.vehicle, clutch);
            bool bothPaddles = LogitechGSDK.LogiButtonIsPressed(WHEEL, 4) && LogitechGSDK.LogiButtonIsPressed(WHEEL, 5);
            if (bothPaddles)
                handbrakeToggled = !handbrakeToggled;

            VPVehicleToolkit.SetHandbrake(vehicle.vehicle, handbrakeToggled ? 1f : 0f);



            // H‑shifter (buttons 12‑18)
            int currentGear = vehicle.engagedGear;  // get current engaged gear
            int requestedGear = 0;

            if (clutch >= clutchThreshold)
            {


                if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 12)) requestedGear = 1;
                else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 13)) requestedGear = 2;
                else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 14)) requestedGear = 3;
                else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 15)) requestedGear = 4;
                else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 16)) requestedGear = 5;
                else if (LogitechGSDK.LogiButtonIsPressed(WHEEL, 17) || LogitechGSDK.LogiButtonIsPressed(WHEEL, 18))
                    requestedGear = -1; // reverse

                // If clutch is pressed enough
                if (clutch >= clutchThreshold)
                {
                    // Only reject if jump is too large
                    if (requestedGear != 0 && Mathf.Abs(requestedGear - lastValidGear) > 1)
                    {
                        Debug.LogWarning($"[Gear Reject] Abrupt gear change: {lastValidGear} → {requestedGear}");
                        if (clutchWarningClip != null && _audio != null && !_audio.isPlaying)
                            _audio.PlayOneShot(clutchWarningClip);
                    }
                    else
                    {
                        vehicle.SetGear(requestedGear);
                        if (requestedGear != 0)
                            lastValidGear = requestedGear;
                    }
                }
                else
                {
                    Debug.LogWarning($"[Gear Reject] Abrupt gear change: {currentGear} → {requestedGear}");
                    if (clutchWarningClip != null && _audio != null && !_audio.isPlaying)
                    {
                        _audio.clip = clutchWarningClip;
                        _audio.loop = false;
                        _audio.Play();
                    }
                }
            }
            else
            {

                // clutch too low → warning
                if (throttle < 0.05f)
                {
                    vehicle.StopEngine();  // simulate stall
                    Debug.Log("[Stall] Clutch released without throttle – stopping engine.");
                }
                Debug.Log("[Clutch Warning] Playing warning sound because clutch < threshold");

                if (clutchWarningClip != null && _audio != null && !_audio.isPlaying)
                {
                    _audio.clip = clutchWarningClip;
                    _audio.loop = false;
                    _audio.Play();
                }
            }

            // —————————— stop the warning as soon as any *other* input arrives —————————
            if (_audio != null && _audio.isPlaying)
            {
                // valid if clutch now above threshold
                bool validInput = clutch >= clutchThreshold;

                // or if any pedal/steer moved
                validInput |= Mathf.Abs(steer) > 0.01f
                           || throttle > 0.01f
                           || brake > 0.01f;

                // or if any button was just pressed
                for (int i = 0; i < MAX_BUTTONS && !validInput; i++)
                    if (currentButtonStates[i] && !previousButtonStates[i])
                        validInput = true;

                if (validInput)
                    _audio.Stop();
            }
            //──────────────── Optional debug ────────────────
            if (enableAxisDebug)
            {
                Debug.Log($"[Axis] lX:{s.lX} lY:{s.lY} lZ:{s.lZ} lRz:{s.lRz}");
            }
        }

        //──────────────────────────────────────────────────
        void HandleButtons(LogitechGSDK.DIJOYSTATE2ENGINES s)
        {
            // cache previous
            System.Array.Copy(currentButtonStates, previousButtonStates, MAX_BUTTONS);

            // scan & react
            for (int i = 0; i < MAX_BUTTONS; i++)
            {
                currentButtonStates[i] = (s.rgbButtons[i] & 0x80) != 0;

                if (enableButtonDebug && (!logOnlyChanges || currentButtonStates[i] != previousButtonStates[i]))
                    Debug.Log($"[Btn {i}] {(currentButtonStates[i] ? "Down" : "Up")}");

                //──────── custom mappings ────────
                if (i == 23 && ButtonTriggered(i))          // Engine START
                {
                    if (!enginestarted)
                    {
                        vehicle.StartEngine();
                        enginestarted = true;
                    }
                    else
                    {
                        vehicle.StopEngine();
                        enginestarted = false;
                    }
                }
            }
        }

        bool ButtonTriggered(int btn) =>
            currentButtonStates[btn] && !previousButtonStates[btn];

        //──────────────── Camera helper ────────────────

        void DebugAllButtons()
        {
            // Store previous states
            System.Array.Copy(currentButtonStates, previousButtonStates, MAX_BUTTONS);

            // Check all possible buttons
            for (int i = 0; i < MAX_BUTTONS; i++)
            {
                currentButtonStates[i] = LogitechGSDK.LogiButtonIsPressed(WHEEL, i);

                if (logOnlyChanges)
                {
                    // Only log when button state changes
                    if (currentButtonStates[i] != previousButtonStates[i])
                    {
                        string state = currentButtonStates[i] ? "PRESSED" : "RELEASED";
                        Debug.Log($"<color=yellow>[BUTTON DEBUG] Button {i}: {state}</color>");
                    }
                }
                else
                {
                    // Log all pressed buttons every frame
                    if (currentButtonStates[i])
                    {
                        Debug.Log($"<color=yellow>[BUTTON DEBUG] Button {i}: PRESSED</color>");
                    }
                }
            }
        }

        void DebugAllAxes(LogitechGSDK.DIJOYSTATE2ENGINES s)
        {
            // Log all axis values
            Debug.Log($"<color=cyan>[AXIS DEBUG] " +
                     $"lX:{s.lX} lY:{s.lY} lZ:{s.lZ} " +
                     $"lRx:{s.lRx} lRy:{s.lRy} lRz:{s.lRz} " +
                     $"rglSlider[0]:{s.rglSlider[0]} rglSlider[1]:{s.rglSlider[1]} " +
                     $"rgdwPOV[0]:{s.rgdwPOV[0]} rgdwPOV[1]:{s.rgdwPOV[1]} " +
                     $"rgdwPOV[2]:{s.rgdwPOV[2]} rgdwPOV[3]:{s.rgdwPOV[3]}</color>");
        }

        // === HELPER METHODS FOR EASY BUTTON MAPPING ===

        [Header("Quick Test - Add your start button mapping here")]
        [Tooltip("Test button number for start functionality")]
        public int testStartButton = -1;

        void LateUpdate()
        {
            // Test your start button mapping here
            if (testStartButton >= 0 && LogitechGSDK.LogiButtonTriggered(WHEEL, testStartButton))
            {
                Debug.Log($"<color=green>[START BUTTON] Button {testStartButton} triggered! Add your start game logic here.</color>");

            }
        }

        // Call this method to get a summary of all currently pressed buttons
        [ContextMenu("Log All Pressed Buttons")]
        public void LogAllPressedButtons()
        {
            if (!LogitechGSDK.LogiIsConnected(WHEEL))
            {
                Debug.LogWarning("Wheel not connected!");
                return;
            }

            string pressedButtons = "";
            for (int i = 0; i < MAX_BUTTONS; i++)
            {
                if (LogitechGSDK.LogiButtonIsPressed(WHEEL, i))
                {
                    pressedButtons += i + " ";
                }
            }

            if (pressedButtons.Length > 0)
            {
                Debug.Log($"<color=green>[PRESSED BUTTONS] Currently pressed: {pressedButtons}</color>");
            }
            else
            {
                Debug.Log("<color=orange>[PRESSED BUTTONS] No buttons currently pressed</color>");
            }
        }
    }
}
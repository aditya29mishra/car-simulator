using UnityEngine;
using VehiclePhysics;
using EdyCommonTools;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(VehicleBase))]
public class VPSelfDrive : MonoBehaviour
{
    [Header("Spline Target")]
    public Transform targetFollower; // Target object with EdyCommonTools.SplineFollower
    public float targetMoveSpeed = 5f;
    private float targetsplinemovespeed = 0f;

    [Header("AI Settings")]
    public bool loopPath = true;
    public bool aiEnabled = true;
    public bool autoStartEngine = true;

    [Header("Visual Effects")]
    public ParticleSystem engineStartEffect;
    public AudioSource engineStartSound;

    [Header("Stop Points")]
    public List<SplineStopPoint> stopPoints = new List<SplineStopPoint>();
    private VehicleBase vehicle;
    private Rigidbody rb;
    private VPVehicleController vpController;
    private EdyCommonTools.SplineFollower splineFollower;

    private bool engineStarted = false;
    private bool readyToMove = false;
    private bool isAtStopPoint = false;
    private Coroutine stopCoroutine = null;
    private List<bool> visitedStops = new List<bool>();
    private int completedLoops = 0;

    [System.Serializable]
    public class SplineStopPoint
    {
        public int splinePointIndex;   // Index of the spline point to stop at
        public float stopDuration;    // How long to wait
        public float stopRadius;
    }

    void CheckForStopPoints()
    {
        if (splineFollower?.spline?.points == null || stopPoints.Count == 0) return;

        float currentPosition = splineFollower.position;
        int totalPoints = splineFollower.spline.points.Length;

        // Handle looping - reset visited stops when we complete a loop
        int currentLoop = Mathf.FloorToInt(currentPosition / totalPoints);
        if (currentLoop > completedLoops)
        {
            completedLoops = currentLoop;
            for (int i = 0; i < visitedStops.Count; i++)
            {
                visitedStops[i] = false;
            }
            Debug.Log($"[VPSelfDrive] New loop started. Loop #{currentLoop + 1}");
        }

        // Get the normalized position (current position within this loop)
        float normalizedPosition = currentPosition % totalPoints;

        // Check if we've passed or reached any stop points
        for (int i = 0; i < stopPoints.Count; i++)
        {
            if (visitedStops[i]) continue;

            SplineStopPoint stopPoint = stopPoints[i];

            // Validate the stop point index
            if (stopPoint.splinePointIndex < 0 || stopPoint.splinePointIndex >= totalPoints)
            {
                Debug.LogWarning($"[VPSelfDrive] Stop point {i} has invalid spline point index {stopPoint.splinePointIndex}. Spline has {totalPoints} points (0-{totalPoints - 1})");
                continue;
            }

            // Check if we've reached or passed the target point
            // We need to handle the case where we might overshoot the exact point
            float targetPoint = stopPoint.splinePointIndex;

            // If we're very close to or have passed the target point, stop
            if (normalizedPosition >= targetPoint - 0.01f && normalizedPosition <= targetPoint + 0.1f)
            {
                // Move the spline follower to the exact target position
                float exactTargetPosition = currentLoop * totalPoints + targetPoint;
                splineFollower.position = exactTargetPosition;

                visitedStops[i] = true;
                StopAtPoint(stopPoint.stopDuration);
                Debug.Log($"[VPSelfDrive] *** STOPPING *** at spline point index {stopPoint.splinePointIndex} (was at pos: {normalizedPosition:F2}, moved to exact pos: {exactTargetPosition:F2})");
                break;
            }
        }
    }

    void StopAtPoint(float duration)
    {
        if (isAtStopPoint) return;

        isAtStopPoint = true;
        splineFollower.speed = 0f;
        if (stopCoroutine != null) StopCoroutine(stopCoroutine);
        stopCoroutine = StartCoroutine(StopTimer(duration));
    }

    IEnumerator StopTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        isAtStopPoint = false;
        splineFollower.speed = targetsplinemovespeed;
        stopCoroutine = null;
    }

    void Awake()
    {
        vehicle = GetComponent<VehicleBase>();
        rb = GetComponent<Rigidbody>();
        vpController = GetComponent<VPVehicleController>();

        if (targetFollower == null)
        {
            Debug.LogError("[VPSelfDrive] ERROR: Missing target follower Transform (not assigned in Inspector)!");
            enabled = false;
            return;
        }

        splineFollower = targetFollower.GetComponent<EdyCommonTools.SplineFollower>();
        if (splineFollower == null)
        {
            Debug.LogError("[VPSelfDrive] ERROR: Target does not have an EdyCommonTools.SplineFollower component!");
            enabled = false;
            return;
        }

        splineFollower.updateMode = EdyCommonTools.SplineFollower.UpdateMode.External;
        splineFollower.mode = EdyCommonTools.SplineFollower.Mode.PreciseSpeed;
        targetsplinemovespeed = splineFollower.speed;

        // freeze car
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        vehicle.enabled = false;
        if (vpController != null) vpController.enabled = false;

        Debug.Log("[VPSelfDrive] Vehicle and Rigidbody frozen until engine starts.");
    }

    void Start()
    {
        if (aiEnabled && autoStartEngine)
        {
            Debug.Log("[VPSelfDrive] Starting engine effects...");
            StartCoroutine(StartEngineSequence());
        }
    }

    IEnumerator StartEngineSequence()
    {
        // Start the engine using VPP's built-in system
        vehicle.SendMessage("StartEngine", SendMessageOptions.DontRequireReceiver);
        vehicle.enabled = true;
        if (vpController != null) vpController.enabled = true;

        // Give VPP some time to finish the engine startup sequence
        yield return new WaitForSeconds(1.5f);

        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None;

        engineStarted = true;
        readyToMove = true;

        Debug.Log("[VPSelfDrive] Engine started. AI control enabled.");

        // Initialize visited stops tracking
        visitedStops.Clear();
        for (int i = 0; i < stopPoints.Count; i++)
        {
            visitedStops.Add(false);
        }
    }

    void Update()
    {
        if (aiEnabled && readyToMove)
        {
            if (!isAtStopPoint)
            {
                CheckForStopPoints();
                FollowTarget();

                if (splineFollower != null)
                {
                    splineFollower.updateMode = EdyCommonTools.SplineFollower.UpdateMode.Update;
                    splineFollower.DoAutoMove();
                }
            }

            // Display current nearest point index instead of speed
            if (splineFollower?.spline?.points != null)
            {
                float currentPosition = splineFollower.position;
                int totalPoints = splineFollower.spline.points.Length;
                float normalizedPosition = currentPosition % totalPoints;
                int nearestPointIndex = Mathf.Clamp(Mathf.RoundToInt(normalizedPosition), 0, totalPoints - 1);

                Debug.Log($"[VPSelfDrive] Current spline position: {normalizedPosition:F2}, Nearest point index: {nearestPointIndex}, Is stopped: {isAtStopPoint}");
            }
        }
    }

    void FollowTarget()
    {
        if (targetFollower == null) return;

        Vector3 targetPos = targetFollower.position;
        targetPos.y = transform.position.y;

        Vector3 direction = targetFollower.forward;
        direction.y = 0f;

        transform.position = Vector3.Lerp(transform.position, targetPos, targetMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), targetMoveSpeed * Time.deltaTime);
    }

    public void ResetToTarget()
    {
        if (targetFollower == null) return;

        Vector3 resetPos = targetFollower.position;
        resetPos.y = transform.position.y;
        transform.position = resetPos;
        transform.rotation = Quaternion.LookRotation(targetFollower.forward);

        Debug.Log("[VPSelfDrive] Reset car to target position.");
    }

    public void SetAIEnabled(bool enabled)
    {
        aiEnabled = enabled;
        Debug.Log("[VPSelfDrive] AI enabled set to: " + enabled);

        if (enabled && autoStartEngine && !engineStarted)
            StartCoroutine(StartEngineSequence());
    }

    // Helper method to validate stop points in the inspector
    void OnValidate()
    {
        if (splineFollower?.spline?.points != null)
        {
            int totalPoints = splineFollower.spline.points.Length;
            foreach (var stopPoint in stopPoints)
            {
                if (stopPoint.splinePointIndex < 0 || stopPoint.splinePointIndex >= totalPoints)
                {
                    Debug.LogWarning($"[VPSelfDrive] Stop point index {stopPoint.splinePointIndex} is out of range. Spline has {totalPoints} points (valid range: 0-{totalPoints - 1})");
                }
            }
        }
    }
}
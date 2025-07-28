using UnityEngine;
using EdyCommonTools;
using EVP;

[RequireComponent(typeof(VehicleController))]
public class VPSelfDriveEVP : MonoBehaviour
{
    [Header("Spline")]
    public Transform Path;

    [Header("AI Tuning")]
    public float lookAheadDistance = 6.0f;
    public float maxSpeed = 25f;
    public float minSpeedOnCurve = 6.0f;
    public float minCurveRadius = 8.0f;
    public float steerSensitivity = 1.2f;
    public float throttleSmooth = 2.0f;

    [Header("Multiple Stop Controls")]
    [Tooltip("Indices in spline.points[] where the car should stop. (e.g. [2,5,9])")]
    public int[] stopPointIndices;
    [Tooltip("How long (seconds) to stop at each point, matches order of stopPointIndices.")]
    public float[] stopDurations;
    [Tooltip("How close (meters) to trigger the stop at a stop point.")]
    public float stopProximity = 2.0f;

    [Header("Debug")]
    public bool showGizmos = true;

    private Spline spline;
    private VehicleController vc;

    private float pathDistance = 0f;
    private Vector3 progressPoint;
    private Vector3 lookPoint;

    // Multi-stop state
    private int currentStop = -1;    // current stop we're doing, -1 means none
    private bool[] hasStoppedAt;     // tracks if we've stopped at each stop point for this lap
    private float stopTimer = 0f;
    private bool stopping = false;

    void Start()
    {
        vc = GetComponent<VehicleController>();
        if (Path == null)
        {
            Debug.LogError("VPSelfDriveEVP: Path not assigned!", this);
            enabled = false;
            return;
        }
        spline = Path.GetComponent<Spline>();
        if (spline == null)
        {
            Debug.LogError("VPSelfDriveEVP: No Spline component found on Path GameObject!", this);
            enabled = false;
            return;
        }
        pathDistance = 0f;
        // Guard: make hasStoppedAt array match stopPoints' count
        hasStoppedAt = stopPointIndices != null ? new bool[stopPointIndices.Length] : new bool[0];
    }

    void FixedUpdate()
    {
        if (spline == null || spline.points == null || spline.points.Length < 2) return;

        // --- MULTISTOP LOGIC ---
        if (!stopping)
        {
            // Check each stop
            for (int i = 0; stopPointIndices != null && i < stopPointIndices.Length; ++i)
            {
                int pIdx = Mathf.Clamp(stopPointIndices[i], 0, spline.points.Length - 1);
                Vector3 stopTarget = spline.transform.TransformPoint(spline.points[pIdx].position);
                float distToStop = Vector3.Distance(transform.position, stopTarget);

                if (!hasStoppedAt[i] && distToStop < stopProximity)
                {
                    stopping = true;
                    currentStop = i;
                    stopTimer = (stopDurations != null && i < stopDurations.Length) ? stopDurations[i] : 2.0f;
                    break;
                }
            }
        }

        if (stopping)
        {
            // Apply brakes, hold
            vc.throttleInput = 0f;
            vc.brakeInput = Mathf.Abs(vc.speed) > 0.5f ? 1.0f : 0.4f;
            vc.handbrakeInput = 0f;

            stopTimer -= Time.fixedDeltaTime;
            if (stopTimer <= 0f)
            {
                stopping = false;
                if (currentStop >= 0 && currentStop < hasStoppedAt.Length)
                    hasStoppedAt[currentStop] = true;
                currentStop = -1;
            }
            return;
        }

        // --- PROGRESS ---
        float localSpeed = Vector3.Dot(vc.cachedRigidbody.velocity, transform.forward);
        localSpeed = Mathf.Max(0f, localSpeed);
        pathDistance += localSpeed * Time.fixedDeltaTime;

        // --- WRAP/CLAMP AND RESET STOP ARRAY on new lap ---
        if (spline.closed)
        {
            if (pathDistance > spline.length)
            {
                pathDistance -= spline.length;
                ResetStopsForLap();
            }
            if (pathDistance < 0f) pathDistance += spline.length;
        }
        else
        {
            pathDistance = Mathf.Clamp(pathDistance, 0, spline.length);
        }

        // --- STANDARD PATH FOLLOWING ---
        float currentS = spline.DistanceToPosition(pathDistance);
        progressPoint = spline.GetPosition(currentS, Spline.WrapMode.Clamp);
        float lookDistance = pathDistance + lookAheadDistance;
        float lookS = spline.DistanceToPosition(lookDistance);
        lookPoint = spline.GetPosition(lookS, Spline.WrapMode.Clamp);

        Vector3 localTarget = transform.InverseTransformPoint(lookPoint);
        float targetSteer = Mathf.Atan2(localTarget.x, localTarget.z);
        vc.steerInput = Mathf.Clamp(targetSteer * steerSensitivity, -1f, 1f);

        float radius = EstimateCurveRadius(spline, currentS);
        float targetSpeed = Mathf.Lerp(minSpeedOnCurve, maxSpeed, Mathf.InverseLerp(minCurveRadius, 120f, radius));
        float speed = vc.speed;
        float throttle, brake;
        if (speed < targetSpeed - 0.5f)
        {
            throttle = 1f; brake = 0f;
        }
        else if (speed > targetSpeed + 0.5f)
        {
            throttle = 0f; brake = Mathf.Clamp01((speed - targetSpeed) / 10f);
        }
        else
        {
            throttle = Mathf.Clamp01((targetSpeed - speed) / 5f); brake = 0f;
        }
        vc.throttleInput = Mathf.MoveTowards(vc.throttleInput, throttle, throttleSmooth * Time.fixedDeltaTime);
        vc.brakeInput = brake;
        vc.handbrakeInput = 0f;
    }

    void ResetStopsForLap()
    {
        for (int i = 0; i < hasStoppedAt.Length; ++i)
            hasStoppedAt[i] = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos || spline == null) return;
        int segments = 40;
        Vector3 prev = spline.GetPosition(0f, Spline.WrapMode.Clamp);
        for (int i = 1; i <= segments; ++i)
        {
            float s = (float)i / segments;
            Vector3 next = spline.GetPosition(s, Spline.WrapMode.Clamp);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        // Visualize stop points
        if (spline.points != null && stopPointIndices != null)
        {
            for (int i = 0; i < stopPointIndices.Length; ++i)
            {
                int idx = Mathf.Clamp(stopPointIndices[i], 0, spline.points.Length - 1);
                Vector3 stopPos = spline.transform.TransformPoint(spline.points[idx].position);
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(stopPos, 1.1f);
            }
        }
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(progressPoint, 0.8f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lookPoint, 1.0f);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, lookPoint);
    }
#endif

    float EstimateCurveRadius(Spline spline, float s)
    {
        float ds = 2.0f / spline.length;
        float s0 = Mathf.Clamp01(s - ds);
        float s2 = Mathf.Clamp01(s + ds);
        Vector3 p0 = spline.GetPosition(s0, Spline.WrapMode.Clamp);
        Vector3 p1 = spline.GetPosition(s, Spline.WrapMode.Clamp);
        Vector3 p2 = spline.GetPosition(s2, Spline.WrapMode.Clamp);
        return EstimateCurveRadiusRaw(p0, p1, p2);
    }

    static float EstimateCurveRadiusRaw(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float a = Vector3.Distance(p1, p2);
        float b = Vector3.Distance(p0, p2);
        float c = Vector3.Distance(p0, p1);
        float s = (a + b + c) / 2f;
        float area = Mathf.Sqrt(Mathf.Max(s * (s - a) * (s - b) * (s - c), 0.00001f));
        if (area < 1e-4f) return 99999f;
        return (a * b * c) / (4f * area);
    }
}
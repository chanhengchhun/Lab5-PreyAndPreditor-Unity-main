using UnityEngine;

public class Agent : MonoBehaviour
{
    private static readonly float[] WallTurnAngles = { -150f, -120f, -90f, -60f, -30f, 30f, 60f, 90f, 120f, 150f, 180f };

    public float speed = 5f;
    public float detectionRadius = 0.5f;
    public Vector3 heading;

    [SerializeField] private float wanderSpeedMultiplier = 0.5f;
    [SerializeField] private float minTurnInterval = 0.6f;
    [SerializeField] private float maxTurnInterval = 1.6f;
    [SerializeField] private float maxTurnAngle = 55f;
    [SerializeField] protected LayerMask obstacleMask;
    [SerializeField] private float collisionProbeRadius = 0.35f;
    [SerializeField] private float wallProbeDistance = 1.2f;
    [SerializeField] private float probeHeight = 0.5f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float wallTurnLockDuration = 0.3f;

    private float nextTurnTime;
    private float forcedTurnUntil;
    private Vector3 forcedTurnHeading;
    private Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (heading.sqrMagnitude < 0.001f)
        {
            heading = RandomFlatDirection();
        }

        heading = FlatNormalized(heading);
        ScheduleNextTurn();
    }

    protected void MoveWander()
    {
        if (Time.time >= forcedTurnUntil && TryNudgeAwayFromWall())
        {
            MoveInHeading(wanderSpeedMultiplier);
            return;
        }

        if (Time.time >= nextTurnTime && Time.time >= forcedTurnUntil)
        {
            heading = FlatNormalized(Quaternion.Euler(0f, Random.Range(-maxTurnAngle, maxTurnAngle), 0f) * heading);
            if (heading.sqrMagnitude < 0.001f)
            {
                heading = Vector3.forward;
            }

            ScheduleNextTurn();
        }

        MoveInHeading(wanderSpeedMultiplier);
    }

    protected void MoveInHeading(float speedMultiplier = 1f)
    {
        Vector3 moveDirection = Time.time < forcedTurnUntil ? forcedTurnHeading : heading;
        moveDirection = FlatNormalized(moveDirection);
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            SetVelocity(Vector3.zero);
            return;
        }

        if (Time.time >= forcedTurnUntil)
        {
            Vector3 probeOrigin = GetProbeOrigin();
            float probeDistance = GetProbeDistance(speedMultiplier);
            if (TrySphereCast(probeOrigin, moveDirection, probeDistance, out _) &&
                !TryGetWallTurnDirection(probeOrigin, moveDirection, probeDistance, out moveDirection))
            {
                SetVelocity(Vector3.zero);
                return;
            }
        }

        heading = moveDirection;
        SetVelocity(moveDirection * (speed * speedMultiplier));
        RotateToHeading();
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision == null || Time.time < forcedTurnUntil)
        {
            return;
        }

        if (!ShouldTreatAsObstacle(collision.collider))
        {
            return;
        }

        Vector3 currentDirection = FlatNormalized(heading.sqrMagnitude < 0.001f ? transform.forward : heading);
        if (currentDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (!TryGetWallTurnDirection(GetProbeOrigin(), currentDirection, GetProbeDistance(1f), out Vector3 turnDirection))
        {
            return;
        }

        heading = turnDirection;
        SetVelocity(turnDirection * speed);
        RotateToHeading();
    }

    protected Transform GetClosestTargetInRange(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        Transform closest = null;
        float closestDistanceSquared = detectionRadius * detectionRadius;

        foreach (Transform child in parent)
        {
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 offset = child.position - transform.position;
            offset.y = 0f;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closest = child;
        }

        return closest;
    }

    private bool TryGetWallTurnDirection(Vector3 origin, Vector3 currentDirection, float probeDistance, out Vector3 turnDirection)
    {
        float bestClearance = -1f;
        Vector3 bestDirection = Vector3.zero;
        int randomStart = Random.Range(0, WallTurnAngles.Length);

        for (int i = 0; i < WallTurnAngles.Length; i++)
        {
            int index = (randomStart + i) % WallTurnAngles.Length;
            Vector3 candidate = FlatNormalized(Quaternion.Euler(0f, WallTurnAngles[index], 0f) * currentDirection);
            if (candidate.sqrMagnitude < 0.001f)
            {
                continue;
            }

            float clearance = MeasureClearance(origin, candidate, probeDistance * 1.25f);
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestDirection = candidate;
            }
        }

        if (bestDirection.sqrMagnitude >= 0.001f)
        {
            ApplyForcedTurn(bestDirection);
        }

        turnDirection = bestDirection;
        return bestClearance > collisionProbeRadius * 0.1f;
    }

    private bool TryNudgeAwayFromWall()
    {
        Vector3 currentDirection = FlatNormalized(heading);
        if (currentDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        Vector3 probeOrigin = GetProbeOrigin();
        float probeDistance = GetProbeDistance(wanderSpeedMultiplier) * 0.75f;
        if (!TrySphereCast(probeOrigin, currentDirection, probeDistance, out _))
        {
            return false;
        }

        return TryGetWallTurnDirection(probeOrigin, currentDirection, probeDistance, out _);
    }

    private float MeasureClearance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        if (TrySphereCast(origin, direction, maxDistance, out RaycastHit hit))
        {
            return Mathf.Max(0f, hit.distance);
        }

        return maxDistance;
    }

    private void ApplyForcedTurn(Vector3 direction)
    {
        direction = FlatNormalized(direction);
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        heading = direction;
        forcedTurnHeading = direction;
        forcedTurnUntil = Time.time + wallTurnLockDuration;
        ScheduleNextTurn();
    }

    private bool ShouldTreatAsObstacle(Collider collider)
    {
        if (collider == null || collider.transform.IsChildOf(transform))
        {
            return false;
        }

        if (collider.GetComponentInParent<Agent>() != null)
        {
            return false;
        }

        if (obstacleMask.value == 0)
        {
            return true;
        }

        return (obstacleMask.value & (1 << collider.gameObject.layer)) != 0;
    }

    private bool TrySphereCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        int castMask = obstacleMask.value == 0 ? Physics.DefaultRaycastLayers : obstacleMask.value;
        RaycastHit[] hits = Physics.SphereCastAll(origin, collisionProbeRadius, direction, distance, castMask, QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        RaycastHit closestHit = default;
        bool foundObstacle = false;

        foreach (RaycastHit castHit in hits)
        {
            if (!ShouldTreatAsObstacle(castHit.collider))
            {
                continue;
            }

            if (castHit.distance < closestDistance)
            {
                closestDistance = castHit.distance;
                closestHit = castHit;
                foundObstacle = true;
            }
        }

        hit = closestHit;
        return foundObstacle;
    }

    protected void RotateToHeading()
    {
        Vector3 flatHeading = FlatNormalized(heading);
        if (flatHeading.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatHeading, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ScheduleNextTurn()
    {
        float minInterval = Mathf.Max(0.05f, minTurnInterval);
        float maxInterval = Mathf.Max(minInterval, maxTurnInterval);
        nextTurnTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    private Vector3 GetProbeOrigin()
    {
        return transform.position + Vector3.up * probeHeight;
    }

    private float GetProbeDistance(float speedMultiplier)
    {
        float speedDistance = speed * Mathf.Max(0.1f, speedMultiplier) * 0.35f;
        return Mathf.Max(wallProbeDistance, collisionProbeRadius + speedDistance);
    }

    private void SetVelocity(Vector3 velocity)
    {
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    private static Vector3 FlatNormalized(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        return direction.normalized;
    }

    private static Vector3 RandomFlatDirection()
    {
        Vector2 random = Random.insideUnitCircle.normalized;
        return new Vector3(random.x, 0f, random.y);
    }
}

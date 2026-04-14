using UnityEngine;

/// Shared movement code for prey and predator.
public class Agent : MonoBehaviour
{
    private static readonly float[] WallTurnAngles = { -150f, -120f, -90f, -60f, -30f, 30f, 60f, 90f, 120f, 150f, 180f };
    public float speed = 5.0f;
    private readonly float detectionRadius = 6.0f;
    public Vector3 heading;
    private readonly float wanderSpeedMultiplier = 0.4f;
    private readonly float minTurnInterval = 0.6f;
    private readonly float maxTurnInterval = 1.6f;
    private readonly float maxTurnAngle = 55.0f;
    private readonly float collisionProbeRadius = 0.35f;
    private readonly float wallProbeDistance = 1.2f;
    private readonly float probeHeight = 0.5f;
    private readonly float rotationSpeed = 540f;
    private readonly float wallTurnLockDuration = 0.3f;

    private float nextTurnTime;
    private float forcedTurnUntil;
    private Vector3 forcedTurnHeading;
    private Rigidbody rb;

    /// Initializes movement state and assigns a random starting direction when none is set.
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

    /// Applies wandering behavior with periodic random turns and early wall avoidance.
    protected void MoveWander()
    {
        // If a wall is directly ahead, pick a new direction first.
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

    /// Moves the agent in its current heading and redirects if a wall is detected ahead.
    /// <param name="speedMultiplier">Multiplier applied to the base movement speed.</param>
    protected void MoveInHeading(float speedMultiplier = 1.0f)
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

            // Probe ahead and redirect before we push into a wall.
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

    /// Forces an immediate turn when the agent physically collides with a wall.
    /// <param name="collision">The collision reported by Unity physics.</param>
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

    /// Finds the nearest agent of type T within detection range.
    /// Use a smaller fovAngle for forward vision, or 360 for all-around vision.
    protected Transform GetClosestTargetInRange<T>(float fovAngle = 360f) where T : Agent
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
        Transform closest = null;
        float closestDistSq = float.MaxValue;
        float halfFov = fovAngle * 0.5f;

        foreach (Collider col in hits)
        {
            T agent = col.GetComponentInParent<T>();
            if (agent == null || agent.transform == transform)
            {
                continue;
            }

            if (!agent.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 offset = agent.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.001f)
            {
                continue;
            }

            // Skip the angle check when the agent has full 360 vision.
            if (halfFov < 180f)
            {
                float angle = Vector3.Angle(transform.forward, offset);
                if (angle > halfFov)
                {
                    continue;
                }
            }

            float distSq = offset.sqrMagnitude;
            if (distSq < closestDistSq)
            {
                closestDistSq = distSq;
                closest = agent.transform;
            }
        }

        return closest;
    }

    /// Chooses the turn direction with the most free space from a fixed set of escape angles.
    /// <param name="origin">Probe origin for clearance tests.</param>
    /// <param name="currentDirection">Current forward direction.</param>
    /// <param name="probeDistance">Base distance used to test open space.</param>
    /// <param name="turnDirection">Selected turn direction when a valid path is found.</param>
    /// <returns>True when a usable wall-avoidance direction is found.</returns>
    private bool TryGetWallTurnDirection(Vector3 origin, Vector3 currentDirection, float probeDistance, out Vector3 turnDirection)
    {
        float bestClearance = -1f;
        Vector3 bestDirection = Vector3.zero;
        int randomStart = Random.Range(0, WallTurnAngles.Length);

        // Try several turn angles and keep the direction with the most open space.
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

    /// Detects a nearby wall during wandering and triggers a turn before contact.
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

    /// Measures how far the agent can move in a direction before hitting an obstacle.
    private float MeasureClearance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        if (TrySphereCast(origin, direction, maxDistance, out RaycastHit hit))
        {
            return Mathf.Max(0f, hit.distance);
        }

        return maxDistance;
    }

    /// Locks the agent into a new heading briefly so it can clear the wall it just avoided.
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

    /// Treats any non-agent collider as a wall or obstacle.
    private bool ShouldTreatAsObstacle(Collider collider)
    {
        if (collider == null || collider.transform.IsChildOf(transform))
        {
            return false;
        }

        // Other agents are not walls.
        if (collider.GetComponentInParent<Agent>() != null)
        {
            return false;
        }

        return true;
    }

    /// Sphere casts forward and returns the closest obstacle hit.
    private bool TrySphereCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionProbeRadius,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

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

    /// Rotates the model so it faces its current heading on the XZ plane.
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

    /// Schedules the next random turn.
    private void ScheduleNextTurn()
    {
        float minInterval = Mathf.Max(0.05f, minTurnInterval);
        float maxInterval = Mathf.Max(minInterval, maxTurnInterval);
        nextTurnTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    /// Returns the origin used for wall-detection probes.
    private Vector3 GetProbeOrigin()
    {
        return transform.position + Vector3.up * probeHeight;
    }

    /// Computes the forward probe length based on speed and the minimum wall distance.
    private float GetProbeDistance(float speedMultiplier)
    {
        float speedDistance = speed * Mathf.Max(0.1f, speedMultiplier) * 0.35f;
        return Mathf.Max(wallProbeDistance, collisionProbeRadius + speedDistance);
    }

    /// Writes velocity to the rigidbody if one is attached.
    private void SetVelocity(Vector3 velocity)
    {
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    /// Removes vertical motion and returns a normalized XZ direction.
    private static Vector3 FlatNormalized(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        return direction.normalized;
    }

    /// Creates a random normalized direction on the XZ plane.
    private static Vector3 RandomFlatDirection()
    {
        Vector2 random = Random.insideUnitCircle.normalized;
        return new Vector3(random.x, 0f, random.y);
    }
}

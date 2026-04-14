using UnityEngine;

public class Agent : MonoBehaviour
{
    public float speed = 5f;
    public float detectionRadius = 0.5f;
    public Vector3 heading;

    [SerializeField] private bool lockToStartHeight = true;
    [SerializeField] private float movementPlaneY = 0f;
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

        if (lockToStartHeight)
        {
            movementPlaneY = transform.position.y;
        }

        if (heading.sqrMagnitude < 0.001f)
        {
            Vector2 random = Random.insideUnitCircle.normalized;
            heading = new Vector3(random.x, 0f, random.y);
        }

        heading.y = 0f;
        heading.Normalize();
        ScheduleNextTurn();
    }

    protected void EnforceFlatMovement()
    {
        Vector3 position = transform.position;
        position.y = movementPlaneY;
        transform.position = position;
    }

    protected void MoveWander()
    {
        if (Time.time >= nextTurnTime)
        {
            float turn = Random.Range(-maxTurnAngle, maxTurnAngle);
            heading = Quaternion.Euler(0f, turn, 0f) * heading;
            heading.y = 0f;

            if (heading.sqrMagnitude < 0.001f)
            {
                heading = Vector3.forward;
            }

            heading.Normalize();
            ScheduleNextTurn();
        }

        MoveInHeading(wanderSpeedMultiplier);
    }

    protected void MoveInHeading(float speedMultiplier = 1f)
    {
        Vector3 moveDirection = heading;
        if (Time.time < forcedTurnUntil && forcedTurnHeading.sqrMagnitude >= 0.001f)
        {
            moveDirection = forcedTurnHeading;
        }

        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
            return;
        }

        moveDirection.Normalize();

        Vector3 probeOrigin = transform.position + Vector3.up * probeHeight;
        float probeDistance = GetProbeDistance(speedMultiplier);
        bool blocked = TrySphereCast(probeOrigin, moveDirection, probeDistance, out _);

        if (blocked && Time.time >= forcedTurnUntil)
        {
            if (!TryGetWallTurnDirection(probeOrigin, moveDirection, probeDistance, out moveDirection))
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }
                return;
            }

            ApplyForcedTurn(moveDirection);
        }

        heading = moveDirection;
        if (rb != null)
        {
            rb.linearVelocity = moveDirection * (speed * speedMultiplier);
        }

        RotateToHeading();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || Time.time < forcedTurnUntil)
        {
            return;
        }

        HandleWallCollision(collision.collider);
    }

    private void HandleWallCollision(Collider collider)
    {
        if (!ShouldTreatAsObstacle(collider))
        {
            return;
        }

        Vector3 currentDirection = heading;
        currentDirection.y = 0f;
        if (currentDirection.sqrMagnitude < 0.001f)
        {
            currentDirection = transform.forward;
            currentDirection.y = 0f;
        }

        if (currentDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        currentDirection.Normalize();
        Vector3 probeOrigin = transform.position + Vector3.up * probeHeight;

        if (!TryGetWallTurnDirection(probeOrigin, currentDirection, GetProbeDistance(1f), out Vector3 turnDirection))
        {
            return;
        }

        ApplyForcedTurn(turnDirection);
        if (rb != null)
        {
            rb.linearVelocity = turnDirection * speed;
        }

        RotateToHeading();
    }

    private float GetProbeDistance(float speedMultiplier)
    {
        float speedDistance = speed * Mathf.Max(0.1f, speedMultiplier) * 0.35f;
        return Mathf.Max(wallProbeDistance, collisionProbeRadius + speedDistance);
    }

    private bool TryGetWallTurnDirection(Vector3 origin, Vector3 currentDirection, float probeDistance, out Vector3 turnDirection)
    {
        float bestClearance = -1f;
        Vector3 bestDirection = Vector3.zero;
        int randomStart = Random.Range(0, 3);

        for (int i = 0; i < 3; i++)
        {
            int index = (randomStart + i) % 3;
            float turnAngle = index switch
            {
                0 => -90f,
                1 => 90f,
                _ => 180f
            };

            Vector3 candidate = Quaternion.Euler(0f, turnAngle, 0f) * currentDirection;
            candidate.y = 0f;
            if (candidate.sqrMagnitude < 0.001f)
            {
                continue;
            }

            candidate.Normalize();
            float clearance = MeasureClearance(origin, candidate, probeDistance);

            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestDirection = candidate;
            }
        }

        turnDirection = bestDirection;
        return bestClearance > collisionProbeRadius * 0.1f;
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
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        direction.Normalize();
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
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionProbeRadius,
            direction,
            distance,
            castMask,
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

    protected void RotateToHeading()
    {
        Vector3 flatHeading = heading;
        flatHeading.y = 0f;
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
}



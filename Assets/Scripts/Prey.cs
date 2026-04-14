using UnityEngine;

// Flee from the closest predator in range.
public class Prey : Agent
{
    [SerializeField] private float fleeSpeedMultiplier = 1.3f;

    private void FixedUpdate()
    {
        Transform threat = GetClosestTargetInRange<Predator>();
        if (threat == null)
        {
            MoveWander();
            return;
        }

        Vector3 fleeDirection = transform.position - threat.position;
        fleeDirection.y = 0f;
        if (fleeDirection.sqrMagnitude > 0.001f)
        {
            heading = fleeDirection.normalized;
        }

        MoveInHeading(fleeSpeedMultiplier);
    }
}

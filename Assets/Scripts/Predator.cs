using UnityEngine;

public class Predator : Agent
{
    // Drag the EMPTY parent object "Preys" here in the Inspector
    public Transform preyParent;
    
    [SerializeField] private float visionAngle = 90f;
    [SerializeField] private bool showDebugRays = true;

    void Update()
    {
        // Find the specific prey that is closest right now AND in field of view
        Transform targetPrey = (preyParent != null && preyParent.childCount > 0) ? GetClosestPreyInFOV() : null;

        if (targetPrey != null)
        {
            float distance = Vector3.Distance(transform.position, targetPrey.position);

            if (distance < detectionRadius)
            {
                // Chase Logic toward the closest prey
                heading = (targetPrey.position - transform.position).normalized;
                MoveInHeading();
            }
            else
            {
                // Wander Logic goes here
                MoveWander();
            }
        }
        else
        {
            MoveWander();
        }
    }

    /// <summary>
    /// Find closest prey that is within field of view and line of sight.
    /// </summary>
    Transform GetClosestPreyInFOV()
    {
        Transform closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (Transform childPrey in preyParent)
        {
            float distanceToChild = Vector3.Distance(transform.position, childPrey.position);
            
            // Check distance first
            if (distanceToChild > detectionRadius)
            {
                continue;
            }

            // Check if in field of view
            Vector3 directionToPrey = (childPrey.position - transform.position).normalized;
            float angleTowardsPrey = Vector3.Angle(heading, directionToPrey);

            if (angleTowardsPrey > visionAngle * 0.5f)
            {
                if (showDebugRays)
                {
                    Debug.DrawLine(transform.position, childPrey.position, Color.red, 0.01f);
                }
                continue;
            }

            // Check line of sight
            Ray rayToPrey = new Ray(transform.position + Vector3.up * 0.5f, directionToPrey);
            bool blocked = Physics.Raycast(rayToPrey, distanceToChild, obstacleMask);

            if (showDebugRays)
            {
                Color rayColor = blocked ? Color.red : Color.green;
                Debug.DrawLine(transform.position, childPrey.position, rayColor, 0.01f);
            }

            if (blocked)
            {
                continue;
            }

            // This prey is visible!
            if (distanceToChild < closestDistance)
            {
                closestDistance = distanceToChild;
                closest = childPrey;
            }
        }

        return closest;
    }

    /// <summary>
    /// Draw FOV cone in Scene view for debugging.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showDebugRays) return;

        Gizmos.color = Color.yellow;
        float halfAngle = visionAngle * 0.5f;
        
        // Draw two rays for FOV boundaries
        Vector3 leftBound = Quaternion.Euler(0f, -halfAngle, 0f) * heading;
        Vector3 rightBound = Quaternion.Euler(0f, halfAngle, 0f) * heading;

        Gizmos.DrawLine(transform.position, transform.position + leftBound * detectionRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBound * detectionRadius);
            Gizmos.DrawLine(
                transform.position + leftBound * detectionRadius,
                transform.position + rightBound * detectionRadius
            );
    }
}
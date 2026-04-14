using UnityEngine;

public class Prey : Agent
{
    public Transform predatorParent;

    private void Update()
    {
        Transform threat = GetClosestTargetInRange(predatorParent);
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

        MoveInHeading();
    }
}

using UnityEngine;

public class Prey : Agent
{
    public Transform predator;
    
    // Update is called once per frame
    void Update()
    {
        bool predatorDetected = false;
        if (predator != null)
        {
            float distance = Vector3.Distance(transform.position, predator.position);
            predatorDetected = distance < detectionRadius;
        }

        if (predatorDetected)
        {
            // flee logic
            heading = (transform.position - predator.position).normalized;
            MoveInHeading();
        }
        else
        {
            // wander logic
            MoveWander();
        }
    }
}

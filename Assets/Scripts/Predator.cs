using UnityEngine;

/// Chase the closest prey inside the predator's forward view cone.
public class Predator : Agent
{
    [SerializeField] private float fieldOfViewAngle = 120f;

    private void FixedUpdate()
    {
        Transform targetPrey = GetClosestTargetInRange<Prey>(fieldOfViewAngle);
        if (targetPrey == null)
        {
            MoveWander();
            return;
        }

        Vector3 chaseDirection = targetPrey.position - transform.position;
        chaseDirection.y = 0f;
        if (chaseDirection.sqrMagnitude > 0.001f)
        {
            heading = chaseDirection.normalized;
        }

        MoveInHeading();
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        // A prey is removed as soon as the predator makes contact.
        Prey prey = collision?.collider.GetComponentInParent<Prey>();
        if (prey != null)
        {
            Destroy(prey.gameObject);
        }
    }
}

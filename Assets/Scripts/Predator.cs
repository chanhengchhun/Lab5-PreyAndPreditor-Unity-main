using UnityEngine;

public class Predator : Agent
{
    public Transform preyParent;

    private void Update()
    {
        Transform targetPrey = GetClosestTargetInRange(preyParent);
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

        if (collision == null)
        {
            return;
        }

        Prey prey = collision.collider.GetComponentInParent<Prey>();
        if (prey != null)
        {
            Destroy(prey.gameObject);
        }
    }
}

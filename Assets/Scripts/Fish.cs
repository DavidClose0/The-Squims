using UnityEngine;
using UnityEngine.AI;

public class Fish : MonoBehaviour
{
    NavMeshAgent agent;
    public float wanderRadius = 10f;
    public float fleeRadius = 10f; // Distance within which to flee Squims
    public float fleeDistance = 15f; // How far to attempt to flee

    private bool isFleeing = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on Fish!");
            return;
        }
        GoToRandomPoint();
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Transform nearestSquim = FindNearestSquim();

        if (nearestSquim != null)
        {
            // Flee from the nearest Squim
            Vector3 fleeDirection = (transform.position - nearestSquim.position).normalized;
            Vector3 targetFleePos = transform.position + fleeDirection * fleeDistance;

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(targetFleePos, out navHit, fleeDistance, NavMesh.AllAreas))
            {
                if (!isFleeing || Vector3.Distance(agent.destination, navHit.position) > 1.0f)
                {
                    agent.SetDestination(navHit.position);
                    isFleeing = true;
                }
            }
        }
        else
        {
            // No Squim nearby, revert to wandering
            if (isFleeing)
            {
                isFleeing = false;
            }

            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                GoToRandomPoint();
            }
        }
    }

    Transform FindNearestSquim()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, fleeRadius);
        Transform nearest = null;
        float minDistanceSqr = fleeRadius * fleeRadius;

        foreach (var hitCollider in hitColliders)
        {
            Squim squim = hitCollider.GetComponent<Squim>();
            if (squim != null)
            {
                float distanceSqr = (hitCollider.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    nearest = hitCollider.transform;
                }
            }
        }
        return nearest; // Returns null if no Squim is within fleeRadius
    }

    void GoToRandomPoint()
    {
        Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, NavMesh.AllAreas);
        if (newPos != Vector3.zero)
        {
            agent.SetDestination(newPos);
        }
    }

    // Static utility function to find a random point on the NavMesh
    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randDirection = Random.insideUnitSphere * dist;
        randDirection += origin;
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randDirection, out navHit, dist, layermask))
        {
            return navHit.position;
        }
        return Vector3.zero;
    }
}
using UnityEngine;
using UnityEngine.U2D.Animation;

public class Enemy : MonoBehaviour
{
    public static Enemy Create(Vector3 position)
    {
        Transform enemyTransform = Resources.Load<Transform>("PFEnemy");
        Transform enemyInstance = Instantiate(enemyTransform, position, Quaternion.identity);
        Enemy enemy = enemyInstance.GetComponent<Enemy>();
        return enemy;
    }

    private Transform targetTransform;
    private Rigidbody2D rigidbody2D;
    private float moveSpeed = 6f;
    private float targetLookRadius = 10f;
    private float lookForTargetTimer;
    private float lookForTargetTimerMax = 0.2f;
    private HealthSystem healthSystem;
    [SerializeField] private Transform enemyVisual;
    private bool isFacingLeft = true;
    private float facingDeadZone = 0.1f;

    [Header("Resource Node Avoidance")]
    [Tooltip("Probe circle radius. Kept near the enemy's own collider radius so it reads as 'my body is blocked'.")]
    [SerializeField] private float avoidanceProbeRadius = 0.45f;
    [Tooltip("How far ahead the enemy looks for a resource node.")]
    [SerializeField] private float avoidanceProbeDistance = 2.5f;
    [Tooltip("Degrees added per step when widening the turn around a node.")]
    [SerializeField] private float avoidanceAngle = 35f;
    [Tooltip("Seconds between probes. The chosen deviation is reused between probes.")]
    [SerializeField] private float avoidanceInterval = 0.2f;
    [Tooltip("How many times the turn is widened before committing to the widest one.")]
    [Range(1, 3)] [SerializeField] private int avoidanceProbeSteps = 2;

    private readonly RaycastHit2D[] avoidanceResults = new RaycastHit2D[16];
    private ContactFilter2D avoidanceFilter;
    private float avoidanceTimer;
    private float avoidanceDeviation;

    private void Start()
    {
        if(BuildingManager.Instance.GetTownHallBuilding() != null)
        {
            targetTransform = BuildingManager.Instance.GetTownHallBuilding().transform;
            rigidbody2D = GetComponent<Rigidbody2D>();
        }
        lookForTargetTimer = Random.Range(0f, lookForTargetTimerMax);
        healthSystem = GetComponent<HealthSystem>();
        healthSystem.OnDead += HealthSystem_OnDead;

        avoidanceFilter = new ContactFilter2D { useTriggers = true };
        // Stagger the probes so a wave never evaluates every enemy on the same frame.
        avoidanceTimer = Random.Range(0f, avoidanceInterval);

        HandleFacing();
    }

   

    private void Update()
    {
        HandleMovement();
        HandleTarget();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Building building = collision.gameObject.GetComponent<Building>();
        if (building != null)
        {
            HealthSystem healthSystem = building.GetComponent<HealthSystem>();
            if (healthSystem != null)
            {
                healthSystem.Damage(10);
                Destroy(gameObject);
            }
        }
    }

    private void HandleMovement()
    {
        if (targetTransform == null || rigidbody2D == null)
        {
            return;
        }

        Vector3 seekDirection = (targetTransform.position - transform.position).normalized;

        avoidanceTimer -= Time.deltaTime;
        if (avoidanceTimer <= 0f)
        {
            avoidanceTimer += avoidanceInterval;
            avoidanceDeviation = ComputeAvoidanceDeviation(seekDirection);
        }

        Vector3 moveDir = avoidanceDeviation == 0f
            ? seekDirection
            : Quaternion.Euler(0f, 0f, avoidanceDeviation) * seekDirection;

        rigidbody2D.linearVelocity = moveDir * moveSpeed;
    }

    /// <summary>
    /// Returns the Z rotation, in degrees, that steers clear of the nearest resource node
    /// blocking the seek direction. Zero means the line to the target is clear.
    /// </summary>
    private float ComputeAvoidanceDeviation(Vector3 seekDirection)
    {
        if (!TryGetResourceNodeAhead(seekDirection, out Vector2 obstaclePoint))
        {
            return 0f; // Clear: keep the original straight-line behaviour exactly.
        }

        // Turn to whichever side the node is not on. Deriving the side from the node's
        // position rather than a fixed handedness keeps the route pointed at the target.
        Vector3 toObstacle = (Vector3)obstaclePoint - transform.position;
        float side = Mathf.Sign(toObstacle.x * seekDirection.y - toObstacle.y * seekDirection.x);
        if (side == 0f)
        {
            side = 1f; // Dead ahead: pick a side so the next probe has a new heading to test.
        }

        // Widen the turn until a heading clears the node. The smallest deviation that
        // works wins, so the enemy stays as direct as it can be.
        for (int step = 1; step <= avoidanceProbeSteps; step++)
        {
            float deviation = side * avoidanceAngle * step;
            if (!TryGetResourceNodeAhead(Quaternion.Euler(0f, 0f, deviation) * seekDirection, out _))
            {
                return deviation;
            }
        }

        return side * avoidanceAngle * avoidanceProbeSteps; // Surrounded: commit to the widest turn.
    }

    private bool TryGetResourceNodeAhead(Vector3 direction, out Vector2 obstaclePoint)
    {
        obstaclePoint = default;

        int count = Physics2D.CircleCast(
            transform.position,
            avoidanceProbeRadius,
            direction,
            avoidanceFilter,
            avoidanceResults,
            avoidanceProbeDistance);

        for (int i = 0; i < count; i++)
        {
            Collider2D hitCollider = avoidanceResults[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            // Physics2D.queriesStartInColliders is on project-wide, so the enemy's own
            // collider, other enemies and buildings all land in these results. Resource
            // nodes are the only triggers among them, and the cheap flag check discards
            // the rest before any hierarchy lookup happens.
            if (!hitCollider.isTrigger)
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<ResourceNode>() == null)
            {
                continue;
            }

            obstaclePoint = avoidanceResults[i].point;
            return true;
        }

        return false;
    }

    private void HandleFacing()
    {
        if (targetTransform == null)
        {
            return;
        }

        bool shouldFaceLeft = targetTransform.position.x < transform.position.x;
        if (shouldFaceLeft == isFacingLeft)
        {
            return;
        }

        isFacingLeft = shouldFaceLeft;
        Vector3 visualScale = enemyVisual.localScale;
        visualScale.x = Mathf.Abs(visualScale.x) * (shouldFaceLeft ? 1f : -1f);
        enemyVisual.localScale = visualScale;
    }

    private void HandleTarget()
    {
        lookForTargetTimer -= Time.deltaTime;
        if (lookForTargetTimer <= 0f)
        {
            lookForTargetTimer += lookForTargetTimerMax;
            LookCloserTarget();
        }
    }

    private void LookCloserTarget()
    {
        Collider2D[] tentantTargets = Physics2D.OverlapCircleAll(transform.position, targetLookRadius);
        foreach(Collider2D collider in tentantTargets)
        {
            Building building = collider.GetComponent<Building>();
            if (building != null)
            {
                if(targetTransform == null)
                {
                    targetTransform = building.transform;
                    HandleFacing();
                }
                else
                {
                    float currentTargetDistance = Vector3.Distance(transform.position, targetTransform.position);
                    float newTargetDistance = Vector3.Distance(transform.position, building.transform.position);
                    if (newTargetDistance < currentTargetDistance)
                    {
                        targetTransform = building.transform;
                        HandleFacing();
                    }
                }
                return;
            }
        }

        if(targetTransform == null)
        {
            if (BuildingManager.Instance.GetTownHallBuilding() != null)
            {
                targetTransform = BuildingManager.Instance.GetTownHallBuilding().transform;
                HandleFacing();
            }
        }

    }

    private void HealthSystem_OnDead(object sender, System.EventArgs e)
    {
        Destroy(gameObject);
    }
}

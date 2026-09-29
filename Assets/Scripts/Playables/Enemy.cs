using UnityEngine;

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
        if (targetTransform != null)
        {
            Vector3 moveDir = (targetTransform.position - transform.position).normalized;
            rigidbody2D.linearVelocity = moveDir * moveSpeed;
        }

        
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
                }
                else
                {
                    float currentTargetDistance = Vector3.Distance(transform.position, targetTransform.position);
                    float newTargetDistance = Vector3.Distance(transform.position, building.transform.position);
                    if (newTargetDistance < currentTargetDistance)
                    {
                        targetTransform = building.transform;
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
            }
        }
    }

    private void HealthSystem_OnDead(object sender, System.EventArgs e)
    {
        Destroy(gameObject);
    }
}

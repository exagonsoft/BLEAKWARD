using UnityEngine;

public class Tower : MonoBehaviour
{
    private Enemy targetEnemy;
    [SerializeField] private float targetLookRadius = 30f;
    [SerializeField] private Transform turretGunTransform;
    [SerializeField] private Transform turretBase;
    [SerializeField] private Transform turretBaseLeft;
    [SerializeField] private Transform turretBaseRight;
    [SerializeField] private Transform turretGun;
    [SerializeField] private float turretRotationSpeed = 180f;
    private bool handleTurretRotation = true;
    private float lookForTargetTimer;
    private float lookForTargetTimerMax = 0.2f;
    private float shootTimer;
    [SerializeField] private float shootTimerMax = 1f;

    private void Awake()
    {
        if(turretGunTransform == null || turretBase == null)
        {
            handleTurretRotation = false;
        }
    }

    private void Update()
    {
        HandleTarget();
        if (handleTurretRotation)
        {
            RotateTurret();
        }
        HandleShooting();
    }

    private void HandleShooting()
    {
        shootTimer -= Time.deltaTime;
        if(shootTimer <= 0f)
        {
            shootTimer += shootTimerMax;
            if (targetEnemy != null)
            {
                Proyectil.Create(turretGunTransform.position, targetEnemy);
            }
        }
    }


    private void LookCloserTarget()
    {
        Collider2D[] tentantTargets = Physics2D.OverlapCircleAll(transform.position, targetLookRadius);
        foreach (Collider2D collider in tentantTargets)
        {
            Enemy enemy = collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                if (targetEnemy == null)
                {
                    targetEnemy = enemy;
                }
                else
                {
                    float currentTargetDistance = Vector3.Distance(transform.position, targetEnemy.transform.position);
                    float newTargetDistance = Vector3.Distance(transform.position, enemy.transform.position);
                    if (newTargetDistance < currentTargetDistance)
                    {
                        targetEnemy = enemy;
                    }
                }
            }
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

    private void RotateTurret()
    {
        if (targetEnemy == null)
        {
            turretGun.localRotation = Quaternion.RotateTowards(
                turretGun.localRotation,
                Quaternion.identity,
                turretRotationSpeed * Time.deltaTime
            );

            return;
        }

        Vector3 scale = turretBase.localScale;

        // Determine target direction
        Vector3 direction =
            targetEnemy.transform.position - turretGun.position;

        bool isEnemyAtRight = direction.x >= 0f;

        // Determine the turret's current orientation
        bool isFacingRight =
            turretBaseLeft.position.x > turretBaseRight.position.x;

        if (isEnemyAtRight)
        {
            if (!isFacingRight)
            {
                // Flip to face right
                scale.x *= -1f;
                turretBase.localScale = scale;
            }
        }
        else
        {
            if (isFacingRight)
            {
                // Flip to face left
                scale.x *= -1f;
                turretBase.localScale = scale;
            }
        }


        direction = targetEnemy.transform.position - turretGunTransform.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

        turretGun.rotation = Quaternion.RotateTowards(
            turretGun.rotation,
            targetRotation,
            turretRotationSpeed * Time.deltaTime
        );

    }
}

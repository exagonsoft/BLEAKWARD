using System.Collections.Generic;
using UnityEngine;

public class Tower : MonoBehaviour
{
    private Enemy targetEnemy;
    [SerializeField] private float targetLookRadius = 30f;
    [SerializeField] private List<Transform> turretGunTransformList;
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
        if (turretGunTransformList == null || turretGunTransformList.Count == 0 || turretBase == null || turretGun == null ||
            turretBaseLeft == null || turretBaseRight == null)
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
                Vector3 targetPosition = targetEnemy.transform.position;
                foreach (Transform gunTransform in turretGunTransformList)
                {
                    Vector3 moveDir = targetPosition - gunTransform.position;
                    Proyectil.Create(gunTransform.position, moveDir, targetEnemy);
                }
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

        // Aim from the gun's pivot, not the muzzle: the gun rotates about this point,
        // so using the muzzle (which swings with the gun) makes the aim angle feed
        // back into itself. turretBase is NOT the pivot -- it sits 0.52 above it.
        Vector3 direction = targetEnemy.transform.position - turretGun.position;

        bool isEnemyAtRight = direction.x >= 0f;

        // Determine the turret's current orientation
        bool isFacingRight =
            turretBaseLeft.position.x > turretBaseRight.position.x;

        if (isEnemyAtRight != isFacingRight)
        {
            Vector3 scale = turretBase.localScale;
            scale.x *= -1f;
            turretBase.localScale = scale;
            isFacingRight = isEnemyAtRight;
        }


        // The rig's rest pose aims the barrel along -X: gun.localScale.x is -1 and
        // TowerProjectilSpowner sits at local +X 2.05 of the gun.
        float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Mirroring turretBase is a reflection, which inverts the gun's angle.
        float targetAngle = isFacingRight
            ? -aimAngle
            : aimAngle - 180f;

        turretGun.localRotation = Quaternion.RotateTowards(
            turretGun.localRotation,
            Quaternion.Euler(0f, 0f, targetAngle),
            turretRotationSpeed * Time.deltaTime);
    }
}

using UnityEngine;

public class Proyectil : MonoBehaviour
{

    public static Proyectil Create(Vector3 position, Vector3 moveDir, Enemy targetEnemy)
    {
        Transform proyectilTransform = Resources.Load<Transform>("PFTowerProyectile");
        Transform proyectilInstance = Instantiate(proyectilTransform, position, Quaternion.identity);
        Proyectil proyectil = proyectilInstance.GetComponent<Proyectil>();
        // Seed the travel direction at spawn. Without this, a target that is already
        // dead by the first Update leaves the direction at zero and the proyectil
        // sits on the muzzle until lifeTime runs out.
        proyectil.lastMoveDir = moveDir.sqrMagnitude > 0.0001f ? moveDir.normalized : Vector3.right;
        proyectil.SetTargetEnemy(targetEnemy);
        return proyectil;
    }

    private Enemy targetEnemy;
    private Vector3 lastMoveDir;
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private float damageAmount = 10f;

    private void Update()
    {
        HandleProyectil();

        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void HandleProyectil()
    {
        if (targetEnemy != null)
        {
            Vector3 toTarget = targetEnemy.transform.position - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                lastMoveDir = toTarget.normalized;
            }
        }

        // No target, or a target we are sitting on top of: keep flying forward
        // instead of standing still.
        if (lastMoveDir.sqrMagnitude < 0.0001f)
        {
            lastMoveDir = transform.right;
        }
        
        transform.position += lastMoveDir * moveSpeed * Time.deltaTime;
        transform.eulerAngles = new Vector3(0, 0, UtilsClass.GetAngleFromVectorFloat(lastMoveDir));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy != null)
        {
            HealthSystem healthSystem = enemy.GetComponent<HealthSystem>();
            if (healthSystem != null)
            {
                healthSystem.Damage(damageAmount);
            }
            Destroy(gameObject);
        }
    }

    private void SetTargetEnemy(Enemy targetEnemy)
    {
        this.targetEnemy = targetEnemy;
    }
}

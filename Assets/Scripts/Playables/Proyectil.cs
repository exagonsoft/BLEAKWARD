using UnityEngine;

public class Proyectil : MonoBehaviour
{

    public static Proyectil Create(Vector3 position, Enemy targetEnemy)
    {
        Transform proyectilTransform = Resources.Load<Transform>("PFTowerProyectile");
        Transform proyectilInstance = Instantiate(proyectilTransform, position, Quaternion.identity);
        Proyectil proyectil = proyectilInstance.GetComponent<Proyectil>();
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
        Vector3 moveDir;
        if (targetEnemy != null) {
            moveDir = (targetEnemy.transform.position - transform.position).normalized;
            lastMoveDir = moveDir;
        }
        else
        {
            moveDir = lastMoveDir;
        }
        
        transform.position += moveDir * moveSpeed * Time.deltaTime;
        transform.eulerAngles = new Vector3(0, 0, UtilsClass.GetAngleFromVectorFloat(moveDir));
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

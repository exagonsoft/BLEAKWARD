using UnityEngine;

public class EnemySpowner : MonoBehaviour
{
    [SerializeField] private float spawnTimer = 2f;
    private float spawnTimerMax;

    private void Awake()
    {
        spawnTimerMax = spawnTimer;
    }

    private void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnTimerMax;
            Enemy.Create(transform.position);
        }
    }
}

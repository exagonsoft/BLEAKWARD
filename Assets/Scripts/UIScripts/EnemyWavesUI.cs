using System;
using TMPro;
using UnityEngine;

public class EnemyWavesUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI waveNumberLabel;
    [SerializeField] private TextMeshProUGUI nextWaveMessageLabel;
    [SerializeField] private EnemyWaveManager enemyWaveManager;
    [SerializeField] private RectTransform nextWaveIndicatorRectTransform;
    [SerializeField] private RectTransform closestEnemyIndicatorRectTransform;
    private Camera mainCamera;

    private void Start()
    {
        enemyWaveManager.OnWaveNumberChanged += EnemyWaveManager_OnWaveNumberChanged;
        mainCamera = Camera.main;
        SetWaveNumberText(enemyWaveManager.GetWaveNumber());
    }

    private void Update()
    {
        HandleNextWaveSpawnPositionindicator();
        HandleEnemyClosestPositionIndicator();
    }

    private void EnemyWaveManager_OnWaveNumberChanged(object sender, EventArgs e)
    {
        SetWaveNumberText(enemyWaveManager.GetWaveNumber());
    }

    private void SetMessageText(string message)
    {
        nextWaveMessageLabel.SetText(message);
    }

    private void SetWaveNumberText(int waveNumber)
    {
        waveNumberLabel.SetText("Wave: " + waveNumber);
    }

    private void HandleEnemyClosestPositionIndicator()
    {
        Collider2D[] tentantTargets = Physics2D.OverlapCircleAll(mainCamera.transform.position, 9999f);
        Enemy targetEnemy = null;
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

        if (targetEnemy != null)
        {
            Vector3 directionToClosestEnemyPosition = (targetEnemy.transform.position - mainCamera.transform.position).normalized;
            closestEnemyIndicatorRectTransform.anchoredPosition = directionToClosestEnemyPosition * 250f;
            closestEnemyIndicatorRectTransform.eulerAngles = new Vector3(0, 0, UtilsClass.GetAngleFromVectorFloat(directionToClosestEnemyPosition));

            float distanceToClosestEnemy = Vector3.Distance(targetEnemy.transform.position, mainCamera.transform.position);
            closestEnemyIndicatorRectTransform.gameObject.SetActive(distanceToClosestEnemy > mainCamera.orthographicSize * 1.5f);
        }
        else
        {
            closestEnemyIndicatorRectTransform.gameObject.SetActive(false);
        }
    }

    private void HandleNextWaveSpawnPositionindicator()
    {
        float nextWaveSpawnTimer = enemyWaveManager.GetNextWaveSpawnTimer();
        if (nextWaveSpawnTimer <= 0f)
        {
            SetMessageText("");
        }
        else
        {
            SetMessageText("Next Wave In: " + Mathf.Ceil(nextWaveSpawnTimer) + "s");
        }

        Vector3 direction = (enemyWaveManager.GetNextWaveSpawnPosition() - mainCamera.transform.position).normalized;
        nextWaveIndicatorRectTransform.anchoredPosition = direction * 300f;
        nextWaveIndicatorRectTransform.eulerAngles = new Vector3(0, 0, UtilsClass.GetAngleFromVectorFloat(direction));

        float distanceToNextWaveSpawn = Vector3.Distance(mainCamera.transform.position, enemyWaveManager.GetNextWaveSpawnPosition());
        nextWaveIndicatorRectTransform.gameObject.SetActive(distanceToNextWaveSpawn > mainCamera.orthographicSize * 1.5f);
    }
}

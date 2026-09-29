using System;
using TMPro;
using UnityEngine;

public class EnemyWavesUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI waveNumberLabel;
    [SerializeField] private TextMeshProUGUI nextWaveMessageLabel;
    [SerializeField] private EnemyWaveManager enemyWaveManager;

    private void Start()
    {
        enemyWaveManager.OnWaveNumberChanged += EnemyWaveManager_OnWaveNumberChanged;
        SetWaveNumberText(enemyWaveManager.GetWaveNumber());
    }

    private void Update()
    {
        float nextWaveSpawnTimer = enemyWaveManager.GetNextWaveSpawnTimer();
        if(nextWaveSpawnTimer <= 0f)
        {
            SetMessageText("");
        }
        else
        {
            SetMessageText("Next Wave In: " + Mathf.Ceil(nextWaveSpawnTimer) + "s");
        }


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
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWaveManager : MonoBehaviour
{
    private enum State
    {
        WaitingToSpawnNextWave,
        SpawningWave,
    }
    [SerializeField] private List<Transform> spawnerTransformList;
    [SerializeField] private Transform nextWaveSpawnPointIndicator;

    public event EventHandler OnWaveNumberChanged;
    private State state;
    private float nextWaveSpawnTimer;
    private float nextEnemySpawnTimer;
    private int remainingEnemiesSpawnAmmount;
    private Vector3 spawnPosition;
    private int waveNumber;

    private void Start()
    {
        state = State.WaitingToSpawnNextWave;
        spawnPosition = spawnerTransformList[UnityEngine.Random.Range(0, spawnerTransformList.Count)].position;
        nextWaveSpawnPointIndicator.position = spawnPosition;
        nextWaveSpawnTimer = 3f;
    }

    private void Update()
    {
        switch(state)
        {
            case State.WaitingToSpawnNextWave:
                nextWaveSpawnTimer -= Time.deltaTime;
                if (nextWaveSpawnTimer < 0f)
                {
                    SpawnWave();
                }
                break;
            case State.SpawningWave:
                if (remainingEnemiesSpawnAmmount > 0)
                {
                    nextEnemySpawnTimer -= Time.deltaTime;
                    if (nextEnemySpawnTimer < 0f)
                    {
                        nextEnemySpawnTimer = UnityEngine.Random.Range(0f, .2f);
                        Enemy.Create(spawnPosition + UtilsClass.GetRandomDirection() * UnityEngine.Random.Range(0f, 10f));
                        remainingEnemiesSpawnAmmount--;

                        if(remainingEnemiesSpawnAmmount <= 0)
                        {
                            state = State.WaitingToSpawnNextWave;
                            spawnPosition = spawnerTransformList[UnityEngine.Random.Range(0, spawnerTransformList.Count)].position;
                            nextWaveSpawnPointIndicator.position = spawnPosition;
                            nextWaveSpawnTimer = 10f;
                        }
                    }
                }
                break;
        }
    }

    private void SpawnWave()
    {
        remainingEnemiesSpawnAmmount = 5 + 3 * waveNumber;
        state = State.SpawningWave;
        waveNumber++;
        OnWaveNumberChanged?.Invoke(this, EventArgs.Empty);
    }

    public int GetWaveNumber()
    {
        return waveNumber;
    }

    public float GetNextWaveSpawnTimer()
    {
        return nextWaveSpawnTimer;
    }
}

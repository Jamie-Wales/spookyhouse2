using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SimpleWaveSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public int minEnemiesPerWave = 3;
    public int maxEnemiesPerWave = 8;
    public float spawnInterval = 2f;
    public float timeBetweenWaves = 20f;
    public Transform spawnPoint;
    public float spawnRadius = 5f;
    public List<EnemyData> enemyTypes = new List<EnemyData>();
    
    [Header("References")]
    public Transform playerTransform;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private int enemiesRemaining = 0;
    private float nextWaveTime = 0f;
    private bool isSpawning = false;
    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private Dictionary<GameObject, Coroutine> monitorCoroutines = new Dictionary<GameObject, Coroutine>();
    
    private void Start()
    {
        // Find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                LogDebug($"Found player at {playerTransform.position}");
            }
        }
        
        // Use this transform as spawn point if none is assigned
        if (spawnPoint == null)
        {
            spawnPoint = transform;
            LogDebug("Using spawner transform as spawn point");
        }
        
        // Validate enemy prefabs
        foreach (var enemyData in enemyTypes)
        {
            if (enemyData.enemyPrefab == null)
            {
                Debug.LogError($"Enemy {enemyData.name} does not have a prefab assigned. All enemy data must have a prefab.");
            }
        }
        
        // Start first wave immediately
        nextWaveTime = Time.time;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        activeEnemies.Clear();
        monitorCoroutines.Clear();
    }
    
    private void Update()
    {
        CleanupDestroyedEnemies();
        if (Time.time >= nextWaveTime && !isSpawning && enemiesRemaining <= 0)
        {
            StartCoroutine(SpawnWave());
        }
    }
    
    private void CleanupDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (!activeEnemies[i])
            {
                enemiesRemaining--;
                LogDebug($"Enemy defeated. Remaining: {enemiesRemaining}");
                activeEnemies.RemoveAt(i);
            }
        }
    }
    
    private IEnumerator SpawnWave()
    {
        isSpawning = true;
        currentWave++;
        
        // Random number of enemies for this wave
        int enemyCount = Random.Range(minEnemiesPerWave, maxEnemiesPerWave + 1);
        enemiesRemaining = enemyCount;
        
        LogDebug($"Wave {currentWave}: Spawning {enemyCount} enemies");
        
        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
        
        isSpawning = false;
        nextWaveTime = Time.time + timeBetweenWaves;
    }
    
    // ReSharper disable Unity.PerformanceAnalysis
    private void SpawnEnemy()
    {
        if (enemyTypes.Count == 0)
        {
            LogDebug("No enemy types defined! Cannot spawn enemies.");
            return;
        }
        EnemyData enemyData = enemyTypes[Random.Range(0, enemyTypes.Count)];
        
        if (enemyData.enemyPrefab == null)
        {
            LogDebug($"Error: Enemy type {enemyData.name} has no prefab assigned!");
            return;
        }
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
        Vector3 spawnPosition = spawnPoint.position + spawnOffset;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPosition, out hit, spawnRadius, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        else
        {
            LogDebug($"Warning: Could not find NavMesh near {spawnPosition}. Using original position.");
        }
        GameObject enemy = Instantiate(enemyData.enemyPrefab, spawnPosition, Quaternion.identity);
        LogDebug($"Spawned enemy: {enemyData.enemyName}");
        activeEnemies.Add(enemy);
        
        var controller = enemy.GetComponent<EnemyController>();
        if (!controller)
        {
            controller = enemy.AddComponent<EnemyController>();
            LogDebug("Added EnemyController to prefab instance");
        }
        controller.enemyData = enemyData;
        controller.playerTransform = playerTransform;
        controller.showDebugLogs = showDebugLogs;
        enemy.name = $"{enemyData.enemyName}_{currentWave}_{enemiesRemaining}";
    }
    
    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[WaveSpawner] {message}");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!spawnPoint) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPoint.position, spawnRadius);
    }
}
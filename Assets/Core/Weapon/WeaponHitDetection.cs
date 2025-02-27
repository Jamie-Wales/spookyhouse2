using UnityEngine;

public class WeaponHitDetection : MonoBehaviour
{
    [Header("Hit Detection")]
    public LayerMask hitLayers;
    public float maxDistance = 100f;
    
    [Header("Impact Effects")]
    public GameObject enemyHitEffect;
    public GameObject environmentHitEffect;
    public float impactEffectDuration = 2.0f;
    
    [Header("Debug")]
    public bool showDebugRays = true;
    
    private Camera _mainCamera;
    
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogError("No main camera found for weapon hit detection!");
        }
    }
    
    public void PerformHitDetection(Vector3 muzzlePosition, float damage)
    {
        if (!_mainCamera) return;
        
        Vector3 shootDirection = _mainCamera.transform.forward;
        
        // Draw debug ray
        if (showDebugRays)
        {
            Debug.DrawRay(muzzlePosition, shootDirection * maxDistance, Color.red, 1.0f);
        }
        
        if (Physics.Raycast(muzzlePosition, shootDirection, out RaycastHit hit, maxDistance, hitLayers))
        {
            EnemyController enemyController = hit.transform.GetComponent<EnemyController>();
            if (!enemyController)
            {
                enemyController = hit.transform.GetComponentInParent<EnemyController>();
            }
            if (enemyController != null)
            {
                enemyController.TakeDamage(damage);
                SpawnImpactEffect(hit, true);
                Debug.Log($"Hit enemy {enemyController.name} for {damage} damage at {hit.point}");
            }
            else
            {
                SpawnImpactEffect(hit, false);
                Debug.Log($"Hit environment ({hit.transform.name}) at {hit.point}");
            }
        }
    }
    
    public void PerformHitDetectionWithSpread(Vector3 muzzlePosition, float damage, float spreadAngle)
    {
        if (!_mainCamera) return;
        
        // Base direction is forward from camera
        Vector3 baseDirection = _mainCamera.transform.forward;
        
        // Add random spread
        float randomSpreadX = Random.Range(-spreadAngle, spreadAngle);
        float randomSpreadY = Random.Range(-spreadAngle, spreadAngle);
        
        Quaternion spreadRotation = Quaternion.Euler(randomSpreadY, randomSpreadX, 0);
        Vector3 shootDirection = spreadRotation * baseDirection;
        
        // Draw debug ray
        if (showDebugRays)
        {
            Debug.DrawRay(muzzlePosition, shootDirection * maxDistance, Color.yellow, 1.0f);
        }
        
        if (Physics.Raycast(muzzlePosition, shootDirection, out RaycastHit hit, maxDistance, hitLayers))
        {
            // Process hit same as normal hit detection
            EnemyController enemyController = hit.transform.GetComponent<EnemyController>();
            if (!enemyController)
            {
                enemyController = hit.transform.GetComponentInParent<EnemyController>();
            }
            
            if (enemyController != null)
            {
                enemyController.TakeDamage(damage);
                SpawnImpactEffect(hit, true);
            }
            else
            {
                SpawnImpactEffect(hit, false);
            }
        }
    }
    
    private void SpawnImpactEffect(RaycastHit hit, bool isEnemyHit)
    {
        Quaternion impactRotation = Quaternion.LookRotation(hit.normal);
        GameObject effectPrefab = isEnemyHit ? enemyHitEffect : environmentHitEffect;
        if (effectPrefab != null)
        {
            GameObject impactEffect = Instantiate(effectPrefab, hit.point, impactRotation);
            impactEffect.transform.position += hit.normal * 0.01f;
            Destroy(impactEffect, impactEffectDuration);
        }
    }
}
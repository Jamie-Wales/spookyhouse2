using UnityEngine;

public class WeaponHitDetection : MonoBehaviour
{
    [Header("Hit Detection")]
    public LayerMask hitLayers;
    public float maxDistance = 100f;
    
    private Camera _mainCamera;
    
    private void Start()
    {
        _mainCamera = Camera.main;
    }
    
    public void PerformHitDetection(Vector3 muzzlePosition, float damage)
    {
        if (!_mainCamera) return;
        
        Vector3 shootDirection = _mainCamera.transform.forward;
        
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
            }
            else
            {
                SpawnImpactEffect(hit, false);
            }
        }
    }
    
    private void SpawnImpactEffect(RaycastHit hit, bool isEnemyHit)
    {
        GameObject impactEffect = null;
        
        if (isEnemyHit)
        {
            Debug.Log("Hit enemy at " + hit.point);
        }
        else
        {
            Debug.Log("Hit environment at " + hit.point);
        }
        if (impactEffect != null)
        {
            Destroy(impactEffect, 2.0f);
        }
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public EnemyData enemyData;
    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _audioSource;
    private Transform _player;
    private GameObject _model;
    private float _currentHealth;
    private float _lastAttackTime;
    private bool _isDead = false;
    private EnemyState _currentState = EnemyState.Idle;
    
    private enum EnemyState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Hurt,
        Dead
    }
    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _audioSource = GetComponent<AudioSource>();
        
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (!enemyData) return;
        _currentHealth = enemyData.maxHealth;
        _agent.speed = enemyData.moveSpeed;
        _agent.stoppingDistance = enemyData.attackRange * 0.8f;
    }
    
    private void Start()
    {
        if (enemyData && enemyData.enemyPrefab && !_model)
        {
            _model = Instantiate(enemyData.enemyPrefab, transform);
            _animator = _model.GetComponent<Animator>() ?? _model.GetComponentInChildren<Animator>();
        }
        
        if (!_audioSource)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f; 
        }
        
        StartCoroutine(StateMachine());
    }
    
    // ReSharper disable Unity.PerformanceAnalysis
    private IEnumerator StateMachine()
    {
        while (!_isDead)
        {
            yield return StartCoroutine(_currentState switch
            {
                EnemyState.Idle => IdleState(),
                EnemyState.Patrol => PatrolState(),
                EnemyState.Chase => ChaseState(),
                EnemyState.Attack => AttackState(),
                EnemyState.Hurt => HurtState(),
                EnemyState.Dead => DeadState(),
                _ => IdleState()
            });
            
            yield return null;
        }
    }
    
    // State implementations
    private IEnumerator IdleState()
    {
        if (_animator && !string.IsNullOrEmpty(enemyData.idleAnimTrigger))
        {
            _animator.SetTrigger(enemyData.idleAnimTrigger);
        }
        
        _agent.isStopped = true;
        
        float idleTime = Random.Range(2f, 5f);
        float timer = 0;
        
        while (timer < idleTime)
        {
            timer += Time.deltaTime;
            
            if (_player && Vector3.Distance(transform.position, _player.position) < enemyData.detectionRadius)
            {
                _currentState = EnemyState.Chase;
                yield break;
            }
            
            yield return null;
        }
        
        if (enemyData.canPatrol)
        {
            _currentState = EnemyState.Patrol;
        }
    }
    
    private IEnumerator PatrolState()
    {
        if (_animator && !string.IsNullOrEmpty(enemyData.walkAnimTrigger))
        {
            _animator.SetTrigger(enemyData.walkAnimTrigger);
        }
        
        _agent.isStopped = false;
        _agent.speed = enemyData.moveSpeed;
        Vector3 randomPoint = GetRandomPointInNavMesh(transform.position, 10f);
        _agent.SetDestination(randomPoint);
        while (!_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance)
        {
            if (_player != null && Vector3.Distance(transform.position, _player.position) < enemyData.detectionRadius)
            {
                _currentState = EnemyState.Chase;
                yield break;
            }
            
            yield return null;
        }
        
        _currentState = EnemyState.Idle;
    }
    
    private IEnumerator ChaseState()
    {
        if (_animator != null && !string.IsNullOrEmpty(enemyData.runAnimTrigger))
        {
            _animator.SetTrigger(enemyData.runAnimTrigger);
        }
        
        _agent.isStopped = false;
        _agent.speed = enemyData.chaseSpeed;
        
        while (_player != null)
        {
            _agent.SetDestination(_player.position);
            
            // Check if within attack range
            if (Vector3.Distance(transform.position, _player.position) <= enemyData.attackRange)
            {
                _currentState = EnemyState.Attack;
                yield break;
            }
            
            // Check if player is out of detection range
            if (Vector3.Distance(transform.position, _player.position) > enemyData.detectionRadius * 1.5f)
            {
                _currentState = EnemyState.Idle;
                yield break;
            }
            
            yield return null;
        }
        _currentState = EnemyState.Idle;
    }
    
    private IEnumerator AttackState()
    {
        _agent.isStopped = true;
        if (_player != null)
        {
            Vector3 direction = (_player.position - transform.position).normalized;
            direction.y = 0;
            transform.rotation = Quaternion.LookRotation(direction);
        }
        if (Time.time - _lastAttackTime < enemyData.attackCooldown)
        {
            _currentState = EnemyState.Chase;
            yield break;
        }
        if (_animator != null && !string.IsNullOrEmpty(enemyData.attackAnimTrigger))
        {
            _animator.SetTrigger(enemyData.attackAnimTrigger);
        }
        _lastAttackTime = Time.time;
        if (enemyData.attackSounds != null && enemyData.attackSounds.Length > 0 && _audioSource)
        {
            AudioClip clip = enemyData.attackSounds[Random.Range(0, enemyData.attackSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }
        yield return new WaitForSeconds(1.0f);
        if (_player != null && Vector3.Distance(transform.position, _player.position) <= enemyData.attackRange)
        {
            PlayerHealth playerHealth = _player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(enemyData.attackDamage);
            }
            if (enemyData.hitEffectPrefab)
            {
                Instantiate(enemyData.hitEffectPrefab, _player.position + Vector3.up, Quaternion.identity);
            }
        }
        _currentState = EnemyState.Chase;
    }
    
    private IEnumerator HurtState()
    {
        _agent.isStopped = true;
        
        if (_animator && !string.IsNullOrEmpty(enemyData.hurtAnimTrigger))
        {
            _animator.SetTrigger(enemyData.hurtAnimTrigger);
        }
        
        if (enemyData.hurtSounds.Length > 0 && _audioSource)
        {
            AudioClip clip = enemyData.hurtSounds[Random.Range(0, enemyData.hurtSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }
        float stunDuration = 0.5f;
        yield return new WaitForSeconds(stunDuration);
        _currentState = EnemyState.Chase;
    }
    
    private IEnumerator DeadState()
    {
        _isDead = true;
        _agent.isStopped = true;
        
        if (_animator != null && !string.IsNullOrEmpty(enemyData.deathAnimTrigger))
        {
            _animator.SetTrigger(enemyData.deathAnimTrigger);
        }
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
        if (enemyData.deathSound && _audioSource)
        {
            _audioSource.PlayOneShot(enemyData.deathSound);
        }
        if (enemyData.deathEffectPrefab)
        {
            Instantiate(enemyData.deathEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
        }
        
        yield return new WaitForSeconds(3.0f);
        Destroy(gameObject);
    }
    
    private Vector3 GetRandomPointInNavMesh(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;
        NavMeshHit hit;
        
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        return center;
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            _currentState = EnemyState.Dead;
        }
        else
        {
            _currentState = EnemyState.Hurt;
        }
    }
    
    public void OnDamageTaken()
    {
        Debug.Log("Some Damage Yo");
    }
    
    public void OnDeath()
    {
        Debug.Log("I'm Dead Yo");
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!enemyData) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyData.detectionRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);
    }
}
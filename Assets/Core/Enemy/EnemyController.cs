using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public EnemyData enemyData;

    [Header("References")] public Transform playerTransform;

    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _audioSource;
    private GameObject _model;
    private float _currentHealth;
    private float _lastAttackTime;
    private bool _isDead;
    private EnemyState _currentState = EnemyState.Idle;

    [Header("Debug")] public bool showDebugLogs = true;

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

        if (enemyData)
        {
            _currentHealth = enemyData.maxHealth;
            _agent.speed = enemyData.moveSpeed;
            _agent.stoppingDistance = enemyData.attackRange * 0.5f;
        }
    }

    private void Start()
    {
        if (!playerTransform)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player)
            {
                playerTransform = player.transform;
                LogDebug($"Found player at {playerTransform.position}");
            }
            else
            {
                Debug.LogWarning("No player found! Please assign the player reference in the inspector.");
            }
        }

        if (enemyData)
        {
            _currentHealth = enemyData.maxHealth;
            LogDebug($"Initialized health: {_currentHealth}/{enemyData.maxHealth}");
        }

        if (!GetComponent<Collider>())
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 1, 0);
            capsule.height = 2.0f;
            capsule.radius = 0.5f;
        }

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

        if (LayerMask.NameToLayer("Enemy") != -1)
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }

        LogDebug(
            $"Enemy initialized - Attack range: {enemyData.attackRange}, Stopping distance: {_agent.stoppingDistance}");

        StartCoroutine(StateMachine());
    }

    private void Update()
    {
        if (playerTransform && showDebugLogs)
        {
            Debug.DrawLine(transform.position, playerTransform.position, Color.red);

            float distance = GetDistanceToPlayer();
            if (distance <= enemyData.attackRange)
            {
                Debug.DrawRay(transform.position, Vector3.up * 3f, Color.green);

                if (_currentState == EnemyState.Chase && Time.time - _lastAttackTime >= enemyData.attackCooldown)
                {
                    LogDebug($"In range ({distance:F2} <= {enemyData.attackRange:F2}), attacking");
                    _currentState = EnemyState.Attack;
                }
            }
        }
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[Enemy:{gameObject.name}] {message}");
        }
    }

    private Vector3 GetPlayerPosition()
    {
        if (!playerTransform) return Vector3.zero;

        Vector3 targetPos = playerTransform.position;
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc) targetPos += cc.center;

        return targetPos;
    }

    private float GetDistanceToPlayer()
    {
        if (!playerTransform) return Mathf.Infinity;
        return Vector3.Distance(transform.position, GetPlayerPosition());
    }

    private IEnumerator StateMachine()
    {
        while (!_isDead)
        {
            EnemyState previousState = _currentState;
            IEnumerator stateCoroutine = null;

            switch (_currentState)
            {
                case EnemyState.Idle:
                    stateCoroutine = IdleState();
                    break;
                case EnemyState.Patrol:
                    stateCoroutine = PatrolState();
                    break;
                case EnemyState.Chase:
                    stateCoroutine = ChaseState();
                    break;
                case EnemyState.Attack:
                    stateCoroutine = AttackState();
                    break;
                case EnemyState.Hurt:
                    stateCoroutine = HurtState();
                    break;
                case EnemyState.Dead:
                    stateCoroutine = DeadState();
                    break;
            }

            if (previousState != _currentState)
            {
                LogDebug($"State change: {previousState} → {_currentState}");
            }

            if (stateCoroutine != null)
            {
                yield return StartCoroutine(stateCoroutine);
            }
            else
            {
                LogDebug("No valid state found, defaulting to Idle");
                _currentState = EnemyState.Idle;
            }

            yield return null;
        }
    }

    private IEnumerator IdleState()
    {
        LogDebug("Entering IDLE state");

        if (_animator && !string.IsNullOrEmpty(enemyData.idleAnimTrigger))
        {
            _animator.SetTrigger(enemyData.idleAnimTrigger);
        }

        _agent.isStopped = true;
        float idleTime = Random.Range(2f, 5f);

        for (float timer = 0; timer < idleTime; timer += Time.deltaTime)
        {
            if (playerTransform)
            {
                float distanceToPlayer = GetDistanceToPlayer();
                if (distanceToPlayer < enemyData.detectionRadius)
                {
                    LogDebug($"Player detected at distance {distanceToPlayer:F2}");
                    _currentState = EnemyState.Chase;
                    yield break;
                }
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
        LogDebug("Entering PATROL state");

        if (_animator && !string.IsNullOrEmpty(enemyData.walkAnimTrigger))
        {
            _animator.SetTrigger(enemyData.walkAnimTrigger);
        }

        _agent.isStopped = false;
        _agent.speed = enemyData.moveSpeed;

        Vector3 randomPoint = GetRandomPointInNavMesh(transform.position, 10f);
        _agent.SetDestination(randomPoint);
        LogDebug($"Patrolling to point {randomPoint}");

        while (!_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance)
        {
            if (playerTransform)
            {
                float distanceToPlayer = GetDistanceToPlayer();
                if (distanceToPlayer < enemyData.detectionRadius)
                {
                    LogDebug($"Player spotted while patrolling at distance {distanceToPlayer:F2}");
                    _currentState = EnemyState.Chase;
                    yield break;
                }
            }

            yield return null;
        }

        _currentState = EnemyState.Idle;
    }

    private IEnumerator ChaseState()
    {
        LogDebug("Entering CHASE state");

        if (_animator && !string.IsNullOrEmpty(enemyData.runAnimTrigger))
        {
            _animator.SetTrigger(enemyData.runAnimTrigger);
        }

        _agent.isStopped = false;
        _agent.speed = enemyData.chaseSpeed;

        int frameCount = 0;

        if (!playerTransform)
        {
            LogDebug("No player to chase");
            _currentState = EnemyState.Idle;
            yield break;
        }

        // Check if we're on a NavMesh
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
        {
            LogDebug("ERROR: Enemy not on NavMesh! Cannot navigate.");
            // Try to find valid NavMesh position
            if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                LogDebug($"Found valid NavMesh position at {hit.position}, moving there");
                transform.position = hit.position;
            }
            else
            {
                LogDebug("Failed to find valid NavMesh position nearby");
                _currentState = EnemyState.Idle;
                yield break;
            }
        }

        LogDebug($"Chasing player at {GetPlayerPosition()}");
        Vector3 lastPosition = transform.position;
        float stuckTimer = 0;
        Vector3 playerPos = GetPlayerPosition();

        // Check if path to player exists
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, playerPos, NavMesh.AllAreas, path))
        {
            LogDebug("No valid path to player!");
        }

        LogDebug($"Path status: {path.status}, corners: {path.corners.Length}");

        while (playerTransform)
        {
            frameCount++;
            float distanceToPlayer = GetDistanceToPlayer();
            playerPos = GetPlayerPosition();

            // Attempt to set destination
            bool success = _agent.SetDestination(playerPos);

            // Check if we're stuck
            if (frameCount % 30 == 0)
            {
                float movedDistance = Vector3.Distance(lastPosition, transform.position);
                lastPosition = transform.position;

                if (movedDistance < 0.05f)
                {
                    stuckTimer += Time.deltaTime * 30;
                    if (stuckTimer > 2f)
                    {
                        LogDebug($"Agent appears stuck (moved {movedDistance:F2} in last 30 frames)");

                        // Check path status
                        if (!NavMesh.CalculatePath(transform.position, playerPos, NavMesh.AllAreas, path))
                        {
                            LogDebug("Path calculation failed");
                        }

                        LogDebug($"Path status: {path.status}, path length: {path.corners.Length}");

                        // Try to unstick by moving directly
                        NavMeshHit navHit;
                        if (NavMesh.SamplePosition(
                                transform.position + (playerPos - transform.position).normalized * 2f,
                                out navHit, 5f, NavMesh.AllAreas))
                        {
                            LogDebug($"Moving to intermediate point: {navHit.position}");
                            _agent.Warp(navHit.position);
                        }

                        stuckTimer = 0;
                    }
                }
                else
                {
                    stuckTimer = 0;
                }

                LogDebug($"Chasing: distance: {distanceToPlayer:F2}, moved: {movedDistance:F2}, path valid: {success}");
            }

            if (distanceToPlayer <= enemyData.attackRange)
            {
                LogDebug($"Within attack range ({distanceToPlayer:F2} <= {enemyData.attackRange:F2})");
                _currentState = EnemyState.Attack;
                yield break;
            }

            yield return null;
        }

        LogDebug("Lost player reference");
        _currentState = EnemyState.Idle;
    }

    private IEnumerator AttackState()
    {
        LogDebug("ATTACKING PLAYER");

        _agent.isStopped = true;

        if (!playerTransform)
        {
            _currentState = EnemyState.Idle;
            yield break;
        }

        Vector3 direction = (GetPlayerPosition() - transform.position).normalized;
        direction.y = 0;
        transform.rotation = Quaternion.LookRotation(direction);

        if (Time.time - _lastAttackTime < enemyData.attackCooldown)
        {
            LogDebug($"Attack on cooldown for {enemyData.attackCooldown - (Time.time - _lastAttackTime):F2} seconds");
            _currentState = EnemyState.Chase;
            yield break;
        }

        _lastAttackTime = Time.time;

        if (_animator && !string.IsNullOrEmpty(enemyData.attackAnimTrigger))
        {
            LogDebug($"Attack animation: {enemyData.attackAnimTrigger}");
            _animator.SetTrigger(enemyData.attackAnimTrigger);
        }

        if (enemyData.attackSounds != null && enemyData.attackSounds.Length > 0 && _audioSource)
        {
            AudioClip clip = enemyData.attackSounds[Random.Range(0, enemyData.attackSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }

        yield return new WaitForSeconds(0.3f);

        if (playerTransform && GetDistanceToPlayer() <= enemyData.attackRange * 1.2f)
        {
            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth)
            {
                LogDebug($"Dealing {enemyData.attackDamage} damage to player");
                playerHealth.TakeDamage(enemyData.attackDamage);

                if (enemyData.hitEffectPrefab)
                {
                    Instantiate(enemyData.hitEffectPrefab, playerTransform.position + Vector3.up, Quaternion.identity);
                }
            }
            else
            {
                LogDebug("Player has no PlayerHealth component");
                playerTransform.SendMessage("TakeDamage", enemyData.attackDamage,
                    SendMessageOptions.DontRequireReceiver);
            }
        }
        else
        {
            LogDebug("Player moved out of range");
        }

        yield return new WaitForSeconds(0.7f);

        LogDebug("Attack complete - returning to Chase");
        _currentState = EnemyState.Chase;
    }

    private IEnumerator HurtState()
    {
        LogDebug("Entering HURT state");

        _agent.isStopped = true;

        if (_animator && !string.IsNullOrEmpty(enemyData.hurtAnimTrigger))
        {
            _animator.SetTrigger(enemyData.hurtAnimTrigger);
        }

        if (enemyData.hurtSounds != null && enemyData.hurtSounds.Length > 0 && _audioSource)
        {
            AudioClip clip = enemyData.hurtSounds[Random.Range(0, enemyData.hurtSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }

        yield return new WaitForSeconds(0.5f);
        _currentState = EnemyState.Chase;
    }

    private IEnumerator DeadState()
    {
        LogDebug("Enemy DIED");

        _isDead = true;
        _agent.isStopped = true;

        if (_animator && !string.IsNullOrEmpty(enemyData.deathAnimTrigger))
        {
            _animator.SetTrigger(enemyData.deathAnimTrigger);
        }

        foreach (var col in GetComponents<Collider>())
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
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            randomDirection += center;
            NavMeshHit hit;

            if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return center;
    }

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHealth -= damage;
        LogDebug($"Took {damage} damage! Health: {_currentHealth}/{enemyData.maxHealth}");

        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            _isDead = true;
            _currentState = EnemyState.Dead;
            StopAllCoroutines();
            StartCoroutine(DeadState());
            return;
        }

        bool wasChasing = (_currentState == EnemyState.Chase || _currentState == EnemyState.Attack);
        _currentState = EnemyState.Hurt;

        if (!wasChasing && playerTransform)
        {
            LogDebug("Enemy alerted by damage!");
            StartCoroutine(AlertAfterHurt());
        }
        else
        {
            StartCoroutine(ContinueChaseAfterHurt());
        }
    }

    private IEnumerator ContinueChaseAfterHurt()
    {
        yield return new WaitForSeconds(0.5f);

        if (!_isDead && _currentState != EnemyState.Dead)
        {
            LogDebug("Continuing chase after being hurt");
            _currentState = EnemyState.Chase;
        }
    }

    private IEnumerator AlertAfterHurt()
    {
        yield return new WaitForSeconds(0.6f);

        if (!_isDead && _currentState != EnemyState.Dead)
        {
            LogDebug("Enemy alerted and now chasing player!");
            _currentState = EnemyState.Chase;
            yield return StartCoroutine(ChaseState());
        }
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
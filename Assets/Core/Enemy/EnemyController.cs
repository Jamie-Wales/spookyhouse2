using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public EnemyData enemyData;

    [Header("References")] 
    public Transform playerTransform; // Can be left empty - will find automatically

    [Header("Player Finding")]
    [Tooltip("Name of the player GameObject to find if not assigned manually")]
    public string playerObjectName = "Player";

    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _audioSource;
    private float _currentHealth;
    private float _lastAttackTime;
    private bool _isDead;
    private bool _isBeingDestroyed; // Flag to track when the enemy is about to be destroyed
    private EnemyState _currentState = EnemyState.Idle;
    private Coroutine _stateMachineCoroutine;

    // Animation boolean parameters
    private static readonly string IS_WALKING = "isWalking";
    private static readonly string IS_RUNNING = "isRunning";

    [Header("Debug")] public bool showDebugLogs = true;
    private Camera _mainCamera;

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
        // Get required components
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _audioSource = GetComponent<AudioSource>();

        // If any required component is missing, try to find it or add it
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
            
            if (_animator == null)
            {
                Debug.LogWarning($"Enemy {gameObject.name} has no Animator component. Animations won't work.");
            }
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f;
            LogDebug("Added missing AudioSource component");
        }

        // Initialize from enemy data
        if (enemyData)
        {
            _currentHealth = enemyData.maxHealth;
            _agent.speed = enemyData.moveSpeed;
            _agent.stoppingDistance = enemyData.attackRange * 0.5f;
        }
        else
        {
            Debug.LogError($"Enemy {gameObject.name} has no EnemyData assigned!");
        }
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        
        // Find player if not assigned
        if (!playerTransform)
        {
            // First try to find by name
            GameObject playerObj = GameObject.Find(playerObjectName);
            if (playerObj)
            {
                playerTransform = playerObj.transform;
                LogDebug($"Found player by name '{playerObjectName}' at {playerTransform.position}");
            }
            else
            {
                // Fallback to tag
                playerObj = GameObject.FindWithTag("Player");
                if (playerObj)
                {
                    playerTransform = playerObj.transform;
                    LogDebug($"Found player by 'Player' tag at {playerTransform.position}");
                }
                else
                {
                    Debug.LogWarning($"No player found with name '{playerObjectName}' or tag 'Player'!");
                }
            }
        }

        // Initialize health
        if (enemyData)
        {
            _currentHealth = enemyData.maxHealth;
            LogDebug($"Initialized health: {_currentHealth}/{enemyData.maxHealth}");
        }

        // Add collider if missing
        if (!GetComponent<Collider>())
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 1, 0);
            capsule.height = 2.0f;
            capsule.radius = 0.5f;
            LogDebug("Added default capsule collider");
        }

        // Set layer
        if (LayerMask.NameToLayer("Enemy") != -1)
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
        }

        LogDebug($"Enemy initialized - Attack range: {enemyData.attackRange}, Stopping distance: {_agent.stoppingDistance}");

        // Start the state machine and store reference to coroutine
        _stateMachineCoroutine = StartCoroutine(StateMachine());
    }

    private void OnDisable()
    {
        // Make sure all coroutines are stopped when disabled
        StopAllCoroutines();
        _isDead = true;
        _isBeingDestroyed = true;
    }

    private void OnDestroy()
    {
        // Ensure we're stopping our behavior when destroyed
        _isDead = true;
        _isBeingDestroyed = true;
    }

    private void Update()
    {
        if (_isDead || _isBeingDestroyed) return;

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

    private void UpdateAnimationState(bool isWalking, bool isRunning)
    {
        if (_animator && !_isBeingDestroyed)
        {
            try
            {
                _animator.SetBool(IS_WALKING, isWalking);
                _animator.SetBool(IS_RUNNING, isRunning);
            }
            catch (System.Exception e)
            {
                // Silently handle the exception
                if (showDebugLogs)
                {
                    Debug.LogWarning($"Animation error: {e.Message}");
                }
            }
        }
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs && !_isBeingDestroyed)
        {
            Debug.Log($"[Enemy:{gameObject.name}] {message}");
        }
    }

    private Vector3 GetPlayerPosition()
    {
        if (!playerTransform || _isBeingDestroyed) return Vector3.zero;

        Vector3 targetPos = playerTransform.position;
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc) targetPos += cc.center;

        return targetPos;
    }

    private float GetDistanceToPlayer()
    {
        if (!playerTransform || _isBeingDestroyed) return Mathf.Infinity;
        return Vector3.Distance(transform.position, GetPlayerPosition());
    }

    private IEnumerator StateMachine()
    {
        while (!_isDead && !_isBeingDestroyed)
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

            if (stateCoroutine != null && !_isBeingDestroyed)
            {
                yield return StartCoroutine(stateCoroutine);
            }
            else
            {
                if (!_isBeingDestroyed)
                {
                    LogDebug("No valid state found, defaulting to Idle");
                    _currentState = EnemyState.Idle;
                }
            }

            if (_isBeingDestroyed) yield break;
            yield return null;
        }
    }

    private IEnumerator IdleState()
    {
        if (_isBeingDestroyed) yield break;
        
        LogDebug("Entering IDLE state");

        UpdateAnimationState(false, false);
        if (_agent) _agent.isStopped = true;
        float idleTime = Random.Range(2f, 5f);

        for (float timer = 0; timer < idleTime; timer += Time.deltaTime)
        {
            if (_isBeingDestroyed) yield break;
            
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

        if (enemyData.canPatrol && !_isBeingDestroyed)
        {
            _currentState = EnemyState.Patrol;
        }
    }

    private IEnumerator PatrolState()
    {
        if (_isBeingDestroyed) yield break;
        
        LogDebug("Entering PATROL state");

        UpdateAnimationState(true, false);
        if (_agent)
        {
            _agent.isStopped = false;
            _agent.speed = enemyData.moveSpeed;

            Vector3 randomPoint = GetRandomPointInNavMesh(transform.position, 10f);
            _agent.SetDestination(randomPoint);
            LogDebug($"Patrolling to point {randomPoint}");

            while (!_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance)
            {
                if (_isBeingDestroyed) yield break;
                
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
        }

        if (!_isBeingDestroyed)
        {
            _currentState = EnemyState.Idle;
        }
    }

    private IEnumerator ChaseState()
    {
        if (_isBeingDestroyed) yield break;
        
        LogDebug("Entering CHASE state");

        UpdateAnimationState(true, true);
        if (!_agent || !playerTransform)
        {
            _currentState = EnemyState.Idle;
            yield break;
        }
        
        _agent.isStopped = false;
        _agent.speed = enemyData.chaseSpeed;

        int frameCount = 0;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
        {
            LogDebug("ERROR: Enemy not on NavMesh! Cannot navigate.");
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

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, playerPos, NavMesh.AllAreas, path))
        {
            LogDebug("No valid path to player!");
        }

        LogDebug($"Path status: {path.status}, corners: {path.corners.Length}");

        while (playerTransform && !_isBeingDestroyed)
        {
            frameCount++;
            float distanceToPlayer = GetDistanceToPlayer();
            playerPos = GetPlayerPosition();

            if (_agent)
            {
                bool success = _agent.SetDestination(playerPos);

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

                            if (!NavMesh.CalculatePath(transform.position, playerPos, NavMesh.AllAreas, path))
                            {
                                LogDebug("Path calculation failed");
                            }

                            LogDebug($"Path status: {path.status}, path length: {path.corners.Length}");

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
            }

            if (distanceToPlayer <= enemyData.attackRange && !_isBeingDestroyed)
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
        if (_isBeingDestroyed) yield break;
        
        LogDebug("ATTACKING PLAYER");

        UpdateAnimationState(false, false);
        if (_agent) _agent.isStopped = true;

        if (!playerTransform || _isBeingDestroyed)
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

        if (_animator && !_isBeingDestroyed && !string.IsNullOrEmpty(enemyData.attackAnimTrigger))
        {
            try
            {
                LogDebug($"Attack animation: {enemyData.attackAnimTrigger}");
                _animator.SetTrigger(enemyData.attackAnimTrigger);
            }
            catch (System.Exception e)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"Animation error: {e.Message}");
                }
            }
        }

        if (enemyData.attackSounds != null && enemyData.attackSounds.Length > 0 && _audioSource && !_isBeingDestroyed)
        {
            AudioClip clip = enemyData.attackSounds[Random.Range(0, enemyData.attackSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }

        yield return new WaitForSeconds(0.3f);

        if (_isBeingDestroyed) yield break;

        if (playerTransform && GetDistanceToPlayer() <= enemyData.attackRange * 1.2f)
        {
            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth)
            {
                LogDebug($"Dealing {enemyData.attackDamage} damage to player");
                playerHealth.TakeDamage(enemyData.attackDamage);

                if (enemyData.hitEffectPrefab && !_isBeingDestroyed)
                {
                    SpawnTemporaryEffect(enemyData.hitEffectPrefab, playerTransform.position + Vector3.up, 1.0f);
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

        if (!_isBeingDestroyed)
        {
            LogDebug("Attack complete - returning to Chase");
            _currentState = EnemyState.Chase;
        }
    }

    private IEnumerator HurtState()
    {
        if (_isBeingDestroyed) yield break;
        
        LogDebug("Entering HURT state");

        UpdateAnimationState(false, false);
        if (_agent) _agent.isStopped = true;

        if (_animator && !_isBeingDestroyed && !string.IsNullOrEmpty(enemyData.hurtAnimTrigger))
        {
            try
            {
                _animator.SetTrigger(enemyData.hurtAnimTrigger);
            }
            catch (System.Exception e)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"Animation error: {e.Message}");
                }
            }
        }

        if (enemyData.hurtSounds != null && enemyData.hurtSounds.Length > 0 && _audioSource && !_isBeingDestroyed)
        {
            AudioClip clip = enemyData.hurtSounds[Random.Range(0, enemyData.hurtSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }

        yield return new WaitForSeconds(0.5f);

        if (!_isBeingDestroyed)
        {
            _currentState = EnemyState.Chase;
        }
    }

    private IEnumerator DeadState()
    {
        LogDebug("Enemy DIED");

        _isDead = true;
        if (_agent) _agent.isStopped = true;
        UpdateAnimationState(false, false);

        if (_animator && !string.IsNullOrEmpty(enemyData.deathAnimTrigger))
        {
            try
            {
                _animator.SetTrigger(enemyData.deathAnimTrigger);
            }
            catch (System.Exception e)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"Animation error on death: {e.Message}");
                }
            }
        }

        foreach (var col in GetComponents<Collider>())
        {
            col.enabled = false;
        }

        // Stop the NavMeshAgent
        if (_agent)
        {
            _agent.enabled = false;
        }

        // Disable any Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        SpawnTemporaryEffect(enemyData.deathEffectPrefab, transform.position + Vector3.up, 0.5f);
        if (enemyData.deathSound && _audioSource)
        {
            _audioSource.PlayOneShot(enemyData.deathSound);
        }

        // Set the flag before the destroy
        _isBeingDestroyed = true;
        
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

    public void TakeDamage(float damage, Vector3? hitPoint = null)
    {
        if (_isDead || _isBeingDestroyed) return;
        
        if (enemyData.hitEffectPrefab)
        {
            Vector3 effectPosition = hitPoint ?? (transform.position + (Vector3.up * 1.5f));
            SpawnTemporaryEffect(enemyData.hitEffectPrefab, effectPosition, 0.5f);
        }

        // Knock enemy back constant amount must be more than its move forward
        if (hitPoint != null && _agent)
        {
            Vector3 direction = (transform.position - hitPoint.Value).normalized;
            direction.y = 0;
            _agent.velocity = direction * 2f;
        }

        if (enemyData.hurtSounds != null && enemyData.hurtSounds.Length > 0 && _audioSource)
        {
            AudioClip clip = enemyData.hurtSounds[Random.Range(0, enemyData.hurtSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }

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

        if (!_isDead && _currentState != EnemyState.Dead && !_isBeingDestroyed)
        {
            LogDebug("Continuing chase after being hurt");
            _currentState = EnemyState.Chase;
        }
    }

    private IEnumerator AlertAfterHurt()
    {
        yield return new WaitForSeconds(0.6f);

        if (!_isDead && _currentState != EnemyState.Dead && !_isBeingDestroyed)
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

    private void SpawnTemporaryEffect(GameObject effectPrefab, Vector3 position, float duration = 0.5f)
    {
        if (effectPrefab && !_isBeingDestroyed)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps)
            {
                ps.Play();
            }

            LogDebug($"Spawned effect {effectPrefab.name} at {position}, duration: {duration}");
            Destroy(effect, duration);
        }
    }
}
using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Basic Enemy Info")] public string enemyName = "Enemy";

    public string description;
    public GameObject enemyPrefab;

    [Header("Stats")] public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float moveSpeed = 5f;
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;

    [Header("AI Settings")] public float detectionRadius = 10f;
    public float chaseSpeed = 5f;
    public float stunResistance = 0f;
    public bool canPatrol = true;
    public bool canUseRangedAttacks = false;

    [Header("Combat")] public AudioClip[] attackSounds;
    public AudioClip[] hurtSounds;
    public AudioClip deathSound;
    public GameObject hitEffectPrefab;
    public GameObject deathEffectPrefab;

    [Header("Animation Parameters")] public string idleAnimTrigger = "Idle";
    public string walkAnimTrigger = "Walk";
    public string runAnimTrigger = "Run";
    public string attackAnimTrigger = "Attack";
    public string hurtAnimTrigger = "Hurt";
    public string deathAnimTrigger = "Death";
}
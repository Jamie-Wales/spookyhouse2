using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")] 
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI Elements")] 
    public Slider healthSlider; // Slider with range 0-100
    public Image damageOverlay;

    private bool _isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
        if (healthSlider)
        {
            // No need to set maxValue if it's already configured in the Inspector
            healthSlider.value = currentHealth; // This assumes maxHealth is also 100
        }
        
        if (damageOverlay)
        {
            Color overlayColor = damageOverlay.color;
            overlayColor.a = 0;
            damageOverlay.color = overlayColor;
        }
    }

    private void Update()
    {
        if (!damageOverlay || !(damageOverlay.color.a > 0)) return;
        var overlayColor = damageOverlay.color;
        overlayColor.a = Mathf.Lerp(overlayColor.a, 0, Time.deltaTime * 2f);
        damageOverlay.color = overlayColor;
    }

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        currentHealth -= damage;

        if (damageOverlay)
        {
            var overlayColor = damageOverlay.color;
            overlayColor.a = Mathf.Min(overlayColor.a + 0.3f, 0.6f);
            damageOverlay.color = overlayColor;
        }

        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider)
        {
            healthSlider.value = (currentHealth / maxHealth) * 100f;
        }
        else
        {
           Debug.Log("Cannot find health UI"); 
        }
    }

    private void Die()
    {
        _isDead = true;
        if (damageOverlay)
        {
            Color overlayColor = damageOverlay.color;
            overlayColor.a = 0.8f;
            damageOverlay.color = overlayColor;
        }

        var playerController = GetComponent<PlayerController>();
        if (playerController)
        {
            playerController.enabled = false;
        }

        var weaponManager = GetComponent<WeaponManager>();
        if (weaponManager)
        {
            weaponManager.enabled = false;
        }
        var animator = GetComponent<Animator>();
        if (animator)
        {
            animator.SetTrigger("Death");
        }
        StartCoroutine(RespawnAfterDelay(3f));
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Respawn();
    }

    public void Respawn()
    {
        _isDead = false;
        currentHealth = maxHealth;
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController)
        {
            playerController.enabled = true;
        }

        WeaponManager weaponManager = GetComponent<WeaponManager>();
        if (weaponManager)
        {
            weaponManager.enabled = true;
        }

        UpdateHealthUI();
        if (damageOverlay)
        {
            Color overlayColor = damageOverlay.color;
            overlayColor.a = 0;
            damageOverlay.color = overlayColor;
        }
    }
}
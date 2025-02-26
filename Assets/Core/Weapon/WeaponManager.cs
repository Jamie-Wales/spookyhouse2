using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using UnityEngine.Pool;

public class WeaponManager : MonoBehaviour
{
    [Header("References")] public List<WeaponData> weapons;
    public Transform weaponSocket;

    private GameObject currentWeaponInstance;
    private WeaponData currentWeaponData;
    private AudioSource weaponAudioSource;
    private Animator animator;
    private Transform trailStartPoint;

    private ObjectPool<TrailRenderer> trailPool;
    private const int DefaultPoolSize = 20;
    private const int MaxPoolSize = 100;

    private bool isProcessingShot = false;
    private float lastFireTime;
    private bool isReloading = false;
    
    private void Awake()
    {
        trailPool = new ObjectPool<TrailRenderer>(
            createFunc: CreateTrailRenderer,
            actionOnGet: OnTrailTakenFromPool,
            actionOnRelease: OnTrailReturnedToPool,
            actionOnDestroy: OnTrailDestroyed,
            collectionCheck: true,
            defaultCapacity: DefaultPoolSize,
            maxSize: MaxPoolSize
        );
    }

    private void Start()
    {
        animator = GetComponentInParent<Animator>();

        if (animator && animator.isHuman)
        {
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand)
            {
                if (weaponSocket)
                {
                    weaponSocket.SetParent(rightHand, false);
                    trailStartPoint = weaponSocket.transform;
                    Debug.Log("Attached weapon socket to right hand");
                }
                else
                {
                    GameObject socketObj = new GameObject("WeaponSocket");
                    weaponSocket = socketObj.transform;
                    weaponSocket.SetParent(rightHand, false);
                    weaponSocket.localPosition = Vector3.zero;
                    weaponSocket.localRotation = Quaternion.identity;
                    Debug.Log("Created new weapon socket on right hand");
                }
            }
            else
            {
                Debug.LogError("Could not find right hand bone!");
                return;
            }
        }
        else
        {
            Debug.LogError("Animator is missing or not set to humanoid!");
            return;
        }

        if (weapons != null && weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    // Pool management functions
    private TrailRenderer CreateTrailRenderer()
    {
        if (!currentWeaponData || currentWeaponData.trailRendererPrefab == null)
        {
            var obj = new GameObject("DefaultTrail");
            var trail = obj.AddComponent<TrailRenderer>();
            trail.startWidth = 0.05f;
            trail.endWidth = 0.0f;
            trail.time = 0.5f;
            trail.emitting = false;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            return trail;
        }

        TrailRenderer instance = Instantiate(currentWeaponData.trailRendererPrefab);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void OnTrailTakenFromPool(TrailRenderer trail)
    {
        trail.Clear();
        trail.gameObject.SetActive(true);
        StartCoroutine(EnableTrailEmissionNextFrame(trail));
    }

    private IEnumerator EnableTrailEmissionNextFrame(TrailRenderer trail)
    {
        yield return null;
        if (trail.gameObject.activeInHierarchy)
        {
            trail.emitting = true;
        }
    }

    private void OnTrailReturnedToPool(TrailRenderer trail)
    {
        trail.emitting = false;
        trail.Clear();
        trail.gameObject.SetActive(false);
    }

    private void OnTrailDestroyed(TrailRenderer trail)
    {
        Destroy(trail.gameObject);
    }

    private void EquipWeapon(int weaponIndex)
    {
        if (currentWeaponInstance)
        {
            Destroy(currentWeaponInstance);
        }

        currentWeaponData = weapons[weaponIndex];
        if (currentWeaponData.weaponPrefab && weaponSocket)
        {
            currentWeaponInstance = Instantiate(currentWeaponData.weaponPrefab, weaponSocket);
            currentWeaponInstance.transform.localPosition = Vector3.zero;
            currentWeaponInstance.transform.localRotation = Quaternion.identity;
            weaponAudioSource = currentWeaponInstance.GetComponentInChildren<AudioSource>();
            Debug.Log($"Equipped {currentWeaponData.weaponName} at socket");
            
            // Update crosshair for the current weapon
            CrosshairController crosshair = FindObjectOfType<CrosshairController>();
            if (crosshair != null && currentWeaponData.crosshairSprite != null)
            {
                crosshair.SetCrosshairSprite(currentWeaponData.crosshairSprite);
            }
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private void _PlayMuzzleFlash()
    {
        if (!currentWeaponInstance) return;
        var muzzlePosition = currentWeaponInstance.transform.position +
                             currentWeaponInstance.transform.TransformDirection(
                                 currentWeaponData.muzzlePosition);

        var vfxInstance = Instantiate(currentWeaponData.muzzleFlash, muzzlePosition,
            currentWeaponInstance.transform.rotation);

        vfxInstance.transform.SetParent(currentWeaponInstance.transform, true);
        vfxInstance.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var visualEffect = vfxInstance.GetComponent<VisualEffect>();
        if (visualEffect)
        {
            visualEffect.Play();
        }

        Destroy(vfxInstance.gameObject, 0.15f);
    }

    public void Update()
    {
        // Don't check for firing if reloading
        if (isReloading)
            return;

        bool shouldFire = false;
        
        // Handle semi-automatic and first shot of automatic weapons
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            shouldFire = true;
        }
        // For automatic weapons, check for held button
        else if (currentWeaponData && currentWeaponData.isAutomatic && Mouse.current.leftButton.isPressed)
        {
            // Check if enough time has passed since the last shot based on fire rate
            float timeSinceLastFire = Time.time - lastFireTime;
            float fireInterval = 1f / currentWeaponData.fireRate;
            
            if (timeSinceLastFire >= fireInterval)
            {
                shouldFire = true;
            }
        }

        if (shouldFire && !isProcessingShot && currentWeaponData.currentAmmo > 0)
        {
            // For automatic weapons, we don't want to block future shots with isProcessingShot
            // We only need to ensure proper timing between shots
            if (!currentWeaponData.isAutomatic)
            {
                isProcessingShot = true;
            }
            
            Shoot();
            lastFireTime = Time.time;
            
            // For semi-automatic weapons, we'll reset isProcessingShot on next frame
            if (!currentWeaponData.isAutomatic)
            {
                StartCoroutine(ResetProcessingFlag());
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentWeaponData.currentAmmo < currentWeaponData.maxAmmo)
        {
            StartCoroutine(Reload());
        }
    }

    private IEnumerator ResetProcessingFlag()
    {
        yield return null;
        isProcessingShot = false;
    }

    private IEnumerator Reload()
    {
        if (isReloading)
            yield break;

        isReloading = true;
        if (weaponAudioSource && currentWeaponData.reloadSound)
        {
            weaponAudioSource.PlayOneShot(currentWeaponData.reloadSound);
        }

        yield return new WaitForSeconds(currentWeaponData.reloadTime);
        currentWeaponData.currentAmmo = currentWeaponData.maxAmmo;
        isReloading = false;
    }

    private void CreateBulletTrail(Vector3 startPosition, Vector3 direction, float distance, float duration)
    {
        TrailRenderer trail = trailPool.Get();
        trail.transform.position = startPosition;
        trail.transform.rotation = Quaternion.LookRotation(direction);
        StartCoroutine(MoveTrail(trail, startPosition, direction, distance, duration));
    }

    private IEnumerator MoveTrail(TrailRenderer trail, Vector3 startPosition, Vector3 direction, float distance,
        float duration)
    {
        var targetPosition = startPosition + (direction * distance);
        float elapsedTime = 0;
        float distanceCovered = 0;
        var lastPosition = startPosition;
        while (elapsedTime < duration && distanceCovered < distance)
        {
            var t = elapsedTime / duration;
            var newPosition = Vector3.Lerp(startPosition, targetPosition, t);
            trail.transform.position = newPosition;
            distanceCovered += Vector3.Distance(lastPosition, newPosition);
            lastPosition = newPosition;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        trail.emitting = false;
        yield return new WaitForSeconds(trail.time);

        if (this && trailPool != null)
        {
            trailPool.Release(trail);
        }
    }

    private void Shoot()
    {
        if (!currentWeaponData || !currentWeaponInstance) return;

        animator.SetTrigger("Shoot");

        if (currentWeaponData.shootSound && weaponAudioSource && currentWeaponData.currentAmmo > 0)
        {
            currentWeaponData.currentAmmo--;
            weaponAudioSource.PlayOneShot(currentWeaponData.shootSound);

            var mainCamera = Camera.main;
            if (!mainCamera) return;
            var muzzlePosition = currentWeaponInstance.transform.position +
                                 currentWeaponInstance.transform.TransformDirection(
                                     currentWeaponData.muzzlePosition);
            var shootDirection = mainCamera.transform.forward;
            if (currentWeaponData.muzzleFlash)
            {
                _PlayMuzzleFlash();
            }

            // Notify crosshair of weapon firing
            CrosshairController crosshair = FindObjectOfType<CrosshairController>();
            if (crosshair != null)
            {
                crosshair.ExpandCrosshairOnFire();
            }

            CreateBulletTrail(
                muzzlePosition,
                shootDirection,
                currentWeaponData.range,
                currentWeaponData.trailDuration
            );
        }
        else if (currentWeaponData.currentAmmo <= 0 && currentWeaponData.reloadSound && weaponAudioSource)
        {
            // Play empty sound or initiate auto-reload
            StartCoroutine(Reload());
        }
    }

    private void OnDestroy()
    {
        trailPool?.Clear();
    }
}
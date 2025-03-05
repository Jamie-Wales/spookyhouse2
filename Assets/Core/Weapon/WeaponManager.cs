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
    public Animator animator;
    public GameObject modelObject;

    [Header("Weapon Settings")] public bool createSocketAutomatically = true;
    public Vector3 socketOffset = Vector3.zero;

    [Header("Debug & Testing")] [Tooltip("Current weapon index (0-based)")]
    public int currentWeaponIndex = 0;

    [Tooltip("Press this key to refresh the current weapon (for testing position/rotation/scale)")]
    public KeyCode refreshWeaponKey = KeyCode.F;

    [Tooltip("Press this key to cycle to the next weapon")]
    public KeyCode cycleWeaponKey = KeyCode.G;

    private GameObject _currentWeaponInstance;
    private WeaponData _currentWeaponData;
    private AudioSource _weaponAudioSource;
    private Transform _trailStartPoint;

    private ObjectPool<TrailRenderer> _trailPool;
    private const int DefaultPoolSize = 20;
    private const int MaxPoolSize = 100;

    private bool _isProcessingShot = false;
    private float _lastFireTime;
    private bool _isReloading = false;

    private void Awake()
    {
        _trailPool = new ObjectPool<TrailRenderer>(
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
        // Try to find the animator if not assigned
        if (animator == null && modelObject != null)
        {
            animator = modelObject.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelObject.GetComponentInChildren<Animator>();
            }
        }

        if (animator && animator.isHuman)
        {
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand)
            {
                if (weaponSocket)
                {
                    weaponSocket.SetParent(rightHand, true);
                    weaponSocket.localPosition = socketOffset;
                    _trailStartPoint = weaponSocket;
                    Debug.Log("Attached weapon socket to right hand");
                }
                else if (createSocketAutomatically)
                {
                    // Create a new socket
                    GameObject socketObj = new GameObject("WeaponSocket");
                    weaponSocket = socketObj.transform;
                    weaponSocket.SetParent(rightHand, true);
                    weaponSocket.localPosition = socketOffset;
                    weaponSocket.localRotation = Quaternion.identity;
                    _trailStartPoint = weaponSocket;
                    Debug.Log("Created new weapon socket on right hand");
                }
                else
                {
                    Debug.LogWarning("No weapon socket assigned and automatic creation is disabled.");
                }
            }
            else
            {
                Debug.LogError("Could not find right hand bone! Make sure your avatar is properly rigged as Humanoid.");
                return;
            }
        }
        else
        {
            Debug.LogError("Animator is missing or not set to humanoid! Please assign an animator in the Inspector.");
            return;
        }

        if (weapons != null && weapons.Count > 0)
        {
            EquipWeapon(currentWeaponIndex);
        }
    }

    // Pool management functions
    private TrailRenderer CreateTrailRenderer()
    {
        if (!_currentWeaponData || _currentWeaponData.trailRendererPrefab == null)
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

        TrailRenderer instance = Instantiate(_currentWeaponData.trailRendererPrefab);
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
        if (trail && trail.gameObject.activeInHierarchy)
        {
            trail.emitting = true;
        }
    }

    private void OnTrailReturnedToPool(TrailRenderer trail)
    {
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
            trail.gameObject.SetActive(false);
        }
    }

    private void OnTrailDestroyed(TrailRenderer trail)
    {
        if (trail != null)
        {
            Destroy(trail.gameObject);
        }
    }

    private void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Count)
        {
            Debug.LogError($"Invalid weapon index: {weaponIndex}. Must be between 0 and {weapons.Count - 1}");
            return;
        }

        currentWeaponIndex = weaponIndex;

        if (_currentWeaponInstance)
        {
            Destroy(_currentWeaponInstance);
        }

        _currentWeaponData = weapons[weaponIndex];
        if (_currentWeaponData.weaponPrefab && weaponSocket)
        {
            // Simply instantiate the weapon prefab directly
            _currentWeaponInstance = Instantiate(_currentWeaponData.weaponPrefab, weaponSocket, true);

            // Apply position, rotation, and scale directly to the instance
            _currentWeaponInstance.transform.localPosition = _currentWeaponData.weaponPositionOffset;
            _currentWeaponInstance.transform.localRotation = Quaternion.Euler(_currentWeaponData.weaponRotationOffset);
            _currentWeaponInstance.transform.localScale = _currentWeaponData.weaponScale;

            // Get audio source from the weapon
            _weaponAudioSource = _currentWeaponInstance.GetComponentInChildren<AudioSource>();

            Debug.Log($"Equipped {_currentWeaponData.weaponName} with simple instantiation method");
        }
    }

// Helper method to print all child scales recursively (for debugging)
    private void PrintChildScales(Transform parent, string indent = "")
    {
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Debug.Log($"{indent}Child '{child.name}' scale: {child.localScale}");

            // Recurse for this child's children, with increased indentation
            PrintChildScales(child, indent + "  ");
        }
    }

    public void RefreshCurrentWeapon()
    {
        if (weapons != null && weapons.Count > 0 && currentWeaponIndex >= 0 && currentWeaponIndex < weapons.Count)
        {
            Debug.Log($"Refreshing weapon: {weapons[currentWeaponIndex].weaponName}");
            EquipWeapon(currentWeaponIndex);
        }
    }

    // Cycle to the next weapon
    public void CycleToNextWeapon()
    {
        if (weapons != null && weapons.Count > 0)
        {
            int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
            Debug.Log($"Cycling weapon from {currentWeaponIndex} to {nextIndex}");
            EquipWeapon(nextIndex);
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private void _PlayMuzzleFlash()
    {
        if (!_currentWeaponInstance) return;
        var muzzlePosition = _currentWeaponInstance.transform.position +
                             _currentWeaponInstance.transform.TransformDirection(
                                 _currentWeaponData.muzzlePosition);

        var vfxInstance = Instantiate(_currentWeaponData.muzzleFlash, muzzlePosition,
            _currentWeaponInstance.transform.rotation);

        vfxInstance.transform.SetParent(_currentWeaponInstance.transform, true);
        var visualEffect = vfxInstance.GetComponent<VisualEffect>();
        if (visualEffect)
        {
            visualEffect.Play();
        }

        Destroy(vfxInstance.gameObject, 0.15f);
    }

    public void Update()
    {
        // Handle weapon update key press
        if (Input.GetKeyDown(refreshWeaponKey))
        {
            RefreshCurrentWeapon();
        }

        // Handle weapon cycle key press
        if (Input.GetKeyDown(cycleWeaponKey))
        {
            CycleToNextWeapon();
        }

        if (_isReloading)
            return;

        bool shouldFire = false;
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            shouldFire = true;
        }
        else if (_currentWeaponData && _currentWeaponData.isAutomatic && Mouse.current.leftButton.isPressed)
        {
            var timeSinceLastFire = Time.time - _lastFireTime;
            var fireInterval = 1f / _currentWeaponData.fireRate;

            if (timeSinceLastFire >= fireInterval)
            {
                shouldFire = true;
            }
        }

        if (shouldFire && !_isProcessingShot && _currentWeaponData && _currentWeaponData.currentAmmo > 0)
        {
            if (!_currentWeaponData.isAutomatic)
            {
                _isProcessingShot = true;
            }

            Shoot();
            _lastFireTime = Time.time;

            if (!_currentWeaponData.isAutomatic)
            {
                StartCoroutine(ResetProcessingFlag());
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && !_isReloading &&
            _currentWeaponData && _currentWeaponData.currentAmmo < _currentWeaponData.maxAmmo)
        {
            StartCoroutine(Reload());
        }
    }

    private IEnumerator ResetProcessingFlag()
    {
        yield return null;
        _isProcessingShot = false;
    }

    private IEnumerator Reload()
    {
        if (_isReloading)
            yield break;

        _isReloading = true;
        if (_weaponAudioSource && _currentWeaponData.reloadSound)
        {
            _weaponAudioSource.PlayOneShot(_currentWeaponData.reloadSound);
        }

        yield return new WaitForSeconds(_currentWeaponData.reloadTime);
        _currentWeaponData.currentAmmo = _currentWeaponData.maxAmmo;
        _isReloading = false;
    }

    private void CreateBulletTrail(Vector3 startPosition, Vector3 direction, float distance, float duration)
    {
        Debug.Log(
            $"Creating bullet trail from {startPosition}, direction {direction}, distance {distance}, duration {duration}");

        TrailRenderer trail = _trailPool.Get();
        if (!trail)
        {
            Debug.LogWarning("Failed to get trail from pool");
            return;
        }

        Debug.Log("Got trail from pool successfully");
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
            if (!trail || !trail.gameObject.activeInHierarchy)
            {
                yield break;
            }

            var t = elapsedTime / duration;
            var newPosition = Vector3.Lerp(startPosition, targetPosition, t);
            trail.transform.position = newPosition;
            distanceCovered += Vector3.Distance(lastPosition, newPosition);
            lastPosition = newPosition;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (trail && trail.gameObject.activeInHierarchy)
        {
            trail.emitting = false;
            yield return new WaitForSeconds(trail.time);
        }

        if (trail && this && _trailPool != null)
        {
            _trailPool.Release(trail);
        }
        else if (trail)
        {
            Destroy(trail.gameObject);
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private void Shoot()
    {
        Debug.Log("Shoot method called");

        if (!_currentWeaponData)
        {
            Debug.LogWarning("No weapon data available");
            return;
        }

        if (!_currentWeaponInstance)
        {
            Debug.LogWarning("No weapon instance available");
            return;
        }

        Debug.Log(
            $"Weapon: {_currentWeaponData.weaponName}, Ammo: {_currentWeaponData.currentAmmo}/{_currentWeaponData.maxAmmo}");

        if (animator != null)
        {
            animator.SetTrigger("Shoot");
            Debug.Log("Shoot animation triggered");
        }

        // Check each condition separately
        if (_currentWeaponData.shootSound == null)
        {
            Debug.LogWarning("Weapon has no shoot sound assigned");
        }

        if (_weaponAudioSource == null)
        {
            Debug.LogWarning("Weapon has no audio source");
        }

        if (_currentWeaponData.currentAmmo <= 0)
        {
            Debug.LogWarning("Weapon is out of ammo");
        }

        if (_currentWeaponData.shootSound && _weaponAudioSource && _currentWeaponData.currentAmmo > 0)
        {
            Debug.Log("All shooting conditions met, proceeding with effects");
            _currentWeaponData.currentAmmo--;
            _weaponAudioSource.PlayOneShot(_currentWeaponData.shootSound);

            var mainCamera = Camera.main;
            if (!mainCamera)
            {
                Debug.LogWarning("No main camera found");
                return;
            }

            var muzzlePosition = _currentWeaponInstance.transform.TransformPoint(_currentWeaponData.muzzlePosition);
            Debug.Log($"Muzzle position: {muzzlePosition}, Muzzle offset: {_currentWeaponData.muzzlePosition}");

            var shootDirection = mainCamera.transform.forward;

            if (_currentWeaponData.muzzleFlash)
            {
                Debug.Log("Playing muzzle flash");
                _PlayMuzzleFlash();
            }
            else
            {
                Debug.LogWarning("No muzzle flash prefab assigned");
            }

            CrosshairController crosshair = FindObjectOfType<CrosshairController>();
            if (crosshair)
            {
                crosshair.ExpandCrosshairOnFire();
            }

            Debug.Log(
                $"Creating bullet trail with range: {_currentWeaponData.range}, duration: {_currentWeaponData.trailDuration}");
            CreateBulletTrail(
                muzzlePosition,
                shootDirection,
                _currentWeaponData.range,
                _currentWeaponData.trailDuration
            );

            WeaponHitDetection hitDetection = GetComponent<WeaponHitDetection>();
            if (hitDetection)
            {
                hitDetection.PerformHitDetection(muzzlePosition, _currentWeaponData.damage);
            }
        }
        else if (_currentWeaponData.currentAmmo <= 0 && _currentWeaponData.reloadSound && _weaponAudioSource)
        {
            Debug.Log("Out of ammo, reloading");
            StartCoroutine(Reload());
        }
        else
        {
            Debug.LogWarning("Shooting conditions not met");
        }
    }

    private void OnDestroy()
    {
        _trailPool?.Clear();
    }
}
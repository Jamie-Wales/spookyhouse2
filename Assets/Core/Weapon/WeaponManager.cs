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
                    // Attach existing socket to right hand
                    weaponSocket.SetParent(rightHand, false);
                    weaponSocket.localPosition = socketOffset;
                    _trailStartPoint = weaponSocket;
                    Debug.Log("Attached weapon socket to right hand");
                }
                else if (createSocketAutomatically)
                {
                    // Create a new socket
                    GameObject socketObj = new GameObject("WeaponSocket");
                    weaponSocket = socketObj.transform;
                    weaponSocket.SetParent(rightHand, false);
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
            EquipWeapon(0);
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
        if (trail != null && trail.gameObject.activeInHierarchy)
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
        if (_currentWeaponInstance)
        {
            Destroy(_currentWeaponInstance);
        }

        _currentWeaponData = weapons[weaponIndex];
        if (_currentWeaponData.weaponPrefab && weaponSocket)
        {
            GameObject prefabInstance = Instantiate(_currentWeaponData.weaponPrefab);
            _currentWeaponInstance = prefabInstance;
            Vector3 originalLocalPos = prefabInstance.transform.localPosition;
            Quaternion originalLocalRot = prefabInstance.transform.localRotation;
            Vector3 originalLocalScale = prefabInstance.transform.localScale;
            prefabInstance.transform.SetParent(weaponSocket, false);
            prefabInstance.transform.localPosition = originalLocalPos;
            prefabInstance.transform.localRotation = originalLocalRot;
            prefabInstance.transform.localScale = originalLocalScale;
            _weaponAudioSource = _currentWeaponInstance.GetComponentInChildren<AudioSource>();
            Debug.Log($"Equipped {_currentWeaponData.weaponName} at socket with original transform values");
            CrosshairController crosshair = FindObjectOfType<CrosshairController>();
            if (crosshair != null && _currentWeaponData.crosshairSprite != null)
            {
                crosshair.SetCrosshairSprite(_currentWeaponData.crosshairSprite);
            }
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
            _currentWeaponData != null && _currentWeaponData.currentAmmo < _currentWeaponData.maxAmmo)
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
        TrailRenderer trail = _trailPool.Get();
        if (!trail) return;

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
            if (trail == null || !trail.gameObject.activeInHierarchy)
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
        if (!_currentWeaponData || !_currentWeaponInstance) return;

        if (animator != null)
        {
            animator.SetTrigger("Shoot");
        }

        if (_currentWeaponData.shootSound && _weaponAudioSource && _currentWeaponData.currentAmmo > 0)
        {
            _currentWeaponData.currentAmmo--;
            _weaponAudioSource.PlayOneShot(_currentWeaponData.shootSound);

            var mainCamera = Camera.main;
            if (!mainCamera) return;
            var muzzlePosition = _currentWeaponInstance.transform.position +
                                 _currentWeaponInstance.transform.TransformDirection(
                                     _currentWeaponData.muzzlePosition);
            var shootDirection = mainCamera.transform.forward;
            if (_currentWeaponData.muzzleFlash)
            {
                _PlayMuzzleFlash();
            }

            CrosshairController crosshair = FindObjectOfType<CrosshairController>();
            if (crosshair)
            {
                crosshair.ExpandCrosshairOnFire();
            }

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
            StartCoroutine(Reload());
        }
    }

    private void OnDestroy()
    {
        _trailPool?.Clear();

        TrailRenderer[] trails = FindObjectsOfType<TrailRenderer>();
        foreach (var trail in trails)
        {
            if (trail != null && trail.gameObject.name.Contains("Trail"))
            {
                Destroy(trail.gameObject);
            }
        }
    }
}
using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Basic Info")]
    public string weaponName;
    public GameObject weaponPrefab;
    
    [Header("Weapon Transform")]
    public Vector3 weaponScale = Vector3.one;
    public Vector3 weaponPositionOffset = Vector3.zero;
    public Vector3 weaponRotationOffset = Vector3.zero;
    
    // Rest of your WeaponData fields
    [Header("Weapon Stats")]
    public Vector3 muzzlePosition;
    public GameObject muzzleFlash;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public int maxAmmo = 30;
    public int currentAmmo = 30;
    public float fireRate = 10f;
    public float reloadTime = 2f;
    public float range = 100f;
    public float damage = 10f;
    public bool isAutomatic = false;
    public Sprite crosshairSprite;
    public TrailRenderer trailRendererPrefab;
    public float trailDuration = 0.1f;
}

using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Weapon Info")]
    public string weaponName = "New Weapon";
    public GameObject weaponPrefab;
    
    [Header("Stats")]
    public float damage = 10f;
    public float fireRate = 0.5f;
    public float range = 100f;
    public int maxAmmo = 30;
    public int currentAmmo;
    public float reloadTime = 2f;
    public bool isAutomatic = false;
    
    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    
    [Header("Visual Effects")]
    public GameObject muzzleFlash;
    public Vector3 muzzlePosition = new Vector3(0f, 0f, 1f);
    
    [Header("Trail Settings")] 
    public TrailRenderer trailRendererPrefab;
    public float trailDuration = 0.5f;
    
    [Header("Crosshair Settings")]
    public Sprite crosshairSprite;
    public float crosshairSize = 20f;
    public float crosshairSpread = 10f;
    public float aimDownSightsCrosshairSize = 10f;
}
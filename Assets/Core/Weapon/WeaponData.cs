using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName = "New Weapon";
    public GameObject weaponPrefab;
    public float damage = 10f;
    public float fireRate = 0.5f;
    public float range = 100f;
    public int maxAmmo = 30;
    public int currentAmmo;
    public float reloadTime = 2f;
    public bool isAutomatic = false;
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public GameObject muzzleFlash;
    public Vector3 muzzlePosition = new Vector3(0f, 0f, 1f);
}
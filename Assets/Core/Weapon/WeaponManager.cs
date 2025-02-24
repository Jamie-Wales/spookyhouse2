using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class WeaponManager : MonoBehaviour
{
    [Header("References")]
    public List<WeaponData> weapons;
    public Transform weaponSocket;
    
    private GameObject currentWeaponInstance;
    private WeaponData currentWeaponData;
    private AudioSource weaponAudioSource;
    private Animator animator;

    private void Start()
    {
        // Get the animator
        animator = GetComponentInParent<Animator>();
        
        if (animator != null && animator.isHuman)
        {
            // Get the right hand bone
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            
            if (rightHand != null)
            {
                // If we have a weapon socket, parent it to the right hand
                if (weaponSocket != null)
                {
                    weaponSocket.SetParent(rightHand, false);
                    Debug.Log("Attached weapon socket to right hand");
                }
                else
                {
                    // Create a new weapon socket if none exists
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

        // Equip first weapon if available
        if (weapons != null && weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    private void EquipWeapon(int weaponIndex)
    {
        if (currentWeaponInstance)
        {
            Destroy(currentWeaponInstance);
        }

        currentWeaponData = weapons[weaponIndex];
        if (currentWeaponData.weaponPrefab && weaponSocket != null)
        {
            currentWeaponInstance = Instantiate(currentWeaponData.weaponPrefab, weaponSocket);
            currentWeaponInstance.transform.localPosition = Vector3.zero;
            currentWeaponInstance.transform.localRotation = Quaternion.identity;
            
            weaponAudioSource = currentWeaponInstance.GetComponentInChildren<AudioSource>();
            Debug.Log($"Equipped {currentWeaponData.weaponName} at socket");
        }
    }

    private void _PlayMuzzleFlash()
    {
        if (!currentWeaponInstance) return;

        Vector3 muzzlePosition = currentWeaponInstance.transform.position +
                               currentWeaponInstance.transform.TransformDirection(
                                   currentWeaponData.muzzlePosition);

        var vfxInstance = Instantiate(currentWeaponData.muzzleFlash, muzzlePosition,
            currentWeaponInstance.transform.rotation);
            
        vfxInstance.transform.SetParent(currentWeaponInstance.transform, true);
        vfxInstance.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        VisualEffect visualEffect = vfxInstance.GetComponent<VisualEffect>();
        if (visualEffect)
        {
            visualEffect.Play();
        }

        Destroy(vfxInstance.gameObject, 0.15f);
    }

    public void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (!currentWeaponData || !currentWeaponInstance) return;

        if (currentWeaponData.muzzleFlash)
        {
            _PlayMuzzleFlash();
        }

        if (currentWeaponData.shootSound && weaponAudioSource)
        {
            weaponAudioSource.PlayOneShot(currentWeaponData.shootSound);
        }
    }
}
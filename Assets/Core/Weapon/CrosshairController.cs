using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("Crosshair Settings")]
    public Image crosshairImage;
    public float defaultSize = 20f;
    public float expandedSize = 40f;
    public float shrinkSpeed = 5f;
    public float expandSpeed = 15f;
    
    [Header("Dynamic Crosshair")]
    public bool isDynamic = true;
    public float movementSpread = 10f;
    public float fireSpread = 20f;
    
    private RectTransform crosshairRect;
    private float currentSize;
    private float targetSize;
    private PlayerController playerController;
    private WeaponManager weaponManager;

    private void Start()
    {
        if (crosshairImage == null)
        {
            Debug.LogError("Crosshair image not assigned!");
            return;
        }

        crosshairRect = crosshairImage.rectTransform;
        currentSize = defaultSize;
        targetSize = defaultSize;
        playerController = FindObjectOfType<PlayerController>();
        weaponManager = FindObjectOfType<WeaponManager>();
    }
    
    private void Update()
    {
        if (!isDynamic)
            return;
        targetSize = defaultSize;
        if (playerController && playerController.IsMoving())
        {
            targetSize += movementSpread;
        }
        AdjustCrosshairSize();
    }
    
    private void AdjustCrosshairSize()
    {
        float speed = currentSize < targetSize ? expandSpeed : shrinkSpeed;
        currentSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * speed);
        crosshairRect.sizeDelta = new Vector2(currentSize, currentSize);
    }
    
    public void ExpandCrosshairOnFire()
    {
        targetSize = expandedSize;
    }
    
    public void SetCrosshairSprite(Sprite newCrosshairSprite)
    {
        if (crosshairImage != null && newCrosshairSprite != null)
        {
            crosshairImage.sprite = newCrosshairSprite;
        }
    }
}
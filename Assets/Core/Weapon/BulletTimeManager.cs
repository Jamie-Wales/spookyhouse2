using System.Collections.Generic;
using UnityEngine;

public class BulletTimeManager : MonoBehaviour
{
    public static BulletTimeManager Instance { get; private set; }

[Header("Audio Settings")]
    [Range(0.1f, 1f)] public float otherSoundVolume = 0.5f; 
    [Range(0.1f, 1f)] public float otherSoundPitch = 0.5f;  
    
    private float _defaultPitch = 1f;
    private Dictionary<AudioSource, float> _originalVolumes = new Dictionary<AudioSource, float>();
    private Dictionary<AudioSource, float> _originalPitches = new Dictionary<AudioSource, float>();
    [Header("Bullet Time Settings")]
    [Range(0.05f, 1f)] public float bulletTimeScale = 0.3f;
    public float maxBulletTimeEnergy = 100f;
    public float energyRechargeRate = 20f;
    public KeyCode bulletTimeKey = KeyCode.Mouse3;

    [Header("Audio")]
    public AudioClip bulletTimeSound;
    [Range(0f, 1f)] public float soundVolume = 0.5f;
    
    [Header("Visual Effects")]
    public Color screenTintColor = new Color(1f, 0.92f, 0.016f, 0.3f); // More visible yellow
    
    private float _currentEnergy;
    private bool _isInBulletTime;
    private AudioSource _audioSource;
    private float _defaultFixedDeltaTime;
    private float _lastPlaybackTime; // Store audio position
    private GameObject _screenTintObj;
    private SpriteRenderer _screenTintRenderer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            _currentEnergy = maxBulletTimeEnergy;
            _defaultFixedDeltaTime = Time.fixedDeltaTime;
            SetupAudio();
            SetupScreenTint();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.clip = bulletTimeSound;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume = soundVolume;
    }

    private void SetupScreenTint()
    {
        // Create a quad that fills the screen
        _screenTintObj = new GameObject("ScreenTint");
        _screenTintObj.transform.parent = Camera.main.transform;
        
        // Position it in front of the camera
        _screenTintObj.transform.localPosition = new Vector3(0, 0, 0.5f);
        _screenTintObj.transform.localRotation = Quaternion.identity;
        
        // Add sprite renderer
        _screenTintRenderer = _screenTintObj.AddComponent<SpriteRenderer>();
        _screenTintRenderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        _screenTintRenderer.color = new Color(0, 0, 0, 0); // Start transparent
        
        // Scale it to cover the screen
        float camHeight = Camera.main.orthographicSize * 2;
        float camWidth = camHeight * Camera.main.aspect;
        _screenTintObj.transform.localScale = new Vector3(camWidth * 10, camHeight * 10, 10);
        
        // Set to overlay
        _screenTintRenderer.sortingOrder = 999;
    }

    private void Update()
    {
        if (Input.GetKeyDown(bulletTimeKey) && _currentEnergy > 0)
        {
            ToggleBulletTime(true);
        }
        else if (Input.GetKeyUp(bulletTimeKey))
        {
            ToggleBulletTime(false);
        }

        if (_isInBulletTime)
        {
            _currentEnergy -= Time.unscaledDeltaTime * 10f;
            if (_currentEnergy <= 0)
            {
                _currentEnergy = 0;
                ToggleBulletTime(false);
            }
        }
        else
        {
            _currentEnergy = Mathf.Min(_currentEnergy + (energyRechargeRate * Time.deltaTime), maxBulletTimeEnergy);
        }
    }

 public void ToggleBulletTime(bool activate)
    {
        if (activate && _currentEnergy > 0)
        {
            Time.timeScale = bulletTimeScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * Time.timeScale;
            _isInBulletTime = true;
            
            if (_audioSource && bulletTimeSound)
            {
                _audioSource.time = _lastPlaybackTime;
                _audioSource.pitch = 1f;
                _audioSource.Play();
            }

            AdjustAllSounds(otherSoundVolume, otherSoundPitch);
            
            if (_screenTintRenderer)
            {
                _screenTintRenderer.color = screenTintColor;
            }
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
            _isInBulletTime = false;
            
            if (_audioSource)
            {
                _lastPlaybackTime = _audioSource.time;
                _audioSource.Stop();
            }
            RestoreAllSounds();
            if (_screenTintRenderer)
            {
                _screenTintRenderer.color = new Color(0, 0, 0, 0);
            }
        }
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _defaultFixedDeltaTime;
    }

  private void AdjustAllSounds(float volume, float pitch)
    {
    
        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource source in allSources)
        {
            if (source == _audioSource) continue;
            _originalVolumes[source] = source.volume;
            _originalPitches[source] = source.pitch;

            source.volume *= volume;
            source.pitch *= pitch;
        }
    }

    private void RestoreAllSounds()
    {
        foreach (var kvp in _originalVolumes)
        {
            AudioSource source = kvp.Key;
            if (source) 
            {
                source.volume = kvp.Value;
                source.pitch = _originalPitches[source];
            }
        }
        _originalVolumes.Clear();
        _originalPitches.Clear();
    }

    private void OnDestroy()
    {
        RestoreAllSounds();
        if (_screenTintObj)
        {
            Destroy(_screenTintObj);
        }
    }
}
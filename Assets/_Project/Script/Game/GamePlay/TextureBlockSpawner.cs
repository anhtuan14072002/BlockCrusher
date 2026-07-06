using UnityEngine;

public sealed class TextureBlockSpawner : MonoBehaviour
{
    [SerializeField] private Texture2D _texture;
    [SerializeField] private GameObject _blockPrefab;
    [SerializeField] private Transform _container;
    [SerializeField] private float _pixelSize = 0.12f;
    [SerializeField, Range(1, 16)] private int _sampleStep = 1;
    [SerializeField, Range(0f, 1f)] private float _alphaThreshold = 0.1f;
    [SerializeField] private bool _centerTexture = true;
    [SerializeField] private bool _applyPixelColor = true;
    [SerializeField] private bool _spawnOnAwake = true;

    private void Awake()
    {
        if (_spawnOnAwake)
            Spawn();
    }

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        if (_texture == null || _blockPrefab == null)
            return;

        Clear();

        Color32[] pixels;
        try
        {
            pixels = _texture.GetPixels32();
        }
        catch (UnityException)
        {
            Debug.LogError("TextureBlockSpawner needs a readable texture.", this);
            return;
        }

        Transform parent = _container != null ? _container : transform;
        int width = _texture.width;
        int height = _texture.height;
        float alphaLimit = _alphaThreshold * 255f;
        Vector3 offset = _centerTexture ? new Vector3((width - _sampleStep) * _pixelSize * -0.5f, (height - _sampleStep) * _pixelSize * -0.5f, 0f) : Vector3.zero;

        for (int y = 0; y < height; y += _sampleStep)
        {
            for (int x = 0; x < width; x += _sampleStep)
            {
                Color32 color = pixels[y * width + x];
                if (color.a <= alphaLimit)
                    continue;

                Vector3 localPosition = offset + new Vector3(x * _pixelSize, y * _pixelSize, 0f);
                GameObject block = Instantiate(_blockPrefab, parent);
                block.name = "PixelBlock_" + x + "_" + y;
                block.transform.localPosition = localPosition;
                block.transform.localRotation = Quaternion.identity;
                block.transform.localScale = Vector3.one * _pixelSize;

                PixelBlock pixelBlock = block.GetComponent<PixelBlock>();
                if (pixelBlock == null)
                    pixelBlock = block.AddComponent<PixelBlock>();

                pixelBlock.Initialize(color, _applyPixelColor);
            }
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        Transform parent = _container != null ? _container : transform;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }
}

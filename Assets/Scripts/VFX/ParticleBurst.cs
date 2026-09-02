using UnityEngine;

/// <summary>
/// Пулируемые процедурные частицы (VisualStyle §6.3): quad-полигоны чистых цветов,
/// разлетаются, fade out. Один экземпляр на тип (пул держит ParticlePool).
/// </summary>
public class ParticleBurst : Poolable
{
    private static readonly int ColorKey = Shader.PropertyToID("_Color");

    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;
    private Transform _tr;
    private float _life = 0.4f;
    private float _age;
    private Color _color;
    private float _speed;
    private Vector3 _dir;
    private float _angularSpeed;
    private Vector3 _scale;

    public void Init(Color color, float speed, float life, float size)
    {
        if (_renderer == null)
        {
            _tr = transform;
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
        }
        _color = color;
        _speed = speed;
        _life = life;
        _age = 0f;
        _scale = Vector3.one * size;
        _dir = Random.insideUnitCircle.normalized;
        _dir.z = 0f;
        _angularSpeed = Random.Range(-540f, 540f);
        _tr.localScale = _scale;
        _tr.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float k = Mathf.Clamp01(_age / _life);
        _tr.position += _dir * (_speed * Time.deltaTime);
        _tr.Rotate(0f, 0f, _angularSpeed * Time.deltaTime);
        float alpha = 1f - k;
        Color c = _color;
        c.a = alpha;
        _block.SetColor(ColorKey, c);
        _renderer.SetPropertyBlock(_block);
        _tr.localScale = _scale * (1f - k * 0.6f);
        if (k >= 1f) Release();
    }
}

using UnityEngine;

/// <summary>
/// Пул частиц: один ObjectPool на тип. Burst() берёт из пула, инициализирует, отпускает.
/// </summary>
public class ParticlePool : MonoBehaviour
{
    [SerializeField] private int prewarm = 24;
    [SerializeField] private Mesh quadMesh; // назначится в Awake из GeometryFactory

    private ObjectPool _pool;

    private void Awake()
    {
        quadMesh = GeometryFactory.Quad(1f);
        _pool = new ObjectPool(Create, transform, prewarm);
    }

    private Poolable Create()
    {
        var go = new GameObject("Particle");
        go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = MaterialProvider.Shared;
        return go.AddComponent<ParticleBurst>();
    }

    /// <summary>Взрыв: count частиц, радиус разлёта, случайный размер в [minSize, maxSize].</summary>
    public void Burst(Vector3 position, Color color, int count, float speedMin, float speedMax,
                      float life, float minSize, float maxSize)
    {
        for (int i = 0; i < count; i++)
        {
            var p = _pool.Get() as ParticleBurst;
            p.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.1f);
            p.Init(color, Random.Range(speedMin, speedMax), life * Random.Range(0.7f, 1.2f),
                   Random.Range(minSize, maxSize));
        }
    }
}

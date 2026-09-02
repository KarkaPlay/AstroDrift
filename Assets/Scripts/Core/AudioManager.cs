using UnityEngine;

/// <summary>
/// 6 звуков в стиле BFXR, синтезируются процедурно в рантайме (DevTask шаг 11:
/// «Сгенерировать в BFXR… или процедурно»). Никаких аудио-ассетов — звук
/// строится из sine/noise + envelope, проигрывается через пул AudioSource.
/// Громкость тихая. Абстракция: методы-события, чтобы позже подменить ассетами.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private int poolSize = 8;
    [SerializeField] private float masterVolume = 0.35f;

    private AudioSource[] _sources;
    private int _cursor;

    // Готовые сэмплы
    private AudioClip _shot;
    private AudioClip _hit;
    private AudioClip _smallExplosion;
    private AudioClip _bigExplosion;
    private AudioClip _death;
    private AudioClip _record;

    private void Awake()
    {
        Instance = this;
        _sources = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("AudioSource_" + i);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = masterVolume;
            _sources[i] = src;
        }

        _shot = SynthesizeShot();
        _hit = SynthesizeHit();
        _smallExplosion = SynthesizeSmallExplosion();
        _bigExplosion = SynthesizeBigExplosion();
        _death = SynthesizeDeath();
        _record = SynthesizeRecord();
    }

    private AudioSource NextSource()
    {
        _cursor = (_cursor + 1) % _sources.Length;
        return _sources[_cursor];
    }

    private void Play(AudioClip clip, float volume = 1f)
    {
        var src = NextSource();
        src.clip = clip;
        src.volume = masterVolume * volume;
        src.Play();
    }

    public void PlayShot() => Play(_shot, 0.5f);
    public void PlayHit() => Play(_hit, 0.6f);
    public void PlaySmallExplosion() => Play(_smallExplosion, 0.7f);
    public void PlayBigExplosion() => Play(_bigExplosion, 0.9f);
    public void PlayDeath() => Play(_death, 1f);
    public void PlayRecord() => Play(_record, 0.8f);

    // ——— Синтез (BFXR-стиль) ———

    private static AudioClip MakeClip(float[] samples, string name, float sampleRate = 44100f)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, (int)sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float[] ApplyEnvelope(float[] s, float attack, float decay)
    {
        int n = s.Length;
        float aSamples = Mathf.Max(1, attack * 44100f);
        float dSamples = Mathf.Max(1, decay * 44100f);
        for (int i = 0; i < n; i++)
        {
            float env = 1f;
            if (i < aSamples) env = i / aSamples;
            else if (i < aSamples + dSamples) env = 1f - (i - aSamples) / dSamples;
            else env = 0f;
            s[i] *= env;
        }
        return s;
    }

    /// <summary>Выстрел — тихий высокий «пиу» (синус с быстрым слайдом вниз).</summary>
    private AudioClip SynthesizeShot()
    {
        int n = 44100 / 8; // 0.125 с
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = Mathf.Lerp(1200f, 300f, t * 8f);
            phase += 2f * Mathf.PI * freq / 44100f;
            s[i] = Mathf.Sin(phase) * 0.5f;
        }
        return MakeClip(ApplyEnvelope(s, 0.001f, 0.09f), "Shot");
    }

    /// <summary>Попадание — глухой «тук» (низкий синус + шум).</summary>
    private AudioClip SynthesizeHit()
    {
        int n = 44100 / 10;
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = Mathf.Lerp(220f, 80f, t * 6f);
            phase += 2f * Mathf.PI * freq / 44100f;
            float noise = Random.value * 2f - 1f;
            s[i] = Mathf.Sin(phase) * 0.6f + noise * 0.3f;
        }
        return MakeClip(ApplyEnvelope(s, 0.001f, 0.07f), "Hit");
    }

    /// <summary>Мелкий взрыв (астероид) — шум с низким затуханием.</summary>
    private AudioClip SynthesizeSmallExplosion()
    {
        int n = 44100 / 4;
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = Mathf.Lerp(180f, 50f, t * 3f);
            phase += 2f * Mathf.PI * freq / 44100f;
            float noise = Random.value * 2f - 1f;
            s[i] = Mathf.Sin(phase) * 0.4f + noise * 0.6f;
        }
        return MakeClip(ApplyEnvelope(s, 0.005f, 0.2f), "SmallExplosion");
    }

    /// <summary>Крупный взрыв (ракета/комбо) — громче, длиннее, ниже.</summary>
    private AudioClip SynthesizeBigExplosion()
    {
        int n = 44100 / 3;
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = Mathf.Lerp(140f, 35f, t * 2f);
            phase += 2f * Mathf.PI * freq / 44100f;
            float noise = Random.value * 2f - 1f;
            s[i] = Mathf.Sin(phase) * 0.5f + noise * 0.5f;
        }
        return MakeClip(ApplyEnvelope(s, 0.005f, 0.3f), "BigExplosion");
    }

    /// <summary>Смерть — низкий «бум» + реверберация (несколько затухающих эхо).</summary>
    private AudioClip SynthesizeDeath()
    {
        int n = 44100; // 1 с
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = Mathf.Lerp(90f, 25f, t * 1.5f);
            phase += 2f * Mathf.PI * freq / 44100f;
            float noise = Random.value * 2f - 1f;
            s[i] = Mathf.Sin(phase) * 0.5f + noise * 0.4f;
            // Ранние отражения (реверб): задержанные копии сигнала
            if (i > 44100 / 8) s[i] += s[i - 44100 / 8] * 0.3f;
            if (i > 44100 / 5) s[i] += s[i - 44100 / 5] * 0.2f;
        }
        return MakeClip(ApplyEnvelope(s, 0.01f, 0.8f), "Death");
    }

    /// <summary>Рекорд — короткий фанфар (две ноты вверх).</summary>
    private AudioClip SynthesizeRecord()
    {
        int n = 44100 / 2;
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = t < 0.25f ? 660f : 880f; // E5 → A5
            phase += 2f * Mathf.PI * freq / 44100f;
            s[i] = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2f) * 0.2f;
        }
        return MakeClip(ApplyEnvelope(s, 0.005f, 0.2f), "Record");
    }
}

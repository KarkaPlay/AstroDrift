using UnityEngine;

/// <summary>
/// 6 звуков в стиле BFXR. По умолчанию синтезируются процедурно в рантайме
/// (DevTask шаг 11: «Сгенерировать в BFXR… или процедурно»). Если в конфиге
/// AudioConfig (Assets/Resources/AudioConfig.asset) назначен AudioClip — играет
/// ассет с громкостью из конфига; пустое поле = fallback на синтез.
/// Проигрывается через пул AudioSource. Громкость тихая.
/// Свои файлы (wav/ogg/mp3) кладите в Assets/Audio и перетащите в поля конфига.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private int poolSize = 8;

    /// <summary>
    /// Rewarded-показ (GDD_DeathScreen_Continue §7): приглушить/вернуть звук.
    /// AudioListener не влияет на нативный звук рекламного SDK — гасим только игру.
    /// </summary>
    public void SetMuted(bool muted) => AudioListener.volume = muted ? 0f : 1f;

    private float _masterVolume = 0.35f; // fallback, если конфига нет

    private AudioConfig config; // Assets/Resources/AudioConfig.asset, может отсутствовать

    // Клипы: из конфига, иначе процедурный синтез
    private AudioClip _shot;
    private AudioClip _hit;
    private AudioClip _smallExplosion;
    private AudioClip _bigExplosion;
    private AudioClip _death;
    private AudioClip _record;

    // Громкости событий (из конфига или дефолты как раньше)
    private float _volShot = 0.5f, _volHit = 0.6f, _volSmall = 0.7f,
                  _volBig = 0.9f, _volDeath = 1f, _volRecord = 0.8f;

    private AudioSource[] _sources;
    private int _cursor;

    private void Awake()
    {
        Instance = this;

        LoadConfig();

        _sources = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("AudioSource_" + i);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            _sources[i] = src;
        }

        // Синтез только для звуков, не подменённых в конфиге
        _shot = ResolveClip(config?.shot, SynthesizeShot(), ref _volShot);
        _hit = ResolveClip(config?.hit, SynthesizeHit(), ref _volHit);
        _smallExplosion = ResolveClip(config?.smallExplosion, SynthesizeSmallExplosion(), ref _volSmall);
        _bigExplosion = ResolveClip(config?.bigExplosion, SynthesizeBigExplosion(), ref _volBig);
        _death = ResolveClip(config?.death, SynthesizeDeath(), ref _volDeath);
        _record = ResolveClip(config?.record, SynthesizeRecord(), ref _volRecord);
    }

    /// <summary>
    /// Грузит AudioConfig из Resources. Конфига нет — работает как раньше
    /// (синтез, masterVolume 0.35). Null-safe.
    /// </summary>
    private void LoadConfig()
    {
        config = Resources.Load<AudioConfig>("AudioConfig");
        if (config == null) return;

        _masterVolume = config.masterVolume;
    }

    /// <summary>Клип из конфига, если назначен (громкость тоже из конфига), иначе синтез с дефолтной громкостью.</summary>
    private static AudioClip ResolveClip(AudioConfig.SoundEntry entry, AudioClip synthesized, ref float volume)
    {
        if (entry != null && entry.clip != null)
        {
            volume = entry.volume;
            return entry.clip;
        }
        return synthesized;
    }

    private AudioSource NextSource()
    {
        _cursor = (_cursor + 1) % _sources.Length;
        return _sources[_cursor];
    }

    private void Play(AudioClip clip, float volume)
    {
        var src = NextSource();
        src.clip = clip;
        src.volume = _masterVolume * volume;
        src.Play();
    }

    public void PlayShot() => Play(_shot, _volShot);
    public void PlayHit() => Play(_hit, _volHit);
    public void PlaySmallExplosion() => Play(_smallExplosion, _volSmall);
    public void PlayBigExplosion() => Play(_bigExplosion, _volBig);
    public void PlayDeath() => Play(_death, _volDeath);
    public void PlayRecord() => Play(_record, _volRecord);

    // ——— Синтез (BFXR-стиль), fallback при пустом конфиге ———

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

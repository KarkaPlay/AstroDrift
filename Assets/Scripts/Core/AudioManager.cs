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

    // Ключи сохранения пользовательских каналов (§0.4: не переименовывать)
    public const string SfxVolumeKey = "AstroDrift.SfxVolume";
    public const string MusicVolumeKey = "AstroDrift.MusicVolume";

    /// <summary>
    /// Rewarded-показ (GDD_DeathScreen_Continue §7): приглушить/вернуть звук.
    /// Гасим не AudioListener, а внутренний фактор: при снятии mute каналы возвращаются
    /// ровно к значениям слайдеров игрока (запись AudioListener.volume=1 их бы перетёрла
    /// только если бы слайдеры жили в AudioListener — они живут здесь).
    /// </summary>
    public void SetMuted(bool muted) => _muted = muted;

    private bool _muted;
    private float _sfxVolume = 1f;   // рантайм-канал «звуки» (единственный источник для Play)
    private float _musicVolume = 1f; // рантайм-канал «музыка» (трека пока нет, значение хранится)

    private float _masterVolume = 0.35f; // fallback, если конфига нет

    private AudioConfig config; // Assets/Resources/AudioConfig.asset, может отсутствовать

    // Клипы: из конфига, иначе процедурный синтез
    private AudioClip _shot;
    private AudioClip _hit;
    private AudioClip _smallExplosion;
    private AudioClip _bigExplosion;
    private AudioClip _death;
    private AudioClip _record;
    private AudioClip _pickup;      // GDD §8 звук 7: подбор пикапа («блип»)
    private AudioClip _perkLevelUp; // GDD §8 звук 8: перк-левелап («дзынь»)

    // Громкости событий (из конфига или дефолты как раньше)
    private float _volShot = 0.5f, _volHit = 0.6f, _volSmall = 0.7f,
                  _volBig = 0.9f, _volDeath = 1f, _volRecord = 0.8f,
                  _volPickup = 0.7f, _volPerk = 0.9f;

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
        _pickup = ResolveClip(config?.pickup, SynthesizePickup(), ref _volPickup);
        _perkLevelUp = ResolveClip(config?.perkLevelUp, SynthesizePerkLevelUp(), ref _volPerk);
    }

    /// <summary>
    /// Грузит AudioConfig из Resources. Конфига нет — работает как раньше
    /// (синтез, masterVolume 0.35). Null-safe.
    /// </summary>
    private void LoadConfig()
    {
        config = Resources.Load<AudioConfig>("AudioConfig");
        if (config != null) _masterVolume = config.masterVolume;

        // Пользовательские каналы: персистентное значение, иначе дефолт конфига.
        // config (вместе с SoundEntry.volume и masterVolume) — ТОЛЬКО чтение, слайдеры его не пишут.
        _sfxVolume = PlatformServices.Save.GetFloat(SfxVolumeKey, config != null ? config.sfxVolume : 1f);
        _musicVolume = PlatformServices.Save.GetFloat(MusicVolumeKey, config != null ? config.musicVolume : 1f);
        _sfxVolume = Mathf.Clamp01(_sfxVolume);
        _musicVolume = Mathf.Clamp01(_musicVolume);
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
        if (clip == null || _muted) return;
        var src = NextSource();
        src.clip = clip;
        // Эффективная громкость = masterVolume (конфиг) × канал игрока × volume звука (конфиг)
        src.volume = _masterVolume * _sfxVolume * volume;
        src.Play();
    }

    // ——— Пользовательские каналы (экран настроек) ———

    public float SfxVolume => _sfxVolume;
    public float MusicVolume => _musicVolume;

    /// <summary>Слайдер «ЗВУКИ ИГРЫ». Пишет ТОЛЬКО рантайм-канал + PlayerPrefs; конфиг не трогает.</summary>
    public void SetSfxVolume(float v)
    {
        _sfxVolume = Mathf.Clamp01(v);
        PlatformServices.Save.SetFloat(SfxVolumeKey, _sfxVolume);
        PlatformServices.Save.Flush();
    }

    /// <summary>Слайдер «МУЗЫКА». Трека в игре нет (§вне скоупа) — значение хранится и переживает перезапуск.</summary>
    public void SetMusicVolume(float v)
    {
        _musicVolume = Mathf.Clamp01(v);
        PlatformServices.Save.SetFloat(MusicVolumeKey, _musicVolume);
        PlatformServices.Save.Flush();
    }

    /// <summary>Вернуть каналы к дефолтам конфига (без записи в сам конфиг).</summary>
    public void ResetVolumesToConfig()
    {
        SetSfxVolume(config != null ? config.sfxVolume : 1f);
        SetMusicVolume(config != null ? config.musicVolume : 1f);
    }

    public void PlayShot() => Play(_shot, _volShot);
    public void PlayHit() => Play(_hit, _volHit);
    public void PlaySmallExplosion() => Play(_smallExplosion, _volSmall);
    public void PlayBigExplosion() => Play(_bigExplosion, _volBig);
    public void PlayDeath() => Play(_death, _volDeath);
    public void PlayRecord() => Play(_record, _volRecord);
    public void PlayPickup() => Play(_pickup, _volPickup);
    public void PlayPerkLevelUp() => Play(_perkLevelUp, _volPerk);

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

    /// <summary>Пикап — bright «блип»: восходящий чистый синус (GDD §8 звук 7).</summary>
    private AudioClip SynthesizePickup()
    {
        int n = 44100 / 6;
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = Mathf.Lerp(880f, 1760f, t * 6f); // A5 → A6
            phase += 2f * Mathf.PI * freq / 44100f;
            s[i] = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2f) * 0.15f;
        }
        return MakeClip(ApplyEnvelope(s, 0.002f, 0.14f), "Pickup");
    }

    /// <summary>Перк-левелап — двухнотный «дзынь» + подтверждение (GDD §8 звук 8).</summary>
    private AudioClip SynthesizePerkLevelUp()
    {
        int n = 44100 / 2;
        var s = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / 44100f;
            float freq = t < 0.18f ? 784f : 1175f; // G5 → D6 (кварта вверх, «дзынь»)
            phase += 2f * Mathf.PI * freq / 44100f;
            s[i] = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 3f) * 0.1f;
        }
        return MakeClip(ApplyEnvelope(s, 0.004f, 0.22f), "PerkLevelUp");
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

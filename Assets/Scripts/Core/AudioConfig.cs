using System;
using UnityEngine;

/// <summary>
/// Конфиг пользовательских звуков (DevTask шаг 11: «Сгенерировать в BFXR… ИЛИ процедурно»).
/// Готовые аудиофайлы (wav / ogg / mp3) кладите в папку Assets/Audio, затем перетащите
/// их мышкой в поля clip ниже в инспекторе ассета Assets/Resources/AudioConfig.asset.
/// Если поле clip пустое — AudioManager играет процедурный синтез (fallback).
/// </summary>
[CreateAssetMenu(fileName = "AudioConfig", menuName = "AstroDrift/AudioConfig")]
public class AudioConfig : ScriptableObject
{
    [Header("Общая громкость (0–1), fallback 0.35")]
    public float masterVolume = 0.35f;

    [Header("Звуки: назначьте клипы из Assets/Audio (пусто = процедурный синтез)")]

    [Tooltip("Выстрел — короткий высокий «пиу» (файл кладётся в Assets/Audio)")]
    public SoundEntry shot = new SoundEntry("Выстрел", 0.5f);

    [Tooltip("Попадание пули по астероиду — глухой «тук» (Assets/Audio)")]
    public SoundEntry hit = new SoundEntry("Попадание", 0.6f);

    [Tooltip("Малый взрыв — раскол астероида (Assets/Audio)")]
    public SoundEntry smallExplosion = new SoundEntry("Малый взрыв", 0.7f);

    [Tooltip("Большой взрыв — ракета / комбо (Assets/Audio)")]
    public SoundEntry bigExplosion = new SoundEntry("Большой взрыв", 0.9f);

    [Tooltip("Смерть корабля — низкий «бум» (Assets/Audio)")]
    public SoundEntry death = new SoundEntry("Смерть", 1f);

    [Tooltip("Новый рекорд — короткая фанфара (Assets/Audio)")]
    public SoundEntry record = new SoundEntry("Рекорд", 0.8f);

    /// <summary>Один звук: имя (справочно), клип из Assets/Audio и его громкость.</summary>
    [Serializable]
    public class SoundEntry
    {
        [Tooltip("Название события — только для ориентира, ни на что не влияет")]
        public string name;

        [Tooltip("Ваш аудиофайл (wav/ogg/mp3 из Assets/Audio). Пусто = процедурный синтез")]
        public AudioClip clip;

        [Tooltip("Громкость этого звука (0–1), умножается на masterVolume")]
        public float volume = 1f;

        public SoundEntry(string name, float volume)
        {
            this.name = name;
            this.volume = volume;
        }
    }
}

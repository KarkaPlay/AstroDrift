using System;
using UnityEngine;

/// <summary>
/// Конфиг звёздного неба (ассет: Assets/Resources/StarFieldConfig.asset).
/// Всё, что можно крутить не трогая код: количество, цвет/яркость, разброс
/// размеров, параллакс и sorting-слои — отдельно для каждого слоя.
/// </summary>
[CreateAssetMenu(fileName = "StarFieldConfig", menuName = "AstroDrift/StarFieldConfig")]
public class StarFieldConfig : ScriptableObject
{
    [Header("Слои звёзд (дальние первыми)")]
    public StarLayer[] layers = Array.Empty<StarLayer>();

    [Header("Переспавн")]
    [Tooltip("Запас за краем экрана, юниты")]
    public float margin = 1f;
    [Tooltip("Глубина кольца переспавна за краем экрана, юниты")]
    public float respawnBelt = 10f;

    [Serializable]
    public class StarLayer
    {
        [Header("Количество и движение")]
        public string name = "Far";
        [Min(0)] public int count = 160;
        [Tooltip("0 = звёзды неподвижны относительно камеры, 1 = звёзды в мире (как корабль)")]
        [Range(0f, 1f)] public float parallax = 0.1f;

        [Header("Яркость")]
        [Tooltip("Цвет звезды (через палитру: Far #2E2E2E, Near #4A4A4A)")]
        public Color color = new Color(0.18f, 0.18f, 0.18f, 1f);

        [Header("Размер")]
        [Tooltip("Базовый размер, юниты (1×1 px ≈ 0.0125, 2×2 px ≈ 0.025)")]
        public float baseSize = 0.0125f;
        [Tooltip("Разброс индивидуального размера: от ×min до ×max")]
        public float sizeMultiplierMin = 0.5f;
        public float sizeMultiplierMax = 2f;

        [Header("Слои рендера")]
        [Tooltip("Sorting Layer (создаётся автоматически, если такого нет)")]
        public string sortingLayerName = "Default";
        [Tooltip("Order in Layer внутри sorting-слоя")]
        public int orderInLayer = 0;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка уровня пилота (ТЗ v1.10): число уровня справа + заливка прогресс-бара.
/// Подпись «УРОВЕНЬ ПИЛОТА» живёт отдельным нодом с LocalizedTextUI (ключ pilot_level_label).
/// Поля необязательные: отсутствующий нод просто не обновляется.
/// </summary>
public class LevelCardUI : MonoBehaviour
{
    [Tooltip("Крупное число уровня пилота (выравнивание по правому краю)")]
    [SerializeField] private TextMeshProUGUI levelValue;

    [Tooltip("Заливка прогресс-бара внутри Mask (UiProgressBar, анкоры)")]
    [SerializeField] private RectTransform barFill;

    private void OnEnable() => Refresh();

    /// <summary>Перечитывает уровень и прогресс (после забега меню показывает актуальное).</summary>
    public void Refresh()
    {
        var pilot = PilotProgressManager.Instance;
        if (pilot == null) return;
        if (levelValue != null) levelValue.text = pilot.PilotLevel.ToString();
        UiProgressBar.Set(barFill, pilot.ProgressToNextLevel());
    }
}

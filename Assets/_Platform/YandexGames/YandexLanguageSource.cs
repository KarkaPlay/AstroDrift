#if UNITY_WEBGL && STORE_YANDEX
using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Гейт G1 (§5.2, вариант A): минимальный аксессор языка аккаунта Яндекс Игр.
/// Источник — AstroDriftLangRequest_js (перенос LangRequest_js, ядро — ysdk.environment.i18n.lang).
/// Только чтение: проводка в игровой язык выполняется задачей 3 (LanguageService).
/// </summary>
public static class YandexLanguageSource
{
    [DllImport("__Internal")]
    private static extern IntPtr AstroDriftLangRequest_js();

    /// <summary>
    /// Код языка аккаунта ЯИ. При сбое плагина/пустом ответе — пустая строка, без исключений.
    /// </summary>
    public static string GetAccountLanguage()
    {
        try
        {
            IntPtr ptr = AstroDriftLangRequest_js();
            if (ptr == IntPtr.Zero) return string.Empty;
            string code = Marshal.PtrToStringUTF8(ptr);
            YG.Insides.YGInsides.FreeBuffer(ptr);
            return code ?? string.Empty;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Lang] AstroDriftLangRequest_js unavailable: {e.Message}");
            return string.Empty;
        }
    }
}
#else
/// <summary>
/// Не-WebGL/не-ЯИ платформы (RuStore, itch, Editor): безопасный no-op источника —
/// основной путь языка остаётся за Application.systemLanguage (§5.2).
/// </summary>
public static class YandexLanguageSource
{
    public static string GetAccountLanguage() => string.Empty;
}
#endif

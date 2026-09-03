#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Редакторный хелпер захвата Game View ВМЕСТЕ со Screen Space Overlay UI
/// (ScreenCapture.CaptureScreenshotAsTexture включает весь кадр, в отличие от
/// camera-rendered снимков MCP). Используется для UX-скриншотов ТЗ v2.
/// Вызов из execute_code: AddComponent<ScreenshotRunner>().Capture(path, delay).
/// </summary>
public class ScreenshotRunner : MonoBehaviour
{
    private string _path;
    private float _delay;
    private System.Action<string> _done;

    public void Capture(string path, float delay = 0.1f, System.Action<string> done = null)
    {
        // ux5-6 r2: путь без "Assets/" по умолчанию кладём в Assets/Screenshots/
        // (раньше голое имя файла писалось в <project>/Screenshots/ вне Assets — мусор).
        if (!path.Contains("/") && !path.Contains("\\"))
            path = System.IO.Path.Combine("Assets/Screenshots", path);
        _path = path;
        _delay = delay;
        _done = done;
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        yield return new WaitForSecondsRealtime(_delay);
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        var png = tex.EncodeToPNG();
        Object.Destroy(tex);
        System.IO.File.WriteAllBytes(_path, png);
        Debug.Log("ScreenshotRunner: saved " + _path);
        _done?.Invoke(_path);
        Destroy(gameObject);
    }
}
#endif

#!/usr/bin/env python3
"""
Повторное применение guard-патчей к SDK-ресолверам defines после того,
как Unity пересоздаёт Library/PackageCache (ре-резолв/обновление пакетов).

Проблема: AppMetricaResolver.cs и AdRevenueAutoCollectionResolver.cs вызывают
PlayerSettings.SetScriptingDefineSymbols БЕЗУСЛОВНО при каждой загрузке домена
([InitializeOnLoadMethod] / resolver-колбэк). Каждая запись помечает
PlayerSettings dirty -> "Requested script compilation because: Player settings
modified" -> домен-релоад -> ресолверы снова пишут -> цикл по 2-5 минут.

Патч: Set вызывается только если множество defines реально изменилось
(нормализация: trim + удаление пустых). У AdRevenue дополнительно убраны
холостые AssetDatabase.SaveAssets() в цикле по таргетам.

Запуск:  python3 Tools/patch_sdk_resolvers.py   (из корня проекта)
Unity на момент запуска можно не закрывать: после патча триггерните
рефреш (Cmd+R в редакторе или Assets -> Refresh).

Идемпотентно: если патч уже применён, файл пропускается.
"""
import glob
import sys

NEW_BLOCK = '''#if UNITY_2021_3_OR_NEWER
                // MCP patch: only write when the set actually changed (prevents "Player settings modified" -> domain reload loop)
                var currentSet = new HashSet<string>(currentDefines.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()));
                var newSet = new HashSet<string>(newDefines.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()));
                if (!currentSet.SetEquals(newSet))
                    PlayerSettings.SetScriptingDefineSymbols(supportedTarget, newDefines);
#else
                var joinedNew = string.Join(";", newDefines);
                if (joinedNew != string.Join(";", currentDefines))
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(supportedTarget, joinedNew);
#endif'''

OLD_BLOCK = '''#if UNITY_2021_3_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(supportedTarget, newDefines);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(supportedTarget, string.Join(";", newDefines));
#endif'''

ADREVENUE_OLD_TAIL = OLD_BLOCK + '''
                AssetDatabase.SaveAssets();
            }
        }'''

ADREVENUE_NEW_TAIL = NEW_BLOCK + '''
            }
        }'''

APPMETRICA_OLD_TAIL = OLD_BLOCK + '''
            }
        }'''

APPMETRICA_NEW_TAIL = NEW_BLOCK + '''
            }
        }'''


def patch(path_glob, old_tail, new_tail, label):
    paths = glob.glob(path_glob)
    if not paths:
        print(f"{label}: package cache dir not found ({path_glob}) - package removed or renamed, skipping")
        return
    for p in paths:
        with open(p, encoding="utf-8-sig") as f:
            s = f.read()
        if "MCP patch" in s:
            print(f"{label}: already patched, skipping ({p})")
            continue
        if old_tail not in s:
            print(f"{label}: PATTERN NOT FOUND in {p} - API changed upstream, patch manually!")
            continue
        s = s.replace(old_tail, new_tail)
        with open(p, "w", encoding="utf-8-sig") as f:
            f.write(s)
        print(f"{label}: patched OK ({p})")


def main():
    patch(
        "Library/PackageCache/io.appmetrica.analytics@*/Editor/AppMetricaResolver.cs",
        APPMETRICA_OLD_TAIL, APPMETRICA_NEW_TAIL, "APPMETRICA",
    )
    patch(
        "Library/PackageCache/com.yandex.mobileads.appmetrica.adrevenue.adapter@*/Editor/AdRevenueAutoCollectionResolver.cs",
        ADREVENUE_OLD_TAIL, ADREVENUE_NEW_TAIL, "ADREVENUE",
    )
    print("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

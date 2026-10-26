# Barometer UWP — сборка в Visual Studio 2017 (Windows 10 Mobile / Lumia 950)

**Стабильная ветка релизов:** `releases/stable` · **Версия:** 1.4.0.35 · **Тег:** `v1.4.0.35-stable`

---

## -1. ВАЖНО: распаковывайте в НОВУЮ пустую папку (лечение WMC1006/WMC9999 «System.Runtime»)

Если вы распаковываете архив релиза ПОВЕРХ старой папки предыдущей версии (или в Downloads
остаётся содержимое от v1.4.0.29–34), вместе с новыми исходниками подхватывается старый каталог
`Barometer_UWP\obj\` с остатками restore-состояния прежних компоновок проекта
(`*.nuget.g.props/.targets`, кэш XamlPreCompile). Именно он возвращает ошибку

    XamlCompiler error WMC1006: Cannot resolve Assembly or Windows Metadata file 'System.Runtime.dll'
    WMC9999: Type universe cannot resolve assembly: System.Runtime, Version=4.0.0.0

несмотря на то, что в самих исходниках затенять больше нечего. Начиная с v1.4.0.35 проект
сам автоматически вычищает такие остатки перед каждой сборкой (таргет PurgeStaleRestoreState),
но надёжнее сделать это один раз руками:

1. Закройте Visual Studio.
2. Удалите папки `Barometer_UWP\obj`, `Barometer_UWP\bin` и `packages` (если есть) — либо
   распакуйте архив v1.4.0.35 в абсолютно новую пустую папку.
3. Откройте `Barometer_UWP.sln`, Build → Rebuild Solution (Debug|ARM). NuGet restore для
   компиляции не требуется (см. раздел 1).


---

## 0. Требования к окружению (строго для VS2017)

| Компонент | Версия | Зачем |
|---|---|---|
| Visual Studio 2017 | 15.x (Update 9+, сборка ≥ 28307) | классическая система проектов UWP |
| Workload «Universal Windows Platform development» | — | шаблоны + XAML-тулчейн |
| .NET Native Toolchain | 1.7.3 (ставится NuGet-restore автоматически) | Release\ARM требует `<UseDotNetNativeToolchain>true</UseDotNetNativeToolchain>` |
| Windows SDK | 10.0.15254 (устанавливается отдельно!) | TargetPlatformVersion проекта |
| Windows 10 Mobile Emulator 15254.1 | из SDK-компонента «Mobile device pack» | только для x86-тестов (опционально) |

> ⚠️ VS2017 не устанавливает SDK 15254 по умолчанию. Скачайте «Windows SDK for Windows 10 (10.0.15254)» из Microsoft Download Center или включите его через VS Installer → Individual components → «Windows 10 SDK (10.0.15254.1)».

## 1. Клонирование и восстановление пакетов

**v1.4.0.35: проект больше НЕ имеет compile-time NuGet-зависимостей.** JSON — vendored
(Helpers\JsonSerializer.cs), xlsx — на System.IO.Compression из Windows SDK. В packages.config
остался только Microsoft.Net.Native.Compiler 1.7.3 (нужен лишь для Release/.NET Native; Debug
собирается без restore вообще). Поэтому:

```bat
git clone -b releases/stable https://github.com/ArtemITuser/Barometer_UWP.git
cd Barometer_UWP
install_deps.cmd        :: опционально: nuget restore для .NET Native compiler (Release)
```

Можно открыть `Barometer_UWP.sln` сразу после распаковки zip — восстановления не требуется.

**История болезни WMC1006/WMC9999 «System.Runtime» (почему раньше не собиралось):**
- v1.4.0.28–31: метапакет Microsoft.NETCore.UniversalWindowsPlatform и/или инжект facade-DLL
  из `packages\netcore50` затеняли SDK reference assemblies → каскады CS0012/WMC1006.
- v1.4.0.32–33: метапакет убран, но остались Newtonsoft.Json + DocumentFormat.OpenXml с
  asset-папками `lib/netstandard1.3`: их compile-time closure = весь граф NETSTANDARD1.3
  facade-пакетов, которого нет в UAP SDK → XamlCompiler падал на первом же нерезолвленном
  System.Runtime, 4.0.0.0.
- v1.4.0.34: обе библиотеки удалены из сборки, сторонних сборочных зависимостей ноль —
  вселенная типов = только Windows SDK + Platform.winmd (как в чистом шаблоне VS2017 UWP).

## 2. Подпись пакета (важно после клонирования!)

Файл `.pfx` намеренно не хранится в git (см. `.gitignore`). При первом открытии в VS
или первой сборке **Signing-вкладка проекта сообщит об отсутствии ключа**:

* Вариант А (рекомендуется): откройте `Barometer_UWP` → Properties → **Signing** →
  **Select from Store… / Create Test Certificate** → задайте пустой пароль. VS создаст
  `Barometer_UWP_TemporaryKey.pfx` рядом с csproj, сборка продолжится без правок.
* Вариант Б (CLI): 
  ```bat
  makecert -r -pe -n "CN=ArtemITuser" -ss BarometerTest Barometer_UWP.cer
  pvk2pfx -pvk BarometerTest.pvk -spc Barometer_UWP.cer -pfx Barometer_UWP_TemporaryKey.pfx
  ```

Для сайдлоада на Lumia 950 установите этот же `.cer` на устройство (перешлите файл
на телефон → установить доверенный корень) либо деплойте из VS (он делает это сам).

## 3. Конфигурации сборки и архитектуры (сознательно закомментированы)

Решение `Barometer_UWP.sln` содержит ровно 4 конфигурации:

```
Debug|ARM      Debug|x86
Release|ARM    Release|x86
```

* **ARM = ARM32 (`PlatformTarget=ARM`)** — основной целевой путь для Lumia 950/950 XL
  и HP Elite x3 (Snapdragon 808/810, Windows 10 Mobile 15254.603).
  Деплой: `Build Release|ARM` → Device Portal / VS Deploy.
* **x86 — ТОЛЬКО для эмулятора Mobile 15254.1.** На реальном телефоне не работает;
  оставлена опциональной по требованию ТЗ. Эмулятор ставится тем же SDK-компонентом.

### Почему нет ARM64 и x64 (задокументировано здесь; в sln — только валидные секции)

Ранее эти заметки хранились в `Barometer_UWP.sln` в виде `REM`-строк между заголовком
файла и первой секцией `Project(...)`. Это синтаксис cmd/MSBuild, а не SLN: парсер
Visual Studio такие строки вне Global-секций не понимает (риск "the solution file is
malformed"). В v1.4.0.27 блок удалён из sln; конспект сохранён здесь:

```
[DISABLED] Debug|ARM64 / Release|ARM64 — ARM64 НЕ поддерживается Windows 10 Mobile
(builds 15254.603 / 15400.0). Целевые устройства (Lumia 950/950XL, Elite x3) — ARM32.
Включать ARM64 можно только во вторичном десктопном sln (UWP PC), см. ниже.
```

Аналогично `x64` отсутствует — он нужен только десктопной редакции UWP, а не мобильной.

### Десктопный вариант (опционально, отдельный sln)

Если понадобится сборка под десктоп Windows 10/11 (там доступен ARM64 и новые фичи
плитки Large Tile из 15400+), скопируйте проект в отдельное решение:

```bat
copy Barometer_UWP.sln Barometer_UWP.Desktop.sln
:: затем в Desktop-sln добавьте конфигурации x64|ARM64 и в csproj переопределите
:: <TargetDeviceFamily Name="Windows.Universal" .../> (уже подходит).
:: Мобильный sln остаётся нетронутым.
```

В текущем мобильном sln конфигураций ARM64/x64 нет вовсе —
так требуемо: «Windows.Mobile, arm32 first, x86 строго опционально».

## 4. Сборка

```bat
:: Релизный ARM32 бандл для Lumia:
msbuild Barometer_UWP.sln /p:Configuration=Release /p:Platform=ARM /p:AppxBundle=Always /p:AppxBundlePlatforms="arm"

:: Отладка на эмуляторе (опционально):
msbuild Barometer_UWP.sln /p:Configuration=Debug /p:Platform=x86
```

Выход: `Barometer_UWP/bin/ARM/Release/AppxPackages/Barometer_UWP_1.4.0.27_arm.appxbundle`.

## 5. Что уже проверено статически (почему код считается стабильным)

* Roslyn-харнесс (Linux, реальные UAP reference-сборки) компилирует весь C#-код
  приложения без ошибок (последний прогон: 0 errors, только CS1998 warnings).
* Все Compile/Page/PRIResource/Content-записи csproj соответствуют файлам в дереве.
* XML csproj/sln/appxmanifest/packages.config проходят парсер; GUID sln == csproj.
* packages.config (v1.4.0.32): только сторонние либы + .NET Native compiler; метапакет
  UWP удалён (см. раздел 1) — facade-тени больше не могут затенить SDK System.Runtime.
* CI GitHub Actions больше не является источником истины для мобильного билда
  (legacy-UWP unsupported на современных runner'ах); для автосборки используйте
  self-hosted runner с VS2017 + SDK 15254 (workflow `build-release.yml` совместим).

## 6. Быстрый чек-лист «клонировал → собрал»

1. `git clone -b releases/stable … && cd Barometer_UWP`
2. (Опционально для Release) `install_deps.cmd` — Debug-сборка не требует restore
3. Открыть `Barometer_UWP.sln` в VS2017 → Signing → Create Test Certificate
4. Выбрать `Release | ARM`, Build Solution (F6)
5. Телефон в режиме разработчика → Deploy (F5) — или взять `.appxbundle` из п.4

Готово: приложение стартует на Lumia 950/950 XL / HP Elite x3 (15254.603).

## ⚠️ v1.4.0.36 — ЕДИНСТВЕННАЯ обязательная предсборочная операция
Если VS упорно выдаёт `WMC1006 Cannot resolve Assembly or Windows Metadata file 'System.Runtime.dll'`
при чистом дереве проекта — виновна машинно-глобальная папка `%USERPROFILE%\.nuget\packages`
(NETStandard/netcore50 копии фасадов затеняют reference assemblies Windows SDK).
Запустите **один раз** `clean_deps.cmd` из корня репозитория, затем Rebuild.
Проект также защищён автоматически: таргет `RejectPackageFacadeShadowReferences` вырезает любые
\packages\ фасады из ReferencePath перед XamlCompiler/csc, а `DisableImplicitNuGetFallbackFolder`
запрещает RAR искать в глобальном packages-фолдере.

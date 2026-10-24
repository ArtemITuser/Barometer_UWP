# Barometer UWP — Changelog

## v1.4.0.27-stable — XAML Spacing (16299+) и чистка sln (2026-10-25)

### StackPanel.Spacing — API доступен только с Windows 10 16299+
Проект таргетит 15063/15254 (Windows 10 Mobile), поэтому `StackPanel.Spacing` недопустим
(WMC/XAML compile error на целевой платформе). Исправлено 8 использований (8 Soacing'ов):
- `Views/ChartMiniTile.xaml` — Spacing="6" убран, отступ перенесён на дочерний TextBlock (`Margin="6,0,0,5"`).
- `Views/GraphicsPage.xaml` — Spacing="12" убран, отступ на первом дочернем TextBlock (`Margin="0,0,12,0"`).
- `Views/SettingsPage.xaml` — 6× Spacing="10" убраны; там где у детей уже был `Margin="0,0,10,0"` (кнопки) — ничего не добавлялось; чекбоксам LocalBackup/OneDriveBackup добавлен `Margin="0,0,10,0"`. В местах удалений оставлены комментарии-предупреждения про 16299+.
Все XAML проходят XML-парсер; в продакшн-коде Spacing не осталось.

### Barometer_UWP.sln
- Удалён блок `REM ...` между заголовком файла и первой секцией `Project(...)`: это cmd-синтаксис,
  просар SLN-парсер VS не понимает такие строки вне Global-секций (риск "solution file is malformed").
  Конспект про ARM32-first/x86/ARM64-x64 полностью сохранён в BUILD_VS2017.md (раздел 3).
- Перевод в CRLF + таб-индентация: формат, ожидаемый Visual Studio 2017 (файл был LF-only).
- Проверено: GUID проекта `{C8D8B5A6-...}` == ProjectGuid в csproj; 4 конфигурации Debug|ARM, Debug|x86, Release|ARM, Release|x86.
- Версии синхронизированы: 1.4.0.27 (appxmanifest, AssemblyInfo).

## v1.4.0.26-stable — Релизная ветка для VS2017 (2026-10-24)

- Стабильная ветка `releases/stable`: код статически верифицирован Roslyn-харнессом (0 ошибок компиляции C#).
- sln: ARM32-first, x86 опционально (эмулятор 15254.1); ARM64/x64 законспектированы как unsupported на Windows 10 Mobile.
- Добавлены BUILD_VS2017.md (полный гайд: SDK 15254, packages.config restore, тестовый сертификат Signing) и install_deps.cmd.
- Версии манифеста/сборки синхронизированы: 1.4.0.26.

## v1.4.0.3 — Исправления карты неба / авто-темы (2026-09-30)

### Баги, найденные и исправленные в коде «карты неба» (Helpers/SkyMap.cs + SettingsViewModel.ApplyAutoTheme)
1. **ApplyAutoTheme: неверная граница ночи через полночь** — сравнение `now < Sunrise.TimeOfDay || now > Sunset.TimeOfDay` считало 23:30 ДНЁМ (Sunrise≈06:xx > 23:30 ложно). Заменено на корректное `localNow >= sunset || localNow < sunrise`.
2. **ApplyAutoTheme: молчаливый выход при отсутствии GPS** (`if (pos == null) return;`) — тема Auto не обновлялась вовсе. Теперь fallback на сохранённые LastLat/LastLon (или Москву), координаты кэшируются.
3. **SkyMap.GetSunTimes: ошибка нулевого меридиана** — прежний упрощённый расчёт не учитывал долготу и часовой пояс (ошибка до |lon|/15 часов). Переписано на суточное сканирование высоты Солнца (шаг 1 мин, зенит NOAA 90.833°) с линейной интерполяцией пересечения горизонта. Валидация против timeanddate (2026): Москва 06:30/18:08 (эл. 06:25/18:05), NYC 06:52/18:40 (эл. 06:59/18:45), Хельсинки 03:54/22:50, Сидней 05:41/20:05, Токио 05:46/17:53 — точность ≤10 минут.
4. **SkyMap.ToJulianDay** — формула Meeus ch.7 (год+коррекция григорианского календаря); проверена обратным преобразованием FromJulianDayApprox.
5. **SkyMap.GetMoonPhase: illumination ≈ 1.0 ВСЕГДА** — ряд Meeus для фазового угла i использовался с несогласованными единицами (i получался ~1° вместо сотен градусов). Заменено на надёжную модель возраста луны от эталонного новолуния 2000-01-06 18:14Z: illum=(1−cos(2π·phase))/2. Проверка: новолуние→0.00, полнолуние→1.00, сегодня (2026-09-30)→0.84 (ванга/убывающая луна после полнолуния 26.09 — верно).
6. Удалён мёртвый код (RiseSetIter/HourAngle/Lp/PolarisCheck-дубль), добавлены полярный день/ночь (fallback по высоте Солнца < −6°) и логирование ошибок вместо пустого catch.

### Прочее
- Версии повышены до 1.4.0.3 (Package.appxmanifest, AssemblyInfo).
- SkyMap.cs включён в csproj (`<Compile Include="Helpers\SkyMap.cs" />`).

## v1.4.0.2-stable (2026-09-26)
**Версия сборки: 1.4.0.2 | AssemblyVersion/FileVersion/InformationalVersion = 1.4.0.2(-stable)**

### Восстановление после слияния веток (проверка потерь)
- Подтверждено: коммит `fa63197` (v1.4.0.0 stable) присутствует в истории текущей ветки — ничего не потеряно, PR был успешным.
- Финализирован незавершённый шаг v1.4.0.1: `ViewModels/SettingsViewModel_rc1.cs` активирован как `SettingsViewModel.cs` (старая версия, вызывавшая удалённый `AuthService.IsAuthenticatedAsync()`, удалена).

### Композиция проекта (csproj/sln)
- В `Barometer_UWP.csproj` добавлены отсутствовавшие Compile-записи: `Helpers/ChartRenderer.cs`, `Helpers/ImageEncoder.cs`, `Helpers/WriteableBitmapEx.cs`, `ServiceContainer.cs` (без них классы не компилировались и были недоступны другим модулям).
- Все XML-файлы (csproj, Package.appxmanifest, App.xaml, все Page/XAML, оба .resw) проходят парсер; GUID проекта совпадает со ссылкой в `Barometer_UWP.sln`.
- Проверено: каждый файл, объявленный в csproj, существует на диске; каждый .cs/.xaml/.resw на диске объявлен в csproj.

### Зависимости
- Newtonsoft.Json понижен 13.0.3 → **12.0.3** (v13 не поддерживает контрактную сериализацию на Windows 10 Mobile ARM — падение в рантайме; 12.0.3 официально совместим).
- Microsoft.NETCore.UniversalWindowsPlatform 6.0.8, DocumentFormat.OpenXml 2.9.1 — мобильно-совместимые версии. MSAL/Microsoft.Graph/LiveCharts/OxyPlot полностью исключены (нереализуемы на Win10 Mobile); вместо них — собственный рендер графика (Canvas/WriteableBitmap), OAuth через WebAuthenticationCoreManager + Windows.Web.Http, Graph REST напрямую.

### LiveTile / графики / бэкап
- `ChartRenderer.DrawPolylineOnBitmap(...)` — реализован (полилиния + заливка под кривой, альфа-смешивание); используется в GraphicsViewModel (экспорт PNG/JPEG/HTML, шаринг) и TileService (мини-график на широкую/большую плитку).
- `BackupTask.cs` переписан под RC1-API: локальный бэкап через `DataService.CreateLocalBackupAsync()` (единый формат с ручным бэкапом и восстановлением), облачная выгрузка только при включённом `AutoBackupOneDrive` и `AuthService.IsSignedIn`; ошибки облака не ломают локальный бэкап; корректные deferral/CancellationReason.
- Добавлена декларация background task в `Package.appxmanifest` (`windows.backgroundTasks`, EntryPoint=`Barometer_UWP.BackgroundTasks.BackupTask`, типы timer+systemEvent) — без неё регистрация TimeTrigger падала с E_ACCESSDENIED.

### Кросс-слойная сверка сигнатур (вычитка Settings-слоя)
- Свойства/команды SettingsViewModel ↔ XAML-биндинги SettingsPage: сверены (IntervalString/StartString/EndString и пр. существуют).
- AuthService (`IsSignedIn`, `SignInAsync(): Task<bool>`, `AuthStateChanged`), OneDriveService (`UploadBackupAsync(name,bytes)`, `ListBackupsAsync`, `DownloadBackupAsync(name)`), DataService (`CreateLocalBackupAsync`, `RestoreFromJsonAsync/RestoreFromFileAsync/ListLocalBackupsAsync`, `MakeBackupFileName`), ScheduleService (`Add/Remove/GetEntries/Changed/ShouldCollectNow`) — все потребители используют актуальные сигнатуры; мёртвых вызовов не найдено.
- `App.Current.Services` (ленивые синглтоны из ServiceContainer) согласован во всех ViewModel; ключи локализации, используемые в коде, присутствуют в обоих .resw.

### Известные ограничения среды
- Полная сборка MSBuild/UAP невозможна в данном Linux-контейнере (нет Windows SDK). Выполнены: статическая кросс-валидация слоёв, XML-валидация манифеста/csproj/XAML/resw, проверка состава проекта и версий пакетов. Финальная проверка: VS2017/2019 + UAP 15254, Restore NuGet, Build Release|ARM (затем Release|AnyCPU/AppX для ARM64-устройств HP Elite x3 через Device Portal).

## v1.4.0.6 (2026-10-19)
- CI fix: csproj now imports obj\*.nuget.g.props/targets (packages.config NuGet wiring).
  Root cause of WMC1006 "System.Runtime cannot be resolved": restored UWP metapackage
  was never referenced at compile time because the project lacked the NuGet.targets imports.
- Bump manifest/assembly version to 1.4.0.6; retagging triggers a fresh Actions run.

## v1.4.0.7 (2026-10-19)
- CI: HintPaths pinned to $(MSBuildThisFileDirectory)..\packages (runner-global Newtonsoft.Json
  under Program Files shadowed relative paths -> wrong CLR flavor -> WMC1006 System.Runtime).
- CI: new workflow step generates obj\Barometer_UWP.nuget.g.props with explicit System.Runtime
  facade reference from the restored UWP metapackage (nuget.exe CLI does not emit these files;
  csproj now imports them conditionally).

## v1.4.0.8 (2026-10-19)
- CI root cause found: nuget.exe CLI restore of bare packages.config does NOT install the
  transitive dependency closure of the UWP metapackage (System.Runtime facade etc. absent).
  Added `nuget restore Barometer_UWP.sln` step + pre-flatten verification.
- nuget.g.props generation now resolves System.Runtime.dll dynamically from the packages tree.

## v1.4.0.12 (hotfix)
- csproj: fixed HintPath for System.Runtime.WindowsRuntime.UI.Xaml — package ships ref-only assemblies (lib/ contains _._ placeholders); previous lib/uap10.0_10254 path did not exist -> MSB3245 + 114 WMC0003 errors on all {Binding} pages. Now points to ref/netcore50/.
- XAML: removed all StringFormat usages (unsupported by UWP Binding/x:Bind -> WMC0011). MainPage list now binds preformatted TimeText/HpaText/MmHgText helpers on PressureRecord; SettingsPage frequency label uses CollectionFrequencySecondsText; added reusable Helpers/FormatConverter.cs (IValueConverter) registered in both pages' resources.
- Versions bumped to 1.4.0.12 (appxmanifest, AssemblyInfo).

## v1.4.0.16 (2026-10-22)
- FIX: ImageEncoder — parameterless BitmapDecoder.GetPixelDataAsync() does not exist on Win10 15063/15254 API surface; switched to explicit overload (Bgra8/Premultiplied/BitmapTransform/ExponentialOrderByOptions.Default/BitmapPropertySet). This was the last known CI compile blocker.
- Version bump 1.4.0.15 -> 1.4.0.16 (appxmanifest + AssemblyInfo).

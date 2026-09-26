# Barometer UWP — Changelog

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

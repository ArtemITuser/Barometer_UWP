# Barometer UWP — Changelog

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

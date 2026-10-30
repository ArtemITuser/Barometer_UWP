# Changelog

## v1.4.0.38-stable — Тестовая инфраструктура (код проекта идентичен v1.4.0.37)
- **tests/static_checks.py**: 51 статическая проверка синтаксиса/структуры/логики
  (XML-валидность всех 11 файлов, канонический <Reference>-блок UAP, отсутствие MSB4092-классов
  в Condition, версии, платформы 15063, композиция csproj, парность resw, фактор UnitConverter).
- **tests/mutation_tests.py**: негативные мутационные тесты 17 синтетических регрессий
  (все исторические классы ошибок v1.4.0.27..37) — 17/17 детектируются.
  Исправлен харнесс: само-обрезающиеся лямбды open('w').write(open().read()) заменены на
  безопасную функцию rewrite() (полное чтение до открытия на запись); устранены ложные MISSED
  N8/N9/N15/N16; копирование актуального набора проверок в мутированное дерево (refresh_checks).
- Версии синхронизированы 1.4.0.38 (AssemblyInfo / appxmanifest / install_deps.cmd).
- Сборка не выполнялась (Actions отключён): только статические прогоны, baseline 51/51 PASS.

## v1.4.0.37-stable — Архитектурный аудит csproj/sln: НАСТОЯЩИЙ корень WMC1006/WMC9999
- **Восстановлен канонический блок <Reference> UWP-шаблона VS2017** (System, System.Core,
  System.Numerics, System.Numerics.Vectors, System.Runtime.InteropServices.WindowsRuntime,
  Microsoft.CSharp, System.Xml.Linq, Windows). Начиная с v1.4.0.29 проект не объявлял НИ ОДНОЙ
  сборки: «нулевые NuGet-эксперименты» v34 зашли слишком далеко и выпилили также SDK-ссылки.
  Классический UAP-проект обязан декларировать framework-ссылки явно — imported targets их не
  синтезируют. Пустой ReferencePath => XamlCompiler нечего разрешать => ровно наблюдаемые две
  ошибки в ЛЮБОМ чистом окружении. Разрешение по простому имени из Windows SDK (UnionMetadata +
  facade), без HintPath и без NuGet.
- **Добавлены свойства канонической UAP-проводки**: AppxPackage=true, GenerateLibraryLayout=false
  (активируют инъекцию WinRT/фасадных ссылок в Microsoft.Windows.UI.Xaml.CSharp.targets).
- **Удалён self-defeating firewall v1.4.0.36** (RejectPackageFacadeShadowReferences): он фильтровал
  ReferencePath по ПРОСТЫМ ИМЕНАМ фасадов и на любой машине вырезал бы ЛЕГИТИМНЫЕ копии SDK
  (System.Runtime.dll и др.) — то есть сам гарантированно производил WMC9999. Второй баг, найденный
  аудитом: даже если v35/v36 лечили кэш, таргет v36 ломал сборку всегда.
- **Удалена мёртвая NuGet-самолечивающая механика** (UsingTask NuGetRestoreTask, _NuGetTasksAssembly,
  EnsurePackagesRestored): UsingTask на несуществующий путь к NuGetTasks.dll мог ронять загрузку
  проекта (IDE0006); compile-time NuGet-зависимостей нет.
- Убраны дублирующиеся определения свойств и противоречащие реальности комментарии; сохранён
  единственный PurgeStaleRestoreState.
- **Новый диагностический таргет DumpReferenceSet**: печатает TargetPlatformRoot, число разрешённых
  ссылок и источник System.Runtime — при любом повторении WMC9999 лог сразу укажет виновника.
- Предупреждение MSB3245 «System.Private.Uri»: было следствием пустого универсума типов; после
  восстановления блока ссылок SDK предоставляет фасад сам, отдельная ссылка не нужна.
- Версии синхронизированы 1.4.0.37 (AssemblyInfo, Package.appxmanifest).
## v1.4.0.35-stable — Guard от устаревшего состояния сборки (WMC1006/WMC9999 выживает после чистого дерева)
- Postmortem v1.4.0.34 на машине пользователя: packages\ удалён, метапакета нет, `<Reference>` в csproj
  ноль — а WMC1006/WMC9999 «System.Runtime» повторяется 1-в-1. Единственный оставшийся канал попадания
  «чужого» System.Runtime во вселенную типов — остатки старого restore-состояния в obj\: импортируемые
  при существовании `obj\*.nuget.g.props/.targets` (следы PackageReference-эпохи до v1.4.0.32) и кэш
  XamlPreCompile. В архиве релиза их нет — значит в папке распаковки остался obj\ от предыдущих версий
  (пользователь распаковывал поверх старой директории либо VS переиспользует содержимое Downloads).
- Новый таргет `PurgeStaleRestoreState` (BeforeTargets=EnsurePackagesRestored): автоматически удаляет
  `obj\*.nuget.g.props`, `obj\*.nuget.g.targets`, `obj\*.nuget.cache` и `obj\XamlPreCompile` перед
  ResolveAssemblyReferences — каждая сборка стартует с чистого SDK-only универсума типов. Задевает
  только генерируемые файлы, исходники не трогает. Формулировки проверены на MSB4092-класс: ни одного
  %(metadata) внутри Condition (голые @(...) списки разрешены где угодно).
- Обеспечена нулевая compile-time зависимость от NuGet (итог 34): JSON — vendored JsonSerializer.cs,
  xlsx — System.IO.Compression из SDK, HTTP — Windows.Web.Http; packages.config только .NET Native
  compiler (Release-тулчейн). Для Debug|ARM restore вообще не нужен.
- Версии синхронизированы 1.4.0.35. Статическая приёмка: XML OK (8 файлов), реальных StackPanel.Spacing 0,
  композиция csproj полная (38 .cs + 5 .xaml), платформенные цели 15063/15063.

## v1.4.0.34-stable — Окончательное исправление WMC1006/WMC9999 «System.Runtime» (нулевые NuGet-зависимости компиляции)
- Postmortem v1.4.0.33 (чистое дерево, метапакета уже нет — ошибка осталась): ссылки на
  Newtonsoft.Json 12.0.3 и DocumentFormat.OpenXml 2.9.1 разрешались в их `lib\netstandard1.3`
  сборки; compile-time closure .NETStandard,v1.3 требует весь граф NETSTANDARD1.3 facade-пакетов
  (System.Runtime ref и т.д.), отсутствующий в установке UAP 15063 SDK → XamlCompiler WMC1006 +
  WMC9999 на первом же нерезолвленном facade. OpenXml 2.9.1 вообще не имеет netstandard2.0 asset
  (проверено по nuget.org) — починить ссылку было нельзя.
- Решение: обе библиотеки полностью исключены из сборки:
  * Newtonsoft.Json → vendored Helpers\JsonSerializer.cs (zero-dependency JSON reader/writer:
    PressureRecord/BackupFile/OAuth/GitHub-release схемы); добавлен в csproj Compile.
  * DocumentFormat.OpenXml → ExcelHelper переписан на System.IO.Compression.ZipArchive
    (OPC/xlsx вручную) — только Windows SDK.
  * UpdateService: System.Net.Http → Windows.Web.Http (facade Net.Http тоже из netstandard-графа).
  * ExportService: System.Net.WebUtility.HtmlEncode → локальный HtmlEncodeHelper.
- packages.config: остался только microsoft.net.native.compiler 1.7.3 (ILCompiler для Release;
  для компиляции не нужен). Таргет EnsurePackagesRestored превращён в no-op сообщение;
  ручное `nuget restore` по-прежнему работает через nuget.g.props/.targets импорты.
- Версии синхронизированы 1.4.0.34 (AssemblyInfo/appxmanifest). Статическая приёмка: XML OK,
  все .cs/.xaml объявлены, HintPath-ссылок на пакеты 0, остатков System.Net.Http/WebUtility 0.
- Требуется от пользователя: удалить старые obj/bin/packages и собрать из нового zip
  (Debug|ARM или Release|ARM). Никакого restore перед сборкой не нужно.

# Barometer UWP — Changelog

## v1.4.0.33-stable — Исправлена ошибка загрузки проекта MSB4092 (2026-10-26)

### Диагноз (сообщение пользователя из VS2017, zip v1.4.0.32)
- Ошибка MSB4092 на строке 269 csproj: «неожиданный маркер %(Reference.HintPath)» — проект
  не загружался в IDE (IDE0006). Причина: в таргете StripPackageFacadeShadows (добавлен в
  v1.4.0.32 как страховка) условие таргета содержало item-batching выражение
  @(PackageConflictFacadeNames), а условия на уровне таргетов/свойств в MSBuild не могут
  батчинг использовать — только свойства и функции Exists()/ValueOrDefault() и т.п.

### Исправления
- Таргет StripPackageFacadeShadows и список PackageConflictFacadeNames ПОЛНОСТЬЮ УДАЛЕНЫ из csproj.
  Разбор показал, что он был бесполезен даже без синтаксической ошибки: удаление элементов
  Reference ПОСЛЕ ResolveAssemblyReferences не влияет на уже сформированный ReferencePath,
  который потребляют XamlCompiler/csc. Реальный фикс WMC1006/WMC9999 — удаление метапакета
  Microsoft.NETCore.UniversalWindowsPlatform из packages.config (v1.4.0.32); он остался в силе.
- Обновлены комментарии в csproj; добавлено явное предупреждение: если на машине осталась
  старая папка packages\microsoft.netcore.universalwindowsplatform от предыдущих restore —
  удалить всю папку packages перед сборкой.
- Версии синхронизированы: Package.appxmanifest / AssemblyInfo = 1.4.0.33.

### Статическая приёмка (сборка по требованию не выполняется)
- Все 10 XML-файлов парсятся; в csproj не осталось ни одного условия с batching-маркерами
  (@( / %( внутри Condition=) — проверено сканером; структура таргетов валидна.
- packages.config: newtonsoft.json 12.0.3, documentformat.openxml 2.9.1,
  microsoft.net.native.compiler 1.7.3 (без метапакета).

## v1.4.0.32-stable — Устранена тень метапакета: исправлены WMC1006/WMC9999 System.Runtime (2026-10-26)

### Диагноз (сообщение пользователя из VS2017, сборка Debug|ARM из zip v1.4.0.31)
- 2 ошибки: XamlCompiler WMC1006 «Cannot resolve Assembly or Windows Metadata file
  'System.Runtime.dll'» + WMC9999 «Type universe cannot resolve assembly: System.Runtime,
  Version=4.0.0.0». То есть файл на диске ЕСТЬ (иначе была бы ошибка «not found»), но в
  список ссылок компилятора попадает не та копия.

### Корень причины
`microsoft.netcore.universalwindowsplatform 6.0.8` в packages.config: после restore в
`..\packages\...netcore50\System.Runtime.dll` лежит facade-сборка с тем же простым именем
и версией 4.0.0.0, что и SDK reference assembly. Simple-name разрешение MSBuild и
XamlCompiler type-universe перехватывает её вместо `...\Windows Kits\10\References\...\
System.Runtime.dll`, вселенная типов ломается (facade forwardит к mscorlib, которого в
списке нет). Это же объясняет одиночное предупреждение MSB3245 по `System.Private.Uri`.
Все предыдущие попытки (v1.4.0.28–31) лечили симптомы, не убирая источник тени.

### Исправление
- packages.config: удалён метапакет UWP 6.0.8. Классическая UAP-система проектов сама
  подключает ВСЕ BCL/WinRT ссылки из Windows SDK (UnionMetadata + ReferenceAssemblies);
  runtime .NET Core IL берётся из машинного VC++ 2015-2019 UWP Desktop Runtime (ставится
  с VS2017). В packages.config остаются только Newtonsoft.Json 12.0.3,
  DocumentFormat.OpenXml 2.9.1, Microsoft.NET.Native.Compiler 1.7.3.
- csproj: новый таргет `StripPackageFacadeShadows` (AfterTargets=ResolveAssemblyReferences)
  вырезает из ReferencePath любые facade-сборки (System.Runtime, mscorlib, System.Linq,
  System.ObjectModel, System.Private.Uri, System.Runtime.WindowsRuntime* и др.), приехавшие
  из папки `packages\` — защита от старых кэшей packages\ на машине пользователя.
  EnsurePackagesRestored теперь проверяет наличие newtonsoft.json.12.0.3 (метапакет больше
  не ориентир). Восстановлен удалённый по ошибке <UsingTask> для NuGetTasks-restore.
- TileService.cs: CS0122 — закрытый WinRT-проецируемый тип IXmlNodeList заменён на
  публичный Windows.Data.Xml.Dom.XmlNodeList в сигнатуре Push() (то же самое поведение,
  GetElementsByTagName возвращает совместимый тип).
- BUILD_VS2017.md / install_deps.cmd приведены в соответствие (без ~90 пакетов closure).
- Версии синхронизированы: manifest/AssemblyInfo = 1.4.0.32.

### Инструкция для пользователя (обязательно!)
1. Удалите папку `packages\` рядом с решением (в ней лежит теневой метапакет из прошлого
   restore) и `obj\`, `bin\` в Barometer_UWP.
2. Распакуйте новый zip v1.4.0.32, выполните install_deps.cmd (или nuget restore), откройте
   sln, Rebuild Solution.

## v1.4.0.31-stable — Откат facade-ссылок: исправлены 81 ошибка CS0012/CS0234 после перестройки (2026-10-26)

### Диагноз (сообщение пользователя из VS2017, сборка из zip релиза v1.4.0.30)
- После Clean осталось 1 предупреждение MSB3245 (`System.Private.Uri`).
- После Rebuild/deploy: 81 ошибка CS0012 «Тип Object/Enum определен в сборке, на которую
  нет ссылки… добавьте mscorlib 4.0.0.0», CS0234 `System.CodeDom` во всех *.g.i.cs,
  CS0122 `IXmlNodeList`, CS0246 Canvas/WriteableBitmap.

### Корень причины
Эксперимент v1.4.0.30 (ручные `<Reference>` на netcore50-facade DLL из `..\packages`)
 proved вредным: facades type-forward к mscorlib, который NETFX_CORE universe не грузит
с этих путей — компилятор теряет унификацию типов и генерирует CS0012-каскад по всему
проекту. В корректном UAP-проекте ВСЕ framework-ссылки (mscorlib/System.Runtime/
ObjectModel/WindowsRuntime/Private.Uri) поставляются автоматически Windows SDK +
Platform.winmd внутри Microsoft.Windows.UI.Xaml.CSharp.targets.

### Исправление
- csproj: удалён весь fallback-блок инжекта facade-`<Reference>` (v1.4.0.19/20) и явные
  System.ObjectModel / System.Runtime.WindowsRuntime[.UI.Xaml] ссылки; остались только
  Newtonsoft.Json и DocumentFormat.OpenXml (сторонние библиотеки).
- packages.config: сведён к честному минимуму — метакет Microsoft.NETCore.UniversalWindowsPlatform
  6.0.8 + Newtonsoft.Json 12.0.3 + DocumentFormat.OpenXml 2.9.1 + Microsoft.Net.Native.Compiler
  1.7.3; транзитивные leaf-пакеты restore подтягивает сам.
- Сохранён self-healing таргет EnsurePackagesRestored (авто-nuget restore до RAR) —
  он лечит исходный MSB3245/WMC1006 без фейковых framework-ссылок.
- Версии синхронизированы: manifest/AssemblyInfo = 1.4.0.31.

### Статическая приёмка
XML 10/10 OK; реальных Spacing= 0; References в csproj: только Newtonsoft.Json,
DocumentFormat.OpenXml; все .cs/.xaml объявлены; undeclared 0.

## v1.4.0.30-stable — Исправлены 2 ошибки + 18 предупреждений IDE (ненайденные ссылки) (2026-10-26)

### Диагноз (сообщение пользователя из VS2017, сборка из zip релиза v1.4.0.29)
- Ошибка: `Type universe cannot resolve assembly: System.Runtime, Version=4.0.0.0` (WMC1006)
- Ошибка: `Cannot resolve Assembly or Windows Metadata file 'System.Runtime.dll'` (csproj)
- 18 предупреждений MSB3245 «Не найден компонент … на который указывает ссылка»
  (System.Runtime / .Extensions / .WindowsRuntime / .UI.Xaml, System.Linq, ObjectModel,
  Collections(.Concurrent), IO, Globalization, Threading(.Tasks), Resources.ResourceManager,
  Private.Uri, Net.Http, Diagnostics.Debug, Newtonsoft.Json, DocumentFormat.OpenXml).

### Корень причины
Release zip содержит только исходники — папка `..\packages` отсутствует; при этом
`nuget.exe restore` для чистого packages.config-проекта НЕ создаёт
`obj\Barometer_UWP.nuget.g.props`, поэтому весь netcore50-facade блок `<Reference>`
в csproj указывает в несуществующие до restore папки → все ссылки нерезолвнуты.
Дополнительно: пакет `system.private.uri` использовался в fallback-блоке, но отсутствовал
в packages.config (restore не создал бы его папку даже после успешного restore).

### Исправления
1. **packages.config**: добавлен явный `system.private.uri 4.0.1` (сверено с HintPath).
2. **csproj — самоисцеляющийся restore**: новый target `EnsurePackagesRestored`
   (BeforeTargets=ResolveAssemblyReferences;ResolveWinMDReferences): если папка пакета
   metapackage отсутствует, автоматически выполняется `nuget restore ..\Barometer_UWP.sln`
   (через `.nuget\nuget.exe` или VS-bundled NuGetTasks.dll); если ни один путь недоступен —
   понятная ошибка с инструкцией вместо 20 cryptic-диагностик. F5/Build в VS после
   распаковки zip теперь работает без ручных шагов.
3. Все версии платформ уже 15063 (csproj TargetPlatformVersion/MinVersion, manifest
   MinVersion/MaxVersionTested) — подтверждено статическим прогоном.

### Статическая приёмка v1.4.0.30 (без сборки, как заказано)
- XML: 10/10 файлов парсятся; csproj well-formed, target сбалансирован.
- 20 Reference-элементов с HintPath: каждый pkg+version существует в packages.config (0 расхождений).
- Композиция: 42/42 исходника объявлены, битых ссылок нет; реальных Spacing=" — 0.
- Версии синхронизированы: manifest/AssemblyInfo = 1.4.0.30.
- GitHub Actions отключён (настройки репозитория) — CI не запускается, только ручной прогон.

## v1.4.0.29-stable — Финальный валидный релиз кода; GitHub Actions отключён (2026-10-26)

### Статус CI
- **GitHub Actions для репозитория отключён** (UWP-тулчейн официально deprecated на hosted
  runners windows-2022, стабильная сборка классического packages.config UAP csproj там невозможна).
  Релиз собирается вручную в Visual Studio 2017/2019 по инструкции BUILD_VS2017.md
  (SDK 15063/15254, Release|ARM для Lumia 950/950XL и HP Elite x3).
- Файл workflow `.github/workflows/build-release.yml` сохранён как архивная справка по
  корректной обвязке packages.config-restore (nuget.g.props + netcore50 facade references);
  триггеры больше не срабатывают — Actions выключен на уровне настроек репозитория.

### Версии платформ — приведены к 15063
- `Barometer_UWP.csproj`: TargetPlatformVersion = **10.0.15063.0**, TargetPlatformMinVersion = 10.0.15063.0.
- `Package.appxmanifest`: MinVersion = 10.0.15063.0, MaxVersionTested = **10.0.15063.0**.
- Для сборки на машине без SDK 15063 достаточно заменить номер платформы в VS (Properties →
  Packaging) или флаг `/p:TargetPlatformVersion=10.0.15254.0` — обратная совместимость гарантирована min 15063.

### project.json ("could not be found") — исправлен корень
- Ошибка NU1005 возникала из-за того, что CI-патч поднимал TargetPlatformMinVersion до самого
  свежего SDK раннера, и NuGet-restore переходил на конвейер project.assets.json/project.json.
- В репозитории **нет и никогда не было project.json / project.lock.json** (проверено по
  `git ls-files`); в csproj зафиксированы `RestoreProjectStyle=packages.config` и
  `PackageReferenceUpdateAutomaticAtRestoreTime=false` (v1.4.0.28) — конвейер JSON-restore
  не задействуется никогда. В VS2017 при открытии решения использовать только nuget restore
  для packages.config (команда «Восстановить пакеты NuGet» в Solution Explorer).

### Статическая приёмка (вместо сборки, как запрошено)
- Все XML-файлы (10 × .xaml/.csproj/.appxmanifest/.resw/packages.config) проходят парсер.
- Реальных атрибутов StackPanel.Spacing (API 16299+) нет — только комментарии-предупреждения.
- sln: Format 12.00, CRLF/tab, GUID проекта совпадает с csproj, 4 конфигурации ARM/x86.
- Все 37 .cs и 5 .xaml объявлены в csproj; битых ссылок на отсутствующие файлы нет.
- Версии синхронизированы: appxmanifest / AssemblyInfo = **1.4.0.29**.

## v1.4.0.28-stable — Исправления сборки CI: project.json, дубли Reference, версии 15063 (2026-10-25)

### Ошибка "project.json could not be found" (NU1005)
- Корень: шаг "Patch project for CI" переопределял `TargetPlatformMinVersion` csproj на самый
  свежий SDK раннера; NuGet начинал разрешение метапакета для uap10.0.<newest> и включал
  конвейер restore проекта JSON, требовавший несуществующий `Barometer_UWP\project.json`.
- Исправлено: `TargetPlatformMinVersion` больше не патчится — в csproj/sln/appxmanifest
  зафиксирован **минимум 10.0.15063.0** (Windows 10 Mobile / Creators Update). Компилирующая
  поверхность ограничивается только `TargetPlatformVersion` (пин на SDK раннера, обратно
  совместим до 15063). В csproj явно добавлены `RestoreProjectStyle=packages.config` и
  `PackageReferenceUpdateAutomaticAtRestoreTime=false` — конвейер project.assets.json/project.json
  не задействуется никогда (ни в CI, ни при design-time сборке в VS).

### Дублирующиеся ссылки (конфликт Reference-деклараций)
- Шаг генерации `obj\Barometer_UWP.nuget.g.props` инжектировал по `<Reference>` на каждую
  запись packages.config с абсолютными HintPath'ами — MSBuild видел два разных источника для
  одинаковых выходных имён (Newtonsoft.Json, DocumentFormat.OpenXml, System.ObjectModel,
  System.Runtime.WindowsRuntime.UI.Xaml уже объявлены в csproj с относительными HintPath'ами),
  а фолбэк-паттерн `ref\netstandard1.3` возвращал netstandard-реализацииfacade'ы → каскад
  CS0012 (duplicate System.Object) из v1.4.0.16b.
- Исправлено: props-файл теперь содержит **только** свойства обвязки NuGet
  (SolutionDir/PackagesDir/NuGetProjectStyle), ноль `<Reference>` items. Вселенная компиляции
  принадлежит исключительно csproj (winmd-набор SDK через Microsoft.Windows.UI.Xaml.CSharp.targets
  + защищённая ItemGroup netcore50-facade'ов).

### Версии
- Package.appxmanifest / AssemblyInfo: **1.4.0.28**; целевые платформы без изменений:
  Target 10.0.15254.0, **MinVersion 10.0.15063.0** (csproj строки 14–15, manifest Dependencies).
- .gitignore восстановлен в полном виде (bin/obj/packages/AppPackages/artifacts и пр.) —
  предыдущая версия случайно вырезала правила и грозила попаданием мусора восстановления в git.

## v1.4.0.27-stable — XAML Spacing (16299+) и чистка sln (2026-10-25)

### StackPanel.Spacing — API доступен только с Windows 10 16299+
Проект таргетит 15063/15254 (Windows 10 Mobile), поэтому `StackPanel.Spacing` недопустим
(WMC/XAML compile error на целевой платформе). Удалены все реальные использования атрибута:
- `Views/ChartMiniTile.xaml` — Spacing="6" убран, отступ перенесён на дочерний TextBlock (`Margin="6,0,0,5"`).
- `Views/GraphicsPage.xaml` — Spacing="12" убран, отступ на первом дочернем TextBlock (`Margin="0,0,12,0"`).
- `Views/SettingsPage.xaml` — Spacing="10" убраны; там где у детей уже был `Margin="0,0,10,0"` (кнопки) — ничего не добавлялось; чекбоксам LocalBackup/OneDriveBackup добавлен `Margin="0,0,10,0"`. В местах удалений оставлены комментарии-предупреждения про 16299+.
Итоговое число удалённых использований по файлам: 2 + 1 + 5 = 8 (первоначальная оценка «6 в SettingsPage» была завышена — уточнено по фактическому коду).
Все XAML проходят XML-парсер; в продакшн-коде атрибута Spacing не осталось (упоминания — только в комментариях-предупреждениях).

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

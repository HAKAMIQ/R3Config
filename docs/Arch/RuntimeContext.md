# بيئة التشغيل والتوافق (المجال الخامس)

R3Config له خمسة مجالات دائمة مستقلة: إعدادات اللعبة، الباتشات الرسمية، Canary/H-Canary، Artemis/NCL، وبيئة التشغيل
والتوافق. المجال الخامس **مزود أدلة فقط**: يقرأ الجهاز وتثبيت RPCS3 والنسخة الحالية واللعبة المحددة، ولا يملك سياسة
تعديل أو تحويل أو توصية. المجالات 1-4 تستهلك أدلته وتحتفظ كل منها بقواعدها.

## المسار

```text
HwProbe (المالك الوحيد لفحص العتاد) ─┐
Rpcs3InstallDescriptor (فحص التثبيت) ─┤
Rpcs3BuildIdentity (لافتة السجل)      ─┼─> RuntimeContextBuilder (Core/Svc/Runtime, قراءة فقط)
Rpcs3GameDescriptor + GameSettingsModel┤        │
Rpcs3BuildCatalog (كتالوج مشحون)      ─┘        v
                                       RuntimeContextSnapshot -> RuntimeVm -> Ctrl/Rtm.xaml (تبويب Runtime)
```

- `RuntimeContextUseCase` (App/Application) يركب اللقطة فقط ولا يفحص شيئًا. `MainVm.RefreshRuntimeContext` يعيد بناءها عند
  اختيار لعبة أو اكتمال التحليل، و`RuntimeVm.SetLang` يجدد النصوص عند تبديل اللغة.
- لا يجوز لأي مجال آخر إنشاء ماسح للعتاد أو للنسخة أو للعبة؛ `RuntimeContextT` يفشل إذا ظهرت علامات فحص العتاد خارج `HwProbe`.

## حالات الدليل

`Exact` و`Known` و`Partial` و`Unknown` و`Unavailable` و`Conflict` و`Unsupported` و`NotApplicable`. المجهول لا يتحول إلى نعم/مدعوم/موصى
به أبدًا. الحقل المعروف بالكامل لا تظهر عليه شارة حالة، وأي حالة أضعف تظهر.

## العتاد

`HwProbe` يعيد `HwInfo` مع: المعالج (الاسم والمصنع من `VendorIdentifier`)، كل بطاقة رسوميات (الاسم و`VEN`/`DEV` من معرف PCI
والمصنع منه، مع إزالة تكرار المخرجات)، الذاكرة، إصدار Windows ومعماريته ومعمارية العملية. مصنع البطاقة من معرف PCI
حالة `Known`؛ المشتق من الاسم فقط حالة `Partial`. عند وجود أكثر من بطاقة لا يُفترض أيها يستخدمه RPCS3 (`GpuSelection` مجهول).

الشرط الموثق للعتاد (`NVIDIA`/`AMD`/`Intel` في ملاحظات الويكي) يقيّمه `NoteScope.HardwareApplies` على مصنعي البطاقات فقط:
تطابق وحيد = ينطبق، غياب المصنع = لا ينطبق، لا دليل أو مصنع مجهول أو مصنعان بينهما المطلوب = غير محسوم (يفشل مغلقًا ولا يُعرض).
الكلمات العامة (`GPU`/`CPU`/`Vulkan`/`OpenGL`) ليست شرط مصنع وتنطبق على الجميع. لا شروط عتاد جديدة تُخترع ولا توصية أداء.

## سياق المعالج (CPU Runtime Context)

`HwProbe.Probe()` يملأ `HwInfo.Cpu` (`HostCpuInfo`) من قراءات خام غير محكومة (`HostCpuRawFacts`) يجمعها `HwProbe.Cpu.cs`
ويحولها `HostCpuAnalyzer` (Core/Svc/Hw، بلا فحص) بالقواعد نفسها التي يطبقها RPCS3 في `sysinfo.cpp` و`Thread.cpp`.
الأساس الموثق والمراجع في [تدقيق سلوك RPCS3 تجاه المعالج](Rpcs3CpuAudit.md)؛ الحقائق المشتقة من المصدر في
`Rpcs3CpuSemantics` (يحمل `FrozenHead`).

- القراءة: CPUID عبر `X86Base.CpuId` (عملية x64 أصلية فقط)، `GetEnabledXStateFeatures` (دليل مضيف مستقل يؤدي غرض `XGETBV(0)` دون ادعاء تكافؤ حرفي)، `GetNativeSystemInfo`
  (نفس عدد الخيوط الذي يقرؤه RPCS3)، `GetLogicalProcessorInformationEx(RelationProcessorCore)`، و`IsProcessorFeaturePresent`
  لـARM64. بلا PowerShell ولا WMI ولا شبكة.
- الحقول: معمارية المعالج (`x64`/`ARM64`، وغيرها `Unsupported`)، ميزات RPCS3 المتتبعة وليست القائمة الكاملة (لكل ميزة `Present/Absent/Unknown`
  ومصدر القاعدة)، الأنوية الفعلية، SMT، الأنوية الهجينة (فئات الكفاءة كما يعيدها Windows دون تسمية P/E)، عدد خيوط RPCS3 وعدد
  مجموعات المعالجة، وتصنيف مجدول RPCS3 (`generic` أو `amd_ccx` أو `intel_ht`) مع أثره على قناع الأنوية. التصنيف حكم RPCS3 من brand ووجود SMT وليس وصفا للطوبولوجيا، فيجتمع مع `CpuHybrid` دون تعارض.
- الحقائق تظهر في مجموعة `Hardware` تحت Details: `CpuArchitecture`, `CpuPhysicalCores`, `CpuSmt`, `CpuHybrid`, `CpuNativeThreads`,
  `CpuFeatures`, `Rpcs3SchedulerLayout`؛ وفي مجموعة `Build`: `BuildArchitectureMatch` و`Rpcs3SpuDecoders` (ما يعرفه المصدر دون LLVM) و`Rpcs3SpuDecodersLlvm` (`Interpreter (dynamic)` و`Recompiler (LLVM)`، حالتها `Unknown` لأن `LLVM_AVAILABLE` للبناء المثبت غير مثبت) و`Rpcs3AccurateDfmaUi`
  وهي تعتمد على المعمارية التي سجلها RPCS3 نفسه ولا تُستبدل بمعمارية الجهاز.
- الفشل المغلق: خانة لا تُقرأ تصبح `Unknown` ولا تتحول إلى `Absent`؛ عملية x64 على Windows ARM64 تعيد `Unknown` لكل الميزات
  (المعالج المحاكى ليس المضيف)؛ فشل استعلام الطوبولوجيا يترك تخطيط Intel `Unknown`؛ مجموعات معالجة متعددة تجعل عدد الخيوط `Partial`؛
  معمارية RPCS3 غير المسجلة في السجل لا تعطي ادعاء عن المفككات.
- ما لا يُقرأ (`NOT_DETECTABLE`): اسم LLVM CPU الذي يشتق منه مترجم LLVM مساراته، `sve_length()`، معايرة TSC، `LLVM_AVAILABLE`، وجداول
  أقنعة AMD/Intel (يعرض R3Config `NoChange` حيث يثبت المصدر أن القناع لا يتضيق و`LayoutSpecific` حيث توجد قاعدة دون تنفيذها).
- لا توصية: لا score ولا تصنيف أداء ولا إعداد موصى به؛ الميزات والتخطيط حقائق عرض فقط، و`HwTier` القديم لا يدخل هذه الحقائق.
  كذلك لا تُعرض لوائح CPU المجتمعية من `rpcs3-knowledge.json` كحقيقة Runtime أو كتقييم للجهاز؛ وجود اسم مطابق في المصنف
  لا يثبت تردد المضيف ولا حالته Stock/Overclocked ولا أداء لعبة.
- الاختبار: `HostCpuT` (fixtures اصطناعية x64 Intel/AMD وهجين وARM64 وبيانات ناقصة، واختبار تكامل للسياق والعربية)، و`HostCpuT.RunRealHost`
  اختبار مفصول يقرأ الجهاز ويتحقق من invariants فقط. `HwProbe` بأي قراءة محقونة لا يقرأ المعالج الحقيقي.

## هوية RPCS3 واللعبة

النسخة الحالية تأتي من `Rpcs3BuildIdentity` وحده: `Exact` تعرض `version-build` والـcommit والفرع والمرحلة كحقول منفصلة؛ `Partial` لا
يُصنع لها بناء مركب؛ `Conflict` لا يُختار له فائز. `DataRoot` هو ما أثبته فحص التثبيت فقط ولا يُفترض أنه مجلد الملف التنفيذي.
هوية اللعبة `Exact` عند إثبات Serial والإصدار المثبت (APP_VER) وPPU hash، وإلا `Partial`؛ والنسخة (digital/disc) من بادئة
الـSerial فقط، وغير المعروفة `Unknown`.

### بصمة PPU التنفيذية (نسخ)

RPCS3 يكتب `PPU executable hash` في السجل فقط، فيعرض تبويب Runtime في مجموعة اللعبة صفًا لكل بصمة موثوقة مع زر نسخ
(`Runtime.PpuHash.Copy`). البصمات الموثوقة هي `PpuList` مع ربط H-Canary المحفوظ (`HCanPpuList`)، وتصل عبر
`RuntimeContextSnapshot.PpuHashes` (`RuntimePpuHash`): القيمة LTR، والمصدر مترجم، ووقت الدليل إن ثبت. المصدر لا يدعي أكثر مما يثبته
الوصف: سجل RPCS3 أو الكاش معًا (`Rpcs3Runtime`) لأن الفحص يدمجهما، و`HCanBinding`، و`Metadata` فقط عند دليل بيانات اللعبة الفعلي (`HashEvi.Meta`)، و`Analysis` لبصمة فحص استبدل الربط
نوع دليلها ووقته فلا يعرض لها وقت، و«غير مسجل» (`RuntimePpuHashSource.Unknown`) لبصمة لا يسجل وصفها نوع دليل (`HashEvi.None`). عدة بصمات تعرض كلها ولا تختار واحدة. بصمة الكاش غير المثبتة للملف التنفيذي المثبت (`Hashes` خارج
`PpuList`) لا تعرض ولا تنسخ ولا تدخل حقيقة `GamePpuHash` ولا هوية `Exact`؛ عند غياب بصمة موثوقة يظهر إرشاد `Setup.Warning.HashUnknown`.
النسخ عبر `RuntimeVm.CopyText` القابل للاستبدال في الاختبار، والحافظة المشغولة لا تسقط التطبيق (`UiDlg.TryCopyText`).
الاختبار: `RuntimePpuHashT` (من `RuntimeContextT`، والعلم `--runtime-context-only`) و`RuntimeViewUiT` داخل UiGate.

### الكيبورد وقارئ الشاشة في تبويب Runtime

قالب FluentTabControl بلا جزء PART_SelectedContentHost، فلا يعرض TabItem محتوى التبويب الداخلي المحدد لـUI Automation.
لذلك يضيف `AccessibleUserControlPeer` محتوى التبويب الداخلي المحدد بعد رؤوس التبويبات (مثل `MainAutomationPeer` للتبويبات
الرئيسية)، ويعيد `RuntimeView` قراءة أبناء الـpeer بعد تبديل التبويب. رؤوس التبويبات الداخلية محطة Tab واحدة (التبويب المحدد)،
والأسهم تنتقل بينها كما في TabPanel؛ وإلا يحدد Tab التبويب التالي ولا يصل للمحتوى أبدًا. عناصر ItemsControl في العرض
لترتيب الصفوف فقط، فليست محطات Tab بلا اسم. الاختبار: `RuntimeA11yUiT` (من `RuntimeViewUiT` داخل UiGate) يمشي شجرة
الـpeers كما يخدمها UIA لكل تبويب داخلي، ويتأكد من زر النسخ والبصمة، ويتنقل بـTab من رأس التبويب حتى زر النسخ باللغتين.

## النسخة المختبرة والنسخة الحالية

النسخة المختبرة دليل موثق من الويكي وليست شرطًا ولا اقتراح تخفيض. تظهر فقط لأدلة RPCN المطابقة لإصدار اللعبة المثبت. الحالات:
`SameAsCurrent`، `OfficialGitHub` (تنزيل دقيق)، `HistoricalArchiveOnly` (بلا تنزيل)، `BuildUnresolved`. كتالوج البناءات موصوف في
[ملاحظات الويكي](WikiEvidence.md). لا «أفضل نسخة» ولا أحدث تلقائيًا ولا تخفيض تلقائيًا. لا يحتوي الدليل الحالي على نطاقات
انحدار (بداية/إصلاح) صريحة، لذلك لا يُقارن الإصدار الحالي بنطاق ولا يُخترع ادعاء.

## أدلة المصادر (Diagnostic Evidence)

`Core/Mdl/DiagnosticEvidence.cs` يفصل الهوية عن الدليل: كل سجل يحمل `EvidenceSourceRole`
(`OfficialRpcnTest`, `OfficialRpcnGuidance`, `OfficialCurrentBrowser`, `CommunityPrivateServerIndex`, `GithubIssue`, `ForumThread`)
و`EvidenceLevel` (Official ... Anecdotal, Unknown) و`SourceId`/`SourceUri`/`SnapshotDate`/`Provenance` وموضوعًا
(`EvidenceSubject`: Resolved / CommunityScope / ContinuationMetadata / Unresolved) ونوع ادعاء وقيمة المصدر الخام.
`AutoApply` و`MayMutateSettings` خاصيتان ثابتتان `false` بلا setter، ولا تُقرآن من البيانات.

`App/Data/rpcn-evidence.json` (مخطط 1، يولَّد ولا يُعدَّل يدويًا) يحمل إسقاطًا مضغوطًا للمصادر المغلقة فقط: متصفح RPCN الحالي
(68 CommId، مناطق وقيم Online/Peak كملاحظات مؤرخة) وفهرس الخوادم الخاصة المجتمعي (98 صفًا: 60 هوية محسومة، 7 نطاقات، 27 غير محسومة،
4 استمرار). لا يحتوي سجلات `OfficialRpcnTest` (اختبارات الويكي تبقى في `wiki-notes.json`)، ولا Issues ولا Forums (التقاط Forums لم
ينتهِ). التوليد: `R3Test.exe --gen-rpcn-evidence <browser-dir> <unified-dir> <tracker-dir> <out.json>`؛ لا مطابقة تقريبية ولا ترقية:
الهوية كما سجلتها المصادر المغلقة، وغير المحسوم يبقى `Unresolved`.

`DiagnosticEvidenceCatalog` يحمّل الملف بشكل fail-closed: مصدر مجتمعي بمستوى Official، أو فهرس خوادم أقوى من Anecdotal، أو ادعاء
لدور غير صاحبه (ملاحظة متصفح كاختبار رسمي)، أو نطاق/غير محسوم/استمرار مرتبط بصفحة لعبة، يُرفض الملف كله. `ForWikiPage` يعيد
`GameScoped` بمطابقة تامة لعنوان صفحة الويكي فقط. `RuntimeContextBuilder` يعرض في مجموعة الأدلة `RpcnCurrentBrowser` (Known، ملاحظة
مؤرخة وليست نتيجة اختبار) و`RpcnCommunity`/`RpcnCommunityCfw` (Partial، ادعاء مجتمعي لا يطبق منه شيء)؛ ولا يغير `Rpcn` (اختبار الويكي)
ولا ينشئ `TestedBuild` أو إصلاحًا أو إعدادًا موصى به. الاختبار: `DiagnosticEvidenceT`.

المخطط يبدأ بـ `Manifest` (إصدار المخطط، هوية المولد وإصداره، تاريخ لقطة كل مصدر و SHA-256 لكل ملف مدخل، عدد السجلات حسب الدور والادعاء والهوية،
وعدد الكيانات المميزة `SubjectsByRole`). الـ457 سجلًا هي صفوف ادعاءات وليست ألعابًا: 68 صفحة متصفح + 98 صفًا من الفهرس المجتمعي.
لا وقت جدار في الملف حتى يبقى التوليد حتميًا. عدم تطابق البيان مع السجلات يرفض الملف. الرفض يُرمى كـ `DiagnosticEvidenceRejectedException`
(القاعدة `EvidenceRejection` والملف ورقم السجل والسبب)، والتطبيق يبقى بكتالوج فارغ ويسجل السبب ولا يحمّل جزئيًا.

## تشخيص السجل

قراءة سجلات RPCS3 واستخراج الأحداث والتوقيعات ومطابقة الحالات الموثقة جزء من هذا المجال؛ التفاصيل في [تشخيص السجل](LogDiag.md).

## الحدود

لا تعديل لملفات RPCS3 من هذا المجال، ولا اعتماد وقت تشغيل على مجلد تطوير خارجي، ولا اسم لعبة أو Serial أو رقم بناء داخل منطق الإنتاج
(القيم المحددة موجودة في بيانات مولدة واختبارات فقط).

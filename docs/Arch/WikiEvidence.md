# ملاحظات الويكي ومعلومات Online/RPCN في شاشة إعدادات اللعبة

هذا المستند يصف مسار البيانات الذي يعرض إعدادات الويكي وملاحظاتها ومعلومات
`{{netplay}}` للعبة المحددة داخل تبويب Settings. لا توجد شاشة موازية: كل شيء يمر
عبر `RecView` و`PlanVm` نفسيهما.

## المسار

```text
Wiki XML (audit فقط)
  -> WikiNotesParser / WikiNotesCorpusReader   (Core/Svc/Cat)
  -> Data/wiki-notes.json  (WikiEvidenceCatalog، اختياري)
  -> WikiEvidenceUseCase   (App/Application، قراءة فقط)
  -> GameSettingsProjector (Core/Svc/Cfg) + SettingsSectionMap
  -> PlanVm.Sections / PlanVm.Evidence (App/Vm)
  -> Ctrl/Rec.xaml
```

- `SettingsSectionMap` هو المالك الوحيد لسؤال: أي تبويب RPCS3 يخص هذا الإعداد؟ (CPU,
  GPU, Audio, I/O, System, Network, Advanced, Emulator, GUI, Debug) وما دوره
  (Required/Recommended/Optional/Conditional). الإعداد يظهر في تبويب واحد فقط، وتبويب
  بلا دليل حقيقي لا يظهر. الويكي لا يعرّف إعدادات System أو GUI، لذلك تبقى صفرًا.
- الملاحظات (`WikiFreeTextNote`) لا تتحول إلى إعدادات أبدًا. كل ملاحظة تحمل نطاقها:
  serial، إصدار اللعبة، النسخة (digital/disc)، المنصة، العتاد، إصدار RPCS3، واعتماد
  الباتش. النطاق غير المحسوم يجعلها `Conditional` وتُعرض دون تطبيق تلقائي. لا يُخفى شيء.
  `WikiNotesParser` لا يأخذ إصدار اللعبة من رقم داخل عنوان IPv4 (مجرد أو host=IPv4) حتى لو ظهرت كلمة
  مثل below في الملاحظة؛ الاختبار `R3Test.exe --wiki-notes-parser-only VersionIpv4`. وبالمثل لا يأخذه من رقم داخل صيغة بناء
  RPCS3 (v0.0.N-NNNN أو rpcs3-v0.0.N)، ونطاق البناء يبقى من قاعدة 0.0.N المجردة وحدها؛ الاختبار
  `R3Test.exe --wiki-notes-parser-only VersionRpcs3Build`.
- `WikiOnlineEvidence.TestedGameVersion` هو الإصدار الذي اختبره الويكي وليس شرطًا.
  `RequiredGameVersion` يبقى `null` ما لم يذكر الويكي الشرط صراحة. غياب الجدول يعني
  Unknown ولا يعني Not supported، وأي حقل ناقص يعرض Unknown.

## العرض في شاشة الإعدادات

- الترتيب: إجراء التطبيق في الأعلى ثم قائمة إعدادات مسطحة مدمجة (الاسم والقيمة وخانة التفعيل فقط) ثم
  التحذيرات ثم RPCN المختصر ثم «استعادة الافتراضي» في الأسفل (زر واحد). لا عناوين أعمدة ولا عمود «الدور»؛
  الدور (Required/Optional/...) و أسماء تبويبات RPCS3 (CPU/GPU/...) بيانات داخلية للتعيين والترتيب والاختبار.
- تظهر الملاحظات المؤكدة لسياق اللعبة فقط: Serial والنسخة (NP*=رقمية، BL*/BC*=قرص) وإصدار APP_VER
  والمنصة والعتاد. الباقي يبقى دليلًا داخل النموذج ولا يُعرض. إصدار غير معروف لا يؤكد ملاحظة مقيدة بإصدار.
- جدول RPCN يظهر مختصرًا فقط إذا طابق الإصدار المختبر الإصدار المثبت، وإلا سطر واحد. الحقول الناقصة لا
  تُعرض في الملخص. رقم بناء RPCS3 المختبر نص عادي، ومعه «تنزيل النسخة» فقط عندما يحسم
  `Rpcs3BuildCatalog` صفًا واحدًا دقيقًا (انظر أدناه). لا رابط عام لصفحة التنزيل ولا رابط يُخترع.
- الرابط دائمًا كلمة ولا يظهر عنوان URL خام في أي مكان (ولا في ToolTip): صيغة الويكي `[url label]` تعرض `label`، والرابط المجرد
  يعرض «صفحة الويكي» إذا كان على wiki.rpcs3.net وإلا «رابط»، وقيمة مربوطة بـ`LinkText.TargetProperty` نصها عنوان تعرض الكلمة نفسها.
  الضغط على الكلمة يفتح الموقع. بجانبها زر صغير «إظهار الرابط» يكشف العنوان مفكوك الترميز في كتلة LTR بخط ثابت مع «نسخ» (العنوان
  الأصلي) و«فتح». `WikiLinks.IsSafeUrl` لم يتغير: مخطط غير http/https أو صيغة مكسورة تبقى نصًا عاديًا. الاختبار `R3Test.exe --wiki-label-link-only`.
- نص RPCS3 IP/Host Switches (host=IPv4 مكرر بفاصل &&، نمط صارم في `NoteParts.Split`) لا يظهر داخل الملاحظة في أي موضع
  يعرض ملاحظة (ملاحظات الويكي وأصلها الإنجليزي، ملاحظات RPCN، ملاحظات cat.json في ToolTip الصف). مكانه زران: «إظهار IP/Host» (كتلة
  LTR بخط ثابت) و«نسخ IP/Host» (النص الأصلي بالضبط من المصدر الإنجليزي عبر `LinkText.SourceProperty`، بلا LRM ولا فواصل أسطر). و«Category:...»
  الملصوقة في نهاية سطر لا تُعرض، وذيلها المترجم «الفئة:» يُحذف فقط إذا كان في المصدر. طبقة العرض فقط: البيانات وجداول الترجمة لا تتغير.
  الاختبار `R3Test.exe --note-reveal-only` و`NoteRevealUiT` داخل `UiGate`.
- ToolTip صف الإعداد (ملاحظة cat.json) يُغلق مع أول نقرة داخله، فيُضبط `LinkText.InteractiveProperty` على False هناك فقط:
  يظهر `label` الرابط نصًا عاديًا بلا رابط ولا «إظهار الرابط» ولا «نسخ» ولا «فتح» ولا أزرار IP/Host، وبلا بديل. القيمة
  الافتراضية True، فبقية المواضع لم تتغير. الاختبار `GameSettingsUiT` داخل `UiGate`.

## كتالوج بناءات RPCS3 (تنزيل النسخة المختبرة)

`App/Data/rpcs3-builds.json` (مشحون، مخطط 2) يحوّل نص الويكي `X.Y.Z-BUILD` إلى أرشيف Windows الرسمي
بالضبط. `Rpcs3BuildCatalog` (Core/Svc/Cat) عام لكل الألعاب: يحلل النص، ويبحث بـ Version+Build فقط،
ولا يبني رابطًا ولا يختار أقرب/أحدث/تقريبيًا أبدًا. `Resolve` يعيد إحدى ثلاث حالات:
`ResolvedOfficialGitHub` (رابط تنزيل دقيق)، `HistoricalArchiveEvidenceOnly` (دليل تاريخي بلا تنزيل)، `Unresolved`.
غياب الصف أو التعارض أو التباس صفوف Archive أو رابط غير معتمد أو منصة غير Windows كلها `Unresolved`.

فئات المصدر داخل الملف (`sourceClass`):

- `OFFICIAL_GITHUB_DIRECT`: أصل Windows رسمي من `RPCS3/rpcs3-binaries-win`. هو مصدر التنزيل الوحيد، وله الأولوية دائمًا على
  Archive. يحمل `commitFull` و`sizeBytes` و`sha256` (قيمة الموقع الرسمي، وتطابق `digest` أصل GitHub حيث وُجد) و`matchMethod`
  (`EXACT_VERSION_BUILD`، أو `OFFICIAL_URL_MATCH` لـ`0.0.29-15565` الذي يحمل الموقع رقمه ورابطه الدقيق بينما اسم أصل GitHub القديم
  بلا رقم بناء). الصفوف التي قال الموقع تاريخيًا إنها غير متاحة وفيها أصل GitHub الآن تحمل `historicalUnavailable=true`
  (فرق حالة زمني وليس تعارضًا).
- `OFFICIAL_SITE_ARCHIVE_POINTER`: صف تاريخي أشار إليه الموقع الرسمي إلى Internet Archive وأثبتت الهوية عبر commit/PR/تاريخ.
  `verification` إما `ARCHIVE_METADATA_VERIFIED` (الهوية والحجم متفقان) أو `ARCHIVE_IDENTITY_VERIFIED` (الهوية فقط). لا يُسمى أي
  صف Hash-verified لأن الملف الثنائي نفسه لم يُحسب له SHA-256. هذه الصفوف **مرجع فقط**: لا يوجد زر تنزيل من Archive ولا رجوع
  تلقائي إليه، لأن واجهة الإعدادات لا تملك بعد وسيلة آمنة لعرض أن المصدر تاريخي وليس مستودع GitHub الرسمي. صفوف
  `ARCHIVE_AMBIGUOUS` و`ARCHIVE_NOT_FOUND` لا تُشحن (تظهر أعدادها في `counts` فقط) وتُحسم `Unresolved`.

الملف يولَّد ولا يُعدَّل يدويًا، من قاعدة بناءات RPCS3 الرئيسية المتحقق منها (تطوير فقط، لا تُقرأ وقت التشغيل ولا يُشحن أي
مسار منها) وقائمة GitHub الكاملة المحدثة:

```text
R3Test.exe --gen-build-catalog <rpcs3-windows-builds-master.json> <github-windows-builds-refreshed.csv> <out.json>
```

الناتج حتمي (مرتب، صف لكل سطر) ويُقارن بالمشحون قبل النسخ إلى `App/Data`. يفشل التوليد إذا تعارض صف مباشر مع قائمة GitHub أو
تصادم صف Archive مع صف GitHub. التغطية ليست كل بناءات RPCS3 المصدرة، وبناء غير موجود يبقى «غير محسوم» بلا أي بديل. راجع
[بيئة التشغيل والتوافق](RuntimeContext.md) لعرض النسخة الحالية مقابل المختبرة.

ملاحظة جدول Online (`GameOnlineVm.Notes`) تمر على مسار الترجمة نفسه لملاحظات الويكي (`NoteSvc.Trans`): تُعرض بالعربية
فقط إذا وُجدت ترجمة معتمدة مطابقة تمامًا، وإلا بقي نص الويكي الأصلي كما هو. الاختبار `R3Test.exe --online-notes-only`.

الترجمات المعتمدة من مراجعة ملف CSV تُستورد في جدول `PNote.Wiki.Ar.cs` بالمفتاح المطبَّع نفسه (`NoteSvc.Trans` يقصّ النقطة الأخيرة ويطوي
الفراغات). لا يُستورد صف تساوي ترجمته المعتمدة نصه الإنجليزي (تواريخ وإصدارات RPCN)، والجدول غير حساس لحالة الأحرف فيُحفظ صف واحد
من كل مفتاحين يختلفان بالحالة فقط. تُستورد الترجمة بلا تشكيل لأن بوابة الترجمة تمنع التشكيل في مصدر App. الاختبار `R3Test.exe --reviewed-notes-only`.

## مصدر البيانات

`App/Data/cat.json` و`App/Data/wiki-notes.json` مشحونان ويولَّدان معًا من ملف واحد هو
`RPCS3+Wiki-20260920160231.xml` (SHA-256 `7977CA26176E12DD167241CDE77D68E6CC64CE24CB39C2682FD5E686CA7936C5`، لقطة 2026-09-20) بالأمر:

```text
R3Test.exe --gen-production-data <xml> <out-dir>
```

لا تعدل الملفين يدويًا. عند تغيير المحلل أو الخريطة يُولَّد مرشح، ويقارَن بالمشحون، ثم يُنسخ.
التحديثات اللاحقة لا تعدّل `App/Data/cat.json`: تُحفظ في طبقة محلية فوقه (انظر [تحديث قاعدة التوصيات](RecSync.md)).
الملف المفقود حالة طبيعية، والتالف يسجل رسالة ويرجع إلى بدون ملاحظات دون لمس الإعدادات.
لتوليد ملف ملاحظات مرشح فقط:

```text
R3Test.exe --wiki-notes-audit <xml> <report-dir> <candidate-wiki-notes.json>
```

## بوابات الاختبار

- `--wiki-notes-audit`: تغطية القسم لكل إعداد مدعوم (`DROPPED = 0`)، ومطابقة كل
  `{{config}}` و`{{netplay}}` في XML الخام مع ما استخرجه المحلل لكل صفحة.
- `GameSettingsUiT`: Minecraft من الملف إلى النموذج إلى الـViewModel، وتصيير
  `Rec.xaml` بالعربية والإنجليزية (يعمل داخل `UiGate` لأن WPF يسمح بـApplication واحد).
- مفاتيح اللغة (`Section.*`, `Note.*`, `Online.*`, `Wiki.*`) موجودة في الملفين.

## العتاد وحالة التغيير الموثق

حالة كل تغيير موثق (`StateSvc`) تُحسب من فئة الدليل (`WikiRecommendationClass`)، ومخاطرته المسجلة، والقيمة الحالية في RPCS3 فقط.
`HwTier` تصنيف قديم للعرض في شريط الحالة، ولا يقرؤه أي مسار يحسم الانطباق: لا يحجب تغييرا موثقا ولا يرفعه ولا يخفضه، ولا يدخل الباتشات
الرسمية ولا التشخيص ولا هوية البناء ولا حقائق Runtime (`EvidenceApplicabilityT`). شرط العتاد لا يأتي إلا من الدليل نفسه: ملاحظة تذكر
مصنع بطاقة رسومات (`NoteScope.HardwareApplies`)، وغير المثبت `Unknown`. شروط CPU الحرة (مثل "weak CPU") لا تتحول إلى عتبة. شروط
المعمارية أو ميزات المعالج أو البناء غير ممثلة حاليا في صفوف الإعدادات (`CfgChg`) وتبقى في نص الملاحظة؛ فجوة معروفة لا تُعوَّض بالتخمين.

## قرار حالة التغيير الموثق (مسار الإنتاج)

`StateSvc` يقرر حالة كل تغيير عبر `ChangeScope` و`EvidencePolicy`، لا عبر فئة الدليل والمخاطرة وحدهما:

```text
CfgChg.Notes -> ChangeScope.Conditions (قواعد صياغة معروفة فقط)
RuntimeFacts.Create(game, host, build, hardware) -> EvidenceRuntimeFacts
EvidencePolicy.Assess + Decide -> Blocked | Inform | Suggest | Apply -> RecState
```

- `Apply` (اختيار تلقائي) فقط لدليل Exact وحتمي (Required/Rec) وحديث. عدم تطابق النطاق = `Blocked`. نطاق معلن لا يمكن إثباته (Partial) أو نص غير محسوم = `Manual` بلا اختيار تلقائي.
- `ChangeScope` لا يستنتج من كلمة حرة: المُصيِّر (Vulkan/OpenGL) والمنصة لا يصبحان نطاقًا إلا بصياغة مثبتة ("only on Vulkan"، "Vulkan only"، "on Vulkan renderer"، "only on Linux"...) وإلا `Unresolved`. اسم شركة (AMD/NVIDIA/Intel) بلا عبارة عتاد معروفة `Unresolved`. المُصيِّر الحالي لا يُعرف وقت التخطيط (قد تغيره الخطة نفسها) فيبقى التغيير المقيد بمصيِّر `Partial`.
- حقائق العتاد (`GpuVendor`/`CpuVendor`) تُملأ فقط عندما يثبتها الفحص (كل المحولات معروفة الشركة ومتفقة)؛ غير المثبت `null` ولا يطابق أبدًا. لا `HwTier`.
- Primary فقط يرث الإعدادات؛ Demo وRelatedEdition لا يرثان. `PATCH_SPECIFIC` و`VERSION_SPECIFIC` و`INFORMATIONAL_NOTE` و`Unsupported` و`Ambiguous` لا تصبح إعدادًا.

## ملف المعرفة المضغوط (`Data/rpcs3-knowledge.json`)

ملف مولَّد وحتمي (`R3Test.exe --gen-knowledge <removals.csv> <lifecycle.csv> <cpu-dir> <cat.json> <wiki-notes.json> <out.json>`)
يلخص خطوط الأدلة التطويرية دون شحن أي corpus خام:

- إزالات الويكي التاريخية ذات المعنى فقط (`ClaimedFixed` و`SettingRemoved` و`Replaced`؛ لا `BulkCleanup` ولا `Unknown`) لصفحات ما زالت بعنوانها نفسه.
  `ClaimedFixed` ادعاء محرر غير موثق: لا يصبح حد بناء ولا يغير التوصية الحالية ولا `VerifiedFixedUpstream`.
- دورة حياة أسماء الإعدادات من مصدر RPCS3 المجمد (أرقام البناء = عدد commits). لا تربط بصفحة لعبة إلا إذا كانت الصفحة نفسها تحمل إزالة تاريخية بالمفتاح نفسه، ولا تشحن السجلات التي لا تملك Commit+Build مثبتين إلى عرض اللعبة. هذا يثبت دورة حياة الإعداد فقط ولا يثبت أن مشكلة اللعبة أصلحت.
- خلايا Thread Scaling ولوائح CPU من المصنف المجتمعي: معلومة عتاد فقط. لا تشابه ولا تخمين ولا `Apply` تلقائي.
  عنوان Thread Scaling لا يكفي وحده لربط الدليل بلعبة وقت التشغيل: يلزم أيضًا أن يحمل الكتالوج الحالي للعبة
  `WikiRec` مطابقًا للـSerial بدور `Primary` وبعنوان Wiki مطابق حرفيًا. `Demo` و`RelatedEdition` وغياب التوصية
  تبقى بلا ربط. قائمة `WikiPageEvidence.Serials` ليست مصدر ملكية للصفحة لأنها sidecar للملاحظات وليست خريطة GameID.
- لوائح CPU العامة تبقى بيانات معرفة مضغوطة ومصدرها `Community` فقط. لا تدخل `RuntimeContext` كتقييم أداء أو score،
  ولا تغيّر `HwTier` ولا أي إعداد؛ العرض الحالي يستهلك فقط الأدلة الخاصة باللعبة (Thread Scaling) بعد إثبات الهوية أعلاه.

`Rpcs3KnowledgeCatalog` يرفض الملف كله عند أي مخالفة (مخطط، SHA، مكرر، رابط غير http(s)، محارف تحكم، حجم > 2MB)، وغيابه حالة عادية.
البحث بعنوان الصفحة حرفيًا (ordinal) ونتيجته `Inform` كحد أقصى (`Suggest` سقف) عبر `EvidencePolicy.Decide`: `HistoricalWiki/Wiki` للإزالات، و`Rpcs3Lifecycle/Official` لدورة الحياة المثبتة بعد تطابق Page+WikiKey، و`HardwareData/Community` للمصنف.
تُعرض أدلة اللعبة في بطاقة «السجل التاريخي وبيانات العتاد» بسطور معلوماتية فقط. `EvidenceAuthority` مصدر فقط، و`RegressionRecord` نوع لا سلطة.


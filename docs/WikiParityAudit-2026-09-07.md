# RPCS3 Wiki Parity Audit - R3Config Game Setup

> **Historical audit.** It assumed June 2026 (`RPCS3+Wiki-20260613075133.xml`) as the canonical baseline. That policy is superseded: the production canonical Wiki source is now `RPCS3+Wiki-20260907205217.xml` (SHA-256 `410AACCBD9A77637671D6DEB6768CD1D7E7506FF83C8197265BEEA34CACB2097`), per the project owner's explicit decision (see docs/Arch/RelTest.md).

Audit date: 2026-09-07
Scope: Wiki XML -> Page identity -> Serial extraction -> Recommendation extraction -> Template interpretation -> Classification -> RPCS3 canonical key/value mapping -> Catalog generation -> cat.json -> Game Setup.
Mutation performed: **none**. No source file, no `App/Data/cat.json`, no RPCS3 file was modified.

---

## 1. الأدلة المستخدمة

| الدليل | الوصف |
| --- | --- |
| Wiki XML (جديد) | `RPCS3+Wiki-20260907205217.xml` - 28,556,025 بايت، SHA256 `410AACCB...CACB2097`، 2320 صفحة / 2320 مراجعة |
| Wiki XML (المثبت) | `RPCS3+Wiki-20260613075133.xml` - **غير موجود** في أي مجلد متاح للتدقيق |
| الكتالوج | `App/Data/cat.json` - DataVersion `2026-06-13`، SourceHash `254A0A41...4BE452`، 8530 توصية، 7450 تغيير |
| مصدر RPCS3 | `rpcs3-master` - `rpcs3/Emu/system_config.h` و `system_config_types.cpp` (255 مفتاح إعداد + جداول قيم الـenums) |
| قالب الويكي | `Template:Config` من نسخة 2026-09-07 - 183 بارامتر (101 إعداد + 82 ملاحظة) |
| كود المشروع | `Core/Svc/Cat/WikiCorpus.cs`, `Core/Svc/Cat/Wiki.cs`, `Core/Svc/Rec/Risk.cs`, `Core/Svc/Rec/State.cs`, `Core/Svc/Cfg/Plan.cs`, `Core/Svc/Setup/GameSetupPlanningService.cs`, `Tst/WikiCorpusT.cs`, `Script/RelTest.ps1` |

### المنهجية

تمت إعادة تنفيذ `WikiSvc.Analyze` بالكامل خارج المشروع (بايثون، للقراءة فقط) بما في ذلك
`FindTemplateBlocks`, `SplitTemplateParameters`, `ParseGameIds`, `ClassifyGameIdRole`,
`ParseConfig`, `ClassifyEntry`, `ConvertCorpusValue`, `BuildRecommendations`, `RecPol.Classify`.

**دليل أمانة النقل:** عند مقارنة 6585 سيريالًا مشتركًا بين الناتج المُعاد بناؤه و`cat.json`:
4893 تغييرًا **متطابق حرفيًا**، و**0** اختلاف في `Risk`، و**2** اختلاف قيمة فقط.
كما تطابقت عدادات المحلل مع الـbaselines المثبتة في `Tst/WikiCorpusT.cs`:
`OrphanNote` 36/36، `MalformedGameId` 3/3، `EmptyGameId` 6/6، `AmbiguousWikiValue` 3/3،
`PatchSpecific` 77/77، `VersionSpecific` 5/5.

---

## 2. ملخص النتائج

| # | التصنيف المطلوب | الشدة | الحالة |
| --- | --- | --- | --- |
| F1 | Wrong serial scope + Catalog stale vs pinned Wiki | حرج | مثبت |
| F2 | بوابة التطابق (Wiki parity gate) لا تعمل ضمن التحقق | حرج | مثبت |
| F3 | Wrong setting (اسم مفتاح RPCS3 غير موجود) | عالٍ | مثبت (كامن) |
| F4 | Missing recommendation (مفاتيح ويكي قابلة للتمثيل ومُهملة) | عالٍ | مثبت (نشط) |
| F5 | Wrong value (حدود رقمية غير مطابقة لـRPCS3) | عالٍ | مثبت (نشط) |
| F6 | Wrong value (نطاقات قيم enum غير مطابقة) | متوسط | مثبت (كامن) |
| F7 | Wrong classification (التصنيف لا يُحفظ في الكتالوج) | عالٍ | مثبت |
| F8 | Template default promoted incorrectly (لا فحص deviation) | متوسط | مثبت |
| F9 | Newer Wiki delta 2026-06-13 -> 2026-09-07 | معلوماتي | مُقاس |

ما ثبت أنه **سليم**: Notes promotion، Patch-specific leakage، General->Mandatory promotion، Unsupported/Ambiguous acceptance، وتغطية مفاتيح القالب. التفاصيل في القسم 4.

---

## 3. النتائج بالتفصيل

### F1 - حرج: `cat.json` يمنح إعدادات اللعبة الكاملة لسيريالات ليست Primary

**الملاحظة.** `Core/Svc/Cat/WikiCorpus.cs` -> `BuildRecommendations` يستبعد صراحةً أي سيريال
ليس له `WikiGameIdRole.Primary` على صفحة واحدة:

```csharp
if (primaryCandidates.Length == 0)
{
    if (candidates.Length > 1) { /* DuplicateSerialConflict finding */ }
    continue;
}
```

لكن `cat.json` يحتوي **1,945 سيريالًا** بلا أي ارتباط Primary في الويكي، تحمل
**2,334 تغييرًا من أصل 7,450 (31.3%)**.

| الدور في الويكي | عدد السيريالات في cat.json | منها يحمل إعدادات فعلية |
| --- | --- | --- |
| Demo | 1187 | 510 |
| Unknown (وسم متغير غير معروف) | 581 | 262 |
| RelatedEdition | 165 | 101 |
| PlayView | 10 | 5 |
| TradeDemo | 2 | 1 |
| **الإجمالي** | **1945** | **879** |

**أمثلة مثبتة.**

- `NPUB90035` (نسخة Demo من *All-Pro Football 2K8*) يرث `Video/Force CPU Blit=true` و`Video/Write Color Buffers=true` من صفحة اللعبة الكاملة.
- `NPEB90009` / `NPUB90008` (Demo من *Armored Core 4*) يرثان `Input/Output/Keyboard=Basic` و`Video/Frame limit=Auto` و`Video/Minimum Scalable Dimension=320`.
- `BLES00898` (*Assassin's Creed II* بوسم `Game Of The Year`) يرث `Video/Force CPU Blit=true`.
- أكثر الصفحات تأثرًا: *Beyond: Two Souls* (31 سيريال متغير)، *The Last of Us* (17)، *Killzone 3* (17)، *Starhawk* (15)، *LittleBigPlanet 2* (14).

نص الويكي لا يترك مجالًا للالتباس؛ مثال *1942: Joint Strike*:

```
|gameid = {{gameid|id=NPEB00026, NPUB30024|ird=NA}}<br>'''''Demo'''''<br>{{gameid|id=NPEB90079, NPUB90104, NPHB00042|ird=NA}}
```

**السبب الجذري.** `cat.json` **لم يُولَّد بالكود الحالي**. توزيع محتواه يطابق تجميعًا على مستوى
الصفحة (page-level grouping) سابقًا لنموذج الأدوار (`WikiGameIdRole`) الموجود اليوم في
`BuildRecommendations`. أي أن الكتالوج المشحون والمحلل المشحون يطبقان سياستين مختلفتين
على نفس المصدر.

**الأثر على المستخدم.** لعبة Demo أو إصدار متغير تُعرض له توصيات لم يربطها الويكي به،
وهذا مخالف مباشرة لقاعدة المشروع "Exact Match / Fail Closed".

**ملاحظة أمانة.** أي الطرفين "الصحيح" (نشر إعدادات الصفحة على كل سيريالاتها، أم قصرها على
Primary) قرار سياسة لم أحسمه هنا. المثبت هو أن **الكتالوج والكود متعارضان**، وأن الكتالوج
يخالف السياسة المعلنة في `docs/Arch` وفي اختبارات المشروع نفسها.

---

### F2 - حرج: بوابة التطابق بين الويكي والكتالوج لا تُشغَّل إطلاقًا

`Script/RelTest.ps1` يستدعي `R3Test.exe` بلا وسائط (أو بمسار RPCS3 فقط):

```powershell
$testArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Rpcs3Path)) { $testArguments += $Rpcs3Path }
Invoke-R3Executable -LiteralPath $testExe -Arguments $testArguments ...
```

بينما `Tst/Program.Wiki.cs` -> `TryRunWikiMode` لا يُفعَّل إلا بوسيط صريح:
`--wiki-corpus` أو `--wiki-parser-only` أو `--wiki-history-audit`.
التشغيل الافتراضي ينفّذ `WikiCorpusT.Fixture` فقط، وهو تجهيزة اصطناعية من **4 صفحات**.

نتيجتان:

1. **لا شيء في سلسلة التحقق يقارن `App/Data/cat.json` بالويكي.** وحتى `--wiki-corpus`
   نفسه لا يقارن الملف المشحون: هو يولّد كتالوجًا مؤقتًا ويقارنه بـ`RecommendationCatalogUseCase.Rebuild`،
   ثم يستخدم `oldCatalogPath` كوسيط خارجي. لا يوجد `Assert` يربط ناتج المولّد بمحتوى `App/Data/cat.json`.
2. **الـbaselines المثبتة في `Tst/WikiCorpusT.cs` لم تعد متسقة مع سلوك الكود الحالي.**
   السطر يؤكد `analysis.CatalogRecommendations.Count == 8530` مقابل `UniqueSerials == 8535`،
   أي أن كل سيريال تقريبًا ينتج مدخلًا. هذا يتطلب أن يكون كل سيريال Primary، وهو ما يناقض
   نموذج الأدوار: التشغيل على نسخة 2026-09-07 يعطي 6593 مدخلًا مقابل 8542 سيريالًا فريدًا.

لذلك عبارة "Build succeeded / 0 Errors / كل الاختبارات PASS" **لا تشكل أي دليل على parity الويكي**.

**كيف تتحقق بنفسك (بدون تعديل):**

```
dotnet run --project Tst\R3Test.csproj -c Release -- --wiki-corpus <wiki.zip> App\Data\cat.json 2026-06-13 report.json
```

إن رجع الأمر بفشل على `CatalogRecommendations.Count == 8530` فهذا يثبت F1 و F2 معًا داخل المشروع نفسه.

---

### F3 - عالٍ: `PPU LLVM Java Mode Handling` ليس مفتاحًا في RPCS3

`Core/Svc/Cat/Wiki.cs` -> `CfgMap`:

```csharp
["ppu llvm java mode handling"] = ("Core", "PPU LLVM Java Mode Handling"),
```

مسح 255 مفتاحًا في `rpcs3/Emu/system_config.h` لا يُظهر هذا المفتاح. المفتاح المكافئ في
RPCS3 الحالي هو:

```cpp
cfg::_bool ppu_use_nj_bit{ this, "PPU Accurate Non-Java Mode", false };
```

هذا هو **المفتاح الوحيد من بين 70 مفتاحًا في `CfgMap` الذي لا يوجد له مقابل في RPCS3 master** -
بقية 69 مفتاحًا (بما فيها `Relaxed ZCULL Sync` و`Accurate ZCULL stats`) مطابقة حرفيًا. لو كُتب هذا
المفتاح إلى `config_<SERIAL>.yml` فسيتجاهله RPCS3 بصمت.

**الأثر الحالي:** كامن. لا توجد صفحة ويكي واحدة تضع قيمة لـ`ppu llvm java mode handling` في نسخة 2026-09-07.

---

### F4 - عالٍ: توصيات ويكي حقيقية تُهمل رغم وجود مفتاح RPCS3 لها

هذه المفاتيح مُدرجة في `ActiveConfigKeys` لكنها **ليست في `CfgMap`**، فتُصنَّف `Unsupported`
وتختفي من الكتالوج دون أن يرى المستخدم شيئًا:

| مفتاح الويكي | مفتاح RPCS3 الموجود فعلًا | ألعاب متأثرة الآن |
| --- | --- | --- |
| `allow host gpu labels` | `Video / Allow Host GPU Labels` | 10: Borderlands 2, Far Cry 2, Far Cry 3, Far Cry 4, James Cameron's Avatar, MotorStorm: Apocalypse, NFL Tour, Rage, Saints Row 2, The Evil Within |
| `emulate special depth comparison` | `Video / Emulate Special Depth Comparison` | 7: Deadly Premonition DC, Heavy Rain, SOCOM 4, Sly Cooper: Thieves in Time, The Darkness, Tomb Raider, Uncharted: Drake's Fortune |
| `ppu thread count` | `Core / PPU Threads` (نطاق 1..8) | 1: Need for Speed ProStreet |
| `disable asynchronous memory manager` | `Video / Disable Asynchronous Memory Manager` | 1: Eternal Sonata |
| `output scaling` | `Video / Output Scaling Mode` | 0 (كامن) |
| `force hardware msaa resolve` | `Video / Force Hardware MSAA Resolve` | 0 (كامن) |

**حالة منفصلة - `firmware libraries` (24 لعبة).** يوجد مفتاح RPCS3 مقابل
(`Core / Libraries Control`) لكنه من نوع `cfg::set_entry` (قائمة) بينما `CfgChg` سلسلة مفردة.
الإسقاط هنا مبرر بنيويًا، لكنه صامت: ألعاب مثل *Metal Gear Solid 4*، *L.A. Noire*، *Journey*،
*Kingdom Hearts HD 1.5/2.5 ReMIX* تعتمد على تحميل مكتبات LLE محددة، ولا يظهر للمستخدم
أي تنبيه بأن خطوة إلزامية من الويكي غير مُنفَّذة.

**مبررة الاستبعاد فعلًا:** `graphics device` (خاص بالعتاد)، `audio out linux` (منصة أخرى)،
`enable spu events busy loop` و`delay each odd mfc command` (لا مفتاح مقابل في RPCS3 master؛
الأقرب `SPU Reservation Busy Waiting *` و`MFC Commands Shuffling *` وليسا مكافئين).

---

### F5 - عالٍ: حدود رقمية في المحلل لا تطابق حدود RPCS3

| مفتاح الويكي | حارس R3Config | نطاق RPCS3 master | الأثر |
| --- | --- | --- | --- |
| `vblank rate` | `TryNumeric(value, 1, 3000)` | `cfg::_int<1, 6000>` | **نشط**: *Need for Speed The Run* تُوصي بـ`6000` -> ترفضها R3Config كقيمة غير صالحة رغم صلاحيتها في RPCS3 |
| `maximum spurs threads` | يقبل `0..5` و`"Unlimited"` | `cfg::_int<1, 6>` (افتراضي 6) | يقبل `0` (غير صالح)، ويكتب `"Unlimited"` نصًا في حقل عددي، ويرفض `6` (صالح) |
| `driver wake-up delay` | `0..7000` | `cfg::uint<0, 16667>` | كامن (أقصى قيمة في الويكي 3000) |
| `resolution scale` | `50..800` | `cfg::uint<25, 800>` | كامن |
| `accurate ppu 128 reservations` | `"Always Enabled"`->-1، `"Disabled"`->0 فقط | `cfg::_int<-1, 14>` | القيم 1..14 غير قابلة للتمثيل (كامن) |

ملاحظة: تعليق القالب في الويكي نفسه يقول `<!-- Value between 1 to 500 -->` لـ`vblank rate`،
أي أن الويكي وRPCS3 وR3Config يعطون ثلاثة نطاقات مختلفة. المرجع الصحيح هو مصدر RPCS3.

---

### F6 - متوسط: نطاقات قيم enum لا تطابق RPCS3 (كامنة اليوم)

من مقارنة `CorpusValues` في `WikiCorpus.cs` بجداول `fmt_class_string<...>::format` في
`system_config_types.cpp`:

**قيم يقبلها R3Config وهي غير صالحة في RPCS3** (لو ظهرت في الويكي تُكتب قيمة يتجاهلها المحاكي):

- `renderer = Disabled` -> RPCS3 يستخدم `Null`
- `audio out windows = Disabled` -> RPCS3 يستخدم `Null`
- `microphone type = Disabled` -> RPCS3 يستخدم `Null`
- `3d rendering mode = Anaglyph` -> ليست قيمة في `stereo_render_mode_options`
- `ppu decoder = Interpreter (dynamic)` -> أُزيلت؛ `ppu_decoder_type` فيه قيمتان فقط

**قيم صالحة في RPCS3 يرفضها R3Config** (توصية ويكي مستقبلية ستُسقط):

`framelimit`: `120`, `Display` - `spu xfloat accuracy`: `Inaccurate` -
`rsx fifo accuracy`: `PS3` - `mouse handler`: `Raw` - `move handler`: `Raw Mouse` -
`audio out windows`: `FAudio` - `default resolution`: `1920x1080i`, `720x480i`, `720x576i` -
`3d rendering mode`: `Anaglyph Custom`

لا توجد صفحة ويكي تستخدم أيًا من هذه القيم اليوم، لذا الشدة متوسطة وليست حرجة.

---

### F7 - عالٍ: تصنيف التوصية لا يصل إلى الكتالوج إطلاقًا

`Core/Mdl/Rec.cs`:

```csharp
public sealed record CfgChg(
    string Section, string Key, string Value,
    string WikiKey, string? Notes, RecRisk Risk);
```

لا يوجد حقل لـ`WikiRecommendationClass`. المحلل يحسب
`MandatoryGameConfig / GeneralRecommendation / PatchSpecific / VersionSpecific / InformationalNote / Unsupported / Ambiguous`
ثم **يرميها** عند حدود الكتالوج، ويبقى فقط `RecRisk` الناتج عن `RecPol.Classify` -
وهو تصنيف كلمات مفتاحية بتصنيفة مختلفة تمامًا.

**النتائج القابلة للقياس:**

- من 1058 توصية مميزة في نسخة 2026-09-07: **126** مصنّفة `MandatoryGameConfig` في المحلل،
  **82 منها لا تُختار تلقائيًا** في `Game Setup`، و**41** منها تحصل على `Risk = Unknown`.
- السبب البنيوي في `Core/Svc/Rec/Risk.cs`: `Required` أو `Rec` مستحيلان إلا لمفتاح داخل
  `AutoKeys` (9 مفاتيح فقط). وهذا مؤكد بالبيانات: المفاتيح الوحيدة التي حصلت على تصنيف
  قابل للاختيار التلقائي هي `write color buffers`, `read color buffers`, `write depth buffers`,
  `read depth buffers`, `strict rendering mode`, `accurate spu dma`,
  `accurate rsx reservation access`, `spu xfloat accuracy`, `xfloat accuracy`.
- `Core/Svc/Rec/State.cs` يربط الاختيار التلقائي بذلك مباشرة:
  `IsAuto(risk) => risk is RecRisk.Required or RecRisk.Rec`.

**خلل ترتيب داخل `RecPol.Classify`.** فحص `Timing` يسبق فحص `PatDep`، و`Keys` (Timing) يضم
`framelimit`, `vblank rate`, `clocks scale`, `vblank ntsc fixup`. لذلك توصية معتمدة على باتش
على أحد هذه المفاتيح تفقد إشارة الاعتماد. مثال مثبت - *Fuse*:

```
framelimit = 30
note: "Required unless using FPS Patch due to 2x game speed at 60 FPS."
-> Risk = ManualTiming   (وليس PatchDependent)
```

هذه هي التوصية الوحيدة التي يبقيها استثناء `PatchDependencyCondition` داخل الكتالوج، وهي
بالضبط الحالة التي صُمم الاستثناء لأجلها - ومع ذلك يضيع الوسم.

---

### F8 - متوسط: لا يوجد فحص انحراف عن قيم RPCS3 الافتراضية

`Core/Svc/Cfg/Plan.cs` يبني الخطة على `config_<SERIAL>.yml` فقط:

```csharp
var targetPath = Path.Combine(install.CustPath, $"config_{game.Serial}.yml");
...
return R3Yaml.ReadVal(targetPath, section, key);
```

ولا يقرأ `config.yml` العام لـRPCS3 ولا يعرف القيم الافتراضية المضمّنة في المحاكي.
و`R3Yaml.ReadVal` يرجع `null` إن لم يوجد الملف، فيصبح كل توصية "غير مضبوطة" وتظهر كانحراف.

بمقارنة `cat.json` بالقيم الافتراضية المستخرجة من `system_config.h`:
**823 من 7,450 تغييرًا (11%) قيمتها مطابقة تمامًا لقيمة RPCS3 الافتراضية**.

| الإعداد | القيمة | افتراضي RPCS3 | عدد المدخلات |
| --- | --- | --- | --- |
| `Video / Anisotropic Filter Override` | `0` | `0` | 262 |
| `Video / Relaxed ZCULL Sync` | `false` | `false` | 166 |
| `Video / Resolution Scale` | `100` | `100` | 127 |
| `Video / Frame limit` | `Auto` | `frame_limit_type::_auto` | 70 |
| `Video / Accurate ZCULL stats` | `true` | `true` | 56 |
| `Core / SPU XFloat Accuracy` | `Approximate` | `xfloat_accuracy::approximate` | 45 |
| `Video / Vulkan.Asynchronous Texture Streaming` | `false` | `false` | 36 |
| `Core / SPU Block Size` | `Safe` | `spu_block_size_type::safe` | 20 |
| `Video / MSAA` | `Auto` | `msaa_level::_auto` | 18 |
| `Video / Multithreaded RSX` | `false` | `false` | 11 |

هذا يخالف القاعدة المعلنة "لا يتم إنشاء Custom Configuration إذا لم يوجد deviation فعلي عن default".
(الملاحظة العادلة: قد يكون تثبيت القيمة الافتراضية مقصودًا لتجاوز إعداد عام غيّره المستخدم -
لكن المشروع لا يستطيع التمييز لأنه لا يقرأ الإعداد العام أصلًا.)

---

### F9 - معلوماتي: فرق الويكي بين 2026-06-13 و 2026-09-07

بمقارنة الكتالوج المولَّد من نسخة سبتمبر مع `cat.json` (للسيريالات المشتركة فقط):

| الفئة | العدد |
| --- | --- |
| تغييرات متطابقة تمامًا | 4893 |
| إعدادات جديدة في الويكي وغير موجودة في cat.json | 267 |
| إعدادات في cat.json ولم تعد في الويكي | 120 |
| اختلاف في نص الملاحظات فقط | 101 |
| اختلاف في القيمة | 2 |
| اختلاف في `Risk` | 0 |
| اختلاف في `WikiTitle` | 0 |

أمثلة: *Formula One Championship Edition* أضافت `Disable ZCull Occlusion Queries=On`؛
*The Darkness* لم تعد توصي بـ`Strict Rendering Mode`؛ *LEGO Dimensions* غيّرت
`resolution scale threshold` من `340x340` إلى `320x320`.

هذه الفروق سلوك طبيعي لتقادم snapshot وليست خللًا برمجيًا. **لا يمكن فصلها بدقة 1:1
عن أثر تغييرات المحلل** لأن ملف الـsnapshot المثبت `RPCS3+Wiki-20260613075133.xml` غير متوفر.

---

## 4. ما ثبت أنه سليم

| القاعدة | النتيجة | الدليل |
| --- | --- | --- |
| Notes لا تتحول إلى Settings تلقائيًا | سليم | 36 `OrphanNote` محفوظة كأدلة فقط؛ بارامترات `* notes` تُفصل في `ParseConfig` |
| Patch-specific لا يتسرب إلى Game Defaults | سليم | 77 مدخلًا `PatchSpecific` مستبعدة من الكتالوج؛ استثناء واحد فقط مقصود وموثق |
| Version-specific لا تصبح unconditional | سليم | 5 مدخلات `VersionSpecific` مستبعدة (Battlefield 4, Gran Turismo 6 x3, Minecraft) |
| تعليقات القالب لا تصبح إعدادات | سليم | `CleanVal` يزيل `<!-- ... -->` ثم يُسقط القيمة الفارغة |
| Unsupported/Ambiguous تفشل مغلقة | سليم | 46 `Unsupported` + 3 `Ambiguous` + 1 `Rejected`، جميعها بأسباب صريحة |
| تغطية مفاتيح `Template:Config` | سليم | 100 من 101 بارامتر إعداد معروفة (الوحيد غير المعروف `!` وهو رمز جدول ويكي) - **0** `UnknownWikiKey` |
| ZCULL يُترجم لهدفين | سليم | `zcull accuracy` -> `Relaxed ZCULL Sync` + `Accurate ZCULL stats` |
| صحة أسماء مفاتيح RPCS3 | سليم عدا واحد | 69 من 70 هدفًا في `CfgMap` مطابقة حرفيًا لـ`system_config.h` (الاستثناء في F3) |
| Region ambiguity | سليم | 0 حالة `RegionAmbiguous` في النسخة الحالية |

---

## 5. قيود هذا التدقيق

1. **ملف الـsnapshot المثبت غير متوفر.** لا يمكن إعادة توليد `cat.json` بايت ببايت
   ولا فصل "دلتا الويكي" عن "تغيّر المحلل" بدقة كاملة في F9.
2. **لم يُشغَّل كود C# فعليًا.** بيئة التدقيق بلا .NET SDK ومنفذ التنزيل محجوب بسياسة الشبكة.
   الاستنتاجات مبنية على إعادة تنفيذ مُتحقق منها بـ4893 تطابقًا حرفيًا و0 اختلاف في `Risk`.
3. **F1 يثبت التعارض، لا الطرف الصحيح.** أي السياستين هي المطلوبة قرار منتج.
4. **F6 و أجزاء من F3/F5 كامنة**، أي لا تؤثر على أي لعبة في نسخة الويكي الحالية.

---

## 6. الخطوات المقترحة (لم يُنفَّذ منها شيء)

بترتيب الأولوية، وكل واحدة أصغر patch متماسك:

1. **قبل أي تعديل:** شغّل `--wiki-corpus` على الـZIP المثبت لتثبيت الفجوة داخل المشروع نفسه،
   ثم أضِف الأمر إلى `RelTest.ps1` مع `Assert` يربط الناتج بـ`App/Data/cat.json` نفسه.
2. احسم سياسة الأدوار (F1) ثم أعد توليد `cat.json` من الـsnapshot المثبت بالكود الحالي -
   لا تعديل يدوي للكتالوج.
3. صحّح `PPU LLVM Java Mode Handling` -> `PPU Accurate Non-Java Mode` (F3).
4. أضِف الأربعة المفاتيح النشطة القابلة للتمثيل إلى `CfgMap` (F4)، واعرض `firmware libraries`
   كخطوة يدوية إلزامية بدل إسقاطها صامتة.
5. اشتق الحدود الرقمية ونطاقات الـenum من مصدر RPCS3 بدل الأرقام المكتوبة يدويًا (F5, F6).
6. مرّر `WikiRecommendationClass` داخل `CfgChg` واستخدمه في `StateSvc` بدل الاعتماد الوحيد
   على `RecPol` (F7)، وأعد ترتيب فحص `PatDep` قبل `Timing`.
7. اقرأ `config.yml` العام كأساس للمقارنة وطبّق قاعدة "لا Custom Config بدون deviation" (F8).

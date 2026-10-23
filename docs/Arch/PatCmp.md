# طبقة توافق باتشات RPCS3

هذا المستند هو العقد المعماري الوحيد لتحديد توافق الباتش الرسمي وCanary
و`Artemis`/ملفات الغش. لا يجوز لأي مصدر أو ViewModel إنشاء قاعدة توافق خاصة به.

## المرجع من RPCS3

تم اشتقاق الطبقة من مسار التنفيذ نفسه في مستودع RPCS3:

- `Utilities/File.cpp:2218` يحدد data root. وجود `<RPCS3>/portable` له الأولوية،
  ثم `RPCS3_CONFIG_DIR`، ثم مجلد التنفيذ على Windows.
- `rpcs3/Emu/System.cpp:1625` يأخذ `TITLE_ID` من `PARAM.SFO`، والسطر `1630`
  يحدد `APP_VER` الذي يستخدمه المحاكي.
- `Utilities/bin_patch.cpp:1458` يستقبل runtime key، ويرجع فورًا إذا لم يوجد
  المفتاح في خريطة الباتشات (`1462`). بعد ذلك يطابق السيريال (`1488`)، ثم
  إصدار التطبيق (`1506`)، ثم `Enabled` (`1523`).
- `rpcs3/Emu/Cell/PPUModule.cpp:2306` يولد `PPU-<40 hex>`.
- `rpcs3/Emu/Cell/lv2/sys_spu.cpp:204` يولد `SPU-<40 hex>`.
- `rpcs3/Emu/Cell/PPUModule.cpp:1925` يولد `PRX-<Base57>-<segment>`.
- `rpcs3/Emu/Cell/PPUModule.cpp:2999` يولد `OVL-<40 hex>`.
- RPCS3 يجرب أيضًا مفتاحًا بديلًا مسبوقًا بـ`TITLE_ID`، مثل
  `BLUS00001-PPU-...`، في `PPUModule.cpp:2320` و`sys_spu.cpp:221`.

إذًا ترتيب الحماية الفعلي هو:

```text
RPCS3 data root
 -> installed TITLE_ID + APP_VER
 -> exact runtime module key
 -> enabled state in patch_config.yml
 -> execute patch operations
```

## مكونات الطبقة

```text
Rpcs3InstallService
  يحل data root بنفس أولوية RPCS3

GameScanner + PatchHashProbeService
  يبنيان هوية النسخة المثبتة وأدلة PPU/SPU/PRX/OVL

Rpcs3PatchRuntimeKey
  يتحقق من أن مفتاح التعريف مفتاح يمكن أن يولده RPCS3

PatchCompatibilityService
  يطابق serial -> version -> runtime key evidence
  ويرجع PatchCompatibilityDecision

CanaryRuntimeBindingService
  يحافظ على مفتاح can.yml كهوية مصدر، ويولد EffectiveRunKey من PPU المحلي
  فقط عندما يكون تعريف Canary للـserial/version غير ملتبس

PatchActivationPolicy
  facade وحيد تستخدمه المعاينة والتجميع والتفعيل والتعطيل
```

`PatchMatchService` موجود للتوافق مع الاستدعاءات القديمة فقط، ويفوض كامل عمله
إلى `PatchCompatibilityService`.

## بناء هوية النسخة المثبتة

1. السيريال من `TITLE_ID` داخل `PARAM.SFO`، مع `games.yml` كمصدر مسار اللعبة.
2. مسار `/dev_hdd0/` لا يبنى من `DataRoot` مباشرة؛ يحل من `config/vfs.yml`
   وفق `$(EmulatorDir)` و`/dev_hdd0/` كما يفعل RPCS3. إذا كان `vfs.yml`
   مفقودا يستخدم default الخاص بـRPCS3، وإذا كان malformed أو المسار غير محلي
   يفشل probe الخاص بالـHDD مغلقا ولا يخمن مسارا بديلا.
3. الإصدار من `APP_VER`. `CATEGORY=GD` بنفس `TITLE_ID` تحت `/dev_hdd0/game`
   المحلول يمكن أن يرفع نسخة لعبة `CATEGORY=DG` المثبتة عندما تكون أحدث، لكنه
   لا ينشئ لعبة مثبتة مستقلة. `TITLE_ID` المخالف لا يشارك في الدمج.
4. بصمة PPU من أحدث boot session مطابق للسيريال أو من مجلد كاش اللعبة.
5. أسماء كاش PPU تستخدم Base57 مثل `ppu-<Base57>-EBOOT.BIN`. تفك Base57 إلى
   SHA-1 ذي 40 خانة قبل المقارنة، لكنها لا تتحول إلى مفتاح باتش Base57؛ RPCS3
   يولد مفتاح PPU التنفيذي بصيغة hex.
   أحدث cache خاص بـ`EBOOT.BIN` فقط يصبح دليل PPU رئيسيًا authoritative إذا
   كان أحدث من ملف `EBOOT.BIN` المثبت. بقية عناصر الكاش تبقى historical
   evidence ولا تستخدم لإثبات تعارض النسخة الحالية.
6. أدلة SPU وPRX وOVL تؤخذ من السطور التي يطبعها RPCS3 داخل boot session
   المطابق للسيريال.
7. تبقى كل البصمات الصحيحة، ولا يختزل الدليل إلى أول قيمة.

كل probes محدودة الحجم وعدد عناصر الكاش، ومسار القراءة لا ينشئ ملفات.

## القرار الموحد

`PatchCompatibilityDecision` يعيد verdict وreason code و`CanEnable` و
`CanDisable` واسم runtime key الفعلي.

`Blocked` يحكم التفعيل فقط. إذا كان مدخل الباتش مفعّلًا من قبل وما زالت هويته
في `patch_config.yml` قابلة للبناء بأمان، يبقى `CanDisable=true` حتى عند تغير
إصدار اللعبة أو بصمة PPU؛ فلا يحبس التطبيق المستخدم داخل إعداد قديم غير متوافق.

| الحالة | القرار | السبب |
|---|---|---|
| serial + version + runtime hash متطابقة | `Compatible` | تطابق مثبت محليًا |
| serial/version مطابقان، المفتاح صالح، والمصدر غير Canary، ولا يوجد تعارض قطعي | `RuntimeGuarded` | RPCS3 يطبق فقط المفتاح المطابق عند boot |
| bundled Canary بلا PPU محلي محفوظ | `Blocked` | يلزم التقاط محلي مرة واحدة |
| bundled Canary + PPU محلي + تعريف وحيد للـserial/version/title | `Compatible` مع `EffectiveRunKey` محلي | يجهز التعريف canonical تحت مفتاح PPU المكتشف |
| bundled Canary + عدة bodies مختلفة ولا يوجد exact variant محلي | `Blocked` | ربط تلقائي ملتبس |
| الإصدار مختلف | `Blocked` | الباتش مخصص لإصدار آخر |
| بصمة PPU الرئيسية الموثوقة مختلفة | `Blocked` | نسخة التنفيذ مختلفة |
| runtime key غير صالح | `Blocked` | RPCS3 لن يولد هذا المفتاح |
| مفتاح `TITLE_ID-...` لسيريال آخر | `Blocked` | نطاق المفتاح مختلف |
| المصدر مجهول والبصمة المحلية غير موجودة | `Blocked` | لا دليل كافٍ للثقة بالحارس المؤجل |
| هدف `All/All` بلا تطابق hash مثبت | `Blocked` | لا يمكن نسبه للعبة المختارة |

### عقد Canary المحلي

قاعدة `can.yml` مصدر canonical ولا تتغير على جهاز المستخدم. عند أول محاولة
لاستخدام Canary للعبة محددة، يسجل R3Config baseline لمساري السجل المركزيين،
يشغل اللعبة، وينتظر `SYS: Serial` المطابق ثم `PPU executable hash` جديدا في الجزء الجديد
من السجل. يبدأ مؤقت 60 ثانية عند هذا الإثبات فقط. بعد انتهاء المدة يغلق العملية
التي يملكها، يقرأ `PPU executable hash` بعد الإغلاق، ويحفظ الربط المحلي مرة واحدة.

عند التحليل التالي يبقى `Rpcs3PatchEntry.RunKey` هو مفتاح تعريف المصدر داخل
`can.yml` حتى يستطيع compiler إعادة إيجاد body المراجع. إذا كان تعريف المصدر
للـserial/version/title وحيدًا، ينتج matcher `PatMatch.EffectiveRunKey` من PPU
المحلي. `PatchSelectionCompiler` ينسخ body نفسه من المصدر لكنه يخرجه تحت
`EffectiveRunKey` في managed `imported_patch.yml`، وplanner يستخدم المفتاح نفسه في
`patch_config.yml`. إذا كانت هناك عدة bodies مختلفة لنفس الهدف ولم يطابق PPU
المحلي أحد مفاتيحها الأصلية، لا يختار R3Config offsets عشوائيًا.

### لماذا لا نعتبر كل بصمة مختلفة تعارضًا؟

- PPU الرئيسي يعرّف build اللعبة؛ لذلك دليل log/metadata المختلف تعارض قطعي.
- مجلدات الكاش قد تحتفظ ببصمات builds قديمة. cache أقدم من `EBOOT.BIN` دليل
  إيجابي عند التطابق فقط، ولا يستخدم للرفض. cache `EBOOT.BIN` الأحدث من الملف
  المثبت يمثل آخر هوية شغلها RPCS3 ويمكنه إثبات التطابق أو التعارض.
- SPU وPRX وOVL وحدات متعددة وقد تُحمّل لاحقًا. مشاهدة وحدة مختلفة لا تعني أن
  الوحدة المطلوبة غير موجودة. التطابق الإيجابي يصبح `Compatible`، وإلا يبقى
  القرار `RuntimeGuarded` ويقوم RPCS3 بالحسم عند تحميل الوحدة.

هذا يمنع الحالتين الخطرتين: تشغيل patch على build متعارض، وإظهار «غير متوافق»
كاذبة لمجرد أن الوحدة لم تُحمّل بعد.

## المصادر

المصدر يحدد provenance وmerge precedence فقط، ولا يغير خوارزمية التوافق:

- `Official`: ملف RPCS3 الرسمي `patch.yml`.
- `Imported`: `imported_patch.yml` المقيم لدى المستخدم.
- `BundledCanary`: قاعدة Canary المشحونة مع التطبيق.
- `Artemis`: كتالوج شفرات أرتميس المشحون.
- `ManagedArtemis`: ملف شفرات أرتميس الذي جهزه R3Config للسيريال.
- `TitleSpecific`: أي `<TITLE_ID>_patch.yml` مقيم عند RPCS3.

كلها تمر عبر reader ثم merge ثم `PatchCompatibilityService` ثم نفس planner
وtransaction. `Unknown` يحتاج تطابق hash محليًا ولا يستخدم runtime guard.

## عقود لا يجوز كسرها

- لا تستخدم العنوان النصي للعبة لإثبات التوافق.
- لا تستخدم risk أو اسم الباتش كدليل توافق.
- لا تعد بناء hash key يدويًا خارج `Rpcs3PatchRuntimeKey` أو
  `Rpcs3PatchEntry.RuntimeHashKey`.
- لا تعتبر وجود الباتش في قاعدة RPCS3 إثباتًا أنه يخص النسخة المثبتة.
- لا تعتبر SPU/PRX/OVL غير المشاهد mismatch.
- لا تسمح لـExpert mode بتجاوز قرار الطبقة.
- لا يكتب preview أو probe أي ملف.
- bundled Canary لا يستخدم unknown-hash runtime guard بديلًا عن local capture.
- لا تعدل `can.yml` عند الربط؛ أخرج النسخة المجهزة فقط إلى managed `imported_patch.yml`.
- التفعيل يكتب enabled state فقط بعد اختيار المستخدم وmutation guard وtransaction.

## إضافة مصدر باتش جديد

المصدر الجديد غير مكتمل حتى يحقق الخطوات التالية:

1. provenance صريح في `PatchSourceOrigin`.
2. parsing ينتج `Rpcs3PatchEntry` ومفتاح runtime صالحًا.
3. merge precedence وتشخيص conflict.
4. المرور عبر `PatchCompatibilityService` بلا استثناء خاص بالمصدر.
5. compiler/preparation يحافظ على `RuntimeHashKey` نفسه.
6. planner يستخدم `PatchActivationPolicy`.
7. UI يعرض reason code مترجمًا ولا يعرض «متوافق» من تلقاء نفسه.
8. اختبارات exact/runtime-guarded/version mismatch/PPU mismatch/unknown source.
9. اختبار ملف مؤقت للتفعيل والتعطيل والrollback، دون تعديل تثبيت حقيقي.

## اختبارات القبول

الحد الأدنى لأي تغيير في الطبقة:

- PPU/SPU/OVL hex وPRX Base57+segment.
- مفاتيح `TITLE_ID-...` المطابقة والمخالفة.
- portable و`RPCS3_CONFIG_DIR` والجذر العادي.
- مصدر رسمي وCanary وcheat يعطون القرار نفسه لنفس الهوية.
- بصمة PPU من log وبصمة PPU Base57 من cache.
- SPU/PRX/OVL من boot session.
- version unknown/mismatch، hash mismatch، global target، unknown source.
- عدم تغير `patch.yml` و`imported_patch.yml` و`patch_config.yml` أثناء التحليل.

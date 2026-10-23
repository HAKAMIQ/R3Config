# الاختبار والنشر

## Runner

`Tst/R3Test.csproj` console smoke/regression runner. `Tst/Program.cs` هو نقطة
الدخول واختيار أوضاع التشغيل فقط. `Tst/TestRunner.cs` يملك اسم الاختبار الجاري
وتنفيذ `RunCase`/`RunValue` وتقرير الفشل، و`Tst/TestContext.cs` يملك دورة حياة
مجلد الاختبار المؤقت، و`Tst/TestAssert.cs` يملك مساعدات assertions العامة،
و`Tst/TestFixtures.cs` يملك الـfixtures والبنى المشتركة الحتمية. اختبارات
الـsmoke/regression القديمة مقسمة حسب المسؤولية داخل `Tst/` في ملفات
`Program.<area>.cs`. كل method جديد يجب أن يُستدعى من sequence واضح أو suite
`Run()`؛ وجوده وحده لا يشغله. يمنع عقد المعمارية رجوع `Program.cs` إلى monolith،
إعادة runner/context/assert state إليه، أو تضخم أي partial suite خارج الحد المتفق عليه.

كل mutation test يستخدم temp RPCS3 tree. المسار الحقيقي، إن مُرر للـ runner،
مخصص لعقد integration read-only فقط.

## عقد toolchain

`global.json` ملف مصدر دائم وليس ملفا مؤقتا للاختبارات. هو مصدر الحقيقة لنسخة
SDK الدقيقة ويستخدم `rollForward=disable`. كل سكربت .NET داخل المستودع يعمل من
repo root حتى يكون حل SDK حتميا. `Script/R3Toolchain.ps1` يملك التحقق من SDK
وتشغيل dotnet وتشغيل PowerShell child processes وتشغيل executables؛ لا تكرر هذه
الوظائف في كل سكربت.

عقد R3Config الحالي workload-free: المشاريع تستخدم `net8.0` و
`net8.0-windows`/WPF فقط. لذلك `R3Toolchain.ps1` يعطل
`MSBuildEnableWorkloadResolver` داخل كل استدعاء dotnet فقط ثم يعيد قيمة البيئة
السابقة في `finally`. هذا يعزل المشروع عن workload manifests المثبتة على الجهاز
لمشاريع اخرى. قبل التعطيل يفحص الـtoolchain كل `*.csproj` ويفشل اذا ظهر
MAUI/Android/iOS/macCatalyst/tvOS/browser/WASM؛ عندها يتغير العقد صراحة بدل اخفاء
workload حقيقي.

`NameChk` اداة build-time وليست منتجا منشورا. يبنى الى DLL ثابت `net8.0` مع
`UseAppHost=false` ثم يشغل بواسطة `dotnet exec`. build output وintermediate output
يوجهان الى system temp بواسطة MSBuild properties ثم يحذفان بعد التنفيذ. قيم
MSBuild directory properties تمر حصرا عبر
`Get-R3MsBuildDirectoryPropertyValue` في `R3Toolchain.ps1` حتى تكون absolute وتنتهي
بـdirectory separator كما يفرض MSBuild. لا تبن هذه القيم يدويا داخل gate ولا تستخدم
`--artifacts-path` لهذا gate لان مسار apphost قد ينفصل عن output الفعلي للمشروع.

## بوابات corpus وprovenance

مصدر الإنتاج canonical للـWiki هو `RPCS3+Wiki-20260920160231.xml` بتاريخ
`2026-09-20` وبصمة SHA-256
`7977CA26176E12DD167241CDE77D68E6CC64CE24CB39C2682FD5E686CA7936C5`
(المثبتة في `WikiSvc.CanonicalSnapshotSha256`). رُقّي Current Wiki من 2026-09-07
بطلب مالك المشروع بعد إثبات الفرق بالـgenerator ومطابقة توليدين بايتًا ببايت.
`App/Data/cat.json` و`App/Data/wiki-notes.json` مولدان معًا من هذا الملف؛
parity الحالية: 8542 سيريالًا و7794 إعدادًا و0 فروق. القيم الملتبسة الأربع وتعارضا
السيريال مستبعدة من الإعدادات. انظر [ملاحظات الويكي](WikiEvidence.md).
أي snapshot أحدث يبقى `NEWER_EVIDENCE` فقط ولا يصبح canonical بمجرد تمريره إلى gate.

`RelTest` يفصل المسارين:

- `-WikiXml <path>` يشغل `R3Test.exe --wiki-verify <path>`. إن لم يمرر يستخدم
  `RelTest.ps1` تلقائيًا مصدر الإنتاج المثبت
  `F:\Downloaded-Websites\rpcs3\Wiki-Snapshots\Candidate\RPCS3+Wiki-20260920160231.xml` بعد التحقق من SHA-256.
  هذا الوضع يرفض أي XML لا يطابق هوية September pinned، ويتحقق أن `App/Data/cat.json`
  يحمل September `DataVersion`/`SourceHash`/`GeneratedAt` قبل مقارنة parity، ثم
  يتحقق أن `App/Data/wiki-notes.json` يحمل المصدر نفسه (hash وتاريخ واسم الملف) ويطابق
  بايتًا ببايت ما يولده هذا المصدر. اختلاف واحد يفشل `WIKI_CANONICAL_PARITY_GATE`.
- `-WikiEvidenceXml <path>` يشغل `R3Test.exe --wiki-delta <path>`. يجب أن يكون
  المصدر أحدث من September وألا يملك hash المصدر canonical. النتيجة تقرير delta فقط؛
  لا يشترط `Differences=0` ولا يعدل provenance أو baseline.

إذا لم يوجد ملف مصدر الإنتاج في مساره الافتراضي ولم يمرر `-WikiXml` يطبع `RelTest`
`WIKI_CANONICAL_PARITY_GATE: BLOCKED/SKIPPED` صراحة لأن غياب September XML يمنع
byte-for-byte regeneration. لا يجوز استخدام snapshot مختلف بدلا منه، ولا يعني نجاح
باقي الاختبارات أن shipped catalog canonical.

إعادة بناء catalog canonical تتم فقط من snapshot يمر بهوية September:

```powershell
.\Tst\bin\Release\net8.0-windows\R3Test.exe --wiki-rebuild <september-snapshot.xml> <out.json>
```

`--wiki-rebuild` و`--wiki-verify` و`--wiki-delta` لا يعدلان ملفات المستودع؛ rebuild
يكتب فقط إلى مسار output الممرر.

بوابتا الباتشات الرسمية منفصلتان:

- `OFFICIAL_RUNTIME_COMPATIBILITY_GATE` **إلزامية**. يشغلها `RelTest` دائمًا عبر
  `R3Test.exe --official-runtime-only` ولا تحتاج corpus خارجيًا ولا تقرأ `patch.yml`
  المثبت على الجهاز. تتحقق من الضمانات الحالية للإنتاج: `patch.yml` الخاص بـRPCS3
  للقراءة فقط، وتحليل schema 1.2 يفشل مغلقًا، والبنى غير المدعومة/المجهولة صريحة، وتطابق
  Serial/الإصدار/الـhash صارم، وUnknown لا يساوي Mismatch، وحالة `patch_config.yml`
  محفوظة، والباتشات الرسمية معزولة عن Wiki وCanary وArtemis والمستوردة.
- `OFFICIAL_HISTORICAL_CORPUS_REPLAY` **اختيارية**. `OfficialCorpusT` له identity pinned
  مستقلة (`ExpectedSnapshotDate=2026-08-25`, 897858 bytes، SHA-256
  `45F746AFC3D4EEA2450F32F92769BB555029807497CFF3512B633428EF74E8AD`).
  `RelTest -OfficialPatch <path>` يشغله فقط عند توفير المصدر المطابق. الملف التاريخي
  غير متوفر حاليًا، فتطبع `OFFICIAL_HISTORICAL_CORPUS_REPLAY: NOT_AVAILABLE` وغيابه
  لا يمنع الإصدار. ملف `patch.yml` من
RPCS3 المثبت أو الأحدث لا يصبح canonical تلقائيا ولا يحدّث baseline.

## بيانات بناءات RPCS3 وبيئة التشغيل

- `Rpcs3BuildCatalogT` يتحقق من `App/Data/rpcs3-builds.json` من مخرجات البناء وحدها (لا مجلد تطوير ولا شبكة): صف واحد لكل
  Version+Build وفئة مصدر، كل صف GitHub رابط رسمي يحل إلى نفسه، وكل صف Archive مرجع موثق بلا تنزيل، والتحكم الحقيقي
  `0.0.30-16004` و`0.0.29-15565` (OFFICIAL_URL_MATCH)، وغياب أي مسار محرك تطوير. الأعداد تُطبع ولا تُثبَّت كأرقام في الاختبار.
- `RuntimeContextT` و`RuntimeViewUiT` يغطيان المجال الخامس (حالات الدليل، شروط العتاد، تدفق السياق إلى إعدادات اللعبة، العربية/الإنجليزية،
  الإخفاء الافتراضي للتفاصيل، ومالك واحد لفحص العتاد).
- `HostCpuT` يغطي سياق المعالج بـfixtures اصطناعية (x64 Intel/AMD وهجين وARM64 وبيانات ناقصة والمحاكاة وتحليل بنية النوى وانطباق
  إعدادات RPCS3 وتكامل السياق والعربية)، و`HostCpuT.RunRealHost` اختبار مفصول يقرأ الجهاز الحقيقي ويؤكد invariants فقط.
- `LogDiagT` (المحلل والتوقيعات وقواعد التشخيص والقراءة للقراءة فقط وقاعدة الحالات المشحونة) و`LogDiagViewUiT` (تبويبات
  العرض الأربعة بالعربية والإنجليزية) يغطيان تشخيص السجل؛ `R3Test.exe --log-diag-only` يشغل الأول وحده،
  و`R3Test.exe --log-diag-real <log|log.gz> [serial]` يحلل سجلًا حقيقيًا للقراءة فقط ويطبع الأحداث والتشخيص، و
  `R3Test.exe --gen-diagnostics-kb <indexes> <attachments> <out.json>` يولد قاعدة الحالات (تطوير فقط).
- توليد الكتالوج (تطوير فقط): `R3Test.exe --gen-build-catalog <master.json> <github.csv> <out.json>`.
- قبول قراءة فقط على تثبيت حقيقي: `R3Test.exe --runtime-context-real <rpcs3.exe> [serial]` يشغل مسار التحليل الإنتاجي ويطبع لقطة
  السياق، ويفشل إذا تغير أي ملف config/patches أثناء التشغيل.

## اوامر القبول

```powershell
.\Script\RelTest.cmd -Configuration Release
```

`R3Cfg.slnx` هو solution metadata وليس CLI acceptance target تحت SDK المثبت حاليا.
`Tst/R3Test.csproj` هو جذر verification graph: يشير الى App وCore وArtImport، وApp
يشير الى Core وLangPack. عقد المعمارية يثبت ان هذا الـclosure يساوي مشاريع
`R3Cfg.slnx` حتى لا يسقط مشروع من البناء بصمت.

اختبار اصدار كامل:

```powershell
.\Script\RelTest.cmd -Configuration Release
```

`RelTest` يشغل restore/build على `Tst/R3Test.csproj` ثم `R3Test.exe` الفعلي ثم
`SrcChk`. لا يستخدم `dotnet run` لتشغيل test harness ولا يمرر `R3Cfg.slnx` الى
.NET CLI تحت الـSDK المثبت. wrappers بصيغة CMD هي واجهة Windows المفضلة
لانها تطبق `-NoProfile -ExecutionPolicy Bypass` بصورة ثابتة على PowerShell 5.1.
`PortPub.ps1` لا يعيد اختراع acceptance sequence؛ يستدعي `RelTest.ps1` اولا مع
مسار RPCS3 الحقيقي للقراءة فقط، ثم ينتقل الى restore الخاص بالـruntime وpublish.

portable release يحتاج `-Rpcs3Path` لتشغيل عقد التثبيت الحقيقي read-only.

النشر النهائي يرفض الحزمة غير الموقعة ويحتاج شهادة Code Signing صالحة عبر
`-SigningCertificateThumbprint` أو المتغير `R3CONFIG_SIGNING_CERT_THUMBPRINT`.
يجب توفر `signtool.exe` من Windows SDK؛ يستخدم SHA-256 وRFC 3161 عبر `/tr`
و`/td SHA256`. بعد ذلك يُوقّع `SHA256SUMS.txt` كتوقيع CMS منفصل
`SHA256SUMS.p7s` وتتحقق بوابة الإصدار من تطابق بصمة الموقّع للملفين.

## Architecture gates الحالية

`RunArchitectureBoundaryTests` يثبت:

- Core بلا WPF/Lang/Vm/View dependencies؛
- App/Application بلا WPF/ViewModel/View/UiDlg/UiText؛
- ViewModels بلا `File.WriteAllText/Move/Replace/Delete`؛
- MainVm موزع على partial files محدودة الحجم؛
- R3Test يبقي CLI routing في `Program.cs` ويفصل runner/context/assert/fixture infrastructure في ملفات مستقلة محدودة الحجم.

`RunMutationGuardTests` يثبت:

- targets داخل config/patches مقبولة؛
- data root خارجي صحيح من `RPCS3_CONFIG_DIR` مقبول؛
- target خارج `config`/`patches` المعلنين أو يخالف layout الخاص بـdata root مرفوض؛
- duplicate target مرفوض؛
- تشغيل RPCS3 يمنع mutation؛
- guarded use case لا ينشئ ملفًا عند الحجب.

لا تحذف هذه الاختبارات لتسهيل refactor. إذا تغير العقد، يلزم قرار معماري موثق.

`Security_Hardening` يثبت:

- عدم وجود PowerShell في Hardware probe؛
- رفض UNC/SMB وWindows device paths؛
- تحميل اللغة من مجلد التطبيق فقط مع حدود JSON ومنع duplicate keys؛
- single-instance gate وpath-derived writer mutex لكل data root؛
- exclusive staging مع فحص canonical/reparse متكرر؛
- منع XML DTD/entity expansion؛
- إلزام release signing وRFC 3161 وsigned manifest.

`RunSecurityClosure` (`Tst/Program.SecurityClosure.cs`، ويمكن تشغيله منفردًا بـ
`R3Test.exe --security-closure-only`) يثبت:

- `patch.yml` يبقى read-only عند فتح جذر RPCS3 عبر junction؛
- writer lock يبقى مملوكًا بعد انتهاء الـthread الذي فحص أول mutation، ويحجب مالكًا
  آخر أو object بنفس الاسم بـ`MutBlock.Locked` دون إسقاط العملية؛
- فشل config/patch قبل commit لا يترك backup snapshot يتيمًا؛
- تصدير الكتالوج يكتب `.json` فقط ولا يكتب عبر hardlink؛
- `PortChk.ps1` يشترط كل ملف منشور في `App/R3App.csproj` ونص GPL-3.0 في `LICENSE`؛
- سكربتات المستودع لا تحمل مسار user profile أو drive ثابتًا.

## Matrix

| المجال | الاختبارات المطلوبة |
|---|---|
| Install/scan | wrong exe، missing، malformed games.yml/SFO، duplicates |
| Hash probe | plain/gzip/cache، bounds، multiple PPU/SPU، ranking |
| Config plan | states/risks/hardware/current=proposed/unreadable |
| Config mutation | preserve unrelated، backup، atomic failure، restore |
| Patch parser | 1.2 schema، types، All، duplicate، partial warnings |
| Merge | resident/bundled، higher/lower/equal، definition conflict |
| Match | serial/version/hash/All/unknown/mismatch/PRX-OVL boundary |
| Compiler | selected-only، dependencies، cycles، groups، entry count |
| Transaction | stage validation، mid-commit failure، rollback، absent target |
| H-Canary | validation، anchors، evidence، round trip، save/delete transaction |
| UI | key parity، XAML smoke، live-language state preservation |
| Architecture | dependency direction، composition، no VM file writes |
| Shipped data | parse، provenance، hash، csproj copy، release required files |

## Test data

- unique system temp directory؛
- minimal RPCS3 tree؛
- deterministic sorting، لا تعتمد على filesystem order؛
- small readable fixtures؛
- generate boundary data بدل تخزين payload ضخم؛
- injectable failpoints للـ writers/transactions؛
- assert before/after file content، لا exception فقط.

## Source verifier

يرفض:

- build/publish folders داخل strict source package؛
- nested ZIP؛
- ملف بيانات التطبيق القديم `cbak.json`؛
- hard-coded local Windows paths؛
- cheats catalog بلا notice/hash/license/provenance lines المطلوبة.

لا تعدل verifier لإخفاء provenance ناقص.

المصدر يرفض أيضًا مواد المفاتيح والأسرار: `.pem/.pfx/.p12/.p8/.key/.snk`، وملفات `.env`/`.dev.vars`،
و`node_modules`، وأي PEM لمفتاح خاص بمحتواه.

`SrcExp.ps1` يستبعد ZIP files عامة، لكن `App/Data/ncl.zip` استثناء shipped وحيد
ومثبت بالـhash/provenance. source handoff يجب ان يحتويه؛ اي ZIP اخر يبقى مرفوضا.

## Portable verifier

يتحقق من required files، executable version، غياب source/debug/emulator payload،
language parity، catalog non-empty، cheats notice، و`SHA256SUMS`.
ويرفض مفاتيح خاصة أو قواعد بيانات محلية أو ملفات المستخدم (`settings.json`، `ui.json`، `hdis.json`)،
ومسارات المطور. فحص الـmanifest
ثنائي الاتجاه: كل سطر يشير إلى ملف داخل مجلد الإصدار وبصمة صحيحة بلا تكرار، وكل
ملف إصدار عدا manifest نفسه يجب أن يكون مدرجًا.

عند إضافة shipped file: csproj + verifier + license/provenance + publish-directory
test، وليس bin test فقط.

## Definition of done

- Release build: 0 errors ويفضل 0 warnings؛
- runner ينتهي بـ `Smoke tests passed.`؛
- architecture/mutation gates ناجحة؛
- source gate ناجح أو failure سابق غير ناتج عن التغيير موثق؛
- release gate إذا تغير packaging/data/version؛
- لا real-install mutation؛
- لا bin/obj/artifacts/local paths في diff.

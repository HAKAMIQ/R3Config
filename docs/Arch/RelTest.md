# الاختبار والنشر

## Runner

`Tst/R3Test.csproj` console smoke/regression runner. كل method جديد يجب أن
يُستدعى من top-level sequence أو suite `Run()`؛ وجوده وحده لا يشغله.

كل mutation test يستخدم temp RPCS3 tree. المسار الحقيقي، إن مُرر للـ runner،
مخصص لعقد integration read-only فقط.

## أوامر القبول

```powershell
dotnet restore .\R3Cfg.slnx
dotnet build .\R3Cfg.slnx -c Release --no-restore --nologo
dotnet run --project .\Tst\R3Test.csproj -c Release --no-build
powershell -ExecutionPolicy Bypass -File .\Script\SrcChk.ps1
```

اختبار إصدار:

```powershell
powershell -ExecutionPolicy Bypass -File .\Script\RelTest.ps1 -Configuration Release
```

portable release يحتاج `-Rpcs3Path` لتشغيل عقد التثبيت الحقيقي read-only.

## Architecture gates الحالية

`RunArchitectureBoundaryTests` يثبت:

- Core بلا WPF/Lang/Vm/View dependencies؛
- App/Application بلا WPF/ViewModel/View/UiDlg/UiText؛
- ViewModels بلا `File.WriteAllText/Move/Replace/Delete`؛
- MainVm موزع على partial files محدودة الحجم.

`RunMutationGuardTests` يثبت:

- targets داخل config/patches مقبولة؛
- data root خارجي صحيح من `RPCS3_CONFIG_DIR` مقبول؛
- target خارج `config`/`patches` المعلنين أو يخالف layout الخاص بـdata root مرفوض؛
- duplicate target مرفوض؛
- تشغيل RPCS3 يمنع mutation؛
- guarded use case لا ينشئ ملفًا عند الحجب.

لا تحذف هذه الاختبارات لتسهيل refactor. إذا تغير العقد، يلزم قرار معماري موثق.

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

## Portable verifier

يتحقق من required files، executable version، غياب source/debug/emulator payload،
language parity، catalog non-empty، cheats notice، و`SHA256SUMS`.

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

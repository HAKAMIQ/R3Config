# تدقيق سلوك RPCS3 تجاه المعالج (مصدر مجمد)

هذا المستند هو الأساس الموثق لحقائق CPU في [بيئة التشغيل والتوافق](RuntimeContext.md). كل بند مأخوذ من الكود لا من الويكي
ولا من الذاكرة. المرجع للقراءة فقط:

- المستودع: `https://github.com/RPCS3/rpcs3.git`، الفرع `master`
- `HEAD` المجمد: `9e86f165d1711b9429d48b0487e7bc5ba0cc9c6c` (ثابت في `Rpcs3CpuSemantics.FrozenHead`)
- المسارات أدناه نسبية لمجلد `rpcs3` داخل المصدر؛ `Utilities/` نسبي لجذر المستودع.

**Frozen RPCS3 HEAD: `9e86f165d1711b9429d48b0487e7bc5ba0cc9c6c`.** المصدر المجمد ليس binary RPCS3 المثبت عند المستخدم: ما يخص
البناء المثبت (مفاتيح الترجمة مثل `LLVM_AVAILABLE`) لا يُستنتج منه.

درجات الدليل:

- `SRC` (`DIRECT_SOURCE_FACT`): حقيقة أو قاعدة موجودة مباشرة في الكود.
- `FLOW` (`DERIVED_FROM_CONTROL_FLOW`): مشتقة من control flow مباشر (مثل تخطيط المجدول من brand+SMT+عدد الخيوط).
- `HOST` (`HOST_RUNTIME_DETECTABLE`): قراءة مستقلة من Windows تؤدي الغرض الوظيفي، وليست نسخة حرفية من مسار RPCS3.
- `RPCS3` (`RPCS3_RUNTIME_DETECTABLE`): يمكن قراءتها من سجل RPCS3 أو إعداداته أو ملفه التنفيذي.
- `NOT_DETECTABLE`: موجودة في المصدر ولا طريق آمن لقراءتها من R3Config.
- `UNKNOWN`: لا دليل.

في R3Config قاعدة كل ميزة `SRC` وقراءتها `HOST`؛ `CpuFeatureFact.Basis` يسجل ذلك: `SourceRuleCpuid` (bits من CPUID على المضيف)،
`SourceRuleCpuidWithHostXState` (مع حالة المتجهات التي يفعلها نظام Windows)، `SourceRuleHostApi` (`IsProcessorFeaturePresent`، وهي
الدالة التي يستدعيها RPCS3 نفسه على ARM64).

## المعمارية

`SRC` الاختيار وقت الترجمة فقط: `ARCH_X64` أو `ARCH_ARM64` (`util/sysinfo.cpp`: `utils::get_architecture()` تعيد `x64` أو
`arm64` أو `unknown`). لا تبديل وقت التشغيل. السجل يطبع `Architecture: <x64|arm64>` وR3Config يقرؤه أصلا
(`Rpcs3BuildIdentityParser`).

| | x64 | ARM64 |
|---|---|---|
| PPU | Interpreter (static)، Recompiler (LLVM) | نفسه |
| SPU | Interpreter (static/dynamic)، Recompiler (ASMJIT)، Recompiler (LLVM) | Interpreter (static/dynamic)، Recompiler (LLVM) |

`FLOW` مصنع الـrecompiler في `Emu/Cell/SPUCommonRecompiler.cpp` ينشئ ASMJIT تحت `ARCH_X64` فقط؛ وفي `Emu/System.cpp`
`fixup_settings()` يستبدل `SPU Decoder = Recompiler (ASMJIT)` عند البدء على ARM64 بـ`Interpreter (dynamic)` إن كان
`LLVM_AVAILABLE` وإلا `Interpreter (static)`. `LLVM_AVAILABLE` مفتاح ترجمة للبناء المثبت (`RPCS3` من حيث المبدأ: `Emulator::Init` يسجل في قناة SYS إما
`LLVM version: Compiled without LLVM` أو إصدار LLVM). R3Config لا يقرأ هذا السطر بعد، فحالته `BuildCapabilityUnknown` ولا تُستنتج من
المعمارية ولا من وجود مصدر LLVM ولا من هذا المصدر المجمد. المفككات التي تعتمد عليه: `Interpreter (dynamic)` و`Recompiler (LLVM)`
(`settings_dialog.cpp` يعطلها بدونه)، وكذلك `Recompiler (LLVM)` في PPU. ما لا يعتمد عليه: `Interpreter (static)`، وASMJIT على x64.
جدول القيم أعلاه يصف ما **يعرّفه المصدر** لكل معمارية، لا ما يحمله البناء المثبت ولا الإعداد الحالي للمستخدم.

## اكتشاف المعالج المضيف

`SRC` `util/sysinfo.cpp` يقرأ فقط ما يلي. لا vendor ولا sockets ولا NUMA ولا cache ولا P/E في هذا الملف.

| الحقل | المصدر في RPCS3 | R3Config |
|---|---|---|
| brand | `get_cpu_brand()`: CPUID `0x80000002..4` على x64، `aarch64::get_cpu_brand()` على ARM64 | `HOST` على x64 (نفس التطبيع)؛ ARM64 غير مستنسخ |
| عدد الخيوط | `get_thread_count()` = `GetNativeSystemInfo().dwNumberOfProcessors` (مجموعة المعالجة الحالية فقط) | `HOST` `CpuNativeThreads` + `ProcessorGroups` |
| family/model | `get_cpu_family/model()` (CPUID 1.EAX) | غير مستخدم في R3Config (يدخل فقط جدول أقنعة AMD) |
| TSC | `s_tsc_freq`: معايرة عند التشغيل، تحتاج invariant TSC | `NOT_DETECTABLE` (النتيجة قياس وقت تشغيل) |

`SRC` لا حقل لـ efficiency class ولا hybrid ولا NUMA في كل `rpcs3/` و`Utilities/`. الاستثناء الوحيد نص وسم macOS ARM64
(`AArch64Common.cpp`: `(%lluP+%lluE)`) وهو عرض فقط. لذلك `CpuHybrid` في R3Config حقيقة عن الجهاز يعرضها ولا يبني عليها.

## ميزات المعالج (x64)

`SRC` كل ميزة قاعدة CPUID/XGETBV في `sysinfo.cpp`، وR3Config يطبق قاعدة CPUID نفسها في `HostCpuAnalyzer`.
AVX/AVX2/AVX-512/ICL/XOP/`fast_vperm2b` تشترط حالة المتجهات المفعلة من نظام التشغيل؛ RPCS3 يقرؤها بـ`XGETBV(0)`، أما R3Config فيأخذها
من `GetEnabledXStateFeatures` (`HOST`): دليل مستقل يؤدي الغرض نفسه، ولا يُدعى تكافؤه الحرفي مع تعليمة RPCS3. إن لم يتوفر جواب
هذا الـAPI أو فشل، تكون هذه الميزات `Unknown` (وتكون `Absent` فقط إذا كانت bits الـCPUID نفسها سالبة).
هذه القائمة **متتبعة وليست كاملة**: `Rpcs3CpuSemantics.UntrackedProbes` يذكر ما لم يُمثل (`has_mpx`, `has_tsx_force_abort`,
`has_rtm_always_abort`, `has_avx512_vnni`, `has_avx10`, `has_um_wait`, `has_appropriate_um_wait`) وسبب كل منها.
المستهلكون في `Rpcs3CpuSemantics.Features` مع الملفات. الملخص:

| الميزة | الأثر الفعلي |
|---|---|
| SSSE3/SSE4.1 | مسارات تنفيذ في PPUInterpreter وSPU ASMJIT وBufferUtils وsimd.hpp وJITASM |
| AVX/AVX2/AVX-512/ICL | مسارات SPU ASMJIT وroutines الذاكرة؛ واختيار اسم LLVM CPU (`jit_compiler::cpu`: خفض إلى `nehalem` بلا AVX، وإلى `skylake` بلا AVX-512) |
| XOP | مسارات SPU ASMJIT |
| CLWB | رفع `znver1` إلى `znver2` في `jit_compiler::cpu` |
| ERMS/FSRM | عتبة `rep movsb` في `SPUThread.cpp` |
| WAITX/WAITPKG | انتظار المستخدم (`asm.hpp`, `lv2.cpp`, `SPUThread.cpp`)؛ `has_appropriate_um_wait` تشترط 8 خيوط على Linux و12 على غيره ومعايرة TSC |
| Invariant TSC | شرط معايرة TSC ثم كل مستهلكي `get_tsc_freq()` |
| `fast_vperm2b` (مشتقة) | `SRC` `has_avx512()` مع VBMI (CPUID 7.ECX[1]) وSSE4A (CPUID 0x80000001.ECX[6])؛ المستهلك `cpu_translator::vperm2b` في `Emu/CPU/CPUTranslator.h` (اختيار lowering في مترجم LLVM). تُتتبع كميزة لأن لها مستهلك تنفيذ مباشر وتُحسب من CPUID مباشرة |
| FMA3/FMA4 | تعطيل خانة Use Accurate DFMA في نافذة إعدادات Qt فقط، وسطر السجل |
| RTM (TSX) | **لا مسار تنفيذ**: `has_rtm` تظهر في وسم السجل فقط؛ `_xbegin/_xend` معلنتان في `vm_reservation.h` بلا مستدعٍ، و`s_tsx_avx` في PPU/SPU هو `has_avx()` |

`SRC` مسارات LLVM لا تقرأ `utils::has_*` مباشرة: `cpu_translator` (`Emu/CPU/CPUTranslator.cpp`) يشتق
`m_use_avx/fma/avx512/avx512_icl/vnni/gfni` من **اسم LLVM CPU** (`getTargetCPU()` أو `Use LLVM CPU`). اسم LLVM CPU الافتراضي من
`llvm::sys::getHostCPUName()` ثم قواعد `jit_compiler::cpu` أعلاه، فلا يمكن إثباته من R3Config (`NOT_DETECTABLE`) ما لم
يُضبط `Use LLVM CPU` صراحة (`RPCS3`).

## ARM64

`SRC` `has_neon` (`PF_ARM_VFP_32_REGISTERS_AVAILABLE`=18 على Windows، وغيابها يرمي عند بدء التشغيل)، `has_sha3`=64،
`has_dotprod`=43، `has_i8mm`=66، `has_sve`=46، `has_sve2`=47 عبر `IsProcessorFeaturePresent`. الثوابت مطابقة لـ`winnt.h`
(SDK 10.0.26100.0). `sve_length()` قراءة سجل SVE في وقت التشغيل ولا تُقرأ من R3Config (`NOT_DETECTABLE`)؛ لذلك لا يُحكم أثر SVE.
`has_fma3()` ثابتة `true` على ARM64. القراءات تعمل فقط في عملية ARM64 أصلية؛ عملية x64 على Windows ARM64 تعيد `Unknown`
(المعالج المحاكى ليس المضيف).

## PPU

`SRC` `PPU Decoder` (`ppu_decoder_type`): `Interpreter (static)` أو `Recompiler (LLVM)`، الافتراضي `llvm`
(`system_config.h`). الاختيار وقت التشغيل من `g_cfg.core.ppu_decoder` (`PPUThread.cpp`, `PPUModule.cpp`, `lv2.cpp`).
`FLOW` `System.cpp` `fixup_settings()` يعطل عند البدء `PPU Accurate Non-Java Mode` و`PPU Accurate Vector NaN Values`
و`PPU Set FPCC Bits` إن لم يكن المفكك `Interpreter (static)`. الإقلاع من مجلد (directory boot) يفرض المفكك الافتراضي
(`from_default()`) ويفعل `LLVM Precompilation` و`SPU Cache`. `Use Accurate DFMA` و`Set DAZ and FTZ` يقرؤهما المفسر والمترجم.

## SPU

`SRC` `SPU Decoder` (`spu_decoder_type`): `Interpreter (static)`, `Interpreter (dynamic)`, `Recompiler (ASMJIT)`,
`Recompiler (LLVM)`، الافتراضي `llvm`. `SPU Block Size` (`safe|mega|giga`، الافتراضي `safe`) يدخل تحليل البرنامج في
`SPUCommonRecompiler.cpp` وباك-إند ASMJIT، ويدخل اسم ملف الكاش `spu-<size>-v1-tane.dat`. `SPU Cache` و`LLVM Precompilation`
يتحكمان بالكاش والتجميع المسبق، وعدد عمال التجميع `rpcs3::utils::get_max_threads()` = `Max LLVM Compile Threads` أو عدد الخيوط.
`SPU XFloat Accuracy` يقرؤه `SPULLVMRecompiler.cpp`، وواجهة Qt تعطل خياره مع ASMJIT.

## الخيوط

`SRC` `Utilities/Thread.cpp` و`Emu/CPU/CPUThread.cpp`:

- `Thread Scheduler Mode` (`os` الافتراضي، `old`، `alt`). عند غير `os`، كل `cpu_thread` (وخيوط RSX) يستدعي
  `set_thread_affinity_mask(get_affinity_mask(class))`.
- `detect_cpu_layout()`: brand يحتوي `Ryzen` → `amd_ccx`؛ وإلا brand يحتوي `Intel` وعلى Windows نواة SMT واحدة على
  الأقل (`GetLogicalProcessorInformationEx(RelationProcessorCore)` مع `LTP_PC_SMT`) → `intel_ht`؛ وغير ذلك `generic`. على غير
  Windows لا يوجد كشف SMT لـIntel (يسجل `todo`).
- `get_affinity_mask()`: `generic` تعيد قناع العملية كاملا؛ `amd_ccx` كذلك حتى 8 خيوط ثم جداول ثابتة حسب family/model
  وعدد الخيوط؛ `intel_ht` تضيق فقط عند 12 خيطا فأكثر وبوضع `alt`. `HostCpuAnalyzer` يحكم `NoChange` في الحالات المثبتة و
  `LayoutSpecific` بعدها دون تنفيذ الجداول.
- التخطيط تصنيف RPCS3 (brand + وجود SMT) وليس وصفا للطوبولوجيا: معالج Intel هجين فيه SMT يصنف `intel_ht` لأن الكود يبحث فقط عن نواة
  واحدة بعلم `LTP_PC_SMT`؛ فلا تعارض بين `intel_ht` وكون الجهاز هجينا. `EfficiencyClass` يعرضه R3Config رقما كما يعيده Windows، دون
  تسمية P/E ودون استنتاج من اسم المعالج، ولا يستهلكه RPCS3 في الجدولة.
- الأولوية: `set_native_priority` تحول إلى `THREAD_PRIORITY_ABOVE_NORMAL/BELOW_NORMAL/NORMAL` على Windows.
- الأقنعة 64 بت للمجموعة الحالية؛ لا معالجة لمجموعات المعالجة المتعددة في الأقنعة.

## الإعدادات (`Core` في `system_config.h`)

الجدول أدناه دلالات يعرفها المصدر المجمد. R3Config لا يقرأ في هذا المجال قيم `config.yml` الفعلية، فلا يدعي أن أي مفكك أو خيار هو
الحالي أو الفعال أو المفعل عند المستخدم.

| المفتاح | النوع/المدى | الافتراضي | المستهلك والدلالة | يعتمد على | كشف الانطباق |
|---|---|---|---|---|---|
| `PPU Decoder` | enum static/llvm | llvm | يختار مفسر أو LLVM في `PPUThread.cpp`/`lv2.cpp` | لا | FLOW (تعطل ثلاثة إعدادات دقة عند غير static) |
| `SPU Decoder` | enum 4 قيم | llvm | `SPUCommonRecompiler.cpp` | معمارية RPCS3 (ASMJIT x64 فقط) | FLOW + `Rpcs3SpuDecoders` |
| `SPU Block Size` | safe/mega/giga | safe | تحليل البرنامج وكاش SPU | لا | لا قيد عتاد |
| `Thread Scheduler Mode` | os/old/alt | os | أقنعة affinity بحسب `detect_cpu_layout` | brand + SMT + عدد الخيوط | جزئي (`SchedulerEffect`) |
| `Preferred SPU Threads` | 0..6 | 0 | حد تنفيذ متزامن لنفس PC في `concurrent_execution_watchdog` (ليس عدد خيوط SPU) | لا | لا قيد عتاد |
| `PPU Threads` | 1..8 | 2 | عدد خيوط PPU التي يجدولها `lv2` معا (ليس خيوط المضيف) | لا | لا قيد عتاد |
| `Max LLVM Compile Threads` | 0..1024 | 0 | يحد عمال التجميع؛ 0 = عدد خيوط `get_thread_count()` | عدد الخيوط | HOST |
| `Use LLVM CPU` | نص | فارغ | يتجاوز اسم LLVM CPU؛ فارغ = كشف LLVM | المضيف (وقت تشغيل LLVM) | RPCS3 (القيمة فقط) |
| `Use Accurate DFMA` | bool | true | مترجمو PPU/SPU؛ خانة Qt معطلة إن FMA3/FMA4 | FMA (واجهة فقط) | `Rpcs3AccurateDfmaUi` |
| `SPU Verification` / `Precise SPU Verification` | bool | true / false | تحقق SPU وغياب xorsum | لا | لا قيد عتاد |
| `SPU Cache` / `LLVM Precompilation` | bool | true | كاش وتجميع مسبق | لا | لا قيد عتاد |
| `Set DAZ and FTZ` | bool | false | PPU flush denormals | لا | لا قيد عتاد |

TSX: لا مفتاح إعداد ولا مسار تنفيذ في هذا المصدر (انظر جدول الميزات).

الإعدادات كلها قابلة للتغيير من الواجهة الرسمية عدا `SPU Debug` وما شابه مما يصنفه RPCS3 تصحيحا؛ R3Config لا يغيرها من هذا المجال.

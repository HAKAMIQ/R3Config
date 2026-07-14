# دليل إضافة الميزات

## بطاقة ما قبل التنفيذ

اكتب قبل الكود:

- user outcome؛
- query/plan/mutation/presentation/adapter؛
- inputs ومصدر الثقة وحالة unknown؛
- files read/written وowner لكل target؛
- invariants وfailure modes؛
- byte/item/depth/time limits؛
- use case المسؤول؛
- tests وrelease impact.

## مصدر اكتشاف لعبة

1. model في Core إذا احتجت evidence جديدًا.
2. bounded reader/probe تحت `Core/Svc/Scan`.
3. malformed source لا يلغي مصادر مستقلة.
4. normalize serial/title/version/path عند boundary.
5. merge بهوية وترتيب deterministic.
6. مرره إلى `AnalysisUseCase` عبر scanner، لا ViewModel.
7. اختبر missing/truncated/duplicate/permissions/ordering.

## مصدر hash

1. evidence kind جديد إذا تغير معنى الثقة.
2. source/message/timestamp محفوظة.
3. hash normalizer الموحد فقط.
4. احتفظ بكل PPU/SPU/PRX/OVL hashes ثم deduplicate.
5. لا تغير evidence ranking بلا conflict tests.
6. لا تعيد استخدام PPU evidence لـ PRX/OVL.
7. اختبر multi-session log وgzip bomb limits وcache entry limit.

## توصية config

1. schema/reader/model في Core.
2. risk/state/actionability في policy/plan، لا XAML.
3. current/proposed/reason ظاهرة.
4. YAML path دقيق؛ لا section replacement.
5. `ConfigUseCase` هو mutation boundary.
6. العربية والإنجليزية.
7. applied/pending/manual/blocked/hardware/invalid-YAML tests.

## مصدر أو نوع patch

نفّذ السلسلة كاملة:

1. source identity وownership.
2. parser وpartial diagnostics.
3. merge precedence/conflict/version.
4. classification للشرح فقط.
5. runtime key صالح ثم serial/version/module evidence عبر
   `PatchCompatibilityService` فقط.
6. preview بلا selection افتراضي.
7. selected-only compiler وmanaged ownership.
8. prepared files لكل targets.
9. guard + one transaction.
10. reload from disk.
11. translations.
12. parser/merge/compatibility/compiler/rollback/UI tests، وتشمل source parity
    والـportable data root وPPU cache Base57.

مكان orchestration هو `PatchCatalogUseCase` للقراءة أو
`PatchMutationUseCase` للتعديل، ثم registration في `AppComposition`.

## H-Canary operation

1. أثبت دعم patch engine version.
2. validation/generation/read-back معًا.
3. عرّف address/value encoding.
4. anchors/dependencies/cycles.
5. port evidence status واضح.
6. YAML preview قبل save.
7. catalog + metadata transaction.
8. draft -> YAML -> catalog/stored-draft round trip.

## شاشة أو command

1. state/command في partial ViewModel الصحيح.
2. use case موجود أو جديد؛ لا domain service `new` داخل VM.
3. العمل الثقيل خارج UI thread.
4. `CanExecute` + recheck داخل command.
5. localized confirmation/error للـ mutation.
6. use case guard هو الحاجز النهائي.
7. key parity للغتين.
8. حفظ selection/filter/expansion عبر rebuild.
9. XAML smoke test.

## خوارزمية أو compiler

```text
NormalizedInput -> DeterministicResult/Plan + Diagnostics
```

- القلب في Core، بلا WPF/global mutable state.
- filesystem/clock injected أو خارج الخوارزمية.
- stable ordering وexplicit unknown/conflict.
- documented complexity/resource bounds.
- compile منفصل عن commit.
- tests: empty/malformed/duplicates/cycles/conflicts/unsupported/boundary/replay.

## إضافة dependency

- constructor parameter في use case/service.
- أنشئها في `AppComposition` فقط.
- شارك stateful dependency المقصودة مثل backup/transaction/readers.
- لا تستخدم service locator أو static mutable singleton.
- interface عند وجود boundary/variant/test seam حقيقي، لا لكل class تلقائيًا.

## Checklist المراجعة

- dependency direction صحيح؟
- AppFlow delegate فقط؟
- Application بلا WPF/UiText/UiDlg؟
- ViewModels بلا file writes؟
- read path بلا side effect؟
- targets معلنة ومملوكة ومحروسة؟
- unrelated content محفوظ؟
- transaction كاملة؟
- activation policy لم تسمح serial/version ناقصًا أو تعارض PPU رئيسيًا؟ وهل
  تجنبت اعتبار SPU/PRX/OVL الجزئي mismatch كاذبًا؟
- اللغتان متطابقتان؟
- test مستدعى؟
- data/provenance/release gate محدثة؟

# R3Config Architecture Pack

هذه الحزمة هي المرجع الرسمي لتطوير R3Config بعد إعادة الهيكلة. سلّم هذا المجلد

## ترتيب القراءة

1. [دليل المطور الكامل](DevGuide.md) لفهم النظام والطبقات والتدفقات.
2. [طبقة توافق الباتشات](PatCmp.md) قبل تعديل الرسمي أو Canary أو الغش.
3. [عقود السلامة](SafeCtr.md) قبل لمس أي مسار يكتب ملفات RPCS3.
4. [دليل إضافة الميزات](FeatBook.md) لاختيار مكان وتنفيذ الميزة.
5. [الاختبار والنشر](RelTest.md) لمعرفة بوابات القبول والإصدار.
6. [دليل الثيم والألوان](UiTheme.md) قبل إضافة شاشة أو نمط WPF.
7. [كتالوج Artemis](Artemis.md) قبل تحديث المصدر أو بيانات `art.yml`.
8. [ملاحظات الويكي وOnline/RPCN](WikiEvidence.md) قبل تعديل عرض الإعدادات أو الملاحظات أو أقسام RPCS3.
9. [بيئة التشغيل والتوافق](RuntimeContext.md) قبل تعديل فحص العتاد أو هوية RPCS3/اللعبة أو كتالوج البناءات.
10. [تشخيص السجل](LogDiag.md) قبل تعديل قراءة سجلات RPCS3 أو تصنيف الأحداث أو التوقيعات أو قاعدة الحالات الموثقة.
11. [تدقيق سلوك RPCS3 تجاه المعالج](Rpcs3CpuAudit.md) قبل تعديل ميزات المعالج أو تخطيط المجدول أو قواعد انطباق مفككات PPU/SPU.
12. [تحديث قاعدة التوصيات](RecSync.md) قبل تعديل مزامنة التوصيات أو مخزنها المحلي أو دمجها مع cat.json.

## الخلاصة المعمارية

```text
WPF View
  -> responsibility-based ViewModel partial
  -> AppFlow compatibility facade
  -> App/Application use case
  -> Core model/service + App adapter
  -> validated result or guarded transaction
```

الإنشاء يتم مرة واحدة من `App/Composition/AppComposition.cs`. لا ينشئ ViewModel
خدمات domain أو writers بنفسه، ولا يكتب ViewModel ملفًا مباشرة.

## قواعد لا تتغير

- `Core` لا يعتمد على WPF أو الترجمة أو ViewModels.
- `App/Application` لا يعتمد على WPF أو dialogs أو `UiText`.
- `AppFlow` facade رقيق وليس مكانًا لمنطق جديد.
- analysis/preview/plan مسارات قراءة بلا side effects.
- كل تعديل لملفات RPCS3 يمر عبر use case و`IRpcs3MutationGuard`.
- لا تفعيل باتش دون اختيار صريح. تعريفات Canary المضمنة تتطلب التقاط PPU
  محلي مرة واحدة بعد تأكيد ظهور `PPU executable hash` للعبة المحددة في جلسة اللوج الجديدة ثم تجهيز نسخة
  managed تحت مفتاح التشغيل المحلي؛ بقية المصادر تتبع عقد `ExactMatch` أو
  runtime guard الموثوق عبر `PatchCompatibilityService`.
- لا تعديل لـ `patch.yml` أو ملفات اللعبة أو binary المحاكي.
- multi-file mutation تنجح كاملة أو تتراجع كاملة.
- العربية والإنجليزية لهما مجموعة المفاتيح نفسها.
- الاختبار الجديد لا يُعد موجودًا حتى يستدعيه runner.

## أين أبدأ؟

| نوع التغيير | نقطة البداية |
|---|---|
| شاشة أو command | `App/Vm/` ثم use case قائم أو جديد |
| orchestration جديد | `App/Application/` |
| إنشاء dependency | `App/Composition/AppComposition.cs` فقط |
| parsing/matching/planning | `Core/Svc/` |
| model أو contract domain | `Core/Mdl/` |
| ترجمة رسالة adapter | `App/Svc/` و`App/Lang/` |
| mutation لملف RPCS3 | use case + mutation guard + backup/transaction |
| regression | `Tst/Program.cs` لتوجيه CLI فقط، و`TestRunner.cs` للتنفيذ/التقرير، و`TestContext.cs` لعمر temp root، و`TestAssert.cs` للمساعدات العامة، و`TestFixtures.cs` للـfixtures المشتركة، مع ملفات `Program.<area>.cs` أو suite متخصصة واستدعاء فعلي |

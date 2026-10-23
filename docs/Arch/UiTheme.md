# R3Config Fluent 2 visual contract

هذا الملف هو عقد طبقة العرض في R3Config. التطبيق يبقى WPF و.NET 8 من دون
WinUI أو مكتبات UI خارجية. Fluent 2 هنا نظام Tokens وقوالب WPF مركزية، وليس
تغييرًا في ViewModels أو Commands أو Workflow.

## مصدر الحقيقة

- `App/Thm/Colors.xaml`: لوحتا Light وDark والـsemantic brushes النشطة.
- `App/Thm/Type.xaml`: خطوط Segoe UI Variable وأدوار Typography.
- `App/Thm/Ctrl.xaml`: Spacing وRadius وStroke وControl sizes وElevation وMotion
  وقوالب عناصر التحكم.
- `App/Svc/WTheme.cs`: اختيار Light/Dark بحسب إعداد تطبيقات Windows مع إبقاء
  مفاتيح الموارد نفسها.
- ملفات `App/View` و`App/Ctrl`: تخطيط وربط بيانات فقط.

لا تستخدم لونًا حرفيًا خارج `Colors.xaml`، ولا تضف `CornerRadius` حرفيًا داخل
View. عند الحاجة إلى قيمة جديدة، أضف Token دلاليًا ثم استخدمه من العرض.

أبقِ `Ctrl.xaml` قاموسًا واحدًا مرتبًا حسب اعتماديات الموارد ما دامت مفاتيح
`StaticResource` تعتمد على مفاتيح سابقة في الملف. لا تقسّمه إلى قواميس خارجية
مترابطة دون إثبات ترتيب التحميل بمصفوفة UI؛ كسر هذا الترتيب قد ينتج
`DependencyProperty.UnsetValue` وقت تشغيل WPF رغم نجاح تحليل XAML الساكن.

## WPF UI 4.3.0 (Fluent 2 foundation)

`App/App.xaml` يدمج `ui:ThemesDictionary` و`ui:ControlsDictionary` من حزمة
`WPF-UI` 4.3.0 **أولًا**، ثم `Colors.xaml` و`Type.xaml` و`Ctrl.xaml`. الترتيب
حتمي: القاموس المدمج لاحقًا يفوز، فكل عنصر تحكم تملكه R3Config يبقى بقالبها
المتحقق منه، وتقتصر WPF UI على موارد Fluent وعناصرها الخاصة.

- `WinTheme.ApplyResources` هو مصدر الحقيقة الوحيد للثيم: يبدّل لوحة R3Config
  ويستبدل `ThemesDictionary` في مكانه بثيم WPF UI المطابق (Dark/Light) دون
  استخدام مدير نوافذ WPF UI أو خلفياته، فتبقى حدود النوافذ ملكًا لـR3Config.
  بوابة UI تتحقق أن ثيم WPF UI يتبع ثيم R3Config في الاتجاهين.
- `Ctrl.xaml` يعيد ملكية `ScrollViewer` (أشرطة تمرير ظاهرة بالحجم الكامل)
  و`Expander` و`ToggleButton` (موضع سهم ComboBox) وزر التحديد الكلي في
  `DataGrid`؛ مقارنة البكسل مع القاعدة أثبتت أن أنماط WPF UI لهذه الأنواع
  تغيّر سلوكًا متحققًا منه.
- الرموز: `FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"` في كل موضع؛
  Segoe Fluent Icons موجود في Windows 11 فقط، وWindows 10 مدعوم ويحمل الرموز
  نفسها في Segoe MDL2 Assets. `IconFontT` يتحقق أن كل رمز مستخدم موجود في الخطين.
- أسماء الألعاب في رؤوس مجموعات Official وCanary وArtemis تأخذ اتجاهها من
  محتواها (`ContentTextFlowConverter`) مع `HorizontalAlignment="Left"` (بداية
  الواجهة): `God of War 3` يبقى كذلك داخل الواجهة العربية. `GroupTitleDirectionT`.
- الترخيص: MIT؛ `THIRD-PARTY-NOTICES.md` يُشحن مع الإصدار وينسخ `LICENSE`
  و`ThirdPartyNotices.txt` من الحزمة حرفيًا (`ThirdPartyNoticeT` و`PortChk`).
- لا يُستخدم `FluentWindow`: قشرة R3Config (WindowChrome، أزرار العنوان الفيزيائية
  في العربية والإنجليزية، DPI) متحقق منها، والتحويل لا يضيف وظيفة.

## Semantic color contract

العروض والقوالب تستخدم هذه المفاتيح فقط:

```text
Surface.Background
Surface.Card
Surface.CardHover
Surface.CardPressed
Surface.Subtle
Surface.Overlay

Text.Primary
Text.Secondary
Text.Disabled
Text.OnAccent

Stroke.Default
Stroke.Subtle
Stroke.Strong
Stroke.Focus

Accent.Default
Accent.Hover
Accent.Pressed
Accent.Disabled

Status.Success
Status.Warning
Status.Danger
Status.Info
```

توجد القيم الفعلية تحت `Fluent.Dark.*.Color` و`Fluent.Light.*.Color`. تغيير
الثيم يبدل الألوان خلف العقد نفسه؛ View لا يعرف اسم الوضع أو قيمة اللون.

الأزرق محصور في الإجراء الأساسي، الحالة المحددة، التركيز، والحالة النشطة
المهمة. لا تستخدم خطوط Accent زخرفية.

## Typography

الموارد الأساسية:

```text
Type.Font.Body
Type.Font.Display
Type.Font.Technical

Type.Display      28
Type.TitleLarge   24
Type.Title        20
Type.Subtitle     16
Type.Body         14
Type.BodyStrong   14 SemiBold
Type.Caption      12
Type.Technical    13
```

استخدم `Segoe UI Variable Text` للنصوص، و`Segoe UI Variable Display` للعناوين،
و`Cascadia Mono` مع fallbacks للقيم التقنية. لا تستخدم Bold مبالغًا فيه.

## Spacing, radius, stroke, and size

```text
Space.2 / 4 / 8 / 12 / 16 / 20 / 24 / 32
Radius.Small / Medium / Large / XLarge
Stroke.Width.Default / Stroke.Width.Focus
Stroke.Thickness.Default / Stroke.Thickness.Focus
Control.Height.Compact / Standard / Large
Button.MinWidth.Compact / Standard
Button.Padding.Compact / Standard / Large
DataGrid.HeaderHeight / DataGrid.RowHeight
```

- العناصر الصغيرة: 4–8.
- داخل الحقول والأزرار: 8–12.
- داخل الأسطح: 12–16.
- بين الأقسام: 20–24.
- الزوايا: Small = 2، Medium = 4، Large = 8، XLarge = 12.
- الزر الصغير: 64×24 كحد أدنى، Padding ‏8×3، ونص 12.
- الزر المتوسط: 96×32 كحد أدنى، Padding ‏12×5، ونص 14 SemiBold.
- استخدم إجراءً أساسيًا واحدًا فقط في السطح؛ الإجراءات الأقل أهمية Secondary
  أو Compact، وتظهر إجراءات النسخ والحذف فقط عندما يوجد عنصر محفوظ.
- في المسارات متعددة الخطوات استخدم «السابق/التالي»، واجعل «حفظ» الإجراء
  الأساسي في خطوة المراجعة الأخيرة.
- الحقل والتبويب المتوسطان: ارتفاع 32.
- التبويبات بعرض محتواها مع مؤشر Accent سفلي؛ لا تستخدم خلايا متساوية ممتدة.
- البطاقات: `Radius.Large`.
- الحوارات: `Radius.XLarge`.
- الحد العادي 1، والتركيز 2 عند الحاجة.

## Elevation and motion

```text
Elevation.1
Elevation.2
Elevation.3
Motion.Hover
Motion.Press
Motion.Surface
```

الظلال خفيفة وتستخدم للأسطح والـflyouts والحوارات فقط. لا تضف ظلًا ثقيلًا إلى
كل Card. الحركة قصيرة وهادئة، ولا تستخدم Animations على DataGrid كبير أو أثناء
تحديث كثيف.

## Named Fluent controls

```text
FluentButton.Primary
FluentButton.Secondary
FluentButton.Subtle
FluentButton.Danger
FluentButton.Icon
FluentButton.Close
FluentButton.Compact
FluentButton.Toolbar

FluentTextBox
FluentComboBox
FluentCheckBox

FluentDataGrid
FluentDataGridRow
FluentDataGridHeader
FluentDataGridCell

FluentTabControl
FluentTabItem

FluentSurface.Card
FluentSurface.Subtle
FluentSurface.Dialog
```

كل Surface أو Dialog يملك إجراء Primary واحدًا فقط. بقية الإجراءات Secondary
أو Subtle. الأزرار الأيقونية تحتاج `ToolTip` و`AutomationProperties.Name`.

## Transient surfaces and feedback

- `Popup` و`ContextMenu` و`ToolTip` تفتح فقط من تفاعل صريح (Hover/Focus للملاحظة،
  Click أو لوحة المفاتيح للقائمة والمعاينة). يمنع `IsOpen` حرفي أو مربوط بـViewModel،
  وكل `IsOpen = true` في الكود يكون داخل معالج حدث إدخال (`RoutedEventArgs`).
  `VerifyTransientSurfaceOwnership` في `UiGate` يفرض ذلك؛ الاستثناء الوحيد
  `{TemplateBinding IsDropDownOpen}` داخل قالب ComboBox.
- المعاينة العائمة مرتبطة بالعنصر الذي فتحها، وتغلق بـEscape أو النقر خارجها أو زر
  الإغلاق، وتعيد التركيز إلى الزر عند الإغلاق من لوحة المفاتيح أو زر الإغلاق، وتغلق
  إذا خرج العنصر المرتبط من الشجرة (تحديث القائمة).
- الملاحظة لا تكرر النص الظاهر لعنصرها، وعلى الزر المعطل تذكر شرط تفعيله.
- أخطاء الحقل تظهر تحت الحقل مباشرة، ونتيجة إجراء ثانوي تظهر كـ`AdvInlineNotice`
  (مكافئ MessageBar داخل السطح، نص + رمز + لون، ولا يطفو فوق المحتوى). رسالة واحدة
  لكل سطح تستبدل سابقتها، وتمسح عند إعادة الدخول إلى الصفحة.
- الحوار مخصص للتأكيد أو لتغير حالة يجب قراءته (مثل استعادة نسخة احتياطية)، ولا يفتح من
  Loaded أو PropertyChanged أو التنقل. رسالة الخطأ غير المتوقع لا تتكدس: صندوق واحد
  ظاهر في كل مرة.

## ToolTip timing and ownership

`App/Svc/ToolTipPolicy.cs` هو المصدر الوحيد لتوقيت الملاحظات في التطبيق:

```text
InitialShowDelay = 600 ms
BetweenShowDelay = 150 ms
ShowDuration = 6000 ms
ShowOnDisabled = true
```

لا تضف `ToolTipService.InitialShowDelay` أو `BetweenShowDelay` أو `ShowDuration`
محليًا داخل XAML. كل صف يملك نافذة ملاحظة واحدة فقط، وتظهر أسفل العنصر بعد
ثبات المؤشر ثم تغلق تلقائيًا. لا تستخدم `ContextMenu` لشرح عنصر غير تنفيذي، ولا
تربط ملاحظات متنافسة بالصف وبالعناصر الداخلية في الوقت نفسه.

في قائمة Canary يعرض الصف مربع الاختيار والاسم وزر المعلومة فقط. حالة التحديد
واضحة من مربع الاختيار والملخص العلوي، لذلك يمنع تكرارها داخل شارة زرقاء أو
خضراء. كذلك لا يعرض قسم Canary شريط مصدر يحمل اسم Canary مرة ثانية؛ معلومات
مصدر RPCS3 الرسمي تبقى في قسم الباتشات الرسمي فقط.

## Direction and alignment

يتبع التخطيط عقد Windows 11 المنطقي بحسب لغة الواجهة:

```text
العربية:  Window/Shell FlowDirection = RightToLeft
           localized TextAlignment = Left
           leading edge = Right, trailing actions = Left

الإنجليزية: Window/Shell FlowDirection = LeftToRight
            localized TextAlignment = Left
            leading edge = Left, trailing actions = Right
```

أزرار عنوان نافذة Windows تتبع اتجاه الواجهة: تكون في الجهة الفيزيائية اليسرى
بالعربية واليمنى بالإنجليزية، مع بقاء رموز الأزرار نفسها `LeftToRight`.
المسارات والـhashes والمعرّفات والإصدارات والكود وYAML والقيم التقنية تبقى
`LeftToRight` ومحاذاتها إلى اليسار باستخدام أنماط Technical المخصصة.
أسماء الألعاب بيانات محتوى وليست نصوص واجهة: يحدد
`ContentTextFlowConverter` اتجاه الاسم من محتواه، فيبقى الاسم اللاتيني
`LeftToRight` والاسم العربي `RightToLeft` في اللغتين، بينما تبقى serials
والإصدارات في أسطح Technical مستقلة `LeftToRight`.

قاعدة الاتجاه الديناميكي عامة بحسب فئة المحتوى وليست قائمة أسماء: الاتجاه من
المحتوى، والموضع دائما عند الطرف الابتدائي للواجهة. عنوان المحتوى يستخدم
`HorizontalAlignment="Left"` داخل حاوية منطقية حتى يلتصق بالطرف الابتدائي حتى
لو اختلف اتجاهه عن الواجهة. عناوين الباتشات الإنجليزية القياسية التي قد تلتف
تستخدم `TechnicalText.StartTextAlignment` (`Right` بالعربية و`Left`
بالإنجليزية، يحدّثه `UiLaySvc` مباشرة) فيبقى كل سطر ملتف عند الطرف الابتدائي.
يتحقق `GroupTitleDirectionT` من ذلك بسبعة عناوين ("God of War 3"، "3D Dot Game
Heroes"، "F1 2014"، "WWE 2K14"، "NieR Replicant ver.1.22474487139..."، "Call of
Duty: Black Ops II"، وعنوان عربي) في رأس هوية اللعبة ورؤوس المجموعات وصفوف
الباتشات الثلاثة، ويقيس موضع النص المرسوم وليس صندوق التخطيط فقط.

تستخدم الحاويات `App.FlowDirection` أو `App.ShellFlowDirection`، والنصوص المحلية
`App.TextFlowDirection` و`App.TextAlignment`، والطرف الابتدائي
`App.HorizontalAlignment`، والطرف التنفيذي المقابل
`App.TrailingHorizontalAlignment`. المسافات الأفقية المنطقية تستخدم موارد
`App.After*Margin` و`App.Before*Margin` بدل هوامش يمين/يسار ثابتة.

يمنع داخل ملفات View:

```xaml
FlowDirection="RightToLeft"  <!-- قيمة صلبة -->
TextAlignment="Right"        <!-- قيمة صلبة -->
HorizontalAlignment="Right"  <!-- للإجراءات المحلية بدل المورد المنطقي -->
```

القيم الصلبة `LeftToRight` و`TextAlignment="Left"` مسموحة فقط للعناصر التقنية
ولشريط عنوان Windows والرسومات التي لا ينبغي قلبها.

## Fluent 2 visual redesign primitives

- الألوان: `Surface.Input` لخلفية حقول الإدخال و`Text.Tertiary` للرموز
  الزخرفية وحالات الفراغ، مسجلة في `WinTheme.SemanticBrushes` للتبديل الحي.
- الأشكال: `Radius.Pill` للشارات والرقائق، و`Radius.Input.Underline` لخط
  الأساس السفلي الثابت في `TextBox` (بدون مشغلات تركيز لوحة المفاتيح؛ التركيز
  يبقى لسياسة `Stroke.FocusVisualStyle`).
- الرموز: `IconGlyphText` (Segoe Fluent Icons مع Segoe MDL2 Assets، غير
  مقلوب)، و`IdentityTile`/`IdentityTileGlyph`، و`EmptyStateGlyph`.
- الهوية: `IdentityChip`/`IdentityChipText` للـserial والإصدار؛ رأس اللعبة
  المختارة في `Ctrl/GameIdentityTemplate.xaml` (`GameIdentity`) واسمه المتاح هو `SelGame`
  الكامل. حالة الجاهزية تبقى داخل أسطح الباتشات.
- التنقل: `FluentTabItem` يعرض رمزا اختياريا من `TabItem.Tag` ومؤشر تحديد قصيرا
  في المنتصف.
- الصفوف: صفوف الباتشات والإعدادات بطاقات مستقلة (`Surface.Card`،
  `Stroke.Subtle`، `Radius.Medium`)، وحالة الباتش شارة `PatStatePill` نصها يسمي
  الحالة دائما واللون يدعمها فقط.

## Data surfaces

كل `DataGrid` يستخدم `FluentDataGrid` ويعلن سياسات التمرير الأفقية والرأسية.
ارتفاع الرأس والصف مركزي. استخدم `Auto` للقيم القصيرة و`*` للنصوص الطويلة مع
`MinWidth` و`MaxWidth` عند الحاجة. المسار الطويل يستخدم Ellipsis وToolTip.

لا تضع DataGrid رئيسيًا داخل ScrollViewer رأسي يملك اتجاه التمرير نفسه. أشرطة
الأوامر الضيقة تستخدم WrapPanel بدل UniformGrid ثابت.

## Advanced tools and H-Canary

بنية `AdvDlg`:

```text
Dialog header
Windows-style navigation rail for H-Canary, Recovery, and Diagnostics
Direct content surface with H-Canary selected by default
One shared MainVm / HCanVm instance
```

- H-Canary: يفتح مباشرة عند فتح الأدوات المتقدمة؛ لا توجد صفحة وسيطة أو زر
  «فتح مساحة». التنقل الجانبي يبدّل المحتوى داخل النافذة نفسها من دون إنشاء
  نافذة أو ViewModel جديد ومن دون فقد المسودة والاختيارات.
- Recovery: ملخص وإجراء منفصلان، DataGrid مرن، Empty state مستقل.
- Diagnostics: Label Auto وقيمة `*`، والمسارات Ellipsis، والسجل Surface خافت.
- مساحة H-Canary تحتوي مستوى تنقل واحدًا من أربع خطوات: الهدف والبصمة، بيانات
  الباتش، التعليمات، ثم المراجعة والحفظ. الباتشات المحفوظة تظهر في Drawer داخل
  المساحة، والنسخ المستهدفة Panel داخل خطوة الهدف، والمراجع المتقدمة Expander
  داخل خطوة التعليمات.
- زر «التالي» يعكس جاهزية الخطوة الحالية، ويظهر سبب التعطيل بجواره. إجراءات
  الملفات الثانوية داخل قائمة «الملفات والأدوات»، وإجراءات الحفظ لا تظهر إلا في
  خطوة المراجعة. قائمة الباتشات المحفوظة تعرض فقط تعريفات H-Canary التي يملك
  R3Config metadata لها؛ لا يفتح المحرر تعريفًا خارجيًا قد يفقد حقولًا غير مدعومة.
- يمنع وضع `TabControl` داخل `TabControl` أو `DataGrid` داخل `DataGrid`.

## Accessibility

- Focus keyboard ظاهر في القوالب المركزية.
- ترتيب Tab منطقي.
- الأزرار الأيقونية لها ToolTip وAutomation name.
- تستخدم جميع ToolTips النمط المركزي `ModernToolTip` بموضع ثابت أسفل العنصر وإزاحة 8 بكسل؛ يمنع `Placement=Mouse`.
- صف القائمة الذي يملك بطاقة شرح تفصيلية هو المالك الوحيد للـToolTip؛ لا تضف ToolTips متنافسة إلى النص أو مربع الاختيار داخله.
- مربع الاختيار القابل للتفاعل يملك هدف تفاعل 28 بكسل ومربعاً داخلياً 20 بكسل وحداً قوياً؛ Hover يستخدم سطح Accent خافتاً وحداً بسماكة 2 بكسل.
- الخطأ والتحذير والتحديد لا يعتمدان على اللون وحده.
- حالات Disabled تستخدم ألوانًا وحدودًا صحيحة، لا Opacity وحدها.
- Runtime gate يختبر Light وDark، العربية والإنجليزية، ثماني نوافذ ومساحة
  H-Canary المضمنة، أربع أحجام، و100% و125% و150% DPI.

## UI gates

`Tst/UiGate.cs` يفشل عند:

- لون حرفي خارج `Colors.xaml`.
- CornerRadius حرفي داخل View.
- Margin سالب.
- اتجاه أو محاذاة نص مخالفة.
- DataGrid بلا `FluentDataGrid` أو سياسة تمرير.
- نمط محلي يكرر نمطًا مركزيًا.
- زر أيقوني بلا ToolTip أو Automation name.
- زر نصي بمقاس يدوي خارج أنماط Fluent المركزية.
- TabControl أو DataGrid متداخل.
- nested vertical ScrollViewer داخل Tab واحد.
- Disabled state يعتمد على Opacity وحدها.
- فقد أي Token أساسي أو أي لوحة Light/Dark.

## Verification

```powershell
.\Script\RelTest.cmd -Configuration Debug
.\Script\RelTest.cmd -Configuration Release
```

`global.json` is the repository SDK source of truth. `RelTest.cmd` is the canonical UI/build entry point because `R3Toolchain.ps1` also owns the scoped workload-resolver environment. The UI gate runs from the built `R3Test.exe`; `dotnet run` is not the acceptance runner.

## Minimal patch action surfaces

في صفحات الباتشات الرسمية وCanary وأرتميس يجب أن تختفي منطقة الإجراءات كاملة
عندما لا توجد عناصر متوافقة. عند وجود عناصر تعرض المنطقة إجراءين فقط: تفعيل
وتعطيل. الملخصات وتعليمات سير العمل وحالة المصدر لا توضع كنصوص دائمة داخل
الصفحة؛ تعرض من هدف معلومات واحد ثابت. تبقى إجراءات تحديد الكل وإلغاء التحديد
داخل قائمة الخيارات التابعة لأرتميس حتى لا تضيع الميزة ولا تزحم شريط الإجراءات.

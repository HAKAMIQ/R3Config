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

## Direction and alignment

تخطيط النافذة والحاويات يعمل دائمًا بهذه القيم:

```text
Shell FlowDirection = LeftToRight
TextAlignment = Left
HorizontalContentAlignment = Left
```

داخل أسطح النص فقط، تستخدم العربية `RightToLeft` لمعالجة bidi الصحيحة مع بقاء
`TextAlignment = Left`، بينما تستخدم الإنجليزية `LeftToRight`. المسارات
والـhashes والقيم التقنية تبقى LTR وخطها تقني. تغيير اللغة لا يعكس الأعمدة أو
ترتيب الأزرار، وأزرار نافذة Windows تبقى في الجهة اليمنى.

يمنع:

```xaml
TextAlignment="Right"
TextAlignment="Center"
TextAlignment="Justify"
FlowDirection="RightToLeft" <!-- على Window/Grid/Panel أو كقيمة صلبة في View -->
```

اتجاه النص المحلي يؤخذ من `App.TextFlowDirection`، واتجاه الحاويات من
`App.ShellFlowDirection`. لا تستخدم قيمة RTL صلبة داخل ملفات View.

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
dotnet restore .\R3Cfg.slnx
dotnet build .\R3Cfg.slnx -c Debug --no-restore --nologo
dotnet build .\R3Cfg.slnx -c Release --no-restore --nologo
dotnet run --project .\Tst\R3Test.csproj -c Release --no-build
powershell -ExecutionPolicy Bypass -File .\Script\SrcChk.ps1
powershell -ExecutionPolicy Bypass -File .\Script\RelTest.ps1 -Configuration Release
```

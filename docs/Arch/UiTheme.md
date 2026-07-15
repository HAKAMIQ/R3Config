# دليل الثيم والألوان

هذا الملف هو عقد واجهة R3Config. الهدف أن تبقى كل شاشة واضحة في الوضع الداكن،
وألا تعود عناصر WPF إلى قوالب ويندوز الفاتحة عند إضافة شاشة أو نمط محلي.

## مصدر الحقيقة

- `App/Thm/Colors.xaml`: قيم الألوان والـ brushes الدلالية فقط.
- `App/Thm/Ctrl.xaml`: القوالب العامة وحالات عناصر التحكم.
- `App/Thm/Type.xaml`: الخطوط وأدوار النص.
- ملفات `App/View` و`App/Ctrl`: التخطيط وربط البيانات فقط؛ لا تضيف ألوانًا حرفية.

لا تستخدم `#RRGGBB` أو `White` أو `Black` خارج `Colors.xaml`. إذا احتاجت ميزة
حالة لونية جديدة، سمِّها بحسب وظيفتها مثل `InputDisabledBrush` أو
`DangerSurfaceBrush`، وليس بحسب درجتها مثل `DarkGrayBrush`.

## أدوار الألوان

| الدور | الموارد الأساسية | الاستخدام |
|---|---|---|
| خلفية التطبيق | `AppBackgroundBrush`, `SidebarBackgroundBrush` | جذور النوافذ والشريط الجانبي |
| الأسطح | `SurfaceBrush`, `SurfaceAltBrush`, `CardBackgroundBrush` | البطاقات والأشرطة والمجموعات |
| الإدخال | `InputBackgroundBrush`, `InputHoverBrush` | `TextBox`, `ComboBox`, القوائم القابلة للتفاعل |
| النوافذ المنبثقة | `PopupSurfaceBrush` | قائمة `ComboBox` وأي popup داكن |
| الحدود | `BorderSubtleBrush`, `BorderBrush`, `FocusBorderBrush` | عادي، مرور، وتركيز لوحة المفاتيح |
| النص | `TextPrimaryBrush`, `TextSecondaryBrush`, `TextMutedBrush` | رئيسي، وصفي، وبيانات ثانوية |
| التعطيل | `InputDisabledBrush`, `DisabledBorderBrush`, `DisabledTextBrush` | عناصر غير متاحة من دون إخفائها |
| التحديد | `SelectionBrush`, `SelectionHoverBrush`, `SelectionInactiveBrush` | الصف أو العنصر المحدد وحالة فقد التركيز |
| الدلالات | `AccentBrush`, `WarningBrush`, `DangerBrush`, `SuccessBrush` | إجراء أساسي، تحذير، خطأ، نجاح |

## عقد حالات عناصر التحكم

كل عنصر تفاعلي يجب أن يعرّف الحالات التالية في قالبه المركزي:

| الحالة | المطلوب |
|---|---|
| عادي | سطح داكن ونص رئيسي وحد خافت |
| مرور المؤشر | رفع تباين السطح أو الحد، من دون قلب العنصر إلى الأبيض |
| ضغط | سطح أغمق واضح |
| تركيز لوحة المفاتيح | `FocusBorderBrush` ظاهر |
| تحديد | خلفية معتمة من `SelectionBrush` ونص مقروء |
| تعطيل | ألوان التعطيل الثلاثة؛ لا تعتمد على `Opacity` وحدها |

الألوان ليست وسيلة البيان الوحيدة: الخطأ يحتاج نصًا، والتعطيل يحتاج `CanExecute`
صحيحًا، والتحديد يحتاج حدًا أو سطحًا واضحًا.

## قاعدة الوراثة المهمة في WPF

النمط المحلي الذي يحمل `x:Key` لا يرث النمط الضمني تلقائيًا. لذلك اكتب:

```xaml
<Style x:Key="FeatureActionButton"
       TargetType="{x:Type Button}"
       BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="MinWidth" Value="104" />
</Style>
```

ينطبق ذلك على `Button`, `TextBox`, `ComboBox`, `ListBox` وبقية العناصر. يجوز
عدم استخدام `BasedOn` فقط إذا كان النمط يملك قالبًا كاملًا ويغطي كل حالات العقد.
الأنماط المحلية تضبط القياسات والمحاذاة عادة، بينما يبقى اللون والسلوك في
`Ctrl.xaml`.

## إضافة شاشة أو عنصر جديد

1. استخدم النمط الضمني أو نمطًا مركزيًا قائمًا.
2. اجعل أي نمط محلي `BasedOn` على النمط العام.
3. أضف الدور المفقود إلى `Colors.xaml` كـ `Color` و`SolidColorBrush` معًا.
4. أضف الحالة إلى القالب المركزي، لا إلى نسخة منفصلة داخل الشاشة.
5. افحص العربية والإنجليزية و`RightToLeft` و`LeftToRight`.
6. افتح القوائم المنسدلة فعليًا؛ الـ `Popup` نافذة بصرية منفصلة وقد يبدو مختلفًا
   عن الحقل المغلق.
7. افحص الحالات العادية، المركّزة، المحددة، والمعطلة بلوحة المفاتيح والمؤشر.

## بوابات المراجعة

```powershell
rg -n '#[0-9A-Fa-f]{6,8}|\b(White|Black|Gray|Red|Blue|Green)\b' App --glob '*.xaml' -g '!App/Thm/Colors.xaml'
rg -n 'Color\.|Colors\.|Brushes\.|FromRgb|FromArgb' App --glob '*.cs'
dotnet build .\R3Cfg.slnx -c Debug
dotnet run --project .\Tst\R3Test.csproj -c Debug --no-build
```

نتيجة `rg` غير الفارغة تحتاج تبريرًا أو نقل اللون إلى اللوحة. بعد ذلك شغّل
التطبيق وافحص النافذة الرئيسية، الأدوات المتقدمة، قائمة `ComboBox` ممتلئة،
تحديد `ListBox`/`DataGrid`، وأزرار `PrimaryButton` والمعطلة.

using System;
using System.Collections.Generic;

namespace R3Config.Svc;

public static partial class NoteSvc
{
    // Reviewed static Arabic presentation map for Official RPCS3 Notes.
    // Baseline source SHA-256: F845F2F5F90CC2B7A1D6220C6B0DA79738F3944E418608E6887F919A26C3B750
    // Baseline: 1,974 patch entries, 1,210 non-empty Note occurrences, 354 unique Notes.
    // Additional reviewed canonical Note texts are sourced from later Official patch database revisions;
    // the runtime corpus gate remains authoritative for the user's selected patches\patch.yml.
    // Canonical RPCS3 setting names and technical identifiers are intentionally preserved in English.
    private static readonly Dictionary<string, string> OfficialArByEn =
        new(StringComparer.Ordinal)
        {
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 62 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 62 FPS.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. UI elements will be stretched."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد عناصر UI.",
            ["Maximum framerate with the patch is half the Vblank frequency."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency.",
            ["Updates references from gamespy.com to openspy.net. Allows to play multiplayer."] =
                "يحدث المراجع من gamespy.com إلى openspy.net، ما يتيح اللعب الجماعي.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU.",
            ["Set the framelimiter to 60, as things will break over 60 FPS."] =
                "اضبط Framelimit على 60، لأن تجاوز 60 FPS سيؤدي إلى تعطل بعض العناصر.",
            ["Allows for the use of Resolution Scaling in the title.\nMust be used with Disable Dynamic Resolution Scale patch for proper upscaling and improved performance.\nSet Resolution Scale Threshold to 256x256 Partially fixes screen pattern on bloom when using Resolution Scaling."] =
                "يتيح استخدام Resolution Scale في اللعبة.\nيجب استخدامه مع باتش Disable Dynamic Resolution Scale للحصول على رفع دقة صحيح وأداء أفضل.\nاضبط Resolution Scale Threshold على 256x256. يصلح جزئيا نمط الشاشة الذي يظهر على Bloom عند استخدام Resolution Scale.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. Certain 2D elements are stretched."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد بعض عناصر 2D.",
            ["Might slightly improve performance."] =
                "قد يحسن الأداء قليلا.",
            ["Required to play Online for now. Might skip something very important..."] =
                "مطلوب حاليا للعب Online. قد يتخطى شيئا مهما جدا...",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. The interface will be stretched and cutscenes will use an increased field of view which may cause visual bugs."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد الواجهة، وستستخدم المشاهد السينمائية مجال رؤية أوسع قد يسبب مشكلات بصرية.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. The interface will be stretched and cutscenes will use the standard field of view."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد الواجهة، بينما ستستخدم المشاهد السينمائية مجال الرؤية القياسي.",
            ["Normally when driving fast enough the resolution becomes 1024x600."] =
                "عادة تنخفض الدقة إلى 1024x600 عند القيادة بسرعة كافية.",
            ["Disabling SSAO (Screen Space Ambient Occlusion) does not improve performance, and makes the game look worse. Optional patch for those that wish to use it."] =
                "تعطيل SSAO (Screen Space Ambient Occlusion) لا يحسن الأداء ويجعل مظهر اللعبة أسوأ. هذا باتش اختياري لمن يرغب في استخدامه.",
            ["Enable Debug Console (DECR) mode to unlock the full menu.\nL3 + Start = Dev Menu\nL3 + Select = Quick Menu (Use this to unlock others)\nL3 + Triangle = Debug Text\nL2 + R2 = Debug Fly\nR2 = Next Frame (While Paused)\nSelect = Pause\nSelect/Start/R2 = Next Frame (While Super Paused)"] =
                "فعل وضع Debug Console (DECR) لفتح القائمة الكاملة.\nL3 + Start = Dev Menu\nL3 + Select = Quick Menu (استخدمها لفتح الخيارات الأخرى)\nL3 + Triangle = Debug Text\nL2 + R2 = Debug Fly\nR2 = الإطار التالي أثناء الإيقاف المؤقت\nSelect = إيقاف مؤقت\nSelect/Start/R2 = الإطار التالي أثناء Super Paused",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 58 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 58 FPS.",
            ["Must be used with the MLAA patch for proper upscaling and improved performance."] =
                "يجب استخدامه مع باتش MLAA للحصول على رفع دقة صحيح وأداء أفضل.",
            ["This game originally rendered at a resolution of 880x720. With this patch, it will render at a resolution of 1280x720."] =
                "كانت اللعبة تعرض أصلا بدقة 880x720. مع هذا الباتش ستعرض بدقة 1280x720.",
            ["Updated with a new version that has correct game speed above 15 FPS.\nIncrease Vblank above 60Hz to go above 60 FPS.\nSome UI elements are still tied to target FPS."] =
                "تم تحديثه بإصدار جديد يحافظ على سرعة اللعبة الصحيحة فوق 15 FPS.\nارفع VBlank Frequency فوق 60 Hz لتجاوز 60 FPS.\nما تزال بعض عناصر UI مرتبطة بمعدل FPS المستهدف.",
            ["Cutscenes will not play with FPS higher than 60. Camera sensitivity lowers at higher FPS."] =
                "لن تعمل المشاهد السينمائية إذا تجاوز FPS قيمة 60. كما تنخفض حساسية الكاميرا عند معدلات FPS الأعلى.",
            ["Improves performance at the cost of graphics quality."] =
                "يحسن الأداء مقابل خفض جودة الرسوم.",
            ["L3 + Start = Dev Menu\nL3 + Select = Quick Menu\nL3 + Triangle = Debug Text\nL2 + R2 = Debug Fly"] =
                "L3 + Start = فتح Dev Menu\nL3 + Select = فتح Quick Menu\nL3 + Triangle = إظهار Debug Text\nL2 + R2 = تفعيل Debug Fly",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to 62 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات حتى 62 FPS.",
            ["Patch accelerates some game effects by 2x, and breaks some effects completely. Does not affect playability."] =
                "يسرع الباتش بعض مؤثرات اللعبة بمقدار 2x ويعطل بعض المؤثرات تماما. لا يؤثر ذلك في إمكانية اللعب.",
            ["Patch works fine for Single-player mode, in Multi-player mode gamespeed is doubled."] =
                "يعمل الباتش بصورة صحيحة في وضع Single-player، أما في وضع Multi-player فتتضاعف سرعة اللعبة.",
            ["Removes blur."] =
                "يزيل الضبابية.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. Certain 2D elements may be stretched."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. قد يتم تمديد بعض عناصر 2D.",
            ["Set VBlank Frequency to 60. Gameplay will slow down if the framerate dips below 60."] =
                "اضبط VBlank Frequency على 60. ستتباطأ اللعبة إذا انخفض معدل الإطارات عن 60.",
            ["Vblank frequency must be 120Hz for 60 FPS or 240Hz for 120 FPS.; Going over 120 FPS is possible, but physics will break a bit (lowering jump height) and make it impossible to lock-on to enemies when flying on the dragon."] =
                "يجب أن تكون VBlank Frequency على 120 Hz للحصول على 60 FPS أو 240 Hz للحصول على 120 FPS؛ يمكن تجاوز 120 FPS، لكن الفيزياء ستتأثر قليلا بخفض ارتفاع القفز، وسيصبح تثبيت الهدف على الأعداء أثناء الطيران بالتنين مستحيلا.",
            ["Allows for the use of Resolution Scaling in the title and improves performance. Some screen effects will be lost. Write Color Buffers is no longer required."] =
                "يتيح استخدام Resolution Scale في اللعبة ويحسن الأداء. ستفقد بعض مؤثرات الشاشة. لم يعد Write Color Buffers مطلوبا.",
            ["Bound to cause issues, needs further testing."] =
                "من المتوقع أن يسبب مشكلات ويحتاج إلى مزيد من الاختبار.",
            ["Camera sensitivity gets increased while having 60+ FPS."] =
                "تزداد حساسية الكاميرا عند تشغيل اللعبة فوق 60 FPS.",
            ["Can give a significant performance boost."] =
                "قد يمنح تحسنا كبيرا في الأداء.",
            ["Completely unlocks FPS and doesn't require any special settings. Use Framelimit or VSync option to cap the framerate."] =
                "يفتح FPS بالكامل ولا يتطلب إعدادات خاصة. استخدم Framelimit أو VSync لتحديد الحد الأقصى لمعدل الإطارات.",
            ["Compromises visuals for extra performance."] =
                "يخفض جودة الرسوم مقابل أداء إضافي.",
            ["Disable Tint and/or FishEye visual effects."] =
                "يعطل مؤثرات Tint وFishEye أو أحدهما.",
            ["Disables caching textures for karts/mods to prevent texture issues, may cause slower loads"] =
                "يعطل تخزين خامات العربات والتعديلات مؤقتا لمنع مشكلات الخامات، وقد يسبب تحميلات أبطأ.",
            ["Disables prop budgets in track studio"] =
                "يعطل حدود العناصر في Track Studio.",
            ["Enable Debug (DECR) Console mode to unlock full menu.\nL3 + Start = Dev Menu\nL3 + Select = Quick Menu\nL3 + Triangle = Debug Text\nL2 + R2 = Debug Fly"] =
                "فعل وضع Debug (DECR) Console لفتح القائمة الكاملة.\nL3 + Start = Dev Menu\nL3 + Select = Quick Menu\nL3 + Triangle = Debug Text\nL2 + R2 = Debug Fly",
            ["Known Issues:\nDelay after the reload is increased at lower FPS.\nGuns shoots longer at lower FPS.\nDoesn't work in multiplayer."] =
                "مشكلات معروفة:\nيزداد التأخير بعد إعادة التلقيم عند FPS المنخفض.\nتستمر الأسلحة في الإطلاق مدة أطول عند FPS المنخفض.\nلا يعمل في Multi-player.",
            ["Turns on developer categories"] =
                "يفعل فئات المطور.",
            ["Allows for proper gamespeed at 60 FPS.\nSet VBlank to 120."] =
                "يتيح سرعة لعب صحيحة عند 60 FPS.\nاضبط VBlank Frequency على 120.",
            ["Allows for the use of Resolution Scaling in the title and improves performance."] =
                "يتيح استخدام Resolution Scale في اللعبة ويحسن الأداء.",
            ["Allows the game to run at 60 FPS when the VBlank frequency is set to 120 Hz.\n\n※ While this patch does set the frame limit to be half of the VBlank frequency, the game will not actually run any higher than 62 FPS except during the legal/copyright notice screen that is shown at boot."] =
                "يتيح تشغيل اللعبة عند 60 FPS عندما تكون VBlank Frequency مضبوطة على 120 Hz.\n\nمع أن هذا الباتش يضبط Frame limit ليكون نصف VBlank Frequency، فلن تعمل اللعبة فعليا بأكثر من 62 FPS إلا أثناء شاشة الإشعار القانوني وحقوق النشر التي تظهر عند الإقلاع.",
            ["Allows you to play the game at 60 FPS or higher without modifying the vblank frequency. Can improve performance."] =
                "يتيح تشغيل اللعبة عند 60 FPS أو أعلى من دون تعديل VBlank Frequency، وقد يحسن الأداء.",
            ["Cutscenes rendered in the game's engine are sped up."] =
                "تعمل المشاهد السينمائية التي يرسمها محرك اللعبة بسرعة أعلى.",
            ["Disables the DoF effect which doesn't correctly scale with resolution upscaling. This patch may slightly affect the game brightness in some levels."] =
                "يعطل تأثير DoF الذي لا يتدرج بصورة صحيحة مع رفع الدقة. قد يؤثر هذا الباتش قليلا في سطوع اللعبة في بعض المراحل.",
            ["Does not provide any performance benefit."] =
                "لا يقدم أي فائدة في الأداء.",
            ["Fixes game speedup at 60fps and above."] =
                "يصلح تسارع سرعة اللعبة عند 60 FPS وما فوق.",
            ["Increases FOV to match 16:9. Only for aspect ratios wider than 16:9."] =
                "يزيد FOV ليتوافق مع 16:9. مخصص لنسب العرض الأوسع من 16:9 فقط.",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to 58 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات حتى 58 FPS.",
            ["Removes all viewport filters including color grading and the night vision goggles effect."] =
                "يزيل جميع فلاتر Viewport بما فيها Color Grading وتأثير نظارات الرؤية الليلية.",
            ["Removes some effects such as explosions and smoke. Provides a moderate performance boost."] =
                "يزيل بعض المؤثرات مثل الانفجارات والدخان، ويوفر تحسنا متوسطا في الأداء.",
            ["Removes some transparency related effects from stages. Provides a small performance boost, but causes monitor visuals to break in-game."] =
                "يزيل بعض المؤثرات المرتبطة بالشفافية من المراحل. يوفر تحسنا بسيطا في الأداء، لكنه يسبب تعطل المرئيات على الشاشات داخل اللعبة.",
            ["Removes the ingame HUD. Useful for videos and screenshots."] =
                "يزيل HUD داخل اللعبة. مفيد للفيديوهات ولقطات الشاشة.",
            ["Removes the ~62 FPS limit. Maximum framerate is half the Vblank frequency."] =
                "يزيل حد نحو 62 FPS. الحد الأقصى لمعدل الإطارات يساوي نصف VBlank Frequency.",
            ["Requires \"Hor+ FOV\" patch for wider than 16:9 and \"Stretch To Display Area\" checked in the GPU settings. Certain 2D elements are stretched."] =
                "يتطلب باتش \"Hor+ FOV\" لنسب العرض الأوسع من 16:9، كما يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد بعض عناصر 2D.",
            ["Skips the Rockstar boot logo like the PS5 version.\n\n※ Does not skip the legal/copyright notice screen."] =
                "يتخطى شعار Rockstar عند الإقلاع مثل إصدار PS5.\n\nلا يتخطى شاشة الإشعار القانوني وحقوق النشر.",
            ["Some elements speed is tied to FPS."] =
                "سرعة بعض العناصر مرتبطة بـFPS.",
            ["Allows framerates of up to 60 FPS."] =
                "يتيح معدلات إطارات تصل إلى 60 FPS.",
            ["Allows gameplay on keyboard and controllers without motion sensors. Can't absorb multiple folk at once meaning lower XP gains."] =
                "يتيح اللعب بلوحة المفاتيح ووحدات التحكم بدون مستشعرات حركة. لا يمكن امتصاص عدة Folk في الوقت نفسه، ما يعني مكاسب XP أقل.",
            ["Breaks menu background video."] =
                "يعطل فيديو خلفية القائمة.",
            ["Bypasses intro logos (THX, Incognito, Sony, Santa Monica) and jumps to the landing page."] =
                "يتجاوز شعارات البداية THX وIncognito وSony وSanta Monica وينتقل مباشرة إلى صفحة البداية.",
            ["Completely unlocks the framerate."] =
                "يفتح معدل الإطارات بالكامل.",
            ["Default values:\n Horizontal: 0.78\n Vertical: 0.62"] =
                "القيم الافتراضية:\n Horizontal: 0.78\n Vertical: 0.62",
            ["Disables MLAA, allowing for the use of resolution scaling. Use in conjunction with the SPU MLAA patch for better performance."] =
                "يعطل MLAA، ما يتيح استخدام Resolution Scale. استخدمه مع باتش SPU MLAA للحصول على أداء أفضل.",
            ["Force 60 FPS in all modes."] =
                "يفرض 60 FPS في جميع الأوضاع.",
            ["Hijack the texture loading function and override the clouds being rendered."] =
                "يعترض دالة تحميل الخامات ويمنع رسم السحب.",
            ["Improves performance slightly by using GPU path for lighting.\nFixes lighting issues in Chapter 9 when shining the beams of light onto the statues."] =
                "يحسن الأداء قليلا باستخدام مسار GPU للإضاءة.\nيصلح مشكلات الإضاءة في Chapter 9 عند تسليط أشعة الضوء على التماثيل.",
            ["Patch version 2.0 and above is not compatible with the settings used for the previous patch. Clocks scale and Vblank rate must be set to the defaults. Vblank rate can be modified to increase the framelimit beyond 60 FPS. However, it's not recommended as users may encounter physics issues."] =
                "إصدار الباتش 2.0 وما بعده غير متوافق مع الإعدادات المستخدمة للباتش السابق. يجب إعادة Clocks scale وVblank Rate إلى القيم الافتراضية. يمكن تعديل Vblank Rate لرفع حد الإطارات فوق 60 FPS، لكن ذلك غير موصى به لأن المستخدم قد يواجه مشكلات في الفيزياء.",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance."] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.",
            ["Required to play Online for now"] =
                "مطلوب حاليا للعب Online.",
            ["Tweaks the chase camera to be a little less static. Vanilla values are 300/30"] =
                "يعدل Chase Camera لتصبح أقل ثباتا بقليل. القيم الأصلية هي 300/30.",
            ["Unlocks FPS during gameplay. Cloth physics break above 50 FPS despite the gameplay working."] =
                "يفتح FPS أثناء اللعب. تتعطل فيزياء الملابس فوق 50 FPS رغم استمرار اللعب بصورة صحيحة.",
            ["When using the FPS unlock patch the horizontal camera sensitivity gets very low. This patch changes a multiplier so that the horizontal camera sensitivity slider in the game's settings does something meaningful again. The multiplier can be altered in the patch file."] =
                "عند استخدام باتش فتح FPS تصبح حساسية الكاميرا الأفقية منخفضة جدا. يغير هذا الباتش معامل الضرب بحيث يصبح منزلق حساسية الكاميرا الأفقية في إعدادات اللعبة مفيدا من جديد. ويمكن تعديل معامل الضرب داخل ملف الباتش.",
            ["Activate and cycle through modes with X + L3. Advance frames in time stop mode with Cross. L2/R2 to move down/up, L1/R1 to slow down/speed up movement."] =
                "فعل الأوضاع وبدل بينها باستخدام X + L3. تقدم إطارا واحدا في وضع إيقاف الزمن باستخدام Cross. استخدم L2/R2 للحركة إلى أسفل/أعلى، وL1/R1 لإبطاء/تسريع الحركة.",
            ["Allows for the use of Resolution Scaling and improves performance.\nVersion 2.0 - Fixes crashes introduced in the original patch.\nVersion 3.0 - Fixes the Green Rain in more areas.\nVersion 4.0 - Fixes high levels of Bloom in some areas.\n\nKnown issues: Listen mode does not work."] =
                "يتيح استخدام Resolution Scale ويحسن الأداء.\nVersion 2.0 - يصلح الانهيارات التي ظهرت في الباتش الأصلي.\nVersion 3.0 - يصلح Green Rain في مناطق إضافية.\nVersion 4.0 - يصلح مستويات Bloom المرتفعة في بعض المناطق.\n\nمشكلات معروفة: لا يعمل Listen mode.",
            ["Allows for the use of Resolution Scaling in the title and improves performance.\nVersion 2.0 Fixes high levels of bloom in some areas. (1.10 and 1.19 Only)\nVersion 3.0 Fixes Crash in some multiplayer levels. (1.19 Only)"] =
                "يتيح استخدام Resolution Scale في اللعبة ويحسن الأداء.\nVersion 2.0 يصلح مستويات Bloom المرتفعة في بعض المناطق. (1.10 و1.19 فقط)\nVersion 3.0 يصلح الانهيار في بعض مراحل Multiplayer. (1.19 فقط)",
            ["Allows for the use of Resolution Scaling in this title.\nSet Resolution Scale Threshold to 1x1 for proper scaling of effects."] =
                "يتيح استخدام Resolution Scale في هذه اللعبة.\nاضبط Resolution Scale Threshold على 1x1 لضمان تدرج المؤثرات بصورة صحيحة.",
            ["Allows the framerate to exceed 65. Still need to set vblank to unlock framerate."] =
                "يتيح لمعدل الإطارات تجاوز 65. ما يزال يلزم ضبط VBlank Frequency لفتح معدل الإطارات.",
            ["Breaks ingame graphics."] =
                "يعطل الرسوم داخل اللعبة.",
            ["Disables broken shifting/refracting effect."] =
                "يعطل تأثير الانزياح/الانكسار المعطل.",
            ["Disables the slow-down effect when having low FPS.\nIncreasing clocks scale allows to speed up the game."] =
                "يعطل تأثير التباطؤ عند انخفاض FPS.\nتؤدي زيادة Clocks scale إلى تسريع اللعبة.",
            ["Field Of View.\nLower FOV = Less things to render = slightly higher fps"] =
                "Field Of View.\nخفض FOV = عناصر أقل للرسم = FPS أعلى قليلا.",
            ["Fixes green graphics when using Resolution Scaling. Do not use this patch if you're not upscaling, it'll introduce white squared artifacts in areas throughout the game."] =
                "يصلح الرسوم الخضراء عند استخدام Resolution Scale. لا تستخدم هذا الباتش إذا لم تكن ترفع الدقة، لأنه سيضيف تشوهات مربعة بيضاء في مناطق متعددة من اللعبة.",
            ["Fully unlocks framerate. No longer requires to change VBlank."] =
                "يفتح معدل الإطارات بالكامل. لم يعد يتطلب تغيير VBlank Frequency.",
            ["Impacts the player's vehicle physics (reduces grip).\nMaximum FPS corresponds to your VBlank Frequency, but higher values will reduce grip further.\nUpdated with a new version that has correct game speed above 15 FPS except for the countdown timer.\nIssues introduced in version 1.0 of the patch have been resolved."] =
                "يؤثر في فيزياء مركبة اللاعب ويقلل التماسك.\nالحد الأقصى لـFPS يطابق VBlank Frequency، لكن القيم الأعلى تقلل التماسك أكثر.\nتم تحديثه بإصدار جديد يحافظ على سرعة اللعبة الصحيحة فوق 15 FPS باستثناء مؤقت العد التنازلي.\nتم حل المشكلات التي ظهرت في الإصدار 1.0 من الباتش.",
            ["Maximum framerate with the patch is half the Vblank frequency.\n\nIncreasing the framerate causes the cutscene before levels to be black, but it can still be waited out, or skipped with Start+Cross."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency.\n\nيسبب رفع معدل الإطارات ظهور شاشة سوداء في المشهد السينمائي السابق للمراحل، لكن يمكن الانتظار حتى ينتهي أو تخطيه باستخدام Start+Cross.",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 57 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 57 FPS.",
            ["Maximum framerate with the patch is proportionate to Vblank frequency. More than 60 Vblank is not recommended as framerates above 75 can cause issues with left analog-stick input detection that are caused by limitations in the game engine. Some in-game actions, namely ladder climbing, may require several attempts to register a proper interaction."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يتناسب مع VBlank Frequency. لا ينصح بتجاوز 60 في VBlank Frequency لأن معدلات الإطارات فوق 75 قد تسبب مشكلات في اكتشاف إدخال العصا التناظرية اليسرى بسبب قيود محرك اللعبة. قد تتطلب بعض الإجراءات داخل اللعبة، وخصوصا تسلق السلالم، عدة محاولات حتى تسجل بصورة صحيحة.",
            ["May work with JP version with adjustments. May cause double cutscenes audio and/or video. May also cause some trinity cutscenes to loop as white screen."] =
                "قد يعمل مع إصدار JP بعد بعض التعديلات. قد يسبب تكرار صوت و/أو فيديو المشاهد السينمائية، وقد يؤدي أيضا إلى تكرار بعض مشاهد Trinity كشاشة بيضاء.",
            ["No longer requires Write Color Buffers."] =
                "لم يعد يتطلب Write Color Buffers.",
            ["Original resolution is 960x704. Game still requires Anti Aliasing set to Auto. Some things are still rendered at half resolution."] =
                "الدقة الأصلية 960x704. يجب ضبط Anti Aliasing على Auto. بعض العناصر ما تزال ترسم بنصف الدقة.",
            ["Press Select+R1 to activate in main menu."] =
                "اضغط Select+R1 للتفعيل في القائمة الرئيسية.",
            ["Reduce the delay of your button presses by 2 frames in all offline modes. \nThis works by completely disabling the forced 'Practice with Frame Delay' system listed in Training (online will still work)."] =
                "يخفض تأخير ضغطات الأزرار بمقدار 2 إطار في جميع الأوضاع Offline.\nيعمل ذلك عبر تعطيل نظام 'Practice with Frame Delay' الإجباري بالكامل والمذكور في Training، بينما يظل Online يعمل.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. Fixes stretching and frustum culling. HUD is stretched."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. يصلح التمدد وFrustum Culling. سيتم تمديد HUD.",
            ["Requires the \"Aspect Ratio\" patch to be enabled as well as the \"Stretch To Display Area\" checked in the GPU settings. Centers HUD/menus by offsetting the UI quad while using the wider UI width."] =
                "يتطلب تفعيل باتش \"Aspect Ratio\" مع تفعيل \"Stretch To Display Area\" في إعدادات GPU. يوسطن HUD والقوائم عبر إزاحة UI Quad مع استخدام عرض UI الأوسع.",
            ["Run at any FPS with proper game speed. Increase VBlank for 30+ FPS."] =
                "يشغل اللعبة عند أي FPS مع سرعة لعب صحيحة. زد VBlank Frequency لتجاوز 30 FPS.",
            ["Set Frame limit to 60. Gamespeed is affected above 60 FPS."] =
                "اضبط Frame limit على 60. تتأثر سرعة اللعبة عند تجاوز 60 FPS.",
            ["The main menu doesn't play music the first time it's opened."] =
                "لا تعمل موسيقى القائمة الرئيسية في أول مرة يتم فتحها.",
            ["To change the maximum bullet counts for each character, edit this patch and input your desired values. By default, this patch retains the game defaults until modified."] =
                "لتغيير الحد الأقصى لعدد الطلقات لكل شخصية، عدل هذا الباتش وأدخل القيم المطلوبة. افتراضيا يحتفظ الباتش بقيم اللعبة الأصلية حتى تعدلها.",
            ["Unlocks a stage venue called \"Pitch Black Out\", which has a completely black background. Useful for getting more performance on lower-spec PCs."] =
                "يفتح ساحة باسم \"Pitch Black Out\" بخلفية سوداء بالكامل. مفيد للحصول على أداء أفضل على أجهزة PC ذات المواصفات المنخفضة.",
            ["Unlocks FPS without any special settings.\nGame may crash above 500 FPS in loading screens."] =
                "يفتح FPS من دون أي إعدادات خاصة.\nقد تنهار اللعبة فوق 500 FPS أثناء شاشات التحميل.",
            ["Uses GPU (RSX) path for lighting effects instead of SPU.\nFixes rendering of lighting effects that are broken via SPU path and greatly improves performance over it.\nThis path is incomplete and results in some missing effects such as Volumetric lighting."] =
                "يستخدم مسار GPU (RSX) لمؤثرات الإضاءة بدلا من SPU.\nيصلح عرض مؤثرات الإضاءة المعطلة عبر مسار SPU ويحسن الأداء بدرجة كبيرة مقارنة به.\nهذا المسار غير مكتمل وقد يؤدي إلى فقدان بعض المؤثرات مثل Volumetric Lighting.",
            ["Allows for variable FPS and correct game speed above 15 FPS.\n\nKnown issues:\n Slower movement through water, especially over 60 FPS.\n Camera shakes when using PPU recompiler. Patch will be kept separate until resolved."] =
                "يتيح FPS متغيرا مع سرعة لعب صحيحة فوق 15 FPS.\n\nمشكلات معروفة:\nالحركة في الماء أبطأ، خصوصا فوق 60 FPS.\nتهتز الكاميرا عند استخدام PPU Recompiler. سيظل الباتش منفصلا حتى يتم حل المشكلة.",
            ["Breaks shadows, causing them to be misaligned as well as making them jump around when the player moves."] =
                "يعطل الظلال، فيجعل محاذاتها غير صحيحة ويؤدي إلى قفزها من مكان لآخر أثناء حركة اللاعب.",
            ["Camera may zoom out during cinematic change."] =
                "قد تبتعد الكاميرا أثناء الانتقال السينمائي.",
            ["Cutscene speed is affected. Slightly improves performance."] =
                "تتأثر سرعة المشاهد السينمائية، مع تحسن بسيط في الأداء.",
            ["Default values:\n Horizontal - 0.78\n Vertical - 0.62"] =
                "القيم الافتراضية:\n Horizontal - 0.78\n Vertical - 0.62",
            ["Disable SSL reverting to plaintext communication for custom servers"] =
                "يعطل SSL ويعيد الاتصال إلى Plaintext للخوادم المخصصة.",
            ["Disables camera shake during gameplay.\nIn-Game cinematics remain unaffected."] =
                "يعطل اهتزاز الكاميرا أثناء اللعب.\nلا تتأثر المشاهد السينمائية داخل اللعبة.",
            ["Disables Camera Shake during gameplay.\nIn-Game-Cinematics are unaffected."] =
                "يعطل Camera Shake أثناء اللعب.\nلا تتأثر In-Game-Cinematics.",
            ["Disables score bubble effects for extra performance."] =
                "يعطل مؤثرات Score Bubble للحصول على أداء إضافي.",
            ["Disables SHA-1 hash checking of the following files:; coalesced_int.bin, coalesced_fra.bin, core.xxx, engine.xxx, gfxui.xxx, ipdrv.xxx, gameframework.xxx, onlinesubsystempc.xxx, cdevsounddriver_loc_int.xxx, cdevsounddriver.xxx, sqexsead.xxx, sqex03game.xxx, startup.xxx, cdevsounddriver_loc_fra.xxx"] =
                "يعطل التحقق من SHA-1 للملفات التالية:; coalesced_int.bin, coalesced_fra.bin, core.xxx, engine.xxx, gfxui.xxx, ipdrv.xxx, gameframework.xxx, onlinesubsystempc.xxx, cdevsounddriver_loc_int.xxx, cdevsounddriver.xxx, sqexsead.xxx, sqex03game.xxx, startup.xxx, cdevsounddriver_loc_fra.xxx",
            ["Disables the camera motion blur for a sharper image."] =
                "يعطل Motion Blur الخاص بالكاميرا للحصول على صورة أكثر حدة.",
            ["Displays live Blaze logs in TTY"] =
                "يعرض سجلات Blaze المباشرة في TTY.",
            ["Don't enable it for first fight with Sam or you will get soft-locked."] =
                "لا تفعله في أول مواجهة مع Sam وإلا ستعلق اللعبة في Softlock.",
            ["Enables more verbose logging"] =
                "يفعل تسجيل Logs بتفاصيل أكثر.",
            ["Everything dies in one hit."] =
                "يموت كل شيء بضربة واحدة.",
            ["For easy save data modding."] =
                "لتسهيل تعديل بيانات الحفظ.",
            ["For people who would like to play the unpatched version of Dark Souls at decent framerates. Unpatched PC version is equivalent to 1.06 on consoles."] =
                "مخصص لمن يريد لعب الإصدار غير المحدث من Dark Souls بمعدل إطارات جيد. إصدار PC غير المحدث يعادل الإصدار 1.06 على أجهزة الكونسول.",
            ["In-game menu is a little ugly, but it works."] =
                "القائمة داخل اللعبة تبدو سيئة قليلا، لكنها تعمل.",
            ["Jumps straight past the startup legal and logo screens to the menu."] =
                "يتخطى مباشرة شاشات الإشعارات القانونية والشعارات عند بدء التشغيل وينتقل إلى القائمة.",
            ["L3 + Start = Dev Menu\nL3 + Select = Quick Menu\nL3 + Triangle = FPS Text\nL3 + R3 = Debug Fly"] =
                "L3 + Start = فتح Dev Menu\nL3 + Select = فتح Quick Menu\nL3 + Triangle = إظهار FPS Text\nL3 + R3 = تفعيل Debug Fly",
            ["Maximum framerate with the patch is half the Vblank frequency. While most of the game works without any issue, some of the button mashing QTEs are tied to framerate. It's possible to play through the game at 60 FPS if you are good at button mashing, but 120 FPS and higher are unplayable in several missions. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 57 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. تعمل معظم اللعبة من دون مشكلات، لكن بعض أحداث QTE التي تتطلب ضغط الأزرار بسرعة مرتبطة بمعدل الإطارات. يمكن إنهاء اللعبة عند 60 FPS إذا كنت جيدا في ضغط الأزرار بسرعة، لكن 120 FPS وما فوق يجعل عدة مهام غير قابلة للعب. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 57 FPS.",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 33 FPS or 60 FPS depending on the scene."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 33 FPS أو 60 FPS بحسب المشهد.",
            ["May break above 60 FPS. Accelerates the turns of the character by 2 times. Speeds up the game by 2 times in splitscreen."] =
                "قد يتعطل فوق 60 FPS. يسرع دوران الشخصية بمقدار 2x، ويضاعف سرعة اللعبة بمقدار 2x في Splitscreen.",
            ["Native resolution is 1120x584."] =
                "الدقة الأصلية هي 1120x584.",
            ["No need for additional settings change.\nNeeds a stable and locked 60 FPS otherwise game will slow down.\n\nKnown issues:\nCamera moves at double speed.\nSlower movement through water."] =
                "لا حاجة إلى تغيير إعدادات إضافية.\nيحتاج إلى 60 FPS ثابتة ومقفلة، وإلا ستتباطأ اللعبة.\n\nمشكلات معروفة:\nتتحرك الكاميرا بسرعة مضاعفة.\nالحركة في الماء أبطأ.",
            ["Original resolution is 960x704. 1280x720 breaks rendering and requires more research."] =
                "الدقة الأصلية 960x704. الدقة 1280x720 تعطل العرض وتحتاج إلى مزيد من البحث.",
            ["Patch accelerates some game effects by 2x. Does not affect playability."] =
                "يسرع الباتش بعض مؤثرات اللعبة بمقدار 2x. لا يؤثر في إمكانية اللعب.",
            ["Press D-pad Left at any time to access the Developer Menu."] =
                "اضغط D-pad Left في أي وقت للوصول إلى Developer Menu.",
            ["Press L3 or R3 on Map Screen."] =
                "اضغط L3 أو R3 في Map Screen.",
            ["Press R1 + START in-game to access the Debug Menu (god mode, no clip, etc.)"] =
                "اضغط R1 + START داخل اللعبة للوصول إلى Debug Menu مثل God Mode وNo Clip وغيرها.",
            ["Press SELECT in Pause Menu."] =
                "اضغط SELECT في Pause Menu.",
            ["Reduce the delay of your button presses by 2 frames in all offline modes. \nThis works by completely disabling the forced 'Input Delay' system listed in Practice Mode (online will still work)."] =
                "يخفض تأخير ضغطات الأزرار بمقدار 2 إطار في جميع أوضاع Offline.\nيعمل ذلك عبر تعطيل نظام 'Input Delay' الإجباري بالكامل والمذكور في Practice Mode، بينما يظل Online يعمل.",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nGod of War: Ascension"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nGod of War: Ascension",
            ["Removes the 30FPS lock in gameplay and cutscenes, with proper working physics, set \"Frame limit\" to \"Off\" to increase the maximum framerate however physics become broken"] =
                "يزيل قفل 30 FPS أثناء اللعب والمشاهد السينمائية مع عمل الفيزياء بصورة صحيحة. اضبط \"Frame limit\" على \"Off\" لرفع الحد الأقصى لمعدل الإطارات، لكن الفيزياء ستتعطل.",
            ["Removes the PV watermark."] =
                "يزيل العلامة المائية PV.",
            ["Removes the stock 40fps cap."] =
                "يزيل الحد الأصلي البالغ 40 FPS.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. HUD will be stretched. Certain scenes may have visual bugs."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد HUD، وقد تظهر مشكلات بصرية في بعض المشاهد.",
            ["Restores the functionality of the network test client."] =
                "يعيد وظيفة Network Test Client.",
            ["Select + Start = Brings Menu\nL3 + R3 = Debug Camera"] =
                "Select + Start = إظهار القائمة\nL3 + R3 = Debug Camera",
            ["SELECT+START = Brings menu.\nUsing the menu makes the screen red until a new map will be loaded."] =
                "SELECT+START = إظهار القائمة.\nاستخدام القائمة يجعل الشاشة حمراء حتى يتم تحميل خريطة جديدة.",
            ["Set Console Language to Polish. Replaces Russian Language."] =
                "اضبط Console Language على Polish. يستبدل Russian Language.",
            ["Skips Starhawk's Online Pass / voucher prompt so you can connect to online multiplayer without redeeming a code."] =
                "يتخطى مطالبة Starhawk الخاصة بـOnline Pass / Voucher لتتمكن من الاتصال بـOnline Multiplayer من دون استخدام رمز.",
            ["Slightly improves performance at the cost of worse graphics."] =
                "يحسن الأداء قليلا مقابل رسوم أسوأ.",
            ["Slightly improves performance."] =
                "يحسن الأداء قليلا.",
            ["This patch allows the files regulation.bin, boot.bnd, ingamescript.bnd, and menustay.bnd to be loaded from an alternative path. If these files are placed in [dev_hdd0/game/[region]/alt/], the game will load them from that folder. If the files are not present there, the game will load them from the normal path."] =
                "يسمح هذا الباتش بتحميل الملفات regulation.bin وboot.bnd وingamescript.bnd وmenustay.bnd من مسار بديل. إذا وضعت هذه الملفات في [dev_hdd0/game/[region]/alt/] فستحملها اللعبة من هذا المجلد. وإذا لم تكن موجودة هناك فستحملها من المسار المعتاد.",
            ["This patch changes the PvP time limit from 15 minutes to 32,767 minutes in order to improve debugging efficiency."] =
                "يغير هذا الباتش حد وقت PvP من 15 دقيقة إلى 32,767 دقيقة لتحسين كفاءة Debugging.",
            ["This patch ensures that the weapon parts of ACs located more than 500 units away are rendered. This improves the visual effects during combat."] =
                "يضمن هذا الباتش رسم أجزاء أسلحة ACs الموجودة على بعد أكثر من 500 وحدة، ما يحسن المؤثرات البصرية أثناء القتال.",
            ["This patch fixes a lock-on issue that occurs under certain conditions in PvP."] =
                "يصلح هذا الباتش مشكلة Lock-on تحدث في ظروف معينة في PvP.",
            ["This patch fixes an issue in PvP where hand missiles stored in a tank’s hanger do not properly track their target. As a result, it increases the variety of possible tank builds. This patch operates on the attacker side."] =
                "يصلح هذا الباتش مشكلة في PvP لا تتبع فيها Hand Missiles المخزنة في Hanger الخاص بالدبابة هدفها بصورة صحيحة. ونتيجة لذلك يزيد تنوع تركيبات الدبابات الممكنة. يعمل هذا الباتش على جهة المهاجم.",
            ["This patch is useful for people who play with something other than DualShock3/4.\nNormaly flashlight battery drains after ~3600 frames, patch disables battery drain, Infinite flashlight battery no controller shake."] =
                "هذا الباتش مفيد لمن يلعب بوحدة تحكم غير DualShock3/4.\nعادة تنفد بطارية المصباح بعد نحو 3600 إطار. يعطل الباتش استنزاف البطارية ويجعل بطارية المصباح غير محدودة من دون الحاجة إلى اهتزاز وحدة التحكم.",
            ["This patch prevents the lock cursor from disappearing while missiles are being fired. As a result, it improves weapon accuracy, especially when using continuous fire missiles, and prevents accuracy loss for weapons such as machine guns that are fired at the same time. This patch operates on the attacker side."] =
                "يمنع هذا الباتش اختفاء مؤشر Lock أثناء إطلاق الصواريخ. وبذلك يحسن دقة السلاح، خصوصا عند استخدام الصواريخ ذات الإطلاق المستمر، ويمنع فقدان الدقة لأسلحة مثل الرشاشات التي تطلق في الوقت نفسه. يعمل هذا الباتش على جهة المهاجم.",
            ["Uses GPU (RSX) path for lighting effects instead of SPU.\nFixes rendering of lightning effects that are being rendered broken via SPU path and greatly improves performance over it.\nThis path is incomplete and results in some missing effects such as Volumetric lighting.\nVersion 2.0 - Fixes Cabin Resort level crashes/artifacts without removing the fog (1.11 only)"] =
                "يستخدم مسار GPU (RSX) لمؤثرات الإضاءة بدلا من SPU.\nيصلح عرض مؤثرات Lightning المعطلة عبر مسار SPU ويحسن الأداء بدرجة كبيرة مقارنة به.\nهذا المسار غير مكتمل وقد يؤدي إلى فقدان بعض المؤثرات مثل Volumetric Lighting.\nVersion 2.0 - يصلح انهيارات وتشوهات مرحلة Cabin Resort من دون إزالة الضباب. (1.11 فقط)",
            ["60 fps gameplay, set vblank rate to 120hz. Game slows down if below 60fps."] =
                "لعب عند 60 FPS؛ اضبط Vblank Rate على 120 Hz. تتباطأ اللعبة إذا انخفضت عن 60 FPS.",
            ["Allows 60 FPS without changing Vblank."] =
                "يتيح 60 FPS من دون تغيير VBlank Frequency.",
            ["Allows flowscripts to print strings to TTY Log."] =
                "يسمح لـFlowscripts بطباعة النصوص في TTY Log.",
            ["Allows for proper gamespeed at 60 FPS.\nSet VBlank to 120.\nSome stuff may still be 2x speed."] =
                "يتيح سرعة لعب صحيحة عند 60 FPS.\nاضبط VBlank Frequency على 120.\nقد تبقى بعض العناصر بسرعة 2x.",
            ["Allows for the use of Resolution Scaling in the title.\nSet Resolution Scale Threshold to 160x160 or some effects will break.\n2.0 Slightly improves performance when using resolution scaling in some cases."] =
                "يتيح استخدام Resolution Scale في اللعبة.\nاضبط Resolution Scale Threshold على 160x160 وإلا ستتعطل بعض المؤثرات.\n2.0 يحسن الأداء قليلا عند استخدام Resolution Scale في بعض الحالات.",
            ["Allows you and enemies to shout without a resting period. Becomes Never shout again for all characters on screen that are in a resting period."] =
                "يتيح لك وللأعداء الصراخ من دون فترة راحة. بالنسبة للشخصيات الظاهرة على الشاشة التي تكون بالفعل في فترة راحة، يتحول التأثير إلى منعها من الصراخ مجددا.",
            ["Apply this patch while you can still run. Unless you prefer being over-encumbered."] =
                "طبق هذا الباتش بينما ما تزال قادرا على الجري، إلا إذا كنت تفضل البقاء محملا فوق الحد.",
            ["Disable HUD by disabling rendering of 2D elements entirely. Breaks things."] =
                "يعطل HUD عبر تعطيل رسم عناصر 2D بالكامل. يعطل بعض الأشياء.",
            ["Disables in-game HUD. Needs further testing."] =
                "يعطل HUD داخل اللعبة. يحتاج إلى مزيد من الاختبار.",
            ["Disables Navigator battle lines (voice and message popups)."] =
                "يعطل حوارات Navigator أثناء القتال، بما فيها الصوت والنوافذ المنبثقة للرسائل.",
            ["Disables some (not all) bloom effects."] =
                "يعطل بعض مؤثرات Bloom وليس كلها.",
            ["Disables Sunlight ShadowBuffer Draw (dynamic shadows) which is done on SPUs.\nImproves Performance."] =
                "يعطل Sunlight ShadowBuffer Draw، أي الظلال الديناميكية التي تنفذ على وحدات SPU.\nيحسن الأداء.",
            ["Disables the crowd noises that can be heard in the menus."] =
                "يعطل أصوات الحشود التي تسمع في القوائم.",
            ["Disables the DoF effect which doesn't correctly scale with resolution upscaling. This patch may slightly affect color-grading and will reduce bloom intensity because those effects share the same shader pipeline with DoF. Additionally, 16x Anisotropic Filtering can now be forced via custom config without causing issues."] =
                "يعطل تأثير DoF الذي لا يتدرج بصورة صحيحة مع رفع الدقة. قد يؤثر هذا الباتش قليلا في Color Grading ويخفض شدة Bloom لأن هذه المؤثرات تشترك في Shader Pipeline نفسه مع DoF. بالإضافة إلى ذلك يمكن الآن فرض 16x Anisotropic Filtering عبر Custom Config من دون التسبب في مشكلات.",
            ["Does not require user to change vblank settings.; Going over 120 FPS is possible, but physics will break a bit (lowering jump height) and make it impossible to lock-on to enemies when flying on the dragon."] =
                "لا يتطلب من المستخدم تغيير إعدادات VBlank Frequency؛ يمكن تجاوز 120 FPS، لكن الفيزياء ستتأثر قليلا بخفض ارتفاع القفز، وسيصبح تثبيت الهدف على الأعداء أثناء الطيران بالتنين مستحيلا.",
            ["Doesn't affect loss after death.\nNeeds testing for other merchants."] =
                "لا يؤثر في الخسارة بعد الموت.\nيحتاج إلى اختبار مع التجار الآخرين.",
            ["Doubles gravity, some stuff moves at double speed."] =
                "يضاعف الجاذبية، وتتحرك بعض العناصر بسرعة مضاعفة.",
            ["Elements can be disabled separately by adding/removing their respective lines from the patch."] =
                "يمكن تعطيل العناصر بصورة منفصلة عبر إضافة أو إزالة الأسطر الخاصة بها من الباتش.",
            ["Enable support for adding mod files (through mod.cpk) to the game. This feature is required to fix the dialog portraits when using resolution scaling."] =
                "يفعل دعم إضافة ملفات Mod إلى اللعبة عبر mod.cpk. هذه الميزة مطلوبة لإصلاح صور الحوارات عند استخدام Resolution Scale.",
            ["Enables Full Pixel Mode on start so you don't have to change it in the options. Disables black borders/overscan."] =
                "يفعل Full Pixel Mode عند بدء اللعبة حتى لا تحتاج إلى تغييره من الخيارات، كما يعطل الحدود السوداء وOverscan.",
            ["Essentially permanent if you save with this patch enabled - adds over 1 billion caps to your inventory which will remain after the patch is disabled."] =
                "يصبح تأثيره دائما فعليا إذا حفظت والباتش مفعل؛ يضيف أكثر من 1 مليار Caps إلى مخزونك وتبقى بعد تعطيل الباتش.",
            ["Fixes a bug where throwing an object at an Infected Severed Head causes a game crash."] =
                "يصلح خطأ يتسبب في انهيار اللعبة عند رمي جسم على Infected Severed Head.",
            ["Fixes a rare bug where quitting to menu causes a game crash."] =
                "يصلح خطأ نادرا يتسبب في انهيار اللعبة عند الخروج إلى القائمة.",
            ["Fixes ship-shield and absorb glow effect getting out of sync when playing above 60 FPS. The \"120\" value can be changed to match your display framerate."] =
                "يصلح خروج مؤثر Ship Shield وAbsorb Glow عن التزامن عند اللعب فوق 60 FPS. يمكن تغيير القيمة \"120\" لتطابق معدل إطارات الشاشة.",
            ["Game normally starts with smooth filter."] =
                "تبدأ اللعبة عادة مع تفعيل Smooth Filter.",
            ["Haven't tested how this affects cutscenes."] =
                "لم يختبر بعد تأثير هذا على المشاهد السينمائية.",
            ["Increases the FOV from the default 70. Also affects viewmodels. FOV is decreased when in water."] =
                "يزيد FOV عن القيمة الافتراضية 70، ويؤثر أيضا في Viewmodels. ينخفض FOV عند الدخول في الماء.",
            ["Increases the internal resolution from 1024x576 to 1280x720."] =
                "يزيد الدقة الداخلية من 1024x576 إلى 1280x720.",
            ["Infinite ammo and grenades."] =
                "ذخيرة وقنابل غير محدودة.",
            ["L3 = Opens Debug Menu\nL1/R1 = Change pages"] =
                "L3 = فتح Debug Menu\nL1/R1 = تغيير الصفحات",
            ["Level up every skill level up. Quick way to unlock perks without unnecessarily making your skills Legendary."] =
                "يرفع كل Skill Level عند رفع المستوى. طريقة سريعة لفتح Perks من دون جعل المهارات Legendary بلا حاجة.",
            ["Like the PS2 releases, the remastered versions of the first two games handle 16:9 aspect ratio by cropping out 25% of the vertical viewport instead of expanding the horizontal FOV. This patch fixes it and makes the game render in proper 16:9."] =
                "مثل إصدارات PS2، تتعامل النسخ المحسنة من أول لعبتين مع نسبة 16:9 بقص 25% من Viewport الرأسي بدلا من توسيع FOV الأفقي. يصلح هذا الباتش ذلك ويجعل اللعبة تعرض 16:9 بصورة صحيحة.",
            ["Lowers/Increases Object Render Distance.\nHigher Values increase render distance.\nCan improve performance or make it worse."] =
                "يخفض أو يزيد Object Render Distance.\nالقيم الأعلى تزيد مسافة الرسم.\nقد يحسن الأداء أو يجعله أسوأ.",
            ["Makes player hosted 'friends-only' game public, allowing anybody to join."] =
                "يجعل مباراة Friends-only التي يستضيفها اللاعب عامة، ما يسمح لأي شخص بالانضمام.",
            ["Makes the game playable by skipping the PSP connection screen. A decrypted save is still required to play the game. The patch can also be used on real hardware."] =
                "يجعل اللعبة قابلة للعب عبر تخطي شاشة اتصال PSP. ما يزال يلزم ملف حفظ مفكوك التشفير للعب. يمكن أيضا استخدام الباتش على الجهاز الحقيقي.",
            ["Matchmaking does not work."] =
                "Matchmaking لا يعمل.",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 31 FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 31 FPS.",
            ["Maximum framerate with the patch is the Vblank frequency value."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي قيمة VBlank Frequency.",
            ["Might work with other versions with adjustments."] =
                "قد يعمل مع إصدارات أخرى بعد بعض التعديلات.",
            ["Native resolution is 1024x720."] =
                "الدقة الأصلية هي 1024x720.",
            ["Needs testing above 60 FPS."] =
                "يحتاج إلى اختبار فوق 60 FPS.",
            ["Only enable this if you know what you are doing!"] =
                "لا تفعل هذا إلا إذا كنت تعرف ما تفعله!",
            ["Original resolution is 1024x600. Might cause rendering bugs."] =
                "الدقة الأصلية 1024x600. قد تسبب مشكلات في العرض.",
            ["Original resolution is 896x600. Might cause rendering bugs."] =
                "الدقة الأصلية 896x600. قد تسبب مشكلات في العرض.",
            ["Patch may accelerate some game effects by 2x. Updated patch fixes subtitle cut-off in cutscenes."] =
                "قد يسرع الباتش بعض مؤثرات اللعبة بمقدار 2x. الإصدار المحدث من الباتش يصلح انقطاع الترجمة النصية في المشاهد السينمائية.",
            ["Patches the timestep values so the game can properly run at a higher FPS. The \"VBlank Frequency\" setting must be changed accordingly. Pitch Black Out patch recommended for anything higher than 120 FPS. WIP, rewinds will trigger early at higher FPS."] =
                "يعدل قيم Timestep حتى تعمل اللعبة بصورة صحيحة عند FPS أعلى. يجب تغيير إعداد \"VBlank Frequency\" بما يتناسب مع ذلك. ينصح بباتش Pitch Black Out عند تجاوز 120 FPS. العمل ما يزال قيد التطوير، وسيبدأ Rewind مبكرا عند FPS الأعلى.",
            ["Plays encounter BGM in file order."] =
                "يشغل BGM للمواجهات بحسب ترتيب الملفات.",
            ["Plays encounter BGM in random order."] =
                "يشغل BGM للمواجهات بترتيب عشوائي.",
            ["Prevents in combat HP/SP Bar UI from loading for testing purposes."] =
                "يمنع تحميل UI لشريط HP/SP أثناء القتال لأغراض الاختبار.",
            ["Prints file paths to the console's TTY Log as they are accessed."] =
                "يطبع مسارات الملفات في TTY Log الخاص بالكونسول عند الوصول إليها.",
            ["Removes 5 per level limit."] =
                "يزيل حد 5 لكل مستوى.",
            ["Removes 62 FPS limit.\nMaximum framerate with the patch is half the Vblank frequency."] =
                "يزيل حد 62 FPS.\nالحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency.",
            ["Removes 62 FPS limit. Maximum framerate with the patch is half the Vblank frequency."] =
                "يزيل حد 62 FPS. الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency.",
            ["Removes both Morphological Anti-Aliasing (MLAA) and Fast Approximate Anti-Aliasing (FXAA), post-process AA solutions that significantly impact image definition and sharpness. Particularly useful when using resolution upscaling."] =
                "يزيل كلا من Morphological Anti-Aliasing (MLAA) وFast Approximate Anti-Aliasing (FXAA)، وهما حلان من Post-process AA يؤثران بدرجة كبيرة في وضوح الصورة وحدتها. مفيد خصوصا عند رفع الدقة.",
            ["Removes SSL certificate validation."] =
                "يزيل التحقق من شهادة SSL.",
            ["Removes the 30FPS lock, cutscenes work properly and physics too, it's not possible to go above 60FPS other than changing the vblank, if done so cutscenes and physics break."] =
                "يزيل قفل 30 FPS، وتعمل المشاهد السينمائية والفيزياء بصورة صحيحة. لا يمكن تجاوز 60 FPS إلا بتغيير VBlank Frequency، وإذا فعلت ذلك ستتعطل المشاهد السينمائية والفيزياء.",
            ["Replaces the demo's \"London\" venue with the \"Pitch Black Out\" venue, which has a completely black background. Useful for getting more performance on lower-spec PCs."] =
                "يستبدل ساحة \"London\" في النسخة التجريبية بساحة \"Pitch Black Out\" ذات الخلفية السوداء بالكامل. مفيد للحصول على أداء أفضل على أجهزة PC ذات المواصفات المنخفضة.",
            ["Replaces the red color in the pause menu. Yellow by default."] =
                "يستبدل اللون الأحمر في Pause Menu. اللون الافتراضي هو الأصفر.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings.\n\nFor optimal splitscreen experience, it's recommended to switch the display orientation to Vertical in the game's settings."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU.\n\nلأفضل تجربة Splitscreen ينصح بتغيير اتجاه العرض إلى Vertical في إعدادات اللعبة.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. Restores original viewport size from PS2 releases. The remastered versions of the first two games handled a 16:9 aspect ratio by cropping out 25% of the vertical viewport instead of expanding the horizontal FOV."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. يعيد حجم Viewport الأصلي من إصدارات PS2. كانت النسخ المحسنة من أول لعبتين تتعامل مع نسبة 16:9 بقص 25% من Viewport الرأسي بدلا من توسيع FOV الأفقي.",
            ["Set Framelimit to 60 FPS as going above this will cause speed up.\nIn-game cinematic animations and camera are only interpolated at 30 FPS."] =
                "اضبط Framelimit على 60 FPS لأن تجاوزه يسبب تسارع اللعبة.\nرسوم المشاهد السينمائية داخل اللعبة وحركة الكاميرا لا يتم Interpolation لها إلا عند 30 FPS.",
            ["Set Framelimit to 60 FPS otherwise loading screen layers will go too fast."] =
                "اضبط Framelimit على 60 FPS، وإلا ستتحرك طبقات شاشة التحميل بسرعة كبيرة.",
            ["Set framerate to fixed 30, prevents audio issues. Use this if you experience slowdowns or stuttering in multiplayer."] =
                "اضبط معدل الإطارات على 30 ثابتة لمنع مشكلات الصوت. استخدم هذا إذا واجهت تباطؤا أو تقطعا في Multiplayer.",
            ["Set vblank to 120Hz for 60 FPS Gameplay.\nMay work with other versions with adjustments."] =
                "اضبط VBlank Frequency على 120 Hz للحصول على 60 FPS أثناء اللعب.\nقد يعمل مع إصدارات أخرى بعد بعض التعديلات.",
            ["Significantly improves performance in some cases, some distant places may look too clear due to removing the Layered fog effect. The patch is a good alternative to the Color Grading patch."] =
                "يحسن الأداء بدرجة كبيرة في بعض الحالات. قد تبدو بعض الأماكن البعيدة أوضح من اللازم بسبب إزالة تأثير Layered Fog. يعد الباتش بديلا جيدا لباتش Color Grading.",
            ["Significantly improves performance, changes visuals for Army of Sparta special attack and some lighting changes, generally only on magic sources."] =
                "يحسن الأداء بدرجة كبيرة، مع تغيير المرئيات لهجوم Army of Sparta الخاص وبعض تغييرات الإضاءة، وغالبا يكون التأثير على مصادر السحر فقط.",
            ["Skips SCE intros up to the first video.\n2.0 now skips first two intro videos."] =
                "يتخطى مقدمات SCE حتى أول فيديو.\nالإصدار 2.0 يتخطى الآن أول فيديوهين من المقدمة.",
            ["This patch fixes an issue where volume settings were not correctly applied when loading save data before a PvP match."] =
                "يصلح هذا الباتش مشكلة لم تكن تطبق فيها إعدادات مستوى الصوت بصورة صحيحة عند تحميل بيانات الحفظ قبل مباراة PvP.",
            ["Unlimited ammo, grenades, etc. without reloading."] =
                "ذخيرة وقنابل وغيرها بلا حدود ومن دون إعادة تلقيم.",
            ["Unlocks all content without disabling high scores, progression or trophies."] =
                "يفتح كل المحتوى من دون تعطيل High Scores أو التقدم أو Trophies.",
            ["Unlocks everything. No save data required."] =
                "يفتح كل شيء. لا يتطلب بيانات حفظ.",
            ["Unlocks some devkit menu without DECR:\n Rendering\n Collision (needs working msgcon)\n Interactive Background\n Physics\n Particles"] =
                "يفتح بعض قوائم Devkit من دون DECR:\n Rendering\n Collision (يتطلب msgcon يعمل)\n Interactive Background\n Physics\n Particles",
            ["Updated with a new version that has correct game speed at any framerate. You no longer need to set the Framelimiter to 30 or 60."] =
                "تم تحديثه بإصدار جديد يحافظ على سرعة اللعبة الصحيحة عند أي معدل إطارات. لم تعد بحاجة إلى ضبط Framelimiter على 30 أو 60.",
            ["Using weapons and armor does not affect their condition. Repairing items brings them to perfect condition."] =
                "استخدام الأسلحة والدروع لا يؤثر في حالتها. إصلاح العناصر يعيدها إلى الحالة المثالية.",
            ["Version 2.0 no longer requires 200% clock scale, reset to default before using this updated patch. Causes issues with certain cutscene animations playing too fast/some dialogue lines may not play when enabled."] =
                "الإصدار 2.0 لم يعد يتطلب Clock Scale بنسبة 200%؛ أعده إلى القيمة الافتراضية قبل استخدام هذا الباتش المحدث. يسبب مشكلات تجعل بعض رسوم المشاهد السينمائية تعمل بسرعة كبيرة، وقد لا تعمل بعض سطور الحوار عند تفعيله.",
            ["Videos won't play above 60 FPS."] =
                "لن تعمل الفيديوهات فوق 60 FPS.",
            ["~20% performance improvement but makes the game look worse."] =
                "تحسن في الأداء بنحو 20% لكنه يجعل مظهر اللعبة أسوأ.",
            ["\"Screen Size\" must be set to Full in the game settings. UI elements will be stretched.\nPillar/letterboxed or stretched 4:3 is the normal aspect ratio without the patch."] =
                "يجب ضبط \"Screen Size\" على Full في إعدادات اللعبة. سيتم تمديد عناصر UI.\nمن دون الباتش تكون نسبة العرض الطبيعية 4:3 مع Pillarbox/Letterbox أو تمدد.",
            ["<b>Picking up some objects such as blueprints will freeze the game. Best used temporarily to duplicate the items you want.</b>\n\nAffects many items in the game, not just ammo and consumables. While this patch is enabled, affected items can't be removed from your inventory, and dropping them will duplicate them. Items can be sold for caps, but they do not leave your inventory."] =
                "التقاط بعض العناصر مثل Blueprints سيجمد اللعبة. يفضل استخدامه مؤقتا فقط لتكرار العناصر التي تريدها.\n\nيؤثر في عناصر كثيرة داخل اللعبة، وليس الذخيرة والمواد الاستهلاكية فقط. أثناء تفعيل الباتش لا يمكن إزالة العناصر المتأثرة من المخزون، كما أن إسقاطها سيكررها. يمكن بيع العناصر مقابل Caps لكنها لا تغادر المخزون.",
            ["Adjusts physics according to the fps. If the fps is lower than the original framerate target, the game slows down. By default this original framerate target is 30 fps, enable the 60 fps patch to increase the framerate target to 60 (only do this if you can keep above 60 fps steadily, otherwise there is no point to combine the 60 fps patch with this one). Increase the vblank rate to increase framerate cap accordingly, keeping the vblank rate at 60hz will render this patch useless. This patch does not limit the fps in menus, which can make them hard to navigate. Known issues are replay instability and slight changes to the engine simulation. This patch was developed thanks to a bounty set by Comanso, who also helped with testing."] =
                "يضبط الفيزياء وفقا لـFPS. إذا كان FPS أقل من معدل الإطارات الأصلي المستهدف فستتباطأ اللعبة. الهدف الأصلي افتراضيا هو 30 FPS؛ فعل باتش 60 FPS لرفع الهدف إلى 60، ولا تفعل ذلك إلا إذا استطعت الحفاظ على أكثر من 60 FPS بثبات، وإلا فلا فائدة من جمع باتش 60 FPS مع هذا الباتش. ارفع Vblank Rate لرفع حد معدل الإطارات بما يناسبه؛ إبقاء Vblank Rate على 60 Hz يجعل هذا الباتش بلا فائدة. لا يحد الباتش FPS في القوائم، ما قد يجعل التنقل فيها صعبا. من المشكلات المعروفة عدم استقرار Replay وتغييرات طفيفة في محاكاة المحرك. تم تطوير الباتش بفضل مكافأة قدمها Comanso، الذي ساعد أيضا في الاختبار.",
            ["Allows for the use of resolution scaling.\nSome effects like fire, smoke, muzzle flashes and possibly more will be missing due to the particle rendering system being tied into MLAA."] =
                "يتيح استخدام Resolution Scale.\nستختفي بعض المؤثرات مثل النار والدخان وMuzzle Flashes وربما غيرها لأن نظام رسم الجسيمات مرتبط بـMLAA.",
            ["Allows jumping while aiming"] =
                "يتيح القفز أثناء التصويب.",
            ["Allows to place any item in offhand."] =
                "يتيح وضع أي عنصر في Offhand.",
            ["Allows to skip intro movies"] =
                "يتيح تخطي فيديوهات المقدمة.",
            ["Allows up to 1000 FPS without affecting game speed.\nChange vblank to go above 60 FPS."] =
                "يتيح ما يصل إلى 1000 FPS من دون التأثير في سرعة اللعبة.\nغير VBlank Frequency لتجاوز 60 FPS.",
            ["Avoid recompiling unused PPU modules."] =
                "يتجنب إعادة ترجمة وحدات PPU غير المستخدمة.",
            ["Being airborne for too long will cause you to respawn."] =
                "البقاء في الهواء مدة طويلة جدا سيؤدي إلى إعادة إحيائك.",
            ["Best used with the Infinite Carry Weight Patch; affects many items in the game, not just ammo and consumables. While this patch is enabled, affected items can't be removed from your inventory, and dropping them will duplicate them. Items can be sold for caps, but they do not leave your inventory."] =
                "يفضل استخدامه مع باتش Infinite Carry Weight. يؤثر في عناصر كثيرة داخل اللعبة، وليس الذخيرة والمواد الاستهلاكية فقط. أثناء تفعيل الباتش لا يمكن إزالة العناصر المتأثرة من المخزون، كما أن إسقاطها سيكررها. يمكن بيع العناصر مقابل Caps لكنها لا تغادر المخزون.",
            ["Bigger world size (it's only 80 blocks difference but setting it to anything beyond 64 chunks makes Minecraft unplayable)"] =
                "يزيد حجم العالم. الفارق 80 Block فقط، لكن ضبطه على أكثر من 64 Chunk يجعل Minecraft غير قابلة للعب.",
            ["Campaign only. Doesn't work on DLC or online."] =
                "يعمل في Campaign فقط. لا يعمل مع DLC أو Online.",
            ["Causes a softlock during a fairly easy boss battle about 1/3 of the way through the game. Enemy health will show as 9999/xxxx in battle if you have the Sensor ability."] =
                "يسبب Softlock أثناء مواجهة Boss سهلة نسبيا بعد نحو 1/3 من اللعبة. إذا كانت لديك قدرة Sensor فستظهر صحة العدو في المعركة بالشكل 9999/xxxx.",
            ["Changes light colors in an unpredictable manner."] =
                "يغير ألوان الإضاءة بطريقة غير متوقعة.",
            ["Completely unlocks FPS and doesn't require any special settings. Use Framelimit or VSync option to cap the maximum framerate."] =
                "يفتح FPS بالكامل ولا يتطلب أي إعدادات خاصة. استخدم Framelimit أو VSync لتحديد الحد الأقصى لمعدل الإطارات.",
            ["Connect to MordorWide (https://mordorwi.de) custom servers, allowing cross-play with PC players"] =
                "يتصل بخوادم MordorWide المخصصة (https://mordorwi.de)، ما يتيح Cross-play مع لاعبي PC.",
            ["Cutscenes in-engine still play at 30FPS."] =
                "المشاهد السينمائية التي يعمل عليها المحرك نفسه ما تزال تعمل عند 30 FPS.",
            ["Disable overall blur effect, small speed improvement."] =
                "يعطل تأثير Blur العام مع تحسن بسيط في الأداء.",
            ["Disable SSL certificate validation for custom servers"] =
                "يعطل التحقق من شهادة SSL للخوادم المخصصة.",
            ["Disables blur effects (Motion blur, DOF and such)"] =
                "يعطل مؤثرات Blur مثل Motion Blur وDOF وغيرها.",
            ["Disables Motion Blur. Doesn't affect performance.\nPlease report any issues."] =
                "يعطل Motion Blur ولا يؤثر في الأداء.\nيرجى الإبلاغ عن أي مشكلات.",
            ["Disables Motion Blur. Shouldn't affect anything else."] =
                "يعطل Motion Blur، ولا يفترض أن يؤثر في أي شيء آخر.",
            ["Disables SHA-1 hash checking of the following files:; coalesced_int.bin, coalesced_chn.bin, coalesced_cze.bin, coalesced_deu.bin, coalesced_esm.bin, coalesced_esn.bin, coalesced_fra.bin, coalesced_hun.bin, coalesced_ita.bin, coalesced_jpn.bin, coalesced_kor.bin, coalesced_pol.bin, coalesced_ptb.bin, coalesced_rus.bin, coalesced_slo.bin, core.xxx, engine.xxx, gfxui.xxx, ipdrv.xxx, gameframework.xxx, onlinesubsystempc.xxx, cdevsounddriver.xxx, sqexsead.xxx, sqex03game.xxx, startup.xxx"] =
                "يعطل التحقق من SHA-1 للملفات التالية:; coalesced_int.bin, coalesced_chn.bin, coalesced_cze.bin, coalesced_deu.bin, coalesced_esm.bin, coalesced_esn.bin, coalesced_fra.bin, coalesced_hun.bin, coalesced_ita.bin, coalesced_jpn.bin, coalesced_kor.bin, coalesced_pol.bin, coalesced_ptb.bin, coalesced_rus.bin, coalesced_slo.bin, core.xxx, engine.xxx, gfxui.xxx, ipdrv.xxx, gameframework.xxx, onlinesubsystempc.xxx, cdevsounddriver.xxx, sqexsead.xxx, sqex03game.xxx, startup.xxx",
            ["Disables SHA-1 hash checking of the following files:; coalesced_int.bin, coalesced_jpn.bin, core.xxx, engine.xxx, gfxui.xxx, ipdrv.xxx, gameframework.xxx, onlinesubsystempc.xxx, cdevsounddriver_loc_int.xxx, cdevsounddriver.xxx, sqexsead.xxx, sqex03game.xxx, startup.xxx, cdevsounddriver_loc_jpn.xxx"] =
                "يعطل التحقق من SHA-1 للملفات التالية:; coalesced_int.bin, coalesced_jpn.bin, core.xxx, engine.xxx, gfxui.xxx, ipdrv.xxx, gameframework.xxx, onlinesubsystempc.xxx, cdevsounddriver_loc_int.xxx, cdevsounddriver.xxx, sqexsead.xxx, sqex03game.xxx, startup.xxx, cdevsounddriver_loc_jpn.xxx",
            ["Disables what the port refers to as Bloom, which only serves to blur the entire screen."] =
                "يعطل ما يسميه هذا المنفذ Bloom، وهو تأثير لا يفعل سوى طمس الشاشة بالكامل.",
            ["Enables create mode permanently in all levels until deactivated."] =
                "يفعل Create Mode بصورة دائمة في جميع المراحل حتى تعطله.",
            ["Enables debug mode. Enable Mouse and Keyboard handlers under Config > I/O to use basic debug menus. Right click for the topmost menu."] =
                "يفعل Debug Mode. فعل معالجي Mouse وKeyboard ضمن Config > I/O لاستخدام قوائم Debug الأساسية. انقر بالزر الأيمن لفتح القائمة العليا.",
            ["Enables Items advanced Tooltips"] =
                "يفعل Tooltips المتقدمة للعناصر.",
            ["Enables verbose logging."] =
                "يفعل تسجيل Logs مفصل.",
            ["File replacement via Ogre1_Mod.cpk file."] =
                "يتيح استبدال الملفات عبر Ogre1_Mod.cpk.",
            ["File replacement via Ogre2_Mod.cpk file."] =
                "يتيح استبدال الملفات عبر Ogre2_Mod.cpk.",
            ["Fixes a softlock at the end of the university chapter."] =
                "يصلح Softlock في نهاية فصل University.",
            ["Fixes an engine bug that saves an unused detecting actors list on all ActorCauses"] =
                "يصلح خطأ في المحرك يسبب حفظ قائمة Detecting Actors غير مستخدمة على جميع ActorCauses.",
            ["Fixes crash after entering MP menu due to offline video streaming API."] =
                "يصلح الانهيار بعد الدخول إلى قائمة MP بسبب API بث الفيديو Offline.",
            ["Fixes significant slowdown because the video is no longer hosted on the server."] =
                "يصلح تباطؤا كبيرا لأن الفيديو لم يعد مستضافا على الخادم.",
            ["Fixes soft-lock when going into Online."] =
                "يصلح Softlock عند الدخول إلى Online.",
            ["Global FOV setting."] =
                "إعداد FOV عام.",
            ["Goes slightly above 60 FPS."] =
                "يتجاوز 60 FPS بقليل.",
            ["Increases the internal resolution from 1024x640 to 1280x720."] =
                "يزيد الدقة الداخلية من 1024x640 إلى 1280x720.",
            ["Issues are expected!\nEnable Debug Menu patch and while in-game, open Quick Menu, and select Player 2.\nKnown Issues:\nPlayer 2 doesn't have a playermodel.\nPlayer 2 cannot move in some levels."] =
                "المشكلات متوقعة!\nفعل باتش Debug Menu، ثم أثناء اللعب افتح Quick Menu واختر Player 2.\nمشكلات معروفة:\nPlayer 2 لا يملك Playermodel.\nPlayer 2 لا يستطيع الحركة في بعض المراحل.",
            ["L1 + R1 = Brings menu during missions"] =
                "L1 + R1 = إظهار القائمة أثناء المهام",
            ["L3 = Opens Debug Menu\nL1/R1 = Change pages\nOnly tested on BCUS98298."] =
                "L3 = فتح Debug Menu\nL1/R1 = تغيير الصفحات\nتم اختباره فقط على BCUS98298.",
            ["Locks FPS during gameplay to as close to 50 as clock syncing allows, parts of the game that run natively at 60fps stay at 60fps. Cloth physics break above 50fps during gameplay but locking fps to 50 breaks parts that natively run at 60 eg. cutscenes. This patch eliminates the need to constantly swap between 50 and 60fps limits in RPCS3 settings when using the fps unlock patch, while boosting the performance significantly. FPS unlock patch must remain turned off for this patch to work."] =
                "يقفل FPS أثناء اللعب عند قيمة قريبة من 50 بقدر ما يسمح به Clock Sync، بينما تبقى أجزاء اللعبة التي تعمل أصلا عند 60 FPS على 60 FPS. تتعطل فيزياء الملابس فوق 50 FPS أثناء اللعب، لكن قفل FPS على 50 يعطل الأجزاء التي تعمل أصلا عند 60 FPS مثل المشاهد السينمائية. يلغي هذا الباتش الحاجة إلى التبديل المستمر بين حدود 50 و60 FPS في إعدادات RPCS3 عند استخدام باتش فتح FPS، مع تحسين الأداء بدرجة كبيرة. يجب أن يبقى باتش فتح FPS معطلا حتى يعمل هذا الباتش.",
            ["Maximum framerate with the patch is half the Vblank frequency. Physics issues at high FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. تظهر مشكلات في الفيزياء عند FPS المرتفع.",
            ["Maximum framerate with the patch is half the Vblank frequency. Without the patch, adjusting Vblank frequency increases the maximum framerate up to about 62 FPS. Some actions are impossible to do at high FPS."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي نصف VBlank Frequency. من دون الباتش، تؤدي زيادة VBlank Frequency إلى رفع الحد الأقصى لمعدل الإطارات إلى نحو 62 FPS. بعض الإجراءات تصبح مستحيلة عند FPS المرتفع.",
            ["Maximum framerate with the patch is the Vblank frequency."] =
                "الحد الأقصى لمعدل الإطارات مع الباتش يساوي VBlank Frequency.",
            ["Necessary for the game to connect to unofficial signon servers."] =
                "ضروري حتى تتصل اللعبة بخوادم Signon غير الرسمية.",
            ["Needs testing on other versions."] =
                "يحتاج إلى اختبار على إصدارات أخرى.",
            ["NPCs will not notice you stealing even if they are watching you and you will not lose karma."] =
                "لن يلاحظ NPCs أنك تسرق حتى لو كانوا يراقبونك، ولن تخسر Karma.",
            ["Only enable this patch if you know what you are doing!!"] =
                "لا تفعل هذا الباتش إلا إذا كنت تعرف ما تفعله!!",
            ["Only works while you're on your board. Jumping from the most extreme heights may still trigger bails because your character folds up and hits their limbs on the ground, so the game thinks you skated into something at high speed."] =
                "يعمل فقط أثناء وجودك على لوح التزلج. القفز من ارتفاعات شديدة قد يظل يؤدي إلى Bail لأن الشخصية تنطوي وتصطدم أطرافها بالأرض، فتعتبر اللعبة أنك اصطدمت بشيء بسرعة عالية.",
            ["Original resolution is 1120x630."] =
                "الدقة الأصلية هي 1120x630.",
            ["Original resolution is 1152x640."] =
                "الدقة الأصلية هي 1152x640.",
            ["Original resolution is 1152x704."] =
                "الدقة الأصلية هي 1152x704.",
            ["Original resolution is 1280x768."] =
                "الدقة الأصلية هي 1280x768.",
            ["Patch to increase the number of players to 16. Not tested with more than 4 players!"] =
                "باتش لزيادة عدد اللاعبين إلى 16. لم يختبر مع أكثر من 4 لاعبين!",
            ["Patched default is 0.6."] =
                "القيمة الافتراضية بعد الباتش هي 0.6.",
            ["Patches out SVO, meaning you no longer need to quit the game after every online race on PSORG (https://discord.gg/GdX4V44CJd), nor must you delete the SVO chunk from your save file/delete your save data."] =
                "يعطل SVO، ولذلك لن تحتاج بعد الآن إلى الخروج من اللعبة بعد كل سباق Online على PSORG (https://discord.gg/GdX4V44CJd)، ولن تضطر إلى حذف SVO Chunk من ملف الحفظ أو حذف بيانات الحفظ.",
            ["Permanently be in create mode until unchecked.\nFlying in the pod will cause sackboy to go far away from the pod. Teleport yourself into the pod via a player teleporter, then stop flying."] =
                "يبقيك في Create Mode بصورة دائمة حتى تلغي التحديد.\nالطيران داخل Pod سيجعل Sackboy يبتعد كثيرا عن Pod. انقل نفسك إلى داخل Pod باستخدام Player Teleporter ثم توقف عن الطيران.",
            ["Pick up any amount of parts or items to activate the cheat.\nReplaces 'SS Printout to Msgcon' in Debug Menu Gameplay tab"] =
                "التقط أي كمية من Parts أو Items لتفعيل Cheat.\nيستبدل 'SS Printout to Msgcon' في تبويب Debug Menu Gameplay.",
            ["Press R1+START in-game to access the Debug Menu (god mode, no clip, etc.)"] =
                "اضغط R1+START داخل اللعبة للوصول إلى Debug Menu مثل God Mode وNo Clip وغيرها.",
            ["Press R1+START in-game to access the Debug Menu."] =
                "اضغط R1+START داخل اللعبة للوصول إلى Debug Menu.",
            ["Press square to instantly reload single-shot weapons like the Missile Launcher and Fat Man."] =
                "اضغط Square لإعادة تلقيم الأسلحة أحادية الطلقة فورا، مثل Missile Launcher وFat Man.",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nAce Combat: Assault Horizon\nBeyond Good & Evil HD\nDmC Devil May Cry\nMotorStorm: Apocalypse\nMotorStorm RC\nPlayStation Move Heroes\nCarnival Island\nHelldivers\nEscape Dead Island\nI Am Alive\nRed Faction: Armageddon\nFarming Simulator\nSly Cooper: Thieves in Time\n\nExtra note for Sly Cooper: Thieves in Time:\nThis patch can be used alongside PPU based patch which allows for resolution scaling."] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nAce Combat: Assault Horizon\nBeyond Good & Evil HD\nDmC Devil May Cry\nMotorStorm: Apocalypse\nMotorStorm RC\nPlayStation Move Heroes\nCarnival Island\nHelldivers\nEscape Dead Island\nI Am Alive\nRed Faction: Armageddon\nFarming Simulator\nSly Cooper: Thieves in Time\n\nملاحظة إضافية لـSly Cooper: Thieves in Time:\nيمكن استخدام هذا الباتش مع باتش مبني على PPU يتيح Resolution Scale.",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nBinary Domain\nJourney\nOddworld: Stranger's Wrath HD\nTokyo Jungle\nSoulCalibur Lost Swords\nSoulCalibur V\nYakuza Ishin\nYakuza 5\nFerrari Racing Legends\nPool Nation\nPortal 2\nThe Ratchet & Clank Trilogy"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nBinary Domain\nJourney\nOddworld: Stranger's Wrath HD\nTokyo Jungle\nSoulCalibur Lost Swords\nSoulCalibur V\nYakuza Ishin\nYakuza 5\nFerrari Racing Legends\nPool Nation\nPortal 2\nThe Ratchet & Clank Trilogy",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nCaptain America: Super Soldier\nYakuza Dead Souls\nNeed for Speed Shift 2 Unleashed\nTransformers Dark of The Moon"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nCaptain America: Super Soldier\nYakuza Dead Souls\nNeed for Speed Shift 2 Unleashed\nTransformers Dark of The Moon",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nICO\nThis patch can be used alongside PPU based patch which allows for resolution scaling."] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nICO\nيمكن استخدام هذا الباتش مع باتش مبني على PPU يتيح Resolution Scale.",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nLittleBigPlanet 2\nLittleBigPlanet 3\nLittleBigPlanet Hub"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nLittleBigPlanet 2\nLittleBigPlanet 3\nLittleBigPlanet Hub",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nLittleBigPlanet 2 (early versions)"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nLittleBigPlanet 2 (الإصدارات المبكرة)",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nLittleBigPlanet 2 (other early versions)"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nLittleBigPlanet 2 (إصدارات مبكرة أخرى)",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nMacross 30"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nMacross 30",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nMedieval Moves: Deadmund's Quest\nShadow of the Colossus\nSports Champions 2\n\nExtra note for Shadow of the Colossus:\nThis patch can be used alongside PPU based patch which allows for resolution scaling."] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nMedieval Moves: Deadmund's Quest\nShadow of the Colossus\nSports Champions 2\n\nملاحظة إضافية لـShadow of the Colossus:\nيمكن استخدام هذا الباتش مع باتش مبني على PPU يتيح Resolution Scale.",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nMetro Last Light"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nMetro Last Light",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nPrince of Persia: The Sands of Time\nPrince of Persia: Warrior Within\nPrince of Persia: The Two Thrones"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nPrince of Persia: The Sands of Time\nPrince of Persia: Warrior Within\nPrince of Persia: The Two Thrones",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nTwisted Metal\nResident Evil: Operation Raccoon City"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nTwisted Metal\nResident Evil: Operation Raccoon City",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nWWE '13\nWWE 2K14"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nWWE '13\nWWE 2K14",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; Known to affect:\nYakuza 0\nYakuza Kiwami\nHandball 16\nHandball 17\nKillAllZombies\nWWE 2k17"] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; معروف بتأثيره في:\nYakuza 0\nYakuza Kiwami\nHandball 16\nHandball 17\nKillAllZombies\nWWE 2k17",
            ["Removes Morphological Anti-Aliasing, known as \"MLAA\", a form of post process anti-aliasing which is done using the CELL's SPUs instead of the RSX for extra performance.; This patch can be used alongside PPU based patch which allows for resolution scaling."] =
                "يزيل Morphological Anti-Aliasing المعروف باسم \"MLAA\"، وهو نوع من Post-process Anti-Aliasing ينفذ باستخدام وحدات SPU في CELL بدلا من RSX لتحسين الأداء.; يمكن استخدام هذا الباتش مع باتش مبني على PPU يتيح Resolution Scale.",
            ["Removes shadows and fog."] =
                "يزيل الظلال والضباب.",
            ["Removes the 60 FPS cap.\nSet the framelimit to Off in the custom configuration for this to work properly."] =
                "يزيل حد 60 FPS.\nاضبط Frame limit على Off في Custom Configuration حتى يعمل بصورة صحيحة.",
            ["Removes the 60 FPS cap.\nSpecial thanks to Whatcookie for showing how to find the sleeping instruction."] =
                "يزيل حد 60 FPS.\nشكر خاص لـWhatcookie على توضيح طريقة العثور على تعليمة Sleep.",
            ["Removes the borders and makes the game take up the entire screen without stretching. Transitions are still 4:3. Breaks some startup screens."] =
                "يزيل الحدود ويجعل اللعبة تملأ الشاشة بالكامل من دون تمدد. الانتقالات ما تزال بنسبة 4:3. يعطل بعض شاشات بدء التشغيل.",
            ["Removes the heavy snow effect on the Shadow Moses stage."] =
                "يزيل تأثير الثلج الكثيف في مرحلة Shadow Moses.",
            ["Renders all objects, even ones that are far away."] =
                "يرسم جميع العناصر حتى البعيدة منها.",
            ["Replaces audio options with various debug features."] =
                "يستبدل خيارات الصوت بميزات Debug متنوعة.",
            ["Replaces some useless options in Memory Tab with rendering features.\nWireframe modes currently do not work on the emulator."] =
                "يستبدل بعض الخيارات غير المفيدة في Memory Tab بميزات Rendering.\nأوضاع Wireframe لا تعمل حاليا على المحاكي.",
            ["Requires \"Stretch To Display Area\" checked in the GPU settings. HUD elements will be stretched and misplaced."] =
                "يتطلب تفعيل \"Stretch To Display Area\" في إعدادات GPU. سيتم تمديد عناصر HUD ووضعها في أماكن غير صحيحة.",
            ["Set Clock scale to 200% for 60 FPS.\nIGC might still be double speed."] =
                "اضبط Clock Scale على 200% للحصول على 60 FPS.\nقد تظل IGC بسرعة مضاعفة.",
            ["Set FPS Limit to 60. Some stuff is still 2x speed."] =
                "اضبط FPS Limit على 60. بعض العناصر ما تزال بسرعة 2x.",
            ["Set Frame Limit to 60"] =
                "اضبط Frame limit على 60.",
            ["Set Framelimit to auto"] =
                "اضبط Framelimit على Auto.",
            ["Set Framerate limit to auto"] =
                "اضبط Framerate Limit على Auto.",
            ["Set VBlank to 120"] =
                "اضبط VBlank Frequency على 120.",
            ["Skip SSL certificate validation"] =
                "يتخطى التحقق من شهادة SSL.",
            ["Skips intro videos, goes to main menu immediately after controller notice"] =
                "يتخطى فيديوهات المقدمة وينتقل إلى القائمة الرئيسية مباشرة بعد إشعار وحدة التحكم.",
            ["Skips SCE intros up to first video\n2.0 Skips intro video."] =
                "يتخطى مقدمات SCE حتى أول فيديو.\nالإصدار 2.0 يتخطى فيديو المقدمة.",
            ["Skips the \"WARNING\" & Sanzaru logo intro/splash images."] =
                "يتخطى صور المقدمة والشعار \"WARNING\" وSanzaru.",
            ["Skips the \"Warning\" & Sanzaru logo intro/splash images."] =
                "يتخطى صور المقدمة والشعار \"Warning\" وSanzaru.",
            ["Slight overbloom in some areas. Needs correction."] =
                "يوجد Bloom زائد قليلا في بعض المناطق ويحتاج إلى تصحيح.",
            ["Stops the ingame camera from swaying and shaking."] =
                "يوقف تمايل واهتزاز الكاميرا داخل اللعبة.",
            ["The game is locked to 60 FPS by default. This patch completely removes the framelimiter, and doesn't need any special settings."] =
                "اللعبة مقفلة افتراضيا على 60 FPS. يزيل هذا الباتش Framelimiter بالكامل ولا يحتاج إلى أي إعدادات خاصة.",
            ["This only works for 3 casinos on the Strip, the Vikki and Vance and Atomic Wrangler use a script to detect the ban."] =
                "يعمل هذا فقط في 3 Casinos على Strip؛ أما Vikki and Vance وAtomic Wrangler فيستخدمان Script لاكتشاف الحظر.",
            ["This patch eliminates the post-processing render pass, which fixes the green screen artifacts caused by upscaling. Additionally, 16x Anisotropic Filtering can now be forced via custom config without causing issues."] =
                "يلغي هذا الباتش مرحلة Post-processing Render Pass، ما يصلح تشوهات الشاشة الخضراء الناتجة عن رفع الدقة. بالإضافة إلى ذلك يمكن الآن فرض 16x Anisotropic Filtering عبر Custom Config من دون التسبب في مشكلات.",
            ["This patch fixes issues when running the game at 60 FPS but you still need to set Vblank frequency to 120Hz in order to play at 60 FPS. You may face issues such as halved input windows, few UI elements not at normal speed, intro quotes stop half-way, projectiles speed is doubled and few graphical glitches (Jotaro not appearing during DIO's GHA)."] =
                "يصلح هذا الباتش مشكلات تشغيل اللعبة عند 60 FPS، لكن يجب مع ذلك ضبط VBlank Frequency على 120 Hz للعب عند 60 FPS. قد تواجه مشكلات مثل تقليص نوافذ الإدخال إلى النصف، وبعض عناصر UI التي لا تعمل بالسرعة الطبيعية، وتوقف عبارات المقدمة في منتصفها، وتضاعف سرعة المقذوفات، وبعض العيوب الرسومية مثل عدم ظهور Jotaro أثناء GHA الخاص بـDIO.",
            ["This patch fixes issues when running the game at 60 FPS but you still need to set Vblank frequency to 120Hz in order to play at 60 FPS. You may face issues such as intro quotes stopping half-way and doubled projectiles speed."] =
                "يصلح هذا الباتش مشكلات تشغيل اللعبة عند 60 FPS، لكن يجب مع ذلك ضبط VBlank Frequency على 120 Hz للعب عند 60 FPS. قد تواجه مشكلات مثل توقف عبارات المقدمة في منتصفها وتضاعف سرعة المقذوفات.",
            ["This patch resolves the PvP unplayability issue by disabling the update check."] =
                "يحل هذا الباتش مشكلة عدم إمكانية لعب PvP عبر تعطيل التحقق من التحديث.",
            ["This will cause the slot machine to always pay out as if you hit a 777, although it will never actually show the 777 on the machine."] =
                "يجعل Slot Machine تدفع دائما كما لو حصلت على 777، رغم أن 777 لن يظهر فعليا على الآلة.",
            ["Tweaks the chase camera to be a little less static"] =
                "يعدل Chase Camera لتصبح أقل ثباتا بقليل.",
            ["Unlocks everything. No save data required. Module achievement percentage is not changed."] =
                "يفتح كل شيء. لا يتطلب بيانات حفظ. لا تتغير نسبة Module Achievement.",
            ["Unlocks FPS without additional settings.\nAs a safety measure, set framelimit to 60."] =
                "يفتح FPS من دون إعدادات إضافية.\nكإجراء أمان، اضبط Framelimit على 60.",
            ["Unlocks internal timestep calculation. Increase vblank to match your desired framerate."] =
                "يفتح حساب Timestep الداخلي. ارفع VBlank Frequency بما يناسب معدل الإطارات المطلوب.",
            ["Unlocks Map and Episode Browser in Pause Menu.\nNot sure if it does anything else."] =
                "يفتح Map وEpisode Browser في Pause Menu.\nغير مؤكد ما إذا كان يفعل شيئا آخر.",
            ["Updates references from gamespy.com to openspy.net. Allows to play multiplayer. UNTESTED"] =
                "يحدث المراجع من gamespy.com إلى openspy.net، ما يتيح اللعب الجماعي. لم يتم اختباره.",
            ["Vblank frequency must be 120Hz for 60 FPS or 240Hz for 120 FPS.; Going over 120 FPS is possible, but physics will break a bit (lowering jump height) and make it impossible to lock-on to enemies when flying on the dragon.; Required for old delisted versions of this gameID. There was a silent update to fix DLC issues. Both versions are listed as \"01.00\" so the patch can't be separated."] =
                "يجب أن تكون VBlank Frequency على 120 Hz للحصول على 60 FPS أو 240 Hz للحصول على 120 FPS؛ يمكن تجاوز 120 FPS، لكن الفيزياء ستتأثر قليلا بخفض ارتفاع القفز، وسيصبح تثبيت الهدف على الأعداء أثناء الطيران بالتنين مستحيلا؛ الباتش مطلوب للإصدارات القديمة المحذوفة من هذا GameID. حدث تحديث صامت لإصلاح مشكلات DLC. كلا الإصدارين مسجلان باسم \"01.00\"، لذلك لا يمكن فصل الباتش بينهما.",
            ["Videos play at double speed and in-game timers clock twice as fast."] =
                "تعمل الفيديوهات بسرعة مضاعفة، كما تعمل مؤقتات اللعبة بسرعة مضاعفة.",
            ["Warning: Log Spam!"] =
                "تحذير: Log Spam!",
            ["You must set the Vblank Frequency to 120Hz for this patch to work. Also note that the game's actual speed may slow down below 30 FPS."] =
                "يجب ضبط VBlank Frequency على 120 Hz حتى يعمل هذا الباتش. لاحظ أيضا أن سرعة اللعبة الفعلية قد تنخفض تحت 30 FPS.",
            ["You must set the Vblank Frequency to 240Hz for this patch to work. Also note that the game's actual speed may slow down below 60 FPS."] =
                "يجب ضبط VBlank Frequency على 240 Hz حتى يعمل هذا الباتش. لاحظ أيضا أن سرعة اللعبة الفعلية قد تنخفض تحت 60 FPS.",
            ["You will never lose health when attacked."] =
                "لن تفقد الصحة عند التعرض للهجوم.",
            ["~10% performance improvement but makes the game look worse."] =
                "تحسن في الأداء بنحو 10% لكنه يجعل مظهر اللعبة أسوأ.",
            ["Enables the ability to use the unreleased Collector's Edition content (adds new cars, tracks and challenges). New cars can only be used on a new save file."] =
                "يتيح استخدام محتوى Collector's Edition غير الصادر (يضيف سيارات وحلبات وتحديات جديدة). لا يمكن استخدام السيارات الجديدة إلا مع ملف حفظ جديد.",
            ["Unlocks Porsche 911 Turbo, Dodge Challenger R/T, Toyota Corolla GT-S, Infiniti G35, Ford Shelby GT500, Pagani Zonda F (officially only released for PC and X360 as DLC). 911 Turbo, Challenger and G35 are usable in Career mode, the other three are only usable in Quick Race mode."] =
                "يفتح Porsche 911 Turbo وDodge Challenger R/T وToyota Corolla GT-S وInfiniti G35 وFord Shelby GT500 وPagani Zonda F (صدرت رسميا فقط على PC وX360 كمحتوى DLC). يمكن استخدام 911 Turbo وChallenger وG35 في وضع Career، أما السيارات الثلاث الأخرى فلا يمكن استخدامها إلا في وضع Quick Race.",
            ["Framelimit OFF is required to go above 60 FPS, beware this can cause buggy character movement and will speed up cutscenes.\nThe game may crash above 500 FPS in loading screens."] =
                "يتطلب تجاوز 60 FPS ضبط Framelimit على OFF؛ انتبه إلى أن ذلك قد يسبب حركة غير سليمة للشخصيات ويسرع المشاهد السينمائية.\nقد تتعطل اللعبة فوق 500 FPS في شاشات التحميل.",
            ["Restores exact PS3 behavior: forces the PS3 NTSC frametime (1/59.94) and caps logic at 59.94 steps/s, fixing RPCS3 overspeed while keeping frame-precise physics intact. Requires 'Vblank NTSC Fixup' enabled."] =
                "يعيد سلوك PS3 الدقيق: يفرض زمن إطار PS3 NTSC ‏(1/59.94) ويحد منطق اللعبة عند 59.94 steps/s، ما يصلح التسارع الزائد في RPCS3 مع الحفاظ على الفيزياء الدقيقة على مستوى الإطار. يتطلب تفعيل 'Vblank NTSC Fixup'.",
            ["Maximum framerate with this patch is the Vblank frequency, so 60 FPS at the default setting. The campaign is a separate executable with its own patch."] =
                "الحد الأقصى لمعدل الإطارات مع هذا الباتش يساوي VBlank Frequency، أي 60 FPS مع الإعداد الافتراضي. طور Campaign عبارة عن ملف تنفيذي منفصل وله باتش خاص به.",
            ["Skips the intro movies"] =
                "يتخطى أفلام المقدمة.",
            ["Set vblank to your desired FPS"] =
                "اضبط vblank على معدل FPS المطلوب.",
            ["Patches server URL to point to local custom server. Makes the beta work again."] =
                "يعدل URL الخاص بالخادم ليشير إلى خادم مخصص محلي. يعيد تشغيل إصدار beta مرة أخرى.",
            ["Fixes white seams on distant character models when Resolution Scale is above 100%. Validated on BCAS25003 01.03 at 200% and 250%; may reduce performance in geometry-heavy scenes."] =
                "يصلح الخطوط البيضاء عند حواف نماذج الشخصيات البعيدة عندما تكون Resolution Scale أعلى من 100%. تم التحقق على BCAS25003 01.03 عند 200% و250%؛ وقد يقلل الأداء في المشاهد كثيفة الهندسة.",
            ["Some stuff will be 2x speed"] =
                "ستعمل بعض العناصر بسرعة 2x.",
            ["Fixes white seams on distant character models when Resolution Scale is above 100%. Validated on BCAS25016 01.12; the patch keeps signed-area, back-face, and later culling stages enabled."] =
                "يصلح الخطوط البيضاء عند حواف نماذج الشخصيات البعيدة عندما تكون Resolution Scale أعلى من 100%. تم التحقق على BCAS25016 01.12؛ ويبقي الباتش مراحل signed-area وback-face ومراحل culling اللاحقة مفعلة.",
            ["Disables the game's tilt-shift depth-of-field"] =
                "يعطل تأثير tilt-shift depth-of-field في اللعبة.",
            ["Camera sensitivity is somewhat affected"] =
                "تتأثر حساسية الكاميرا إلى حد ما.",
        };
}

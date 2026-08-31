using System;
using System.Collections.Generic;

namespace R3Config.Svc;

public static partial class NoteSvc
{
    private static readonly Dictionary<string, string> ArByEn =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Prevents random freezing/crashing while playing the game"] =
                "يمنع حالات التجمد أو الانهيار العشوائي أثناء اللعب.",
            ["Fixes random crashes"] =
                "يصلح حالات الانهيار العشوائي.",
            ["Fixes graphical issues"] =
                "يصلح المشكلات الرسومية.",
            ["Fixes flickering textures"] =
                "يصلح وميض الخامات.",
            ["Fixes audio crackling"] =
                "يصلح تقطع الصوت.",
            ["Can improve performance"] =
                "قد يحسن الأداء.",
            ["Improves performance"] =
                "يحسن الأداء.",
            ["Improves stability"] =
                "يحسن الاستقرار.",
            ["May cause visual bugs"] =
                "قد يسبب مشكلات رسومية.",
            ["Compromises visuals for extra performance"] =
                "يخفض جودة الرسوم مقابل أداء أعلى.",
            ["Unlocks the FPS"] =
                "يلغي قفل معدل الإطارات.",
            ["Unlocks FPS"] =
                "يلغي قفل معدل الإطارات.",
            ["Unlocks the frame rate"] =
                "يلغي قفل معدل الإطارات.",
            ["Requires Stretch To Display Area checked in the GPU settings"] =
                "يتطلب تفعيل Stretch To Display Area في إعدادات GPU.",
            ["Requires Write Color Buffers"] =
                "يتطلب تفعيل Write Color Buffers في إعدادات GPU.",
            ["Requires Write Depth Buffers"] =
                "يتطلب تفعيل Write Depth Buffers في إعدادات GPU.",
            ["Does not provide any performance benefit"] =
                "لا يقدم أي فائدة في الأداء.",
            ["HUD toggle with Select in-game"] =
                "يمكن إخفاء واجهة HUD أو إظهارها باستخدام زر Select داخل اللعبة.",
            ["People might not instantly stop, but once they do they will stay stopped until you toggle the code off"] =
                "قد لا يتوقف الأشخاص فورا، لكن بعد توقفهم سيبقون متوقفين حتى تعطل الشفرة.",
            ["Be careful when going into water, some of it has no bottom so you will fall forever unless you quit or use the code to fly"] =
                "احذر عند دخول الماء؛ بعض المناطق بلا قاع، وقد تستمر في السقوط بلا نهاية ما لم تخرج من اللعبة أو تستخدم شفرة الطيران.",

            ["Anything other than auto can cause visual issues"] =
                "أي خيار غير Auto قد يسبب مشكلات رسومية.",
            ["Improves overly bright lighting"] =
                "يحسن الإضاءة شديدة السطوع.",
            ["Slightly improves performance"] =
                "يحسن الأداء بدرجة طفيفة.",
            ["Improves performance with no noticeable issues"] =
                "يحسن الأداء دون مشكلات ملحوظة.",
            ["Small performance gain"] =
                "يوفر تحسنا بسيطا في الأداء.",
            ["Changing to anything other than Auto causes graphical issues"] =
                "تغيير Auto إلى أي خيار آخر يسبب مشكلات رسومية.",
            ["Fixes broken graphics"] =
                "يصلح الرسوم المعطلة.",
            ["Slightly improves performance without any noticeable issues"] =
                "يحسن الأداء بدرجة طفيفة دون مشكلات ملحوظة.",
            ["Improves performance without any noticeable issues"] =
                "يحسن الأداء دون مشكلات ملحوظة.",
            ["Fixes some lighting issues"] =
                "يصلح بعض مشكلات الإضاءة.",
            ["Improves performance with no noticeable visual sacrifices"] =
                "يحسن الأداء دون تراجع رسومي ملحوظ.",
            ["Depends on CPU. It usually improves performance on high-core processors"] =
                "يعتمد على CPU؛ وغالبا يحسن الأداء على المعالجات ذات عدد الأنوية المرتفع.",
            ["Fixes visual artifacts on areas with bloom when using resolution scaling"] =
                "يصلح التشوهات الرسومية في مناطق Bloom عند استخدام Resolution Scaling.",
            ["Required for reflections and heat wave effects"] =
                "مطلوب لعرض الانعكاسات وتأثيرات Heat Wave.",
            ["To be able to get ingame for move edition"] =
                "مطلوب للدخول إلى اللعبة في إصدار Move.",
            ["Required for the game to render correctly"] =
                "مطلوب لعرض اللعبة بصورة صحيحة.",
            ["Fixes Freezes"] =
                "يصلح حالات التجمد.",
            ["May improve performance"] =
                "قد يحسن الأداء.",
            ["Fixes missing graphics ingame"] =
                "يصلح العناصر الرسومية المفقودة داخل اللعبة.",
            ["To be able to get ingame"] =
                "مطلوب للدخول إلى اللعبة.",
            ["Resolves graphical issues when resolution scaling is set above 100%"] =
                "يحل المشكلات الرسومية عند ضبط Resolution Scaling فوق 100%.",
            ["Improves performance, especially in Historic District"] =
                "يحسن الأداء، خصوصا في Historic District.",
            ["Improves performance depending on the GPU"] =
                "يحسن الأداء بدرجات متفاوتة حسب GPU.",
            ["Buffering introduces delay between microphone input and playback in the game"] =
                "يضيف التخزين المؤقت تأخيرا بين إدخال الميكروفون وتشغيل الصوت داخل اللعبة.",
            ["Required for the game to think it has a SingStar Microphone"] =
                "مطلوب لكي تتعرف اللعبة على SingStar Microphone.",
            ["Helps with stability"] =
                "يساعد على تحسين الاستقرار.",
            ["Prevents random freezing/crashing"] =
                "يمنع حالات التجمد أو الانهيار العشوائي.",
            ["Fixes various visual issues"] =
                "يصلح عدة مشكلات رسومية.",
            ["Fixes visual output"] =
                "يصلح الإخراج الرسومي.",
            ["Fixes visual issues caused by Write Depth Buffer"] =
                "يصلح المشكلات الرسومية الناتجة عن Write Depth Buffer.",
            ["Fixes green rain and particle issues. Slightly reduces performance"] =
                "يصلح المطر الأخضر ومشكلات الجزيئات، مع انخفاض طفيف في الأداء.",
            ["Required to go ingame also improves loading times"] =
                "مطلوب للدخول إلى اللعبة، كما يحسن أوقات التحميل.",
            ["Fixes Ocean Water"] =
                "يصلح عرض مياه المحيط.",
            ["Fixes excessively bright bloom"] =
                "يصلح Bloom شديد السطوع.",
            ["Fixes lighting issues"] =
                "يصلح مشكلات الإضاءة.",
            ["Fixes visual artifacts when using resolution scaling"] =
                "يصلح التشوهات الرسومية عند استخدام Resolution Scaling.",
            ["Reduces blocky graphics"] =
                "يقلل التكتلات الرسومية.",
            ["Faster than Relaxed in this game, no known issues"] =
                "أسرع من Relaxed في هذه اللعبة، ولا توجد مشكلات معروفة.",
            ["Required when analyzing the flask and nightmare stages"] =
                "مطلوب عند تحليل مرحلتي Flask وNightmare.",
            ["Required for game to work"] =
                "مطلوب لكي تعمل اللعبة.",
            ["Anything other than auto causes sprites to have green artifacts"] =
                "أي خيار غير Auto يسبب تشوهات خضراء في Sprites.",
            ["Gamespeed increases above 60fps"] =
                "تزداد سرعة اللعبة عند تجاوز 60 FPS.",
            ["If you have a stronger GPU, this can give substantial performance boost"] =
                "إذا كان لديك GPU أقوى، فقد يمنح هذا تحسنا كبيرا في الأداء.",
            ["Prevents screen borders from showing up"] =
                "يمنع ظهور حواف الشاشة.",
            ["Async mode will make player's character textures black"] =
                "وضع Async يجعل خامات شخصية اللاعب سوداء.",
            ["Big performance boost"] =
                "يحسن الأداء بدرجة كبيرة.",
            ["Fixes bloom artifacts when using Resolution scaling above 100%"] =
                "يصلح تشوهات Bloom عند استخدام Resolution Scaling فوق 100%.",
            ["Fixes missing character portraits and photos"] =
                "يصلح صور الشخصيات والصور المفقودة.",
            ["Improves performance, but may introduce black visual artifacts on some hardware"] =
                "يحسن الأداء، لكنه قد يسبب تشوهات سوداء على بعض الأجهزة.",
            ["To be used when Resolution scaling is set above 100%"] =
                "يستخدم عند ضبط Resolution Scaling فوق 100%.",
            ["Properly scales bloom when using resolution scaling"] =
                "يضبط Bloom بصورة صحيحة عند استخدام Resolution Scaling.",
            ["Needed to fully render all textures"] =
                "مطلوب لعرض جميع الخامات بالكامل.",
            ["Slight performance improvement"] =
                "تحسن طفيف في الأداء.",
            ["Requires Write Color Buffers, partially fixes reflections"] =
                "يتطلب تفعيل Write Color Buffers، ويصلح الانعكاسات جزئيا.",
            ["Fixes broken save previews"] =
                "يصلح معاينات الحفظ المعطلة.",
            ["Fixes broken visuals"] =
                "يصلح العرض الرسومي المعطل.",
            ["LLVM is broken"] =
                "LLVM لا يعمل بصورة صحيحة مع هذه اللعبة.",
            ["Fixes missing graphics"] =
                "يصلح العناصر الرسومية المفقودة.",
            ["Improves performance without any noticeable side effects"] =
                "يحسن الأداء دون آثار جانبية ملحوظة.",
            ["Required to get ingame"] =
                "مطلوب للدخول إلى اللعبة.",

            ["Fixes annoying non-working effect"] =
                "يصلح تأثيرا مزعجا لا يعمل بصورة صحيحة.",
            ["Fixes Memory Access Violation error when trying to play certain stages or going into certain areas of the maps"] =
                "يصلح خطأ Memory Access Violation عند تشغيل مراحل معينة أو دخول مناطق محددة من الخرائط.",
            ["Fixes a crash when starting first career mission. The game will randomly crash later on, which may or may not be related"] =
                "يصلح انهيارا عند بدء أول مهمة Career. قد تنهار اللعبة عشوائيا لاحقا، وقد يكون ذلك مرتبطا بالمشكلة أو غير مرتبط بها.",
            ["Fixes game crashes/freezes"] =
                "يصلح انهيارات اللعبة وحالات التجمد.",
            ["Fixes half-screen bugs. Great speed improvement"] =
                "يصلح مشكلات ظهور الصورة على نصف الشاشة، ويوفر تحسنا كبيرا في السرعة.",
            ["Fixes pad initialization when using the keyboard handler"] =
                "يصلح تهيئة يد التحكم عند استخدام Keyboard Handler.",
            ["Fixes square artifacts on particles"] =
                "يصلح التشوهات المربعة في الجزيئات.",
            ["GTA:OG - GTA Online server revival project"] =
                "GTA:OG هو مشروع لإحياء خوادم GTA Online.",
            ["Original resolution is 1024x600. Causes rendering bugs"] =
                "الدقة الأصلية هي 1024x600. يسبب هذا الخيار مشكلات في العرض.",
            ["Original resolution is 960x540. Causes rendering bugs"] =
                "الدقة الأصلية هي 960x540. يسبب هذا الخيار مشكلات في العرض.",
            ["Original resolution is 1152x640. Breaks shadows"] =
                "الدقة الأصلية هي 1152x640. يؤدي هذا الخيار إلى تعطل الظلال.",
            ["Unlocks internal timestep calculation. Increase vblank to match your desired framerate"] =
                "يفتح حساب Timestep الداخلي. ارفع VBlank ليتوافق مع معدل الإطارات المطلوب.",
            ["Updates references from gamespy.com to openspy.net. UNTESTED"] =
                "يستبدل مراجع gamespy.com بـ openspy.net. غير مختبر.",
            ["WIP. Contact me for patch revisions"] =
                "قيد التطوير. تواصل مع مؤلف الباتش للحصول على النسخ المنقحة.",
            ["Workaround for 0x68 crash"] =
                "حل مؤقت لانهيار 0x68.",
            ["Workaround for flickering ground textures"] =
                "حل مؤقت لوميض خامات الأرض.",
            ["Only recommended for gameplay. Needs vblank rate set to 120hz. Most IGC (in-game cinematics) Camera movements are still tied to 30 FPS and are sped up as a result"] =
                "موصى به أثناء اللعب فقط. يتطلب ضبط VBlank على 120 Hz. معظم حركات كاميرا IGC ما تزال مرتبطة بـ30 FPS، ولذلك ستعمل بسرعة أعلى.",
            ["Transitions/fadeouts are cut off on the bottom. Does not increase resolution. Makes screen blurry even with 1280x704 patch"] =
                "تقص الانتقالات وتأثيرات التلاشي من الأسفل. لا يرفع الدقة، ويجعل الصورة ضبابية حتى مع باتش 1280x704.",
            ["Disables Visual Effects. Gives just a tiny bit more fps than the 'Unstable VFX' patch. Use only as the last resort"] =
                "يعطل المؤثرات البصرية، ويمنح زيادة ضئيلة جدا في FPS مقارنة بباتش Unstable VFX. استخدمه كحل أخير فقط.",
            ["Increases FPS by about 50% by partially breaking fire, lightning and (some) lighting effects. Causes projectiles to become partially invisible"] =
                "يرفع FPS بنحو 50% عبر تعطيل جزء من تأثيرات النار والبرق وبعض تأثيرات الإضاءة، ويجعل المقذوفات غير مرئية جزئيا.",
            ["Increases FPS by around 50% in certain parts by disabling some shadows and lighting. Doesn't help much in sewer areas. May break sunlight completely on AMD GPUs using Vulkan. May cause glitches with some other effects like lightning"] =
                "يرفع FPS بنحو 50% في بعض المناطق عبر تعطيل بعض الظلال والإضاءة. فائدته محدودة في مناطق المجاري، وقد يعطل ضوء الشمس بالكامل على بعض بطاقات AMD عند استخدام Vulkan، كما قد يسبب خللا في تأثيرات أخرى مثل البرق.",
            ["Improves sound cackling by disabling some functions related to the audio. Lightning sounds might be distorted. Please, report any other side effects"] =
                "يخفف تقطع الصوت عبر تعطيل بعض الوظائف المرتبطة بالصوت. قد تتشوه أصوات البرق؛ يرجى الإبلاغ عن أي آثار جانبية أخرى.",
            ["Enable this patch if you are using \"Write Color Buffers\" to restore performance"] =
                "فعل هذا الباتش عند استخدام Write Color Buffers لاستعادة الأداء.",
            ["Enables rendering of Heat Wave effect under Approximate ZCull, normally works with Precise ZCull only, but Precise ZCull has a big speed impact"] =
                "يفعل عرض تأثير Heat Wave مع Approximate ZCull. يعمل هذا التأثير عادة مع Precise ZCull فقط، لكن Precise ZCull يؤثر بشدة في الأداء.",
            ["A special patch that scales down some UI elements. Rexis's 4K Mod requires this patch. Don't enable it if you don't use the 4K Mod"] =
                "باتش خاص يصغر بعض عناصر الواجهة. يتطلب Rexis's 4K Mod هذا الباتش؛ لا تفعله إذا لم تكن تستخدم 4K Mod.",
            ["A collection of QoL patches to help make some game mods possible. Currently includes: Single Combat GAP Expand DLC BGM IDs Forcibly Enable motionse.dat/acb on All Enemies Disable Player Swordtrack Loading Load Proper Boss Voices"] =
                "مجموعة من باتشات QoL للمساعدة على تشغيل بعض Mods. تتضمن حاليا: Single Combat GAP؛ توسيع معرفات DLC BGM؛ فرض تفعيل motionse.dat/acb على جميع الأعداء؛ تعطيل Player Swordtrack Loading؛ وتحميل أصوات الزعماء الصحيحة.",
            ["Fixes a threading deadlock/livelock that causes the game to freeze during gameplay under RPCS3's PPU LLVM recompiler. Changes from v1.0: - Introduces use of sys_ppu_thread_yield (syscall 0x4C) Certain RPCS3 settings can reintroduce sensitivity to this issue. Avoid changing the following from their defaults where possible: - Clock speed (PPU/SPU) - VBlank frequency - Sleep timers accuracy - SPU XFloat accuracy - Accurate RSX reservations - ZCull accuracy Remove PPU cache after applying"] =
                "يصلح حالة Deadlock/Livelock في الخيوط تتسبب في تجمد اللعبة أثناء اللعب مع PPU LLVM في RPCS3. التغييرات منذ v1.0: استخدام sys_ppu_thread_yield (syscall 0x4C). قد تعيد بعض إعدادات RPCS3 حساسية المشكلة؛ تجنب قدر الإمكان تغيير Clock speed (PPU/SPU) وVBlank frequency وSleep timers accuracy وSPU XFloat accuracy وAccurate RSX reservations وZCull accuracy عن قيمها الافتراضية. احذف PPU cache بعد تطبيق الباتش.",
            ["Version 1.0 - Fixes flickering water reflections under Approximate ZCull, normally this effect requires Precise ZCull, but Precise ZCull has a big speed impact.\\nVersion 2.0 - Enables rendering of Heat Wave effect under Approximate ZCull, again, normally works with Precise ZCull only"] =
                "الإصدار 1.0: يصلح وميض انعكاسات الماء مع Approximate ZCull؛ يحتاج هذا التأثير عادة إلى Precise ZCull، لكن Precise ZCull يؤثر بشدة في الأداء. الإصدار 2.0: يفعل عرض Heat Wave مع Approximate ZCull، رغم أن التأثير يعمل عادة مع Precise ZCull فقط.",
            ["WARNING: Causes physics issues with harpies, you will need to disable this patch to get past some sections of the game. Unlocks internal timestep calculation and fixes IGC timing issues at 60 FPS and above. Increase vblank to match your desired framerate"] =
                "تحذير: يسبب مشكلات في الفيزياء مع Harpies، وستحتاج إلى تعطيل الباتش لتجاوز بعض أقسام اللعبة. يفتح حساب Timestep الداخلي ويصلح توقيت IGC عند 60 FPS فما فوق. ارفع VBlank ليتوافق مع معدل الإطارات المطلوب.",

            ["(L3 = Restore Power)"] =
                "L3 = استعادة الطاقة.",
            ["(Must Be On)"] =
                "يجب أن تكون الشفرة مفعلة.",
            ["(Requires Enable Codes)"] =
                "يتطلب تفعيل Enable Codes.",
            ["(Requires Nothing)"] =
                "لا توجد متطلبات إضافية.",
            ["(Type Code ASM)"] =
                "نوع الشفرة: ASM.",
            ["Big explosions can kill you"] =
                "الانفجارات الكبيرة قد تقتلك.",
            ["Combine it with infinite health"] =
                "استخدمها مع Infinite Health.",
            ["Game will not progress if you use this code"] =
                "لن تتقدم اللعبة إذا استخدمت هذه الشفرة.",
            ["If you encounter some parts of the game not functioning correctly, toggle this off and retry"] =
                "إذا واجهت جزءا من اللعبة لا يعمل بصورة صحيحة، عطل الشفرة وأعد المحاولة.",
            ["It also affects enemies"] =
                "تؤثر أيضا على الأعداء.",
            ["It is your health"] =
                "هذه القيمة تمثل صحتك.",
            ["Melee attacks can still hurt you"] =
                "لا تزال هجمات الاشتباك القريب قادرة على إلحاق الضرر بك.",
            ["Not Working"] =
                "لا تعمل حاليا.",
            ["Not needed since you can fly"] =
                "غير مطلوبة ما دمت تستطيع الطيران.",
            ["Some missions might not work correctly with this"] =
                "قد لا تعمل بعض المهمات بصورة صحيحة عند تفعيل هذه الشفرة.",
            ["This can break a few missions, activate v2 for those"] =
                "قد تعطل هذه الشفرة بعض المهمات؛ فعل v2 عند مواجهة تلك الحالات.",
            ["This needs to be activated"] =
                "يجب تفعيل هذه الشفرة أولا.",
            ["This needs to be turned on before any other button codes or the game will crash"] =
                "يجب تفعيل هذه الشفرة قبل أي Button Codes أخرى، وإلا ستنهار اللعبة.",
            ["You will need to disable it if you fight them"] =
                "ستحتاج إلى تعطيل الشفرة إذا قاتلتهم.",
            ["does not work right now"] =
                "لا تعمل حاليا.",
            ["not working (WIP)"] =
                "لا تعمل حاليا (WIP).",
            ["testing"] =
                "قيد الاختبار.",
            ["("] =
                "ملاحظة المصدر غير مكتملة.",
            ["(Hold Select And Damage It, Press Select + L3 To Unmark)"] =
                "اضغط باستمرار على Select ثم ألحق ضررا بالهدف لتحديده، واضغط Select + L3 لإلغاء التحديد.",
            ["0001=Normal;000A=10x;0064=100x;03E8=1000x;2710=10000x"] =
                "القيم: 0001=عادي؛ 000A=10x؛ 0064=100x؛ 03E8=1000x؛ 2710=10000x.",
            ["000A=10x;0063=99x;01F4=500x;03E7=999x"] =
                "القيم: 000A=10x؛ 0063=99x؛ 01F4=500x؛ 03E7=999x.",
            ["000A=10x;0064=100x;03E8=1,000x;2710=10,000x"] =
                "القيم: 000A=10x؛ 0064=100x؛ 03E8=1,000x؛ 2710=10,000x.",
            ["000A=10x;0064=100x;03E8=1,000x;2710=10,000x You can still kill as many enemies as you want and this will still work"] =
                "القيم: 000A=10x؛ 0064=100x؛ 03E8=1,000x؛ 2710=10,000x. ستستمر الشفرة في العمل مهما بلغ عدد الأعداء الذين تقتلهم.",
            ["000A=10x;0064=100x;03E8=1000x;2710=10000x"] =
                "القيم: 000A=10x؛ 0064=100x؛ 03E8=1000x؛ 2710=10000x.",
            ["0=Evil 3=Good 7=Neutral and no crosshair"] =
                "القيم: 0=شر؛ 3=خير؛ 7=محايد مع عدم ظهور مؤشر التصويب.",
            ["0=Evil;3=Good"] =
                "القيم: 0=شر؛ 3=خير.",
            ["3F800000=Normal;00000000=0x;3F000000=0.5x;3F400000=0.75x;3F600000=0.875x;3FA00000=1.25x;3FC00000=1.5x;3FE00000=1.75x;40000000=2x;40400000=3x;40800000=4x;40A00000=5x"] =
                "القيم: 3F800000=عادي؛ 00000000=0x؛ 3F000000=0.5x؛ 3F400000=0.75x؛ 3F600000=0.875x؛ 3FA00000=1.25x؛ 3FC00000=1.5x؛ 3FE00000=1.75x؛ 40000000=2x؛ 40400000=3x؛ 40800000=4x؛ 40A00000=5x.",
            ["3F800000=Normal;3F000000=0.5x;3F400000=0.75x;3F600000=0.875x;3F700000=0.9375x;3FA00000=1.25x;3FC00000=1.5x;3FE00000=1.75x;40000000=2x;40200000=2.5x;40400000=3x;40800000=4x;40A00000=5x"] =
                "القيم: 3F800000=عادي؛ 3F000000=0.5x؛ 3F400000=0.75x؛ 3F600000=0.875x؛ 3F700000=0.9375x؛ 3FA00000=1.25x؛ 3FC00000=1.5x؛ 3FE00000=1.75x؛ 40000000=2x؛ 40200000=2.5x؛ 40400000=3x؛ 40800000=4x؛ 40A00000=5x.",
            ["3F800000=Normal;40000000=2x;40400000=3x;40800000=4x;40A00000=5x;40C00000=6x;40E00000=7x;41000000=8x;41100000=9x;41200000=10x"] =
                "القيم: 3F800000=عادي؛ 40000000=2x؛ 40400000=3x؛ 40800000=4x؛ 40A00000=5x؛ 40C00000=6x؛ 40E00000=7x؛ 41000000=8x؛ 41100000=9x؛ 41200000=10x.",
            ["3F800000=Normal;40000000=2x;40400000=3x;40800000=4x;40B00000=5x"] =
                "القيم: 3F800000=عادي؛ 40000000=2x؛ 40400000=3x؛ 40800000=4x؛ 40B00000=5x.",
            ["3F800000=normal;3FA00000=1.25x;3FC00000=1.5x;3FE00000=1.75x;40000000=2x;40200000=2.5x;40400000=3x;40800000=4x;40A00000=5x;40C00000=6x;4120000=10x"] =
                "القيم: 3F800000=عادي؛ 3FA00000=1.25x؛ 3FC00000=1.5x؛ 3FE00000=1.75x؛ 40000000=2x؛ 40200000=2.5x؛ 40400000=3x؛ 40800000=4x؛ 40A00000=5x؛ 40C00000=6x؛ 4120000=10x.",
            ["3F80=Normal;3FA0=1.25x;3FC0=1.5x;3FE0=1.75;4000=2x;4020=2.5x;4040=3x"] =
                "القيم: 3F80=عادي؛ 3FA0=1.25x؛ 3FC0=1.5x؛ 3FE0=1.75؛ 4000=2x؛ 4020=2.5x؛ 4040=3x.",
            ["3F80=Normal;4000=2x;4040=3x;4080=4x;40A0=5x"] =
                "القيم: 3F80=عادي؛ 4000=2x؛ 4040=3x؛ 4080=4x؛ 40A0=5x.",
            ["3F80=default;3F90=1.125x;3FA0=1.25x;3FB0=1.375x;3F C0=1.5x;3FD0=1.625x;3FE0=1.75x;3FF0=1.875x;4000=2x"] =
                "القيم: 3F80=افتراضي؛ 3F90=1.125x؛ 3FA0=1.25x؛ 3FB0=1.375x؛ 3F C0=1.5x؛ 3FD0=1.625x؛ 3FE0=1.75x؛ 3FF0=1.875x؛ 4000=2x.",
            ["4120=10x;42C8=100x;447A=1,000x"] =
                "القيم: 4120=10x؛ 42C8=100x؛ 447A=1,000x.",
            ["4120=10x;=100x;447A=1,000x"] =
                "القيم كما وردت في المصدر: 4120=10x؛ =100x؛ 447A=1,000x.",
            ["Bars do not visually work correctly, but the code works fine for Racer"] =
                "الأشرطة لا تظهر بصورة صحيحة، لكن الشفرة تعمل بصورة سليمة مع Racer.",
            ["Better than the synchronization bar code, enemy attacks go through you without affecting you while you can still hit them"] =
                "أفضل من شفرة شريط التزامن؛ تمر هجمات الأعداء من خلالك دون أن تؤثر فيك، بينما تظل قادرا على ضربهم.",
            ["CPU Control"] =
                "التحكم في CPU.",
            ["Chanes in cheat file 000A=10x;0064=100x;03E8=1000x;2710=10000x"] =
                "القيم المتاحة في ملف الشفرة: 000A=10x؛ 0064=100x؛ 03E8=1000x؛ 2710=10000x.",
            ["Change value to get 000A=10x;0064=100x;03E8=1,000x;2710=10,000x"] =
                "غير القيمة للحصول على: 000A=10x؛ 0064=100x؛ 03E8=1,000x؛ 2710=10,000x.",
            ["Change value to get FDA00A10=Good;FDA00910=Evil"] =
                "غير القيمة للحصول على: FDA00A10=خير؛ FDA00910=شر.",
            ["Check your Artifacts screen to trigger the code, and then enter your Options and change things to make the game save them"] =
                "افتح شاشة Artifacts لتفعيل الشفرة، ثم ادخل إلى Options وغير أحد الخيارات حتى تحفظ اللعبة التغييرات.",
            ["DEV MENU (In Main Menu) + Debug Menu (Press SELECT In Pause Menu)"] =
                "DEV MENU من Main Menu، وDebug Menu بالضغط على SELECT في Pause Menu.",
            ["Disable If You Are Planning Playing Story Mode, 7CA62814=Disable"] =
                "عطل الشفرة إذا كنت تخطط للعب Story Mode؛ القيمة 7CA62814=Disable.",
            ["Do not use this without the code \"Cole Never Dies\" or the screen will go black right about where you would normally die"] =
                "لا تستخدم هذه الشفرة دون Cole Never Dies، وإلا ستصبح الشاشة سوداء تقريبا عند الموضع الذي كان من المفترض أن تموت فيه.",
            ["Don't stay in the air for more than a few seconds at a time or the game will kill you"] =
                "لا تبق في الهواء أكثر من بضع ثوان في كل مرة، وإلا ستقتلك اللعبة.",
            ["Dont change options far as time seems to be a bug woking on fixing \"Chidreams\""] =
                "لا تغير خيارات الوقت؛ يبدو أن هناك خللا، ويعمل Chidreams على إصلاحه.",
            ["Dose not work whine on vechical"] =
                "لا تعمل الشفرة أثناء وجودك في مركبة.",
            ["Enemies dying on spawn can definitely break the game, Enemies dying from 1 hit also breaks game"] =
                "موت الأعداء فور ظهورهم يكسر اللعبة بالتأكيد، كما أن قتلهم بضربة واحدة يكسرها أيضا.",
            ["Enemies never know where I am making \"Hit Clueless Enemy\" very easy to do. Turrets always get you, and enemies sometimes attack during missions"] =
                "لا يعرف الأعداء مكانك، مما يجعل Hit Clueless Enemy سهلا جدا. الأبراج ترصدك دائما، وقد يهاجمك بعض الأعداء أثناء المهمات.",
            ["Enemies will not move or do anything at all, they just turn to face you but can still be killed. If they are in turrets they will still attack"] =
                "لن يتحرك الأعداء أو ينفذوا أي أفعال؛ سيكتفون بالالتفات نحوك، ومع ذلك يمكن قتلهم. إذا كانوا داخل الأبراج فسيستمرون في الهجوم.",
            ["Energy drinks, MREs, those sort of things"] =
                "يشمل مشروبات الطاقة وMREs والعناصر المشابهة.",
            ["Enter the Chapters menu to trigger it, enter the game, and trigger a save and they will stay unlocked permanently. If you do not see Very Hard or Revengeance difficulties, use the code to unlock all difficulties to make those 2 appear in the chapters menu"] =
                "ادخل قائمة Chapters لتفعيل الشفرة، ثم ادخل اللعبة وأنشئ عملية حفظ لتبقى الخيارات مفتوحة بصورة دائمة. إذا لم تظهر صعوبتا Very Hard وRevengeance، فاستخدم شفرة فتح جميع الصعوبات أولا لإظهارهما في قائمة Chapters.",
            ["Enter the Customization Menu to access everything, and save to keep them"] =
                "ادخل Customization Menu للوصول إلى كل العناصر، ثم احفظ للاحتفاظ بها.",
            ["Flares, grenades, those sort of things"] =
                "يشمل Flares والقنابل والعناصر المشابهة.",
            ["Get A Military Or Police Unit's Attention To Acquire All Upgrades + 999,999,999 EP Points, And They Will Ignore You"] =
                "اجذب انتباه وحدة عسكرية أو وحدة شرطة للحصول على جميع الترقيات و999,999,999 EP، وبعدها سيتجاهلونك.",
            ["If an enemy grabs you this will instantly complete the button masher for you. This won't work on sequences like the train in the beginning or other things like that, I was too lazy to try finding those"] =
                "إذا أمسك بك عدو، فستكمل الشفرة ضغط الأزرار المتكرر فورا. لا تعمل في مقاطع خاصة مثل القطار في بداية اللعبة أو الحالات المشابهة؛ ولم أبحث عن تلك الحالات الأخرى.",
            ["If you have no MREs and you pick up 1, you will have 1 MRE. You pick up another MRE and you will have 9"] =
                "إذا لم يكن لديك أي MRE والتقطت واحدا، فسيصبح لديك 1 MRE. وعند التقاط MRE آخر سيصبح العدد 9.",
            ["If you have the code to fly with L2 + X on, you will not need this and can just fly from transformer to transformer"] =
                "إذا كانت شفرة الطيران باستخدام L2 + X مفعلة، فلن تحتاج هذه الشفرة ويمكنك الطيران مباشرة من محول إلى آخر.",
            ["If you notice that magic is not full for anything other than the Fire Of Ares, it is because the other 3 elemental weapons require you to upgrade them to be able to use their magic abilities"] =
                "إذا لاحظت أن السحر ليس ممتلئا لغير Fire Of Ares، فالسبب أن الأسلحة العنصرية الثلاثة الأخرى تحتاج إلى الترقية قبل أن تتمكن من استخدام قدراتها السحرية.",
            ["In Chapter 2, you will encounter a spot where you will need to control a Dwarf Gekko to continue on, and this works for it without marking the other Drawf Gekkos around"] =
                "في Chapter 2 ستصل إلى موضع يجب فيه التحكم في Dwarf Gekko للمتابعة، وتعمل الشفرة عليه دون تحديد بقية Dwarf Gekkos القريبة.",
            ["In the very beginning room in Chapter 4 after you go up the steps but before you get on the elevator, there are 2 turrets you can use on the enemies"] =
                "في الغرفة الأولى من Chapter 4، بعد صعود الدرج وقبل ركوب المصعد، توجد برجتان يمكنك استخدامهما ضد الأعداء.",
            ["Infinite Heal activates after the first power is drained, but the effect will remain active afterward"] =
                "يتفعل Infinite Heal بعد استهلاك أول جزء من الطاقة، ويبقى تأثيره فعالا بعد ذلك.",
            ["Infinite Heal activates after the first power is drained, but the effect will remain active afterward. Power chare during fighting"] =
                "يتفعل Infinite Heal بعد استهلاك أول جزء من الطاقة، ويبقى تأثيره فعالا بعد ذلك، مع شحن الطاقة أثناء القتال.",
            ["Infinite Health On Medkit gain, if you approach a Ring Of Life, the health goes back to normal and you need another Medkit for Infinite Health"] =
                "يتفعل Infinite Health عند الحصول على Medkit. إذا اقتربت من Ring Of Life فستعود الصحة إلى وضعها الطبيعي، وستحتاج إلى Medkit آخر لإعادة تفعيل Infinite Health.",
            ["It also includes Data Storage 21 to 23, I'm not certain if they can be unlocked a normal way"] =
                "يشمل أيضا Data Storage من 21 إلى 23، ولست متأكدا من إمكانية فتحها بالطريقة العادية.",
            ["It also works for the health upgrades now too, so it is no longer always 100%"] =
                "يعمل الآن مع ترقيات الصحة أيضا، لذلك لم تعد القيمة ثابتة دائما عند 100%.",
            ["J'avo Can Still Grabs"] =
                "لا يزال J'avo قادرا على الإمساك بك.",
            ["Just Know there can be only one"] =
                "انتبه: يمكن أن يكون واحد فقط مفعلا في الوقت نفسه.",
            ["Just hold select and then damage whatever it is that you want to mark to prevent its health from decreasing"] =
                "اضغط باستمرار على Select ثم ألحق ضررا بالعنصر الذي تريد تحديده لمنع انخفاض صحته.",
            ["Normally as soon as the game completely generates an item into the level somewhere, like a Hand Grenade always being in a certain spot in the level or the special green left arm the game creates after you correctly chop one off, this will cause it to instantly be picked up without you needing to walk over it"] =
                "بمجرد أن تنشئ اللعبة عنصرا بالكامل داخل المرحلة، مثل Hand Grenade الموجود في موضع ثابت أو الذراع اليسرى الخضراء الخاصة التي تنشئها اللعبة بعد قطعها بصورة صحيحة، سيلتقط العنصر فورا دون الحاجة إلى المرور فوقه.",
            ["Only Work For Zombie And Some Boss"] =
                "تعمل فقط مع Zombie وبعض الزعماء.",
            ["People might not instantly stop, but once they do they will stay stopped until you turn it off"] =
                "قد لا يتوقف الأشخاص فورا، لكن بعد توقفهم سيبقون متوقفين حتى تعطل الشفرة.",
            ["Player Contol"] =
                "التحكم باللاعب.",
            ["Remove after you have all of the items you want and then turn it off because it causes problems with blue and bronze pieces"] =
                "بعد الحصول على جميع العناصر التي تريدها، عطل الشفرة؛ استمرارها يسبب مشكلات مع القطع الزرقاء والبرونزية.",
            ["Required to make enemies ignore you by pressing L3. Turn on 1st or the game will crash"] =
                "مطلوب لجعل الأعداء يتجاهلونك عند الضغط على L3. فعل هذه الشفرة أولا، وإلا ستنهار اللعبة.",
            ["Requires the Button Enable Code to be activated once before this or the game will instantly freeze"] =
                "يتطلب تفعيل Button Enable Code مرة واحدة قبل هذه الشفرة، وإلا ستتجمد اللعبة فورا.",
            ["Requires the Marker codes for either the Dwarf Gekko in Chapter 2 or the Turrets in Chapter 4 or this will do nothing. The only unwanted effect of the code is that 1 enemy might not respond to damage for the 1st time they take damage, but after that the effect is gone again until you either control the Dwarf Gekko or Turrets again"] =
                "يتطلب Marker Codes الخاصة بـDwarf Gekko في Chapter 2 أو Turrets في Chapter 4، وإلا فلن تعمل الشفرة. الأثر الجانبي الوحيد هو أن عدوا واحدا قد لا يستجيب لأول ضرر يتلقاه؛ بعد ذلك يختفي الأثر حتى تتحكم في Dwarf Gekko أو Turrets مرة أخرى.",
            ["Requires the button enable code to be on 1st"] =
                "يتطلب تفعيل Button Enable Code أولا.",
            ["Requires the button enable code to be turned on 1st"] =
                "يتطلب تفعيل Button Enable Code أولا.",
            ["Requires the button enable code to be turned on 1st. People might not instantly stop, but once they do they will stay stopped until you toggle the code off"] =
                "يتطلب تفعيل Button Enable Code أولا. قد لا يتوقف الأشخاص فورا، لكن بعد توقفهم سيبقون متوقفين حتى تعطل الشفرة.",
            ["Save Your Game Immediately When You Have Lot Of WP"] =
                "احفظ اللعبة فورا بعد الحصول على كمية كبيرة من WP.",
            ["Select + Left = Toggle Invincibility, Select + Up = Cycle Up Through Superstyle Levels, Select + Down = Toggle Full Superstyle,Select + Right = Toggle Timers"] =
                "التحكم: Select + Left لتبديل Invincibility؛ Select + Up للتنقل صعودا بين مستويات Superstyle؛ Select + Down لتبديل Full Superstyle؛ Select + Right لتبديل المؤقتات.",
            ["Take \"Infinite WP\" After Gain Lot Of WP With \"Lot Of WP On Gain\""] =
                "فعل Infinite WP بعد الحصول على كمية كبيرة من WP باستخدام Lot Of WP On Gain.",
            ["The health code has been fixed so there should be no more weird effects on the screen when you look up or jump"] =
                "تم إصلاح شفرة الصحة، لذلك لم يعد من المفترض ظهور تأثيرات غريبة على الشاشة عند النظر إلى أعلى أو القفز.",
            ["They might still throw grenades, but this exact branch has to do with them figuring out whether they need to reload their weapons or not and it kind of tells them they need to reload but they never reload so they never shoot"] =
                "قد يستمر الأعداء في رمي القنابل، لكن هذا الفرع تحديدا يؤثر في قرارهم بشأن إعادة تلقيم الأسلحة؛ يجعلهم يعتقدون أنهم بحاجة إلى إعادة التلقيم، لكنهم لا يفعلون ذلك، ولذلك لا يطلقون النار.",
            ["Think of how the Warmonger or Cryoshot require you to repeatedly press fire instead of just holding it to fire. All weapons will have that behavior but can fire as fast as you can press it"] =
                "كما في Warmonger أو Cryoshot، ستحتاج إلى الضغط المتكرر على زر الإطلاق بدل الاستمرار في الضغط عليه. ستعمل جميع الأسلحة بهذه الطريقة، ويمكنها الإطلاق بالسرعة التي تضغط بها على الزر.",
            ["This is for the missions named \"At the Dog Track\" and \"A Desperate Race\""] =
                "هذه الشفرة مخصصة لمهمتي At the Dog Track وA Desperate Race.",
            ["This needs to be activated before holding L2 + X to fly, toggling between Arc Lightning and Overload Burst with L2 + L1, toggling AI movement with L2 + L3, and making certain objects invincible"] =
                "يجب تفعيل هذه الشفرة قبل استخدام L2 + X للطيران، أو التبديل بين Arc Lightning وOverload Burst باستخدام L2 + L1، أو تبديل حركة AI باستخدام L2 + L3، أو جعل عناصر محددة غير قابلة للضرر.",
            ["Use this with 999,999,999 HP Max Mod and you can set your Stats over 99,999. Do not use this with Max HP Code"] =
                "استخدمها مع 999,999,999 HP Max Mod لرفع Stats فوق 99,999. لا تستخدمها مع Max HP Code.",
            ["Used Hiei-YYH Ported by ChIdReamS's code for partner health to create a marker to separate your AI partner from enemies otherwise they would die from any damage too"] =
                "استخدمت شفرة صحة الشريك من Hiei-YYH والمنقولة بواسطة ChIdReamS لإنشاء Marker يفصل شريك AI عن الأعداء؛ وإلا فسيتأثر شريكك بالضرر مثلهم.",
            ["When inside the ship and flying to the planet, there's a moment where Isaac gets out of the seat to go to the back of the ship to lock those 3 canisters in place. The game normally gives you exactly 60 seconds to do it and if you don't you instantly die"] =
                "أثناء الرحلة داخل السفينة إلى الكوكب، يغادر Isaac المقعد في لحظة معينة ليتجه إلى مؤخرة السفينة ويثبت الحاويات الثلاث. تمنحك اللعبة عادة 60 ثانية بالضبط لإنهاء ذلك، وإذا لم تفعل فستموت فورا.",
            ["When you have the codes on to have all powers and to be able to use powers from both good and evil sides at the same time, the same still applies where you get Overload Burst with good karma and Arc Lightning with evil karma"] =
                "عند تفعيل شفرات جميع القوى وإتاحة قوى الخير والشر معا، تبقى القاعدة نفسها: تحصل على Overload Burst مع Good Karma وعلى Arc Lightning مع Evil Karma.",
            ["Without this, the game will only spawn a total of 40 treasure chests and then you will be stuck"] =
                "بدون هذه الشفرة ستنشئ اللعبة 40 صندوق كنز فقط، وبعدها سيتعذر عليك التقدم.",
            ["Works even if you do not do 1 headshot"] =
                "تعمل حتى إذا لم تنفذ أي Headshot.",
            ["Works even if you do not kill 1 enemy"] =
                "تعمل حتى إذا لم تقتل أي عدو.",
            ["X + Left = Health Regeneration On X + Right = Health Regeneration Off X + Up = Unfreeze NPCs X + Down = Freeze NPCs"] =
                "التحكم: X + Left لتفعيل تجدد الصحة؛ X + Right لتعطيله؛ X + Up لإلغاء تجميد NPCs؛ X + Down لتجميد NPCs.",
            ["You can control the brightness from the Options Menu with this"] =
                "يمكنك التحكم في السطوع من Options Menu باستخدام هذه الشفرة.",
            ["You can not sell all your accessories or weapons. Delete the game from xmb and start from the disc, then after that just activate the code again"] =
                "لا تبع جميع الملحقات أو الأسلحة. احذف بيانات اللعبة من XMB وشغل اللعبة من القرص، ثم فعل الشفرة مجددا.",
            ["You can still be grabbed, but it is now far easier to break out of the grab and they can not damage your health"] =
                "لا يزال من الممكن الإمساك بك، لكن الإفلات أصبح أسهل بكثير، ولن يتمكنوا من خفض صحتك.",
            ["so only 1 can be active at a time"] =
                "لذلك يمكن تفعيل خيار واحد فقط في الوقت نفسه.",

        };
}

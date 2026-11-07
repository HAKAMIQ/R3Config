using System;
using System.Collections.Generic;

namespace R3Config.Svc;

public static partial class NoteSvc
{
    private static readonly Dictionary<string, string> WikiArByEn =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["\"Optional\" Game is very blurry with MSAA enabled."] =
                "اختياري: تصبح صورة اللعبة ضبابية جدا عند تفعيل MSAA.",
            ["16x makes transparent surfaces like windows look stripy."] =
                "تجعل قيمة 16x الأسطح الشفافة مثل النوافذ تبدو مخططة.",
            ["A locked performance of 30 FPS is required to resolve issues with flickering textures and audio speed up"] =
                "يتطلب حل وميض الخامات وتسارع الصوت ثبات الأداء عند 30 FPS.",
            ["Addresses glitching graphics"] =
                "يعالج الخلل الرسومي.",
            ["Aligns graphical effects when increasing resolution scale."] =
                "يضبط محاذاة المؤثرات الرسومية عند رفع Resolution Scale.",
            ["Allows for 120 FPS cap; can go higher."] =
                "يتيح ضبط حد الإطارات على 120 FPS، ويمكن رفعه أكثر.",
            ["Allows for 58fps gameplay"] =
                "يتيح اللعب بمعدل 58 FPS.",
            ["Allows for 60 FPS with no known issues."] =
                "يتيح اللعب بمعدل 60 FPS دون مشكلات معروفة.",
            ["Allows for 60 FPS. Causes motion blur to become less pronounced."] =
                "يتيح 60 FPS ويقلل وضوح تأثير Motion Blur.",
            ["Allows for 60fps"] =
                "يتيح 60 FPS.",
            ["Allows for 60fps gameplay, no known issues. Can go higher (240 for 120fps)"] =
                "يتيح اللعب بمعدل 60 FPS دون مشكلات معروفة، ويمكن رفعه أكثر؛ استخدم 240 للوصول إلى 120 FPS.",
            ["Allows for 60FPS with no issues."] =
                "يتيح 60 FPS دون مشكلات.",
            ["Allows for 60fps with no known issues"] =
                "يتيح 60 FPS دون مشكلات معروفة.",
            ["Allows for 60FPS, can increase the value to go higher."] =
                "يتيح 60 FPS، ويمكن زيادة القيمة للوصول إلى معدل أعلى.",
            ["Allows for 60fps. Can go higher."] =
                "يتيح 60 FPS، ويمكن الوصول إلى معدل أعلى.",
            ["Allows for resolution scale to work. Greatly reduces performance."] =
                "يتيح عمل Resolution Scale، لكنه يخفض الأداء بدرجة كبيرة.",
            ["Allows the game to run above 60FPS. See Special Notes."] =
                "يتيح تشغيل اللعبة بأكثر من 60 FPS. راجع Special Notes.",
            ["Allows the game to run at 60FPS."] =
                "يتيح تشغيل اللعبة بمعدل 60 FPS.",
            ["Allows the game to run at 60FPS. Increase the value to go higher."] =
                "يتيح تشغيل اللعبة بمعدل 60 FPS. زد القيمة للوصول إلى معدل أعلى.",
            ["Allows to get ingame"] =
                "يتيح الدخول إلى اللعبة.",
            ["Amazing performance improvement."] =
                "يحسن الأداء بدرجة كبيرة جدا.",
            ["Anti-Aliasing causes this game to look pixelated."] =
                "يجعل Anti-Aliasing صورة هذه اللعبة متكسرة البكسلات.",
            ["Anti-Aliasing uses a lot of VRAM especially when paired with resolution scaling and can introduce crashes"] =
                "يستهلك Anti-Aliasing قدرا كبيرا من VRAM، خصوصا مع Resolution Scale، وقد يسبب انهيارات.",
            ["Any value higher than 100% breaks graphics."] =
                "أي قيمة أعلى من 100% تعطل الرسوم.",
            ["Anything above Auto will produce graphical glitches."] =
                "أي قيمة أعلى من Auto تسبب خللا رسوميا.",
            ["Anything else but Auto produces graphical issues."] =
                "أي قيمة غير Auto تسبب مشكلات رسومية.",
            ["Anything other than Auto causes visual issues on the game's mipmapping."] =
                "أي قيمة غير Auto تسبب مشكلات في Mipmapping الخاص باللعبة.",
            ["Anything other than Auto will introduce visual issues."] =
                "أي قيمة غير Auto تسبب مشكلات بصرية.",
            ["Anything other than PS3 Native may impact game speed/physics/audio etc."] =
                "أي قيمة غير PS3 Native قد تؤثر في سرعة اللعبة والفيزياء والصوت وغير ذلك.",
            ["Anything other than Safe causes freezing."] =
                "أي قيمة غير Safe تسبب التجمد.",
            ["Approximate and Relaxed have major graphical issues (Dark and flickering lights)"] =
                "تسبب القيمتان Approximate وRelaxed مشكلات رسومية كبيرة، منها الإضاءة الداكنة والوامضة.",
            ["At 60 menu FPS is at 60, in game maxes to 30. At 120, menu remains at 60, upper limit in game increases to 40 fps. At 240, menu increases to 80 fps, ingame remains at 40."] =
                "عند 60 تعمل القوائم بمعدل 60 FPS بينما يبلغ الحد الأقصى داخل اللعبة 30 FPS. عند 120 تبقى القوائم عند 60 ويرتفع الحد داخل اللعبة إلى 40 FPS. عند 240 ترتفع القوائم إلى 80 FPS وتبقى اللعبة عند 40 FPS.",
            ["Audio buffering can delay the audio, which can impact gameplay negatively as Project Diva games relies heavily on timing."] =
                "قد يؤخر Audio Buffering الصوت، مما يؤثر سلبا في اللعب لأن ألعاب Project Diva تعتمد بدرجة كبيرة على التوقيت.",
            ["Avoids black screen"] =
                "يمنع ظهور الشاشة السوداء.",
            ["Big performance boost. On a decent CPU, \"Approximate\" is the way to go. It provides good accuracy (slightly less than on PS3) almost across all the game. \"Precise\" would be required only on some sections on Stahl Arms (with Relaxed/Approximate, some light sources produce bloom or wrong light color not present on PS3)."] =
                "يحسن الأداء بدرجة كبيرة. على CPU جيد، تعد Approximate القيمة الأنسب وتوفر دقة جيدة أقل قليلا من PS3 في معظم اللعبة. لا تحتاج Precise إلا في بعض أقسام Stahl Arms؛ إذ قد تنتج Relaxed أو Approximate توهج Bloom أو ألوان إضاءة خاطئة غير موجودة على PS3.",
            ["Breaks pod windows and other transparent objects if disabled. Only disable if you experience graphical issues on AMD GPUs."] =
                "يؤدي تعطيله إلى كسر نوافذ Pod والعناصر الشفافة الأخرى. عطله فقط إذا واجهت مشكلات رسومية على بطاقات AMD GPU.",
            ["Can help performance on low core-count CPUs, otherwise use unlimited"] =
                "قد يحسن الأداء على وحدات CPU قليلة الأنوية؛ وفي غير ذلك استخدم Unlimited.",
            ["Can help performance on low core-count CPUs, otherwise use unlimited."] =
                "قد يحسن الأداء على وحدات CPU قليلة الأنوية؛ وفي غير ذلك استخدم Unlimited.",
            ["Can improve performance depending on the GPU."] =
                "قد يحسن الأداء حسب GPU.",
            ["Can improve performance, but Taurdus overlay texture breaks."] =
                "قد يحسن الأداء، لكنه يعطل خامة Taurdus Overlay.",
            ["Can improve performance, if your GPU is strong enough to use it."] =
                "قد يحسن الأداء إذا كان GPU قويا بما يكفي لاستخدامه.",
            ["Can prevent audio crackling on weaker CPU's"] =
                "قد يمنع تقطع الصوت على وحدات CPU الأضعف.",
            ["Causes visual issues (off by default)"] =
                "يسبب مشكلات بصرية، وهو Off افتراضيا.",
            ["Causes visual issues, including certain UI elements."] =
                "يسبب مشكلات بصرية، تشمل بعض عناصر UI.",
            ["Changing it to this value allows the game to run in 60 FPS"] =
                "يتيح تغييرها إلى هذه القيمة تشغيل اللعبة بمعدل 60 FPS.",
            ["Changing resolution scale will cause green texture artifacts."] =
                "يؤدي تغيير Resolution Scale إلى تشوهات خضراء في الخامات.",
            ["Changing this to anything besides auto can glitch out parts of the game."] =
                "قد يؤدي تغييرها إلى أي قيمة غير Auto إلى خلل في أجزاء من اللعبة.",
            ["Correctly renders visual effects on characters, otherwise it renders random textures"] =
                "يعرض المؤثرات البصرية على الشخصيات بصورة صحيحة؛ ومن دونه تظهر خامات عشوائية.",
            ["Correctly scales bloom effects when resolution scale is above 100%."] =
                "يضبط تأثيرات توهج الإضاءة (Bloom) بصورة صحيحة عندما تتجاوز Resolution Scale نسبة 100%.",
            ["Currently required for the game to boot."] =
                "مطلوب حاليا لتشغيل اللعبة.",
            ["Currently required to go ingame."] =
                "مطلوب حاليا للدخول إلى اللعبة.",
            ["Cutscenes, specifically pre-recorded cutscenes, are difficult to skip if the FPS is too high. When pausing to skip a cutscene it unpauses very quickly on its own. Capping the FPS fixes this issue. See Special Notes."] =
                "يصعب تخطي المشاهد، وخصوصا المشاهد المسجلة مسبقا، إذا كان FPS مرتفعا جدا. عند الإيقاف المؤقت لتخطي المشهد تستأنف اللعبة بسرعة تلقائيا. يؤدي وضع حد لـFPS إلى حل المشكلة. راجع Special Notes.",
            ["Degrades performance but fixes broken graphics when used with Write Color Buffers."] =
                "يخفض الأداء، لكنه يصلح الرسوم المعروضة بشكل غير صحيح عند استخدام Write Color Buffers.",
            ["Depends on GPU. It usually improves performance. It can break graphics on some sections (e.g. \"Boat Ride\")."] =
                "يعتمد على GPU. يحسن الأداء عادة، لكنه قد يعطل الرسوم في بعض الأقسام مثل Boat Ride.",
            ["Depends on GPU. It usually improves performance. It can produce some flickering on some objects (e.g. on the big red flags). The flickering seems related to \"ZCULL Accuracy\" value (more evident with \"Relaxed\")."] =
                "يعتمد على GPU ويحسن الأداء عادة. قد يسبب وميضا في بعض العناصر، مثل الأعلام الحمراء الكبيرة. يبدو أن الوميض مرتبط بقيمة ZCull Accuracy ويظهر بوضوح أكبر مع Relaxed.",
            ["Disables crowd rendering. Current workaround to <i>RPCS3 issue #12059</i>"] =
                "يوقف رسم الجمهور. حل بديل حالي لمشكلة <i>RPCS3 رقم #12059</i>.",
            ["Enable this if the game crashes on boot."] =
                "فعله إذا انهارت اللعبة عند بدء التشغيل.",
            ["Enable this when Write color buffers is enabled, if enabling Write color buffers results in effects like smoke appearing/clipping through objects that should block their visibility."] =
                "فعله مع Write Color Buffers إذا تسبب تفعيلها في ظهور مؤثرات مثل الدخان عبر عناصر يفترض أن تحجبها.",
            ["Enable to fix flat water and missing wave graphics"] =
                "فعله لإصلاح الماء المسطح واختفاء رسوم الأمواج.",
            ["Enable to fix glitches in the wave height map structure"] =
                "فعله لإصلاح الخلل في بنية خريطة ارتفاع الأمواج.",
            ["Enable to fix glitching with textures of water and skybox textures"] =
                "فعله لإصلاح الخلل في خامات الماء وخامات Skybox.",
            ["Enable to fix stuttering and freezing in-game"] =
                "فعله لإصلاح التقطع والتجمد داخل اللعبة.",
            ["Enabling this option with stronger GPU gains substantial performance boost."] =
                "يمنح تفعيل هذا الخيار تحسنا كبيرا في الأداء عند استخدام GPU أقوى.",
            ["Ensure \"Network Status\" is \"Connected\" otherwise you cannot boot past the menus to play the game."] =
                "تأكد من ضبط Network Status على Connected، وإلا فلن تتمكن من تجاوز القوائم وتشغيل اللعبة.",
            ["Faster than precise. Don't use Relaxed as it can cause crashes."] =
                "أسرع من Precise. لا تستخدم Relaxed لأنها قد تسبب انهيارات.",
            ["Fix artifacts, when using Resolution scaling above 100%"] =
                "يصلح التشوهات عند استخدام Resolution Scale فوق 100%.",
            ["Fix for bloom if a custom resolution scale is used."] =
                "يصلح Bloom عند استخدام Resolution Scale مخصصة.",
            ["Fix invisible character and readable after Create a Sims."] =
                "يصلح اختفاء الشخصية ويجعلها ظاهرة بعد Create a Sims.",
            ["Fixes 'cross stitch' visual bug."] =
                "يصلح الخلل البصري Cross Stitch.",
            ["Fixes 0.5 FPS lock on menu and crashes."] =
                "يصلح قفل القوائم عند 0.5 FPS والانهيارات.",
            ["Fixes 95% of the player's logo corruption and AC texture corruption."] =
                "يصلح 95% من تلف شعار اللاعب وتلف خامات AC.",
            ["Fixes a black screen caused by Force CPU Blit Emulation"] =
                "يصلح الشاشة السوداء الناتجة عن Force CPU Blit Emulation.",
            ["Fixes an issue causing missing or broken text in menus and loading screens"] =
                "يصلح مشكلة تسبب اختفاء النصوص أو تلفها في القوائم وشاشات التحميل.",
            ["Fixes artifacts on bloom when using resolution scaling."] =
                "يصلح تشوهات Bloom عند استخدام Resolution Scale.",
            ["Fixes artifacts when damaged by electricity."] =
                "يصلح التشوهات التي تظهر عند التعرض لضرر كهربائي.",
            ["Fixes artifacts when using Resolution Scaling above 100%."] =
                "يصلح التشوهات عند استخدام Resolution Scale فوق 100%.",
            ["Fixes audio cutting out and crashes related to audio."] =
                "يصلح انقطاع الصوت والانهيارات المرتبطة بالصوت.",
            ["Fixes audio popping."] =
                "يصلح طقطقة الصوت.",
            ["Fixes bad vignette effect (gradient made of bars) in game and on menus visible if upscaling at 200% or above."] =
                "يصلح تأثير Vignette السيئ، وهو تدرج على شكل أشرطة يظهر داخل اللعبة والقوائم عند رفع الدقة إلى 200% أو أكثر.",
            ["Fixes black artifacts"] =
                "يصلح التشوهات السوداء.",
            ["Fixes black box around camera, visible wider areas and hallways"] =
                "يصلح المربع الأسود حول الكاميرا ويتيح رؤية مناطق وممرات أوسع.",
            ["Fixes black boxes in cutscenes with Lucifer and Beatrice."] =
                "يصلح المربعات السوداء في مشاهد Lucifer وBeatrice.",
            ["Fixes black boxes in particles."] =
                "يصلح المربعات السوداء في الجزيئات.",
            ["Fixes black boxes on some particle effects."] =
                "يصلح المربعات السوداء في بعض تأثيرات الجزيئات.",
            ["Fixes black camera pictures."] =
                "يصلح صور الكاميرا السوداء.",
            ["Fixes black field."] =
                "يصلح الملعب الأسود.",
            ["Fixes black graphics"] =
                "يصلح الرسوم السوداء.",
            ["Fixes black lines on the ground visible if upscaling with \"Anti-Aliasing\" set to \"Auto\" (default)."] =
                "يصلح الخطوط السوداء على الأرض التي تظهر عند رفع الدقة مع ضبط Anti-Aliasing على Auto، وهي القيمة الافتراضية.",
            ["Fixes black screen"] =
                "يصلح الشاشة السوداء.",
            ["Fixes black screen during gameplay."] =
                "يصلح الشاشة السوداء أثناء اللعب.",
            ["Fixes black screen during new machine scene."] =
                "يصلح الشاشة السوداء في مشهد الآلة الجديدة.",
            ["Fixes Black Screen in Menu Screen and Main Game"] =
                "يصلح الشاشة السوداء في القوائم واللعبة الرئيسية.",
            ["Fixes black screen in menus and races"] =
                "يصلح الشاشة السوداء في القوائم والسباقات.",
            ["Fixes black screen in open world areas."] =
                "يصلح الشاشة السوداء في مناطق العالم المفتوح.",
            ["Fixes black screen in-game"] =
                "يصلح الشاشة السوداء داخل اللعبة.",
            ["Fixes Black Screen on Startup"] =
                "يصلح الشاشة السوداء عند بدء التشغيل.",
            ["Fixes black screen while in-game."] =
                "يصلح الشاشة السوداء أثناء اللعب.",
            ["Fixes black screen."] =
                "يصلح الشاشة السوداء.",
            ["Fixes black sky color issue."] =
                "يصلح مشكلة لون السماء الأسود.",
            ["Fixes black squares in pause menu and black patches on the ground. Noticeable in Re Chain of Memories."] =
                "يصلح المربعات السوداء في قائمة الإيقاف والبقع السوداء على الأرض، وهي واضحة في Re Chain of Memories.",
            ["Fixes black textures on AMD Radeon GPUs."] =
                "يصلح الخامات السوداء على بطاقات AMD Radeon GPU.",
            ["Fixes black track."] =
                "يصلح الحلبة السوداء.",
            ["Fixes black visuals"] =
                "يصلح العرض الأسود.",
            ["Fixes blackscreen."] =
                "يصلح الشاشة السوداء.",
            ["Fixes blinding white haze in chapter 8; Beach."] =
                "يصلح الضباب الأبيض الساطع جدا في Chapter 8: Beach.",
            ["Fixes blocky artifacts at higher resolutions"] =
                "يصلح التشوهات المتكتلة عند الدقات الأعلى.",
            ["Fixes blocky shadows when Resolution Scale is over 100%."] =
                "يصلح الظلال المتكتلة عندما تتجاوز Resolution Scale نسبة 100%.",
            ["Fixes bloom and lighting when using Resolution scaling above 100%."] =
                "يصلح Bloom والإضاءة عند استخدام Resolution Scale فوق 100%.",
            ["Fixes bloom artifacts when using resolution scaling."] =
                "يصلح تشوهات Bloom عند استخدام Resolution Scale.",
            ["Fixes bloom being too bright."] =
                "يصلح سطوع Bloom الزائد.",
            ["Fixes bloom issue."] =
                "يصلح مشكلة Bloom.",
            ["Fixes blur effect on top and bottom borders"] =
                "يصلح تأثير الضبابية على الحافتين العلوية والسفلية.",
            ["Fixes blurry screen in some chapters."] =
                "يصلح ضبابية الشاشة في بعض الفصول.",
            ["Fixes bright flickering colors"] =
                "يصلح وميض الألوان الساطع.",
            ["Fixes bright scenes while in-game, but expect a small performance hit."] =
                "يصلح المشاهد شديدة السطوع أثناء اللعب، مع انخفاض بسيط متوقع في الأداء.",
            ["Fixes bright screen ingame"] =
                "يصلح سطوع الشاشة الزائد داخل اللعبة.",
            ["Fixes bright screen ingame."] =
                "يصلح سطوع الشاشة الزائد داخل اللعبة.",
            ["Fixes brightness in some scenes."] =
                "يصلح السطوع في بعض المشاهد.",
            ["Fixes brightness issues on night tracks"] =
                "يصلح مشكلات السطوع في الحلبات الليلية.",
            ["Fixes brightness."] =
                "يصلح السطوع.",
            ["Fixes broken audio."] =
                "يصلح الصوت المعطل.",
            ["Fixes broken bloom if resolution scaling is used."] =
                "يصلح Bloom المعطل عند استخدام Resolution Scale.",
            ["Fixes broken bloom when using resolution scale."] =
                "يصلح Bloom المعطل عند استخدام Resolution Scale.",
            ["Fixes broken blur when using resolution scaling."] =
                "يصلح تأثير Blur المعطل عند استخدام Resolution Scale.",
            ["Fixes broken brightness adjustment in the sky."] =
                "يصلح ضبط السطوع المعطل في السماء.",
            ["Fixes broken brightness."] =
                "يصلح السطوع المعطل.",
            ["Fixes broken car liveries and player character textures."] =
                "يصلح طلاء السيارات وخامات شخصية اللاعب المعطلة.",
            ["Fixes broken character textures."] =
                "يصلح خامات الشخصيات المعطلة.",
            ["Fixes broken characters texture."] =
                "يصلح خامات الشخصيات المعطلة.",
            ["Fixes broken characters' outfits."] =
                "يصلح ملابس الشخصيات المعطلة.",
            ["Fixes broken Chromatic Aberration in Split-Screen Co-op."] =
                "يصلح Chromatic Aberration المعطل في نمط Split-Screen Co-op.",
            ["Fixes broken collision with ragdolls."] =
                "يصلح الاصطدامات المعطلة مع Ragdolls.",
            ["Fixes broken collision with ragdolls. Reduces performance."] =
                "يصلح تصادم الأجسام المترهلة، لكنه يخفض الأداء.",
            ["Fixes broken crowd graphics"] =
                "يصلح رسوم الجمهور المعطلة.",
            ["Fixes broken effect in pause menu and broken lighting in night-time scenarios."] =
                "يصلح المؤثر المعطل في قائمة الإيقاف والإضاءة المعطلة في المشاهد الليلية.",
            ["Fixes broken graphics in player's car and detached parts of it after crashing into any object."] =
                "يصلح الرسوم المعروضة بشكل غير صحيح في سيارة اللاعب والأجزاء المنفصلة منها بعد الاصطدام بأي عنصر.",
            ["Fixes broken graphics on some sections with the rain effect (e.g. \"Ambush\")."] =
                "يصلح الرسوم المعروضة بشكل غير صحيح في بعض الأقسام التي تستخدم تأثير المطر، مثل Ambush.",
            ["Fixes broken half screen."] =
                "يصلح ظهور الصورة على نصف الشاشة.",
            ["Fixes broken lighting"] =
                "يصلح الإضاءة المعطلة.",
            ["Fixes Broken Lighting And Missing Graphics"] =
                "يصلح الإضاءة المعطلة والعناصر الرسومية المفقودة.",
            ["Fixes broken lighting."] =
                "يصلح الإضاءة التالفة.",
            ["Fixes broken Map menu in Cellphone (Select Button)."] =
                "يصلح قائمة Map المعطلة في Cellphone عبر زر Select.",
            ["Fixes broken Map menu in Cellphone (Select Button). | Fixes flickering in the map menu."] =
                "يصلح قائمة Map المعطلة في Cellphone عبر زر Select، ويصلح الوميض في قائمة Map.",
            ["Fixes broken menu."] =
                "يصلح القائمة المعطلة.",
            ["Fixes broken menus."] =
                "يصلح القوائم المعطلة.",
            ["Fixes broken ocean rendering when using Resolution Scaling."] =
                "يصلح رسم المحيط عند استخدام Resolution Scale.",
            ["Fixes broken ocean wave logic."] =
                "يصلح منطق أمواج المحيط التالف.",
            ["Fixes broken pause menu and blood effects."] =
                "يصلح قائمة الإيقاف وتأثيرات الدم المعطلة.",
            ["Fixes broken player's character textures in cutscenes."] =
                "يصلح خامات شخصية اللاعب المعطلة في المشاهد.",
            ["Fixes broken red shadows and depth of field (buildings) when using resolution scaling."] =
                "يصلح الظلال الحمراء وتأثير Depth of Field المعطل على المباني عند استخدام Resolution Scale.",
            ["Fixes broken rendering when using split-screen."] =
                "يصلح العرض المعطل عند استخدام Split-Screen.",
            ["Fixes broken rival's face textures."] =
                "يصلح خامات وجه المنافس المعطلة.",
            ["Fixes broken screen rendering."] =
                "يصلح عرض الشاشة المعطل.",
            ["Fixes broken shadows"] =
                "يصلح الظلال المعطلة.",
            ["Fixes broken shutter compilation shaders."] =
                "يصلح Shaders المعطلة الخاصة بترجمة Shutter.",
            ["Fixes broken sunlight."] =
                "يصلح ضوء الشمس المعطل.",
            ["Fixes broken textures"] =
                "يصلح الخامات المعطلة.",
            ["Fixes broken textures on character models."] =
                "يصلح الخامات المعطلة على نماذج الشخصيات.",
            ["Fixes broken textures."] =
                "يصلح الخامات المعطلة.",
            ["Fixes broken visual in menu after playing some maps."] =
                "يصلح العرض المعطل في القائمة بعد لعب بعض الخرائط.",
            ["Fixes broken visuals and UI elements in-game."] =
                "يصلح العرض وعناصر UI المعطلة داخل اللعبة.",
            ["Fixes broken visuals when using resolution scaling."] =
                "يصلح العرض المعطل عند استخدام Resolution Scale.",
            ["Fixes broken visuals. Massive performance loss."] =
                "يصلح العرض المعطل، لكنه يسبب انخفاضا هائلا في الأداء.",
            ["Fixes broken visuals. Significant stability loss."] =
                "يصلح العرض المعطل، لكنه يقلل الاستقرار بدرجة كبيرة.",
            ["Fixes broken warp effect in Temple Trouble. May reduce performance."] =
                "يصلح تأثير Warp المعطل في Temple Trouble، وقد يخفض الأداء.",
            ["Fixes broken water reflections."] =
                "يصلح انعكاسات الماء التالفة.",
            ["Fixes broken water texture when upscaling."] =
                "يصلح خامة الماء المعطلة عند رفع الدقة.",
            ["Fixes bug present on some sections (e.g. \"Holar Tower, Iceland\") consisting on the appearance of smoke (not present on PS3) when running and disappearing when walking."] =
                "يصلح خللا في بعض الأقسام، مثل Holar Tower, Iceland، يظهر فيه دخان غير موجود على PS3 عند الركض ويختفي عند المشي.",
            ["Fixes car visuals."] =
                "يصلح عرض السيارات.",
            ["Fixes certain menu items not showing up."] =
                "يصلح عدم ظهور بعض عناصر القوائم.",
            ["Fixes character portraits."] =
                "يصلح صور الشخصيات.",
            ["Fixes characters and some areas being completely black"] =
                "يصلح ظهور الشخصيات وبعض المناطق باللون الأسود بالكامل.",
            ["Fixes clothes lagging behind player."] =
                "يصلح تأخر حركة الملابس عن اللاعب.",
            ["Fixes colour gradients and dark area colours when uprendering."] =
                "يصلح تدرجات الألوان وألوان المناطق الداكنة عند رفع دقة العرض.",
            ["Fixes continuous audio stuttering past the start screen"] =
                "يصلح تقطع الصوت المستمر بعد شاشة البداية.",
            ["Fixes corrupt save data icons"] =
                "يصلح أيقونات بيانات الحفظ التالفة.",
            ["Fixes corrupted textures on the body of a custom colored car the player drives during races."] =
                "يصلح الخامات التالفة على هيكل السيارة ذات اللون المخصص التي يقودها اللاعب في السباقات.",
            ["Fixes crackling audio"] =
                "يصلح تقطع الصوت.",
            ["Fixes crash before title screen loads. If it still crashes try 7.1/5.1"] =
                "يصلح الانهيار قبل تحميل شاشة العنوان. إذا استمر الانهيار فجرب 7.1 أو 5.1.",
            ["Fixes crashes caused by RSX desyncs. Increase to 100 if still occurring."] =
                "يصلح الانهيارات الناتجة عن عدم تزامن RSX. زد القيمة إلى 100 إذا استمرت المشكلة.",
            ["Fixes crashes on drag and time attack races, but crashes still happens during grip races."] =
                "يصلح الانهيار في سباقات Drag وTime Attack، لكن الانهيار ما زال يحدث في سباقات Grip.",
            ["Fixes crashes when using Resolution scaling."] =
                "يصلح الانهيارات عند استخدام Resolution Scale.",
            ["Fixes crashes that can happen when blit engine upscaling is enabled due to data overlap in the engine itself."] =
                "يصلح الانهيارات التي قد تحدث عند تفعيل رفع الدقة عبر محرك Blit بسبب تداخل البيانات داخل المحرك نفسه.",
            ["Fixes crashing/freezing"] =
                "يصلح الانهيار والتجمد.",
            ["Fixes dark screen."] =
                "يصلح الشاشة الداكنة.",
            ["Fixes dark visuals."] =
                "يصلح العرض الداكن.",
            ["Fixes desync audio with the cutscenes."] =
                "يصلح عدم تزامن الصوت مع المشاهد.",
            ["Fixes disappearing enemies and game crashing when a giant conduit is killed."] =
                "يصلح اختفاء الأعداء وانهيار اللعبة عند قتل Giant Conduit.",
            ["Fixes dynamic lighting bloom."] =
                "يصلح Bloom الخاص بالإضاءة الديناميكية.",
            ["Fixes environment lighting and materials"] =
                "يصلح إضاءة البيئة وخاماتها.",
            ["Fixes excessive brightness."] =
                "يصلح السطوع المفرط.",
            ["Fixes fire/smoke effects."] =
                "يصلح تأثيرات النار والدخان.",
            ["Fixes flashing image between transitions. | Fixes flashing image on transitions."] =
                "يصلح وميض الصورة أثناء الانتقالات.",
            ["Fixes flickering artifacts in some areas of the game."] =
                "يصلح تشوهات الوميض في بعض مناطق اللعبة.",
            ["Fixes flickering artifacts in some areas of the game. Can greatly reduce performance, especially when resolution scaling."] =
                "يصلح تشوهات الوميض في بعض مناطق اللعبة، لكنه قد يقلل الأداء بشدة، خاصة عند رفع Resolution Scale.",
            ["Fixes flickering bloom."] =
                "يصلح وميض Bloom.",
            ["Fixes flickering foliage"] =
                "يصلح وميض الأشجار والنباتات.",
            ["Fixes flickering foliage."] =
                "يصلح وميض الأشجار والنباتات في المشهد.",
            ["Fixes flickering glitches on robots."] =
                "يصلح خلل الوميض على الروبوتات.",
            ["Fixes flickering HUD."] =
                "يصلح وميض HUD.",
            ["Fixes HUD meter rendering issues where only one meter can be displayed at a time causing the health/weapon XP bars to render incorrectly and the mini-map to flicker"] =
                "يصلح مشكلات عرض عدادات HUD التي لا يظهر فيها إلا عداد واحد في كل مرة، مما يؤدي إلى عرض أشرطة الصحة وخبرة السلاح (XP) بشكل غير صحيح ووميض الخريطة المصغرة.",
            ["Fixes flickering in the main menu."] =
                "يصلح الوميض في القائمة الرئيسية.",
            ["Fixes flickering issue."] =
                "يصلح مشكلة الوميض.",
            ["Fixes flickering lights"] =
                "يصلح وميض الإضاءة.",
            ["Fixes flickering loading screen before the level begins."] =
                "يصلح وميض شاشة التحميل قبل بدء المرحلة.",
            ["Fixes flickering loading screens."] =
                "يصلح وميض شاشات التحميل.",
            ["Fixes flickering shadows."] =
                "يصلح وميض الظلال.",
            ["Fixes flickering texture."] =
                "يصلح وميض الخامات.",
            ["Fixes flickering. Required only if you are using Async Texture Streaming."] =
                "يصلح الوميض. لا يلزم إلا عند استخدام Async Texture Streaming.",
            ["Fixes flickering/tearing textures usually present on top left part of screen."] =
                "يصلح وميض الخامات وتمزقها الذي يظهر عادة في الجزء العلوي الأيسر من الشاشة.",
            ["Fixes flood."] =
                "يصلح عرض الفيضان.",
            ["Fixes foliage graphics."] =
                "يصلح رسوم الأشجار والنباتات.",
            ["Fixes frequent crashes."] =
                "يصلح الانهيارات المتكررة.",
            ["Fixes Frequent Freezes"] =
                "يصلح حالات التجمد المتكررة.",
            ["Fixes game logic issues"] =
                "يصلح مشكلات منطق اللعبة.",
            ["Fixes garbled audio."] =
                "يصلح الصوت المشوش.",
            ["Fixes geometry flickering. Values above 30FPS cause incorrect game logic."] =
                "يصلح وميض الأشكال الهندسية. تسبب القيم الأعلى من 30 FPS خللا في منطق اللعبة.",
            ["Fixes glitched menu text when using Vulkan."] =
                "يصلح نص القوائم المشوه عند استخدام Vulkan.",
            ["Fixes glitches with golfers clothing"] =
                "يصلح الخلل في ملابس لاعبي الغولف.",
            ["Fixes graininess when resolution scaling."] =
                "يصلح خشونة الصورة عند رفع Resolution Scale.",
            ["Fixes grainy effects when resolution scaling."] =
                "يصلح التأثيرات المحببة في الصورة عند رفع Resolution Scale.",
            ["Fixes graphical glitches on the golfer's clothing"] =
                "يصلح الخلل الرسومي في ملابس لاعب الغولف.",
            ["Fixes graphical glitches with golfers clothing"] =
                "يصلح الخلل الرسومي في ملابس لاعبي الغولف.",
            ["Fixes graphical issues when using resolution scale."] =
                "يصلح المشكلات الرسومية عند استخدام Resolution Scale.",
            ["Fixes graphics repeating"] =
                "يصلح تكرار الرسوم.",
            ["Fixes graphics."] =
                "يصلح الرسوم.",
            ["Fixes green artifacts and scales bloom when using Resolution Scaling."] =
                "يصلح التشوهات الخضراء ويضبط تحجيم Bloom عند استخدام Resolution Scale.",
            ["Fixes grey/untextured player mech"] =
                "يصلح ظهور Mech الخاص باللاعب رماديا أو بلا خامات.",
            ["Fixes helmets not rendering properly"] =
                "يصلح عرض الخوذ بصورة غير صحيحة.",
            ["Fixes horrible gas, smoke and fire."] =
                "يصلح عرض الغاز والدخان والنار المشوه.",
            ["Fixes icons missing for some of the seed packets"] =
                "يصلح الأيقونات المفقودة لبعض عبوات البذور.",
            ["Fixes incorrect bloom effect"] =
                "يصلح تأثير توهج الإضاءة (Bloom) غير الصحيح.",
            ["Fixes incorrect game speed."] =
                "يصلح سرعة اللعبة غير الصحيحة.",
            ["Fixes incorrect horrible gas colors when using resolution scaling."] =
                "يصلح ألوان الغاز المشوهة عند استخدام Resolution Scale.",
            ["Fixes incorrect textures in muddy tracks like Mudpool."] =
                "يصلح الخامات غير الصحيحة في الحلبات الطينية مثل Mudpool.",
            ["Fixes invisible players."] =
                "يصلح اختفاء اللاعبين.",
            ["Fixes issue with animals not detected in photos"] =
                "يصلح عدم اكتشاف الحيوانات في الصور.",
            ["Fixes issue with shaders"] =
                "يصلح مشكلة Shaders.",
            ["Fixes issues when increasing render resolution."] =
                "يصلح المشكلات عند رفع دقة العرض.",
            ["Fixes Issues with Resolution Scaling"] =
                "يصلح مشكلات Resolution Scale.",
            ["Fixes jumping screen and improves perfomance."] =
                "يصلح اهتزاز الشاشة ويحسن الأداء.",
            ["Fixes lagging audio."] =
                "يصلح تأخر الصوت.",
            ["Fixes lighting at wrong depth when rendering lighting on the GPU"] =
                "يصلح ظهور الإضاءة على عمق خاطئ عند عرضها على GPU.",
            ["Fixes lighting issue"] =
                "يصلح مشكلة الإضاءة.",
            ["Fixes lighting issues, specially in Chapter 4."] =
                "يصلح مشكلات الإضاءة، وخصوصا في Chapter 4.",
            ["Fixes lines on the sky on NVIDIA Turing GPUs or newer."] =
                "يصلح الخطوط في السماء على بطاقات NVIDIA Turing GPU أو الأحدث.",
            ["Fixes long loading times. Also fixes broken audio during loading screens."] =
                "يصلح طول زمن التحميل، ويصلح كذلك تلف الصوت في شاشات التحميل.",
            ["Fixes low performance"] =
                "يصلح انخفاض الأداء.",
            ["Fixes low performance in-game."] =
                "يصلح ضعف الأداء داخل اللعبة.",
            ["fixes low performance ingame"] =
                "يصلح ضعف الأداء داخل اللعبة.",
            ["Fixes menu glitches when combined with Force CPU Blit"] =
                "يصلح خلل القوائم عند استخدامه مع Force CPU Blit.",
            ["Fixes minor graphical bug on electric gates."] =
                "يصلح خللا رسوميا بسيطا في البوابات الكهربائية.",
            ["Fixes minor graphical bug on electric gates. Only use if you're not using resolution scaling."] =
                "يصلح خللا رسوميا بسيطا في البوابات الكهربائية. استخدمه فقط إذا لم تكن تستخدم Resolution Scale.",
            ["Fixes missing AC graphics."] =
                "يصلح رسوم AC المفقودة.",
            ["Fixes missing audio"] =
                "يصلح الصوت المفقود.",
            ["Fixes missing audio in final cutscene."] =
                "يصلح الصوت المفقود في المشهد الأخير.",
            ["Fixes missing audio in intro videos."] =
                "يصلح الصوت المفقود في فيديوهات المقدمة.",
            ["Fixes missing audio."] =
                "يصلح اختفاء الصوت.",
            ["Fixes missing bloom and random overly bright sky in Radiator Springs."] =
                "يصلح Bloom المفقود والسطوع العشوائي الزائد للسماء في Radiator Springs.",
            ["Fixes missing car and \"speed zone\" textures"] =
                "يصلح خامات السيارة وSpeed Zone المفقودة.",
            ["Fixes missing car liveries"] =
                "يصلح طلاء السيارات المفقود.",
            ["Fixes missing collision. Requires SPU LLVM recompiler. Expect audio stutter."] =
                "يصلح الاصطدامات المفقودة. يتطلب SPU Decoder بالقيمة Recompiler (LLVM)، مع توقع تقطع الصوت.",
            ["Fixes missing font characters in menus"] =
                "يصلح اختفاء بعض حروف الخط في القوائم.",
            ["Fixes missing geometry caused by Force CPU Blit Emulation"] =
                "يصلح اختفاء المجسمات الناتج عن Force CPU Blit.",
            ["Fixes missing graphics while in-game."] =
                "يصلح العناصر الرسومية المفقودة أثناء اللعب.",
            ["Fixes missing ground and lighting."] =
                "يصلح الأرض والإضاءة المفقودتين.",
            ["Fixes missing ground. Breaks resolution scale."] =
                "يصلح الأرض المفقودة، لكنه يعطل Resolution Scale.",
            ["Fixes missing image when saving costumes and black image when saving pictures as stickers"] =
                "يصلح اختفاء الصورة عند حفظ الأزياء وسواد الصورة عند حفظ اللقطات كملصقات.",
            ["Fixes missing in-game visuals."] =
                "يصلح العناصر البصرية المفقودة داخل اللعبة.",
            ["Fixes missing lighting in the main menu."] =
                "يصلح الإضاءة المفقودة في القائمة الرئيسية.",
            ["Fixes missing lighting outdoors."] =
                "يصلح الإضاءة المفقودة في المناطق الخارجية.",
            ["Fixes missing lighting."] =
                "يصلح الإضاءة المفقودة.",
            ["Fixes missing livery."] =
                "يصلح طلاء السيارة المفقود.",
            ["Fixes missing major graphics."] =
                "يصلح العناصر الرسومية الرئيسية المفقودة.",
            ["Fixes missing menu highlight."] =
                "يصلح تمييز عناصر القائمة المفقود.",
            ["Fixes missing player names on shirts"] =
                "يصلح أسماء اللاعبين المفقودة على القمصان.",
            ["Fixes missing portraits on some menus"] =
                "يصلح صور الشخصيات المفقودة في بعض القوائم.",
            ["Fixes missing raindrops."] =
                "يصلح قطرات المطر المفقودة.",
            ["Fixes missing reflections."] =
                "يصلح الانعكاسات المفقودة.",
            ["Fixes missing visuals in cutscenes and constant flickering."] =
                "يصلح العناصر البصرية المفقودة في المشاهد والوميض المستمر.",
            ["Fixes MOFmodCallback crash (RPCS3 issue #13546)"] =
                "يصلح انهيار MOFmodCallback المرتبط بمشكلة RPCS3 رقم 13546.",
            ["Fixes most broken graphics."] =
                "يصلح معظم الرسوم المشوهة.",
            ["Fixes most visual issues when using resolution scale."] =
                "يصلح معظم المشكلات الرسومية عند استخدام Resolution Scale.",
            ["Fixes multiplayer loading screen tips"] =
                "يصلح نصائح شاشة التحميل في Multiplayer.",
            ["Fixes multiplayer menu rendering"] =
                "يصلح عرض قائمة Multiplayer.",
            ["Fixes multiplayer smearing effect staying on screen for too long"] =
                "يصلح بقاء تأثير التمويه في Multiplayer على الشاشة مدة طويلة.",
            ["Fixes objects falling and popping in ground with cost of some performance."] =
                "يصلح سقوط العناصر داخل الأرض وظهورها المفاجئ، مقابل بعض الأداء.",
            ["Fixes ocean and rainbow colors on land"] =
                "يصلح ألوان المحيط وألوان قوس قزح على اليابسة.",
            ["Fixes offscreen Z-Fighting."] =
                "يصلح ظاهرة Z-Fighting خارج حدود الشاشة.",
            ["Fixes overbright graphics in some tracks."] =
                "يصلح السطوع الزائد للرسوم في بعض الحلبات.",
            ["Fixes overbright graphics."] =
                "يصلح السطوع الزائد للرسوم.",
            ["Fixes overbright image."] =
                "يصلح السطوع الزائد للصورة.",
            ["Fixes overly bright lighting"] =
                "يصلح الإضاءة شديدة السطوع.",
            ["Fixes overly bright lighting in-game."] =
                "يصلح الإضاءة شديدة السطوع داخل اللعبة.",
            ["Fixes particles not rendering properly, e.g. fire and water particles."] =
                "يصلح عرض الجزيئات بصورة غير صحيحة، مثل جزيئات النار والماء.",
            ["Fixes particles when using Resolution Scaling."] =
                "يصلح الجزيئات عند استخدام Resolution Scale.",
            ["Fixes performance issues in MGS:PW."] =
                "يصلح مشكلات الأداء في MGS:PW.",
            ["Fixes performance issues on menus"] =
                "يصلح مشكلات الأداء في القوائم.",
            ["Fixes permanent border around the screen."] =
                "يصلح الإطار الدائم حول الشاشة.",
            ["Fixes permanent vignette effect on screen."] =
                "يصلح بقاء تأثير Vignette الدائم على الشاشة.",
            ["Fixes permanent vignette on screen."] =
                "يصلح بقاء تأثير Vignette الدائم على الشاشة.",
            ["Fixes pitching bug"] =
                "يصلح خلل Pitching.",
            ["Fixes pixelated bloom when upscaling."] =
                "يصلح تكسر بكسلات Bloom عند رفع الدقة.",
            ["Fixes pixelated smoke in game and bad vignette effect (gradient made of bars) on menus visible if upscaling at 200% or above"] =
                "يصلح تكسر بكسلات الدخان داخل اللعبة وتأثير Vignette السيئ في القوائم، وهو تدرج على شكل أشرطة يظهر عند رفع الدقة إلى 200% أو أكثر.",
            ["Fixes platform outline graphical issue"] =
                "يصلح المشكلة الرسومية في حدود المنصات.",
            ["Fixes player models"] =
                "يصلح نماذج اللاعبين.",
            ["Fixes player visibility for in-game enemies to detect"] =
                "يصلح رؤية الأعداء للاعب داخل اللعبة.",
            ["Fixes player's textures"] =
                "يصلح خامات اللاعب.",
            ["Fixes problems with custom character textures breaking."] =
                "يصلح تعطل خامات الشخصيات المخصصة.",
            ["Fixes radgolls and weapons falling through the ground."] =
                "يصلح سقوط Ragdolls والأسلحة عبر الأرض.",
            ["Fixes rainbow color water"] =
                "يصلح ظهور الماء بألوان قوس قزح.",
            ["Fixes rainbow color water and dark lighting"] =
                "يصلح ظهور الماء بألوان قوس قزح والإضاءة الداكنة.",
            ["Fixes red and green artifacts on and around textures."] =
                "يصلح التشوهات الحمراء والخضراء على الخامات وحولها.",
            ["Fixes red graphics when increasing resolution."] =
                "يصلح تحول الرسوم إلى اللون الأحمر عند رفع الدقة.",
            ["Fixes rendering issues"] =
                "يصلح مشكلات العرض.",
            ["Fixes save icon preview."] =
                "يصلح معاينة أيقونة الحفظ.",
            ["Fixes screen being darker than usual"] =
                "يصلح ظهور الشاشة أغمق من المعتاد.",
            ["Fixes screen pattern on bloom when using Resolution Scaling."] =
                "يصلح النمط الظاهر على Bloom عند استخدام Resolution Scale.",
            ["Fixes skater previews, picture missions and dynamic lighting bloom, but slightly reduces performance."] =
                "يصلح معاينات المتزلجين ومهمات الصور وBloom الخاص بالإضاءة الديناميكية، لكنه يخفض الأداء قليلا.",
            ["Fixes softlocks, improves performance, infinite loading screens and button inputs not being registered consistently over 60 FPS. If issues still occur, try PS3 Native."] =
                "يصلح حالات التعليق التي تمنع التقدم ويحسن الأداء ويصلح شاشات التحميل اللانهائية وعدم تسجيل ضغطات الأزرار باستمرار فوق 60 FPS. إذا استمرت المشكلات فجرب PS3 Native.",
            ["Fixes some bloom and brightness issues. May cause game to freeze on some hardware."] =
                "يصلح بعض مشكلات Bloom والسطوع، وقد يسبب تجمد اللعبة على بعض الأجهزة.",
            ["Fixes some broken graphics like water on Battleship Bay and potentially others."] =
                "يصلح بعض الرسوم المشوهة، مثل الماء في Battleship Bay وربما مواضع أخرى.",
            ["Fixes some geometry flickering. Values above 30FPS cause incorrect game logic."] =
                "يصلح بعض وميض الأشكال الهندسية. تسبب القيم الأعلى من 30 FPS خللا في منطق اللعبة.",
            ["Fixes some graphical bugs on fighters,"] =
                "يصلح بعض الأخطاء الرسومية على المقاتلين.",
            ["Fixes some graphical bugs on fighters."] =
                "يصلح بعض الأخطاء الرسومية على المقاتلين.",
            ["Fixes some lighting issues in some instances."] =
                "يصلح بعض مشكلات الإضاءة في حالات معينة.",
            ["Fixes some loud noises during gameplay."] =
                "يصلح بعض الأصوات المرتفعة أثناء اللعب.",
            ["Fixes some minor graphical issues."] =
                "يصلح بعض المشكلات الرسومية البسيطة.",
            ["Fixes some missing visuals."] =
                "يصلح بعض العناصر البصرية المفقودة.",
            ["Fixes some overly bright scenes."] =
                "يصلح بعض المشاهد شديدة السطوع.",
            ["Fixes some visual issues"] =
                "يصلح بعض المشكلات الرسومية.",
            ["Fixes some weird stripes when Resolution scaling is set above 100%."] =
                "يصلح بعض الخطوط الغريبة عند ضبط Resolution Scale فوق 100%.",
            ["Fixes stuttering in pre-rendered cutscenes, values above 10ms introduce stuttering however."] =
                "يصلح التقطع في المشاهد المعروضة مسبقا، لكن القيم الأعلى من 10 ms تسبب التقطع.",
            ["Fixes text at start of races."] =
                "يصلح النص في بداية السباقات.",
            ["Fixes text in the menus."] =
                "يصلح النص في القوائم.",
            ["Fixes texture glitches on terrain"] =
                "يصلح خلل الخامات على التضاريس.",
            ["Fixes textures"] =
                "يصلح الخامات.",
            ["Fixes the bloom being too bright ingame"] =
                "يصلح سطوع Bloom الزائد داخل اللعبة.",
            ["Fixes the game appearing too dark."] =
                "يصلح ظهور اللعبة بدرجة داكنة جدا.",
            ["Fixes the green artfacting when shooting entrencher"] =
                "يصلح التشوهات الخضراء عند إطلاق Entrencher.",
            ["Fixes the majority of broken graphics."] =
                "يصلح معظم الرسوم المشوهة.",
            ["Fixes the missing shaders on the first boot."] =
                "يصلح Shaders المفقودة عند بدء التشغيل الأول.",
            ["Fixes the ocean being invisible"] =
                "يصلح اختفاء المحيط.",
            ["Fixes the X on mission markers flickering."] =
                "يصلح وميض علامة X على مؤشرات المهام.",
            ["Fixes tiling artifacts"] =
                "يصلح تشوهات Tiling.",
            ["Fixes tiling artifacts seen in some areas of the game"] =
                "يصلح تشوهات Tiling الظاهرة في بعض مناطق اللعبة.",
            ["Fixes timing issue."] =
                "يصلح مشكلة التوقيت.",
            ["Fixes unloaded textures on npcs."] =
                "يصلح الخامات غير المحملة على NPCs.",
            ["Fixes various cases of Z-Fighting."] =
                "يصلح حالات متعددة من ظاهرة Z-Fighting.",
            ["Fixes videos and blurry gameboard"] =
                "يصلح الفيديوهات وضبابية لوحة اللعب.",
            ["Fixes visual issues in the main menu and while in-game."] =
                "يصلح المشكلات الرسومية في القائمة الرئيسية وأثناء اللعب.",
            ["Fixes visual issues in-game (30 Years of Wrestlemania Mode)"] =
                "يصلح المشكلات الرسومية داخل اللعبة في 30 Years of Wrestlemania Mode.",
            ["Fixes visual issues in-game (Attitude Era Mode)"] =
                "يصلح المشكلات الرسومية داخل اللعبة في Attitude Era Mode.",
            ["Fixes visual issues in-game."] =
                "يصلح المشكلات الرسومية داخل اللعبة.",
            ["Fixes visual issues such as main menu being red."] =
                "يصلح المشكلات الرسومية، مثل ظهور القائمة الرئيسية باللون الأحمر.",
            ["Fixes visual issues when using resolution scale."] =
                "يصلح المشكلات الرسومية عند استخدام Resolution Scale.",
            ["Fixes visual issues when using resolution scaling."] =
                "يصلح المشكلات المرئية عند استخدام Resolution Scale.",
            ["Fixes visual issues while in-game."] =
                "يصلح المشكلات الرسومية أثناء اللعب.",
            ["Fixes visual issues with surfaces e.g mud."] =
                "يصلح المشكلات الرسومية في الأسطح، مثل الوحل.",
            ["Fixes visual issues."] =
                "يصلح المشكلات الرسومية.",
            ["Fixes visual problems in-game."] =
                "يصلح المشكلات الرسومية داخل اللعبة.",
            ["Fixes water graphics when upscaling."] =
                "يصلح رسوم الماء عند رفع دقة العرض.",
            ["Fixes water on the distance."] =
                "يصلح عرض الماء في المسافات البعيدة.",
            ["Fixes water/sky artifacts while in-game."] =
                "يصلح تشوهات الماء والسماء أثناء اللعب.",
            ["Fixes weird audio bugs."] =
                "يصلح أخطاء الصوت الغريبة.",
            ["Fixes white boxes on some particle effects."] =
                "يصلح المربعات البيضاء في بعض تأثيرات الجزيئات.",
            ["Fixes white characters in battle."] =
                "يصلح ظهور الشخصيات باللون الأبيض في المعارك.",
            ["Fixes white screen in-game."] =
                "يصلح الشاشة البيضاء داخل اللعبة.",
            ["Fixes white screen."] =
                "يصلح الشاشة البيضاء.",
            ["Fixes white squares pop in when at low health"] =
                "يصلح ظهور مربعات بيضاء مفاجئة عند انخفاض الصحة.",
            ["Fixes white squares pop in when ingame"] =
                "يصلح ظهور مربعات بيضاء مفاجئة داخل اللعبة.",
            ["Fixes Z-Fighting on geometry."] =
                "يصلح ظاهرة Z-Fighting في المجسمات.",
            ["Fixes Z-Fighting on some parts of the game. Currently causes some visual issues."] =
                "يصلح ظاهرة Z-Fighting في بعض أجزاء اللعبة، لكنه يسبب حاليا بعض المشكلات المرئية.",
            ["Forcing AF causes graphical issues with sprites"] =
                "يسبب فرض AF مشكلات رسومية في Sprites.",
            ["Forcing Anisotropic filtering causes red artifacts, use Auto"] =
                "يسبب فرض Anisotropic Filter تشوهات حمراء؛ استخدم Auto.",
            ["Framerate is dependent on Vblank rate."] =
                "يعتمد معدل الإطارات على VBlank Frequency.",
            ["Framerate is half of vblank: 60=30, 120=60, etc."] =
                "معدل الإطارات يساوي نصف VBlank Frequency: القيمة 60 تعطي 30 FPS والقيمة 120 تعطي 60 FPS، وهكذا.",
            ["Game doesn't scale correctly and appears more blurry and jagged, stick to 100."] =
                "لا تتدرج اللعبة بصورة صحيحة وتبدو أكثر ضبابية وتعرجا؛ استخدم القيمة 100.",
            ["Game speed is affected if below or above 60FPS."] =
                "تتأثر سرعة اللعبة إذا انخفض المعدل عن 60 FPS أو ارتفع عنه.",
            ["Game speed is tied to 60 FPS so going above it will cause the game to run too fast."] =
                "ترتبط سرعة اللعبة بمعدل 60 FPS، لذلك يؤدي تجاوزه إلى تشغيل اللعبة بسرعة زائدة.",
            ["Game speed is tied to 60 FPS. So a cap is necessary to avoid speedup, and a locked performance of 60 FPS is required to stop the game from slowing down."] =
                "ترتبط سرعة اللعبة بمعدل 60 FPS. يلزم وضع حد لمنع التسارع، كما يلزم ثبات الأداء عند 60 FPS لمنع تباطؤ اللعبة.",
            ["Game speed is tied to 60 FPS. Therefore, a cap is necessary to avoid speedup and a locked performance of 60 FPS is required to stop the game from slowing down."] =
                "ترتبط سرعة اللعبة بمعدل 60 FPS. لذلك يلزم وضع حد لمنع التسارع، كما يلزم ثبات الأداء عند 60 FPS لمنع تباطؤ اللعبة.",
            ["Game speed scales to framerate, and SS HD can easily reach >200 fps"] =
                "تتغير سرعة اللعبة مع معدل الإطارات، ويمكن أن تصل SS HD بسهولة إلى أكثر من 200 FPS.",
            ["Game tries to login to PSN"] =
                "تحاول اللعبة تسجيل الدخول إلى PSN.",
            ["Game uses a fixed framerate, so going above will speed up the game."] =
                "تستخدم اللعبة معدل إطارات ثابتا، لذلك يؤدي تجاوزه إلى تسريع اللعبة.",
            ["Game will not start otherwise"] =
                "لن تبدأ اللعبة من دونه.",
            ["Game works at 60 FPS"] =
                "تعمل اللعبة بمعدل 60 FPS.",
            ["Gives a 7% performance boost"] =
                "يحسن الأداء بنسبة 7%.",
            ["Greatly improves texture quality. Leave on Auto for AMD GPUs otherwise a scan line glitch will be visible."] =
                "يحسن جودة الخامات بدرجة كبيرة. اتركه على Auto مع بطاقات AMD GPU، وإلا سيظهر خلل Scan Line.",
            ["Helps chain climbing physics."] =
                "يحسن فيزياء تسلق السلاسل.",
            ["Helps fix visual issues in-game."] =
                "يساعد على إصلاح المشكلات الرسومية داخل اللعبة.",
            ["Helps prevent freezing"] =
                "يساعد على منع التجمد.",
            ["Helps reduce bugs when resolution scale is set to 125% +"] =
                "يساعد على تقليل الأخطاء عند ضبط Resolution Scale على 125% أو أكثر.",
            ["Helps the game run at full framerate"] =
                "يساعد اللعبة على العمل بمعدل الإطارات الكامل.",
            ["Helps to decrease minor graphical glitches"] =
                "يساعد على تقليل الخلل الرسومي البسيط.",
            ["Helps with broken graphics."] =
                "يساعد على إصلاح الرسوم المشوهة.",
            ["Helps with preventing crashes, but murders performance."] =
                "يساعد على منع الانهيار لكنه يخفض الأداء بشدة.",
            ["Helps with stability, reduces crashes."] =
                "يحسن الاستقرار ويقلل الانهيارات.",
            ["Highly suggested: Allows you to type in the chat box with your keyboard instead of your controller."] =
                "موصى به بشدة: يتيح الكتابة في مربع المحادثة باستخدام لوحة المفاتيح بدلا من يد التحكم.",
            ["Hurts performance even on a 13900K."] =
                "يخفض الأداء حتى على 13900K.",
            ["Hurts performance even on an RTX 4090."] =
                "يخفض الأداء حتى على RTX 4090.",
            ["If FPS goes over 60 the intro videos will stop you from going ingame."] =
                "إذا تجاوز FPS القيمة 60 فستمنعك فيديوهات المقدمة من الدخول إلى اللعبة.",
            ["If not set to Auto some small lines may be visible in geometry."] =
                "إذا لم يضبط على Auto فقد تظهر خطوط صغيرة في الأشكال الهندسية.",
            ["If not set to Auto some small lines may be visible over stickers."] =
                "إذا لم يضبط على Auto فقد تظهر خطوط صغيرة فوق الملصقات.",
            ["If your GPU is strong enough to use it, this can give a substantial boost in performance."] =
                "إذا كان GPU قويا بما يكفي لاستخدامه، فقد يمنح تحسنا كبيرا في الأداء.",
            ["Improves FPS and allows for better resolution upscaling."] =
                "يحسن FPS ويتيح رفع الدقة بجودة أفضل.",
            ["Improves FPS and allows for better resolution upscaling. Disable if you experience missing graphics in arcade games."] =
                "يحسن FPS ويتيح رفع الدقة بجودة أفضل. عطله إذا ظهرت عناصر رسومية مفقودة في ألعاب Arcade.",
            ["Improves image quality."] =
                "يحسن جودة الصورة.",
            ["Improves image quality. Best paired with resolution scale."] =
                "يحسن جودة الصورة، وتظهر أفضل نتيجة عند استخدامه مع Resolution Scale.",
            ["Improves level of detail. Without this environment textures will occasionally look smooth. Not influenced by framerate."] =
                "يحسن مستوى التفاصيل. من دونه قد تبدو خامات البيئة ملساء أحيانا. لا يتأثر بمعدل الإطارات.",
            ["Improves loading times."] =
                "يحسن أوقات التحميل.",
            ["Improves performance a little."] =
                "يحسن الأداء قليلا.",
            ["Improves performance but makes foliage look worse."] =
                "يحسن الأداء، لكنه يجعل النباتات تبدو أسوأ.",
            ["Improves performance depending on the GPU. Causes graphical issues without Read Depth Buffers enabled."] =
                "يحسن الأداء حسب GPU، لكنه يسبب مشكلات رسومية إذا لم تكن Read Depth Buffers مفعلة.",
            ["Improves performance depending on the hardware."] =
                "يحسن الأداء حسب الجهاز.",
            ["Improves performance if you have capable GPU."] =
                "يحسن الأداء إذا كان لديك GPU مناسب.",
            ["Improves performance over precise. Don't try relaxed as it breaks graphics and kills performance."] =
                "يحسن الأداء مقارنة بـPrecise. لا تستخدم Relaxed لأنها تعطل الرسوم وتخفض الأداء بشدة.",
            ["Improves performance significantly when using Resolution Scaling. Makes grass on the razorback level look pixelated. Leave it on Auto for splitscreen otherwise graphical glitches."] =
                "يحسن الأداء بدرجة كبيرة عند استخدام Resolution Scale. يجعل العشب في مرحلة Razorback متكسر البكسلات. اتركه على Auto في Split-Screen لتجنب الخلل الرسومي.",
            ["Improves performance slightly without affecting stability or causing graphical issues."] =
                "يحسن الأداء قليلا دون التأثير في الاستقرار أو التسبب في مشكلات رسومية.",
            ["Improves performance with no major visual/graphical glitches."] =
                "يحسن الأداء دون خلل بصري أو رسومي كبير.",
            ["Improves performance with no visible side effects."] =
                "يحسن الأداء دون آثار جانبية ظاهرة.",
            ["Improves performance, and should help with stability."] =
                "يحسن الأداء، ومن المتوقع أن يساعد على الاستقرار.",
            ["Improves performance, no known issues."] =
                "يحسن الأداء دون مشكلات معروفة.",
            ["Improves performance. May cause texture flickering when write color buffers is set to on."] =
                "يحسن الأداء، وقد يسبب وميض الخامات عند ضبط Write Color Buffers على On.",
            ["Improves performance. Optional."] =
                "يحسن الأداء، وهو اختياري.",
            ["Improves performance. Requires Write color buffers enabled."] =
                "يحسن الأداء ويتطلب تفعيل Write Color Buffers.",
            ["Improves performance. Warning: May slowdown the game if your CPU does not have enough threads."] =
                "يحسن الأداء. تحذير: قد يبطئ اللعبة إذا لم يكن لدى CPU عدد كاف من Threads.",
            ["In Time Crisis 4, the light is too bright, write color buffer fix this."] =
                "في Time Crisis 4 تكون الإضاءة شديدة السطوع، ويصلح Write Color Buffers هذه المشكلة.",
            ["Increases stability during loading, tiny performance impact."] =
                "يزيد الاستقرار أثناء التحميل مع تأثير ضئيل في الأداء.",
            ["Increasing resolution scale will cause visual issues, increasing threshold does not fix them all."] =
                "تسبب زيادة Resolution Scale مشكلات بصرية، ولا تؤدي زيادة Resolution Scale Threshold إلى إصلاحها كلها.",
            ["Increasing the resolution fixes missing explosion effects on handle rsx memory tiling, but causes visual issues."] =
                "يصلح رفع الدقة تأثيرات الانفجار المفقودة عند معالجة RSX Memory Tiling، لكنه يسبب مشكلات بصرية.",
            ["Increasing this doesn't do much as the game is based around 2D Sprites, stick with 100%"] =
                "لا تفيد زيادة هذه القيمة كثيرا لأن اللعبة تعتمد على 2D Sprites؛ استخدم 100%.",
            ["Keep this value on auto, otherwise graphical artifacts will appear."] =
                "أبق هذه القيمة على Auto، وإلا ستظهر تشوهات رسومية.",
            ["Limit the FPS to 60 if you are having trouble with Decryptor puzzles not working."] =
                "اضبط حد FPS على 60 إذا واجهت مشكلة في عدم عمل ألغاز Decryptor.",
            ["Linux users are required to change to \"Usleep Only\" since it defaults to \"As Host\" there. Setting to \"Usleep Only\" improves performance."] =
                "يجب على مستخدمي Linux تغيير القيمة إلى Usleep Only لأنها تكون As Host افتراضيا هناك. يؤدي ضبطها على Usleep Only إلى تحسين الأداء.",
            ["LLVM is more prone to freezing."] =
                "خيار LLVM أكثر عرضة للتجمد.",
            ["Login required when Network Status is set to Connected. Prevents the game from hanging on boot."] =
                "يلزم تسجيل الدخول عند ضبط Network Status على Connected. يمنع ذلك تعليق اللعبة عند بدء التشغيل.",
            ["Lower values will cause severe, striped visual artifacting in the sky if the render resolution is upscaled beyond 100%."] =
                "تسبب القيم الأقل تشوهات بصرية شديدة ومخططة في السماء إذا رفعت دقة العرض فوق 100%.",
            ["Major performance improvement"] =
                "تحسن كبير في الأداء.",
            ["Makes smear in multiplayer look a bit better as well as fixing some incorrectly rendered graphics"] =
                "يحسن مظهر تأثير Smear في Multiplayer قليلا، ويصلح بعض الرسوم المعروضة بصورة غير صحيحة.",
            ["Massive performance improvement."] =
                "تحسن كبير جدا في الأداء.",
            ["Maximum FPS is equal to half your Vblank rate."] =
                "أقصى FPS يساوي نصف قيمة Vblank Rate لديك.",
            ["May allow to get ingame, expect bad performance."] =
                "قد يتيح الدخول إلى اللعبة، مع توقع أداء ضعيف.",
            ["May be needed when using asynchronous texture streaming for some hardware to upkeep performance."] =
                "قد يلزم مع Async Texture Streaming على بعض الأجهزة للحفاظ على الأداء.",
            ["May fix graphics bugs in some levels. And unsupported on MacOS."] =
                "قد يصلح أخطاء رسومية في بعض المراحل، وهو غير مدعوم على macOS.",
            ["May help prevent the game from freezing."] =
                "قد يساعد على منع تجمد اللعبة.",
            ["May improve performance depending on hardware."] =
                "قد يحسن الأداء حسب الجهاز.",
            ["May improve performance depending on the hardware."] =
                "قد يحسن الأداء حسب الجهاز.",
            ["May improve performance on some CPUs."] =
                "قد يحسن الأداء على بعض وحدات CPU.",
            ["May improve performance on stronger GPUs."] =
                "قد يحسن الأداء على وحدات GPU الأقوى.",
            ["May improve performance on weaker CPUs."] =
                "قد يحسن الأداء على وحدات CPU الأضعف.",
            ["May prevents from random freezes/crashes."] =
                "قد يمنع حالات التجمد والانهيار العشوائية.",
            ["Mega seems a bit slower with more fluctuation in FPS."] =
                "تبدو Mega أبطأ قليلا مع تقلب أكبر في FPS.",
            ["Mesh Trimming and broken menu graphics are made visible. Stick to 100."] =
                "تظهر مشكلات Mesh Trimming ورسوم القوائم المعطلة؛ استخدم القيمة 100.",
            ["Minor Performance Increase"] =
                "زيادة طفيفة في الأداء.",
            ["Minor performance increase."] =
                "تحسن بسيط في الأداء.",
            ["Mostly fixes pixelated bloom when using resolution scaling."] =
                "يصلح معظم تكسر بكسلات Bloom عند استخدام Resolution Scale.",
            ["Motion blur effect leaves lines on right side of screen when MSAA is enabled. Also improves image quality."] =
                "يترك تأثير Motion Blur خطوطا على الجانب الأيمن من الشاشة عند تفعيل MSAA، كما يحسن جودة الصورة.",
            ["Needed along with Read Color Buffers to resolve black screen."] =
                "مطلوب مع Read Color Buffers لحل مشكلة الشاشة السوداء.",
            ["Needed along with Write Color Buffers to resolve black screen."] =
                "مطلوب مع Write Color Buffers لحل مشكلة الشاشة السوداء.",
            ["Needed for Creat Engine games"] =
                "مطلوب لألعاب Creat Engine.",
            ["Needed for digital edition to not crash on boot"] =
                "يلزم للنسخة الرقمية حتى لا تنهار عند بدء التشغيل.",
            ["Needed for graphics to display"] =
                "مطلوب لعرض الرسوم.",
            ["Needed for some mission that refuse to start with required equipment in MGS:PW"] =
                "مطلوب لبعض المهمات في MGS:PW التي ترفض البدء بالمعدات المطلوبة.",
            ["Needed for some mission that refuse to start with required equipment in MGS:PW. This also fixes crashes when you load the save for MGS2 and MGS3."] =
                "مطلوب لبعض المهمات في MGS:PW التي ترفض البدء بالمعدات المطلوبة. يصلح أيضا الانهيارات عند تحميل الحفظ في MGS2 وMGS3.",
            ["Needed for stability."] =
                "مطلوب للاستقرار.",
            ["Needed for the game to be playable"] =
                "مطلوب لتصبح اللعبة قابلة للعب.",
            ["Needed if you're on an RTX 20/30/40/50 card only, workaround for RPCS3 issue #14113."] =
                "مطلوب فقط مع بطاقات RTX 20 أو 30 أو 40 أو 50، وهو حل مؤقت لمشكلة RPCS3 رقم 14113.",
            ["Needed in order to fix [https://github.com/RPCS3/rpcs3/issues/6861/ black field glitch during day games], which is present in various NCAA Football and Madden NFL games. After building up a shader cache using Async with Shader Interpreter mode, users can switch back to Async (multithreaded) for subsequent boots if required."] =
                "مطلوب لإصلاح [https://github.com/RPCS3/rpcs3/issues/6861/ ظهور الملعب باللون الأسود أثناء المباريات النهارية] في بعض ألعاب NCAA Football وMadden NFL. بعد إنشاء Shader Cache بوضع Async with Shader Interpreter، يمكن العودة إلى Async (multithreaded) عند تشغيل اللعبة لاحقا.",
            ["Needed otherwise trainer is missing"] =
                "يلزم تفعيله، وإلا اختفت شاشة التدريب.",
            ["Needed to fix game-breaking walls of broken geometry in the races."] =
                "مطلوب لإصلاح جدران الأشكال الهندسية المعطلة التي تمنع مواصلة السباقات.",
            ["Needed to fix graphical issues in-game."] =
                "مطلوب لإصلاح المشكلات الرسومية داخل اللعبة.",
            ["Needed to fix the black menu screen and flickering issues in some levels."] =
                "مطلوب لإصلاح الشاشة السوداء في القائمة ومشكلات الوميض في بعض المراحل.",
            ["Needed to get rid of audio crackling."] =
                "مطلوب للتخلص من تقطع الصوت.",
            ["Needed to prevent game hanging/crashing when loading matches."] =
                "مطلوب لمنع تعليق اللعبة أو انهيارها عند تحميل المباريات.",
            ["Needed to render cars properly when upscaling."] =
                "مطلوب لعرض السيارات بصورة صحيحة عند رفع الدقة.",
            ["Needed to render the game properly."] =
                "مطلوب لعرض اللعبة بصورة صحيحة.",
            ["Needed to use Vulkan without black screen."] =
                "مطلوب لاستخدام Vulkan دون شاشة سوداء.",
            ["Needs high wake up delay or else it will randomly crash during races."] =
                "يتطلب قيمة مرتفعة لـDriver Wake-Up Delay، وإلا ستنهار اللعبة عشوائيا أثناء السباقات.",
            ["Needs to be enabled alongside with Write Color Buffers to fix broken graphics."] =
                "يجب تفعيله مع Write Color Buffers لإصلاح الرسوم المشوهة.",
            ["No known issues, might improve performance."] =
                "لا توجد مشكلات معروفة، وقد يحسن الأداء.",
            ["Non-default values will increase the chance of promo events crashing."] =
                "تزيد القيم غير الافتراضية احتمال انهيار أحداث Promo.",
            ["Off and Infinite crashes the game, so keep it default."] =
                "تؤدي القيمتان Off وInfinite إلى انهيار اللعبة، لذا أبق القيمة الافتراضية.",
            ["On by default. Required to fix black skyboxes and missing glow on Virgil."] =
                "القيمة الافتراضية On. مطلوب لإصلاح Skyboxes السوداء والتوهج المفقود على Virgil.",
            ["Only if you're upscaling above 150%, fixes lighting and inaccurate window bloom rendering."] =
                "استخدمه فقط عند رفع الدقة فوق 150%؛ إذ يصلح الإضاءة وعرض Bloom غير الدقيق على النوافذ.",
            ["Optional: Game has native USB keyboard support for writing on signs and books."] =
                "اختياري: تدعم اللعبة لوحة مفاتيح USB مباشرة للكتابة على اللافتات والكتب.",
            ["Other options will cause incorrect colors on Nvidia GPUs"] =
                "تسبب الخيارات الأخرى ألوانا غير صحيحة على بطاقات NVIDIA GPU.",
            ["Otherwise audio does not work"] =
                "من دونه لن يعمل الصوت.",
            ["Otherwise the video player doesn't work, or the video plays sped up"] =
                "من دونه قد لا يعمل مشغل الفيديو أو قد يعمل الفيديو بسرعة زائدة.",
            ["Paired with Read Color Buffers these settings remove a black screen effect"] =
                "تزيل هذه الإعدادات تأثير الشاشة السوداء عند استخدامها مع Read Color Buffers.",
            ["Paired with Write Color Buffers these settings remove a black screen effect"] =
                "تزيل هذه الإعدادات تأثير الشاشة السوداء عند استخدامها مع Write Color Buffers.",
            ["Partially fixes blocky/pixilated graphics"] =
                "يصلح جزئيا الرسوم المتكتلة أو متكسرة البكسلات.",
            ["Partially fixes broken visuals"] =
                "يصلح العرض المعطل جزئيا.",
            ["Partially fixes broken visuals."] =
                "يصلح العرض المعطل جزئيا.",
            ["Partially fixes half screen and other graphical issues."] =
                "يصلح جزئيا مشكلة نصف الشاشة ومشكلات رسومية أخرى.",
            ["Partially fixes half screen and other issues."] =
                "يصلح جزئيا مشكلة نصف الشاشة ومشكلات أخرى.",
            ["Past 60 Hz, vocals detection works erratically and online play has connection issues."] =
                "عند تجاوز 60 Hz يصبح اكتشاف الصوت غير مستقر وتظهر مشكلات اتصال في اللعب عبر الإنترنت.",
            ["Prevents artifacts and scales bloom when using Resolution Scaling."] =
                "يمنع التشوهات ويضبط تحجيم Bloom عند استخدام Resolution Scale.",
            ["Prevents artifacts."] =
                "يمنع التشوهات.",
            ["Prevents characters and bolts from turning invisible."] =
                "يمنع اختفاء الشخصيات وBolts.",
            ["Prevents characters from turning invisible."] =
                "يمنع اختفاء الشخصيات.",
            ["Prevents crashes, recommended values between 100-300."] =
                "يمنع الانهيارات، والقيم الموصى بها بين 100 و300.",
            ["Prevents crashing shortly after going in-game."] =
                "يمنع الانهيار بعد الدخول إلى اللعبة بوقت قصير.",
            ["Prevents desyncs while playing the game."] =
                "يمنع عدم التزامن أثناء اللعب.",
            ["Prevents freezes during loading screens."] =
                "يمنع التجمد أثناء شاشات التحميل.",
            ["Prevents game from freezing"] =
                "يمنع تجمد اللعبة.",
            ["Prevents game from freezing."] =
                "يمنع تجمد اللعبة.",
            ["Prevents hanging on boot on the game launcher. (Disc Versions only)"] =
                "يمنع التعليق عند بدء التشغيل في مشغل اللعبة (نسخ الأقراص فقط).",
            ["Prevents long pauses when choosing songs."] =
                "يمنع التوقفات الطويلة عند اختيار الأغاني.",
            ["Prevents vertex explosions out of the ground up to the sky"] =
                "يمنع انفجار Vertex من الأرض حتى السماء.",
            ["Prevents weird texture corruption in Tailfin Pass background and potentially other graphical issues."] =
                "يمنع تلف الخامات الغريب في خلفية Tailfin Pass وربما مشكلات رسومية أخرى.",
            ["Properly renders bloom when using Resolution Scaling."] =
                "يعرض Bloom بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scale bloom when using Resolution Scaling."] =
                "يضبط تحجيم Bloom بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales bloom and shadows when using Resolution Scaling."] =
                "يضبط Bloom والظلال بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales bloom effects when using resolution scaling."] =
                "يضبط تأثيرات توهج الإضاءة (Bloom) بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales Depth of Field seen in the pause menu and cutscenes when using Resolution Scaling."] =
                "يضبط تحجيم Depth of Field الظاهر في قائمة الإيقاف والمشاهد السينمائية عند استخدام Resolution Scale.",
            ["Properly scales Depth of Field when using Resolution Scaling."] =
                "يضبط Depth of Field بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales lighting effects when resolution scaling is set above 100%."] =
                "يضبط تأثيرات الإضاءة بصورة صحيحة عند ضبط Resolution Scale فوق 100%.",
            ["Properly scales shadows when using Resolution Scaling."] =
                "يضبط تحجيم الظلال بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales some bloom when using Resolution Scaling (eg: certain lights in the Citadel.)"] =
                "يضبط تحجيم بعض Bloom عند استخدام Resolution Scale (مثل بعض الأضواء في Citadel).",
            ["Properly scales the depth of field effect when doing resolution scaling above 100%."] =
                "يضبط تأثير Depth of Field بصورة صحيحة عند استخدام Resolution Scale فوق 100%.",
            ["Properly scales the sky in Jak 2 and 3 when using Resolution Scaling."] =
                "يضبط تحجيم السماء في Jak 2 و3 عند استخدام Resolution Scale.",
            ["Provides good accuracy (slightly less than on PS3) almost across all the game. \"Relaxed\" is broken. It provides worse performance than Approximate and causes big FPS drop when aiming."] =
                "توفر Approximate دقة جيدة أقل قليلا من PS3 في معظم اللعبة. قيمة Relaxed معطلة؛ فهي تقدم أداء أسوأ من Approximate وتسبب انخفاضا كبيرا في FPS عند التصويب.",
            ["Raises the game's FPS limit. Can go higher. Causes the camera's speed in the Garage and Car Lot to increase."] =
                "يرفع حد FPS في اللعبة ويمكن رفعه أكثر، لكنه يزيد سرعة الكاميرا في Garage وCar Lot.",
            ["Recommended due to the game crashing very frequently"] =
                "موصى به بسبب الانهيارات المتكررة جدا للعبة.",
            ["Recommended to fix game speed."] =
                "موصى به لإصلاح سرعة اللعبة.",
            ["Recommended to get the best performance / lower RSX load."] =
                "موصى به للحصول على أفضل أداء وخفض حمل RSX.",
            ["Recommended to get the best performance."] =
                "موصى به للحصول على أفضل أداء.",
            ["Recompiler LLVM is more prone to freezing."] =
                "تكون Recompiler (LLVM) أكثر عرضة للتجمد.",
            ["Reduce crashes"] =
                "يقلل الانهيارات.",
            ["Reduces audio stutter"] =
                "يقلل تقطع الصوت.",
            ["Reduces Freeze Rate"] =
                "يقلل معدل التجمد.",
            ["Reduces freezes, kills performance. You can try Interpreter (dynamic) but that one freezes more often."] =
                "يقلل حالات التجمد لكنه يخفض الأداء بشدة. يمكنك تجربة Interpreter (dynamic)، لكنها تتجمد بوتيرة أكبر.",
            ["Reduces RSX crashes."] =
                "يقلل انهيارات RSX.",
            ["Reduces the amount of texture/model flickering while in-game."] =
                "يقلل وميض الخامات والنماذج أثناء اللعب.",
            ["Reduces the frequency of RSX desyncs."] =
                "يقلل تكرار عدم تزامن RSX.",
            ["Removes Square outline around character faces"] =
                "يزيل الإطار المربع حول وجوه الشخصيات.",
            ["Removes tearing during cutscenes and intros."] =
                "يزيل تمزق الصورة أثناء المشاهد والمقدمات.",
            ["Rendering the game above 100% results in visual problems. Play at default res scale."] =
                "يؤدي عرض اللعبة فوق 100% إلى مشكلات بصرية. استخدم Resolution Scale الافتراضية.",
            ["Renders lighting and bloom correctly."] =
                "يعرض الإضاءة وBloom بصورة صحيحة.",
            ["Renders missing fog (currently broken)"] =
                "يعرض الضباب المفقود، لكنه معطل حاليا.",
            ["Renders missing fog."] =
                "يعرض الضباب المفقود.",
            ["Renders the skybox with correct colors"] =
                "يعرض Skybox بألوان صحيحة.",
            ["Required alongside Force CPU blit emulation to fix broken graphics."] =
                "مطلوب مع Force CPU Blit Emulation لإصلاح الرسوم المشوهة.",
            ["Required for 2D UI elements which are otherwise missing."] =
                "مطلوب لعناصر 2D UI التي تكون مفقودة من دونه.",
            ["Required for Core Temp mechanic to properly work"] =
                "مطلوب حتى تعمل آلية Core Temp بصورة صحيحة.",
            ["Required for correct graphics rendering"] =
                "مطلوب لعرض الرسوم بصورة صحيحة.",
            ["Required for Creat Engine games."] =
                "مطلوب لألعاب Creat Engine.",
            ["Required for liquid physics to function properly."] =
                "مطلوب لكي تعمل فيزياء السوائل بصورة صحيحة.",
            ["Required for Rejuvenation brush technique to work."] =
                "مطلوب لعمل تقنية فرشاة Rejuvenation.",
            ["Required for resolution scaling to prevent broken graphics. Reduces performance."] =
                "مطلوب مع Resolution Scale لمنع تعطل الرسوم، ويخفض الأداء.",
            ["Required for resolution scaling. Reduces performance."] =
                "مطلوب مع Resolution Scale، ويخفض الأداء.",
            ["Required for some character sprites to render properly"] =
                "مطلوب لعرض بعض Sprites الخاصة بالشخصيات بصورة صحيحة.",
            ["Required for stability."] =
                "مطلوب للاستقرار.",
            ["Required for the game to boot properly."] =
                "مطلوب لتشغيل اللعبة بصورة صحيحة.",
            ["Required for the game to boot."] =
                "مطلوب لإقلاع اللعبة.",
            ["Required for the game to not render as a black screen"] =
                "مطلوب لمنع عرض اللعبة كشاشة سوداء.",
            ["Required for the pause menu to render"] =
                "مطلوب لعرض قائمة الإيقاف.",
            ["Required for the thumbnails to save properly in the Save/Load Menu."] =
                "مطلوب لحفظ الصور المصغرة بصورة صحيحة في Save/Load Menu.",
            ["Required if using Force CPU blit emulation and Write Color buffers to fix screen not being filled."] =
                "مطلوب عند استخدام Force CPU Blit Emulation وWrite Color Buffers لإصلاح عدم امتلاء الشاشة.",
            ["Required in some sections and the demo version."] =
                "مطلوب في بعض الأقسام وفي إصدار Demo.",
            ["Required on Nvidia GPUs to fix Flashlight and player/gun model being transparent"] =
                "مطلوب على بطاقات NVIDIA GPU لإصلاح شفافية Flashlight ونموذج اللاعب أو السلاح.",
            ["Required to access main menu."] =
                "مطلوب للوصول إلى القائمة الرئيسية.",
            ["Required to automatically change the FPS cap while ingame"] =
                "مطلوب لتغيير حد FPS تلقائيا أثناء اللعب.",
            ["Required to be able to shoot enemies and objects"] =
                "مطلوب للتمكن من إطلاق النار على الأعداء والعناصر.",
            ["Required to boot and get ingame."] =
                "مطلوب لتشغيل اللعبة والدخول إليها.",
            ["Required to boot the game, also fixes some visual problems in-game."] =
                "مطلوب لتشغيل اللعبة، كما يصلح بعض المشكلات الرسومية داخلها.",
            ["Required to change the FPS cap while ingame."] =
                "مطلوب لتغيير حد FPS أثناء اللعب.",
            ["Required to control the Gondola before Venice when you help the lady"] =
                "مطلوب للتحكم في Gondola قبل Venice عند مساعدة السيدة.",
            ["Required to control your character."] =
                "مطلوب للتحكم في شخصيتك.",
            ["Required to correctly display ingame videos. Reduces random crashes while loading levels."] =
                "مطلوب لعرض الفيديوهات داخل اللعبة بصورة صحيحة، ويقلل الانهيارات العشوائية أثناء تحميل المراحل.",
            ["Required to correctly display the background during dialogue sequences. Without Write color buffers, only a black background will be visible."] =
                "مطلوب لعرض الخلفية بصورة صحيحة أثناء الحوارات. من دون Write Color Buffers لن تظهر إلا خلفية سوداء.",
            ["Required to correctly display the background when interacting with shopkeepers. Without Write color buffers, only a black background will be visible."] =
                "مطلوب لعرض الخلفية بصورة صحيحة عند التفاعل مع البائعين. من دون Write Color Buffers لن تظهر إلا خلفية سوداء.",
            ["Required to display some graphical effects"] =
                "مطلوب لعرض بعض المؤثرات الرسومية.",
            ["Required to fix black ingame graphics and broken geometry in races."] =
                "مطلوب لإصلاح الرسوم السوداء داخل اللعبة والأشكال الهندسية المعطلة في السباقات.",
            ["Required to fix black screen in-game."] =
                "مطلوب لإصلاح الشاشة السوداء داخل اللعبة.",
            ["Required to fix black screen."] =
                "مطلوب لإصلاح الشاشة السوداء.",
            ["Required to fix broken graphics and properly scales bloom when using Resolution Scaling."] =
                "مطلوب لإصلاح الرسوم التالفة، ويضبط تحجيم Bloom عند استخدام Resolution Scale.",
            ["Required to fix broken graphics when using resolution scaling. Reduces performance."] =
                "مطلوب لإصلاح الرسوم المعطلة عند استخدام Resolution Scale، ويخفض الأداء.",
            ["Required to fix broken text and graphics."] =
                "مطلوب لإصلاح النص والرسوم المشوهة.",
            ["Required to fix collision and not fall through the ground"] =
                "مطلوب لإصلاح الاصطدامات ومنع السقوط عبر الأرض.",
            ["Required to fix crashing when highlighting Complete Machines and Special Machines at cost of performance."] =
                "مطلوب لإصلاح الانهيار عند تمييز Complete Machines وSpecial Machines، مقابل انخفاض الأداء.",
            ["Required to fix dark graphics."] =
                "مطلوب لإصلاح الرسوم الداكنة.",
            ["Required to fix freeze in intro."] =
                "مطلوب لإصلاح التجمد في المقدمة.",
            ["Required to fix graphical flickering corruption"] =
                "مطلوب لإصلاح تلف الرسوم المصحوب بالوميض.",
            ["Required to fix graphics"] =
                "مطلوب لإصلاح الرسوم.",
            ["Required to fix missing car liveries"] =
                "مطلوب لإصلاح طلاء السيارات المفقود.",
            ["Required to fix missing in-game text."] =
                "مطلوب لإصلاح النص المفقود داخل اللعبة.",
            ["Required to fix player portraits and inaccurate crowd colors at the expense of causing audio crackling."] =
                "مطلوب لإصلاح صور اللاعبين وألوان الجمهور غير الدقيقة، لكنه يسبب تقطع الصوت.",
            ["Required to fix transparent player gun model on Nvidia GPUs."] =
                "مطلوب لإصلاح شفافية نموذج سلاح اللاعب على بطاقات NVIDIA GPU.",
            ["Required to fix very poor performance."] =
                "مطلوب لإصلاح الأداء الضعيف جدا.",
            ["Required to fix whitescreen issue on titlescreen & ingame at certain locations"] =
                "مطلوب لإصلاح الشاشة البيضاء في شاشة العنوان ومواضع محددة داخل اللعبة.",
            ["Required to get in-game."] =
                "مطلوب للدخول إلى اللعبة.",
            ["Required to get ingame, expect bad performance."] =
                "مطلوب للدخول إلى اللعبة، مع توقع أداء ضعيف.",
            ["Required to get past intro screen"] =
                "مطلوب لتجاوز شاشة المقدمة.",
            ["Required to get past intro screen, \"Delay each odd MFC Command\" must be checked as well"] =
                "مطلوب لتجاوز شاشة المقدمة، ويجب أيضا تفعيل Delay each odd MFC Command.",
            ["Required to get past main menu"] =
                "مطلوب لتجاوز القائمة الرئيسية.",
            ["Required to get past main menu, \"Delay each odd MFC Command\" must be checked as well"] =
                "مطلوب لتجاوز القائمة الرئيسية، ويجب أيضا تفعيل Delay each odd MFC Command.",
            ["Required to go ingame."] =
                "مطلوب للدخول إلى اللعبة.",
            ["Required to improve stability."] =
                "مطلوب لتحسين الاستقرار.",
            ["Required to play the game."] =
                "مطلوب لتشغيل اللعبة.",
            ["Required to prevent flickering."] =
                "مطلوب لمنع الوميض.",
            ["Required to prevent severe graphical issues in some areas."] =
                "مطلوب لمنع مشكلات رسومية شديدة في بعض المناطق.",
            ["Required to properly scale bloom when using resolution scaling."] =
                "مطلوب لضبط Bloom بصورة صحيحة عند استخدام Resolution Scale.",
            ["Required to reach the main menu"] =
                "مطلوب للوصول إلى القائمة الرئيسية.",
            ["Required to render graphics properly."] =
                "مطلوب لعرض الرسوم بصورة صحيحة.",
            ["Required to resolve flickering graphics."] =
                "مطلوب لحل وميض الرسوم.",
            ["Required unless using FPS Patch due to 2x game speed at 60 FPS."] =
                "مطلوب ما لم تستخدم FPS Patch بسبب تضاعف سرعة اللعبة 2x عند 60 FPS.",
            ["Required when analyzing the flask and nightmare stages. Also fixes permanent vignette effect on screen."] =
                "مطلوب عند فحص القارورة ومراحل الكوابيس، ويصلح كذلك بقاء تأثير Vignette الدائم على الشاشة.",
            ["Required when using resolution scaling."] =
                "مطلوب عند استخدام Resolution Scale.",
            ["Requires to fixes broken black rain from the menu when using resolution scaling."] =
                "مطلوب لإصلاح المطر الأسود المعطل في القائمة عند استخدام Resolution Scale.",
            ["Requires Write Color Buffers and Read Color Buffers, fixes visuals but removes explosion effects."] =
                "يتطلب Write Color Buffers وRead Color Buffers. يصلح العرض لكنه يزيل تأثيرات الانفجار.",
            ["Resolution scaling is not supported in this game, play at 100% scaling. Going above 100% introduces visual problems."] =
                "لا تدعم هذه اللعبة Resolution Scale؛ استخدم 100%. يؤدي تجاوز 100% إلى مشكلات بصرية.",
            ["Resolves missing cutscene audio."] =
                "يصلح الصوت المفقود في المشاهد.",
            ["Resolves the black screen in the main menu and black screen due to weather effects"] =
                "يصلح الشاشة السوداء في القائمة الرئيسية والشاشة السوداء الناتجة عن تأثيرات الطقس.",
            ["Set below 16x16 for proper visuals when using resolution scaling above 100%."] =
                "اضبط القيمة دون 16x16 للحصول على عرض صحيح عند استخدام Resolution Scale فوق 100%.",
            ["Setting the frame Limit to 30 will prevent the sprite artifact, glitch."] =
                "يمنع ضبط Frame Limit على 30 خلل وتشوه Sprite.",
            ["Setting this to 200-400 μs greatly reduces random crashes"] =
                "يؤدي ضبطها بين 200 و400 μs إلى تقليل الانهيارات العشوائية بدرجة كبيرة.",
            ["Shadows in MGS2 and MGS3 require the Color buffer to be enabled to appear without borders."] =
                "تتطلب الظلال في MGS2 وMGS3 تفعيل Write Color Buffers لتظهر دون حدود.",
            ["Significantly improves render blur."] =
                "يحسن ضبابية العرض بدرجة كبيرة.",
            ["Significantly reduces audio stutter"] =
                "يقلل تقطع الصوت بدرجة كبيرة.",
            ["Slight Performance Increase"] =
                "تحسن طفيف في الأداء.",
            ["Slightly improves depth of field effect when using Resolution Scaling."] =
                "يحسن تأثير Depth of Field قليلا عند استخدام Resolution Scale.",
            ["Slightly improves performance without affecting stability."] =
                "يحسن الأداء قليلا دون التأثير في الاستقرار.",
            ["Slightly improves stability."] =
                "يحسن الاستقرار قليلا.",
            ["Slighty improves performance."] =
                "يحسن الأداء قليلا.",
            ["Small performance boost."] =
                "يحسن الأداء بدرجة بسيطة.",
            ["Small performance improvement."] =
                "تحسن بسيط في الأداء.",
            ["Solves issues where bloom/lighting is way too bright"] =
                "يحل مشكلات السطوع المفرط في Bloom والإضاءة.",
            ["Solves the issue about wrong coloring"] =
                "يحل مشكلة الألوان غير الصحيحة.",
            ["SPU LLVM causes the monster not to spawn on stage 5-3 (and likely others), use ASMJIT as it's faster than using Accurate xFloat to fix the issue."] =
                "تمنع Recompiler (LLVM) في SPU Decoder ظهور الوحش في المرحلة 5-3 وربما مراحل أخرى. استخدم Recompiler (ASMJIT) لأنها أسرع من استخدام Accurate في SPU XFloat Accuracy لإصلاح المشكلة.",
            ["Stops the game from crashing during startup."] =
                "يمنع انهيار اللعبة أثناء بدء التشغيل.",
            ["Supports keyboard for multiplayer features."] =
                "يدعم لوحة المفاتيح لميزات Multiplayer.",
            ["The game frequently switches between 30 FPS and 60 FPS."] =
                "تنتقل اللعبة كثيرا بين 30 FPS و60 FPS.",
            ["The game hangs on boot with black screen without this option enabled."] =
                "تعلق اللعبة على شاشة سوداء عند بدء التشغيل إذا لم يفعل هذا الخيار.",
            ["This allows the game to be played at 60FPS, increasing the Vblank rate further allows the game to run at a maximum of 62FPS but no higher, unfortunately."] =
                "يتيح هذا لعب اللعبة بمعدل 60 FPS. تسمح زيادة VBlank Frequency أكثر بالوصول إلى 62 FPS كحد أقصى، ولا يمكن تجاوزه.",
            ["This allows the game to run at 60 fps. The game can also run at even higher framerates with minimal issues but the game's performance is currently too inconsistent to reccomend this."] =
                "يتيح هذا تشغيل اللعبة بمعدل 60 FPS. يمكن تشغيلها بمعدلات أعلى مع مشكلات قليلة، لكن أداءها غير مستقر حاليا بما يكفي للتوصية بذلك.",
            ["This fixes red visual artifacts."] =
                "يصلح هذا التشوهات البصرية الحمراء.",
            ["This is a native 1080p PS3 title"] =
                "هذه لعبة PS3 تعمل بدقة 1080p أصلية.",
            ["This is a native 1080p title on PS3."] =
                "هذه لعبة تعمل بدقة 1080p أصلية على PS3.",
            ["This is a native 1080p title."] =
                "هذه اللعبة بدقة أصلية 1080p.",
            ["This is needed to fix a graphical issue during the Kakashi bell test boss fight."] =
                "هذا مطلوب لإصلاح مشكلة رسومية أثناء مواجهة Kakashi Bell Test.",
            ["This is to remove red hue from in game"] =
                "يستخدم لإزالة الصبغة الحمراء داخل اللعبة.",
            ["This prevents dithering artifacts on bloom at higher resolutions."] =
                "يمنع تشوهات Dithering على Bloom عند الدقات الأعلى.",
            ["This setting fixes a bug in split-screen multiplayer rendering."] =
                "يصلح هذا الإعداد خللا في عرض Multiplayer بنمط Split-Screen.",
            ["To be able to get ingame, expect low performance."] =
                "مطلوب للدخول إلى اللعبة، مع توقع أداء منخفض.",
            ["To be enabled alongside Force CPU Blit"] =
                "يجب تفعيله مع Force CPU Blit.",
            ["To be enabled alongside Write Color Buffers"] =
                "يجب تفعيله مع Write Color Buffers.",
            ["To be enabled if resolution scaling is used."] =
                "يجب تفعيله عند استخدام Resolution Scale.",
            ["To be enabled if resolution scaling is used. Fixes discoloration in certain scenes."] =
                "يجب تفعيله عند استخدام Resolution Scale. يصلح تغير الألوان في مشاهد معينة.",
            ["To be enabled in conjunction with \"Read Color Buffers,\" seems to fix UI glitches."] =
                "يجب تفعيله مع Read Color Buffers، ويبدو أنه يصلح خلل UI.",
            ["To be enabled in conjunction with \"Write Color Buffers,\" seems to fix UI glitches."] =
                "يجب تفعيله مع Write Color Buffers، ويبدو أنه يصلح خلل UI.",
            ["To be paired with CPU Blit, fixes ghost artifacts but blurs the game."] =
                "يستخدم مع CPU Blit؛ يصلح تشوهات الأشباح لكنه يجعل اللعبة ضبابية.",
            ["To be paired with WCB, fixes ghost artifacts but blurs the game."] =
                "يستخدم مع WCB؛ يصلح تشوهات الأشباح لكنه يجعل اللعبة ضبابية.",
            ["To be set if resolution scaling is used. Fixes red artifacting in shadows."] =
                "يجب ضبطه عند استخدام Resolution Scale. يصلح التشوهات الحمراء في الظلال.",
            ["To be used with resolution upscaling if the cinematic video is missing the lower third of the screen. Note that you can use keyboard arrows for precise changes on the slider."] =
                "يستخدم مع رفع الدقة إذا كان الثلث السفلي من فيديو المشهد مفقودا. يمكن استخدام أسهم لوحة المفاتيح لإجراء تغييرات دقيقة على شريط التمرير.",
            ["To fix the crash before the game even launch."] =
                "يصلح الانهيار قبل بدء اللعبة.",
            ["Unlocks the framerate."] =
                "يلغي قفل معدل الإطارات.",
            ["Upscaling the resolution above 720p looks a lot worse, use FSR as the alternative upscale."] =
                "يبدو رفع الدقة فوق 720p أسوأ بكثير؛ استخدم FSR بديلا لرفع الدقة.",
            ["Upscaling the resolution causes graphical issues."] =
                "يسبب رفع الدقة مشكلات رسومية.",
            ["Upscaling the resolution causes vertex explosions."] =
                "يسبب رفع الدقة انفجار Vertex.",
            ["Use 100%, 200% or 300%. Non-integer values e.g 150% can cause pixelated HUD."] =
                "استخدم 100% أو 200% أو 300%. قد تسبب القيم غير الصحيحة مثل 150% تكسر بكسلات HUD.",
            ["Use 100%, 200% or 300%. Non-integer values e.g 150% can pixelate menus."] =
                "استخدم 100% أو 200% أو 300%. قد تسبب القيم غير الصحيحة مثل 150% تكسر بكسلات القوائم.",
            ["Use 120 for 60fps, can go higher if desired"] =
                "استخدم 120 للوصول إلى 60 FPS، ويمكن رفع القيمة عند الرغبة.",
            ["Use if you experience audio issues."] =
                "استخدمه إذا واجهت مشكلات في الصوت.",
            ["Use this if you experience audio issues."] =
                "استخدمه إذا واجهت مشكلات في الصوت.",
            ["Use with \"Allow Host GPU Labels\" to eliminate lockups upon formation change."] =
                "استخدمه مع Allow Host GPU Labels لمنع التعليق عند تغيير التشكيلة.",
            ["Using default resolution of 1280x720 will produce black borders when maximised."] =
                "يؤدي استخدام الدقة الافتراضية 1280x720 إلى ظهور حدود سوداء عند تكبير النافذة.",
            ["Using Framelimiter can cause issues with the game, including game speed and audio."] =
                "قد يسبب استخدام Frame Limit مشكلات في اللعبة، تشمل سرعة اللعبة والصوت.",
            ["Using Relaxed XFloat makes the audio loudly broken."] =
                "يجعل استخدام Relaxed في SPU XFloat Accuracy الصوت مرتفعا ومشوها.",
            ["Values above 60FPS cause incorrect game logic. Menus will be sped up without the framelimit capped at 60 FPS. Explosion damage scales with framerate, so going above 60FPS will create game balance issues."] =
                "تسبب القيم الأعلى من 60 FPS خللا في منطق اللعبة. تتسارع القوائم إذا لم يضبط Frame Limit على 60 FPS. يتغير ضرر الانفجارات مع معدل الإطارات، لذلك يؤدي تجاوز 60 FPS إلى مشكلات في توازن اللعبة.",
            ["Values above 60Hz increase the game's frame rate limit."] =
                "تزيد القيم الأعلى من 60 Hz حد معدل الإطارات في اللعبة.",
            ["Values over 100 can cause serious graphical issues when using Detective Vision, especially when using non-integer values such as 150."] =
                "قد تسبب القيم الأعلى من 100 مشكلات رسومية خطيرة عند استخدام Detective Vision، وخصوصا القيم غير الصحيحة مثل 150.",
            ["Values over 100% will only upscale the HUD and downscale the rest. Use FSR as an alternative."] =
                "تؤدي القيم الأعلى من 100% إلى رفع دقة HUD فقط وخفض دقة بقية الصورة. استخدم FSR بديلا.",
            ["Very minor performance increase. Prevents car reward screens from dropping to near zero FPS."] =
                "تحسن طفيف جدا في الأداء. يمنع انخفاض شاشات مكافآت السيارات إلى ما يقارب صفر FPS.",
            ["Vulkan causes certain 2D graphics to display incorrectly, OpenGL impacts performance but fixes it."] =
                "يسبب Vulkan عرض بعض رسوم 2D بصورة غير صحيحة. يؤثر OpenGL في الأداء لكنه يصلحها.",
            ["Vulkan works until you reach Chapter 2. Once in Chapter 2 the game will randomly hang unless you use OpenGL."] =
                "يعمل Vulkan حتى الوصول إلى Chapter 2. بعد دخول Chapter 2 تعلق اللعبة عشوائيا ما لم تستخدم OpenGL.",
            ["When enabled, outlines seen on characters and objects under shadows and water will be visible. Additionally, buildings in the distance are not rendered correctly."] =
                "عند تفعيله تظهر حدود حول الشخصيات والعناصر الواقعة تحت الظلال وفي الماء، كما لا تعرض المباني البعيدة بصورة صحيحة.",
            ["When enabled, outlines seen on characters and objects under shadows will be visible. Additionally, buildings in the distance are not rendered correctly."] =
                "عند تفعيله تظهر حدود حول الشخصيات والعناصر الواقعة تحت الظلال، كما لا تعرض المباني البعيدة بصورة صحيحة.",
            ["When using Resolution scaling above 100%, this must be set to 160x160. Otherwise bloom will appear pixelated."] =
                "عند استخدام Resolution Scale فوق 100% يجب ضبط هذه القيمة على 160x160، وإلا سيظهر Bloom متكسر البكسلات.",
            ["When using Resolution scaling above 100%, this must be set to 512x512. Otherwise bloom will appear pixelated."] =
                "عند استخدام Resolution Scale فوق 100% يجب ضبط هذه القيمة على 512x512، وإلا سيظهر Bloom متكسر البكسلات.",
            ["Without disabling some text is missing"] =
                "من دون تعطيله تختفي بعض النصوص.",
            ["Write color buffers and higher resolution scale cause vertex explosions. Especially noticeable during Thunder run mission."] =
                "يسبب Write Color Buffers مع رفع Resolution Scale انفجار Vertex، ويظهر ذلك بوضوح في مهمة Thunder Run.",
            // Notes on the Core / Libraries Control rows shipped from the 2026-09-07 snapshot.
            ["Fixes blackscreen on boot"] =
                "يصلح الشاشة السوداء عند التشغيل.",
            ["Fixes crash on intro"] =
                "يمنع التعطل عند المقدمة.",
            ["Fixes cutscene videos"] =
                "يصلح مقاطع المشاهد السينمائية.",
            ["Fixes missing logos intro"] =
                "يصلح اختفاء شعارات المقدمة.",
            ["Fixes some videos that appear distorted."] =
                "يصلح بعض مقاطع الفيديو التي تظهر مشوهة.",
            ["Fixes Videos not playing"] =
                "يصلح عدم تشغيل مقاطع الفيديو.",
            ["Needed to get ingame"] =
                "مطلوب للوصول إلى اللعب الفعلي.",
            ["Needs to be enabled to fix the freezing at the EA logo screen when you boot the game."] =
                "يجب تفعيله لإصلاح التجمد عند شاشة شعار EA أثناء تشغيل اللعبة.",
            ["Needs to be enabled to fix the freezing at the EA Sports logo screen when you boot the game."] =
                "يجب تفعيله لإصلاح التجمد عند شاشة شعار EA Sports أثناء تشغيل اللعبة.",
            ["Needs to be enabled to stop freezing at EA Sports logo screen"] =
                "يجب تفعيله لإيقاف التجمد عند شاشة شعار EA Sports.",
            ["Needs to be LLE'd to fix cutscene crash."] =
                "يجب تحميله بوضع LLE لإصلاح تعطل المشهد السينمائي.",
            ["Needs to be switched to LLE to stop looping through intro screens"] =
                "يجب تبديله إلى LLE لإيقاف التكرار المستمر لشاشات المقدمة.",
            ["Required for videos to play."] =
                "مطلوب لتشغيل مقاطع الفيديو.",
            ["Required to fix an error about unsupported interlaced video"] =
                "مطلوب لإصلاح خطأ فيديو interlaced غير المدعوم.",
            ["Required to fix black videos"] =
                "مطلوب لإصلاح مقاطع الفيديو السوداء.",
            ["Required to fix cutscene that plays on title screen in Chain of Memories freezing at last frame."] =
                "مطلوب لإصلاح تجمد المشهد السينمائي في شاشة العنوان في Chain of Memories عند الإطار الأخير.",
            ["Required to improve stability"] =
                "مطلوب لتحسين الاستقرار.",
            ["Required to prevent a crash during the credits."] =
                "مطلوب لمنع التعطل أثناء عرض أسماء فريق العمل.",
            ["Required to prevent the game from freezing in menus. <i>RPCS3 issue #12406</i>"] =
                "مطلوب لمنع تجمد اللعبة في القوائم. <i>مشكلة RPCS3 رقم #12406</i>.",
            ["Required to properly play FMV cutscenes"] =
                "مطلوب لتشغيل مشاهد FMV بشكل صحيح.",
            ["Required to properly play some videos"] =
                "مطلوب لتشغيل بعض مقاطع الفيديو بشكل صحيح.",
            ["This game will freeze at the EA logo if it's not loaded - RPCS3 issue #14192"] =
                "ستتجمد اللعبة عند شعار EA إذا لم يتم تحميلها. مشكلة RPCS3 رقم #14192.",
            ["\"Allow Host GPU Labels\" must be enabled in conjunction with the ASMJIT Recompiler in order to eliminate lockups when changing party formation"] =
                "يجب تفعيل «Allow Host GPU Labels» مع ASMJIT Recompiler لإزالة التجمد عند تغيير تشكيلة الفريق (party formation).",
            ["\"Ghosting\" around the player character model"] =
                "«Ghosting» (ظلال/أشباح) حول نموذج شخصية اللاعب.",
            ["\"Remastered\" version introduced a lot of bugs and glitches that were never fixed by Rockstar. These videos [https://youtu.be/jhVdb4K1M-M part 1] and [https://youtu.be/TTxY_S0XD8E part 2] show how bad the remastered port is"] =
                "إصدار «Remastered» أدخل الكثير من الأخطاء والعيوب التي لم تصلحها Rockstar أبدا. يوضح هذان الفيديوان [https://youtu.be/jhVdb4K1M-M part 1] و[https://youtu.be/TTxY_S0XD8E part 2] مدى سوء النسخة المنقولة Remastered",
            ["# If you want an extra bit of performance, depending on your CPU, setting Preferred SPU Threads to any value from 2 to 4 may help. If you don't see any difference set this back to auto"] =
                "# إذا أردت قدرا إضافيا من الأداء، بحسب المعالج (CPU) لديك، فقد يساعد ضبط Preferred SPU Threads على أي قيمة من 2 إلى 4. إذا لم تلاحظ أي فرق فأعده إلى auto.",
            ["# Make sure you have already downloaded the edit songs via the in-game interface on your PlayStation 3. Make sure you have the music files. You should have already tested that the songs work. # Make sure you have saved your save data after the songs are downloaded. # Exit the game. # Export all save data onto an USB stick using Saved Data Utility (PS3), this is located where you press up in the game section of the PS3 interface. # If the region title ID is different from the PS3 game and the game in RPCS3, you need to convert the save files: ## Place all save datas in .../rpcs3/dev_hdd0/home/00000001/savedata/ ## Use [https://bucanero.github.io/apollo-ps3/ Apollo Save Tool] to change the Region Title ID of all the save datas (make sure to apply for every one). Then exit and start Apollo again, then Decrypt all save datas. Copy all the decryped save datas into the original save data directories, overwriting SECURE.BIN. # Copy all music files to the music directory (.../rpcs3/dev_hdd0/music/) # Start up the game, it should load your new save data. If it gives an error, make sure you executed the steps correctly. # Go to Edit Play and it should show all your edit songs. When starting the song, it should show the music files in the MP3 tab, and it should be able to play them"] =
                "# تأكد من أنك نزلت أغاني التعديل (edit songs) مسبقا عبر واجهة اللعبة على PlayStation 3. تأكد من وجود ملفات الموسيقى. يجب أن تكون قد اختبرت عمل الأغاني. # تأكد من أنك حفظت بيانات الحفظ بعد تنزيل الأغاني. # اخرج من اللعبة. # صدر كل بيانات الحفظ إلى USB باستخدام Saved Data Utility (PS3)، وهو موجود حيث تضغط للأعلى في قسم الألعاب في واجهة PS3. # إذا كان Title ID الخاص بالمنطقة مختلفا بين لعبة PS3 واللعبة في RPCS3، فعليك تحويل ملفات الحفظ: ## ضع كل بيانات الحفظ في .../rpcs3/dev_hdd0/home/00000001/savedata/ ## استخدم [https://bucanero.github.io/apollo-ps3/ Apollo Save Tool] لتغيير Region Title ID لكل بيانات الحفظ (تأكد من تطبيقه على كل واحدة). ثم اخرج وشغل Apollo مرة أخرى، ثم فك تشفير (Decrypt) كل بيانات الحفظ. انسخ كل بيانات الحفظ المفكوكة التشفير إلى مجلدات بيانات الحفظ الأصلية، مع استبدال SECURE.BIN. # انسخ كل ملفات الموسيقى إلى مجلد الموسيقى (.../rpcs3/dev_hdd0/music/) # شغل اللعبة، ويفترض أن تحمل بيانات الحفظ الجديدة. إذا ظهر خطأ، فتأكد من أنك نفذت الخطوات بشكل صحيح. # اذهب إلى Edit Play ويفترض أن يعرض كل أغاني التعديل لديك. عند بدء الأغنية يفترض أن يعرض ملفات الموسيقى في تبويب MP3، ويفترض أن يكون قادرا على تشغيلها",
            ["* RPCS3 issue #12514: Black lines appear across the title screen and game menus in RPCS3 versions after v0.0.21-13399"] =
                "* مشكلة RPCS3 رقم #12514: تظهر خطوط سوداء عبر شاشة العنوان وقوائم اللعبة في إصدارات RPCS3 التي تلي v0.0.21-13399.",
            ["* RPCS3 issue #14001: Wrong colors are shown compared to console (mainly blue instead of red and yellow)"] =
                "* مشكلة RPCS3 رقم #14001: تظهر ألوان خاطئة مقارنة بالجهاز الحقيقي (أزرق في الغالب بدلا من الأحمر والأصفر).",
            ["*RPCS3 issue #11324: Incorrect colors appear both in menus and in-game"] =
                "*مشكلة RPCS3 رقم #11324: تظهر ألوان غير صحيحة في القوائم وداخل اللعبة على حد سواء.",
            ["- Katamari is invisible on file select screen. - The sprite representing the completed Katamari on the results screen is invisible. - Characters lag on the mode select screen. - Cannot change to other cousins via tagging on the mode select screen. (Can still change cousins in the Cousins Mode in Collection) - Music will restart over and over during levels RPCS3 issue #8963"] =
                "- لا تظهر كرة Katamari في شاشة اختيار الملف. - تختفي صورة Katamari المكتملة من شاشة النتائج. - تتأخر حركة الشخصيات في شاشة اختيار الوضع. - لا يمكن تغيير الشخصيات عبر Tagging في شاشة اختيار الوضع (يمكن تغييرها من Cousins Mode داخل Collection). - قد تتكرر الموسيقى أثناء المراحل. مشكلة RPCS3 رقم #8963.",
            ["0x68005874: - Defense points - Signed 32 bits - \"\""] =
                "0x68005874: - نقاط الدفاع - عدد صحيح بإشارة، 32 بت - \"\"",
            ["0x68005878: - Athletic points - Signed 32 bits - \"\""] =
                "0x68005878: - نقاط اللياقة - عدد صحيح بإشارة، 32 بت - \"\"",
            ["10GB GPU = Don't go over 175% scaling"] =
                "GPU بسعة 10GB = لا تتجاوز 175% scaling.",
            ["10GB GPU = Don't go over 275% scaling"] =
                "GPU بسعة 10GB = لا تتجاوز 275% scaling.",
            ["11GB GPU = Don't go over 200% scaling"] =
                "GPU بسعة 11GB = لا تتجاوز 200% scaling.",
            ["11GB GPU = Don't go over 300% scaling"] =
                "GPU بسعة 11GB = لا تتجاوز 300% scaling.",
            ["185.194.142.4 Save Confrontation discord. https://discord.gg/CCKUy4ZS3J Category:Custom servers (Netplay)"] =
                "185.194.142.4 Discord الخاص بمجتمع Save Confrontation. https://discord.gg/CCKUy4ZS3J الفئة: خوادم مخصصة (اللعب عبر الشبكة)",
            ["185.194.142.4 [https://discord.com/invite/puHGNZr PSONE Discord] Category:Custom servers (Netplay)"] =
                "185.194.142.4 [https://discord.com/invite/puHGNZr PSONE Discord] الفئة: خوادم مخصصة (اللعب عبر الشبكة)",
            ["192.236.168.216 Category:Custom servers (Netplay)"] =
                "192.236.168.216 الفئة: خوادم مخصصة (اللعب عبر الشبكة)",
            ["4GB GPU = Don't go over 100% scaling"] =
                "GPU بسعة 4GB = لا تتجاوز 100% scaling.",
            ["4GB GPU = Don't go over 175% scaling"] =
                "GPU بسعة 4GB = لا تتجاوز 175% scaling.",
            ["5GB GPU = Don't go over 125% scaling"] =
                "GPU بسعة 5GB = لا تتجاوز 125% scaling.",
            ["5GB GPU = Don't go over 200% scaling"] =
                "GPU بسعة 5GB = لا تتجاوز 200% scaling.",
            ["6GB GPU = Don't go over 125% scaling"] =
                "GPU بسعة 6GB = لا تتجاوز 125% scaling.",
            ["6GB GPU = Don't go over 225% scaling"] =
                "GPU بسعة 6GB = لا تتجاوز 225% scaling.",
            ["8GB GPU = Don't go over 150% scaling"] =
                "GPU بسعة 8GB = لا تتجاوز 150% scaling.",
            ["8GB GPU = Don't go over 250% scaling"] =
                "GPU بسعة 8GB = لا تتجاوز 250% scaling.",
            ["8GB+ GPU = 200% Scaling [Users with GPUs of greater VRAM capacity are encouraged to investigate and contribute to this section.]"] =
                "GPU بسعة 8GB+ = 200% Scaling [يشجع المستخدمون الذين لديهم GPU بسعة VRAM أكبر على البحث والمساهمة في هذا القسم.]",
            ["A 10% discount is offered on the Starship Pheonix vendor. You need a RaC1 save with the Hologuise unlocked, which can be done after completing the Hover Board race on Kalebo III. Note that NG+ runs do not carry over the item as it is a story item that is unlocked at that point of the game"] =
                "يعرض خصم 10% عند بائع Starship Pheonix. تحتاج إلى حفظ RaC1 مع فتح Hologuise، ويمكن ذلك بعد إكمال سباق Hover Board في Kalebo III. لاحظ أن تشغيلات NG+ لا تنقل العنصر لأنه عنصر قصة (story item) يفتح عند تلك النقطة من اللعبة",
            ["A Resolution Scale Threshold of 240x240 is required to scale bloom effects properly with Resolution Scale. Unfortunately, this setting will also cause the minimap to downsize"] =
                "لإظهار توهج الإضاءة بصورة صحيحة عند رفع الدقة، اضبط Resolution Scale Threshold على 240x240. لكن ذلك سيصغر الخريطة المصغرة.",
            ["A few textures appear blue when not in close proximity"] =
                "تظهر بعض الخامات (textures) باللون الأزرق عندما لا تكون قريبة.",
            ["A lot of models use wrong textures or are simply pitch black. Using settings above can fix those issues"] =
                "كثير من النماذج تستخدم خامات خاطئة أو تكون سوداء تماما. استخدام الإعدادات أعلاه يمكن أن يصلح هذه المشاكل.",
            ["A minor graphical bug which is mainly visible in the gunge game mode, but can be spotted throughout other parts of the game"] =
                "خلل رسومي بسيط يظهر بشكل رئيسي في نمط لعب gunge، لكن يمكن ملاحظته في أجزاء أخرى من اللعبة.",
            ["A myriad of visual issues are present, as is the case will all Frostbite 2/3 engine games on the emulator"] =
                "توجد مجموعة كبيرة من المشاكل البصرية، كما هو الحال مع جميع ألعاب محرك Frostbite 2/3 على المحاكي.",
            ["A patch that addresses texture problems and improves the overall texture quality can be downloaded here: https://raymanpc.com/forum/viewtopic.php?f=89&t=28443 Note that this patch is a pkg installer and actually replaces game files so won't be added to RPCS3's Patch Manager"] =
                "يمكن تنزيل تعديل يعالج مشاكل الخامات ويحسن جودتها عموما من هنا: https://raymanpc.com/forum/viewtopic.php?f=89&t=28443 لاحظ أن هذا التعديل مثبت pkg ويستبدل ملفات اللعبة فعليا، لذلك لن يضاف إلى Patch Manager في RPCS3",
            ["A specific room during a story mission will show incorrect lighting, looking like a cartoon world"] =
                "ستظهر غرفة معينة أثناء مهمة قصة بإضاءة غير صحيحة، وتبدو كعالم كرتوني.",
            ["According to kd-11, the game does not send draw commands to the RSX (PS3 GPU); \"It appears to be a synchronization bug in lv2 kernel semaphores. The game has two threads to manage which objects are on screen and presumably tag them to be rendered by the render thread, but they are not running and are stuck in an infinite loop\""] =
                "بحسب kd-11، لا ترسل اللعبة أوامر الرسم (draw commands) إلى RSX (GPU الخاص بـ PS3); \"It appears to be a synchronization bug in lv2 kernel semaphores. The game has two threads to manage which objects are on screen and presumably tag them to be rendered by the render thread, but they are not running and are stuck in an infinite loop\"",
            ["Activating the Debug Menu"] =
                "تفعيل Debug Menu.",
            ["Afro"] =
                "تسريحة أفرو (Afro)",
            ["After RPCS3 PR #10917 was merged, the game no longer boots without SPU interpreter set to static to prevent a crash relating to unimplemented SPU interrupts. This setting causes the game to run very poorly"] =
                "بعد دمج طلب دمج RPCS3 رقم #10917، لم تعد اللعبة تقلع دون ضبط SPU interpreter على static لمنع انهيار متعلق بمقاطعات SPU غير المنفذة. هذا الإعداد يجعل اللعبة تعمل بشكل سيئ جدا.",
            ["After a recent update to the emulator, players will disappear after the ball is snapped and then a review of the play will happen and it will be reversed. Then there is weird glitching on the players and the o-line's helmet's come off. This happens every time the ball is snapped. It may work to restart your game and also check to see if RPCS3 is up-to-date"] =
                "بعد تحديث حديث للمحاكي، يختفي اللاعبون بعد تمرير الكرة (snap) ثم تجري مراجعة للحركة فتعكس. بعد ذلك تظهر أخطاء غريبة على اللاعبين وتنخلع خوذات خط الهجوم (o-line). يحدث هذا في كل مرة تمرر فيها الكرة. قد تفيد إعادة تشغيل اللعبة، وتحقق أيضا من أن RPCS3 محدث",
            ["After completing the game, the credits start to roll; when they are finally over, or if you decide to skip them, the game will freeze and RPCS3 will crash"] =
                "بعد إنهاء اللعبة تبدأ أسماء فريق العمل (credits) في العرض؛ وعند انتهائها أخيرا، أو إذا قررت تخطيها، ستتجمد اللعبة وسينهار RPCS3.",
            ["After defeating Radec and heading to the room where Visari is at, the game will freeze before the cutscene can fully begin. This only occurs on PPU LLVM Recompilier, setting it to Interpreter Static prevents this freeze from occurring. You can also skip the cutscene before the freeze"] =
                "بعد هزيمة Radec والتوجه إلى الغرفة التي فيها Visari، ستتجمد اللعبة قبل أن يبدأ المشهد السينمائي بالكامل. يحدث هذا فقط مع PPU LLVM Recompilier، وضبطه على Interpreter Static يمنع حدوث هذا التجمد. يمكنك أيضا تخطي المشهد السينمائي قبل التجمد",
            ["After installing the 1.02 update, game will get stuck on a black screen"] =
                "بعد تثبيت تحديث 1.02، ستعلق اللعبة على شاشة سوداء.",
            ["After some time, a desync occurs which causes the emulated CELL to send garbage data to the RSX. This causes an inconsistency and crashes the game. Using Driver Wakeup Delay can help delay the time it takes to crash, but not preventing it all together"] =
                "بعد مدة يحدث عدم تزامن (desync) يجعل CELL المحاكى يرسل بيانات عشوائية إلى RSX، مما يسبب عدم اتساق وانهيار اللعبة. استخدام Driver Wakeup Delay يمكن أن يؤخر وقت الانهيار لكنه لا يمنعه تماما",
            ["After the credits roll, the game will continuously show a black screen until the game is closed, thus having no Battle Record and no Finished game trophy/trophies. Enable LLE libvdec.sprx to fix it"] =
                "بعد عرض أسماء فريق العمل، ستظهر شاشة سوداء باستمرار حتى تغلق اللعبة، وبالتالي لا يوجد Battle Record ولا كأس/كؤوس إنهاء اللعبة (Finished game trophy/trophies). فعل LLE libvdec.sprx لإصلاح ذلك.",
            ["After the first match in a session of Captain Your Country or FIFA World Cup Tournament mode the HUD will fail to render beginning in the practice arena right before the start of a match, which reoccurs every other match afterwards. Another side effect of this rendering bug is that if a penalty is awarded, the emulation will crash. The only known temporary solution at the moment is to exit out of the respective mode you are running and reenter it, resetting the HUD"] =
                "بعد أول مباراة في جلسة وضع Captain Your Country أو FIFA World Cup Tournament، سيفشل عرض HUD بدءا من ساحة التدريب قبل بداية المباراة مباشرة، ويتكرر ذلك كل مباراة ثانية بعدها. من الآثار الجانبية الأخرى لهذا الخلل في العرض أنه إذا منحت ضربة جزاء فسينهار المحاكاة. الحل المؤقت الوحيد المعروف حاليا هو الخروج من الوضع الذي تلعبه وإعادة دخوله، مما يعيد ضبط HUD",
            ["After the game is finished installing data to the HDD, the game will hang and be unable to progress to the next screen. The emulator is unable to be closed normally after this point"] =
                "بعد انتهاء اللعبة من تثبيت البيانات على القرص الصلب HDD، تتجمد اللعبة ولا تستطيع الانتقال إلى الشاشة التالية. ولا يمكن إغلاق المحاكي بشكل طبيعي بعد هذه النقطة.",
            ["After the game is launched, the initial splash screen pops up and the game gets stuck loading infinitely. While stuck, it will spam the log file thousands of times a second with the same warning entry"] =
                "بعد تشغيل اللعبة تظهر شاشة البداية الأولية وتعلق اللعبة في تحميل لا نهائي. وأثناء ذلك تملأ ملف السجل (log) آلاف المرات في الثانية بنفس إدخال التحذير.",
            ["After the intro you may be greeted with a message asking you to reconnect your controller to continue, there's no known fix for this. However, you can try this workaround: after booting the game, minimise the game window and open pad settings in RPCS3, then select on your controller under the \"Handlers\" dropdown (even if it's already been selected). Then hit save and close the pad settings window and switch back to the game window. Input should start working again after this"] =
                "بعد المقدمة (intro) قد تظهر لك رسالة تطلب إعادة توصيل يد التحكم للمتابعة، ولا يوجد حل معروف لذلك. لكن يمكنك تجربة هذا الحل البديل: بعد تشغيل اللعبة، صغر نافذة اللعبة وافتح إعدادات pad في RPCS3، ثم اختر يد التحكم لديك من القائمة المنسدلة «Handlers» (حتى لو كانت مختارة مسبقا). ثم اضغط حفظ وأغلق نافذة إعدادات pad وعد إلى نافذة اللعبة. يفترض أن يعمل الإدخال مجددا بعد ذلك",
            ["After the point-of-no-return at the final dungeon of the game, the game will freeze and crash during the cutscenes when using the Vulkan renderer. In order to bypass this, users are required to either skip the cutscenes or play the final sequence with the OpenGL renderer. This issue is tracked as part of RPCS3 issue #6468 (Warning, contains major spoilers regarding the final boss battle)"] =
                "بعد نقطة اللاعودة في الزنزانة الأخيرة من اللعبة، ستتجمد اللعبة وتنهار أثناء المشاهد السينمائية عند استخدام Vulkan renderer. لتجاوز ذلك، على المستخدمين إما تخطي المشاهد السينمائية أو لعب التسلسل الأخير باستخدام OpenGL renderer. تتتبع هذه المشكلة ضمن مشكلة RPCS3 رقم #6468 (تحذير: تحتوي على حرق كبير لأحداث معركة الزعيم الأخير)",
            ["After updating the game, it won't launch at all same happens with other FIFA games"] =
                "بعد تحديث اللعبة لا تعمل إطلاقا، ويحدث الأمر نفسه مع ألعاب FIFA الأخرى.",
            ["All Elemental Upgrades: Hold L1 and press left, up, down, up, right"] =
                "All Elemental Upgrades: اضغط مع الاستمرار على L1 ثم اضغط يسار، أعلى، أسفل، أعلى، يمين.",
            ["All three Jak games have a hidden debug mode that can be activated using a two-part button combination. [https://jadtech.miraheze.org/wiki/Cheat_Mode Here is how to enable it, while also listing its debug features.]"] =
                "تحتوي ألعاب Jak الثلاث على وضع تصحيح (debug mode) مخفي يمكن تفعيله باستخدام تركيبة أزرار من جزأين. [https://jadtech.miraheze.org/wiki/Cheat_Mode Here is how to enable it, while also listing its debug features.]",
            ["All three games suffer from high amounts of RSX drawcalls, which causes heavy slowdown. RaC1 has less slowdown, but RaC2 and RaC3 have the most slowdown, making both games unplayable at times"] =
                "تعاني الألعاب الثلاث من كمية كبيرة من RSX drawcalls مما يسبب بطئا شديدا. RaC1 فيها بطء أقل، لكن RaC2 وRaC3 فيهما أكبر قدر من البطء، مما يجعل اللعبتين غير قابلتين للعب أحيانا.",
            ["Allows access to the Network menu required to edit user's profile to unlock special events. [https://discord.com/channels/272035812277878785/319224795785068545/1380919445001867384 Log file]"] =
                "يتيح الوصول إلى قائمة Network المطلوبة لتعديل ملف المستخدم الشخصي لفتح الأحداث الخاصة. [https://discord.com/channels/272035812277878785/319224795785068545/1380919445001867384 Log file]",
            ["Analog Triggers : This game uses analog triggers for it flight controls. Not sure if game technically playable without them as you required to hold trigger at 50% in order to complete the tutorial"] =
                "Analog Triggers: تستخدم هذه اللعبة الأزرار الزنادية التناظرية (analog triggers) لتحكمات الطيران. لست متأكدا من أن اللعبة قابلة للعب تقنيا بدونها، إذ يجب أن تبقي الزناد عند 50% لإكمال الشرح التعليمي (tutorial)",
            ["Applies to this game's DLC, Clash in the Clouds. Leaderboards now work, but currently very empty"] =
                "ينطبق على المحتوى الإضافي (DLC) لهذه اللعبة، Clash in the Clouds. تعمل لوحات الصدارة الآن، لكنها حاليا فارغة جدا.",
            ["Apply the included patch to connect and cross-play with PC players using [https://mordorwi.de MordorWide] community servers"] =
                "طبق التعديل المرفق للاتصال واللعب المشترك (cross-play) مع لاعبي PC باستخدام خوادم مجتمع [https://mordorwi.de MordorWide]",
            ["Arcadia is a third-party revival for PS3 EA games that is currently attempting to revive the online functionality of TF2. As of the 1st of December 2025, you can get up to the main menu and view your stats"] =
                "Arcadia هو إحياء من طرف ثالث لألعاب EA على PS3، ويحاول حاليا إحياء وظائف الإنترنت في TF2. اعتبارا من 1 ديسمبر 2025، يمكنك الوصول إلى القائمة الرئيسية وعرض إحصائياتك.",
            ["Army Helmet /w Long Hair"] =
                "خوذة عسكرية مع شعر طويل (Army Helmet /w Long Hair)",
            ["Army Helmet /w Short Hair"] =
                "خوذة عسكرية مع شعر قصير (Army Helmet /w Short Hair)",
            ["As RPCN menus pause emulation, this means instruments set up via MIDI and passthrough instruments end up softlocking and require having an alternative input method mapped. [https://github.com/RPCS3/rpcs3/issues/15628/ Track the issue here.]"] =
                "عند فتح قوائم RPCN تتوقف المحاكاة مؤقتا؛ لذلك قد تتعطل الآلات المتصلة عبر MIDI أو Passthrough دون إمكانية متابعة اللعب. عين وسيلة إدخال بديلة. [https://github.com/RPCS3/rpcs3/issues/15628/ متابعة المشكلة]",
            ["As a workaround for save management, it is recommended to export/import profiles (start > settings > profile settings > import/export profile), that can be read from the Save Data Utility folder (dev_hdd0/home//savedata)"] =
                "كحل بديل لإدارة الحفظ، ينصح بتصدير/استيراد الملفات الشخصية (start > settings > profile settings > import/export profile)، ويمكن قراءتها من مجلد Save Data Utility (dev_hdd0/home//savedata).",
            ["Assassin's Creed has a built-in frame limiter that will limit the game to either 20 or 30 FPS is the target 60 cannot be achieved. Setting your vBlank Frequency to 3x your screen's refresh rate will effectively disable these limiters, and unlock the game's framerate"] =
                "تحتوي Assassin's Creed على محدد إطارات مدمج يحد اللعبة إلى 20 أو 30 FPS إذا تعذر تحقيق 60 المستهدفة. ضبط vBlank Frequency على 3 أضعاف معدل تحديث شاشتك سيعطل هذه المحددات فعليا ويفتح قفل معدل إطارات اللعبة",
            ["Asynchronous texture streaming does not work on macOS"] =
                "Asynchronous texture streaming لا يعمل على macOS.",
            ["At random, the graphics turn red, sometimes having some other artifacts on the side of the screen. The game also tends to freeze at random with no current way to prevent it"] =
                "بشكل عشوائي، تتحول الرسومات إلى اللون الأحمر، وأحيانا تظهر عيوب أخرى على جانب الشاشة. كما تميل اللعبة إلى التجمد عشوائيا دون وجود طريقة حاليا لمنع ذلك.",
            ["At this time, reflections are broken when the player is close to bodies of water"] =
                "في الوقت الحالي، تكون الانعكاسات معطوبة عندما يكون اللاعب قريبا من المسطحات المائية.",
            ["Attempting to load saves created as Desmond will crash the game at the start screen. Avoid closing the game as Desmond for now"] =
                "محاولة تحميل حفظات أنشئت بشخصية Desmond ستؤدي إلى انهيار اللعبة عند شاشة البداية. تجنب إغلاق اللعبة وأنت بشخصية Desmond في الوقت الحالي.",
            ["Attempting to use quick match or find match results in a crash, but invites to lobbies function fine. Ranked matches, watch replay with friends online and leaderboards also work. Tournaments untested as of now. Cannot upload replays"] =
                "تؤدي خيارات Quick Match وFind Match إلى تعطل اللعبة، بينما تعمل دعوات غرف الانتظار. تعمل المباريات المصنفة ومشاهدة الإعادات مع الأصدقاء ولوحات الصدارة. لم تختبر البطولات، ولا يمكن رفع الإعادات.",
            ["Audio heavily stutters in most parts of the game no matter the settings. Can only be fixed with a high-end CPU"] =
                "الصوت يتقطع بشدة في معظم أجزاء اللعبة مهما كانت الإعدادات. لا يمكن إصلاحه إلا بـ CPU عالي الأداء.",
            ["Audio sometimes stops working"] =
                "يتوقف الصوت عن العمل أحيانا.",
            ["Aviator Cap"] =
                "قبعة طيار (Aviator Cap)",
            ["Bad performance as PPU Interpreter needs to be used to get in-game"] =
                "أداء سيئ لأن PPU Interpreter يجب استخدامه للدخول إلى اللعبة.",
            ["Battlefield: Bad Company uses a default infantry field of view of 36.85000229. Aim down sights uses 16.75. Cheat Engine may be used to alter this field for infantry, aim down sights and vehicles. Alternatively, a patched set of level files is available at [https://www.nexusmods.com/battlefieldbadcompany/mods/3 ModDB]"] =
                "تستخدم Battlefield: Bad Company مجال رؤية افتراضي للمشاة قدره 36.85000229. ويستخدم التصويب عبر المنظار 16.75. يمكن استخدام Cheat Engine لتغيير هذا المجال للمشاة والتصويب عبر المنظار والمركبات. وبدلا من ذلك، تتوفر مجموعة ملفات مراحل معدلة على [https://www.nexusmods.com/battlefieldbadcompany/mods/3 ModDB]",
            ["Be sure to enable the following Canary patches: Crash fix (addresses crashes related to error code 0x68). Canary patches like these need to be added manually then enabled via RPCS3's patch manager. You'll find instructions on how to apply canary patches here. There's also a canary patch Flickering textures fix, consider enabling this too if you're experiencing this issue. Though it should be noted that this particular patch is known to cause crashes for some users, if you're experiencing them avoid using it"] =
                "احرص على تفعيل تعديلات Canary التالية: Crash fix (يعالج الأعطال المرتبطة برمز الخطأ 0x68). تعديلات Canary من هذا النوع تحتاج إلى إضافتها يدويا ثم تفعيلها عبر مدير التعديلات في RPCS3. ستجد تعليمات تطبيق تعديلات canary هنا. يوجد أيضا تعديل canary باسم Flickering textures fix، فكر في تفعيله إذا كنت تعاني من هذه المشكلة. ومع ذلك يجدر التنبيه إلى أن هذا التعديل بالذات معروف بأنه يسبب أعطالا لدى بعض المستخدمين، فإذا واجهتها فتجنب استخدامه",
            ["Biker Cap"] =
                "قبعة راكب دراجة (Biker Cap)",
            ["Biker Cap /w Long Hair"] =
                "قبعة راكب دراجة مع شعر طويل (Biker Cap /w Long Hair)",
            ["Bloom effects are currently broken with no current way to fix it"] =
                "تأثيرات توهج الإضاءة (Bloom) معطلة حاليا ولا توجد طريقة حالية لإصلاحها.",
            ["Boss cutscenes can start the fight while it plays"] =
                "مشاهد الزعماء السينمائية قد تبدأ القتال أثناء عرضها.",
            ["Both Tales of Symphonia and Tales of Symphona: Dawn of the New World require the framerate to be capped at 30fps in GPU settings for the game to run at the proper speed. However with the cap, cinematics in Tales of Symphonia will not play correctly"] =
                "تتطلب كل من Tales of Symphonia وTales of Symphona: Dawn of the New World تحديد معدل الإطارات عند 30 FPS في إعدادات GPU لكي تعمل اللعبة بالسرعة الصحيحة. لكن مع هذا الحد، لن تعرض المشاهد السينمائية في Tales of Symphonia بشكل صحيح",
            ["Broken player input during whirlpool free fall"] =
                "تعطل إدخال اللاعب أثناء السقوط الحر في الدوامة.",
            ["Buzz Cut"] =
                "شعر قصير جدا (Buzz Cut)",
            ["By default options are disabled in demo, but can be enabled by holding L1 and L2 and inputting the following button sequence: Up, Down, Up, Down and pressing Start. The top menu will reload and afterwards will show the Options menu available. While in Options menu:"] =
                "افتراضيا تكون الخيارات معطلة في النسخة التجريبية، لكن يمكن تفعيلها بالضغط مع الاستمرار على L1 وL2 وإدخال تسلسل الأزرار التالي: Up, Down, Up, Down ثم الضغط على Start. ستعاد تحميل القائمة العلوية وبعدها ستظهر قائمة Options متاحة. أثناء التواجد في قائمة Options:",
            ["By default the game runs at 30 FPS, but the game will run at 60 FPS with no issues if you set the VBlank Frequency (under the Advanced tab in RPCS3's configuration) to 120 Hz. The game will not run above 60 FPS, so increasing above 120 Hz is unnecessary"] =
                "افتراضيا تعمل اللعبة بمعدل 30 FPS، لكنها ستعمل بمعدل 60 FPS دون مشاكل إذا ضبطت VBlank Frequency (ضمن تبويب Advanced في إعدادات RPCS3) على 120 Hz. لن تعمل اللعبة بأكثر من 60 FPS، لذا فرفعها فوق 120 Hz غير ضروري",
            ["By setting Frame Limiter to Off, the framerate will be unlocked"] =
                "بضبط Frame Limiter على Off سيتم فتح معدل الإطارات.",
            ["By setting Vblank Frequency to 120hz, you can run the game at 60FPS, however the building and hacking minigames may flicker"] =
                "بضبط Vblank Frequency على 120 Hz يمكنك تشغيل اللعبة بمعدل 60 FPS، لكن قد تومض ألعاب البناء والاختراق المصغرة.",
            ["By setting the Frame Limiter to Off you can unlock the framerate. However cutscenes will speed up"] =
                "بضبط Frame Limiter على Off يمكنك فتح معدل الإطارات. لكن المشاهد السينمائية ستتسارع.",
            ["By setting the vBlank Frequency rate to 120hz, you can run the game at 60FPS. Rag dolls and some game physics may be altered than intended. Do not exceed above 60FPS as game speed will increase"] =
                "اضبط VBlank Frequency على 120 Hz لتشغيل اللعبة بمعدل 60 FPS. قد تتأثر حركة الأجسام الساقطة (Ragdoll Physics) وفيزياء اللعبة. لا تتجاوز 60 FPS حتى لا تتسارع اللعبة.",
            ["By setting the vBlank Frequency rate to 120hz, you can run the game at 60FPS. Rag dolls and some game physics may be altered than intended. Do not exceed above 60FPS as game speed will increase. If the framerate is too inconsistent, the camera may look jittery"] =
                "اضبط VBlank Frequency على 120 Hz للوصول إلى 60 FPS. قد تتأثر حركة الأجسام الساقطة (Ragdoll Physics) وفيزياء اللعبة. لا تتجاوز 60 FPS حتى لا تتسارع اللعبة. وقد تهتز الكاميرا إذا كان معدل الإطارات غير ثابت.",
            ["By setting the vBlank Rate to 120hz, you can run the game at 60FPS. Increase it further to fully unlock the framerate"] =
                "بضبط vBlank Rate على 120 Hz يمكنك تشغيل اللعبة بمعدل 60 FPS. ارفعه أكثر لفتح معدل الإطارات بالكامل.",
            ["CPUs with less than 4 cores and 8 threads can have its performance improved by setting \"Maximum Number of SPURS Threads\" to 3 in the Advanced tab"] =
                "يمكن تحسين أداء المعالجات (CPUs) التي لديها أقل من 4 أنوية و8 مسارات تنفيذ بضبط \"Maximum Number of SPURS Threads\" على 3 في تبويب Advanced.",
            ["CPUs with less than 8 cores and 16 threads can have its performance improved by setting \"Maximum Number of SPURS Threads\" to 3 in the Advanced tab. Can cause issues on MacOS and is not necessary for high end systems"] =
                "يمكن تحسين أداء المعالجات (CPUs) التي لديها أقل من 8 أنوية و16 خيطا بضبط \"Maximum Number of SPURS Threads\" على 3 في تبويب Advanced. قد يسبب مشاكل على MacOS وغير ضروري للأنظمة عالية الأداء.",
            ["Camera speed"] =
                "سرعة الكاميرا.",
            ["Can create public and private lobbies but can't connect to other players. Attempting to join an invite results in \"Failed to join the session.\""] =
                "يمكن إنشاء غرف عامة وخاصة لكن لا يمكن الاتصال بلاعبين آخرين. محاولة الانضمام إلى دعوة تنتج الرسالة \"Failed to join the session.\".",
            ["Can go into tutorial using arcadia. [https://github.com/valters-tomsons/arcadia Custom server WIP]"] =
                "يمكن الدخول إلى التدريب باستخدام arcadia. [https://github.com/valters-tomsons/arcadia Custom server WIP]",
            ["Certain (mostly transparent) things like particles, glass, etc are rendered at half resolution on console"] =
                "بعض العناصر (الشفافة في الغالب) مثل الجزيئات والزجاج وغيرها تعرض بنصف الدقة على الجهاز الأصلي.",
            ["Changing Vblank to 120Hz allows for 60 FPS, but menus and pinball speed will be doubled. Duke's walking speed will break if above 120 FPS"] =
                "تغيير Vblank إلى 120 Hz يتيح 60 FPS، لكن سرعة القوائم والبنبول ستتضاعف. وستتعطل سرعة مشي Duke إذا تجاوزت 120 FPS.",
            ["Changing Vblank to 120Hz allows for 60 FPS, going higher affects the gamespeed"] =
                "تغيير Vblank إلى 120 Hz يتيح 60 FPS، والرفع أكثر يؤثر على سرعة اللعبة.",
            ["Changing Vblank to 120Hz allows for 60 FPS, will affect cutscenes speed"] =
                "تغيير Vblank إلى 120 Hz يتيح 60 FPS، وسيؤثر على سرعة المشاهد السينمائية.",
            ["Changing Vblank to 120Hz allows for 60 FPS. Some elements of the game are bugged at higher framerates"] =
                "تغيير Vblank إلى 120 Hz يتيح 60 FPS. بعض عناصر اللعبة تحدث فيها أخطاء عند معدلات الإطارات الأعلى.",
            ["Changing the Frame Limiter from Auto to Infinite unlocks the framerate. The game, however, does not exceed 120FPS"] =
                "تغيير Frame Limiter من Auto إلى Infinite يفتح معدل الإطارات. لكن اللعبة لا تتجاوز 120 FPS.",
            ["Character textures appear to be either corrupted, missing, incorrectly colored, or not mapped correctly during gameplay"] =
                "تبدو خامات الشخصيات تالفة أو مفقودة أو بألوان خاطئة أو غير مطبقة بشكل صحيح أثناء اللعب.",
            ["Characters may appear pitch white when going in and out of the VR Rooms or VR Missions"] =
                "قد تظهر الشخصيات بيضاء تماما عند الدخول إلى غرف VR أو مهام VR والخروج منها.",
            ["Classic Mop"] =
                "شعر كثيف كلاسيكي (Classic Mop)",
            ["Co-op and Versus Ops work without issues. (Peace Walker)"] =
                "يعمل الوضعان Co-op وVersus Ops دون مشاكل. (Peace Walker).",
            ["Community features (Clan, Mailbox, Tournaments) are currently not implemented on either server, however statistics and trophies are available on PSRewired"] =
                "ميزات المجتمع (Clan وMailbox وTournaments) غير مطبقة حاليا على أي من الخادمين، لكن الإحصائيات والكؤوس (trophies) متاحة على PSRewired.",
            ["Compared to other platforms such as PC and Xbox, the game experiences graphical issues on PS3 and RPCS3. These issues appear as banding on some surfaces when moving close to them. This issue has been observed on Retomoto Tower after taking the briefcase, but it could also occur in other parts of the game. These videos show the difference:"] =
                "مقارنة بمنصات أخرى مثل PC وXbox، تعاني اللعبة من مشاكل رسومية على PS3 وRPCS3. تظهر هذه المشاكل على شكل خطوط (banding) على بعض الأسطح عند الاقتراب منها. لوحظت هذه المشكلة في Retomoto Tower بعد أخذ الحقيبة، لكنها قد تحدث أيضا في أجزاء أخرى من اللعبة. توضح هذه الفيديوهات الفرق:",
            ["Compared to other platforms such as PC and Xbox, the game experiences graphical issues when rendering dynamic shadows on PS3 and RPCS3. This issue is most noticeable at the start of the first mission of chapter four, Topside near the two large boulders next to the road when in the helicopter. These videos show the difference:"] =
                "مقارنة بمنصات أخرى مثل PC وXbox، تعاني اللعبة من مشاكل رسومية عند عرض الظلال الديناميكية على PS3 وRPCS3. تكون هذه المشكلة أوضح ما يكون في بداية المهمة الأولى من الفصل الرابع، Topside، قرب الصخرتين الكبيرتين بجانب الطريق عند التواجد في الهليكوبتر. توضح هذه الفيديوهات الفرق:",
            ["Completely random crashes at any time, but mostly during shader compilation or when many objects are on-screen. If the game runs longer than 15 minutes without crashing, it will continue to run for hours without this problem"] =
                "أعطال عشوائية تماما في أي وقت، لكن أغلبها أثناء تجميع المظللات (Shaders) أو عند وجود كثير من الأجسام على الشاشة. إذا عملت اللعبة أكثر من 15 دقيقة دون أعطال، فستستمر في العمل لساعات دون هذه المشكلة.",
            ["Connects fine. Needs testing"] =
                "يتصل بشكل جيد. يحتاج إلى اختبار.",
            ["Constant crashing on loading, while playing in a level, exiting a level , Customizing a character and opening chests"] =
                "انهيار متكرر عند التحميل، وأثناء اللعب داخل مرحلة، وعند الخروج من مرحلة، وعند تخصيص شخصية وفتح الصناديق.",
            ["Crashes are all but gone with default settings and the entire game can be played from start to finish in 1 sitting. Performance keeps this game from having a playable report at this time"] =
                "الأعطال شبه معدومة مع الإعدادات الافتراضية ويمكن لعب اللعبة كاملة من البداية إلى النهاية في جلسة واحدة. الأداء يمنع هذه اللعبة من الحصول على تقرير playable في الوقت الحالي.",
            ["Crashes when creating lobby"] =
                "تعطل عند إنشاء غرفة.",
            ["Currently as of right now, looping and shuffling songs is not implemented. Press R1 to cycle to the next available song"] =
                "حاليا، التكرار والتشغيل العشوائي للأغاني غير مطبقين. اضغط R1 للانتقال إلى الأغنية التالية المتاحة.",
            ["Currently game graphics is a mess making it unplayable"] =
                "حاليا رسوميات اللعبة فوضوية مما يجعلها غير قابلة للعب.",
            ["Currently only the PS3 version of the board is supported"] =
                "لا يدعم المحاكي حاليا سوى إصدار PS3 من لوح التزلج الإلكتروني المستخدم مع اللعبة.",
            ["Currently the game doesn't work on official RPCS3 builds but can be played on a [https://mgo2pc.com/ custom build]"] =
                "حاليا لا تعمل اللعبة على إصدارات RPCS3 الرسمية لكن يمكن لعبها على [https://mgo2pc.com/ نسخة مخصصة]",
            ["Currently, the game has various transparent objects that are rendered incorrectly. This is notably seen on Score/Prize Bubbles and the Pod windows"] =
                "حاليا، تحتوي اللعبة على أجسام شفافة متعددة تعرض بشكل غير صحيح. يظهر هذا بوضوح في فقاعات Score/Prize ونوافذ Pod.",
            ["Custom icon artwork provided by soundreaction. Can be switched with original \"ICON0.PNG\" inside the game's \"PS3_GAME\" directory (It is recommended to retain the original art for safety). This replaces the original game icon artwork with the movie's logo"] =
                "رسمة أيقونة مخصصة من soundreaction. يمكن استبدالها بالأيقونة الأصلية \"ICON0.PNG\" داخل مجلد \"PS3_GAME\" الخاص باللعبة (ينصح بالاحتفاظ بالرسمة الأصلية للأمان). هذا يستبدل رسمة أيقونة اللعبة الأصلية بشعار الفيلم",
            ["Custom match works. Quick match doesn't work (it tries to bring up ui that gets stuck), Invite player doesn't work (ui wont appear), The \"accept invite\" button appears in the main menu even though no invite was received"] =
                "تعمل المباراة المخصصة (Custom Match)، لكن المباراة السريعة (Quick Match) تتجمد عند فتح واجهتها. خيار دعوة لاعب (Invite Player) لا يفتح الواجهة، وقد يظهر زر قبول الدعوة حتى دون ورود دعوة.",
            ["Custom replacement icon art by user Jjw410. Can be switched with original icon by renaming the image \"ICON0.PNG\" and replacing the file of the same name in the game's \"PS3_GAME\" directory (it is recommended to retain the original art for safety)"] =
                "أيقونة بديلة مخصصة من تصميم المستخدم Jjw410. يمكن استبدالها بالأيقونة الأصلية بإعادة تسمية الصورة «ICON0.PNG» واستبدال الملف الذي يحمل الاسم نفسه في مجلد «PS3_GAME» الخاص باللعبة (ينصح بالاحتفاظ بالأيقونة الأصلية للأمان)",
            ["Custom replacement icon art by user Jjw410. Can be switched with original icon by renaming the image \"ICON0.PNG\" and replacing the file of the same name in the game's \"PS3_GAME\" directory (it is recommended to retain the original art for safety). Author has centered the original logo and given it a black drop shadow to match the style of the GOW Collection's icon"] =
                "رسمة أيقونة بديلة مخصصة من المستخدم Jjw410. يمكن استبدالها بالأيقونة الأصلية بإعادة تسمية الصورة \"ICON0.PNG\" واستبدال الملف الذي يحمل الاسم نفسه في مجلد \"PS3_GAME\" الخاص باللعبة (ينصح بالاحتفاظ بالرسمة الأصلية للأمان). قام المؤلف بتوسيط الشعار الأصلي وإضافة ظل أسود له ليتوافق مع أسلوب أيقونة GOW Collection",
            ["Custom replacement icon art by user Jjw410. Can be switched with original icon by renaming the image \"ICON0.PNG\" and replacing the file of the same name in the game's \"PS3_GAME\" directory (it is recommended to retain the original art for safety). The Author has removed the background and has given the logo a subtle white glow to match that of the Jak Trilogy's icon"] =
                "رسمة أيقونة بديلة مخصصة من المستخدم Jjw410. يمكن استبدالها بالأيقونة الأصلية بإعادة تسمية الصورة \"ICON0.PNG\" واستبدال الملف الذي يحمل الاسم نفسه في مجلد \"PS3_GAME\" الخاص باللعبة (ينصح بالاحتفاظ بالرسمة الأصلية للأمان). قام المؤلف بإزالة الخلفية وإضافة توهج أبيض خفيف للشعار ليتوافق مع أيقونة Jak Trilogy",
            ["Custom replacement icon art by user Jjw410. Can be switched with original icon by renaming the image \"ICON0.PNG\" and replacing the file of the same name in the game's \"PS3_GAME\" directory (it is recommended to retain the original art for safety). The Author removed the TM symbol (looked out of place and cheap next to the logo) and fixed a small black dot obfuscating the logo's glow"] =
                "رسمة أيقونة بديلة مخصصة من المستخدم Jjw410. يمكن استبدالها بالأيقونة الأصلية بإعادة تسمية الصورة \"ICON0.PNG\" واستبدال الملف الذي يحمل الاسم نفسه في مجلد \"PS3_GAME\" الخاص باللعبة (ينصح بالاحتفاظ بالرسمة الأصلية للأمان). قام المؤلف بإزالة رمز TM (بدا في غير مكانه ورخيصا بجانب الشعار) وأصلح نقطة سوداء صغيرة كانت تحجب توهج الشعار",
            ["Custom replacement icon art by user soundreaction. Can be switched with original icon by renaming the image \"ICON0.PNG\" and replacing the file of the same name in the game's \"PS3_GAME\" directory. (it is recommended to retain the original art for safety)"] =
                "رسمة أيقونة بديلة مخصصة من المستخدم soundreaction. يمكن استبدالها بالأيقونة الأصلية بإعادة تسمية الصورة \"ICON0.PNG\" واستبدال الملف الذي يحمل الاسم نفسه في مجلد \"PS3_GAME\" الخاص باللعبة. (ينصح بالاحتفاظ بالرسمة الأصلية للأمان)",
            ["Custom server WIP by [https://github.com/PSOnlineReturnalGaming PSORG]. Servers were shut down on April 8, 2014"] =
                "خادم مخصص قيد التطوير (WIP) من [https://github.com/PSOnlineReturnalGaming PSORG]. أغلقت الخوادم في 8 أبريل 2014.",
            ["Cutscene dialogue desyncs with animations"] =
                "عدم تزامن حوار المشاهد السينمائية مع الحركات.",
            ["DS4 inputs may appear sluggish, but this might be due to framerate fluctuations"] =
                "قد تبدو مدخلات DS4 بطيئة، لكن قد يكون السبب تذبذب معدل الإطارات.",
            ["Dapper Dan (Left Part)"] =
                "تصفيفة أنيقة بفرق أيسر (Dapper Dan (Left Part))",
            ["Dapper Dan (Right Part)"] =
                "تصفيفة أنيقة بفرق أيمن (Dapper Dan (Right Part))",
            ["Dark banding lines in shadows"] =
                "خطوط معتمة (banding) في الظلال.",
            ["Demon's Souls has an internal technique where it will output duplicate frames if the games performance is not at the FPS cap. This makes the FPS counters report that the game is running at 30 FPS or 60 FPS at all times while the actual performance could be 15 FPS as the game is outputting duplicate frames if your performance is too low. This is not a bug. It's just an unfortunate result of how Demon's Souls works. The reliable way to check true performance is to record gameplay footage and then use tools to analyze the frame-rate from the footage. However, users may use the FPS patch which disables the game's internal frame duplication logic to check performance levels"] =
                "تستخدم Demon's Souls تقنية داخلية تخرج إطارات مكررة إذا لم يصل أداء اللعبة إلى حد الـ FPS. هذا يجعل عدادات FPS تظهر أن اللعبة تعمل على 30 FPS أو 60 FPS طوال الوقت بينما قد يكون الأداء الفعلي 15 FPS لأن اللعبة تخرج إطارات مكررة إذا كان أداؤك منخفضا جدا. هذا ليس خطأ (bug). إنه مجرد نتيجة مؤسفة لطريقة عمل Demon's Souls. الطريقة الموثوقة للتحقق من الأداء الحقيقي هي تسجيل مقاطع اللعب ثم استخدام أدوات لتحليل معدل الإطارات من المقطع. ومع ذلك، يمكن للمستخدمين استخدام تعديل FPS الذي يعطل منطق تكرار الإطارات الداخلي في اللعبة للتحقق من مستويات الأداء",
            ["Depth Buffer Viewport and Depth Border Fix patches are mandatory patches that are needed to fix all the visual problems in this game currently. Since these are Canary patches you will need to enable them manually. Follow the instructions [https://wiki.rpcs3.net/index.php?title=Help:Game_Patches#Manually_adding_custom_patches here] to learn how to"] =
                "تعديلا Depth Buffer Viewport وDepth Border Fix إلزاميان ومطلوبان لإصلاح جميع المشاكل البصرية في هذه اللعبة حاليا. بما أنهما تعديلا Canary فستحتاج إلى تفعيلهما يدويا. اتبع التعليمات [https://wiki.rpcs3.net/index.php?title=Help:Game_Patches#Manually_adding_custom_patches هنا] لمعرفة الطريقة",
            ["Despite being a game made up of sprites, 200% or higher yields more sharper visuals (must add from 50% otherwise graphical issues are present.)"] =
                "رغم أن اللعبة مبنية من sprites، فإن 200% أو أعلى يعطي صورة أكثر حدة (يجب الإضافة من 50% وإلا ستظهر مشاكل رسومية.).",
            ["Despite the \"Top Spin 4 server is not available at this time\" message you can still go into player match and play online, add master10.doublefusion.com=127.0.0.1&&p.ts4.prod.2kgames.com=127.0.0.1 for quicker load times"] =
                "رغم رسالة \"Top Spin 4 server is not available at this time\" لا يزال بإمكانك الدخول إلى player match واللعب أونلاين، أضف master10.doublefusion.com=127.0.0.1&&p.ts4.prod.2kgames.com=127.0.0.1 لتحميل أسرع",
            ["Destiny requires private servers in order to be playable, and are currently in development"] =
                "تتطلب Destiny خوادم خاصة لتصبح قابلة للعب، وهي قيد التطوير حاليا.",
            ["Do not enable V-Sync in the game's options as this can reduce performance"] =
                "لا تفعل V-Sync في خيارات اللعبة لأن ذلك قد يقلل الأداء.",
            ["Do not exceed 60FPS as game speed will be faster"] =
                "لا تتجاوز 60 FPS لأن سرعة اللعبة ستزداد.",
            ["Doing this allows for LBP1 saves to be brought over to LBP2"] =
                "هذا يتيح نقل حفظات LBP1 إلى LBP2.",
            ["Doing this allows for LBP1/2 saves to be brought over to LBP3"] =
                "هذا يتيح نقل حفظات LBP1/2 إلى LBP3.",
            ["Don't bother with this game if you're on MacOS, it very rarely goes in-game and performs poorly"] =
                "لا تتعب نفسك بهذه اللعبة إذا كنت على MacOS، فنادرا ما تدخل اللعب وأداؤها ضعيف.",
            ["Don't update the game as it lowers performance significantly! If you've already updated you can revert to the original version by going to \"dev_hdd0\\game\" inside RPCS3's folder, and deleting the folder matching your GameID for Red Dead Redemption (e.g BLUS30418)"] =
                "لا تحدث اللعبة لأن ذلك يخفض الأداء بشكل كبير! إذا حدثتها بالفعل يمكنك الرجوع إلى النسخة الأصلية بالذهاب إلى \"dev_hdd0\\game\" داخل مجلد RPCS3 وحذف المجلد المطابق لـ GameID الخاص بـ Red Dead Redemption (مثل BLUS30418)",
            ["Due to Render Mode 0 not currently being implemented, crowd rendering is broken. Use the debug setting mentioned to workaround this bug. This however will break brightness"] =
                "رسم الجمهور لا يعمل لأن Render Mode 0 غير مدعوم حاليا. استخدم إعداد Debug المذكور كحل بديل، لكن ذلك سيؤثر سلبا في السطوع.",
            ["Due to a very old regression, the game display is incorrectly rendered and appears stretched. The current workarounds are to mess with the in-game brightness scale or set the base resolution to either 576p or 480p"] =
                "بسبب تراجع (regression) قديم جدا، تعرض شاشة اللعبة بشكل غير صحيح وتبدو ممدودة. الحلول المؤقتة الحالية هي العبث بمقياس السطوع داخل اللعبة أو ضبط الدقة الأساسية على 576p أو 480p.",
            ["Due to the amount of shaders that need compiling at first launch, the intro, and some cutscenes, some textures may be missing or not rendered correctly. This issue will be resolved on all successional launches"] =
                "بسبب كمية المظللات (Shaders) التي تحتاج إلى تجميع عند أول تشغيل، قد تكون بعض الخامات مفقودة أو غير معروضة بشكل صحيح في المقدمة وبعض المشاهد السينمائية. ستحل هذه المشكلة في جميع عمليات التشغيل اللاحقة",
            ["During cutscenes, there is a bug where the dialogue would desync with the animation, causing a slight delay. While this was very common on original hardware, it can still happen on the emulator in some circumstances. There is no fix for this issue however"] =
                "أثناء المشاهد السينمائية، يوجد خطأ يجعل الحوار غير متزامن مع الحركة مسببا تأخرا بسيطا. ورغم أن هذا كان شائعا جدا على الجهاز الأصلي، فلا يزال يمكن أن يحدث على المحاكي في بعض الظروف. لا يوجد حل لهذه المشكلة",
            ["During races or in Trial Mode, the player's shadow will be rendered as a square while airborne. Shadows will also fill up parts of the track, only if there are other racers nearby. No settings can fix this"] =
                "أثناء السباقات أو في Trial Mode، يعرض ظل اللاعب على شكل مربع أثناء وجوده في الهواء. كما تملأ الظلال أجزاء من المضمار فقط إذا كان هناك متسابقون آخرون قريبون. لا يوجد إعداد يصلح هذا.",
            ["Dynamic shadows do not properly scale with Resolution Scaling"] =
                "الظلال الديناميكية لا تتناسب بشكل صحيح مع Resolution Scaling.",
            ["Enabling 'Write Color Buffers' can induce strong audio crackle on some systems, but setting the GPU config Framelimit to 'PS3 Native' seems to fix this"] =
                "تفعيل 'Write Color Buffers' قد يسبب طقطقة صوتية قوية على بعض الأنظمة، لكن ضبط Framelimit في إعدادات GPU على 'PS3 Native' يبدو أنه يصلح ذلك.",
            ["Enabling Async Texture Streaming in GPU tab may slightly improve performance depending on your hardware, but causes shop logo to flicker. Unsupported on macOS"] =
                "تفعيل Async Texture Streaming في تبويب GPU قد يحسن الأداء قليلا حسب عتادك، لكنه يسبب وميض شعار المتجر. غير مدعوم على macOS.",
            ["Enabling Multithreaded RSX and Async Texture Streaming in GPU tab may improve your performance depending on the hardware. Avoid enabling on macOS they're not supported there"] =
                "تفعيل Multithreaded RSX وAsync Texture Streaming في تبويب GPU قد يحسن أداءك حسب العتاد. تجنب تفعيلهما على macOS فهما غير مدعومين هناك.",
            ["Enabling Multithreaded RSX in GPU tab may slightly improve performance depending on your hardware. Unsupported on macOS"] =
                "تفعيل Multithreaded RSX في تبويب GPU قد يحسن الأداء قليلا حسب عتادك. غير مدعوم على macOS.",
            ["Enabling Write Colour Buffers and Read Colour Buffers fixes visual issues while in-game but it does so at the cost of FPS loss (30-40 FPS without them enabled, 25-35 FPS with them enabled. Tested on a Ryzen 5700U)"] =
                "تفعيل Write Colour Buffers وRead Colour Buffers يصلح المشاكل البصرية أثناء اللعب لكن على حساب خسارة في FPS (30-40 FPS دون تفعيلهما، 25-35 FPS مع تفعيلهما. تم الاختبار على Ryzen 5700U).",
            ["Enter the below text in the IP/Host Switches section: theater.ps3.arcadia=152.53.15.83&&lotr-pandemic-ps3.fesl.ea.com=152.53.15.83"] =
                "أدخل النص التالي في قسم IP/Host Switches: theater.ps3.arcadia=152.53.15.83&&lotr-pandemic-ps3.fesl.ea.com=152.53.15.83",
            ["Enter the below text in the IP/Hosts switches section: gosredirector.ea.com==95.217.209.57"] =
                "أدخل النص التالي في قسم إعدادات IP/Hosts Switches: gosredirector.ea.com==95.217.209.57",
            ["Even if the Vblank rate with 3000Hz solves the RSX crashes before ingame, it still isn't considered to be playable"] =
                "حتى لو كان معدل Vblank عند 3000 Hz يحل أعطال RSX قبل الدخول إلى اللعب، فلا تزال اللعبة غير مصنفة على أنها playable.",
            ["Even when using wiki settings. The game is prone to random crashes"] =
                "حتى عند استخدام إعدادات الويكي، اللعبة عرضة لأعطال عشوائية.",
            ["Even with Write Color Buffers enabled, at least on NVIDIA hardware on Linux, characters can appear with corrupted-looking textures"] =
                "حتى مع تفعيل Write Color Buffers، على الأقل على عتاد NVIDIA في Linux، قد تظهر الشخصيات بخامات تبدو تالفة.",
            ["Even with optimal settings and very powerful hardware, this game is known to have a pretty inconsistent framerate, especially in stages such as Rooftop Run Night and Jungle Joyride Day"] =
                "حتى مع الإعدادات المثلى وعتاد قوي جدا، من المعروف أن معدل الإطارات في هذه اللعبة غير مستقر إلى حد كبير، خصوصا في مراحل مثل Rooftop Run Night وJungle Joyride Day.",
            ["Even with wiki settings, you may still see graphical artifacts in-game"] =
                "حتى مع إعدادات الويكي، قد تظل ترى عيوبا رسومية داخل اللعبة.",
            ["Every time you enter/exit a vacuum, the game will render at half the resolution"] =
                "عند دخول مناطق الفراغ أو الخروج منها، تنخفض دقة عرض اللعبة إلى النصف.",
            ["Everything works, but the friends list and invites are glitched and will result in a black screen"] =
                "كل شيء يعمل، لكن قائمة الأصدقاء والدعوات معطلة وتؤدي إلى شاشة سوداء.",
            ["Expect a heavy drop in performance when using the RYNO V"] =
                "توقع انخفاضا كبيرا في الأداء عند استخدام RYNO V.",
            ["Expect semi-frequent crashes in areas with lots of particle effects. Most notably in multiplayer and near the end of Chapter 6"] =
                "قد تتعطل اللعبة في المناطق التي تحتوي على مؤثرات جسيمات كثيرة، خاصة أثناء اللعب الجماعي وقرب نهاية الفصل السادس.",
            ["Expect semi-lighting problems while in-game"] =
                "توقع مشاكل شبه متعلقة بالإضاءة أثناء اللعب.",
            ["Expect some minor flickering bright artifacts on some objects"] =
                "توقع بعض العيوب المضيئة الوامضة الطفيفة على بعض الأجسام.",
            ["Expect visual/graphical problems while in-game"] =
                "توقع مشاكل بصرية/رسومية أثناء اللعب.",
            ["Fab Fedora"] =
                "قبعة فيدورا أنيقة (Fab Fedora)",
            ["Fade-in or fade-out transitions are not present in splash screens and cutscenes. There's no setting to fix this glitch. See RPCS3 issue #17021 for details"] =
                "انتقالات التلاشي الداخل أو الخارج (fade-in/fade-out) غير موجودة في شاشات البداية والمشاهد السينمائية. لا يوجد إعداد لإصلاح هذا الخلل. راجع مشكلة RPCS3 رقم #17021 للتفاصيل.",
            ["Fancy Newsboy"] =
                "قبعة بائع صحف أنيقة (Fancy Newsboy)",
            ["Fans and seats appearing on the pitchers mound or in the sky"] =
                "ظهور جماهير ومقاعد على تلة الرامي أو في السماء.",
            ["Feminine"] =
                "أنثوي (Feminine)",
            ["File:P5 bustup fix.jpg|Dialog portrait corrupted due to Resolution upscaling"] =
                "File:P5 bustup fix.jpg|صورة الحوار تالفة بسبب رفع الدقة (Resolution upscaling).",
            ["Final segment of Metal Gear Solid 3 experiences severe slowdown due to CPU bottleneck, but is still playable"] =
                "المقطع الأخير من Metal Gear Solid 3 يعاني من بطء شديد بسبب اختناق CPU، لكنه لا يزال قابلا للعب.",
            ["Finding public matches & invites are functional. Being in a party & joining a public match will disband"] =
                "العثور على المباريات العامة والدعوات يعملان. التواجد في party والانضمام إلى مباراة عامة سيؤدي إلى حل الـ party.",
            ["Fire/Smoke effects are rendered incorrectly and darken the background. Does not affect playability"] =
                "تأثيرات النار/الدخان تعرض بشكل غير صحيح وتغمق الخلفية. لا تؤثر على قابلية اللعب.",
            ["First tutorial mission: Forced motion movement can be skipped by pressing L3 + R3 instead"] =
                "مهمة التدريب الأولى: يمكن تخطي الحركة الإجبارية بالحركة بالضغط على L3 + R3 بدلا منها.",
            ["For certain CPUs, the game hangs on a black screen after the Atlus logo. This issue was first visible after RPCS3 PR #4422 and is being tracked as part of RPCS3 issue #4473. For people affected by this issue, use this [https://obk.ee/rpcs3/archive/rpcs3-v0.0.5-6664-47752221_win64.7z older build] of the emulator"] =
                "لدى بعض المعالجات (CPUs)، تتوقف اللعبة على شاشة سوداء بعد شعار Atlus. ظهرت هذه المشكلة أول مرة بعد طلب دمج RPCS3 رقم #4422 ويجري تتبعها ضمن مشكلة RPCS3 رقم #4473. للمتأثرين بهذه المشكلة، استخدموا هذه [https://obk.ee/rpcs3/archive/rpcs3-v0.0.5-6664-47752221_win64.7z نسخة أقدم] من المحاكي",
            ["For certain CPUs, the game may crash upon trying to open Single Player Modes and Versus Modes. Setting SPU Decoder to ASMJIT Recompiler may fix this"] =
                "لدى بعض المعالجات (CPUs)، قد تتعطل اللعبة عند محاولة فتح Single Player Modes وVersus Modes. ضبط SPU Decoder على ASMJIT Recompiler قد يصلح هذا.",
            ["For players using a controller without motion controls, the use of [https://github.com/ZeptoBST/DualShock4-emulator/releases/tag/Release DualShock4-emulator] will be required to play through sections with mandatory gyro controls. Use the D-Pad to control the virtual gyroscope. Make sure to use the DualShock 4 pad handler in the gamepad configuration"] =
                "للاعبين الذين يستخدمون وحدة تحكم بدون عناصر تحكم بالحركة، سيكون استخدام [https://github.com/ZeptoBST/DualShock4-emulator/releases/tag/Release DualShock4-emulator] مطلوبا لإكمال المقاطع التي تتطلب التحكم الإجباري بالجيروسكوب. استخدم D-Pad للتحكم بالجيروسكوب الافتراضي. تأكد من استخدام DualShock 4 pad handler في إعدادات gamepad",
            ["For the physical releases, the game uses a launcher to choose the game you want. On first launch, the game will install to the (emulated) hard drive but will not prompt the user. If your controls are not working, allow the game around 20 minutes to install and it should start working. This should not happen on future boots"] =
                "بالنسبة للإصدارات الفعلية (الأقراص)، تستخدم اللعبة launcher لاختيار اللعبة التي تريدها. عند أول تشغيل، ستثبت اللعبة على القرص الصلب (المحاكى) دون إشعار المستخدم. إذا لم تعمل أزرار التحكم لديك، اترك اللعبة نحو 20 دقيقة لتكتمل عملية التثبيت وينبغي أن تبدأ بالعمل. لا ينبغي أن يحدث هذا في عمليات التشغيل اللاحقة",
            ["For users playing the original disc release, the game may not boot and either gets stuck on the SPU cache screen, a black screen, or the game closes. Here are some solutions to possibly help"] =
                "للمستخدمين الذين يلعبون إصدار القرص الأصلي، قد لا تشتغل اللعبة وتعلق إما على شاشة SPU cache أو شاشة سوداء أو تغلق اللعبة. فيما يلي بعض الحلول التي قد تساعد.",
            ["Fortunately, GTA V has a 'checkpoint skip' feature which allows you to get through missions such as 'The Merryweather Heist' by dying 3 times and skipping to the next checkpoint which isn't in the water"] =
                "لحسن الحظ، تحتوي GTA V على ميزة 'checkpoint skip' التي تتيح لك تجاوز مهام مثل 'The Merryweather Heist' بالموت 3 مرات والانتقال إلى نقطة الحفظ التالية التي ليست في الماء.",
            ["Frame Limiter when left on Auto can make the game run at 60FPS by default, causing game speed to be doubled. This can be fixed by setting frame limiter to 30"] =
                "عند ترك Frame Limiter على Auto قد تعمل اللعبة على 60 FPS افتراضيا مما يضاعف سرعة اللعبة. يمكن إصلاح ذلك بضبط frame limiter على 30.",
            ["Frequent infinite loading screens when attempting to play multiplayer"] =
                "شاشات تحميل لا نهائية متكررة عند محاولة اللعب الجماعي.",
            ["Full support on DualShock and DualSense controllers. Change in-game controller layout to No3 for full gyro steering"] =
                "دعم كامل لوحدات DualShock وDualSense. غير تخطيط وحدة التحكم داخل اللعبة إلى No3 للتوجيه الكامل بالجيروسكوب.",
            ["G.I. Cap"] =
                "قبعة عسكرية (G.I. Cap)",
            ["G.I. Jane"] =
                "تسريحة عسكرية نسائية (G.I. Jane)",
            ["GUI friend invites not implemented yet"] =
                "دعوات الأصدقاء من الواجهة الرسومية (GUI) غير مدعومة بعد.",
            ["Game crashes when loading a story mode/free mode battle"] =
                "تتعطل اللعبة عند تحميل معركة في story mode/free mode.",
            ["Game crashes when selecting either online option"] =
                "تتعطل اللعبة عند اختيار أي من خياري اللعب عبر الإنترنت.",
            ["Game does not boot"] =
                "اللعبة لا تقلع.",
            ["Game isn't playable due to random crashes"] =
                "اللعبة غير قابلة للعب بسبب أعطال عشوائية.",
            ["Game isn't very popular, so you may not find anyone. (UPNP was on during testing.)"] =
                "اللعبة ليست شائعة كثيرا، لذا قد لا تجد أحدا. (كان UPNP مفعلا أثناء الاختبار.).",
            ["Game makes use of SIXAXIS and you cannot disable that, so you need a controller that supports motions (DS3/4/Dualsense)"] =
                "تستخدم اللعبة SIXAXIS ولا يمكنك تعطيله، لذلك تحتاج إلى وحدة تحكم تدعم الحركة (DS3/4/Dualsense).",
            ["Game makes use of SIXAXIS tilt functionality, you can disable it in ingame options"] =
                "تستخدم اللعبة وظيفة الإمالة (tilt) في SIXAXIS، ويمكنك تعطيلها من خيارات اللعبة.",
            ["Game needs RPCN to get ingame, when updated it will get stuck on \"Download Error\". Game servers were closed on September 2013"] =
                "تحتاج اللعبة إلى RPCN للدخول إلى اللعب، وعند تحديثها ستعلق على \"Download Error\". أغلقت خوادم اللعبة في سبتمبر 2013.",
            ["Game only renders button prompts and the loading screen spinning ring resulting in black screen unless patches or RCB are enabled"] =
                "تعرض اللعبة أزرار الإرشادات وحلقة التحميل الدوارة فقط مما ينتج شاشة سوداء ما لم تفعل التعديلات أو RCB.",
            ["Game performs well in up to 4k in most scenarios. Occasional slowdown in areas with an incredibly large amount of textures with alpha effects such as The Pain, of the field of flowers in MGS3. Performance improves somewhat when using classic Overhead Camera due to limiting render distance in these instances"] =
                "تعمل اللعبة بشكل جيد حتى 4k في معظم الحالات. يحدث بطء أحيانا في المناطق ذات الكمية الضخمة جدا من الخامات ذات تأثيرات alpha مثل The Pain، وحقل الأزهار في MGS3. يتحسن الأداء نوعا ما عند استخدام Overhead Camera الكلاسيكية بسبب تقليل مسافة العرض في هذه الحالات",
            ["Game speed is tied to frame rate"] =
                "سرعة اللعبة مرتبطة بمعدل الإطارات.",
            ["Game tends to freeze when you're idling or have the game paused for a while, you will have to restart RPCS3 when this happens"] =
                "تميل اللعبة إلى التجمد عندما تكون خاملا أو تترك اللعبة متوقفة مؤقتا لفترة، وسيتعين عليك إعادة تشغيل RPCS3 عند حدوث ذلك.",
            ["Game version tested: WRC 3 [BLUS31168]. Netplay allows RPCN users to upload their fastest times. Only the record times leader board portion of multiplayer has been tested. Leader boards showing RPCN's gamertags are working"] =
                "إصدار اللعبة المختبر: WRC 3 [BLUS31168]. يتيح Netplay لمستخدمي RPCN رفع أسرع أوقاتهم. تم اختبار جزء لوحة الصدارة لأوقات الأرقام القياسية فقط من اللعب الجماعي. لوحات الصدارة التي تعرض gamertags الخاصة بـ RPCN تعمل",
            ["Game will crash if sustained below or above 30 frames per second for too long, therefore it is important that you lock the framerate to 30 fps and have a CPU which is powerful enough to sustain that framerate"] =
                "ستنهار اللعبة إذا استمرت أقل من 30 إطارا في الثانية أو أكثر منها لفترة طويلة، لذا من المهم أن تقفل معدل الإطارات على 30 FPS وأن يكون لديك CPU قوي بما يكفي للحفاظ على هذا المعدل.",
            ["Game will hang with VM: Access violation writing location 0x17 (unmapped memory) when playing split screen multiplayer. Check updates for this bug here: https://github.com/RPCS3/rpcs3/issues/16426"] =
                "قد تتجمد اللعبة في وضع الشاشة المقسمة مع الخطأ VM: Access violation writing location 0x17 (unmapped memory). تابع المشكلة: https://github.com/RPCS3/rpcs3/issues/16426",
            ["Game will ignore custom resolution scale in some cutscenes and in the gameplay \"field\". During battle it will use custom resolution, and then after exiting battle the field will also be displayed with custom resolution until loading a new area where it ignores the custom resolution scale again"] =
                "ستتجاهل اللعبة Resolution Scale المخصص في بعض المشاهد السينمائية وفي \"field\" اللعب. أثناء المعركة ستستخدم الدقة المخصصة، وبعد الخروج من المعركة سيعرض الـ field أيضا بالدقة المخصصة إلى أن يتم تحميل منطقة جديدة حيث تتجاهل Resolution Scale المخصص مرة أخرى",
            ["Ghost artifacts are present during gameplay"] =
                "تظهر عيوب شبحية (ghost artifacts) أثناء اللعب.",
            ["Go to USRDIR\\WHEELMANGAME\\CONFIG and open the COALESCED.INI file with a text editor, and change this line: m_bShowDebugMenu=False to be True instead of False. Save and launch the game"] =
                "اذهب إلى USRDIR\\WHEELMANGAME\\CONFIG وافتح الملف COALESCED.INI بمحرر نصوص، وغير هذا السطر: m_bShowDebugMenu=False ليصبح True بدلا من False. احفظ وشغل اللعبة.",
            ["God of War games have some bugs in their original release, even on a real PS3 (remaster collection) or PS2. They may or may not be happening more often on RPCS3, but they aren't emulator bugs. In GoW1 Pandora's Box may not appear in the room, and in GoW2 the event in titan's cave might not trigger halting progression. Restarting the game can fix the issue but you might have to do it a few times. The reason for this has never been identified, and looks to be an engine issue. Source for GoW 2 [https://forums.afterdawn.com/threads/possible-god-of-war-2-glitch.537797/ 2007 forum thread about this issue]"] =
                "تحتوي ألعاب God of War على بعض الأخطاء في إصدارها الأصلي، حتى على PS3 حقيقي (remaster collection) أو PS2. قد تحدث أو لا تحدث بشكل أكثر على RPCS3، لكنها ليست أخطاء محاكي. في GoW1 قد لا يظهر Pandora's Box في الغرفة، وفي GoW2 قد لا يفعل الحدث في كهف titan مما يوقف التقدم. إعادة تشغيل اللعبة قد تصلح المشكلة لكن قد تضطر إلى فعل ذلك عدة مرات. لم يعرف سبب هذا قط، ويبدو أنه مشكلة في المحرك. المصدر لـ GoW 2 [https://forums.afterdawn.com/threads/possible-god-of-war-2-glitch.537797/ نقاش في منتدى عام 2007 حول هذه المشكلة]",
            ["Grass textures are not displayed correctly with default settings, this can be resolved with turning on Read Color Buffers and turning on Write color Buffers"] =
                "خامات العشب لا تعرض بشكل صحيح مع الإعدادات الافتراضية، ويمكن حل ذلك بتفعيل Read Color Buffers وتفعيل Write color Buffers.",
            ["Hairstyles"] =
                "تسريحات الشعر (Hairstyles)",
            ["Hats"] =
                "القبعات (Hats)",
            ["Heavy audio stuttering"] =
                "تقطع صوتي شديد.",
            ["Holar Tower objective might randomly not show up if playing on PPU LLVM and can not progress further, can be fixed by changing to PPU Interpreter (static)"] =
                "قد لا يظهر هدف Holar Tower عشوائيا عند اللعب على PPU LLVM ولا يمكن التقدم أكثر، ويمكن إصلاحه بالتغيير إلى PPU Interpreter (static).",
            ["Hold L1 and L2 Up, Up, Left, Left, Up, Left and press Start to unlock Debug menu"] =
                "اضغط مع الاستمرار على L1 وL2 ثم Up, Up, Left, Left, Up, Left واضغط Start لفتح قائمة Debug.",
            ["Hold L1 and R1 Right, Up, Right, Right, Up, Up, Right, Right Right, Up, Up, Up and press Start to unlock Secret menu"] =
                "اضغط مع الاستمرار على L1 وR1 ثم Right, Up, Right, Right, Up, Up, Right, Right Right, Up, Up, Up واضغط Start لفتح القائمة السرية (Secret menu).",
            ["However, you may use a post-processing solution such as [https://reshade.me/ Reshade] in order to adjust the brightness and gamma using the tonemap.fx shader by using these suggested settings: {| class=\"wikitable\" ! [Tonemap.fx]"] =
                "ومع ذلك، يمكنك استخدام حل معالجة لاحقة (post-processing) مثل [https://reshade.me/ Reshade] لضبط السطوع وgamma باستخدام شيدر tonemap.fx عبر هذه الإعدادات المقترحة: {| class=\"wikitable\" ! [Tonemap.fx]",
            ["If GT6 won't boot past the Sony Computer Entertainment logo, deleting GT6's save data should fix the problem"] =
                "إذا لم تتجاوز GT6 شعار Sony Computer Entertainment عند بدء التشغيل، فحذف بيانات حفظ GT6 ينبغي أن يحل المشكلة.",
            ["If Quickplay is played, a race condition will cause the game to crash when entering Practice as guitar or bass. This can be fixed by installing the [https://rb3dx.milohax.org/ Rock Band 3 Deluxe] mod"] =
                "إذا لعب Quickplay، فإن race condition ستتسبب في تعطل اللعبة عند الدخول إلى Practice بالجيتار أو البيس. يمكن إصلاح ذلك بتثبيت مود [https://rb3dx.milohax.org/ Rock Band 3 Deluxe]",
            ["If UPNP is enabled in the Network tab of Rock Band 3's custom configuration, the emulator can crash when going online, searching for others, or accepting/receiving invites. Disabling UPNP will restrict you to only finding two players. Manually forwarding port 9103 (UDP) in your router settings is recommended if RPCS3's UPNP function is causing crashes"] =
                "قد يؤدي تفعيل UPnP في تبويب Network بإعدادات Rock Band 3 المخصصة إلى تعطل RPCS3 عند الاتصال بالإنترنت أو البحث عن لاعبين أو استخدام الدعوات. تعطيل UPnP قد يحصر النتائج في لاعبين اثنين. إذا كان UPnP يسبب التعطل، فاضبط إعادة توجيه منفذ UDP رقم 9103 يدويا في جهاز التوجيه.",
            ["If for any reason you cannot use Host GPU Labels, use Force CPU Blit Emulation + Write Color Buffers to fix the issue. This does come at the cost of performance"] =
                "إذا لم تتمكن لأي سبب من استخدام Host GPU Labels، فاستخدم Force CPU Blit Emulation + Write Color Buffers لإصلاح المشكلة. هذا يأتي على حساب الأداء.",
            ["If some character models start to disappear in certain parts of the early-game, this can be resolved by setting the SPU Xfloat accuracy to Accurate, but this will greatly hurt performance and audio, do not use this setting unless absolutely required"] =
                "إذا بدأت بعض نماذج الشخصيات بالاختفاء في أجزاء معينة من بداية اللعبة، يمكن حل ذلك بضبط SPU Xfloat accuracy على Accurate، لكن هذا سيضر بالأداء والصوت كثيرا، لا تستخدم هذا الإعداد إلا عند الضرورة القصوى",
            ["If the game for some reason does not bring up the instructions for the Batmobile, either reload your save or update the game to latest if you're on 1.00. Appears to be a issue that happens randomly on base version"] =
                "إذا لم تعرض اللعبة لأي سبب تعليمات Batmobile، فأعد تحميل حفظك أو حدث اللعبة إلى أحدث إصدار إن كنت على 1.00. يبدو أنها مشكلة تحدث عشوائيا في النسخة الأساسية.",
            ["If users install more that 16 DLCs, the game will crash after the intro logos. This issue is tracked as part of RPCS3 issue #4091"] =
                "إذا ثبت المستخدمون أكثر من 16 DLC، فستتعطل اللعبة بعد شعارات المقدمة. يجري تتبع هذه المشكلة ضمن مشكلة RPCS3 رقم #4091.",
            ["If using the 60 FPS patch, or you have the framelimit setting to higher than 30 FPS, Joker will glitch out at the Saori puzzle in the 2nd palace. Also, in the 5th Palace you will get stuck in some elevators. The only fix for that is disabling the 60 FPS patch, limiting the framerate to 30 FPS from the GPU settings and reloading the save"] =
                "عند استخدام تعديل 60 FPS، أو ضبط إعداد framelimit على أعلى من 30 FPS، ستتعطل حركة Joker عند لغز Saori في القصر الثاني. كذلك في القصر الخامس ستعلق في بعض المصاعد. الحل الوحيد لذلك هو تعطيل تعديل 60 FPS وتحديد معدل الإطارات عند 30 FPS من إعدادات GPU وإعادة تحميل الحفظ",
            ["If using the digital edition of WipEout HD and you would like to use the HD Fury DLC, please install all available updates before installing the DLC as installing the DLC first may break the game"] =
                "إذا كنت تستخدم النسخة الرقمية من WipEout HD وتريد استخدام HD Fury DLC، فالرجاء تثبيت جميع التحديثات المتاحة قبل تثبيت الـ DLC لأن تثبيت الـ DLC أولا قد يعطل اللعبة.",
            ["If you are on MacOS do not use Multithreaded RSX and Async Texture Streaming, they are unsupported and lead to a black screen or a freeze during boot"] =
                "إذا كنت على MacOS فلا تستخدم Multithreaded RSX وAsync Texture Streaming، فهما غير مدعومين ويؤديان إلى شاشة سوداء أو تجمد أثناء بدء التشغيل.",
            ["If you don't want to adjust resolution scale threshold for proper DoF scaling when resolution scaling (as mentioned in the configuration section above), then alternatively you can use [https://www.nexusmods.com/demonssouls/mods/1 this mod] to disable this effect entirely. Be sure to read the installation instruction in the description tab of the linked page carefully when applying this mod"] =
                "إذا كنت لا تريد ضبط resolution scale threshold للحصول على تحجيم DoF صحيح عند استخدام resolution scaling (كما ذكر في قسم الإعدادات أعلاه)، فيمكنك بدلا من ذلك استخدام [https://www.nexusmods.com/demonssouls/mods/1 هذا المود] لتعطيل هذا التأثير بالكامل. احرص على قراءة تعليمات التثبيت في تبويب الوصف بالصفحة المرتبطة بعناية عند تطبيق هذا المود",
            ["If you experience a \"crunchy\" or \"crackly\" audio effect while playing the game, this is caused by FPS desynchronization. Set the framelimit in the GPU options to \"PS3 Native\" in order to fix this issue"] =
                "إذا واجهت تأثير صوت \"crunchy\" أو \"crackly\" أثناء اللعب، فهذا ناتج عن عدم تزامن FPS. اضبط framelimit في خيارات GPU على \"PS3 Native\" لإصلاح هذه المشكلة.",
            ["If you experience issues relating to audio volume in certain situations, especially dialog, try setting audio output to stereo. Issue appears to be related to incorrect audio mixing"] =
                "إذا واجهت مشاكل تتعلق بمستوى الصوت في حالات معينة، خصوصا الحوار، فجرب ضبط مخرج الصوت على stereo. يبدو أن المشكلة مرتبطة بمزج صوتي غير صحيح.",
            ["If you find that the music and some sound effects are missing when playing the game with RPCS3's default settings, you need to modify settings present only in the config.yml. Click here for instructions on accessing the same. Search for \"PPU Threads\" which should be the third line in the file. Change the value from 2 to 1, save and close the file. This should fix any audio issues present"] =
                "إذا وجدت أن الموسيقى وبعض المؤثرات الصوتية مفقودة عند تشغيل اللعبة بالإعدادات الافتراضية لـ RPCS3، فعليك تعديل إعدادات موجودة فقط في config.yml. اضغط هنا للحصول على تعليمات الوصول إليه. ابحث عن «PPU Threads» وهو يجب أن يكون السطر الثالث في الملف. غير القيمة من 2 إلى 1، ثم احفظ الملف وأغلقه. هذا يجب أن يصلح أي مشاكل صوتية",
            ["If you get a crash with version 01.11, set the network status as disabled"] =
                "إذا واجهت عطلا مع الإصدار 01.11، فعطل حالة الشبكة (network status).",
            ["If you get stuck at the Scouting the Port mission you can use the [https://gtacentral.com/gta-5/gta-5-cheats-codes-ps3/ Raise Wanted level cheat] to bypass, restarting the game seems to re-enable the trophies"] =
                "إذا علقت في مهمة Scouting the Port يمكنك استخدام [https://gtacentral.com/gta-5/gta-5-cheats-codes-ps3/ Raise Wanted level cheat] لتجاوزها، ويبدو أن إعادة تشغيل اللعبة تعيد تفعيل الكؤوس (trophies).",
            ["If you have Dead Space 1 installed or a save file, the Refurbished Plasma Cutter is unlocked as a free alternative design to the base Plasma Cutter"] =
                "إذا كان لديك Dead Space 1 مثبتة أو ملف حفظ، فسيتم فتح Refurbished Plasma Cutter كتصميم بديل مجاني لـ Plasma Cutter الأساسي.",
            ["If you have a Dead Space 2 save, you will unlock the Planet Cracker Plasma Cutter as an alternative to the base Plasma Cutter with slightly stronger stats"] =
                "إذا كان لديك حفظ Dead Space 2، فستفتح Planet Cracker Plasma Cutter كبديل لـ Plasma Cutter الأساسي بإحصائيات أقوى قليلا.",
            ["If you have a save or game data from Tools of Destruction, you receive a 25% discount on items that originated from that game including Mr. Zurkon, Buzz Blades, Mag-net Launcher, and the Groovitron"] =
                "إذا كان لديك حفظ أو بيانات لعبة من Tools of Destruction، فستحصل على خصم 25% على العناصر التي نشأت من تلك اللعبة بما فيها Mr. Zurkon وBuzz Blades وMag-net Launcher وGroovitron.",
            ["If you have a savegame from Saints Row IV, you'll have your character in this game. Make sure savegame folder has the same region as your game"] =
                "إذا كان لديك savegame من Saints Row IV، فستحصل على شخصيتك في هذه اللعبة. تأكد أن مجلد savegame من نفس منطقة (region) لعبتك.",
            ["If you skip the Bamco logos, the game gets stuck on a white screen. This was reported in RPCS3 issue #12465"] =
                "إذا تخطيت شعارات Bamco، فستعلق اللعبة على شاشة بيضاء. تم الإبلاغ عن هذا في مشكلة RPCS3 رقم #12465.",
            ["If you want to use resolution scaling with SRM enabled, you can adjust the values via the Home Menu Overlay. This will be edited once it's changed to where SRM no longer disables resolution scaling"] =
                "إذا أردت استخدام resolution scaling مع تفعيل SRM، يمكنك ضبط القيم عبر Home Menu Overlay. سيتم تعديل هذا بعد تغييره بحيث لا يعطل SRM خاصية resolution scaling.",
            ["If you're interested in installing the game updates for the jp copy of this game then before doing that you must first run the game in the base version (v1.00) and let it install its game data to the HDD, after that's done you can safely install the game updates. Failing to do so before installing the updates will prevent the game from going past the initial boot screen"] =
                "إذا كنت ترغب في تثبيت تحديثات النسخة اليابانية (jp) من هذه اللعبة، فيجب أولا تشغيل اللعبة بالإصدار الأساسي (v1.00) وترك اللعبة تثبت بياناتها على القرص الصلب HDD، وبعد اكتمال ذلك يمكنك تثبيت تحديثات اللعبة بأمان. عدم فعل ذلك قبل تثبيت التحديثات سيمنع اللعبة من تجاوز شاشة الإقلاع الأولى",
            ["If you're on BLUS, do not update the game, otherwise you will get stuck on an infinite loading screen and cannot go ingame. Not even PPU Interpreter fixes this issue"] =
                "إذا كنت على BLUS، فلا تحدث اللعبة وإلا ستعلق على شاشة تحميل لا نهائية ولن تتمكن من دخول اللعب. حتى PPU Interpreter لا يصلح هذه المشكلة.",
            ["If you're on MacOS do not use Async Texture Streaming or Multithreaded RSX. They are unsupported and will lead to a black screen or a freeze during boot"] =
                "إذا كنت تستخدم MacOS فلا تستخدم Async Texture Streaming أو Multithreaded RSX. فهما غير مدعومين وسيؤديان إلى شاشة سوداء أو تجمد أثناء بدء التشغيل.",
            ["If you're on MacOS do not use Multithreaded RSX or Async Texture Streaming. They don't work on MacOS and will cause the game to freeze during boot or produce a black screen"] =
                "إذا كنت تستخدم MacOS فلا تستخدم Multithreaded RSX أو Async Texture Streaming. فهما لا يعملان على MacOS وسيتسببان في تجمد اللعبة أثناء بدء التشغيل أو ظهور شاشة سوداء.",
            ["If you're on MacOS don't update the game past 1.00. The game will freeze on the menu screen when trying to start a new game if updated"] =
                "إذا كنت تستخدم MacOS فلا تحدث اللعبة إلى ما بعد 1.00. ستتجمد اللعبة في شاشة القائمة عند محاولة بدء لعبة جديدة إذا تم تحديثها.",
            ["In 2.00 disc release of BCUS98242, the included 1.06 update EBOOT hash differs from the one provided via Sony Update API. This does not affect gameplay in any way other than requiring users to use patches with (2.00 Disc) in name"] =
                "في إصدار القرص 2.00 من BCUS98242، يختلف hash ملف EBOOT الخاص بتحديث 1.06 المضمن عن الذي يوفره Sony Update API. لا يؤثر ذلك على اللعب بأي شكل سوى أنه يتطلب من المستخدمين استخدام التعديلات التي يحتوي اسمها على (2.00 Disc)",
            ["In CPU -> SPU Decoder, select \"Interpreter (Dynamic)\". In Advanced - > Core, select \"Delay each odd MFC Command\""] =
                "في CPU -> SPU Decoder، اختر «Interpreter (Dynamic)». وفي Advanced - > Core، اختر «Delay each odd MFC Command».",
            ["In Metal Gear Solid 2, after defeating the final boss, arriving to the cutscenes and when the credits start, the game might crash. However, this is known to happen on real hardware as well"] =
                "في Metal Gear Solid 2، بعد هزيمة الزعيم الأخير والوصول إلى المشاهد السينمائية وعند بدء أسماء فريق العمل (credits)، قد تتعطل اللعبة. لكن من المعروف أن هذا يحدث على الجهاز الحقيقي أيضا.",
            ["In RaC3, there are bonuses if you have saves from RaC1 and RaC2 inside your savedata folder"] =
                "في RaC3، توجد مكافآت إذا كانت لديك ملفات حفظ من RaC1 وRaC2 داخل مجلد savedata.",
            ["In Secret Agent Clank, there was a code that gives you the \"Convict Ratchet\" skin for this game. The code is as follows: SQUARE, CIRCLE, SQUARE, CIRCLE, UP, UP, TRIANGLE, DOWN, UP (Use the D-Pad)"] =
                "في Secret Agent Clank، كان هناك كود يمنحك سكن «Convict Ratchet» لهذه اللعبة. الكود كالتالي: SQUARE, CIRCLE, SQUARE, CIRCLE, UP, UP, TRIANGLE, DOWN, UP (استخدم D-Pad).",
            ["In The Sands of Time some cut-scenes have a distorted black bar seen at the bottom assumed to be an issue caused by Ubisoft when upscaling the game for the HD remaster. This is not an emulator bug and can be ignored"] =
                "في The Sands of Time تحتوي بعض المشاهد السينمائية على شريط أسود مشوه في الأسفل، ويفترض أنه مشكلة سببتها Ubisoft عند رفع دقة اللعبة لإعادة الإصدار HD. هذه ليست علة في المحاكي ويمكن تجاهلها.",
            ["In Toy Box Mode - when going in split-screen mode - weird bars may appear on screen when playing with the renderer 'Vulkan'. Switching to 'OpenGl' Fixes this"] =
                "في Toy Box Mode - عند الدخول في وضع الشاشة المقسمة - قد تظهر أشرطة غريبة على الشاشة عند اللعب بمحرك العرض 'Vulkan'. التحويل إلى 'OpenGl' يصلح ذلك.",
            ["In Warrior Within and especially The Sands Of Time some light sources maybe observed flickering and passing through walls. This unfortunately is an issue also present on original hardware, Although annoying it can be ignored"] =
                "في Warrior Within وخصوصا The Sands Of Time قد تلاحظ بعض مصادر الإضاءة وهي ترتعش وتمر عبر الجدران. للأسف هذه المشكلة موجودة أيضا على الجهاز الأصلي، ومع أنها مزعجة يمكن تجاهلها.",
            ["In certain cases with resolution scaling lag at the start of races can be seen. This can be fixed by using the dynamic resolution scaling patch"] =
                "في بعض الحالات مع تكبير الدقة (resolution scaling) قد يلاحظ تأخر (lag) في بداية السباقات. يمكن إصلاح ذلك باستخدام تعديل dynamic resolution scaling.",
            ["In certain parts of the game the colors are rendered incorrectly. This is only present in the Vulkan renderer"] =
                "في بعض أجزاء اللعبة تعرض الألوان بشكل خاطئ. هذا يحدث فقط في محرك العرض Vulkan.",
            ["In game bloom is blown out resulting in the game being hard to see on certain daytime tracks"] =
                "توهج الإضاءة (Bloom) في اللعبة مفرط مما يجعل رؤية اللعبة صعبة في بعض مسارات النهار.",
            ["In game, bloom emitted from light sources (seen in the training level) will flicker. While Strict Rendering Mode is required to fix this issue, using Resolution Scaling is not possible unless you dynamically change it using the Home menu overlay"] =
                "قد يومض توهج الإضاءة في اللعبة، خاصة بمرحلة التدريب. يتطلب الإصلاح تفعيل Strict Rendering Mode، وقد يمنع ذلك تعديل Resolution Scale إلا من قائمة Home أثناء التشغيل.",
            ["In order to use the DLC characters, you need to provide a save from your PS3 with them unlocked by connecting to PSN. If you do not want to do this process, you can use the save [https://www.mediafire.com/file/2s1dtwsyr7cwiho/Tekken_Tag_Tournament_2_save_data.rar/file linked here.]"] =
                "لاستخدام شخصيات DLC، تحتاج إلى توفير ملف حفظ من جهاز PS3 الخاص بك مفتوحة فيه هذه الشخصيات عن طريق الاتصال بـ PSN. إذا كنت لا تريد القيام بهذه العملية، يمكنك استخدام ملف الحفظ [https://www.mediafire.com/file/2s1dtwsyr7cwiho/Tekken_Tag_Tournament_2_save_data.rar/file المرتبط هنا.]",
            ["In some cutscenes, rainbow-colored patterns appear on character's faces and clothing. This is specific to the shader compiler in the proprietary AMD driver (mainly used by Windows users)"] =
                "في بعض المشاهد السينمائية تظهر أنماط بألوان قوس قزح على وجوه الشخصيات وملابسها. هذا خاص بمترجم المظلل (Shader) في تعريف AMD المغلق المصدر (يستخدمه في الغالب مستخدمو Windows).",
            ["In some missions the bloom is too bright, and the characters can be green or red"] =
                "في بعض المهام يكون توهج الإضاءة (Bloom) ساطعا جدا، وقد تظهر الشخصيات باللون الأخضر أو الأحمر.",
            ["In some situations, there are vertex explosions. No setting fixes this issue"] =
                "قد تظهر تشوهات حادة في المجسمات (Vertex Explosions). لا يوجد حاليا إعداد يعالج المشكلة.",
            ["In the Ghosting Mater minigame, the “Scare Points” boost is broken on the game's targeted 60FPS, making the minigame very difficult to complete. This is a bug that occurs on the PS3 and Wii versions of the game and can be fixed by locking the framerate to 30FPS, which can be done using frame limiter or setting the resolution to 1080p as the game caps to 30 when set. The provided patch allows for the minigame to play properly at 60FPS, lower framerates will cause issues if enabled"] =
                "في اللعبة المصغرة Ghosting Mater، تعمل مكافأة «Scare Points» بشكل معطوب على 60 FPS المستهدفة في اللعبة، مما يجعل إكمال اللعبة المصغرة صعبا جدا. هذه علة تحدث في نسختي PS3 وWii ويمكن إصلاحها بقفل معدل الإطارات على 30 FPS، ويمكن فعل ذلك باستخدام frame limiter أو بضبط الدقة على 1080p لأن اللعبة تحدد السقف بـ 30 عند ضبطها. يتيح التعديل المرفق تشغيل اللعبة المصغرة بشكل صحيح على 60 FPS، أما معدلات الإطارات الأقل فستسبب مشاكل إذا فعلت",
            ["In very rare cases the game will softlock during song transitions when playing a setlist (the next song never starts and trying to leave to the main menu causes the game to hang entirely)"] =
                "في حالات نادرة جدا تتجمد اللعبة (softlock) أثناء الانتقال بين الأغاني عند تشغيل setlist (لا تبدأ الأغنية التالية أبدا ومحاولة الخروج إلى القائمة الرئيسية تجعل اللعبة تتعلق بالكامل).",
            ["Inside the path \"BLUS31059\\PS3_GAME\\USRDIR\\autoexec.cfg\" is a config file that you can edit inside of a text editor to set the \"MaxFPS\" value from the default 45 to 0 for unlocked or whatever value you would like to use. This causes no known side effects and allows you to play at 60 fps"] =
                "داخل المسار \"BLUS31059\\PS3_GAME\\USRDIR\\autoexec.cfg\" يوجد ملف إعدادات يمكنك تعديله في محرر نصوص لتغيير قيمة \"MaxFPS\" من الافتراضية 45 إلى 0 لإلغاء الحد، أو أي قيمة تريدها. لا يسبب ذلك أي آثار جانبية معروفة ويتيح لك اللعب على 60 FPS",
            ["Instruments, hair, and other accessories may fly off of the character seemingly at random. This occurs to a lesser degree on real PS3 hardware as well, but is exaggerated in RPCS3. A fix exists for guitars, microphones, hairstyles, and hats created by the Milohax community but it requires modifying the original disc files. [https://guides.milohax.org/en/rb3/teleport/ The patch is available here, along with a visual list of affected cosmetics to avoid in the vanilla game]"] =
                "قد تطير الآلات الموسيقية والشعر والإكسسوارات الأخرى عن الشخصية بشكل يبدو عشوائيا. يحدث ذلك بدرجة أقل على جهاز PS3 الحقيقي أيضا، لكنه مبالغ فيه في RPCS3. يوجد إصلاح للغيتارات والميكروفونات وتسريحات الشعر والقبعات من مجتمع Milohax لكنه يتطلب تعديل ملفات القرص الأصلية. [https://guides.milohax.org/en/rb3/teleport/ التعديل متوفر هنا، مع قائمة مرئية بالعناصر التجميلية المتأثرة التي يجب تجنبها في اللعبة الأصلية]",
            ["Intermittent vertex explosions may also occur while driving vehicles under sunny conditions"] =
                "قد تحدث أيضا تشوهات حادة في المجسمات (Vertex Explosions) متقطعة أثناء قيادة المركبات في الأجواء المشمسة.",
            ["Interpreter isn't a fix all freezes setting for Liberation. The game still has frequent freezes even with currently known configurations"] =
                "Interpreter ليس إعدادا يصلح جميع حالات التجمد في Liberation. لا تزال اللعبة تتجمد كثيرا حتى مع الإعدادات المعروفة حاليا.",
            ["It is best you do not change the frame-limiter while the game is running, as doing so can cause the game to freeze"] =
                "من الأفضل ألا تغير frame-limiter أثناء تشغيل اللعبة، لأن ذلك قد يسبب تجمد اللعبة.",
            ["It is heavily advised to update the game to 1.10 if you're on the disc release as the original 1.00 version has some game breaking issues and graphical bugs that were fixed in later updates. The demo, however, retains these issues and cannot be fixed"] =
                "ينصح بشدة بتحديث اللعبة إلى 1.10 إذا كنت تستخدم إصدار القرص، لأن النسخة الأصلية 1.00 فيها مشاكل تعطل اللعبة وعلل رسومية أصلحت في التحديثات اللاحقة. أما النسخة التجريبية (demo) فتحتفظ بهذه المشاكل ولا يمكن إصلاحها",
            ["It is possible to play user-made edit songs in the game in the emulator. Generally these are the steps to make it work:"] =
                "من الممكن تشغيل الأغاني المعدلة التي صنعها المستخدمون (edit songs) في اللعبة على المحاكي. بشكل عام هذه هي الخطوات لجعل ذلك يعمل:",
            ["It is possible to run the game above 30 FPS by setting VBlank above 60Hz in the Advanced tab. However, animations for certain info banners will be sped up proportionally and the calculation of \"Speed Points\" will be altered, making it exponentially more difficult to complete certain objectives in the game's campaign. A simple solution is to complete the speed point objectives using the default VBlank Frequency"] =
                "من الممكن تشغيل اللعبة بأكثر من 30 FPS بضبط VBlank فوق 60 Hz في تبويب Advanced. لكن ستتسارع حركة بعض لافتات المعلومات بما يتناسب مع ذلك، وسيتغير حساب «Speed Points»، مما يجعل إكمال بعض الأهداف في حملة اللعبة أصعب بشكل متضاعف. الحل البسيط هو إكمال أهداف speed points باستخدام VBlank Frequency الافتراضي",
            ["It's a PSP game, expected to not work, pspemu doesn't work"] =
                "هذه لعبة PSP، ولا يتوقع عملها لأن محاكي PSP الداخلي (pspemu) لا يعمل.",
            ["Jersey and face textures being warped, discolored, or nonexistent"] =
                "تشوه أو تغير لون أو غياب كامل لنسيج القمصان والوجوه.",
            ["Just copy over them, or after deleting ( or renaming ) create multiple copies of blank.bik in MOVIES folder and rename it to the filenames above"] =
                "فقط انسخ فوقها، أو بعد الحذف (أو إعادة التسمية) أنشئ عدة نسخ من blank.bik في مجلد MOVIES وأعد تسميتها بأسماء الملفات المذكورة أعلاه.",
            ["Just like in God of War III, Resolution Scaling above 100% can expose mesh-trimming artifacts, visible as white seams on some geometry and Kratos' face. On supported versions, enable the Disable Mesh Trimming game patch"] =
                "تماما كما في God of War III، قد يكشف Resolution Scaling فوق 100% عن عيوب تقليم الشبكة (mesh-trimming)، وتظهر كخطوط بيضاء على بعض الأشكال الهندسية وعلى وجه Kratos. في الإصدارات المدعومة، فعل تعديل اللعبة Disable Mesh Trimming",
            ["Knit Cap"] =
                "قبعة صوفية (Knit Cap)",
            ["LIMBO runs at 30FPS on console, whereas emulator runs at a higher framerate, that being 60FPS"] =
                "تعمل LIMBO على 30 FPS على الجهاز، بينما يعمل المحاكي بمعدل إطارات أعلى وهو 60 FPS.",
            ["Lair Network confirmed to work, but currently very empty"] =
                "تم تأكيد عمل Lair Network، لكنه فارغ جدا حاليا.",
            ["Large sections of the stands missing"] =
                "أجزاء كبيرة من المدرجات مفقودة.",
            ["Large transparent blue squares flash around an AC when wall-jumping horizontally. This issue is related to the dynamic light emitted by the AC's boosters"] =
                "تومض مربعات زرقاء شفافة كبيرة حول AC عند القفز على الجدار أفقيا (wall-jumping). هذه المشكلة مرتبطة بالإضاءة الديناميكية الصادرة من معززات (boosters) الـ AC.",
            ["Later storyline mission: Player is forced to throw somebody off the ledge by moving Sixaxis forwards. No alternative known at the moment"] =
                "مهمة متأخرة في القصة: يضطر اللاعب إلى رمي شخص من حافة عن طريق تحريك Sixaxis للأمام. لا يوجد بديل معروف حاليا.",
            ["Leaderboards and Stats are not yet available"] =
                "لوحات الصدارة والإحصاءات غير متاحة بعد.",
            ["Leaderboards and online play are confirmed to work, however online play is iffy and sometimes doesn't work"] =
                "تم تأكيد عمل لوحات الصدارة واللعب أونلاين، لكن اللعب أونلاين غير مستقر وأحيانا لا يعمل.",
            ["Leaderboards and online play both work"] =
                "لوحات الصدارة واللعب أونلاين كلاهما يعمل.",
            ["Leaderboards and player replays work"] =
                "لوحات الصدارة وإعادات اللاعبين (player replays) تعمل.",
            ["Leaderboards are confirmed to work"] =
                "تأكد عمل لوحات الصدارة.",
            ["Leaderboards are untested"] =
                "لوحات الصدارة لم تختبر.",
            ["Leaderboards work"] =
                "لوحات الصدارة تعمل.",
            ["Leaderboards work for all 3 game modes"] =
                "لوحات الصدارة تعمل لجميع أوضاع اللعب الثلاثة.",
            ["Leaderboards work, but are currently very empty"] =
                "لوحات الصدارة تعمل، لكنها فارغة جدا حاليا.",
            ["Leaderboards work, online play now works as of 0.0.39"] =
                "لوحات الصدارة تعمل، واللعب أونلاين يعمل الآن اعتبارا من 0.0.39.",
            ["Leaderboards works"] =
                "تعمل لوحات الصدارة.",
            ["Leaderboards works and unlocks the trophy \"International Zombie Hunter.\""] =
                "لوحة الصدارة تعمل وتفتح الكأس \"International Zombie Hunter.\".",
            ["Leaderboards works, matchmaking untested"] =
                "لوحة الصدارة تعمل، والبحث عن لاعبين (Matchmaking) لم تختبر.",
            ["Leave anisotropic filter at auto, otherwise there will be red artifacts"] =
                "اترك Anisotropic Filtering على Auto؛ تغييرها قد يسبب تشوهات رسومية حمراء.",
            ["Level 3: Snipers, Choppers and Wizards - sky is rendered incorrectly"] =
                "المرحلة 3: Snipers, Choppers and Wizards - السماء تعرض بشكل خاطئ.",
            ["Lighting sometimes flickers, especially during shader compilation, which may pose a problem to those with epilepsy"] =
                "الإضاءة ترتعش أحيانا، خصوصا أثناء تجميع المظلل (Shader)، وقد يشكل ذلك مشكلة لمن يعانون من الصرع.",
            ["Lights can be seen through the movie theater building and this occurs on real hardware too. This is not an RPCS3 bug"] =
                "يمكن رؤية الأضواء عبر مبنى دار السينما، وهذا يحدث على الجهاز الحقيقي أيضا. هذه ليست علة في RPCS3.",
            ["Liquids fall through the walls resulting in unplayable levels"] =
                "تتسرب السوائل عبر الجدران مما يجعل المراحل غير قابلة للعب.",
            ["Little Pigtails"] =
                "ضفيرتان صغيرتان (Little Pigtails)",
            ["Loading a save state will cause a server disconnect that cannot be resolved. The game must be launched fresh every time"] =
                "تحميل save state يسبب انقطاع الاتصال بالخادم ولا يمكن حله. يجب تشغيل اللعبة من جديد في كل مرة.",
            ["Lolita Hat"] =
                "قبعة لوليتا (Lolita Hat)",
            ["Make sure the \"ASSASSIN_ISO\" folder is inside your PS3_GAME/USRDIR directory (this folder is required for the game to boot.)"] =
                "تأكد من وجود مجلد \"ASSASSIN_ISO\" داخل المجلد PS3_GAME/USRDIR (هذا المجلد مطلوب لتشغيل اللعبة.).",
            ["Many UI elements do not scale with widescreen. This is seen on the episode select for Sly 2 and 3, cutscenes and mission briefing cutscenes"] =
                "بعض عناصر الواجهة لا تتكيف مع الشاشة العريضة، خاصة شاشة اختيار الفصول في Sly 2 وSly 3 والمشاهد السينمائية ومقاطع شرح المهام.",
            ["Maps using bloom display a flashing checkerboarding pattern across the screen"] =
                "تظهر في الخرائط التي تستخدم توهج الإضاءة (Bloom) مربعات وامضة تشبه رقعة الشطرنج.",
            ["Maps without commentary will crash after a class is selected. Pressing start makes the game try to reconnect to EA online again"] =
                "الخرائط بدون تعليق ستتعطل بعد اختيار الفئة (class). الضغط على start يجعل اللعبة تحاول إعادة الاتصال بـ EA أونلاين مرة أخرى.",
            ["Masculine"] =
                "ذكوري (Masculine)",
            ["Matchmaking and leaderboards work. Private matches currently don't work as players cannot view/accept invites"] =
                "تعمل ميزة البحث عن لاعبين ولوحات الصدارة. المباريات الخاصة لا تعمل حاليا لأن اللاعبين لا يستطيعون عرض الدعوات أو قبولها.",
            ["Matchmaking not tested. Leaderboards currently with no data due to lack of players. Needs testing with at least two players"] =
                "البحث عن لاعبين (Matchmaking) لم تختبر. لوحات الصدارة بلا بيانات حاليا بسبب قلة اللاعبين. تحتاج إلى اختبار مع لاعبين على الأقل.",
            ["Matchmaking works. Private matches do not work due to players not receiving invites. Leaderboard also confirmed to work, but extremely empty"] =
                "يعمل البحث عن لاعبين، لكن المباريات الخاصة لا تعمل بسبب عدم وصول الدعوات. تعمل لوحة الصدارة، إلا أنها شبه فارغة.",
            ["Max XP: Hold R1 and press left, right, right, up, up"] =
                "أقصى XP: امسك R1 واضغط يسار، يمين، يمين، أعلى، أعلى.",
            ["Menu theme and background ingame music stops playing most of the times"] =
                "موسيقى القائمة وموسيقى الخلفية داخل اللعبة تتوقف عن العمل في أغلب الأوقات.",
            ["Menus in this game are tied to the framerate. If the framerate is above or below 60FPS, certain menu elements like the Change Skin menu and the Interface cursor will be affected"] =
                "القوائم في هذه اللعبة مرتبطة بمعدل الإطارات. إذا كان معدل الإطارات أعلى أو أقل من 60 FPS، فستتأثر بعض عناصر القائمة مثل قائمة Change Skin ومؤشر الواجهة (Interface cursor).",
            ["Messy Mop"] =
                "شعر كثيف غير مرتب (Messy Mop)",
            ["Metal Outlaw /w Short Hair"] =
                "قبعة Metal Outlaw مع شعر قصير (Metal Outlaw /w Short Hair)",
            ["Mics currently don't work"] =
                "الميكروفونات لا تعمل حاليا.",
            ["Minor graphical glitch - Darkened polygons on the characters at certain angles"] =
                "علة رسومية بسيطة - مضلعات مظلمة على الشخصيات من زوايا معينة.",
            ["Minor visual bugs (Hair displacement, Bulging eyes, CDLC broken bow animation)"] =
                "علل بصرية بسيطة (إزاحة الشعر، انتفاخ العيون، حركة قوس مكسورة في CDLC).",
            ["Missing dialogue in cutscenes as well as missing dialogue and other sound effects (notably Ratchet's wrench swing and the grind rail sound) in-game. This is a bug that occurs while using the English (UK) system language. Change the system language from English (UK) to English (US) to fix this issue"] =
                "حوار مفقود في المشاهد السينمائية، وحوار ومؤثرات صوتية أخرى مفقودة داخل اللعبة (وأبرزها حركة مفتاح Ratchet وصوت سكة الانزلاق). هذه علة تحدث عند استخدام لغة النظام English (UK). غير لغة النظام من English (UK) إلى English (US) لإصلاح المشكلة",
            ["Mission 04 - Maggia Factories: crashes everytime as soon as Whiplash shows up on the EU version BLES00247. Does not crash on the US version BLUS30134 patch 1.02 - fully playable on the US version"] =
                "المهمة 04 - Maggia Factories: تتعطل في كل مرة بمجرد ظهور Whiplash في النسخة الأوروبية BLES00247. لا تتعطل في النسخة الأمريكية BLUS30134 مع التعديل 1.02 - قابلة للعب بالكامل في النسخة الأمريكية.",
            ["More often than not, when trying to swap one party member for another, the game will crash with a MEM Access Violation error. This crash can be worked around by first removing the party member and then adding the new party member. This issue is being tracked as part of RPCS3 issue #6117. The game also suffers from a few random crashes during gameplay"] =
                "في أغلب الأحيان، عند محاولة استبدال أحد أعضاء الفريق بآخر، تتعطل اللعبة مع خطأ MEM Access Violation. يمكن تجاوز هذا التعطل بإزالة عضو الفريق أولا ثم إضافة العضو الجديد. تتم متابعة هذه المشكلة ضمن مشكلة RPCS3 رقم #6117. كما تعاني اللعبة من بعض التعطلات العشوائية أثناء اللعب",
            ["Most of the adventure worlds turn invisible when using OpenGL, essentially making them unplayable"] =
                "معظم عوالم المغامرة تصبح غير مرئية عند استخدام OpenGL، مما يجعلها غير قابلة للعب فعليا.",
            ["Multi-threaded RSX can also cause the GPU process to hang, even on OpenGL"] =
                "يمكن أن يتسبب Multi-threaded RSX أيضا في تعليق عملية GPU، حتى على OpenGL.",
            ["Music and cutscene audio do not play. Issue also present on SSD-swapped consoles"] =
                "الموسيقى وصوت المشاهد السينمائية لا يعملان. المشكلة موجودة أيضا على الأجهزة التي استبدل فيها الـ SSD.",
            ["NHL Legacy - Be a Pro 0x68005870: - Offensive points - Signed 32 bits - \"\""] =
                "NHL Legacy - Be a Pro 0x68005870: - نقاط الهجوم - عدد صحيح بإشارة، 32 بت - \"\"",
            ["NOTE: Digital copies come pre-updated and do not face this issue"] =
                "ملاحظة: النسخ الرقمية تأتي محدثة مسبقا ولا تواجه هذه المشكلة.",
            ["Needs a full test with 4 or more players. Player can send invites, but not receive them, meaning private matches are not possible. In-game leaderboards have no data due to no entries"] =
                "تحتاج إلى اختبار كامل مع 4 لاعبين أو أكثر. يستطيع اللاعب إرسال الدعوات لكن لا يستطيع استلامها، أي أن المباريات الخاصة غير ممكنة. لوحات الصدارة داخل اللعبة بلا بيانات لعدم وجود مدخلات.",
            ["Needs custom server"] =
                "تحتاج إلى خادم مخصص (custom server).",
            ["New server has been created by PS Rewired"] =
                "تم إنشاء خادم جديد بواسطة PS Rewired.",
            ["Newsie"] =
                "قبعة بائع الصحف (Newsie)",
            ["NieR has pixelated bloom at higher resolutions than native, especially at +200%. To work around this, set your \"Resolution Scale Threshold\" to 512x512. This makes Depth of Field more pixelated"] =
                "NieR فيها bloom مبكسل عند دقات أعلى من الأصلية، خصوصا عند +200%. للالتفاف على ذلك، اضبط \"Resolution Scale Threshold\" على 512x512. وهذا يجعل Depth of Field أكثر تبكسلا.",
            ["Nivida cards can exhibit minor graphical Issues when in-game in certain scenarios due to hardware issues. So Shader Quality needs to be set to low to fix graphical issues occurring"] =
                "قد تظهر على بطاقات Nivida مشاكل رسومية بسيطة داخل اللعبة في بعض السيناريوهات بسبب مشاكل في العتاد. لذلك يجب ضبط Shader Quality على low لإصلاح المشاكل الرسومية الحاصلة.",
            ["No whitelisting. [https://discord.com/invite/edAbSQXWDm Starhawk Revived Discord]"] =
                "لا يوجد whitelisting. [https://discord.com/invite/edAbSQXWDm Starhawk Revived Discord]",
            ["No whitelisting. [https://discord.com/invite/edAbSQXWDm Warhawk Revived Discord]"] =
                "لا يوجد whitelisting. [https://discord.com/invite/edAbSQXWDm Warhawk Revived Discord]",
            ["Non move versions of EyePet will fail to read data before getting ingame"] =
                "النسخ غير الداعمة لـ Move من EyePet ستفشل في قراءة البيانات قبل الدخول إلى اللعبة.",
            ["Non updated version of a game will get stuck on a blackscreen, simply update the game"] =
                "النسخة غير المحدثة من اللعبة ستعلق على شاشة سوداء، ببساطة حدث اللعبة.",
            ["Non-default values can cause vertex explosions"] =
                "قد تسبب القيم غير الافتراضية تشوها شديدا في المجسمات (Vertex Explosions).",
            ["Normally Team Fortress 2 requires you to connect to the EA servers. However, you can play certain maps with a bypass. # Right click The Orange Box in the game list (game, NOT data) and press Open Install Folder. # Go to USRDIR/GAME/TF. # Create a folder named CFG. # Create a file in CFG named MODSETTINGS.CFG with the following: map_commentary MAPNAME exec CONFIG.PS3.CFG MAPNAME must be replaced with a map that has developer commentary:"] =
                "عادة تتطلب Team Fortress 2 الاتصال بخوادم EA. لكن يمكنك لعب بعض الخرائط باستخدام حل التفافي. # انقر بزر الفأرة الأيمن على The Orange Box في قائمة الألعاب (اللعبة، وليس البيانات) واضغط Open Install Folder. # اذهب إلى USRDIR/GAME/TF. # أنشئ مجلدا باسم CFG. # أنشئ ملفا في CFG باسم MODSETTINGS.CFG يحتوي على التالي: map_commentary MAPNAME exec CONFIG.PS3.CFG يجب استبدال MAPNAME بخريطة تحتوي على تعليق المطورين:",
            ["Note that the settings recommended above that mention multiplayer in their notes only apply to the multiplayer mode of the game, the single player mode does not require these settings"] =
                "لاحظ أن الإعدادات الموصى بها أعلاه والتي تذكر multiplayer في ملاحظاتها تنطبق فقط على وضع اللعب الجماعي في اللعبة، أما وضع اللاعب الفردي فلا يتطلب هذه الإعدادات.",
            ["Note: Enabling this patch disables DNS-based overrides (such as Arcadia's IP/Host switches above)"] =
                "ملاحظة: تفعيل هذا التعديل يعطل تجاوزات DNS (مثل إعدادات IP/Host Switches الخاصة بـ Arcadia أعلاه).",
            ["Occasional crashing while in game"] =
                "تعطل عرضي أثناء اللعب.",
            ["Occasional lighting issues"] =
                "مشاكل إضاءة عرضية.",
            ["Occasionally randomly crashes. Thankfully checkpoints are frequent even on Impossible difficulty. Fully playable in singleplayer"] =
                "تتعطل عشوائيا أحيانا. لحسن الحظ نقاط الحفظ (checkpoints) متكررة حتى على صعوبة Impossible. قابلة للعب بالكامل في singleplayer.",
            ["Occasionally, the graphics will break when you pause. This appears to happen at random and is seen as visible normal mapping"] =
                "أحيانا تتعطل الرسوميات عند الإيقاف المؤقت. يبدو أن ذلك يحدث عشوائيا ويظهر على شكل normal mapping مرئي.",
            ["Official servers and music store have been permanently shut down. A custom server exists: [https://github.com/ihatecompvir/GoCentral GoCentral], which has functional RPCN invites, leaderboards, a DNS server to allow crossplay between PS3, Wii, and RPCS3 users, and has nearly one-to-one functionality with the original servers"] =
                "تم إغلاق الخوادم الرسمية ومتجر الموسيقى بشكل دائم. يوجد خادم مخصص: [https://github.com/ihatecompvir/GoCentral GoCentral]، وفيه دعوات RPCN تعمل، ولوحات متصدرين، وخادم DNS يتيح اللعب المشترك بين مستخدمي PS3 وWii وRPCS3، ويوفر وظائف تكاد تطابق الخوادم الأصلية",
            ["Official servers were closed on 2020/8/6"] =
                "أغلقت الخوادم الرسمية في 2020/8/6.",
            ["Older versions of game rely on GameSpy"] =
                "النسخ القديمة من اللعبة تعتمد على GameSpy.",
            ["On M1 Pro, a glitch happens on the game's intro and menu, to sort that you need to change the SPU Decoder to Recompiler (LLVM) and enable VSYNC. Set preffered SPU Threads to 3 and SPU Block Size to Mega to prevent the error \"Access violation writing location 0xffdead00\" [Unmapped Memory]\". The shadow of moving objects/characters are glitching too, Write Color Buffers won't fix"] =
                "على M1 Pro تحدث علة في مقدمة اللعبة والقائمة، ولحلها تحتاج إلى تغيير SPU Decoder إلى Recompiler (LLVM) وتفعيل VSYNC. اضبط preffered SPU Threads على 3 وSPU Block Size على Mega لمنع الخطأ \"Access violation writing location 0xffdead00\" [Unmapped Memory]\". كما أن ظل الأجسام/الشخصيات المتحركة يعاني من علل أيضا، ولن يصلحه Write Color Buffers",
            ["On Mac systems, The intro movie will randomly hang, typically after replaying several times"] =
                "على أنظمة Mac، يتعلق فيديو المقدمة عشوائيا، عادة بعد إعادة تشغيله عدة مرات.",
            ["On systems running macOS, to get past the PPU Trap on the loading screen, find your configuration file (should typically be in ~/Library/Application Support/rpcs3/custom_configs), open it in a text editor, then search for the configuration key named \"Stub PPU Traps\". This value will normally show as 0. Change it to 1 and save the configuration file. The PPU Traps should no longer appear during the loading screen, and during gameplay"] =
                "في macOS، إذا ظهر PPU Trap أثناء التحميل، افتح ملف إعدادات اللعبة (غالبا في ~/Library/Application Support/rpcs3/custom_configs) بمحرر نصوص. ابحث عن Stub PPU Traps وغير قيمته من 0 إلى 1، ثم احفظ الملف. يفترض أن تختفي المشكلة أثناء التحميل واللعب.",
            ["Online game-play works. Can create and join lobbies, send/receive invites and find specific lobbies via Custom Match. PSN Leaderboard is broken. Tested with 6 players. Please note it is suggested to close and re-host a lobby if a player leaves the session to clear any RPCN errors. [https://discord.com/channels/272035812277878785/319224795785068545/1008042296547217520 Host Log] [https://discord.com/channels/272035812277878785/319224795785068545/1008042566371004439 Guest Log]"] =
                "اللعب أونلاين يعمل. يمكن إنشاء الغرف (lobbies) والانضمام إليها، وإرسال/استلام الدعوات، والعثور على غرف محددة عبر Custom Match. لوحة متصدرين PSN معطلة. تم الاختبار مع 6 لاعبين. يرجى ملاحظة أنه يقترح إغلاق الغرفة وإعادة استضافتها إذا غادر لاعب الجلسة لتصفية أي أخطاء RPCN. [https://discord.com/channels/272035812277878785/319224795785068545/1008042296547217520 Host Log] [https://discord.com/channels/272035812277878785/319224795785068545/1008042566371004439 Guest Log]",
            ["Only Hong Kong version is in English"] =
                "النسخة الهونغ كونغية فقط هي الإنجليزية.",
            ["Only the \"Clear Microphone\" doesn't exhibit teleporting out of every available microphone"] =
                "فقط «Clear Microphone» لا يعاني من الانتقال الآني (teleporting) بين جميع الميكروفونات المتاحة.",
            ["Only the demos can be heard playing in the background. Setting \"PPU Decoder\" to \"Interpreter (static)\" allows the games to display correctly, however this reduces performance in DOOM 3"] =
                "لا يسمع إلا العروض التجريبية (demos) تعمل في الخلفية. ضبط \"PPU Decoder\" على \"Interpreter (static)\" يتيح للألعاب العرض بشكل صحيح، لكن ذلك يقلل الأداء في DOOM 3.",
            ["Open Source; Fully public"] =
                "مفتوح المصدر؛ عام بالكامل.",
            ["Open Source; Fully public. Uses Home Laboratory's software | [https://github.com/GitHubProUser67/MultiServer3 Github]"] =
                "مفتوح المصدر؛ عام بالكامل. يستخدم برمجيات Home Laboratory | [https://github.com/GitHubProUser67/MultiServer3 Github]",
            ["Pageboy"] =
                "تسريحة بيج بوي (Pageboy)",
            ["Pause the game and press the directions on the left joystick"] =
                "أوقف اللعبة مؤقتا واضغط على الاتجاهات في عصا التحكم اليسرى.",
            ["Pickelhaube"] =
                "خوذة عسكرية مدببة (Pickelhaube)",
            ["Pipes roll faster in Tokyo Mater"] =
                "تتدحرج الأنابيب أسرع في Tokyo Mater.",
            ["Playable when searching for public games in matchmaking & co-op. Invites are non-functional"] =
                "قابلة للعب عند البحث عن ألعاب عامة في matchmaking والتعاون (co-op). الدعوات لا تعمل.",
            ["Playing levels with water causes the game to freeze after some time. This is common on the Pirates of Caribbean DLC and user levels that use water"] =
                "اللعب في مراحل فيها ماء يسبب تجمد اللعبة بعد مدة. هذا شائع في DLC الخاص بـ Pirates of Caribbean ومراحل المستخدمين التي تستخدم الماء.",
            ["Please note that if you use keyboard as input you won't be able to reach the titlescreen unless you change pressure sensitivity, to allow you to hold L2/R2 triggers at around 75%. The game has a speedometer on the title screen which you must hold trigger down slightly but not fully for a few seconds in order to get into the game. Similarly the game requires trigger sensitivity to play"] =
                "يرجى ملاحظة أنك إذا استخدمت لوحة المفاتيح كأداة إدخال فلن تتمكن من الوصول إلى شاشة العنوان إلا إذا غيرت حساسية الضغط (pressure sensitivity) للسماح لك بإبقاء الزرين L2/R2 عند حوالي 75%. في شاشة العنوان يوجد عداد سرعة يجب أن تضغط عنده على الزر قليلا وليس بالكامل لبضع ثوان للدخول إلى اللعبة. وبالمثل تتطلب اللعبة حساسية الزر (trigger sensitivity) للعب",
            ["Pre-Race and Replay cameras are GPU demanding, you may experience audio stutter and FPS drops in them with increased Resolution Scale. Setting Default Resolution to 1920x1080 will lower GPU requirements, but you will encounter the artefacts mentioned in the Known Issues, which are fixed by Strict Rendering Mode. Regular gameplay is way lighter on the GPU"] =
                "كاميرات ما قبل السباق (Pre-Race) والإعادة (Replay) تتطلب الكثير من GPU، وقد تواجه تقطعا في الصوت وانخفاضا في FPS فيها مع زيادة Resolution Scale. ضبط Default Resolution على 1920x1080 سيخفف متطلبات GPU، لكنك ستواجه العيوب المذكورة في Known Issues، والتي يصلحها Strict Rendering Mode. أما اللعب العادي فهو أخف بكثير على GPU",
            ["Previously, this meant that 3D rendered objects would rarely appear and you would just be left with a skybox and 2D UI elements. After RPCS3 PR #12258 was merged, the graphics improved to where they flicker in and out of existence and are at least somewhat visible. It may be possible to complete the game, but a seizure warning is advised"] =
                "في السابق كان هذا يعني أن الأجسام المرسومة ثلاثية الأبعاد نادرا ما تظهر وتبقى لديك فقط سماء (skybox) وعناصر واجهة ثنائية الأبعاد. بعد دمج طلب دمج RPCS3 رقم #12258، تحسنت الرسوميات إلى أن أصبحت ترتعش بين الظهور والاختفاء وتكون مرئية إلى حد ما على الأقل. قد يكون من الممكن إكمال اللعبة، لكن ينصح بتحذير من نوبات الصرع",
            ["Prince of Persia is more demanding on the GPU than most other RPCS3 titles. Expect to play the game at lower resolution values than other titles to achieve good performance"] =
                "Prince of Persia أكثر طلبا على GPU من معظم عناوين RPCS3 الأخرى. توقع اللعب بقيم دقة أقل من العناوين الأخرى للحصول على أداء جيد.",
            ["Problematic hairstyles and hats"] =
                "تسريحات شعر وقبعات إشكالية.",
            ["Promo events have a much higher chance of crashing with certain graphics settings. It is recommended to keep them all default. Even then, crashes can still occur"] =
                "الأحداث الترويجية (Promo events) أكثر عرضة بكثير للتعطل مع بعض إعدادات الرسوميات. ينصح بإبقائها جميعا على الافتراضي. وحتى مع ذلك قد تحدث تعطلات.",
            ["Public game works, Private and invite don't work. Leaderboard works"] =
                "اللعبة العامة تعمل، أما الخاصة والدعوات فلا تعمل. لوحة الصدارة تعمل.",
            ["Quick match, custom match, Invites work. Sometimes if player 2 disconnects, the host will no longer be \"visible\" and must restart the session"] =
                "Quick match وcustom match والدعوات تعمل. أحيانا إذا انقطع اللاعب 2، لن يعود المضيف \"مرئيا\" ويجب إعادة تشغيل الجلسة.",
            ["RCPN 1.2.4 2024/07/19 You can view online ranking in the leaderboard"] =
                "RCPN 1.2.4 2024/07/19 يمكنك عرض الترتيب أونلاين في لوحة الصدارة.",
            ["RCPN 1.3.0 2024 Oct 11. Can create a lobby but not join"] =
                "RCPN 1.3.0 2024 Oct 11. يمكن إنشاء غرفة (lobby) لكن لا يمكن الانضمام.",
            ["RCPN 1.3.0 2024 Oct 11. Can create a lobby but not join. Attempting to invite softlocks the game with a dimmed screen and no invite GUI"] =
                "RCPN 1.3.0 2024 Oct 11. يمكن إنشاء غرفة (lobby) لكن لا يمكن الانضمام. محاولة الدعوة تجمد اللعبة (softlock) مع شاشة معتمة وبدون واجهة دعوة.",
            ["RCPN 1.3.0 2024 Sept 21. Exiting lobby room will game crash. When searching in any rooms will softlock, same goes for quick match when not found the room"] =
                "RPCN 1.3.0 — 21 سبتمبر 2024. قد تتعطل اللعبة عند مغادرة غرفة الانتظار. ويؤدي البحث عن الغرف أو استخدام Quick Match دون العثور على غرفة إلى تعليق يمنع المتابعة.",
            ["RPCN 1.0 - 2024/01/14. **If you are crashing or freezing when trying to go to \"Play Game\" or \"Mini Games\", you must pre-load all the skins in the \"Help & Options\" menu, video demonstration.** https://i.imgur.com/wJWi7Au.mp4, you can also try clearing the cache by right-clicking the game, going to remove and then \"Remove All Cache\" while still doing the preloading step"] =
                "RPCN 1.0 - 2024/01/14. **إذا كانت اللعبة تتعطل أو تتجمد عند محاولة الذهاب إلى \"Play Game\" أو \"Mini Games\"، فيجب عليك تحميل جميع السكنات مسبقا (pre-load) في قائمة \"Help & Options\"، مع فيديو توضيحي.** https://i.imgur.com/wJWi7Au.mp4، ويمكنك أيضا محاولة مسح الكاش بالنقر بزر الفأرة الأيمن على اللعبة ثم الذهاب إلى remove ثم \"Remove All Cache\" مع الاستمرار في تنفيذ خطوة التحميل المسبق",
            ["RPCN 1.2.0 2024.02.24 - Browser doesn't seem to work"] =
                "RPCN 1.2.0 2024.02.24 - يبدو أن المتصفح (Browser) لا يعمل.",
            ["RPCN 1.2.1 - 2024/02/25 - Replay Upload server not working [https://discord.com/channels/272035812277878785/319224795785068545/1211529164096807023 Log]"] =
                "RPCN 1.2.1 - 2024/02/25 - خادم رفع الإعادات (Replay Upload) لا يعمل [https://discord.com/channels/272035812277878785/319224795785068545/1211529164096807023 Log]",
            ["RPCN 1.2.4 - 2024/03/30 rank match and player match can be started but after loading into level both player hang on \"waiting\" screen, private match invite function freeze the game"] =
                "RPCN 1.2.4 - 2024/03/30 يمكن بدء rank match وplayer match لكن بعد التحميل إلى المرحلة يعلق كلا اللاعبين على شاشة \"waiting\"، ووظيفة الدعوة في المباراة الخاصة تجمد اللعبة.",
            ["RPCN 1.2.4 - 2024/04/07 [https://discord.com/channels/272035812277878785/738838647176036393/1226418170529710112 Link]"] =
                "RPCN 1.2.4 - 2024/04/07 [https://discord.com/channels/272035812277878785/738838647176036393/1226418170529710112 رابط]",
            ["RPCN 1.2.4 - 2024/04/25 - COOP campaign works, versus public match due to lack of minimum players unable to start, private match call invite freeze the game"] =
                "RPCN 1.2.4 - 2024/04/25 - حملة COOP تعمل، أما المباراة العامة versus فلا يمكن بدؤها بسبب عدم وجود الحد الأدنى من اللاعبين، واستدعاء الدعوة في المباراة الخاصة يجمد اللعبة.",
            ["RPCN 1.2.4 - 2024/05/03 give a unable to connect PSN network when enter multiplayer, modified eboot.bin to openspy service still didnt help, same result"] =
                "RPCN 1.2.4 - 2024/05/03 تظهر رسالة تعذر الاتصال بشبكة PSN عند دخول اللعب الجماعي، وتعديل eboot.bin إلى خدمة openspy لم يفد أيضا، والنتيجة نفسها.",
            ["RPCN 1.2.4 2024/05/19 - co-op works, but versus softlock the host upon ready up and match didnt start tho start button pressed. UPNP or Port forwards helps connectivity with this title. [https://discord.com/channels/272035812277878785/319224795785068545/1087087904003739699 Log]"] =
                "RPCN 1.2.4 2024/05/19 - co-op يعمل، لكن في versus تتجمد اللعبة عند المضيف (softlock) عند الضغط على ready وبدء المباراة لم يحدث رغم الضغط على زر start. UPNP أو Port forwards يساعد في الاتصال مع هذه اللعبة. [https://discord.com/channels/272035812277878785/319224795785068545/1087087904003739699 Log]",
            ["RPCN 1.6.0 - Matchmaking and invites work as intended. Time Trials leaderboards also work"] =
                "RPCN 1.6.0 - البحث عن لاعبين (Matchmaking) والدعوات تعمل كما هو مقصود. لوحات متصدرين Time Trials تعمل أيضا.",
            ["RPCN 1.8.6 - Requires custom IP/Hosts switches. Requires game patch. A connection error will appear when first entering the game but it can be safely ignored. Restarting the game will wipe all save data"] =
                "RPCN 1.8.6 - يتطلب إعدادات IP/Hosts Switches مخصصة. يتطلب تعديل للعبة. سيظهر خطأ اتصال عند الدخول إلى اللعبة أول مرة لكن يمكن تجاهله بأمان. إعادة تشغيل اللعبة ستمسح جميع بيانات الحفظ.",
            ["RPCN GUI friend invites not implemented yet"] =
                "دعوات الأصدقاء عبر واجهة RPCN GUI لم تنفذ بعد.",
            ["RPCN Version 1.2.4 - 2024/07/23 access multiplayer require a connection to EA server since its not accessible, should be a third-party server"] =
                "RPCN Version 1.2.4 - 2024/07/23 الوصول إلى اللعب الجماعي يتطلب اتصالا بخادم EA لأنه غير متاح، ويجب أن يكون خادما من طرف ثالث.",
            ["RPCS3 issue #10094: The game will freeze when trying to play the logo intro videos. If you remove the movies folder from the installed game data, the game continues past this stage and works fine"] =
                "مشكلة RPCS3 رقم #10094: ستتجمد اللعبة عند محاولة تشغيل فيديوهات مقدمة الشعارات. إذا أزلت مجلد movies من بيانات اللعبة المثبتة، تتابع اللعبة بعد هذه المرحلة وتعمل بشكل جيد.",
            ["RPCS3 issue #10191: Broken shadows occur depending on the camera position"] =
                "مشكلة RPCS3 رقم #10191: تحدث ظلال معطلة بحسب موقع الكاميرا.",
            ["RPCS3 issue #10330: It is not possible to progress into certain sections of the game because the gate matrix view is broken"] =
                "مشكلة RPCS3 رقم #10330: لا يمكن التقدم إلى بعض أقسام اللعبة لأن عرض gate matrix معطل.",
            ["RPCS3 issue #10494: During a race, some of the game's textures appear as white polygons rather than the actual textures"] =
                "مشكلة RPCS3 رقم #10494: تظهر بعض خامات مضمار السباق على هيئة مضلعات بيضاء بدلا من صورتها الأصلية.",
            ["RPCS3 issue #10657: In Metal Gear Solid 3, there is a cutscene where a jet plane should be shown, but this doesn't render correctly"] =
                "مشكلة RPCS3 رقم #10657: في Metal Gear Solid 3 يوجد مشهد سينمائي يفترض أن تظهر فيه طائرة نفاثة، لكنه لا يعرض بشكل صحيح.",
            ["RPCS3 issue #10985: Portraits of created fighters don't show in the menus"] =
                "مشكلة RPCS3 رقم #10985: صور المقاتلين الذين تم إنشاؤهم لا تظهر في القوائم.",
            ["RPCS3 issue #10994: Only a single texture is displayed at a time"] =
                "مشكلة RPCS3 رقم #10994: تعرض خامة واحدة فقط في كل مرة.",
            ["RPCS3 issue #10998: Player's skin and ground lighting and shadows appear different to what's expected"] =
                "مشكلة RPCS3 رقم #10998: يظهر جلد اللاعب وإضاءة الأرض والظلال بشكل مختلف عن المتوقع.",
            ["RPCS3 issue #11006: When buying a car from regular or used dealership, the game will crash if the SPU Decoder is set to Recompiler (LLVM). This can be avoided by using Recompiler (ASMJIT) or Accurate Xfloat"] =
                "مشكلة RPCS3 رقم #11006: عند شراء سيارة من معرض السيارات العادي أو المستعملة، تتعطل اللعبة إذا كان SPU Decoder مضبوطا على Recompiler (LLVM). يمكن تجنب ذلك باستخدام Recompiler (ASMJIT) أو Accurate Xfloat",
            ["RPCS3 issue #11122: Excessive amounts of bloom occur in versions of RPCS3 since 0.0.17-12650"] =
                "مشكلة RPCS3 رقم #11122: تظهر كمية مفرطة من تأثير توهج الإضاءة (Bloom) في إصدارات RPCS3 منذ 0.0.17-12650.",
            ["RPCS3 issue #11282: Depth of Field renders inaccurately. This is most noticeable in Photo Mode"] =
                "مشكلة RPCS3 رقم #11282: يظهر تأثير عمق المجال (Depth of Field) بشكل غير دقيق، خاصة في وضع التصوير (Photo Mode).",
            ["RPCS3 issue #11390: When playing split-screen multiplayer, a shadow from the left player's vehicle appears on the right-player's screen"] =
                "مشكلة RPCS3 رقم #11390: عند اللعب متعدد اللاعبين بالشاشة المقسمة، يظهر ظل مركبة اللاعب الأيسر على شاشة اللاعب الأيمن.",
            ["RPCS3 issue #11513: Anything that has reflective properties (e.g. floor/mirrors/water) is either broken or missing"] =
                "مشكلة RPCS3 رقم #11513: أي شيء له خصائص عاكسة (مثل الأرضيات/المرايا/الماء) إما أنه معطوب أو مفقود.",
            ["RPCS3 issue #11529: Game freezes either in the opening video or shortly after with a 'sys_mutex_unlock' error"] =
                "مشكلة RPCS3 رقم #11529: تتجمد اللعبة إما في فيديو الافتتاح أو بعده بقليل مع ظهور خطأ 'sys_mutex_unlock'.",
            ["RPCS3 issue #11533: Black textures replace the normal liveries on cars after starting the second race in a session"] =
                "مشكلة RPCS3 رقم #11533: تحل نسيج سوداء محل الطلاءات (liveries) المعتادة للسيارات بعد بدء السباق الثاني في الجلسة.",
            ["RPCS3 issue #11548: A Z-Fighting issue can be observed when using an Nvidia graphics card"] =
                "مشكلة RPCS3 رقم #11548: يمكن ملاحظة مشكلة Z-Fighting عند استخدام بطاقة رسوميات Nvidia.",
            ["RPCS3 issue #11664: Occasionally crashes during end battle sequence. Occasionally menu screens disappear when PPU decoder is set to Static"] =
                "مشكلة RPCS3 رقم #11664: تتعطل اللعبة أحيانا أثناء تسلسل المعركة الأخيرة. وأحيانا تختفي شاشات القوائم عندما يكون PPU decoder مضبوطا على Static.",
            ["RPCS3 issue #11682: The game crashes when continuing a career in the Build a Label mode. Loading into Throw Down before continuing a career prevents this crash"] =
                "مشكلة RPCS3 رقم #11682: تتعطل اللعبة عند متابعة المسيرة (career) في وضع Build a Label. تحميل Throw Down قبل متابعة المسيرة يمنع هذا التعطل.",
            ["RPCS3 issue #11816: Deadstorm Pirates have sound issue, the music may disappear, or maybe the whole sound"] =
                "مشكلة RPCS3 رقم #11816: لعبة Deadstorm Pirates فيها مشكلة في الصوت، فقد تختفي الموسيقى أو ربما الصوت كله.",
            ["RPCS3 issue #11843: In some nighttime scenarios, no headlights emit from the vehicle when the surroundings are in darkness"] =
                "مشكلة RPCS3 رقم #11843: في بعض سيناريوهات الليل، لا تصدر أي أضواء أمامية من المركبة عندما يكون المحيط مظلما.",
            ["RPCS3 issue #11912: The track and scenery intermittently doesn't get illuminated correctly causing everything to appear as if was in shadow. This only affects the bumper-cam view, other cameras are unaffected"] =
                "مشكلة RPCS3 رقم #11912: المسار والمناظر المحيطة لا تضاء بشكل صحيح بصورة متقطعة، مما يجعل كل شيء يبدو وكأنه في الظل. يؤثر هذا فقط على منظور كاميرا الصدام (bumper-cam)، أما الكاميرات الأخرى فلا تتأثر.",
            ["RPCS3 issue #11912: When using the bumper camera, and on the rear-view mirror, lighting and shadows may render incorrectly"] =
                "مشكلة RPCS3 رقم #11912: عند استخدام كاميرا الصدام وفي مرآة الرؤية الخلفية، قد يتم عرض الإضاءة والظلال بشكل غير صحيح.",
            ["RPCS3 issue #12009: The game has issue with the stability in a period of time, even using RSX FIFO Atomic can't fix this"] =
                "مشكلة RPCS3 رقم #12009: اللعبة فيها مشكلة في الاستقرار بعد فترة من الزمن، وحتى استخدام RSX FIFO Atomic لا يصلح ذلك.",
            ["RPCS3 issue #12160: In the Fury expansion pack, the ship models don't display correctly in the menus"] =
                "مشكلة RPCS3 رقم #12160: في حزمة التوسعة Fury، لا تعرض نماذج السفن بشكل صحيح في القوائم.",
            ["RPCS3 issue #12174 - Interactive objects should be highlighted with a blue border, this effect is missing"] =
                "مشكلة RPCS3 رقم #12174 - يفترض أن تبرز الأجسام التفاعلية بحدود زرقاء، وهذا التأثير مفقود.",
            ["RPCS3 issue #12262: Wrong/missing colors on the field as compared to real hardware including the grass appears yellow instead of green; distance line markings, logos etc. appear black instead of the expected colors; end zone is not the right shade of blue"] =
                "مشكلة RPCS3 رقم #12262: ألوان خاطئة/مفقودة في الملعب مقارنة بالجهاز الحقيقي، منها أن العشب يظهر أصفر بدلا من الأخضر؛ وعلامات خطوط المسافة والشعارات وغيرها تظهر سوداء بدلا من الألوان المتوقعة؛ ومنطقة النهاية (end zone) ليست بدرجة الأزرق الصحيحة",
            ["RPCS3 issue #12309: When selecting custom music files for Goal Horns/menu music, the music track plays once but won't then play again the next time that it should"] =
                "مشكلة RPCS3 رقم #12309: عند اختيار ملفات موسيقى مخصصة لأبواق الأهداف (Goal Horns)/موسيقى القوائم، يتم تشغيل المقطع الموسيقي مرة واحدة ثم لا يعمل مرة أخرى في المرة التالية التي يفترض أن يعمل فيها.",
            ["RPCS3 issue #12498: Sometimes during playback of the intro FMV, the video will freeze and lock up the emulator"] =
                "مشكلة RPCS3 رقم #12498: أحيانا أثناء تشغيل فيديو المقدمة (FMV)، يتجمد الفيديو ويعلق المحاكي.",
            ["RPCS3 issue #12514: Black lines appear across the title screen and game menus in RPCS3 versions after v0.0.21-13399"] =
                "مشكلة RPCS3 رقم #12514: تظهر خطوط سوداء عبر شاشة العنوان وقوائم اللعبة في إصدارات RPCS3 التي تلي v0.0.21-13399.",
            ["RPCS3 issue #12567: A number of graphical issues are known including screen flickering, incorrectly rendered reflections and broken anti-aliasing"] =
                "مشكلة RPCS3 رقم #12567: توجد عدة مشاكل رسومية معروفة منها وميض الشاشة، وانعكاسات تعرض بشكل غير صحيح، وتنعيم حواف (anti-aliasing) معطوب.",
            ["RPCS3 issue #12610: In the game's Legacy Mode, the game hangs on the loading screen before the fight when trying to use a custom fighter"] =
                "مشكلة RPCS3 رقم #12610: في وضع Legacy Mode في اللعبة، تتعلق اللعبة في شاشة التحميل قبل القتال عند محاولة استخدام مقاتل مخصص.",
            ["RPCS3 issue #12783: Player shirts in FIFA 14 don't have the player's name on the back"] =
                "مشكلة RPCS3 رقم #12783: قمصان اللاعبين في FIFA 14 لا يظهر عليها اسم اللاعب من الخلف.",
            ["RPCS3 issue #12892: The game will crash when attempting to play the game's career mode ('World Tour Mode')"] =
                "مشكلة RPCS3 رقم #12892: تتعطل اللعبة عند محاولة لعب وضع المسيرة في اللعبة ('World Tour Mode').",
            ["RPCS3 issue #12923 - Without 'Disable vertex cache' setting enabled, sometimes the platfor outline will disappear"] =
                "مشكلة RPCS3 رقم #12923 - دون تفعيل إعداد 'Disable vertex cache'، يختفي أحيانا خط حدود المنصة.",
            ["RPCS3 issue #13036: Several in-game error messages appear when it tries to auto-load saved data on starting the game"] =
                "مشكلة RPCS3 رقم #13036: تظهر عدة رسائل خطأ داخل اللعبة عندما تحاول تحميل البيانات المحفوظة تلقائيا عند بدء اللعبة.",
            ["RPCS3 issue #13072: This happens when you play it for a certain time, making it unstable, using SPU ASMJIT won't fix. SPU Interpreter can prevent crashes, but makes the game run very slow as a result"] =
                "مشكلة RPCS3 رقم #13072: يحدث هذا عند اللعب لفترة معينة، مما يجعل اللعبة غير مستقرة، ولا يصلحه استخدام SPU ASMJIT. يمكن لـ SPU Interpreter منع التعطل، لكنه يجعل اللعبة تعمل ببطء شديد نتيجة لذلك.",
            ["RPCS3 issue #13253: There are graphical glitches (flimmering on helmets + pixelated banners) since RPCS v0.0.26-14569 when both WCB and CPU blit are enabled"] =
                "مشكلة RPCS3 رقم #13253: توجد مشاكل رسومية (وميض على الخوذ ولافتات مبكسلة) منذ RPCS v0.0.26-14569 عند تفعيل كل من WCB وCPU blit معا.",
            ["RPCS3 issue #13291: There is an issue with graphics being duplicated and distorted during matches since RPCS v0.0.26-14570"] =
                "مشكلة RPCS3 رقم #13291: توجد مشكلة في تكرار الرسوميات وتشوهها أثناء المباريات منذ RPCS v0.0.26-14570.",
            ["RPCS3 issue #13301 - The demo version and full game prior to version 1.05 can't make it past the intro FMV's due to an error calculating available disk space to create the autosave data. - Resolved by PR 15313"] =
                "مشكلة RPCS3 رقم #13301 - لا تستطيع النسخة التجريبية (demo) واللعبة الكاملة قبل الإصدار 1.05 تجاوز فيديوهات المقدمة (FMV) بسبب خطأ في حساب مساحة القرص المتاحة لإنشاء بيانات الحفظ التلقائي. - تم حلها بواسطة PR 15313",
            ["RPCS3 issue #13422 - The game freezes unless the debug setting 'Accurate SPU DMA' is enabled"] =
                "مشكلة RPCS3 رقم #13422 - تتجمد اللعبة ما لم يتم تفعيل إعداد التصحيح (debug) 'Accurate SPU DMA'.",
            ["RPCS3 issue #13609: Car reflections appear different when compared to a real PS3 console"] =
                "مشكلة RPCS3 رقم #13609: تبدو انعكاسات السيارات مختلفة مقارنة بجهاز PS3 الحقيقي.",
            ["RPCS3 issue #13680: The graphics appear blurry or as pixelated colors"] =
                "مشكلة RPCS3 رقم #13680: تظهر الرسوميات ضبابية أو على شكل ألوان مبكسلة.",
            ["RPCS3 issue #13680: The graphics show as pixelated colors"] =
                "مشكلة RPCS3 رقم #13680: تظهر الرسوميات على شكل ألوان مبكسلة.",
            ["RPCS3 issue #13922: Blinking white dots appear on screen when Default Resolution is set to 1920x1080. This does not happen during races, only replays, pre-race screens and menus, and thus, does not affect gameplay. Fixed by Strict Rendering Mode"] =
                "مشكلة RPCS3 رقم #13922: تظهر نقاط بيضاء وامضة على الشاشة عندما يكون Default Resolution مضبوطا على 1920x1080. لا يحدث هذا أثناء السباقات، بل فقط في الإعادات وشاشات ما قبل السباق والقوائم، وبالتالي لا يؤثر على اللعب. تم إصلاحه بواسطة Strict Rendering Mode",
            ["RPCS3 issue #13991: When scrolling through the car selection screen, emulation immediately stops when most special machines are highlighted unless PPU Decoder is set to Interpreter (static)"] =
                "مشكلة RPCS3 رقم #13991: عند التنقل في شاشة اختيار السيارة، يتوقف المحاكي فورا عند تحديد معظم الآلات الخاصة ما لم يكن PPU Decoder مضبوطا على Interpreter (static).",
            ["RPCS3 issue #14058 - The water wave effect that appears in some stages (for example the beach stage) is not rendering at all"] =
                "مشكلة RPCS3 رقم #14058 - تأثير موجة الماء الذي يظهر في بعض المراحل (مثل مرحلة الشاطئ) لا يعرض إطلاقا.",
            ["RPCS3 issue #14072: Some objects will start flickering when the camera is at certain angle"] =
                "مشكلة RPCS3 رقم #14072: تبدأ بعض الأجسام بالوميض عندما تكون الكاميرا بزاوية معينة.",
            ["RPCS3 issue #14079: In-race lighting behavior is different compared to hardware"] =
                "مشكلة RPCS3 رقم #14079: سلوك الإضاءة أثناء السباق يختلف مقارنة بالجهاز الحقيقي.",
            ["RPCS3 issue #14113: This issue is present only in RTX GPU's, does not happen with GTX GPU's"] =
                "مشكلة RPCS3 رقم #14113: هذه المشكلة موجودة فقط في بطاقات الرسوم RTX، ولا تحدث مع بطاقات GTX GPU.",
            ["RPCS3 issue #14163: Blood effects only appear during replays and not during gameplay itself"] =
                "مشكلة RPCS3 رقم #14163: تظهر تأثيرات الدم فقط أثناء الإعادات وليس أثناء اللعب نفسه.",
            ["RPCS3 issue #14194: The base game (v1.00) crashes during booting. The only way to fix it is by updating. It happens with the standard version (e.g. BLUS30127)"] =
                "مشكلة RPCS3 رقم #14194: اللعبة الأساسية (v1.00) تتعطل أثناء بدء التشغيل. الطريقة الوحيدة لإصلاحها هي التحديث. يحدث ذلك مع النسخة القياسية (مثل BLUS30127).",
            ["RPCS3 issue #14215: This happens only on NVIDIA GPUs (specifically the GTX series)"] =
                "مشكلة RPCS3 رقم #14215: يحدث هذا فقط على وحدات NVIDIA GPU (وتحديدا سلسلة GTX).",
            ["RPCS3 issue #14216: Minor bug, this affects the story mode, once you finish the Chocolate level, and further levels up to Mob Rules"] =
                "مشكلة RPCS3 رقم #14216: خلل بسيط، يؤثر على وضع القصة، بعد إنهاء مرحلة Chocolate والمراحل التي تليها حتى Mob Rules.",
            ["RPCS3 issue #14238: The game will sometimes freeze on the title screen. Repeated attempts of closing/reopening the emulator and starting the game will sometimes be able to get past this issue"] =
                "مشكلة RPCS3 رقم #14238: تتجمد اللعبة أحيانا في شاشة العنوان. محاولات متكررة لإغلاق المحاكي وإعادة فتحه وبدء اللعبة قد تتمكن أحيانا من تجاوز هذه المشكلة.",
            ["RPCS3 issue #14396: Flickering of colors and bloom effects occur while rotating camera/moving character around"] =
                "مشكلة RPCS3 رقم #14396: تومض الألوان وتأثيرات توهج الإضاءة عند تدوير الكاميرا أو تحريك الشخصية.",
            ["RPCS3 issue #14402: The emulator freezes when trying to start a New Game from main menu. This is due to the game's install data check failing to install the necessary data for the game to function. Importing installed data from a console or using the canary patch allows the game to progress and get in-game. Once the data is installed, you can simply disable the patch"] =
                "مشكلة RPCS3 رقم #14402: يتجمد المحاكي عند محاولة بدء New Game من القائمة الرئيسية. ويعود ذلك إلى فشل فحص بيانات التثبيت في اللعبة في تثبيت البيانات اللازمة لعمل اللعبة. استيراد البيانات المثبتة من جهاز كونسول أو استخدام تعديل Canary يسمح للعبة بالتقدم والدخول إلى اللعب. بعد تثبيت البيانات، يمكنك ببساطة تعطيل التعديل",
            ["RPCS3 issue #14457: With the GAC Pack DLC installed, a fatal error occurs after the initial copyright screen"] =
                "مشكلة RPCS3 رقم #14457: مع تثبيت GAC Pack DLC، يحدث خطأ فادح بعد شاشة حقوق النشر الأولية.",
            ["RPCS3 issue #14572: There are some graphical glitches present, even with recommended configuration settings"] =
                "تتبع مشكلة RPCS3 رقم #14572: تظهر تشوهات رسومية حتى مع الإعدادات الموصى بها.",
            ["RPCS3 issue #14708: When using the detective mode, certain things in the environment do not render correctly"] =
                "مشكلة RPCS3 رقم #14708: عند استخدام وضع المحقق (detective mode)، لا تعرض بعض العناصر في البيئة بشكل صحيح.",
            ["RPCS3 issue #14738: When selecting a playlist for a custom soundtrack, the music doesn't play"] =
                "مشكلة RPCS3 رقم #14738: عند اختيار قائمة تشغيل (playlist) لموسيقى مخصصة، لا تعمل الموسيقى.",
            ["RPCS3 issue #14755: Without \"Delay each odd MFC Command\" setting on, game crashes on boot. With this enabled, the game will freeze at the title screen"] =
                "مشكلة RPCS3 رقم #14755: دون تفعيل إعداد \"Delay each odd MFC Command\"، تتعطل اللعبة عند بدء التشغيل. ومع تفعيله، ستتجمد اللعبة في شاشة العنوان.",
            ["RPCS3 issue #14778: Attempting to go into the player creator mode locks up the game - music continues to play but the game freezes up"] =
                "مشكلة RPCS3 رقم #14778: محاولة الدخول إلى وضع منشئ اللاعب (player creator) تعلق اللعبة - تستمر الموسيقى في العمل لكن اللعبة تتجمد.",
            ["RPCS3 issue #14779: After going in-game, it eventually will crash with a RSX 'Verification failed' crash on load_program_env"] =
                "مشكلة RPCS3 رقم #14779: بعد الدخول إلى اللعب، ستتعطل في النهاية مع تعطل RSX 'Verification failed' في load_program_env.",
            ["RPCS3 issue #14783: While playing a lot of flickering graphics occur"] =
                "مشكلة RPCS3 رقم #14783: يحدث الكثير من وميض الرسوميات أثناء اللعب.",
            ["RPCS3 issue #14792: Glitched texture 'walls' appear in places where they shouldn't, blocking views of the track"] =
                "مشكلة RPCS3 رقم #14792: تظهر خامات مشوهة تشبه الجدران في أماكن غير صحيحة وتحجب رؤية مضمار السباق.",
            ["RPCS3 issue #14833: When shooting a gun, some walls and other scenery flash brightly"] =
                "مشكلة RPCS3 رقم #14833: عند إطلاق النار من سلاح، تومض بعض الجدران والمناظر الأخرى بسطوع شديد.",
            ["RPCS3 issue #14842: A black screen freeze occurs after the final boss cutscene, right before the ending starts"] =
                "مشكلة RPCS3 رقم #14842: يحدث تجمد بشاشة سوداء بعد المشهد السينمائي لزعيم النهاية، قبل بدء النهاية مباشرة.",
            ["RPCS3 issue #14860: The game freezes at the main menu - Resolved by PR 15167"] =
                "مشكلة RPCS3 رقم #14860: تتجمد اللعبة في القائمة الرئيسية - تم حلها بواسطة PR 15167.",
            ["RPCS3 issue #14872: When looking at certain objects from certain camera angles, the most obvious of which are roads, paths, and some structures in the city areas, they start to become discolored or black"] =
                "مشكلة RPCS3 رقم #14872: عند النظر إلى بعض الأجسام من زوايا كاميرا معينة، وأوضحها الطرق والمسارات وبعض المباني في مناطق المدينة، تبدأ بفقدان لونها أو تصبح سوداء.",
            ["RPCS3 issue #14902: The lighting appears extremely bright in-game"] =
                "مشكلة RPCS3 رقم #14902: تبدو الإضاءة ساطعة للغاية داخل اللعبة.",
            ["RPCS3 issue #14927: This game suffers from broken rendering issues during ingame"] =
                "مشكلة RPCS3 رقم #14927: تعاني هذه اللعبة من مشاكل عرض معطوب أثناء اللعب.",
            ["RPCS3 issue #14948: The clothing of characters lags behind the player. Enabling Disable Vertex Cache fixes this"] =
                "مشكلة RPCS3 رقم #14948: ملابس الشخصيات تتأخر عن اللاعب. تفعيل Disable Vertex Cache يصلح ذلك.",
            ["RPCS3 issue #15009: When going into a level, the game's HUD will show correctly but the actual game itself will show as either a black screen or as pixelated colors"] =
                "مشكلة RPCS3 رقم #15009: عند الدخول إلى مرحلة، تظهر واجهة اللعبة (HUD) بشكل صحيح لكن اللعبة نفسها تظهر إما كشاشة سوداء أو كألوان مبكسلة.",
            ["RPCS3 issue #15128: Erroneous flat 'spiky' polygons appear at certain points on certain stages and demos"] =
                "مشكلة RPCS3 رقم #15128: تظهر مضلعات (polygons) مسطحة 'شائكة' خاطئة في نقاط معينة في مراحل ونسخ تجريبية معينة.",
            ["RPCS3 issue #15327: Game will crash on boot with a dead FIFO commands queue error"] =
                "مشكلة RPCS3 رقم #15327: تتعطل اللعبة عند بدء التشغيل بسبب خطأ «dead FIFO commands queue».",
            ["RPCS3 issue #16529: The shadow under Mickey flickers in certain locations"] =
                "مشكلة RPCS3 رقم #16529: الظل تحت Mickey يومض في أماكن معينة.",
            ["RPCS3 issue #16617: The opening cinematic and video background in menu stutter. Disabling Asynchronous Memory Manager in debug tab fixes the issue. Stuttering does not occur when using the 60 FPS patch. If the FPS patch is not used then setting \"VBlank Frequency\" to 120 in Advanced tab and limiting FPS to 30 remedies the issue as well"] =
                "مشكلة RPCS3 رقم #16617: يحدث تقطع في المقطع السينمائي الافتتاحي وفي خلفية الفيديو في القائمة. تعطيل Asynchronous Memory Manager في تبويب debug يصلح المشكلة. لا يحدث التقطع عند استخدام تعديل 60 FPS. وإذا لم يستخدم تعديل FPS، فإن ضبط \"VBlank Frequency\" على 120 في تبويب Advanced وتحديد FPS بـ 30 يعالج المشكلة أيضا",
            ["RPCS3 issue #17007: After pressing start in the title screen, input stops working not allowing you to proceed further"] =
                "مشكلة RPCS3 رقم #17007: بعد الضغط على start في شاشة العنوان، يتوقف الإدخال عن العمل مما يمنعك من المتابعة.",
            ["RPCS3 issue #17008: Game requires PPU Thread set to 3 otherwise it will freeze after trophy installation"] =
                "مشكلة RPCS3 رقم #17008: تتطلب اللعبة ضبط PPU Thread على 3، وإلا ستتجمد بعد تثبيت الكؤوس (trophy).",
            ["RPCS3 issue #17179: The game after playing the intro videos will hang on a black screen, you need to enable the Disable MLAA patch to get into the menus"] =
                "مشكلة RPCS3 رقم #17179: تتجمد اللعبة على شاشة سوداء بعد مقاطع المقدمة؛ يجب تفعيل تعديل Disable MLAA للوصول إلى القوائم.",
            ["RPCS3 issue #17279: During cutscenes, animations will play much faster than intended and causes a desync with the dialogue due to a bug that's more noticeable on emulator. Frame limiters do not fix this issue"] =
                "مشكلة RPCS3 رقم #17279: أثناء المشاهد السينمائية (cutscenes) تعرض الحركات أسرع بكثير من المقصود مما يسبب عدم تزامن مع الحوار، بسبب خلل أكثر وضوحا على المحاكي. محددات الإطارات (Frame limiters) لا تصلح هذه المشكلة",
            ["RPCS3 issue #17732: Small white blob flickering will show up during the stages of the game. PPU Interpreter solves this but the performance will lower tremendously"] =
                "مشكلة RPCS3 رقم #17732: يظهر وميض لبقعة بيضاء صغيرة أثناء مراحل اللعبة. PPU Interpreter يحل هذه المشكلة لكن الأداء ينخفض بشكل كبير جدا.",
            ["RPCS3 issue #17850: Using a specific card (Looming Darkness card) or having a random crash, you have to use PPU Interpreter Static to get more stability, when you done or changing to another game, set it back to default configuration"] =
                "مشكلة RPCS3 رقم #17850: عند استخدام بطاقة معينة (بطاقة Looming Darkness) أو عند حدوث تعطل عشوائي، يجب استخدام PPU Interpreter Static للحصول على استقرار أكثر، وعند الانتهاء أو الانتقال إلى لعبة أخرى أعده إلى الإعداد الافتراضي",
            ["RPCS3 issue #17865: The top area of the screen may sometimes render areas of the game incorrectly. No current fixes available"] =
                "مشكلة RPCS3 رقم #17865: قد تعرض المنطقة العلوية من الشاشة أحيانا أجزاء من اللعبة بشكل غير صحيح. لا توجد حلول حاليا.",
            ["RPCS3 issue #17986: In KH Re: Chain of Memories, When you level up to 99, it causes the bar to glitch"] =
                "مشكلة RPCS3 رقم #17986: في KH Re: Chain of Memories، عند الوصول إلى المستوى 99 يحدث خلل في الشريط.",
            ["RPCS3 issue #18684: Various graphical glitches, including tree textures appearing with black square backgrounds, Anisotropic Filtering causing green lines on the ground, and others"] =
                "مشكلة RPCS3 رقم #18684: مشاكل رسومية متنوعة، منها ظهور نسيج الأشجار بخلفيات مربعة سوداء، وتسبب Anisotropic Filtering في خطوط خضراء على الأرض، وغيرها.",
            ["RPCS3 issue #2262: There are black textures in some places that only happens in cutscenes. There isn't any setting to fix it"] =
                "مشكلة RPCS3 رقم #2262: توجد نسيج سوداء في بعض الأماكن تحدث فقط في المشاهد السينمائية. لا يوجد أي إعداد لإصلاحها.",
            ["RPCS3 issue #4133: The game has a problem with alpha channels, which is why black lines are visible on some backdrops"] =
                "مشكلة RPCS3 رقم #4133: اللعبة فيها مشكلة في قنوات ألفا (alpha channels)، ولهذا تظهر خطوط سوداء على بعض الخلفيات.",
            ["RPCS3 issue #4357: Some wrong colors are displayed on characters textures and there is a lack of bloom effect in certain cutscenes"] =
                "مشكلة RPCS3 رقم #4357: تظهر بعض الألوان الخاطئة على نسيج الشخصيات ويوجد نقص في تأثير توهج الإضاءة (Bloom) في بعض المشاهد السينمائية.",
            ["RPCS3 issue #4433: The emulator will randomly freeze when playing, usually when the game loads or when switching scenes"] =
                "مشكلة RPCS3 رقم #4433: سيتجمد المحاكي عشوائيا أثناء اللعب، وعادة عند تحميل اللعبة أو عند التبديل بين المشاهد.",
            ["RPCS3 issue #5495: Flickering green, white and red artifacts appear instead of the expected textures"] =
                "مشكلة RPCS3 رقم #5495: تظهر عيوب (artifacts) خضراء وبيضاء وحمراء وامضة بدلا من النسيج المتوقعة.",
            ["RPCS3 issue #6229: Sliced effect on the graphics and flickering occurs"] =
                "مشكلة RPCS3 رقم #6229: يحدث تأثير تقطيع في الرسوميات ووميض.",
            ["RPCS3 issue #6270: Some vertex streching can occur and there is an issue with ghosting/trails in the skybox"] =
                "مشكلة RPCS3 رقم #6270: قد تتمدد أجزاء من المجسمات بصورة غير طبيعية (Vertex Stretching)، وتظهر آثار متتابعة للأجسام في خلفية السماء (Skybox).",
            ["RPCS3 issue #6365 - Some games regressed to Loadable \"Tried different settings, but no luck, the game throws this error before rendering of graphics: F {PPU[0x1000005] Thread (UnityMain) [0x000ff458]} class std::runtime_error thrown: Trap! (0xff458)"] =
                "مشكلة RPCS3 رقم #6365 - بعض الألعاب تراجعت إلى حالة Loadable \"جربت إعدادات مختلفة دون جدوى، تظهر اللعبة هذا الخطأ قبل عرض الرسوميات: F {PPU[0x1000005] Thread (UnityMain) [0x000ff458]} class std::runtime_error thrown: Trap! (0xff458)",
            ["RPCS3 issue #6688: The graphics show as pixelated colors"] =
                "مشكلة RPCS3 رقم #6688: تظهر الرسوميات على شكل ألوان مبكسلة.",
            ["RPCS3 issue #7041: In sections of the game, the graphics appear pixelated even with WCB enabled"] =
                "مشكلة RPCS3 رقم #7041: في بعض أجزاء اللعبة تظهر الرسوميات مبكسلة حتى مع تفعيل WCB.",
            ["RPCS3 issue #7971: When playing the player phases through branches and objects that are normally supposed to be walked on"] =
                "مشكلة RPCS3 رقم #7971: عند اللعب، يخترق اللاعب الأغصان والأجسام التي يفترض عادة أن يمشي عليها.",
            ["RPCS3 issue #8442: The grass in the distance is not rendered correctly"] =
                "مشكلة RPCS3 رقم #8442: العشب في البعيد لا يعرض بشكل صحيح.",
            ["RPCS3 issue #8449: Crowd graphics are not rendered"] =
                "مشكلة RPCS3 رقم #8449: رسوميات الحشود لا تعرض.",
            ["RPCS3 issue #8672: On MacOS, the game's graphics appear sliced vertically"] =
                "مشكلة RPCS3 رقم #8672: على MacOS، تظهر رسوميات اللعبة مقطعة عموديا.",
            ["RPCS3 issue #8939: Snow on the ground in the stage featuring it disappears during the fight"] =
                "مشكلة RPCS3 رقم #8939: الثلج على الأرض في المرحلة التي يظهر فيها يختفي أثناء القتال.",
            ["RPCS3 issue #9150: Minor lightning bugs with some effects such as when the Ghost bird appears"] =
                "مشكلة RPCS3 رقم #9150: أخطاء إضاءة (lighting) بسيطة مع بعض التأثيرات مثل ظهور الطائر الشبح (Ghost bird).",
            ["RPCS3 issue #9650: In the Attract Mode demo, the CPU-controlled cars don't follow the expected path and end up crashing into walls"] =
                "مشكلة RPCS3 رقم #9650: في عرض Attract Mode التجريبي، لا تتبع السيارات التي يتحكم بها CPU المسار المتوقع وينتهي بها الأمر بالاصطدام بالجدران.",
            ["RPCS3 issue #9885: The game is very unstable and will crash constantly while loading and during races"] =
                "مشكلة RPCS3 رقم #9885: اللعبة غير مستقرة جدا وتتعطل باستمرار أثناء التحميل وأثناء السباقات.",
            ["RPCS3 will instantly close itself upon playing through the level Camelot Falls with the dash move"] =
                "سيغلق RPCS3 نفسه فورا عند اجتياز المرحلة Camelot Falls باستخدام حركة الاندفاع (dash).",
            ["Rain reflections on wet terrain also do not render correctly, and certain objects may at times appear translucent or render with minor artifacting at object edges that are in front of the sky"] =
                "انعكاسات المطر على التضاريس المبللة أيضا لا تعرض بشكل صحيح، وقد تبدو بعض الأجسام أحيانا شفافة أو تعرض بعيوب بسيطة (artifacts) على حواف الأجسام التي تكون أمام السماء.",
            ["Random occurring crashes/freezes can happen on weaker hardware. Just restart the game and keep playing"] =
                "قد تحدث تعطلات/تجمدات عشوائية على الأجهزة الأضعف. فقط أعد تشغيل اللعبة وواصل اللعب.",
            ["Ranked not tested. Trying to use chat feature will soft-lock your game"] =
                "لم يختبر اللعب المصنف (Ranked). استخدام الدردشة قد يعلق اللعبة ويمنع متابعة اللعب.",
            ["Raspberry Cap"] =
                "قبعة توتية (Raspberry Cap)",
            ["Refer to the special notes if you accept the risk, and wish to play with correct graphics"] =
                "ارجع إلى الملاحظات الخاصة إذا كنت تقبل المخاطرة وترغب في اللعب برسوميات صحيحة.",
            ["Replay speed is tied to the framerate of the original gameplay. Although the game targets an unlocked 60FPS, unstable performance causes replays to speed up or slow down. This issue was common on original hardware due to frequent frame drops. If you want to have replays with consistent speed, lock the framerate to whatever your hardware can handle during gameplay"] =
                "سرعة الإعادة (Replay) مرتبطة بمعدل الإطارات في اللعب الأصلي. ورغم أن اللعبة تستهدف 60 FPS غير مقفلة، فإن الأداء غير المستقر يجعل الإعادات تتسارع أو تتباطأ. كانت هذه المشكلة شائعة على الجهاز الأصلي بسبب كثرة انخفاض الإطارات. إذا أردت إعادات بسرعة ثابتة، فاقفل معدل الإطارات على أي قيمة يتحملها جهازك أثناء اللعب",
            ["Requires Bypass sceNpCommerce patch. Gets stuck in loading the match after both player get ready"] =
                "يتطلب تعديل Bypass sceNpCommerce. وقد تتجمد اللعبة في شاشة تحميل المباراة بعد استعداد اللاعبين.",
            ["Requires GameSpy"] =
                "يتطلب GameSpy.",
            ["Requires GameSpy. OpenSpy has bare minimum PS3 support"] =
                "يتطلب GameSpy. OpenSpy لديه دعم PS3 في حده الأدنى فقط.",
            ["Requires GameSpy. Openspy doesn't support PS3 enough for this game to work"] =
                "تتطلب GameSpy. Openspy لا يدعم PS3 بما يكفي لتعمل هذه اللعبة.",
            ["Requires Online MP DLC"] =
                "تتطلب إضافة اللعب الجماعي عبر الإنترنت (Online MP DLC).",
            ["Requires OpenSpy patch"] =
                "تتطلب تعديل OpenSpy.",
            ["Requires OpenSpy patch and a custom pkg. Custom pkgs are provided and frequently updated via SARPBC+ Discord server: https://discord.gg/qzDfqkaVTB"] =
                "يتطلب تعديل OpenSpy وحزمة PKG مخصصة. تتوفر الحزم وتحدث باستمرار عبر مجتمع SARPBC+ على Discord: https://discord.gg/qzDfqkaVTB",
            ["Requires OpenSpy patch. Matching only works through invites and RPCN names are not displayed properly"] =
                "يتطلب تعديل OpenSpy. لا يعمل البحث عن لاعبين إلا عبر الدعوات، كما لا تظهر أسماء مستخدمي RPCN بصورة صحيحة.",
            ["Requires OpenSpy patch. Quick match doesn't work but you are able to join via custom match. Player username shows up as Luchador"] =
                "يتطلب تعديل OpenSpy. المباراة السريعة (Quick Match) لا تعمل لكن يمكنك الانضمام عبر مباراة مخصصة. يظهر اسم مستخدم اللاعب باسم Luchador.",
            ["Requires OpenSpy patch. Unable to find eachother lobby nor invite"] =
                "يتطلب تعديل OpenSpy. لا يمكن العثور على ردهة (lobby) الطرف الآخر ولا دعوته.",
            ["Requires Uplay Passport to connect online. You can free drive, use the director mode, and upload times in the \"Blast in the Past\" challenge (accessing leaderboard is prone to softlock)"] =
                "يتطلب Uplay Passport للاتصال بالإنترنت. يمكنك القيادة الحرة، واستخدام وضع المخرج (director mode)، ورفع الأوقات في تحدي \"Blast in the Past\" (الوصول إلى لوحة الصدارة عرضة للتعليق soft-lock).",
            ["Requires `irekej Custom Server` patch to play online"] =
                "يتطلب تعديل `irekej Custom Server` للعب عبر الإنترنت.",
            ["Requires `irekej Custom Server` patch to play online Can also be played Online through a hidden LAN Mode - [https://discord.gg/gt-online-community-839257361486708816 More info here.]"] =
                "يتطلب تعديل `irekej Custom Server` للعب عبر الإنترنت. ويمكن أيضا استخدام وضع LAN المخفي: [https://discord.gg/gt-online-community-839257361486708816 مزيد من التفاصيل].",
            ["Requires a DNS or a custom IP Host Switch. Features cross-play with PS3 users"] =
                "يتطلب DNS أو مفتاح Host IP مخصص (IP Host Switch). يدعم اللعب المشترك بين المنصات (cross-play) مع مستخدمي PS3.",
            ["Requires a DNS or a custom IP Host Switch. UPlay Passport is required for the online co-op modes. Features cross-play with PS3 users"] =
                "يتطلب DNS أو مفتاح Host IP مخصص (IP Host Switch). يتطلب UPlay Passport لأوضاع التعاون (co-op) عبر الإنترنت. يدعم اللعب المشترك بين المنصات (cross-play) مع مستخدمي PS3.",
            ["Requires custom DNS and patch enabled"] =
                "يتطلب DNS مخصصا وتفعيل التعديل.",
            ["Requires custom DNS or IP/Hosts switches"] =
                "يتطلب DNS مخصصا أو إعدادات IP/Hosts Switches.",
            ["Requires custom DNS. More info"] =
                "يتطلب DNS مخصصا. مزيد من المعلومات.",
            ["Requires custom IP/Hosts switches"] =
                "تتطلب إعدادات IP/Hosts Switches مخصصة.",
            ["Requires custom IP/Hosts switches. (Up Your Arsenal)"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. (Up Your Arsenal).",
            ["Requires custom IP/Hosts switches. If unable to log in initially, wait 10-15 seconds on the start screen for the cutscene to start before retrying"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. إذا تعذر تسجيل الدخول في البداية، انتظر 10-15 ثانية في شاشة البداية حتى يبدأ المشهد السينمائي قبل إعادة المحاولة.",
            ["Requires custom IP/Hosts switches. Leaderboards uses SceNpScore"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. لوحات الصدارة تستخدم SceNpScore.",
            ["Requires custom IP/Hosts switches. May require several attempts to log in"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. قد يتطلب تسجيل الدخول عدة محاولات.",
            ["Requires custom IP/Hosts switches. Only Onslaught DLC is playable. [https://github.com/valters-tomsons/arcadia Custom server]"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. فقط Onslaught DLC قابل للعب. [https://github.com/valters-tomsons/arcadia خادم مخصص]",
            ["Requires custom IP/Hosts switches. PSORG requires you to enable the custom RPCS3 patch via our patch manager as SVO does not work"] =
                "تتطلب اللعبة إعدادات مخصصة ضمن IP/Hosts Switches. كما يتطلب خادم PSORG تفعيل تعديل RPCS3 الخاص به من مدير التعديلات، لأن SVO لا يعمل.",
            ["Requires custom IP/Hosts switches. Ranked matchmaking is currently not implemented on either server, however statistics are available on PSRewired. The v1.7 patch which enables Vibration & DLC map pack support is available on both servers"] =
                "تتطلب اللعبة إعدادات IP/Hosts Switches مخصصة. البحث عن مباريات مصنفة غير مدعوم حاليا على الخادمين، لكن الإحصاءات متاحة على PSRewired. يتوفر التعديل v1.7 لتفعيل الاهتزاز ودعم خرائط DLC على كلا الخادمين.",
            ["Requires custom IP/Hosts switches. Requires game patch"] =
                "تتطلب إعدادات مخصصة ضمن IP/Hosts Switches وتعديلا للعبة.",
            ["Requires custom IP/Hosts switches. The 3.1 patch allows for DLC to be used, while also supporting rumble. Both servers provide the update to be installed"] =
                "تتطلب اللعبة إعدادات IP/Hosts Switches مخصصة. يتيح التعديل 3.1 استخدام DLC مع دعم الاهتزاز (Rumble). ويوفر الخادمان هذا التحديث للتثبيت.",
            ["Requires custom IP/Hosts switches. [https://thearchstones.com/serverinfo.html More info] Alternatively players can host their own [https://github.com/ymgve/desse server.]"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. [https://thearchstones.com/serverinfo.html مزيد من المعلومات] وبدلا من ذلك يمكن للاعبين استضافة [https://github.com/ymgve/desse خادمهم الخاص.]",
            ["Requires custom IP/Hosts switches. [https://wiki.aiwarehouse.xyz/en/tfc-dotm-ps3-guide More info]"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. [https://wiki.aiwarehouse.xyz/en/tfc-dotm-ps3-guide مزيد من المعلومات]",
            ["Requires custom IP/Hosts switches. [https://wiki.aiwarehouse.xyz/en/tfc-foc-ps3-guide More info]"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. [https://wiki.aiwarehouse.xyz/en/tfc-foc-ps3-guide مزيد من المعلومات]",
            ["Requires custom IP/Hosts switches. [https://wiki.aiwarehouse.xyz/en/tfc-wfc-ps3-guide More info]"] =
                "يتطلب إعدادات IP/Hosts Switches مخصصة. [https://wiki.aiwarehouse.xyz/en/tfc-wfc-ps3-guide مزيد من المعلومات]",
            ["Requires custom server: [https://hallofmeat.net custom server(WIP) page]"] =
                "يتطلب خادما مخصصا: [https://hallofmeat.net صفحة الخادم المخصص (قيد التطوير)]",
            ["Requires online pass to play online"] =
                "يتطلب online pass للعب أونلاين.",
            ["Requires online pass. You may need to turn your NAT on your router to open, but you can just invite them instead if you don't wanna do that"] =
                "يتطلب online pass. قد تحتاج إلى فتح NAT على الراوتر، لكن يمكنك ببساطة دعوتهم بدلا من ذلك إذا لم ترغب بذلك.",
            ["Requires specific PKG"] =
                "يتطلب PKG محددا.",
            ["Requires specific PKG and DNS"] =
                "يتطلب PKG وDNS محددين.",
            ["Requires specific build, PKG and DNS or specific PKG and DNS"] =
                "يتطلب إصدارا (build) محددا وPKG وDNS، أو PKG وDNS محددين.",
            ["Resolution Scale Threshold: 720 x 720 - Fixes graininess if also using Resolution Scale"] =
                "Resolution Scale Threshold: 720 x 720 - يصلح التحبب (graininess) عند استخدام Resolution Scale أيضا.",
            ["Resolution Scale is not compatible with this title. Use FSR1 as an alternative"] =
                "Resolution Scale غير متوافق مع هذه اللعبة. استخدم FSR1 كبديل.",
            ["Resolution Scale values other than 300% or 500% will cause excessive vertex explosions"] =
                "استخدام قيمة غير 300% أو 500% في Resolution Scale قد يسبب تشوها شديدا في المجسمات (Vertex Explosions).",
            ["Resolution Scaling does not work with this title"] =
                "Resolution Scaling لا يعمل مع هذه اللعبة.",
            ["Resolution Scaling doesn't work properly unless the Disable MLAA patch is enabled. Game patches can be enabled via RPCS3's Patch Manager (go to Manage > Game Patches). The game is also quite RSX intensive and even an RTX 3090 loses a bit of performance at 4K (300% scaling). Due to this, most users should stay at 200% or below if they want the best performance. At values above 100%, mesh trimming can appear as white seams on some geometry, especially Kratos' face. On supported versions, enable the Disable Mesh Trimming game patch"] =
                "Resolution Scaling لا يعمل بشكل صحيح ما لم يتم تفعيل تعديل Disable MLAA. يمكن تفعيل تعديلات الألعاب عبر Patch Manager في RPCS3 (اذهب إلى Manage > Game Patches). كما أن اللعبة تستهلك RSX بكثافة، وحتى RTX 3090 يفقد قليلا من الأداء عند 4K (تحجيم 300%). لذلك ينبغي لمعظم المستخدمين البقاء عند 200% أو أقل إذا أرادوا أفضل أداء. عند قيم أعلى من 100%، قد يظهر حذف المضلعات البعيدة (Mesh Trimming) كدرزات بيضاء على بعض الأشكال الهندسية، وخصوصا وجه Kratos. في الإصدارات المدعومة، فعل game patch الـ Disable Mesh Trimming",
            ["Resolution scaling does not work for this title"] =
                "Resolution scaling لا يعمل مع هذه اللعبة.",
            ["Retail : [https://drive.google.com/file/d/1cl_CkMdB7C0VHMb_do6MDkelsxaRcOiF/view Specific PKG download]"] =
                "Retail : [https://drive.google.com/file/d/1cl_CkMdB7C0VHMb_do6MDkelsxaRcOiF/view تنزيل PKG محدد]",
            ["Retail : [https://pshomeologylab.net/release/1.86/retail/rpcs3/HOMEOLOGYLAB_RETAIL_RPCS3_1.86_LATEST.pkg Specific PKG Download] | QA : [https://pshomeologylab.net/release/1.86/qa/rpcs3/HOMEOLOGYLAB_QA_RPCS3_1.86_LATEST.pkg Specific PKG Download]"] =
                "Retail : [https://pshomeologylab.net/release/1.86/retail/rpcs3/HOMEOLOGYLAB_RETAIL_RPCS3_1.86_LATEST.pkg تنزيل PKG محدد] | QA : [https://pshomeologylab.net/release/1.86/qa/rpcs3/HOMEOLOGYLAB_QA_RPCS3_1.86_LATEST.pkg تنزيل PKG محدد]",
            ["Retail : [https://psorg-web-revival.us/cdn/EP9000/NPIA00005_00/EP9000-NPIA00005_00-PSORGHOMERPCS300-A0186-V0101.pkg Specific PKG download]"] =
                "Retail : [https://psorg-web-revival.us/cdn/EP9000/NPIA00005_00/EP9000-NPIA00005_00-PSORGHOMERPCS300-A0186-V0101.pkg تنزيل PKG محدد]",
            ["Retail: [https://drive.google.com/file/d/1xqvQ0SPudSfC9SPxiiL17LgSv1smZuvW/view Specific PKG Download]"] =
                "Retail: [https://drive.google.com/file/d/1xqvQ0SPudSfC9SPxiiL17LgSv1smZuvW/view تنزيل PKG محدد]",
            ["Revived by PS3 REBORN. Requires a DNS or a custom IP switch. Features cross-play with PS3 users"] =
                "أعيد إحياؤها بواسطة PS3 REBORN. تتطلب DNS أو مفتاح IP مخصص. تدعم اللعب المشترك بين المنصات (cross-play) مع مستخدمي PS3.",
            ["Revived by PS3 REBORN. Requires a custom IP Host Switch: onlineconfigservice.ubi.com==85.90.247.249"] =
                "أعيد إحياؤها بواسطة PS3 REBORN. تتطلب مفتاح IP Host مخصص: onlineconfigservice.ubi.com==85.90.247.249.",
            ["Right quarter of the screen will be completely black when screen distortion effects occur, such as when at low health or affected by stun grenade"] =
                "يصبح الربع الأيمن من الشاشة أسود بالكامل عند حدوث تأثيرات تشويه الشاشة، مثل عند انخفاض الصحة أو التأثر بقنبلة الصعق (stun grenade).",
            ["Rough VRAM estimates with Anti-Aliasing Disabled:"] =
                "تقديرات تقريبية لـ VRAM مع تعطيل Anti-Aliasing:",
            ["Rough VRAM estimates with Anti-Aliasing Enabled:"] =
                "تقديرات تقريبية لـ VRAM مع تفعيل Anti-Aliasing:",
            ["Same as the some other frostibite games simillar to RPCS3 issue #11463"] =
                "نفس ما يحدث في بعض ألعاب frostibite الأخرى المشابهة لـ مشكلة RPCS3 رقم #11463.",
            ["Saving is currently broken for this title. if you try to load a saved game your save will not show up. Try to use savestates if it is possible"] =
                "الحفظ معطل حاليا في هذه اللعبة. إذا حاولت تحميل لعبة محفوظة فلن يظهر حفظك. حاول استخدام savestates إن أمكن.",
            ["Second tutorial mission: Forced motion movement can be skipped by dragging the person to the grave. This will skip the entire tutorial section"] =
                "المهمة التعليمية الثانية: يمكن تخطي حركة الحركة الإجبارية (Forced motion) بسحب الشخص إلى القبر. سيؤدي ذلك إلى تخطي قسم التدريب بأكمله.",
            ["See IP/Host switches below"] =
                "انظر إعدادات IP/Host Switches أدناه.",
            ["See RPCS3 issue #3959 for further details"] =
                "راجع مشكلة RPCS3 رقم #3959 لمزيد من التفاصيل.",
            ["Semi Closed Source; Currently in Open Beta"] =
                "مصدر مغلق جزئيا؛ حاليا في Open Beta.",
            ["Servers shut down March 20, 2014. P2P multiplayer elements fully functional with RPCN"] =
                "تم إغلاق الخوادم في 20 March 2014. عناصر اللعب الجماعي P2P تعمل بشكل كامل مع RPCN.",
            ["Servers shut down March 31, 2024. P2P multiplayer elements fully functional with RPCN"] =
                "تم إغلاق الخوادم في 31 March 2024. عناصر اللعب الجماعي P2P تعمل بشكل كامل مع RPCN.",
            ["Servers were shut down in 2011"] =
                "تم إغلاق الخوادم في 2011.",
            ["Servers were shut down on 30 January 2019"] =
                "تم إغلاق الخوادم في 30 January 2019.",
            ["Servers were shutdown on April 8th, 2014"] =
                "تم إغلاق الخوادم في 8 April 2014.",
            ["Set DNS to 142.171.43.167"] =
                "اضبط DNS على 142.171.43.167.",
            ["Setting Frame Limiter to Off allows the game to run uncapped, but high framerates will break various parts of the game. These include:"] =
                "ضبط Frame Limiter على Off يسمح للعبة بالعمل دون حد أقصى، لكن معدلات الإطارات العالية ستعطل أجزاء متعددة من اللعبة. ومنها:",
            ["Setting Frame Limiter to Off unlocks the framerate"] =
                "ضبط Frame Limiter على Off يفتح قفل معدل الإطارات.",
            ["Setting Framelimit to Off fully unlocks the framerate. However high framerates breaks the world map cursor. "] =
                "ضبط Framelimit على Off يفتح قفل معدل الإطارات بالكامل. لكن معدلات الإطارات العالية تعطل مؤشر خريطة العالم. .",
            ["Setting Vblank Rate at 120hz allows the game to run at 60FPS, however some elements of the game are affected including:"] =
                "ضبط Vblank Rate على 120 Hz يسمح للعبة بالعمل على 60 FPS، لكن بعض عناصر اللعبة تتأثر ومنها:",
            ["Setting Vblank Rate to 120hz allows the game to run at 60FPS"] =
                "ضبط Vblank Rate على 120 Hz يتيح للعبة العمل بـ 60 FPS.",
            ["Setting Vblank Rate to 120hz allows the game to run at 60FPS, however this causes textures to flicker in Toy Box Mode. If you do not want to deal with this issue, set the Vblank Rate back to default"] =
                "ضبط Vblank Rate على 120 Hz يتيح للعبة العمل بسرعة 60 FPS، لكن هذا يسبب وميض في الخامات (textures) في وضع Toy Box Mode. إذا لم تكن تريد التعامل مع هذه المشكلة، أعد Vblank Rate إلى القيمة الافتراضية.",
            ["Setting Vblank Rate to 120hz allows the game to run at 60FPS. Use the Unlock FPS patch to go higher"] =
                "ضبط Vblank Rate على 120 Hz يتيح للعبة العمل بسرعة 60 FPS. استخدم تعديل Unlock FPS للوصول إلى معدل أعلى.",
            ["Setting Vblank frequency to 120HZ allows the game to run at 60FPS. Going higher causes frame pacing issues"] =
                "ضبط تردد Vblank على 120HZ يتيح للعبة العمل بسرعة 60 FPS. الرفع إلى أعلى من ذلك يسبب مشاكل في تناسق الإطارات (frame pacing).",
            ["Setting the Frame Limiter to Infinite allows the game to unlock the framerate. This can cause some issues with the physics"] =
                "ضبط Frame Limiter على Infinite يتيح للعبة فتح قفل معدل الإطارات. قد يسبب هذا بعض المشاكل في الفيزياء.",
            ["Setting the Vblank Rate to 120hz allows the game to run at 60FPS"] =
                "ضبط Vblank Rate على 120 Hz يتيح للعبة العمل بسرعة 60 FPS.",
            ["Shadows appear too opaque suffer and from heavy flickering"] =
                "تظهر الظلال معتمة أكثر من اللازم وتعاني من وميض شديد.",
            ["Shimmering/flickering around edges of the screen can sometimes occur when running at the default 30 FPS cap. Enabling the Unlock FPS patch and setting VBlank = 120 (to allow 60 FPS) resolves this issue"] =
                "قد يحدث أحيانا لمعان/وميض حول حواف الشاشة عند التشغيل على الحد الافتراضي 30 FPS. تفعيل تعديل Unlock FPS وضبط VBlank = 120 (للسماح بـ 60 FPS) يحل هذه المشكلة.",
            ["Short Spikes"] =
                "ارتفاعات قصيرة مفاجئة (Short Spikes).",
            ["Shows \"There was a network error\" when going into multiplayer"] =
                "تظهر رسالة «There was a network error» عند الدخول إلى اللعب الجماعي.",
            ["Skip Intros: You can skip intros by changing the files below with [https://github.com/esc0rtd3w/blank-intro-videos/raw/master/blank.bik this]"] =
                "تخطي المقدمات: يمكنك تخطي المقدمات بتغيير الملفات أدناه باستخدام [https://github.com/esc0rtd3w/blank-intro-videos/raw/master/blank.bik هذا]",
            ["Sleek Mop"] =
                "شعر كثيف مصفف (Sleek Mop)",
            ["Slim Cognito on Aquatos will give you the weapons for free if you have purchased them on RaC2, some of these weapons include the Lava Gun and the Bouncer"] =
                "Slim Cognito في Aquatos سيعطيك الأسلحة مجانا إذا كنت قد اشتريتها في RaC2، ومن هذه الأسلحة Lava Gun وBouncer.",
            ["Sly 1 received an recomposed soundtrack where most of the original tracks have extra instruments and beats added. This effects the Mz. Ruby fight and can mess up the button rhythm since the same track was also updated and not accounted for this minigame"] =
                "حصلت Sly 1 على موسيقى تصويرية معاد توزيعها، حيث أضيفت إلى معظم المقطوعات الأصلية آلات وإيقاعات إضافية. هذا يؤثر على قتال Mz. Ruby وقد يفسد إيقاع الأزرار، لأن المقطوعة نفسها حدثت أيضا ولم يراع ذلك في هذه اللعبة المصغرة",
            ["Smoke and fog shaders have rendering issues though doesn't impact gameplay"] =
                "تظهر مشاكل في عرض شيدرات الدخان والضباب، لكنها لا تؤثر على اللعب.",
            ["Some cutscenes featuring Lucifer and Beatrice don't render correctly RPCS3 issue #7308"] =
                "بعض المشاهد السينمائية التي تظهر فيها Lucifer وBeatrice لا تعرض بشكل صحيح مشكلة RPCS3 رقم #7308.",
            ["Some cutscenes slow down massively (<5fps) despite seeming to be pre-rendered"] =
                "تبطؤ بعض المشاهد السينمائية بشكل كبير (<5 FPS) رغم أنها تبدو مصيرة مسبقا.",
            ["Some elements in the game do not render such as water in Tailfin Pass and Casa Della Tires interior"] =
                "بعض عناصر اللعبة لا تعرض، مثل الماء في Tailfin Pass وداخل Casa Della Tires.",
            ["Some extra performance can be obtained by setting SPU XFloat Accuracy to Relaxed but be aware of the following issue:"] =
                "يمكن الحصول على أداء إضافي بضبط SPU XFloat Accuracy على Relaxed، لكن انتبه للمشكلة التالية:",
            ["Some objects occasionally flicker with white graphics"] =
                "تومض بعض الأجسام أحيانا برسومات بيضاء.",
            ["Some of the textures on the footballers don't load"] =
                "بعض خامات (textures) لاعبي كرة القدم لا تحمل.",
            ["Some stages in this game (especially Dojo), will have bad performance due to SPU bottlenecking. You can use the mentioned settings to help, but it will not guarantee the game maintaining a stable 60FPS unless you brute-force performance with a faster CPU"] =
                "بعض المراحل في هذه اللعبة (خصوصا Dojo) سيكون أداؤها سيئا بسبب اختناق SPU. يمكنك استخدام الإعدادات المذكورة للمساعدة، لكنها لن تضمن ثبات اللعبة على 60 FPS إلا إذا فرضت الأداء بالقوة باستخدام CPU أسرع",
            ["Some transparent surfaces are rendered incorrectly - dotted dark pattern appears over glass, eyeballs"] =
                "بعض الأسطح الشفافة تعرض بشكل خاطئ - يظهر نمط منقط داكن فوق الزجاج والعيون.",
            ["Some videos, especially the one in the Central Plaza (2014) appear distorted, but the audio plays fine. This can be fixed with libvdec.sprx set to LLE"] =
                "بعض الفيديوهات، خصوصا الفيديو في Central Plaza (2014)، تظهر مشوهة بينما الصوت يعمل بشكل سليم. يمكن إصلاح ذلك بضبط libvdec.sprx على LLE.",
            ["Sometimes the character will get stuck at the lobby. It fixes itself after a while, in most cases"] =
                "أحيانا تعلق الشخصية في غرفة الانتظار. تصلح نفسها بعد فترة في معظم الحالات.",
            ["Source: [https://forums.rpcs3.net/showthread.php?tid=178278&pid=307901#pid307901 RPCS3 Forums]"] =
                "المصدر: [https://forums.rpcs3.net/showthread.php?tid=178278&pid=307901#pid307901 RPCS3 Forums]",
            ["Starhawk requires the Framebuffer Aliasing Heuristic Bias GPU setting (GPU → Debug) set to Prefer Color. Without it, lit surfaces, characters, and the sky render black. Added in RPCS3 0.0.41-19440 (PR [https://github.com/RPCS3/rpcs3/pull/18644 #18644])"] =
                "تتطلب Starhawk ضبط إعداد Framebuffer Aliasing Heuristic Bias في GPU (GPU → Debug) على Prefer Color. بدونه تعرض الأسطح المضاءة والشخصيات والسماء باللون الأسود. أضيف في RPCS3 0.0.41-19440 (PR [https://github.com/RPCS3/rpcs3/pull/18644 #18644])",
            ["Steam Deck: Set TDP to 15w and GPU to 1600 to solve the frame pacing issue"] =
                "Steam Deck: اضبط TDP على 15w وGPU على 1600 لحل مشكلة تناسق الإطارات (frame pacing).",
            ["Strict Rendering Mode: On - Fixes incorrect lighting"] =
                "Strict Rendering Mode: On - يصلح الإضاءة الخاطئة.",
            ["Super Visor"] =
                "قبعة ذات حاجب واسع (Super Visor)",
            ["Super Visor /w Short Hair"] =
                "قبعة ذات حاجب واسع مع شعر قصير (Super Visor /w Short Hair)",
            ["Supports Retail Only"] =
                "يدعم النسخ التجارية (Retail) فقط.",
            ["Supports Retail Only | [https://www.destinationhome.online/docs/getting-started Getting Started]"] =
                "يدعم النسخ التجارية (Retail) فقط | [https://www.destinationhome.online/docs/getting-started Getting Started]",
            ["Supports Retail Only | [https://youtu.be/KxrCJ2mnOKs?si=iCe0w-GRxi701WuL YouTube Tutorial]"] =
                "يدعم النسخ التجارية (Retail) فقط | [https://youtu.be/KxrCJ2mnOKs?si=iCe0w-GRxi701WuL YouTube Tutorial]",
            ["Supports Retail and QA (Developer)"] =
                "يدعم النسخ التجارية (Retail) ونسخ QA (المطورين).",
            ["T.W.A"] =
                "T.W.A",
            ["Team Fortress 2 almost always hangs when trying to connect to EA Online"] =
                "Team Fortress 2 تتجمد دائما تقريبا عند محاولة الاتصال بـ EA Online.",
            ["Tested with two players only"] =
                "جربت مع لاعبين اثنين فقط.",
            ["Thanks to Whatcookie, a SPU MLAA removal patch is available for this title. Do note, it won't work at 100% resolution scale, needs to be either above or below. No visual issues were found using the patch"] =
                "بفضل Whatcookie، يتوفر تعديل لإزالة SPU MLAA لهذه اللعبة. لاحظ أنه لا يعمل عند Resolution Scale بنسبة 100%، بل يجب أن تكون النسبة أعلى منها أو أقل. لم تلاحظ أي مشاكل بصرية عند استخدام التعديل.",
            ["The \"Rock Band\" drum class results in unresponsive controls. Use \"Rock Band Pro\" instead, even if you have non-Pro drums. If you are using a Rock Band 2/3 drum kit or real ekit, do not forget to map the drum pad modifier and cymbal modifier buttons to ensure drum pad navigation works. See this page for details. You may need to manually edit your input configuration in a text editor for this. Other platforms' keyboards cannot be mapped in RPCS3's controller settings"] =
                "فئة الطبول «Rock Band» تؤدي إلى أدوات تحكم لا تستجيب. استخدم «Rock Band Pro» بدلا منها، حتى لو كانت طبولك غير Pro. إذا كنت تستخدم طقم طبول Rock Band 2/3 أو ekit حقيقيا، فلا تنس ربط زري معدل لوح الطبل ومعدل الصنج لضمان عمل التنقل بلوح الطبل. راجع هذه الصفحة للتفاصيل. قد تحتاج إلى تعديل إعدادات الإدخال يدويا في محرر نصوص لهذا الغرض. لا يمكن ربط لوحات مفاتيح المنصات الأخرى في إعدادات أذرع التحكم في RPCS3",
            ["The \"Unlock FPS\" patch is required to prevent a game bug where completing a mission above 30FPS crashes the game. The replay buffer for replays is set to 5400 which is never hit on real hardware due to poor performance, but easily overflows on the emulator"] =
                "تعديل «Unlock FPS» مطلوب لتفادي خلل في اللعبة يتسبب في انهيارها عند إكمال مهمة بأكثر من 30 FPS. مخزن الإعادة (replay buffer) للإعادات مضبوط على 5400 ولا يبلغ أبدا على الجهاز الحقيقي بسبب الأداء الضعيف، لكنه يفيض بسهولة على المحاكي",
            ["The 'Disable Dynamic Shadows' patch can give a significant performance boost at the cost of removing shadows, which may make platforming a bit harder"] =
                "تعديل 'Disable Dynamic Shadows' قد يعطي تحسنا كبيرا في الأداء على حساب إزالة الظلال، مما قد يجعل القفز بين المنصات أصعب قليلا.",
            ["The BioShock games have a setting in-game that allows you to unlock the framerate found under the Options menu"] =
                "ألعاب BioShock فيها إعداد داخل اللعبة يتيح لك فتح قفل معدل الإطارات، ويوجد ضمن قائمة Options.",
            ["The BioShock games have a setting in-game that allows you to unlock the framerate found under the Options menu. However for this game, it's found under \"Graphics\" in the Options menu"] =
                "ألعاب BioShock فيها إعداد داخل اللعبة يتيح فتح قفل معدل الإطارات، وتجده تحت قائمة Options. لكن في هذه اللعبة تجده تحت «Graphics» في قائمة Options.",
            ["The Caldera Ridge Volcanic DLC track can have serious performance issues with resolution scaling higher than 100% in certain cases, which can also be fixed by disabling dynamic resolution scaling"] =
                "مضمار Caldera Ridge Volcanic DLC قد يعاني من مشاكل أداء خطيرة عند استخدام تحجيم الدقة أعلى من 100% في بعض الحالات، ويمكن إصلاح ذلك أيضا بتعطيل تحجيم الدقة الديناميكي.",
            ["The Disney Infinity series requires the use of a real Infinity Base with figures or the built-in Infinity Manager under Manage > Infinity Base"] =
                "تتطلب سلسلة Disney Infinity استخدام Infinity Base حقيقية مع الشخصيات، أو مدير Infinity المدمج ضمن Manage > Infinity Base.",
            ["The HD ports has a few bugs seen on all three games. Some include the air/Suck Cannon meter in RaC1 still appearing on the side of the screen and RaC2/3 where Ratchet's helmet is bigger in cutscenes compared to gameplay. These are not emulation bugs"] =
                "نسخ HD لها بعض الأخطاء الظاهرة في الألعاب الثلاث كلها. منها بقاء مؤشر Air/Suck Cannon في RaC1 ظاهرا على جانب الشاشة، وفي RaC2/3 خوذة Ratchet تكون أكبر في المشاهد السينمائية مقارنة بوقت اللعب. هذه ليست أخطاء محاكاة",
            ["The Last of Us can be played from start to finish, but requires a good CPU and using RPCS3's patches (especially the MLAA patch) for serviceable performance"] =
                "يمكن لعب The Last of Us من البداية إلى النهاية، لكنها تتطلب CPU جيدا واستخدام تعديلات RPCS3 (خصوصا تعديل MLAA) للحصول على أداء مقبول.",
            ["The PS3 port is known to contain numerous bugs such as incorrect frame-skipping in cutscenes due to it being a very rushed port. These quirks are not an emulation issue"] =
                "من المعروف أن نسخة PS3 تحتوي على أخطاء كثيرة مثل تخطي إطارات غير صحيح في المشاهد السينمائية، لأنها نسخة أنجزت على عجل. هذه العيوب ليست مشكلة محاكاة.",
            ["The PS3 version of the Orange Box has issues that aren't present on any other release of the games including incorrect audio mixing/higher pitched sounds and minor graphical issues"] =
                "نسخة PS3 من Orange Box فيها مشاكل غير موجودة في أي إصدار آخر من الألعاب، منها خلط صوت غير صحيح/أصوات أحد نبرة ومشاكل رسومية بسيطة.",
            ["The Ratchet and Clank series have various issues that are tracked on this GitHub issue"] =
                "تواجه سلسلة Ratchet & Clank مشكلات متعددة يجري تتبعها في بلاغ GitHub المرتبط.",
            ["The Ratchet and Clank series have various issues that are tracked on this GitHub issue. Some of them include broken cube maps seen in this game"] =
                "سلسلة Ratchet and Clank لها مشاكل متنوعة متتبعة في هذا الـ GitHub issue. من بينها خرائط مكعبة (cube maps) معطلة تظهر في هذه اللعبة.",
            ["The Ratchet and Clank series have various issues that are tracked on this GitHub issue. Some of them include the Pyro Blaster/Incinerator that reduces performance when used in this game"] =
                "سلسلة Ratchet and Clank لها مشاكل متنوعة متتبعة في هذا الـ GitHub issue. من بينها أن Pyro Blaster/Incinerator يقلل الأداء عند استخدامه في هذه اللعبة.",
            ["The Sly Collection is home to various issues that were not present on the original PS2 release. Here are some of the issues to note"] =
                "تحتوي Sly Collection على مشاكل متنوعة لم تكن موجودة في إصدار PS2 الأصلي. فيما يلي بعض المشاكل التي ينبغي الانتباه لها.",
            ["The Xillia titles have low brightness and appear darker than other titles. This is present even on the PlayStation 3 and reflects the aesthetics the developer were going for. This is NOT a bug with RPCS3. If you want to check, confirm it with [https://www.youtube.com/watch?v=N0aTh7RcyT4 walkthrough videos] from the PlayStation 3"] =
                "عناوين Xillia سطوعها منخفض وتبدو أغمق من العناوين الأخرى. وهذا موجود حتى على PlayStation 3 ويعكس الجمالية التي أرادها المطور. هذا ليس خللا في RPCS3. إذا أردت التحقق، فتأكد من ذلك عبر [https://www.youtube.com/watch?v=N0aTh7RcyT4 walkthrough videos] من PlayStation 3",
            ["The audio can cut out in the beginning of for example cutscenes. If you are having this issue, try going to the configuration Advanced tab and select 'libatxdec.sprx' in the upper Firmware Libraries list"] =
                "قد ينقطع الصوت في بداية المشاهد السينمائية مثلا. إذا واجهت هذه المشكلة، فجرب الذهاب إلى تبويب Advanced في الإعدادات واختر 'libatxdec.sprx' في قائمة Firmware Libraries العلوية.",
            ["The credits scrolling speed is dependent on the framerate, so very high FPS will scroll very fast"] =
                "سرعة تمرير أسماء فريق العمل (credits) تعتمد على معدل الإطارات، لذا ستتمرر بسرعة كبيرة جدا عند FPS مرتفع جدا.",
            ["The demo version of this game there is a minor graphical Issue that causes the ropes to be rendered in front of the camera in one of the intro sequences in the MMA 101 mode. This Issue was fixed for the full game and did not affect the Xbox version of the demo These videos show the difference:"] =
                "في النسخة التجريبية، قد تظهر الحبال أمام الكاميرا خطأ في مشهد افتتاحي بوضع MMA 101. أصلحت المشكلة في النسخة الكاملة، ولم تؤثر في نسخة Xbox التجريبية. تعرض المقاطع التالية الفرق:",
            ["The demo, beta, kiosk, and unpatched versions currently freeze randomly"] =
                "النسخ التجريبية (demo) وبيتا (beta) وكشك العرض (kiosk) والنسخ غير المحدثة تتجمد حاليا بشكل عشوائي.",
            ["The depth-of-field buffer created by the game seems to be ~322x322 pixels, which is unfortunately lower than the above recommended resolution scale threshold of 512x512, causing the depth-of-field effect to not get scaled up when using it"] =
                "يبدو أن مخزن عمق الميدان (depth-of-field buffer) الذي تنشئه اللعبة بحجم ~322x322 بكسل، وهو للأسف أقل من حد تحجيم الدقة الموصى به أعلاه وهو 512x512، مما يجعل تأثير depth-of-field لا يكبر عند استخدامه",
            ["The developer commentaries for Sly 1 are not on the disc version of the collection, but were re-added on the PSN version"] =
                "تعليقات المطورين (developer commentaries) للعبة Sly 1 غير موجودة في نسخة القرص من المجموعة، لكنها أعيدت في نسخة PSN.",
            ["The digital version comes as an installer and cannot be installed on RPCS3 due to needing PSN. Download the game on your PS3 and dump the files + license to play"] =
                "النسخة الرقمية تأتي كمثبت ولا يمكن تثبيتها على RPCS3 لأنها تحتاج إلى PSN. نزل اللعبة على جهاز PS3 الخاص بك واستخرج (dump) الملفات + الرخصة (license) للعب.",
            ["The disc version of the game comes incomplete and requires an update to play. To learn how to update the game, refer to [https://wiki.rpcs3.net/index.php?title=Help:Installing_Game_Updates here]"] =
                "نسخة القرص من اللعبة تأتي ناقصة وتتطلب تحديثا للعب. لمعرفة كيفية تحديث اللعبة، راجع [https://wiki.rpcs3.net/index.php?title=Help:Installing_Game_Updates هنا]",
            ["The field does not render properly in-game with no current way to fix it"] =
                "لا يعرض الملعب بشكل صحيح داخل اللعبة ولا توجد حاليا طريقة لإصلاح ذلك.",
            ["The frame rate decreases as more of the field and crowd are shown on screen, as well as during play reviews"] =
                "ينخفض معدل الإطارات كلما ظهر جزء أكبر من الملعب والجمهور على الشاشة، وكذلك أثناء مراجعات اللعب (play reviews).",
            ["The game allows for custom songs to be played during in-game. To prepare, create a folder in dev_hdd0/music and give it a name, afterwards place your songs inside (preferably .mp3 format.) Then while in-game, pause and select Audio Settings > Soundtrack and you will be able to select the named folder that contains your songs. Press L1 to cycle to the next song"] =
                "تتيح اللعبة تشغيل أغان مخصصة أثناء اللعب. للتحضير، أنشئ مجلدا في dev_hdd0/music وأعطه اسما، ثم ضع أغانيك بداخله (يفضل بصيغة .mp3). بعد ذلك أثناء اللعب، أوقف اللعبة مؤقتا واختر Audio Settings > Soundtrack وستتمكن من اختيار المجلد المسمى الذي يحتوي أغانيك. اضغط L1 للانتقال إلى الأغنية التالية",
            ["The game allows for custom songs to be played during in-game. To prepare, create a folder in dev_hdd0/music and give it a name, afterwards place your songs inside (preferably .mp3 format.) Then while in-game, pause and select Settings > Audio > Soundtrack and you will be able to select the named folder that contains your songs. You can select whether to play the songs in order or shuffle. Press R1 to cycle to the next song"] =
                "تتيح اللعبة تشغيل أغان مخصصة أثناء اللعب. للتحضير، أنشئ مجلدا في dev_hdd0/music وأعطه اسما، ثم ضع أغانيك بداخله (يفضل بصيغة .mp3). بعد ذلك أثناء اللعب، أوقف اللعبة مؤقتا واختر Settings > Audio > Soundtrack وستتمكن من اختيار المجلد المسمى الذي يحتوي أغانيك. يمكنك اختيار تشغيل الأغاني بالترتيب أو عشوائيا. اضغط R1 للانتقال إلى الأغنية التالية",
            ["The game applies a subtle blur to LEGO objects and characters, which is more pronounced at 720p and 1080p but less noticeable at 480p. This makes 480p the preferred base resolution for achieving cleaner resolution scaling results"] =
                "تطبق اللعبة تمويها خفيفا (blur) على أجسام وشخصيات LEGO، وهو أوضح عند 720p و1080p وأقل وضوحا عند 480p. هذا يجعل 480p هي الدقة الأساسية المفضلة للحصول على نتائج تحجيم دقة أنظف.",
            ["The game can crash at random, even with RSX FIFO Accuracy set to Atomic"] =
                "قد تنهار اللعبة بشكل عشوائي، حتى مع ضبط RSX FIFO Accuracy على Atomic.",
            ["The game can played at 60fps by changing the VBLank Frequency to 120 under Advanced Settings. The game, in its current state (build 0.0.24-14141), will have difficulty maintaining 60fps in some areas, even with a high-spec system. Spider-Man 2099 levels have the most noticeable drops. The game can be played at even higher framerates by further increasing the VBlank Frequency but this currently causes significant visual artefacts"] =
                "يمكن لعب اللعبة بسرعة 60 FPS بتغيير VBlank Frequency إلى 120 ضمن Advanced Settings. ستواجه اللعبة، بحالتها الحالية (build 0.0.24-14141)، صعوبة في الحفاظ على 60 FPS في بعض المناطق، حتى مع جهاز عالي المواصفات. مراحل Spider-Man 2099 فيها أوضح الانخفاضات. يمكن لعب اللعبة بمعدلات إطارات أعلى أيضا برفع VBlank Frequency أكثر، لكن هذا يسبب حاليا عيوبا بصرية (artefacts) كبيرة",
            ["The game can randomly softlock during loading screens"] =
                "قد تتجمد اللعبة عشوائيا (softlock) أثناء شاشات التحميل.",
            ["The game can take several minutes to boot. However after loading in, the game plays fine"] =
                "قد تستغرق اللعبة عدة دقائق للإقلاع. لكن بعد التحميل تعمل اللعبة بشكل جيد.",
            ["The game can use up to 19GB of RAM by itself, check your own specs"] =
                "قد تستخدم اللعبة وحدها حتى 19GB من RAM، تحقق من مواصفات جهازك.",
            ["The game currently lacks performance for it to be considered Playable, even on recommended specs"] =
                "تفتقر اللعبة حاليا إلى الأداء الكافي لتعد Playable، حتى على المواصفات الموصى بها.",
            ["The game currently suffers from random crashing without any log output"] =
                "قد تتعطل اللعبة عشوائيا دون تسجيل أي رسالة في سجل المحاكي.",
            ["The game freezes after the intro cutscene when you start a new game, making it unplayable without a custom configuration. A workaround has been found for this problem but the performance hit and graphics hit is large"] =
                "تتجمد اللعبة بعد المشهد التمهيدي عند بدء لعبة جديدة، مما يجعلها غير قابلة للعب بدون إعدادات مخصصة. وجد حل بديل لهذه المشكلة لكن الأثر على الأداء والرسومات كبير.",
            ["The game has a custom soundtrack feature that allows users to have their songs play during gameplay (game needs to be updated.) Create a folder inside dev_hdd0/music and place your songs inside (preferably .mp3 format.) You can access this via the Easydrive menu by pressing LEFT on the D-Pad"] =
                "تحتوي اللعبة على ميزة موسيقى مخصصة تتيح للمستخدمين تشغيل أغانيهم أثناء اللعب (يجب تحديث اللعبة). أنشئ مجلدا داخل dev_hdd0/music وضع أغانيك بداخله (يفضل بصيغة .mp3). يمكنك الوصول إليها عبر قائمة Easydrive بالضغط على LEFT في D-Pad",
            ["The game has a segment requiring use of the SIXAXIS tilt functionality, which is currently only usable with actual PlayStation controllers. You may be able to work around this on a non-PlayStation controller, such as an Xbox controller, by moving the right analog stick in circles when the game tells you to tilt/shake the controller"] =
                "تحتوي اللعبة على مقطع يتطلب استخدام خاصية الإمالة SIXAXIS، وهي حاليا قابلة للاستخدام فقط مع أذرع PlayStation الحقيقية. قد تتمكن من التحايل على ذلك على ذراع غير PlayStation، مثل ذراع Xbox، بتحريك العصا التناظرية اليمنى في دوائر عندما تطلب اللعبة منك إمالة/هز الذراع",
            ["The game has a tendency to crash/freeze at random"] =
                "تميل اللعبة إلى الانهيار/التجمد بشكل عشوائي.",
            ["The game has a tendency to randomly crash"] =
                "تميل اللعبة إلى الانهيار بشكل عشوائي.",
            ["The game has a very inconsistent framerate and runs very poorly. Stronger hardware can help improve performance"] =
                "معدل إطارات اللعبة غير مستقر إطلاقا وتعمل بشكل سيئ جدا. قد تساعد العتاد الأقوى في تحسين الأداء.",
            ["The game has plenty of graphical issues that cannot be fixed with settings"] =
                "تحتوي اللعبة على الكثير من المشاكل الرسومية التي لا يمكن إصلاحها بالإعدادات.",
            ["The game has plenty of rendering issues as Frostbite 2 is known to be very broken on the emulator"] =
                "تحتوي اللعبة على الكثير من مشاكل العرض لأن Frostbite 2 معروف بأنه معطل جدا على المحاكي.",
            ["The game has various gameplay elements that use SIXAXIS which is enabled by default. However, if you go into OPTIONS > CONTROL OPTIONS, you can disable SIXAXIS. Keep note that the Tornado Gun will still use SIXAXIS regardless of it being disabled"] =
                "تحتوي اللعبة على عناصر لعب متنوعة تستخدم SIXAXIS وهي مفعلة افتراضيا. لكن يمكنك تعطيل SIXAXIS من OPTIONS > CONTROL OPTIONS. لاحظ أن Tornado Gun ستظل تستخدم SIXAXIS سواء عطلته أم لا.",
            ["The game inevitably freezes or crashes after the first few minutes of gameplay, most often during cutscenes and invariably before the character creator stage is passed"] =
                "تتجمد اللعبة أو تنهار حتما بعد الدقائق الأولى من اللعب، غالبا أثناء المشاهد السينمائية ودائما قبل تجاوز مرحلة منشئ الشخصيات (character creator).",
            ["The game is currently not considered \"playable\" as the CPU requirement is too high and due to the visual issue listed in Known Issues. But it can played from start to finish with good performance on a good CPU"] =
                "لا تعد اللعبة حاليا «playable» لأن متطلبات CPU عالية جدا وبسبب المشكلة البصرية المذكورة في Known Issues. لكن يمكن لعبها من البداية إلى النهاية بأداء جيد مع معالج جيد.",
            ["The game is currently not considered \"playable\" as the CPU requirement is too high. But it can played from start to finish with good performance on a good CPU"] =
                "لا تعد اللعبة حاليا «playable» لأن متطلبات CPU عالية جدا. لكن يمكن لعبها من البداية إلى النهاية بأداء جيد مع معالج جيد.",
            ["The game is known to crash after the Ubisoft logo. It may take a few attempts before getting ingame"] =
                "من المعروف أن اللعبة تنهار بعد شعار Ubisoft. قد يستغرق الأمر عدة محاولات قبل الدخول إلى اللعبة.",
            ["The game is known to freeze or even crash every now and then on some setups. There are reports that using the CPU config setting SPU Decoder: 'Recompiler (ASMJIT)' instead of the default makes the game more stable in general, so that can be tested if you are having similar issues"] =
                "من المعروف أن اللعبة تتجمد أو حتى تنهار من حين لآخر على بعض الأجهزة. توجد تقارير تفيد بأن استخدام إعداد CPU التالي SPU Decoder: 'Recompiler (ASMJIT)' بدلا من الافتراضي يجعل اللعبة أكثر استقرارا عموما، فيمكن تجربته إذا كنت تواجه مشاكل مشابهة",
            ["The game is known to randomly crash"] =
                "من المعروف أن اللعبة تنهار بشكل عشوائي.",
            ["The game is more prone to crash/freeze when updated to 1.02. It is highly recommended you stay on 1.00 for the best stability. However when evading/getting busted by police in the open world, the game will always crash"] =
                "اللعبة أكثر عرضة للانهيار/التجمد عند تحديثها إلى 1.02. ينصح بشدة بالبقاء على 1.00 لأفضل استقرار. لكن عند الهروب من الشرطة/القبض عليك منها في العالم المفتوح، ستنهار اللعبة دائما.",
            ["The game is playable but will crash/freeze randomly, often when a player or an enemy throws a grenade and the explosion effect from a grenade is displayed. Since it is not possible to prevent enemies from throwing grenades the game is not playable. Sometimes grenades will not trigger a crash but crashes i did see were when grenades were on screen. The game does not handle increasing resolution well either and will slow down/freeze if resolution is increased"] =
                "اللعبة قابلة للعب لكنها ستنهار/تتجمد عشوائيا، غالبا عندما يرمي لاعب أو عدو قنبلة يدوية ويظهر تأثير انفجارها. ولأنه لا يمكن منع الأعداء من رمي القنابل اليدوية، فاللعبة غير قابلة للعب. أحيانا لا تسبب القنابل انهيارا، لكن الانهيارات التي رأيتها كانت عندما كانت القنابل ظاهرة على الشاشة. كما أن اللعبة لا تتعامل جيدا مع رفع الدقة وستبطؤ/تتجمد إذا رفعت الدقة",
            ["The game is prone to freezing, even when using recommended settings"] =
                "اللعبة عرضة للتجمد، حتى عند استخدام الإعدادات الموصى بها.",
            ["The game is updated to 1.10"] =
                "اللعبة محدثة إلى 1.10.",
            ["The game is very sensitive to framerates below its targeted 30FPS. If the “All Titles” MLAA Patch isn’t enabled, the game with start to produce artifacts. Enabling the patch can prevent them, but expect screen tearing if you’re still below 30FPS"] =
                "اللعبة حساسة جدا لمعدلات الإطارات الأقل من هدفها 30 FPS. إذا لم يكن تعديل MLAA «All Titles» مفعلا، ستبدأ اللعبة بإظهار عيوب بصرية (artifacts). تفعيل التعديل يمكن أن يمنعها، لكن توقع تمزق الصورة (screen tearing) إذا كنت لا تزال أقل من 30 FPS",
            ["The game is very unstable and will frequently crash within seconds without a special patch outlined in the Special Notes section below"] =
                "اللعبة غير مستقرة جدا وستنهار بشكل متكرر خلال ثوان بدون تعديل خاص موضح في قسم Special Notes أدناه.",
            ["The game makes heavy use of SPU post-processing, which makes the game very slow to run. The Disable SPU MLAA patch under All titles in the patch manager can help boost performance at the cost of the game looking much worse"] =
                "تعتمد اللعبة بشكل كبير على المؤثرات اللاحقة (Post-processing) عبر SPU، مما يجعلها بطيئة جدا في التشغيل. تعديل Disable SPU MLAA تحت All titles في مدير التعديلات (patch manager) يمكن أن يساعد في رفع الأداء على حساب أن تبدو اللعبة أسوأ بكثير",
            ["The game may crash after a short while unless \"Accurate SPU DMA\" is enabled in the Advanced settings"] =
                "قد تنهار اللعبة بعد فترة قصيرة ما لم يفعل «Accurate SPU DMA» في الإعدادات المتقدمة (Advanced).",
            ["The game may set itself to use surround sound causing muffled sound effects ingame, you can fix this by changing the ingame setting to stereo"] =
                "قد تضبط اللعبة نفسها على استخدام الصوت المحيطي (surround sound) مما يسبب كتم المؤثرات الصوتية داخل اللعبة، ويمكنك إصلاح ذلك بتغيير الإعداد داخل اللعبة إلى stereo.",
            ["The game needs to be patched to 1.01 (online feature was only added through the patch)"] =
                "يجب تحديث اللعبة إلى 1.01 (ميزة اللعب عبر الإنترنت أضيفت فقط من خلال التحديث).",
            ["The game no longer requires OpenGL to get past the start menu, however it is less stable on Vulkan with frequent freezes and desyncs. Use OpenGL instead as it is more stable at the expense of performance"] =
                "لم تعد اللعبة تتطلب OpenGL لتجاوز القائمة الأولى، لكنها أقل استقرارا على Vulkan مع تجمدات متكررة وفقدان تزامن (desyncs). استخدم OpenGL بدلا منه فهو أكثر استقرارا على حساب الأداء.",
            ["The game requires a 'Ride Gate'/'Summonride Portal' USB peripheral to be connected and configured for passthrough to RPCS3. Support for this was added in RPCS3 version 0.0.17-12652"] =
                "تتطلب اللعبة توصيل وإعداد ملحق USB من نوع 'Ride Gate'/'Summonride Portal' للتمرير (passthrough) إلى RPCS3. أضيف الدعم لهذا في RPCS3 الإصدار 0.0.17-12652.",
            ["The game requires a proprietary skateboard peripheral and won't work (yet) with a regular controller"] =
                "تتطلب اللعبة جهاز لوح تزلج (skateboard) خاصا، ولن تعمل (حتى الآن) مع يد تحكم عادية.",
            ["The game requires a uDraw GameTablet USB peripheral to be connected and configured for passthrough to RPCS3. Support for this was added in RPCS3 version 0.0.13-11475"] =
                "تتطلب اللعبة توصيل جهاز uDraw GameTablet عبر USB وضبط تمرير الجهاز مباشرة إلى RPCS3 (Passthrough). أضيف الدعم في الإصدار 0.0.13-11475.",
            ["The game runs well on 01.00, but if you update to 02.05 performance goes down drastically (1000fps to 20 on level select with high-end CPU). Some users had 3fps ingame with the update"] =
                "تعمل اللعبة جيدا على 01.00، لكن إذا حدثتها إلى 02.05 ينخفض الأداء بشكل كبير (من 1000 FPS إلى 20 في اختيار المراحل مع CPU عالي المستوى). بعض المستخدمين حصلوا على 3 FPS داخل اللعبة بعد التحديث.",
            ["The game seems to be using some form of post-processing using SPU and doesn't upscale"] =
                "يبدو أن اللعبة تستخدم نوعا من post-processing عبر SPU ولا تقوم بالتكبير (upscale).",
            ["The game suffers from audio 'cackling' issues. Using the 'Unstable audio' canary patch and limiting the fps below your average (by using the second frame limiter) can help reduce it"] =
                "تعاني اللعبة من مشاكل 'تقطع' في الصوت. استخدام تعديل 'Unstable audio' في canary وتحديد fps بأقل من متوسطك (باستخدام محدد الإطارات الثاني) يمكن أن يساعد في تقليلها.",
            ["The game suffers from very poor performance, seemingly from high amounts of RSX draw calls throughout the game. This slows down the game heavily in some scenarios (such as the opening level)"] =
                "تعاني اللعبة من أداء ضعيف جدا، يبدو أنه بسبب كمية كبيرة من RSX draw calls في أنحاء اللعبة. هذا يبطئ اللعبة بشدة في بعض الحالات (مثل المستوى الافتتاحي).",
            ["The game targets 60FPS in the singleplayer whilst mulitiplayer targets 30FPS for performance reasons. If you wish to bypass 60FPS in the multiplayer mode, set vblank frequency past 120HZ"] =
                "تستهدف اللعبة 60 FPS في اللعب الفردي بينما يستهدف اللعب الجماعي 30 FPS لأسباب تتعلق بالأداء. إذا أردت تجاوز 60 FPS في وضع اللعب الجماعي، اضبط تردد vblank على أعلى من 120HZ.",
            ["The game uses the SIXAXIS tilt feature. You will need a DualShock 3, DualShock 4, or a DualSense controller. A controller that supports motion control other than one of these will not work, as RPCS3 does not yet support its features"] =
                "تستخدم اللعبة ميزة الإمالة SIXAXIS. ستحتاج إلى ذراع DualShock 3 أو DualShock 4 أو DualSense. أي ذراع آخر يدعم التحكم بالحركة غير هذه لن يعمل، لأن RPCS3 لا يدعم ميزاته بعد.",
            ["The game was able to progress from the loadable to the playable state by RPCS3 PR #4764"] =
                "تمكنت اللعبة من التقدم من حالة loadable إلى حالة playable بفضل طلب دمج RPCS3 رقم #4764.",
            ["The game will crash while playing the ending credits if libvdec.sprx isn't set to LLE"] =
                "ستنهار اللعبة أثناء تشغيل اعتمادات النهاية (ending credits) إذا لم يضبط libvdec.sprx على LLE.",
            ["The game will eventually crash unless \"Accurate SPU DMA\" is enabled"] =
                "ستنهار اللعبة في النهاية ما لم يفعل «Accurate SPU DMA».",
            ["The game will freeze during the loading screen if its not updated to version 1.09"] =
                "ستتجمد اللعبة أثناء شاشة التحميل إذا لم تحدث إلى الإصدار 1.09.",
            ["The game will freeze very shortly after the title screen. This can be worked around by disabling Audio Output in Audio tab"] =
                "ستتجمد اللعبة بعد شاشة العنوان بوقت قصير جدا. يمكن التحايل على ذلك بتعطيل Audio Output في تبويب Audio.",
            ["The game's Coalesced.ini configuration file can be modified for better performance. See [https://youtu.be/gk67QFORdEM this video] for installation instructions for a premade performance-oriented Coalesced.ini"] =
                "يمكن تعديل ملف الإعدادات Coalesced.ini الخاص باللعبة لتحسين الأداء. شاهد [https://youtu.be/gk67QFORdEM هذا الفيديو] لتعليمات تثبيت ملف Coalesced.ini جاهز موجه للأداء.",
            ["The game's sound gets completely cut off by certain triggers. It happens every time you enter a garage, every time you do a stunt jump when the game goes into slow motion and in some mission sequences and can only be solved by restarting the game"] =
                "ينقطع صوت اللعبة تماما عند محفزات معينة. يحدث ذلك في كل مرة تدخل فيها كراجا، وفي كل مرة تقوم فيها بقفزة بهلوانية (stunt jump) عندما تدخل اللعبة في الحركة البطيئة، وفي بعض تسلسلات المهام، ولا يحل إلا بإعادة تشغيل اللعبة",
            ["The game's speed is tied to 60 FPS so adjustments to the Vblank frequency will only increase the game's speed and not in-game FPS"] =
                "سرعة اللعبة مرتبطة بـ 60 FPS، لذا فإن تعديل تردد Vblank سيزيد سرعة اللعبة فقط وليس FPS داخل اللعبة.",
            ["The game, despite targeting 30FPS on original hardware, runs above the 30FPS cap without special settings, only going as high as 60FPS. While game speed seems to be unaffected, the water ripple animation in the pause menu speeds up above 30FPS. Set the Frame Limiter to either \"30\" or \"PS3 Native\" to restore the original framerate"] =
                "اللعبة، رغم استهدافها 30 FPS على العتاد الأصلي، تعمل فوق حد 30 FPS بدون إعدادات خاصة، ولا تتجاوز 60 FPS. ورغم أن سرعة اللعبة لا تبدو متأثرة، فإن حركة تموج الماء في قائمة الإيقاف المؤقت تتسارع فوق 30 FPS. اضبط Frame Limiter على «30» أو «PS3 Native» لاستعادة معدل الإطارات الأصلي",
            ["The locations presently found are Bleak Falls Temple, House Gray-Mane, Temple of Kynareth, War Maiden's, New Gnisis Cornerclub, Goldenglow Estate, Broken Oar Grotto, The Bee and Barb. All of these places except for The Bee and Barb can be temporarily played with PPU threads set to 3 in the debug tab. Although, this setting WILL break games logic for certain non-essential events. Like you can watch how guards spawn"] =
                "المواقع التي وجدت حاليا هي Bleak Falls Temple وHouse Gray-Mane وTemple of Kynareth وWar Maiden's وNew Gnisis Cornerclub وGoldenglow Estate وBroken Oar Grotto وThe Bee and Barb. يمكن لعب كل هذه الأماكن مؤقتا، ما عدا The Bee and Barb، بضبط PPU threads على 3 في تبويب debug. مع ذلك، هذا الإعداد سيفسد منطق اللعبة في بعض الأحداث غير الأساسية. مثل أن ترى كيف يظهر الحراس (guards spawn)",
            ["The main menu does not scale well and will appear very jagged as opposed to in-game"] =
                "القائمة الرئيسية لا تكبر بشكل جيد وتظهر مسننة جدا مقارنة بما داخل اللعبة.",
            ["The minimap does not display things such as walls, gates, or the ground but does correctly display the player, allies, enemies, and fields. Once a field is captured, everything will be displayed correctly for the current and all future missions"] =
                "لا تعرض الخريطة المصغرة أشياء مثل الجدران والبوابات أو الأرض، لكنها تعرض اللاعب والحلفاء والأعداء والحقول بشكل صحيح. بمجرد الاستيلاء على حقل، سيعرض كل شيء بشكل صحيح للمهمة الحالية وجميع المهام القادمة",
            ["The original digital 1.00 version released with a bug where the game's music is incorrectly pitched and sped up. Keep this in mind if you are interested in playing on 1.00"] =
                "صدرت النسخة الرقمية الأصلية 1.00 مع خلل يجعل موسيقى اللعبة بنبرة خاطئة ومسرعة. ضع هذا في اعتبارك إذا كنت مهتما باللعب على 1.00.",
            ["The original release (v1.00) of NPEB01407 had a bug where DLCs were not recognised correctly. Instead of pushing an update to fix this issue, Square Enix pulled the entire game from the store and replaced it with the fixed version (v1.01). Users who had downloaded the original version were required to completely uninstall the game and re-download the fixed version. Fortunately, if a user tried to download the game today, only the fixed version will be downloaded. It is therefore important for owners of NPEB01407 to download the newer version of the game using the instructions mentioned in our game dumping guide. License file is unchanged between the two versions"] =
                "الإصدار الأصلي (v1.00) من NPEB01407 كان فيه خلل جعل DLCs لا تعرف بشكل صحيح. وبدلا من إصدار تحديث لإصلاح المشكلة، سحبت Square Enix اللعبة بالكامل من المتجر واستبدلتها بالنسخة المصلحة (v1.01). كان على المستخدمين الذين نزلوا النسخة الأصلية إلغاء تثبيت اللعبة بالكامل وإعادة تنزيل النسخة المصلحة. لحسن الحظ، إذا حاول مستخدم تنزيل اللعبة اليوم فلن تنزل إلا النسخة المصلحة. لذلك من المهم لمالكي NPEB01407 تنزيل النسخة الأحدث من اللعبة باتباع التعليمات المذكورة في دليل استخراج الألعاب (game dumping guide) لدينا. ملف الرخصة (License) لم يتغير بين النسختين",
            ["The original release of the game has graphical effects, car stats, and physics that were altered through updates and provides a completely different gameplay experience. If you wish to play this version of the game, don't update the game"] =
                "الإصدار الأصلي من اللعبة فيه مؤثرات رسومية وإحصائيات سيارات وفيزياء عدلت عبر التحديثات، ويقدم تجربة لعب مختلفة تماما. إذا أردت لعب هذه النسخة من اللعبة، فلا تحدث اللعبة.",
            ["The original releases of the game were considerably more difficult that was altered in an update. Simply don't update the game if you wish for a challenge"] =
                "كانت الإصدارات الأصلية من اللعبة أصعب بكثير، ثم عدل ذلك في تحديث. ببساطة لا تحدث اللعبة إذا أردت التحدي.",
            ["The physical copies Will often crash at the \"installing additional data\" screen before the main menu"] =
                "النسخ الفيزيائية (physical copies) ستنهار غالبا عند شاشة «installing additional data» قبل القائمة الرئيسية.",
            ["The player can fall through the floor and respawn when a boss fight starts. To also add, the player can levitate in the air after jumping, which can be canceled out by moving"] =
                "قد يسقط اللاعب عبر الأرض ويعاد ظهوره (respawn) عند بدء قتال الزعيم. وبالإضافة إلى ذلك، قد يطفو اللاعب في الهواء بعد القفز، ويمكن إلغاء ذلك بالحركة.",
            ["The player car in some races and pitstops can turn blue. RPCS3 issue #18944"] =
                "قد تتحول سيارة اللاعب إلى اللون الأزرق في بعض السباقات ومحطات الصيانة (pitstops). مشكلة RPCS3 رقم #18944.",
            ["The player character portrait in races does not render"] =
                "صورة شخصية اللاعب في السباقات لا تعرض.",
            ["The short Picture-in-Picture scenes can use a ton of VRAM when Anti-Aliasing is enabled and Resolution Scaling is cranked up. This results in crashes if your GPU doesn't have enough VRAM. Disabling Anti-Aliasing is highly recommended"] =
                "مشاهد Picture-in-Picture القصيرة قد تستهلك الكثير من VRAM عند تفعيل Anti-Aliasing ورفع Resolution Scaling كثيرا. ينتج عن ذلك انهيارات إذا لم يكن لدى GPU لديك VRAM كاف. ينصح بشدة بتعطيل Anti-Aliasing",
            ["The snow in the characters get a stripe pattern that extends throughout the characters. This can be corrected with WCB but CPU Blit needs to be disabled (not recommended)"] =
                "يظهر على الثلج في الشخصيات نمط خطوط يمتد عبر الشخصيات. يمكن تصحيح ذلك بـ WCB لكن يجب تعطيل CPU Blit (غير موصى به).",
            ["The sound in different places/moments of the game gets partially cut off e.g. you can still hear footsteps but won't be able to hear doors opening. Updating the game fixes this"] =
                "ينقطع الصوت جزئيا في أماكن/لحظات مختلفة من اللعبة، مثلا يمكنك سماع خطوات الأقدام لكن لا تسمع فتح الأبواب. تحديث اللعبة يصلح ذلك.",
            ["The workaround to fix the broken depth of field is enabling Force CPU Blit Emulation in [https://wiki.rpcs3.net/index.php?title=Help:Frequently_Asked_Questions#Debug_configuration Debug tab] or enabling the Disable Depth of Field patch in patch manager"] =
                "الحل البديل لإصلاح depth of field المعطل هو تفعيل Force CPU Blit Emulation في [https://wiki.rpcs3.net/index.php?title=Help:Frequently_Asked_Questions#Debug_configuration تبويب Debug] أو تفعيل تعديل Disable Depth of Field في مدير التعديلات (patch manager)",
            ["There appears to be heavy performance issues with the game. While the Xbox 360 port runs at 60 FPS, due to PS3 Emulation restrictions, the game currently freezes and/or crashes often"] =
                "يبدو أن اللعبة تعاني من مشاكل أداء شديدة. فبينما تعمل نسخة Xbox 360 بسرعة 60 FPS، فإن اللعبة حاليا، بسبب قيود محاكاة PS3، تتجمد و/أو تنهار كثيرا.",
            ["There are broken character textures in character selection and cutscenes. In battles, it isn't present. It's a minor issue"] =
                "توجد خامات شخصيات معطلة في اختيار الشخصيات والمشاهد السينمائية. وهي غير موجودة في المعارك. مشكلة بسيطة.",
            ["There are currently no publicly hosted custom servers, however players can host their own: [https://github.com/jackcaver/PLGarage PLGarage (WIP)], [https://github.com/ennuo/Bombd Bombd]"] =
                "لا توجد حاليا خوادم مخصصة مستضافة علنا، لكن يمكن للاعبين استضافة خوادمهم الخاصة: [https://github.com/jackcaver/PLGarage PLGarage (WIP)]، [https://github.com/ennuo/Bombd Bombd]",
            ["There are graphical issues with the game if the default settings are used. To fix them, use the settings mentioned above"] =
                "توجد مشاكل رسومية في اللعبة إذا استخدمت الإعدادات الافتراضية. لإصلاحها، استخدم الإعدادات المذكورة أعلاه.",
            ["There are no known issues reported for this title"] =
                "لا توجد مشاكل معروفة مبلغ عنها لهذه اللعبة.",
            ["There are no reported issues for this title"] =
                "لا توجد مشاكل مبلغ عنها لهذه اللعبة.",
            ["There are no reported issues with the digital version of the 3 individual games"] =
                "لا توجد مشاكل مبلغ عنها في النسخة الرقمية للألعاب الثلاث المنفصلة.",
            ["There are no reported issues with this title"] =
                "لا توجد مشكلات مبلغ عنها لهذه اللعبة.",
            ["There are no reported issues with this title. There are no reported issues with this title"] =
                "لا توجد مشاكل مبلغ عنها لهذه اللعبة. لا توجد مشاكل مبلغ عنها لهذه اللعبة.",
            ["There are not reported issues with this title"] =
                "لا توجد مشاكل مبلغ عنها لهذه اللعبة.",
            ["There are some graphical issues that are present on Vulkan such as incorrect water lighting. OpenGL seems to not have these issues"] =
                "توجد بعض المشاكل الرسومية على Vulkan مثل إضاءة الماء غير الصحيحة. يبدو أن OpenGL لا تظهر فيه هذه المشاكل.",
            ["Theres a Disable mesh trimming patch for this patch. Without it using resolution scale above 100% will result in culling of polygons in complex models. Small triangles and lines that would not be visible at certain distances at 720p will be culled, affecting most notably the main character's face and NPCs at long distances"] =
                "يوجد تعديل Disable mesh trimming لهذه اللعبة. بدونه، سيؤدي استخدام resolution scale أعلى من 100% إلى استبعاد (culling) مضلعات في النماذج المعقدة. المثلثات الصغيرة والخطوط التي لن تكون مرئية على مسافات معينة عند 720p سيتم استبعادها، مما يؤثر بشكل ملحوظ على وجه الشخصية الرئيسية وشخصيات NPC على المسافات البعيدة",
            ["Theres a posibility to use button sequences for cheats in this game"] =
                "توجد إمكانية استخدام تسلسلات أزرار للغش (cheats) في هذه اللعبة.",
            ["This Blu-Ray title is a separate entry to the PlayStation Network title (NPEA00374/NPUA80756) that shares the same name"] =
                "هذه اللعبة على Blu-Ray إدخال منفصل عن لعبة PlayStation Network (NPEA00374/NPUA80756) التي تحمل الاسم نفسه.",
            ["This could be fixed by RPCS3 implementing a different approach to forcing Anisotropic Filtering: RPCS3 issue #14023 but it is not a bug with the emulator"] =
                "يمكن إصلاح هذا إذا اعتمد RPCS3 طريقة مختلفة لفرض Anisotropic Filtering: مشكلة RPCS3 رقم #14023 لكنه ليس خللا في المحاكي.",
            ["This game allows for increased framerate by editing the VBlank Frequency setting, however, playing above 60 FPS will affect camera behavior, ship fall speed, Zone Mode scoring and the distance of the EMP item in Detonator Mode"] =
                "تتيح هذه اللعبة رفع معدل الإطارات بتعديل إعداد VBlank Frequency، لكن اللعب فوق 60 FPS سيؤثر على سلوك الكاميرا وسرعة سقوط السفينة وتسجيل النقاط في Zone Mode ومسافة عنصر EMP في Detonator Mode.",
            ["This game currently has broken MLAA, which is seen as a flickering black square and visible normal mapping that flickers on screen. The workaround is to use the Disable MLAA patch to fix the broken graphics. PSHomeology Lab, Home Headquarters, and PSORG all have it disabled by default via an EBOOT.BIN patch"] =
                "تعاني هذه اللعبة حاليا من MLAA معطل، يظهر كمربع أسود وامض وكخرائط normal mapping مرئية تومض على الشاشة. الحل البديل هو استخدام تعديل Disable MLAA لإصلاح الرسومات المعطلة. PSHomeology Lab وHome Headquarters وPSORG جميعها معطل فيها افتراضيا عبر تعديل EBOOT.BIN",
            ["This game does not work with resolution scale"] =
                "هذه اللعبة لا تعمل مع resolution scale.",
            ["This game has a fan-made translation mod which can be found [https://www.moddb.com/mods/yakuza-kenzan-translation-mod here]"] =
                "لهذه اللعبة مود ترجمة من صنع المعجبين يمكن إيجاده [https://www.moddb.com/mods/yakuza-kenzan-translation-mod هنا]",
            ["This game has a fan-made translation patch by Froid San which can be found [https://pixeldrain.com/u/NY3nNxYF here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين من Froid San يمكن إيجاده [https://pixeldrain.com/u/NY3nNxYF هنا]",
            ["This game has a fan-made translation patch which can be downloaded [https://falions.net/blog/posts/installation here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين يمكن تنزيله [https://falions.net/blog/posts/installation هنا]",
            ["This game has a fan-made translation patch which can be downloaded [https://pixeldrain.com/u/4ZEzD6eE here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين يمكن تنزيله [https://pixeldrain.com/u/4ZEzD6eE هنا]",
            ["This game has a fan-made translation patch which can be found [https://fantranslators.info/projects/e-x-troopers/ here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين يمكن إيجاده [https://fantranslators.info/projects/e-x-troopers/ هنا]",
            ["This game has a fan-made translation patch which can be found [https://gbatemp.net/threads/yakuza-0-ps3-english-patch.580074/ here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين يمكن إيجاده [https://gbatemp.net/threads/yakuza-0-ps3-english-patch.580074/ هنا]",
            ["This game has a fan-made translation patch which can be found [https://gbatemp.net/threads/yakuza-kiwami-ps3-english-patch.580142/ here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين يمكن إيجاده [https://gbatemp.net/threads/yakuza-kiwami-ps3-english-patch.580142/ هنا]",
            ["This game has a fan-made translation patch which can be found [https://www.psx-place.com/threads/initial-d-extreme-stage-bljm-60055-english-patch.33904/ here]"] =
                "لهذه اللعبة تعديل ترجمة من صنع المعجبين يمكن إيجاده [https://www.psx-place.com/threads/initial-d-extreme-stage-bljm-60055-english-patch.33904/ هنا]",
            ["This game has a random bug involving the text. They're sometimes shown correctly, but in some cases, they will appear as string IDs. For example, it can appear as [!:#GUI_MENU001] instead of PRESS X TO START. See RPCS3 issue #13139 for details"] =
                "في هذه اللعبة خلل عشوائي يتعلق بالنصوص. تظهر أحيانا بشكل صحيح، لكنها في بعض الحالات تظهر كمعرفات نصية (string IDs). مثلا، قد يظهر [!:#GUI_MENU001] بدلا من PRESS X TO START. راجع مشكلة RPCS3 رقم #13139 للتفاصيل",
            ["This game has a rather inconsistent framerate that can jump from 30FPS to 60FPS during gameplay. Use the provided patch to set game to fixed 30FPS, other methods of limiting framerate will cause audio issues. A faster CPU may help with more stable framerates without the patch enabled"] =
                "معدل إطارات هذه اللعبة غير مستقر نوعا ما ويمكن أن يقفز من 30 FPS إلى 60 FPS أثناء اللعب. استخدم التعديل المرفق لتثبيت اللعبة على 30 FPS، فالطرق الأخرى لتحديد معدل الإطارات ستسبب مشاكل في الصوت. قد يساعد CPU أسرع في الحصول على معدلات إطارات أكثر استقرارا بدون تفعيل التعديل",
            ["This game has an update not obtained through the Sony API, but rather the in-game server. Unless you have the Complete Edition, you must connect to either one of the custom servers to obtain it. See Netplay notes"] =
                "لهذه اللعبة تحديث لا يحصل عليه عبر Sony API، بل من خادم اللعبة نفسه. ما لم تكن لديك Complete Edition، يجب الاتصال بأحد الخوادم المخصصة للحصول عليه. راجع ملاحظات اللعب عبر الشبكة.",
            ["This game has an update not obtained through the Sony API, but rather the in-game server. You must connect to the custom server provided by PSRewired to obtain it. See Netplay notes"] =
                "لهذه اللعبة تحديث لا يحصل عليه عبر Sony API، بل من خادم اللعبة نفسه. يجب الاتصال بالخادم المخصص الذي توفره PSRewired للحصول عليه. راجع ملاحظات اللعب عبر الشبكة.",
            ["This game has mandatory sections where SIXAXIS is required to progress like Killzone 2. Unlike the 2nd game, you are given the option to disable it if you update the game to 1.14"] =
                "في هذه اللعبة أقسام إلزامية يلزم فيها SIXAXIS للتقدم مثل Killzone 2. وعلى عكس اللعبة الثانية، تمنح خيار تعطيله إذا حدثت اللعبة إلى 1.14.",
            ["This game has various points in the campaign that requires SIXAXIS controls to progress. If you do not have a controller that supports gyro, use the [https://github.com/ZeptoBST/DualShock4-emulator/releases/tag/Release DualShock4-emulator] and set the controller handler to DualShock 4"] =
                "في هذه اللعبة نقاط متعددة في الحملة تتطلب تحكم SIXAXIS للتقدم. إذا لم يكن لديك ذراع يدعم الجيروسكوب (gyro)، فاستخدم [https://github.com/ZeptoBST/DualShock4-emulator/releases/tag/Release DualShock4-emulator] واضبط معالج الذراع (controller handler) على DualShock 4",
            ["This game is currently not fully playable due to broken water reflections that can cause intermittent flickering of bright colors at certain camera angles, even with the fixes above"] =
                "هذه اللعبة غير قابلة للعب بالكامل حاليا بسبب انعكاسات الماء المعطلة التي قد تسبب وميضا متقطعا لألوان ساطعة عند زوايا كاميرا معينة، حتى مع الإصلاحات المذكورة أعلاه.",
            ["This game is currently not playable"] =
                "هذه اللعبة غير قابلة للعب حاليا.",
            ["This game is currently not playable. Although it can be completed, it uses a lot of CPU and GPU resources"] =
                "هذه اللعبة غير قابلة للعب حاليا. ورغم أنه يمكن إكمالها، فإنها تستهلك الكثير من موارد CPU وGPU.",
            ["This game is known to be unstable and crashes very often"] =
                "من المعروف أن هذه اللعبة غير مستقرة وتنهار كثيرا جدا.",
            ["This game is known to be very unstable"] =
                "من المعروف أن هذه اللعبة غير مستقرة جدا.",
            ["This game is not playable, and it will freezes/crashes randomly"] =
                "هذه اللعبة غير قابلة للعب، وستتجمد/تتعطل عشوائيا.",
            ["This game is prone to crashing with no current fix"] =
                "هذه اللعبة عرضة للتعطل ولا يوجد حل حاليا.",
            ["This game is prone to random crashes, which may make progression more tedious"] =
                "هذه اللعبة عرضة للتعطل العشوائي، مما قد يجعل التقدم فيها أكثر إرهاقا.",
            ["This game is very unstable and will very frequently hang at random, making it extremely difficult to progress through the game. Use the provided canary patch to workaround the issue. Keep in mind it could cause potential issues"] =
                "هذه اللعبة غير مستقرة جدا وتتجمد عشوائيا بشكل متكرر للغاية، مما يجعل التقدم فيها صعبا للغاية. استخدم تعديل canary المرفق للالتفاف على المشكلة. ضع في اعتبارك أنه قد يسبب مشاكل محتملة.",
            ["This game makes use of Mesh-Trimming, a technique that culls out polygons from far distances (mostly faces on character models) to improve performance. This becomes more noticeable when using resolution scaling"] =
                "تستخدم هذه اللعبة Mesh-Trimming، وهي تقنية تستبعد (culls) المضلعات من المسافات البعيدة (غالبا وجوه نماذج الشخصيات) لتحسين الأداء. يصبح هذا أكثر وضوحا عند استخدام resolution scaling.",
            ["This game makes use of Mesh-Trimming, a technique that culls out polygons from far distances (mostly faces on character models) to improve performance. This becomes more noticeable when using resolution scaling. Use the provided patch to disable this entirely"] =
                "تستخدم هذه اللعبة Mesh-Trimming، وهي تقنية تقوم بإزالة المضلعات من المسافات البعيدة (غالبا وجوه نماذج الشخصيات) لتحسين الأداء. يصبح هذا أكثر وضوحا عند استخدام تكبير الدقة. استخدم التعديل المرفق لتعطيل ذلك بالكامل",
            ["This game makes use of SIXAXIS controls such as balancing on logs and throwing grenades"] =
                "تستخدم هذه اللعبة تحكمات SIXAXIS مثل الموازنة على جذوع الأشجار ورمي القنابل اليدوية.",
            ["This game makes use of mesh trimming, a technique that culls out polygons on complex models (commonly character faces) from far distances to improve performance"] =
                "تستخدم هذه اللعبة mesh trimming، وهي تقنية تقوم بإزالة المضلعات من النماذج المعقدة (غالبا وجوه الشخصيات) من المسافات البعيدة لتحسين الأداء.",
            ["This game makes use of post-processing that breaks when using resolution scaling. No standalone patch has been made, but the Guitar Hero 3 Deluxe mod has a toggle to disable it, which as a result, also disables certain screen effects"] =
                "تستخدم هذه اللعبة معالجة لاحقة (post-processing) تتعطل عند استخدام تكبير الدقة. لم يتم إنشاء تعديل مستقل، لكن مود Guitar Hero 3 Deluxe يحتوي على خيار لتعطيلها، وبالتالي يعطل أيضا بعض تأثيرات الشاشة.",
            ["This game natively renders at 1088x624, however when moving the camera, it renders at 544x624. Use the provided patch to disable this"] =
                "تعرض هذه اللعبة أصلا بدقة 1088x624، لكن عند تحريك الكاميرا تعرض بدقة 544x624. استخدم التعديل المرفق لتعطيل ذلك.",
            ["This game requires Playstation Move controls. Real controllers are not currently supported so you will have to change the PS Move handler to Fake. Mouse has been tested and unfortunately does not work"] =
                "تتطلب هذه اللعبة تحكمات Playstation Move. أجهزة التحكم الحقيقية غير مدعومة حاليا، لذا عليك تغيير PS Move handler إلى Fake. تمت تجربة الماوس ولسوء الحظ لا يعمل.",
            ["This game requires SIXAXIS to play. For those who do not own a controller with motion controls, refer to the [https://github.com/ZeptoBST/DualShock4-emulator/releases/tag/Release DualShock4-emulator] which can be used to play. You'll need to set the controller handler to DualShock 4 to use"] =
                "تتطلب هذه اللعبة SIXAXIS للعب. لمن لا يملك يد تحكم بحساسات حركة، راجع [https://github.com/ZeptoBST/DualShock4-emulator/releases/tag/Release DualShock4-emulator] الذي يمكن استخدامه للعب. ستحتاج إلى ضبط controller handler على DualShock 4 لاستخدامه",
            ["This game requires a proprietary Cable used with real instruments and won't work with a regular controller"] =
                "تتطلب هذه اللعبة كابلا خاصا يستخدم مع الآلات الموسيقية الحقيقية ولن تعمل مع يد تحكم عادية.",
            ["This game requires a proprietary EA Active peripheral and won't work with a regular controller"] =
                "تتطلب هذه اللعبة ملحق EA Active خاصا ولن تعمل مع يد تحكم عادية.",
            ["This game requires an Online Pass dumped to play online. However the Free to Play version does not require one and Reloaded highly recommends. It is part of a free DLC bundle found on the PS3 Store. Keep note that this bundle does not include Co-op"] =
                "تتطلب هذه اللعبة Online Pass مستخرجا (dumped) للعب عبر الإنترنت. لكن النسخة المجانية (Free to Play) لا تتطلب واحدا، ويوصي بها Reloaded بشدة. وهو جزء من حزمة DLC مجانية موجودة في PS3 Store. لاحظ أن هذه الحزمة لا تتضمن Co-op",
            ["This game requires either a real Toy Pad with character and vehicle tags (included in the starter pack), the [https://github.com/Berny23/LD-ToyPad-Emulator Toy Pad emulator], or the built-in Toy Pad emulator found under \"Manage -> Dimensions Toy Pad\""] =
                "تتطلب هذه اللعبة إما Toy Pad حقيقيا مع علامات الشخصيات والمركبات (مرفقة في الحزمة الأساسية)، أو [https://github.com/Berny23/LD-ToyPad-Emulator Toy Pad emulator]، أو محاكي Toy Pad المدمج الموجود ضمن «Manage -> Dimensions Toy Pad»",
            ["This game suffers from occasional vertex explosions, particularly in areas like Rivet City and Tenpenny Tower. These can be typically be resolved by fast traveling to a distant location and back, or by restarting the emulator"] =
                "تعاني هذه اللعبة من تشوهات حادة في المجسمات (Vertex Explosions) في بعض الأحيان، خاصة في مناطق مثل Rivet City وTenpenny Tower. يمكن عادة حلها بالسفر السريع إلى موقع بعيد والعودة، أو بإعادة تشغيل المحاكي.",
            ["This game suffers from some graphical rendering issues"] =
                "تعاني هذه اللعبة من بعض مشاكل العرض الرسومي.",
            ["This game suffers some significant vertex explosions when loading into levels as well mini map not rendering when open map to view whole dungeon [https://youtu.be/WE2TJGhUe7Q?list=PLm67wIIVI_g5VFnoA2UMA_BFyKURfFST2&t=3251 (Reference)]"] =
                "تعاني هذه اللعبة من تشوهات حادة في المجسمات (Vertex Explosions) كبيرة عند التحميل إلى المراحل، وكذلك عدم ظهور الخريطة المصغرة عند فتح الخريطة لعرض كامل الدنجن [https://youtu.be/WE2TJGhUe7Q?list=PLm67wIIVI_g5VFnoA2UMA_BFyKURfFST2&t=3251 (Reference)]",
            ["This game supports custom music playlists; to use this feature create a \"music\" folder in dev_hdd0 and then create a folder within \"music\" containing your music files of choice (tested with .mp3 format files). To use this playlist in game select it in Options > Audio > Music Playlist"] =
                "تدعم هذه اللعبة قوائم تشغيل موسيقى مخصصة؛ لاستخدام هذه الميزة أنشئ مجلد «music» في dev_hdd0 ثم أنشئ داخل «music» مجلدا يحتوي ملفات الموسيقى التي تريدها (تمت التجربة مع ملفات بصيغة .mp3). لاستخدام قائمة التشغيل هذه داخل اللعبة اخترها من Options > Audio > Music Playlist",
            ["This game uses MLAA for its Anti-Aliasing and reduces performance. Unlike other games requiring patches to disable, this game lets you toggle MLAA on and off in the in-game options. It is recommended to disable to improve performance"] =
                "تستخدم هذه اللعبة MLAA للتنعيم (Anti-Aliasing) وهو يقلل الأداء. بخلاف الألعاب الأخرى التي تتطلب تعديلات لتعطيله، تتيح لك هذه اللعبة تشغيل MLAA وإيقافه من خيارات اللعبة. ينصح بتعطيله لتحسين الأداء.",
            ["This game uses a lot of VRAM when using resolution scale which can lead to crashing"] =
                "تستخدم هذه اللعبة قدرا كبيرا من VRAM عند استخدام تكبير الدقة (resolution scale) مما قد يؤدي إلى التعطل.",
            ["This game uses the server for updates like Resistance FOM"] =
                "تستخدم هذه اللعبة الخادم للتحديثات مثل Resistance FOM.",
            ["This game utilizes SIXAXIS in many parts of the game. The game gives you an option to use regular analog stick controls when the game is updated. Some actions still require SIXAXIS to be used"] =
                "تستخدم هذه اللعبة SIXAXIS في أجزاء كثيرة منها. تمنحك اللعبة خيار استخدام تحكم العصا التناظرية العادية عند تحديث اللعبة. لا تزال بعض الإجراءات تتطلب استخدام SIXAXIS.",
            ["This issue appears to be fixed in later builds of the emulator"] =
                "يبدو أن هذه المشكلة تم إصلاحها في الإصدارات الأحدث من المحاكي.",
            ["This issue is caused by the way RPCS3 forces Anisotropic Filtering (AF). Leaving AF at the default auto setting fixes the issue, but because of how much texture quality improves by forcing 16x AF, we still recommend using 16x AF because the texture quality improvement across the whole game is far more significant than minor pixelation on the portraits and pause menu"] =
                "تنتج هذه المشكلة عن الطريقة التي يفرض بها RPCS3 التصفية متباينة الخواص (Anisotropic Filtering - AF). ترك AF على الإعداد الافتراضي auto يحل المشكلة، لكن بسبب مقدار التحسن الكبير في جودة الخامات عند فرض 16x AF، ما زلنا نوصي باستخدام 16x AF لأن تحسن جودة الخامات في اللعبة كلها أهم بكثير من البكسلة الطفيفة في الصور الشخصية وقائمة الإيقاف المؤقت",
            ["This menu allows for various aspects of the game to be adjusted, such as unlocking all (or limiting) cars or courses available, setting time limits, and other features. It was most-likely intended for in-store displays and public kiosks, given the available options"] =
                "تتيح هذه القائمة ضبط جوانب متعددة من اللعبة، مثل فتح كل السيارات أو المسارات المتاحة (أو تقييدها)، وضبط حدود الوقت، وميزات أخرى. ويرجح أنها كانت مخصصة لأجهزة العرض في المتاجر والأكشاك العامة، نظرا للخيارات المتاحة",
            ["This title has a 60 FPS cap during combat and a 30 FPS cap for all other scenarios in-game. While the frame limiter can be toggled while RPCS3 is running, it doesn't happen automatically. To allow the framerate to hit 60 in combat AND lock to 30 in other sections to stop them from speeding up, currently you must set Framelimit option to 60 and the Vblank frequency option to 30. Issue has been fixed, with new PS3 Native framelimiter. Or you can use the games 60 FPS patch to bypass this altogether"] =
                "تحدد هذه اللعبة سقف 60 FPS أثناء القتال وسقف 30 FPS في بقية الحالات داخل اللعبة. وبينما يمكن تبديل محدد الإطارات أثناء تشغيل RPCS3، إلا أنه لا يحدث تلقائيا. للسماح للإطارات بالوصول إلى 60 في القتال والتثبيت على 30 في الأقسام الأخرى لمنعها من التسارع، يجب حاليا ضبط خيار Framelimit على 60 وخيار Vblank frequency على 30. تم إصلاح المشكلة بواسطة محدد الإطارات الجديد PS3 Native. أو يمكنك استخدام تعديل 60 FPS الخاص باللعبة لتجاوز ذلك بالكامل",
            ["This title has a 60FPS cap during combat and a 30FPS cap for all other scenarios in-game. This is how it was on console"] =
                "تحدد هذه اللعبة سقف 60 FPS أثناء القتال وسقف 30 FPS في بقية الحالات داخل اللعبة. هكذا كانت على الجهاز الأصلي.",
            ["This title is currently not considered \"playable\", as the hardware requirement is too high. But it can be played from start to finish with decent performance on a good CPU and GPU, especially if you use the game's patches"] =
                "لا تعد هذه اللعبة حاليا «قابلة للعب»، لأن متطلبات العتاد مرتفعة جدا. لكن يمكن لعبها من البداية إلى النهاية بأداء مقبول على CPU وGPU جيدين، خاصة إذا استخدمت تعديلات اللعبة.",
            ["This title is currently not considered \"playable\", as the hardware requirement is too high. But it can be played from start to finish with decent performance on a good CPU, especially if you use the game's patches"] =
                "لا تعد هذه اللعبة حاليا «قابلة للعب»، لأن متطلبات العتاد مرتفعة جدا. لكن يمكن لعبها من البداية إلى النهاية بأداء مقبول مع معالج جيد، خاصة إذا استخدمت تعديلات اللعبة.",
            ["This title is currently not considered \"playable\", as the hardware requirement is too high. But it can be played with decent performance on a good CPU, especially if you use the game's patches"] =
                "لا تعتبر هذه اللعبة حاليا «playable» لأن متطلبات العتاد مرتفعة جدا. لكن يمكن لعبها بأداء جيد على CPU قوي، خاصة إذا استخدمت تعديلات اللعبة.",
            ["This title requires a private server to access Netplay features"] =
                "تتطلب هذه اللعبة خادما خاصا لتفعيل ميزات اللعب عبر الشبكة.",
            ["This title requires a private server to access all Netplay features"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة.",
            ["This title requires a private server to access all Netplay features. Enter the below text in the DNS section:"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. أدخل النص التالي في قسم DNS:",
            ["This title requires a private server to access all Netplay features. Enter the below text in the DNS:"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. أدخل النص التالي في DNS:",
            ["This title requires a private server to access all Netplay features. Enter the below text in the IP/Host Switches section:"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. أدخل النص التالي في قسم إعدادات IP/Host Switches:",
            ["This title requires a private server to access all Netplay features. Enter the below text in the IP/Host Switches section: ds-eu-c.scej-online.jp=206.189.232.242&&ds-eu-g.scej-online.jp=206.189.232.242&&c.demons-souls.com=206.189.232.242&&g.demons-souls.com=206.189.232.242&&cmnap.scej-online.jp=206.189.232.242&&demons-souls.scej-online.jp=206.189.232.242 Category:Custom servers (Netplay)"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. أدخل النص التالي في قسم IP/Host Switches: ds-eu-c.scej-online.jp=206.189.232.242&&ds-eu-g.scej-online.jp=206.189.232.242&&c.demons-souls.com=206.189.232.242&&g.demons-souls.com=206.189.232.242&&cmnap.scej-online.jp=206.189.232.242&&demons-souls.scej-online.jp=206.189.232.242 Category:Custom servers (Netplay)",
            ["This title requires a private server to access all Netplay features. Enter the below text in the IP/Host Switches/DNS section:"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. أدخل النص التالي في قسم IP/Host Switches/DNS:",
            ["This title requires a private server to access all Netplay features. Enter the below text in the IP/Hosts switches section:"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. أدخل النص التالي في قسم IP/Hosts switches:",
            ["This title requires a private server to access all Netplay features. It serves the regulation file AC4 requires before using RPCN. Enter the below text in the IP/Hosts switches section:"] =
                "تتطلب هذه اللعبة خادما خاصا لاستخدام جميع ميزات اللعب عبر الشبكة. يقدم الخادم ملف regulation الذي تتطلبه AC4 قبل استخدام RPCN. أدخل النص التالي في قسم IP/Hosts switches:",
            ["This title requires a private server to play. Below are 4 servers that you can play this game on:"] =
                "تتطلب هذه اللعبة خادما خاصا للعب. فيما يلي 4 خوادم يمكنك اللعب عليها:",
            ["This title suffers from broken graphics, mainly seen on fighters during gameplay. Both settings don't fully fix them"] =
                "تعاني هذه اللعبة من رسومات معطلة، تظهر أساسا على المقاتلين أثناء اللعب. كلا الإعدادين لا يصلحانها بالكامل.",
            ["Those with extremely strong CPUs can bruteforce enough performance (near locked 60fps) on Torren IV to where this issue does not occur"] =
                "من يملكون CPU قوية للغاية يمكنهم الحصول بالقوة الغاشمة على أداء كاف (قرابة 60 FPS ثابتة) في Torren IV بحيث لا تحدث هذه المشكلة.",
            ["To access it, go to the game's Options, scroll down to the Miscellaneous category, and while holding down L1 and R1, input Down, Right, Right, Down, Right, Right, Down, Right, Right, Down, Right, Right, START and the Secret category will appear"] =
                "للوصول إليها، اذهب إلى Options الخاصة باللعبة، ثم مرر للأسفل إلى فئة Miscellaneous، وأثناء الضغط مع الاستمرار على L1 وR1، أدخل: Down, Right, Right, Down, Right, Right, Down, Right, Right, Down, Right, Right, START وستظهر فئة Secret",
            ["To access it, go to the game's Options, scroll down to the Miscellaneous category, and while holding down L1, L2, R1, R2, input Down, Right, Right, Down, Right, Right, Down, Right, Right, Down, Right, Right, START and the Special Settings category will appear"] =
                "للوصول إليها، اذهب إلى Options الخاصة باللعبة، ثم مرر للأسفل إلى فئة Miscellaneous، وأثناء الضغط مع الاستمرار على L1 وL2 وR1 وR2، أدخل: Down, Right, Right, Down, Right, Right, Down, Right, Right, Down, Right, Right, START وستظهر فئة Special Settings",
            ["To play online, you must first launch the game on the default configuration while connected to RPCN and install 2 mandatory patches before switching to the custom configuration. Features cross-play with PS3 users"] =
                "للعب عبر الإنترنت، شغل اللعبة بالإعدادات الافتراضية أثناء الاتصال بـ RPCN وثبت تعديلين إلزاميين، ثم انتقل إلى إعدادات اللعبة المخصصة. تدعم اللعب المشترك مع مستخدمي PS3.",
            ["To significantly reduce the vertex explosions, the \"PPU Thread Count\" on the Debug Tab must be set to 1, and a 60 FPS patch cannot be used. A slight audio click might be present unless you deselect the \"Enable Buffering\" option in the Audio Tab. There is also a random crash bug that is yet to be solved, but it's infrequent"] =
                "لتقليل انفجارات الرؤوس (vertex explosions) بشكل كبير، يجب ضبط «PPU Thread Count» في تبويب Debug على 1، ولا يمكن استخدام تعديل 60 FPS. قد توجد طقطقة صوتية خفيفة ما لم تلغ تحديد خيار «Enable Buffering» في تبويب Audio. كما توجد مشكلة تعطل عشوائي لم تحل بعد، لكنها غير متكررة",
            ["To unlock the hidden \"Bancho Ratchet\" skin, input the following code in-game:"] =
                "لفتح سكن «Bancho Ratchet» المخفي، أدخل الرمز التالي داخل اللعبة:",
            ["To use resolution scaling for this title, enable the \"Allow Blit Engine Upscaling\" setting"] =
                "لاستخدام resolution scaling في هذه اللعبة، فعل إعداد «Allow Blit Engine Upscaling».",
            ["To use the skateboard controller, first add a normal pad in port 1, and in port 2 set handler (top left) to Skateboard, and set Device Class (bottom) to Skateboard"] =
                "لاستخدام يد تحكم لوح التزلج (skateboard)، أضف أولا pad عاديا في المنفذ 1، وفي المنفذ 2 اضبط handler (أعلى اليسار) على Skateboard، واضبط Device Class (في الأسفل) على Skateboard.",
            ["Tom Clancy's Splinter Cell: Chaos Theory is capped to 35 FPS. This is normal and happens on real hardware"] =
                "لعبة Tom Clancy's Splinter Cell: Chaos Theory محدودة بسقف 35 FPS. هذا طبيعي ويحدث على العتاد الحقيقي.",
            ["Totally Generic"] =
                "نمط عادي تماما (Totally Generic)",
            ["Tracks with muddy terrain such as \"Mudpool\" and \"The Grizzly\" are very GPU intensive so it's advised to not use high resolution scale values as it will reduce performance"] =
                "المسارات ذات التضاريس الطينية مثل «Mudpool» و«The Grizzly» تستهلك GPU بشكل كبير، لذا ينصح بعدم استخدام قيم مرتفعة لـ resolution scale لأنها ستقلل الأداء.",
            ["Trihawk"] =
                "تسريحة موهوك ثلاثية (Trihawk)",
            ["Two missions (10 - Desperate Times & 18 - Stories of the Past) will cause the game to crash. Set to Approximate XFloat when playing these missions"] =
                "مهمتان (10 - Desperate Times و18 - Stories of the Past) ستسببان تعطل اللعبة. اضبط Approximate XFloat عند لعب هاتين المهمتين.",
            ["UP, RIGHT, DOWN, LEFT, TRIANGLE, SQUARE, CROSS, CIRCLE, R3 (Use the D-Pad)"] =
                "UP, RIGHT, DOWN, LEFT, TRIANGLE, SQUARE, CROSS, CIRCLE, R3 (استخدم D-Pad).",
            ["USB passthrough will work with a real PS3 keyboard or MIDI pro adapter, but Wii and Xbox 360 keyboards connected by wireless adapter are not currently supported. They can be hooked up as USB MIDI instruments and configured in Config > I/O > Emulated MIDI Device 1-4 with the caveat of needing to change octave to navigate menus. See this page for more details"] =
                "سيعمل USB passthrough مع لوحة مفاتيح PS3 حقيقية أو محول MIDI pro، لكن لوحات مفاتيح Wii وXbox 360 المتصلة بمحول لاسلكي غير مدعومة حاليا. يمكن توصيلها كآلات USB MIDI وضبطها في Config > I/O > Emulated MIDI Device 1-4 مع محذور الحاجة إلى تغيير الأوكتاف للتنقل في القوائم. راجع هذه الصفحة لمزيد من التفاصيل",
            ["Unlike other games, LBP stores saves in the Game Data Utility (dev_hdd0/game) instead of the Save Data Utility (dev_hdd0/home//savedata). There is no known way of using encrypted saves"] =
                "خلافا لبقية الألعاب، تخزن LBP الحفظ في Game Data Utility (dev_hdd0/game) بدلا من Save Data Utility (dev_hdd0/home//savedata). ولا توجد طريقة معروفة لاستخدام الحفظ المشفر.",
            ["Unlimited Life: Hold L1 and press right, right, down, down, left"] =
                "حياة غير محدودة: اضغط مطولا على L1، ثم يمين، يمين، أسفل، أسفل، يسار.",
            ["Unlimited mana: Hold R1 and press up, right, up, left, down"] =
                "مانا غير محدودة: اضغط مع الاستمرار على R1 ثم اضغط up, right, up, left, down.",
            ["Unranked works, Search public and private lobby works, Ranked works (both players searching ranked will find each other, Leaderboard works"] =
                "Unranked يعمل، والبحث في الردهات العامة والخاصة يعمل، وRanked يعمل (اللاعبان اللذان يبحثان في Ranked سيجدان بعضهما)، ولوحة الصدارة تعمل.",
            ["Updates post 2.00 are heavier on the CPU and slighly more demanding to run"] =
                "التحديثات بعد 2.00 أثقل على CPU وأكثر تطلبا بقليل للتشغيل.",
            ["Updating the game to version 01.11 breaks the texture of the characters. This can be bypassed by enabling the \"Disable Customization Cache\" patch, however loading times may be a bit longer"] =
                "تحديث اللعبة إلى الإصدار 01.11 يفسد خامات الشخصيات. يمكن تجاوز ذلك بتفعيل تعديل «Disable Customization Cache»، لكن قد تكون أوقات التحميل أطول قليلا.",
            ["Upon entering Matt Groening's mansion, you may not be able to switch from Homer to Bart. Restarting the emulator fixes the problem but only if you are playing through the level normally in story mode. If you're playing through it by manually selecting the level from the \"Episodes & Challenges\" menu, then you have to use PPU Interpreter (Static) in CPU tab of config. Once you are past this area you can revert back to the default setting"] =
                "عند دخول قصر Matt Groening قد لا تتمكن من التبديل من Homer إلى Bart. إعادة تشغيل المحاكي تحل المشكلة لكن فقط إذا كنت تلعب المرحلة بشكل طبيعي في وضع القصة. أما إذا كنت تلعبها باختيار المرحلة يدويا من قائمة «Episodes & Challenges»، فعليك استخدام PPU Interpreter (Static) في تبويب CPU في الإعدادات. بعد تجاوز هذه المنطقة يمكنك إعادة الإعداد الافتراضي",
            ["Uprendering on non updated version of the game will cause huge frametime jumping. Either update the game or don't uprender"] =
                "رفع الدقة (uprendering) على نسخة غير محدثة من اللعبة سيسبب قفزات كبيرة في frametime. إما حدث اللعبة أو لا تستخدم رفع الدقة.",
            ["Upscaling in this game can be quite GPU intensive. If you have a weak GPU (for example 1050, 1050ti, 1650 etc), it is recommended to play the game at default resolution scaling. Expect lower FPS if upscaling above 100% on such GPUs"] =
                "قد يكون رفع الدقة (upscaling) في هذه اللعبة مستهلكا للـ GPU بشكل كبير. إذا كان لديك GPU ضعيف (مثل 1050 أو 1050ti أو 1650 وغيرها)، ينصح بلعب اللعبة بتكبير الدقة الافتراضي. توقع FPS أقل عند رفع الدقة فوق 100% على مثل هذه الـ GPU",
            ["Use \"Standard\" or \"RockSmith\" for vocals/karaoke gameplay. Up to three microphones are supported. When not on vocals and playing online, the first microphone will be used for voice chat. It is recommended to use lower Audio Buffer Duration values (32 ms or lower) to reduce distracting echo when playing as a vocalist"] =
                "استخدم «Standard» أو «RockSmith» للعب الغناء/الكاريوكي. يدعم حتى ثلاثة ميكروفونات. عند عدم اللعب على الغناء واللعب عبر الإنترنت، سيستخدم الميكروفون الأول للدردشة الصوتية. ينصح باستخدام قيم أقل لـ Audio Buffer Duration (32 ms أو أقل) لتقليل الصدى المزعج عند اللعب كمغن",
            ["Use the provided patch for ability to host public games"] =
                "استخدم التعديل المرفق لإمكانية استضافة الألعاب العامة.",
            ["Users are to need to create an account to the respective server they want to join and use Refresher to patch their game for online play. Keep in mind this will make the game patches incompatible unless modified to use the patched game's PPU hash. [https://youtu.be/3CjSsJda71c?si=Zmu2eXYBuDUixvgv Video guide on how to patch the game using Refresher]"] =
                "على المستخدمين إنشاء حساب في الخادم الذي يريدون الانضمام إليه واستخدام Refresher لتعديل لعبتهم للعب عبر الإنترنت. لاحظ أن هذا سيجعل تعديلات اللعبة غير متوافقة ما لم تعدل لاستخدام PPU hash الخاص باللعبة المعدلة. [https://youtu.be/3CjSsJda71c?si=Zmu2eXYBuDUixvgv شرح تعديل اللعبة باستخدام Refresher]",
            ["Users have reported that subtitles break after the first cutscene"] =
                "أبلغ المستخدمون أن الترجمة (subtitles) تتعطل بعد أول مشهد سينمائي.",
            ["Users may encounter some horrible graphical artifacts that appears on the screen, this only occurs on the Vulkan renderer. The workaround as of right now is to enable the Disable MLAA patch or enable Force CPU Blit Emulation"] =
                "قد يواجه المستخدمون عيوبا رسومية سيئة جدا تظهر على الشاشة، وهذا يحدث فقط مع Vulkan renderer. الحل البديل حاليا هو تفعيل تعديل Disable MLAA أو تفعيل Force CPU Blit Emulation.",
            ["Users may notice corrupted dialog portraits of characters when using resolution scaling (greater than 100%). To fix the dialog portraits when using resolution scaling, you will need to install the Bustup mod: # Download \"mod_v2_by_lipsum.7z\" from [https://drive.google.com/drive/folders/13NTlPzEp05GLDpZ9-aRXJ18BPdnx9Zbg here]. #* If you can't open the archive you may need to install [https://www.7-zip.org/ 7-Zip]. # Extract the file and place mod.cpk inside the game's USRDIR folder, the easiest way to find this is to right-click Persona 5 in RPCS3 and click Open Install Folder; if you cannot see the USRDIR folder, then open PS3_GAME first. # Open RPCS3, go to Manage > Game Patches and click on Download Patches. # Once you have downloaded latest patches, enable \"Enable Mod Support\" for Persona 5"] =
                "قد يلاحظ المستخدمون تشوه صور الشخصيات في الحوارات عند استخدام resolution scaling (أكبر من 100%). لإصلاح صور الحوارات عند استخدام resolution scaling، ستحتاج إلى تثبيت مود Bustup: # نزل «mod_v2_by_lipsum.7z» من [https://drive.google.com/drive/folders/13NTlPzEp05GLDpZ9-aRXJ18BPdnx9Zbg هنا]. #* إذا لم تستطع فتح الأرشيف فقد تحتاج إلى تثبيت [https://www.7-zip.org/ 7-Zip]. # استخرج الملف وضع mod.cpk داخل مجلد USRDIR الخاص باللعبة، وأسهل طريقة لإيجاده هي النقر بزر الماوس الأيمن على Persona 5 في RPCS3 ثم النقر على Open Install Folder؛ وإذا لم تر مجلد USRDIR فافتح PS3_GAME أولا. # افتح RPCS3، واذهب إلى Manage > Game Patches وانقر على Download Patches. # بعد تنزيل أحدث التعديلات، فعل «Enable Mod Support» للعبة Persona 5",
            ["Users may see minor graphical glitches in some stages and cutscenes. There may also be a crash while navigating through the Capsule house"] =
                "قد يلاحظ المستخدمون أعطالا رسومية طفيفة في بعض المراحل والمشاهد السينمائية. وقد يحدث أيضا تعطل أثناء التنقل في منزل Capsule.",
            ["Users on weaker machines like the Deck can try settings SPURS Threads to 4 in Advanced Tab and playing around with SPU Threads Priority values 1-3 in CPU Tab for some performance gains. These settings should not be adjusted on good hardware"] =
                "يمكن للمستخدمين على الأجهزة الأضعف مثل Deck تجربة ضبط SPURS Threads على 4 في تبويب Advanced والتجربة بقيم SPU Threads Priority من 1 إلى 3 في تبويب CPU للحصول على بعض مكاسب الأداء. لا ينبغي تعديل هذه الإعدادات على عتاد جيد",
            ["Users who own an base BCUS copy may see a different serial on both the game box and disc shown as BCUS98199. This was due to a delay for the game regarding the lyrics to the song \"Tapha Niang\" and all later releases of the game used this serial. The internal serial code remains as BCUS98148"] =
                "قد يرى من يملك نسخة BCUS الأساسية رقما تسلسليا مختلفا على علبة اللعبة والقرص يظهر على أنه BCUS98199. كان ذلك بسبب تأخير في اللعبة يتعلق بكلمات أغنية «Tapha Niang» وجميع الإصدارات اللاحقة من اللعبة استخدمت هذا الرقم التسلسلي. الرمز التسلسلي الداخلي يبقى BCUS98148",
            ["Users with CPUs with less than 12 threads may experience audio or physics issues. This happens when the audio or physics thread are being starved. To fix it, you can try changing the Preferred SPU Thread setting to 1, 2 or 3. Test it out and see what's best. If the game has high FPS but is running in slow-motion, then the physics thread is being starved, try a different value"] =
                "قد يواجه من لديهم CPU بأقل من 12 thread مشاكل في الصوت أو الفيزياء. يحدث هذا عندما يتم تجويع (starving) thread الصوت أو الفيزياء. لإصلاحه يمكنك تجربة تغيير إعداد Preferred SPU Thread إلى 1 أو 2 أو 3. جرب واختر الأفضل. إذا كانت اللعبة بـ FPS عالية لكنها تعمل بحركة بطيئة، فهذا يعني أن thread الفيزياء يتم تجويعه، جرب قيمة مختلفة",
            ["Using Default Resolution 480p (720x480) with Resolution Scale is noticeably less blurry than 720p with Resolution Scale. Your scaling value must be higher to account for the lower base resolution (ex. 225% for 1080p, 300% for 1440p, 450% for 4k)"] =
                "قد يبدو العرض أقل ضبابية عند اختيار Default Resolution بقيمة 480p (720x480) مع Resolution Scale بدلا من 720p. عوض الدقة الأساسية الأقل برفع النسبة: 225% لـ1080p، و300% لـ1440p، و450% لـ4K.",
            ["Using Relaxed XFloat may cause the game to softlock on loading screens or not play audio when using an SPU cache from a previous session"] =
                "قد يسبب استخدام Relaxed XFloat تجمد اللعبة (softlock) في شاشات التحميل أو عدم تشغيل الصوت عند استخدام SPU cache من جلسة سابقة.",
            ["Using Resolution Scaling values at 200% and beyond will result in lights rendering through walls at specific angles"] =
                "استخدام قيم Resolution Scaling عند 200% وأكثر سيؤدي إلى ظهور الإضاءة عبر الجدران من زوايا معينة.",
            ["Using a threshold of 321x321 or lower allows DoF to scale properly, but will cause issues with other effects (eg. most glow effects will become artifacted). Until a way is found to possibly resize the DoF buffer separately, a patch to disable it entirely can be found below for certain releases"] =
                "استخدام threshold بحجم 321x321 أو أقل يتيح لـ DoF أن يتكبر بشكل صحيح، لكنه سيسبب مشاكل مع التأثيرات الأخرى (مثل: ستظهر تشوهات في معظم تأثيرات التوهج). إلى أن يوجد حل لتغيير حجم DoF buffer بشكل منفصل، يوجد أدناه تعديل لتعطيله بالكامل لبعض الإصدارات",
            ["Using resolution scale above 100% will result in culling of polygons in complex models. Small triangles that would not be visible at certain distances at 720p will be culled, affecting most character's faces. Use the provided patch to completely disable this function"] =
                "استخدام resolution scale فوق 100% سيؤدي إلى إزالة (culling) مضلعات في النماذج المعقدة. المثلثات الصغيرة التي لا تكون مرئية على مسافات معينة عند 720p ستزال، مما يؤثر على وجوه معظم الشخصيات. استخدم التعديل المرفق لتعطيل هذه الوظيفة بالكامل",
            ["Using resolution scale above 100% will result in culling of polygons in complex models. Small triangles that would not be visible at certain distances at 720p will be culled, affecting most notably the main character's face and NPCs at long distances"] =
                "استخدام resolution scale فوق 100% سيؤدي إلى إزالة (culling) مضلعات في النماذج المعقدة. المثلثات الصغيرة التي لا تكون مرئية على مسافات معينة عند 720p ستزال، مما يؤثر بشكل أوضح على وجه الشخصية الرئيسية والـ NPCs على المسافات البعيدة",
            ["Using resolution scale above 100% will result in culling of polygons in complex models. Small triangles that would not be visible at certain distances at 720p will be culled, affecting most notably the main character's face and NPCs at long distances. This can be fixed by enabling the Disable Mesh Trimming game patch"] =
                "استخدام resolution scale فوق 100% سيؤدي إلى استبعاد (culling) مضلعات في النماذج المعقدة. المثلثات الصغيرة التي لا تكون مرئية عند مسافات معينة بدقة 720p ستستبعد، مما يؤثر بشكل ملحوظ على وجه الشخصية الرئيسية والشخصيات غير القابلة للعب (NPCs) على مسافات بعيدة. يمكن إصلاح ذلك بتفعيل تعديل اللعبة Disable Mesh Trimming",
            ["Using the 60FPS patch allows the game to run at 60FPS, however going below 60FPS will slow the game down"] =
                "استخدام تعديل 60 FPS يتيح للعبة العمل على 60 FPS، لكن الهبوط دون 60 FPS سيبطئ اللعبة.",
            ["Using the bumper camera can improve performance considerably"] =
                "استخدام كاميرا المصد (bumper camera) يمكن أن يحسن الأداء بشكل كبير.",
            ["Version 1.00 has a game-breaking crash when finishing races, that was fixed with the 1.02 update. Do not, however, update the game version beyond version 1.05, as newer versions suffer from severe graphical issues and poor frame rate. If you still want to update the game with a version higher than 1.05, you must enable Write Color Buffers and Read Color Buffers. Even with these settings, expect issues like screen tearing"] =
                "يتسبب الإصدار 1.00 بتعطل يمنع إكمال السباقات؛ أصلحه تحديث 1.02. ينصح بعدم تجاوز الإصدار 1.05 لأن الأحدث يعاني من مشكلات رسومية وانخفاض معدل الإطارات. إذا استخدمت إصدارا أحدث، ففعل Write Color Buffers وRead Color Buffers، مع احتمال استمرار تمزق الصورة.",
            ["Visible lines appear in distant geometry, commonly seen on shadows"] =
                "تظهر خطوط مرئية في الهندسة البعيدة، وترى عادة على الظلال.",
            ["Vital graphics don't render even with the above configuration"] =
                "الرسومات الحيوية لا تعرض حتى مع الإعدادات أعلاه.",
            ["Vulkan hangs the GPU process (at least on Linux with NVIDIA), so OpenGL is required: [https://forums.rpcs3.net/thread-192808-post-301903.html#pid301903]"] =
                "Vulkan يعلق عملية GPU (على الأقل على Linux مع NVIDIA)، لذا OpenGL مطلوب: [https://forums.rpcs3.net/thread-192808-post-301903.html#pid301903]",
            ["WALL-E's body will flicker while moving. No settings currently can fix this"] =
                "جسم WALL-E سيومض أثناء الحركة. لا توجد إعدادات حاليا تصلح ذلك.",
            ["WWE Shop items not working. \"Online Axxess\" required"] =
                "عناصر WWE Shop لا تعمل. «Online Axxess» مطلوب.",
            ["When Anti Aliasing isn't set to Auto the game can't determine where and where not to draw them and ends up pixelating the entire screen"] =
                "عندما لا يكون Anti Aliasing مضبوطا على Auto لا تستطيع اللعبة تحديد أين ترسمه وأين لا ترسمه فينتهي بها الأمر بعمل بكسلة لكامل الشاشة.",
            ["When MSAA is enabled, there will be a on-screen motion artifact on the right side of the screen. Disabling MSAA resolves the bug, but breaks transparent textures such as fences and trees"] =
                "عند تفعيل MSAA سيظهر تشوه حركة على الشاشة في الجانب الأيمن منها. تعطيل MSAA يحل المشكلة، لكنه يفسد الخامات الشفافة مثل الأسوار والأشجار.",
            ["When attempting to create/join a world or minigame lobby via Split-screen while connected to RPCN, the game will crash. To prevent this, disconnect other player's controllers and load into the world. Afterwards, connect the other controllers. Must be done for every world/lobby you join"] =
                "عند محاولة إنشاء/الانضمام إلى عالم أو ردهة ألعاب مصغرة عبر Split-screen أثناء الاتصال بـ RPCN ستتعطل اللعبة. لمنع ذلك، افصل أجهزة تحكم اللاعبين الآخرين وحمل العالم. بعد ذلك صل أجهزة التحكم الأخرى. يجب فعل ذلك مع كل عالم/ردهة تنضم إليها",
            ["When doing resolution scaling in this game, gray outlines appear in the hud while in-game and in certain UI elements. You can use [https://www.nexusmods.com/demonssouls/mods/8 this mod] to eliminate that. Be sure to read the installation instruction in the description tab of the linked page carefully when applying this mod"] =
                "عند استخدام resolution scaling في هذه اللعبة تظهر حدود رمادية في الـ HUD أثناء اللعب وفي بعض عناصر الواجهة. يمكنك استخدام [https://www.nexusmods.com/demonssouls/mods/8 this mod] للتخلص منها. احرص على قراءة تعليمات التثبيت في تبويب الوصف في الصفحة المرتبطة بعناية عند تطبيق هذا المود",
            ["When driving fast enough the game renders at 1024x600. This can be disabled by using the 1280x720 resolution patch"] =
                "عند القيادة بسرعة كافية تعرض اللعبة بدقة 1024x600. يمكن تعطيل ذلك باستخدام تعديل الدقة 1280x720.",
            ["When going above 60FPS, in-game cutscenes take longer to finish than intended (around 30 seconds after ending.) While this isn't a major issue as you can press Start to skip, it is something to keep in mind when playing above 60FPS"] =
                "عند تجاوز 60 FPS، تستغرق المشاهد السينمائية داخل اللعبة وقتا أطول من المقصود لتنتهي (حوالي 30 ثانية بعد الانتهاء). ورغم أن هذا ليس مشكلة كبيرة إذ يمكنك الضغط على Start للتخطي، إلا أنه أمر يجب وضعه في الاعتبار عند اللعب فوق 60 FPS",
            ["When in-game, objects such as rubbish bins can fall through the ground then appear in the normal position and fall through the ground again repeatedly. A workaround for it is to use ASMJIT for SPU Decoding, you can also try testing on RPCS3 builds older than May 2022 as according to user reports this regression only popped up sometime around that month. Rebooting the game will also fix it"] =
                "أثناء اللعب، قد تسقط أشياء مثل حاويات القمامة عبر الأرض ثم تعود لمكانها وتتكرر المشكلة. جرب ASMJIT في إعداد SPU Decoder، أو إصدارا من RPCS3 أقدم من مايو 2022. قد تكفي إعادة تشغيل اللعبة.",
            ["When looking through semi-transparent objects (e.g. leaves on large trees), the area viewable through the object is grayscale and many objects get culled out. Semi-transparent objects are also more transparent than they should be, most noticeable with tarps on the half-built buildings"] =
                "عند النظر عبر عناصر شبه شفافة مثل أوراق الأشجار، قد تظهر الخلفية بالرمادي وتختفي بعض المجسمات. كما تبدو بعض العناصر أكثر شفافية من المطلوب، خاصة الأغطية القماشية فوق المباني غير المكتملة.",
            ["When playing the game using PPU LLVM, it will often fail to load the Medical Pavilion level and the World of Hurt challenge map. Due to inaccuracies on PPU LLVM, the game fails to properly decompress the level game data, causing an infinite loading screen or a crash if playing on 1.00. This can be worked around with PPU Interpreter (Static), but it is still possible to load on PPU LLVM with a few lucky attempts. This will heavily reduce performance and cause slower loading times. After completing Medical Pavilion, you will not have any other issues for the rest of the game"] =
                "عند لعب اللعبة باستخدام PPU LLVM، كثيرا ما تفشل في تحميل مرحلة Medical Pavilion وخريطة تحدي World of Hurt. بسبب عدم الدقة في PPU LLVM، تفشل اللعبة في فك ضغط بيانات المرحلة بشكل صحيح، مما يسبب شاشة تحميل لا نهائية أو تعطلا عند اللعب على 1.00. يمكن الالتفاف على ذلك بـ PPU Interpreter (Static)، لكن لا يزال من الممكن التحميل على PPU LLVM بعد عدة محاولات محظوظة. سيقلل هذا الأداء بشكل كبير ويسبب بطء في التحميل. بعد إكمال Medical Pavilion لن تواجه أي مشاكل أخرى في بقية اللعبة",
            ["When rail-grinding on metal rails specifically, after a jump you occasionally fall through the rail. This occurs most notably at two points. The first is at Planet Torren IV(Molonoth Fields) after a lightning trap mid-way through the rail-grinding scene. To avoid the first collision issue wait for either being hit by the trap, or a frame or two before to jump. Be sure to hold jump for as long as possible to avoid the trouble collision point in the rail. The second area is at the Great Clock during the final fight scene. The collision point is directly after starting the rail-grinding around the first two trap bombs. To avoid these collision points simply do not jump for the first 2 bombs"] =
                "عند الانزلاق على القضبان المعدنية تحديدا (rail-grinding)، تسقط أحيانا من خلال القضيب بعد القفز. يحدث هذا بشكل أوضح في نقطتين. الأولى في Planet Torren IV(Molonoth Fields) بعد فخ البرق في منتصف مشهد الانزلاق. لتجنب مشكلة الاصطدام الأولى انتظر حتى تصاب بالفخ، أو اقفز قبله بإطار أو إطارين. احرص على الضغط مع الاستمرار على القفز لأطول فترة ممكنة لتجنب نقطة الاصطدام المشكلة في القضيب. المنطقة الثانية في Great Clock أثناء مشهد القتال الأخير. نقطة الاصطدام مباشرة بعد بدء الانزلاق حول أول قنبلتي فخ. لتجنب نقطتي الاصطدام هاتين لا تقفز ببساطة عند أول قنبلتين",
            ["When setting Frame Limiter to Off, it unlocks the framerate"] =
                "عند ضبط Frame Limiter على Off يتم فتح قفل معدل الإطارات.",
            ["When the resolution is 720p, in-game animations and UI will jitter if the player does not move. This does not happen if you are using a resolution lower or higher than 720p"] =
                "عندما تكون الدقة 720p، ستهتز الرسوم المتحركة والواجهة داخل اللعبة إذا لم يتحرك اللاعب. لا يحدث هذا إذا كنت تستخدم دقة أقل أو أعلى من 720p.",
            ["When trying to access the garage/dealer, the game will freeze with no log input"] =
                "عند محاولة الدخول إلى الكراج/الوكيل، ستتجمد اللعبة دون أي مدخلات في السجل (log).",
            ["When using Hover boots with 'boost' pads occasionally you're stopped instantly on the pad leading to an unexpected death. All pads are usable and all areas requiring a boost pad are reachable, but many attempts may be required(particularly for the final boss fight)"] =
                "عند استخدام أحذية Hover مع منصات 'boost' يتوقف اللاعب أحيانا فجأة على المنصة مما يؤدي إلى موت غير متوقع. جميع المنصات قابلة للاستخدام وجميع المناطق التي تتطلب منصة boost يمكن الوصول إليها، لكن قد تلزم محاولات كثيرة (خاصة في قتال الزعيم الأخير)",
            ["When using Resolution scaling, you will see black rectangles appear around the character's faces during skits. This is oddly similar to the Persona 5 sprites issue where the moving parts of the face were on different tiles and a game patch was required to solve the issue. Currently, no such patch exists for this title. Users who find it too jarring can keep the Resolution scaling at default (100%)"] =
                "عند استخدام Resolution scaling ستظهر مستطيلات سوداء حول وجوه الشخصيات أثناء skits. هذا يشبه بشكل غريب مشكلة sprites في Persona 5 حيث كانت الأجزاء المتحركة من الوجه على tiles مختلفة وكان مطلوبا تعديل للعبة لحل المشكلة. حاليا لا يوجد تعديل كهذا لهذه اللعبة. يمكن لمن يجدها مزعجة جدا إبقاء Resolution scaling على الافتراضي (100%)",
            ["When using a special attack, the game will crash with no current way to prevent it"] =
                "عند استخدام هجوم خاص ستتعطل اللعبة ولا توجد حاليا طريقة لمنع ذلك.",
            ["When using resolution scaling and patches, lines on shadows are made more visible as opposed to not using them. This is how the actual shadows are rendered in-game"] =
                "عند استخدام resolution scaling والتعديلات، تصبح الخطوط على الظلال أكثر وضوحا مقارنة بعدم استخدامها. هكذا تعرض الظلال الفعلية داخل اللعبة.",
            ["When using the \"legacy mode\" (career mode) with a custom fighter, the game might not load the texture for the player face for the first fight, causing the player face (only the face) to be pure black, finishing the first fight will load the texture the next fight"] =
                "عند استخدام «legacy mode» (وضع المهنة) مع مقاتل مخصص، قد لا تحمل اللعبة خامة وجه اللاعب في القتال الأول، مما يجعل وجه اللاعب (الوجه فقط) أسود تماما، وإنهاء القتال الأول سيحمل الخامة في القتال التالي",
            ["When using the Binocucom, the game will lock to 30FPS"] =
                "عند استخدام Binocucom ستقفل اللعبة على 30 FPS.",
            ["When using weaker CPUs, there can be audio crackle occasionally. If you are having this issue, try turning on the option 'Enable Time Stretching' in the Audio configuration ('Enable Buffering' audio option is already enabled by default). This will not fix the overall issue of the CPU not being strong enough to run Skate, but will negate the audio crackle during gameplay"] =
                "عند استخدام CPU أضعف، قد يحدث طقطقة في الصوت أحيانا. إذا واجهت هذه المشكلة، جرب تشغيل خيار 'Enable Time Stretching' في إعدادات Audio (خيار الصوت 'Enable Buffering' مفعل أصلا افتراضيا). لن يحل هذا المشكلة العامة المتمثلة في أن CPU ليست قوية بما يكفي لتشغيل Skate، لكنه سيلغي طقطقة الصوت أثناء اللعب",
            ["When you anchor to a titan half of the crosshair is a black square, same sometimes happens with distress signal icon"] =
                "عندما ترسو (anchor) على titan يصبح نصف علامة التصويب مربعا أسود، ويحدث الأمر نفسه أحيانا مع أيقونة إشارة الاستغاثة (distress signal).",
            ["When you die the whole emulator crashes"] =
                "عندما تموت يتعطل المحاكي بالكامل.",
            ["While PS3 Native is the most accurate option, it can also cause the framerate to fluctuate often, as it will try to lock to 30 FPS if it can't hold 60 FPS like it does in real hardware. Changing the Framelimit to 60 gives better performance at the cost of audio crackling and missing the audio of certain codec calls unless Driver wake-up delay is set to 100us (just for codecs), but be advised that this is just a temporary workaround"] =
                "رغم أن PS3 Native هو الخيار الأدق، إلا أنه قد يسبب أيضا تذبذبا متكررا في معدل الإطارات، إذ سيحاول التثبيت على 30 FPS إذا لم يستطع الحفاظ على 60 FPS كما يفعل على العتاد الحقيقي. تغيير Framelimit إلى 60 يعطي أداء أفضل على حساب طقطقة في الصوت وفقدان صوت بعض نداءات codec ما لم يضبط Driver wake-up delay على 100us (للـ codecs فقط)، لكن انتبه إلى أن هذا مجرد حل مؤقت",
            ["While Resolution Scaling is possible, Depth of Field does not get scaled and makes distance objects look very jagged"] =
                "رغم إمكانية Resolution Scaling، فإن Depth of Field لا يتم تكبيره ويجعل الأجسام البعيدة تبدو مسننة جدا.",
            ["While Vblank works for this game, keep in mind that if your hardware cannot maintain your target framerate, the game as mentioned will break unless you lower Vblank to whatever value your hardware can handle. This can also be applied to help prevent screen artifacts with/without patch if you’re unable to maintain a stable framerate"] =
                "بينما يعمل Vblank مع هذه اللعبة، ضع في اعتبارك أنه إذا لم يستطع عتادك الحفاظ على معدل الإطارات المستهدف، فإن اللعبة كما ذكر ستتعطل ما لم تخفض Vblank إلى القيمة التي يستطيع عتادك تحملها. يمكن تطبيق ذلك أيضا للمساعدة في منع تشوهات الشاشة مع/بدون التعديل إذا كنت غير قادر على الحفاظ على معدل إطارات مستقر",
            ["While it is possible to complete the game from start to finish, it is prone to randomly freezing"] =
                "رغم إمكانية إكمال اللعبة من البداية إلى النهاية، فهي عرضة للتجمد عشوائيا.",
            ["While it is possible to unlock the framerate using Frame Limiter set to Off, keep in mind that rag-doll physics will break at high framerates"] =
                "رغم إمكانية فتح قفل معدل الإطارات بضبط Frame Limiter على Off، ضع في اعتبارك أن فيزياء rag-doll ستتعطل عند معدلات الإطارات العالية.",
            ["While locking-on to an enemy, geometry nearby and behind the enemy will disappear. This issue is related to the flashing overlay animation during lock-on"] =
                "أثناء القفل على عدو (lock-on)، تختفي الهندسة القريبة والتي خلف العدو. ترتبط هذه المشكلة برسم الوميض المتراكب (overlay) أثناء lock-on.",
            ["While most of the game can be played with any controller, there are at least 3 sections in the game that require motion controls to pass. Two of these can be skipped by performing other actions, but the third cannot be bypassed ingame"] =
                "رغم إمكانية لعب معظم اللعبة بأي يد تحكم، يوجد 3 أقسام على الأقل في اللعبة تتطلب تحكمات الحركة لتجاوزها. يمكن تخطي اثنين منها بتنفيذ إجراءات أخرى، لكن الثالث لا يمكن تجاوزه داخل اللعبة.",
            ["While resolution scaling works for this title, please keep in mind that some visuals like electricity glow and some light effects will be gone. Force CPU Blit Emulation is no longer needed for this process"] =
                "رغم أن resolution scaling يعمل في هذه اللعبة، يرجى الانتباه إلى أن بعض العناصر البصرية مثل توهج الكهرباء وبعض مؤثرات الإضاءة ستختفي. لم يعد Force CPU Blit Emulation مطلوبا لهذه العملية.",
            ["While you can play the game above 60FPS setting Vblank past 60hz, very high framerates will break game physics. It is suggested you play at anything lower than 160HZ to prevent this"] =
                "رغم أنه يمكنك لعب اللعبة فوق 60 FPS بضبط Vblank فوق 60 Hz، فإن معدلات الإطارات العالية جدا ستفسد فيزياء اللعبة. يقترح اللعب على أي قيمة أقل من 160HZ لمنع ذلك.",
            ["While you can play the game from start to finish just fine, the game lacks performance for it to be considered Playable"] =
                "رغم أنه يمكنك لعب اللعبة من البداية إلى النهاية دون مشكلة، فإن أداءها لا يكفي لاعتبارها Playable.",
            ["White boxes will appear on the screen if the patch Disable SPU Post-processing is enabled and resolution scale is left at 100%. Only use the patch if you're upscaling"] =
                "ستظهر مربعات بيضاء على الشاشة إذا كان تعديل Disable SPU Post-processing مفعلا وكان resolution scale على 100%. استخدم التعديل فقط إذا كنت ترفع الدقة (upscaling).",
            ["With \"Shader Mode\" set to \"Async (multi threaded)\", users may encounter broken text when booting up the game for the very first time. This is shown as various letters missing on text in the menus. This only happens once and can be fixed by restarting the game. This is not a bug in that shader mode but simply the way it works"] =
                "عند استخدام Shader Mode بالقيمة Async (multi threaded)، قد تظهر حروف ناقصة في القوائم عند التشغيل الأول فقط. أعد تشغيل اللعبة لحل المشكلة؛ وهذا سلوك معروف لهذا الوضع.",
            ["With OpenGL, the main menu background can appear to artifact occasionally on NVIDIA with Linux"] =
                "مع OpenGL، قد تظهر تشوهات أحيانا في خلفية القائمة الرئيسية على NVIDIA مع Linux.",
            ["With low performance and varying framerates, physics issues can occur, such as Kratos not being placed properly on walls and floating off the map. Updating the game version to 1.03 may rectify this"] =
                "مع الأداء المنخفض وتغير معدل الإطارات، قد تحدث مشاكل فيزياء، مثل عدم وضع Kratos بشكل صحيح على الجدران وطفوه خارج الخريطة. قد يصلح تحديث إصدار اللعبة إلى 1.03 ذلك.",
            ["With the 60FPS patch enabled, cutscenes will start to play very fast, alongside the lighting at times will completely break. Physics will also be affected, such as Kutaro's jump height, which will be way less than intended and will halt progress on certain Acts/Curtains. If you do not want to deal with these issues, disable the patch"] =
                "مع تفعيل تعديل 60 FPS، ستبدأ المشاهد السينمائية بالتشغيل بسرعة كبيرة جدا، كما قد تتعطل الإضاءة تماما في بعض الأحيان. ستتأثر الفيزياء أيضا، مثل ارتفاع قفزة Kutaro الذي سيكون أقل بكثير من المقصود وسيوقف التقدم في بعض Acts/Curtains. إذا كنت لا تريد التعامل مع هذه المشاكل، عطل التعديل",
            ["Wool Bully Cap"] =
                "قبعة صوفية (Wool Bully Cap)",
            ["Works as of RPCN 0.7"] =
                "تعمل اعتبارا من RPCN 0.7.",
            ["Works. However, the performance is much lower than that of single-player and is heavily impacted by resolution scaling. Most users should stick with 100% or 125% scaling. Even at 100% (720p) 60 FPS is very difficult to reach, so we don't recommend the 60FPS+ patches for split screen"] =
                "تعمل. لكن الأداء أقل بكثير من اللعب الفردي ويتأثر بشدة بـ resolution scaling. على معظم المستخدمين الالتزام بتكبير 100% أو 125%. حتى عند 100% (720p) يصعب جدا الوصول إلى 60 FPS، لذا لا نوصي بتعديلات 60 FPS+ للشاشة المقسمة (split screen)",
            ["World Selection shows one server, tested a 4 player lobby while only playing by myself in an online game, everything worked as intended"] =
                "تعرض شاشة World Selection خادما واحدا. اختبرت غرفة لأربعة لاعبين مع لاعب واحد متصل، وعملت الميزات المجربة كما هو متوقع.",
            ["Write Color Buffers: On - Fixes missing car liveries"] =
                "Write Color Buffers: On - يصلح غياب ألوان/ملصقات السيارات (liveries).",
            ["You are able to create lobby and join player matches. Invites also work. Both Dukematch and coop work. (Ranked however doesn't work since it tries to download \"playlist\" from PSN)"] =
                "يمكنك إنشاء ردهة والانضمام إلى مباريات اللاعبين. الدعوات تعمل أيضا. يعمل كل من Dukematch وcoop. (لكن Ranked لا يعمل لأنه يحاول تنزيل «playlist» من PSN).",
            ["You are able to create lobby and play levels. Leaderboards work as of RPCN 0.7. [https://youtu.be/PxZRny6iBpA Online test video on RPCS3]"] =
                "يمكنك إنشاء ردهة ولعب المراحل. لوحات الصدارة تعمل اعتبارا من RPCN 0.7. [https://youtu.be/PxZRny6iBpA Online test video on RPCS3]",
            ["You can get the \"Captain Ratchet\" skin if you have a save from Quest for Booty or the game installed and access the Extras menu"] =
                "يمكنك الحصول على سكن «Captain Ratchet» إذا كان لديك حفظ من Quest for Booty أو اللعبة مثبتة ودخلت إلى قائمة Extras.",
            ["You may join a game that's \"in-progress\" using quick match"] =
                "يمكنك الانضمام إلى لعبة «in-progress» (قيد التقدم) باستخدام quick match.",
            ["You may play up to 60fps by changing VBlank Frequency to 120hz and the framelimiter to 60 to fix menu speed up. Some minigames will be sped up, set VBlank Frequency back to 60hz"] =
                "لتشغيل اللعبة حتى 60 FPS، اضبط VBlank Frequency على 120 Hz ومحدد الإطارات على 60 لمنع تسارع القوائم. بعض الألعاب المصغرة ستصبح أسرع؛ عندها أعد VBlank Frequency إلى 60 Hz.",
            ["You need to be whitelisted first. [https://discord.com/invite/puHGNZr PSONE Discord]"] =
                "يجب أن تضاف أولا إلى قائمة السماح. [https://discord.com/invite/puHGNZr PSONE Discord]",
            ["You need to use non-integer values to prevent graphical issues when using resolution scaling. Examples include 200% and 300%"] =
                "لتجنب مشكلات الرسوم عند استخدام Resolution Scale، جرب قيم 200% أو 300% كما تذكر ملاحظات اللعبة.",
            ["You will be able to now start a new game but expect FPS to range from 5 to 20, instead of the butter smooth 60 FPS during the Menu and intro Cut Scene with no custom configuration"] =
                "يمكن بدء لعبة جديدة، لكن توقع أداء بين 5 و20 FPS بدلا من 60 FPS الظاهرة في القوائم والمشهد الافتتاحي عند استخدام الإعدادات الافتراضية.",
            ["ZamboniMultiplayerPatch Game patch must be applied to play online"] =
                "يجب تطبيق تعديل اللعبة ZamboniMultiplayerPatch للعب عبر الإنترنت.",
            ["ZamboniMultiplayerPatch Game patch must be applied to play online!"] =
                "يجب تطبيق تعديل اللعبة ZamboniMultiplayerPatch للعب عبر الإنترنت!",
            ["ZamboniMultiplayerPatch is only required for modified rosters. If your roster is default, this patch is not required"] =
                "ZamboniMultiplayerPatch مطلوب فقط للقوائم (rosters) المعدلة. إذا كانت قائمتك افتراضية فهذا التعديل غير مطلوب.",
            ["[http://www.psorg-web-revival.us PSORG's Website]"] =
                "[http://www.psorg-web-revival.us موقع PSORG]",
            ["[https://agracingfoundation.org/ AGRF Website]"] =
                "[https://agracingfoundation.org/ موقع AGRF]",
            ["[https://discord.com/invite/dsAz2g9S8v Community discord]"] =
                "[https://discord.com/invite/dsAz2g9S8v مجتمع Discord]",
            ["[https://discord.gg/87W5qaMtgB Home Headquarters Discord Server]"] =
                "[https://discord.gg/87W5qaMtgB مجتمع Home Headquarters على Discord]",
            ["[https://discord.gg/JHaKNcRcjA PSORG's Discord Server]"] =
                "[https://discord.gg/JHaKNcRcjA مجتمع PSORG على Discord]",
            ["[https://discord.gg/playstationhome Destination Home Discord Server]"] =
                "[https://discord.gg/playstationhome مجتمع Destination Home على Discord]",
            ["[https://discord.gg/yqWjB3wkU7 Home Laboratory Discord Server]"] =
                "[https://discord.gg/yqWjB3wkU7 مجتمع Home Laboratory على Discord]",
            ["[https://github.com/BusterDaemon/ACInf-GO GO server] (Linux)"] =
                "[https://github.com/BusterDaemon/ACInf-GO خادم GO] (Linux)",
            ["[https://github.com/Optimus1200/Ace_Combat_Infinity_Campaign_Patch_and_Local_Server .NET 8.0 server]"] =
                "[https://github.com/Optimus1200/Ace_Combat_Infinity_Campaign_Patch_and_Local_Server خادم .NET 8.0]",
            ["[https://github.com/ZamboniDevelopment Custom server source]"] =
                "[https://github.com/ZamboniDevelopment مصدر الخادم المخصص]",
            ["[https://github.com/sal063/ACI-local-mock-server Python server] (more stable)"] =
                "[https://github.com/sal063/ACI-local-mock-server Python server] (أكثر استقرارا).",
            ["[https://homeheadquarters.net/ Home Headquarters Website]"] =
                "[https://homeheadquarters.net/ موقع Home Headquarters]",
            ["[https://homelaboratory.online/ Home Laboratory Website]"] =
                "[https://homelaboratory.online/ موقع Home Laboratory]",
            ["[https://jojowiki.com/JoJo%27s_Bizarre_Adventure:_All_Star_Battle All Star Battle Wiki Page]"] =
                "[https://jojowiki.com/JoJo%27s_Bizarre_Adventure:_All_Star_Battle صفحة All Star Battle في الويكي]",
            ["[https://wiki.aiwarehouse.xyz/ Warehouse Website]"] =
                "[https://wiki.aiwarehouse.xyz/ موقع Warehouse]",
            ["[https://www.destinationhome.online Destination Home Website]"] =
                "[https://www.destinationhome.online موقع Destination Home]",
            ["[https://youtu.be/LD4AwcsctVo?t=24 PS3 Full Game]"] =
                "[https://youtu.be/LD4AwcsctVo?t=24 لعبة PS3 الكاملة]",
            ["[https://youtu.be/fIFQikioS18?t=140 XBOX 360 Demo]"] =
                "[https://youtu.be/fIFQikioS18?t=140 نسخة Xbox 360 التجريبية]",
            ["[https://youtu.be/fuN2pntb6yw?t=169 PS3 Demo]"] =
                "[https://youtu.be/fuN2pntb6yw?t=169 نسخة PS3 التجريبية]",
            ["copy blank.bik 00_AUTOSAVE.BIK etc"] =
                "copy blank.bik 00_AUTOSAVE.BIK etc",
            ["crashes in the middle of a game when playing dynasty mode"] =
                "تتعطل في منتصف المباراة عند اللعب في وضع dynasty.",
            ["https://github.com/RPCS3/rpcs3/issues/17684 This issue ticket explains how RWCB is needed now vs before. Pending investigation RWCB may persist as a required setting"] =
                "https://github.com/RPCS3/rpcs3/issues/17684 تشرح تذكرة المشكلة هذه كيف أن RWCB مطلوب الآن مقارنة بالسابق. بانتظار التحقيق، قد يبقى RWCB إعدادا مطلوبا.",
            ["the game crashes when loading into the first HIN initiation race"] =
                "تتعطل اللعبة عند التحميل إلى أول سباق بدء HIN.",
        };
}

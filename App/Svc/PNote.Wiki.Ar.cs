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
                "يضبط تأثيرات Bloom بصورة صحيحة عندما تتجاوز Resolution Scale نسبة 100%.",
            ["Currently required for the game to boot."] =
                "مطلوب حاليا لإقلاع اللعبة.",
            ["Currently required to go ingame."] =
                "مطلوب حاليا للدخول إلى اللعبة.",
            ["Cutscenes, specifically pre-recorded cutscenes, are difficult to skip if the FPS is too high. When pausing to skip a cutscene it unpauses very quickly on its own. Capping the FPS fixes this issue. See Special Notes."] =
                "يصعب تخطي المشاهد، وخصوصا المشاهد المسجلة مسبقا، إذا كان FPS مرتفعا جدا. عند الإيقاف المؤقت لتخطي المشهد تستأنف اللعبة بسرعة تلقائيا. يؤدي وضع حد لـFPS إلى حل المشكلة. راجع Special Notes.",
            ["Degrades performance but fixes broken graphics when used with Write Color Buffers."] =
                "يخفض الأداء، لكنه يصلح الرسوم المعطلة عند استخدام Write Color Buffers.",
            ["Depends on GPU. It usually improves performance. It can break graphics on some sections (e.g. \"Boat Ride\")."] =
                "يعتمد على GPU. يحسن الأداء عادة، لكنه قد يعطل الرسوم في بعض الأقسام مثل Boat Ride.",
            ["Depends on GPU. It usually improves performance. It can produce some flickering on some objects (e.g. on the big red flags). The flickering seems related to \"ZCULL Accuracy\" value (more evident with \"Relaxed\")."] =
                "يعتمد على GPU ويحسن الأداء عادة. قد يسبب وميضا في بعض العناصر، مثل الأعلام الحمراء الكبيرة. يبدو أن الوميض مرتبط بقيمة ZCull Accuracy ويظهر بوضوح أكبر مع Relaxed.",
            ["Enable this if the game crashes on boot."] =
                "فعله إذا انهارت اللعبة عند الإقلاع.",
            ["Enable this when Write color buffers is enabled, if enabling Write color buffers results in effects like smoke appearing/clipping through objects that should block their visibility."] =
                "فعله مع Write Color Buffers إذا تسبب تفعيلها في ظهور مؤثرات مثل الدخان عبر عناصر يفترض أن تحجبها.",
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
            ["Fixes bright scenes while in-game, but expect a small performance hit."] =
                "يصلح المشاهد شديدة السطوع أثناء اللعب، مع انخفاض بسيط متوقع في الأداء.",
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
            ["Fixes broken crowd graphics"] =
                "يصلح رسوم الجمهور المعطلة.",
            ["Fixes broken effect in pause menu and broken lighting in night-time scenarios."] =
                "يصلح المؤثر المعطل في قائمة الإيقاف والإضاءة المعطلة في المشاهد الليلية.",
            ["Fixes broken graphics in player's car and detached parts of it after crashing into any object."] =
                "يصلح الرسوم المعطلة في سيارة اللاعب والأجزاء المنفصلة منها بعد الاصطدام بأي عنصر.",
            ["Fixes broken graphics on some sections with the rain effect (e.g. \"Ambush\")."] =
                "يصلح الرسوم المعطلة في بعض الأقسام التي تستخدم تأثير المطر، مثل Ambush.",
            ["Fixes broken half screen."] =
                "يصلح ظهور الصورة على نصف الشاشة.",
            ["Fixes broken lighting"] =
                "يصلح الإضاءة المعطلة.",
            ["Fixes Broken Lighting And Missing Graphics"] =
                "يصلح الإضاءة المعطلة والعناصر الرسومية المفقودة.",
            ["Fixes broken Map menu in Cellphone (Select Button)."] =
                "يصلح قائمة Map المعطلة في Cellphone عبر زر Select.",
            ["Fixes broken Map menu in Cellphone (Select Button). | Fixes flickering in the map menu."] =
                "يصلح قائمة Map المعطلة في Cellphone عبر زر Select، ويصلح الوميض في قائمة Map.",
            ["Fixes broken menu."] =
                "يصلح القائمة المعطلة.",
            ["Fixes broken menus."] =
                "يصلح القوائم المعطلة.",
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
            ["Fixes crashes when using Resolution scaling."] =
                "يصلح الانهيارات عند استخدام Resolution Scale.",
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
            ["Fixes excessive brightness."] =
                "يصلح السطوع المفرط.",
            ["Fixes fire/smoke effects."] =
                "يصلح تأثيرات النار والدخان.",
            ["Fixes flashing image between transitions. | Fixes flashing image on transitions."] =
                "يصلح وميض الصورة أثناء الانتقالات.",
            ["Fixes flickering bloom."] =
                "يصلح وميض Bloom.",
            ["Fixes flickering glitches on robots."] =
                "يصلح خلل الوميض على الروبوتات.",
            ["Fixes flickering HUD."] =
                "يصلح وميض HUD.",
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
            ["Fixes flickering texture."] =
                "يصلح وميض الخامات.",
            ["Fixes flickering. Required only if you are using Async Texture Streaming."] =
                "يصلح الوميض. لا يلزم إلا عند استخدام Async Texture Streaming.",
            ["Fixes flood."] =
                "يصلح عرض الفيضان.",
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
            ["Fixes grey/untextured player mech"] =
                "يصلح ظهور Mech الخاص باللاعب رماديا أو بلا خامات.",
            ["Fixes helmets not rendering properly"] =
                "يصلح عرض الخوذ بصورة غير صحيحة.",
            ["Fixes horrible gas, smoke and fire."] =
                "يصلح عرض الغاز والدخان والنار المشوه.",
            ["Fixes icons missing for some of the seed packets"] =
                "يصلح الأيقونات المفقودة لبعض عبوات البذور.",
            ["Fixes incorrect bloom effect"] =
                "يصلح تأثير Bloom غير الصحيح.",
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
            ["Fixes low performance in-game."] =
                "يصلح ضعف الأداء داخل اللعبة.",
            ["fixes low performance ingame"] =
                "يصلح ضعف الأداء داخل اللعبة.",
            ["Fixes menu glitches when combined with Force CPU Blit"] =
                "يصلح خلل القوائم عند استخدامه مع Force CPU Blit.",
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
            ["Fixes missing bloom and random overly bright sky in Radiator Springs."] =
                "يصلح Bloom المفقود والسطوع العشوائي الزائد للسماء في Radiator Springs.",
            ["Fixes missing car and \"speed zone\" textures"] =
                "يصلح خامات السيارة وSpeed Zone المفقودة.",
            ["Fixes missing car liveries"] =
                "يصلح طلاء السيارات المفقود.",
            ["Fixes missing collision. Requires SPU LLVM recompiler. Expect audio stutter."] =
                "يصلح الاصطدامات المفقودة. يتطلب SPU Decoder بالقيمة Recompiler (LLVM)، مع توقع تقطع الصوت.",
            ["Fixes missing graphics while in-game."] =
                "يصلح العناصر الرسومية المفقودة أثناء اللعب.",
            ["Fixes missing ground and lighting."] =
                "يصلح الأرض والإضاءة المفقودتين.",
            ["Fixes missing ground. Breaks resolution scale."] =
                "يصلح الأرض المفقودة، لكنه يعطل Resolution Scale.",
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
                "يصلح معظم الرسوم المعطلة.",
            ["Fixes most visual issues when using resolution scale."] =
                "يصلح معظم المشكلات البصرية عند استخدام Resolution Scale.",
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
                "يصلح بعض الرسوم المعطلة، مثل الماء في Battleship Bay وربما مواضع أخرى.",
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
            ["Fixes some missing visuals."] =
                "يصلح بعض العناصر البصرية المفقودة.",
            ["Fixes some overly bright scenes."] =
                "يصلح بعض المشاهد شديدة السطوع.",
            ["Fixes some visual issues"] =
                "يصلح بعض المشكلات البصرية.",
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
                "يصلح معظم الرسوم المعطلة.",
            ["Fixes the missing shaders on the first boot."] =
                "يصلح Shaders المفقودة عند الإقلاع الأول.",
            ["Fixes the ocean being invisible"] =
                "يصلح اختفاء المحيط.",
            ["Fixes tiling artifacts"] =
                "يصلح تشوهات Tiling.",
            ["Fixes tiling artifacts seen in some areas of the game"] =
                "يصلح تشوهات Tiling الظاهرة في بعض مناطق اللعبة.",
            ["Fixes timing issue."] =
                "يصلح مشكلة التوقيت.",
            ["Fixes unloaded textures on npcs."] =
                "يصلح الخامات غير المحملة على NPCs.",
            ["Fixes videos and blurry gameboard"] =
                "يصلح الفيديوهات وضبابية لوحة اللعب.",
            ["Fixes visual issues in the main menu and while in-game."] =
                "يصلح المشكلات البصرية في القائمة الرئيسية وأثناء اللعب.",
            ["Fixes visual issues in-game (30 Years of Wrestlemania Mode)"] =
                "يصلح المشكلات البصرية داخل اللعبة في 30 Years of Wrestlemania Mode.",
            ["Fixes visual issues in-game (Attitude Era Mode)"] =
                "يصلح المشكلات البصرية داخل اللعبة في Attitude Era Mode.",
            ["Fixes visual issues in-game."] =
                "يصلح المشكلات البصرية داخل اللعبة.",
            ["Fixes visual issues such as main menu being red."] =
                "يصلح المشكلات البصرية، مثل ظهور القائمة الرئيسية باللون الأحمر.",
            ["Fixes visual issues when using resolution scale."] =
                "يصلح المشكلات البصرية عند استخدام Resolution Scale.",
            ["Fixes visual issues while in-game."] =
                "يصلح المشكلات البصرية أثناء اللعب.",
            ["Fixes visual issues with surfaces e.g mud."] =
                "يصلح المشكلات البصرية في الأسطح، مثل الوحل.",
            ["Fixes visual issues."] =
                "يصلح المشكلات البصرية.",
            ["Fixes visual problems in-game."] =
                "يصلح المشكلات البصرية داخل اللعبة.",
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
                "يساعد على إصلاح المشكلات البصرية داخل اللعبة.",
            ["Helps prevent freezing"] =
                "يساعد على منع التجمد.",
            ["Helps reduce bugs when resolution scale is set to 125% +"] =
                "يساعد على تقليل الأخطاء عند ضبط Resolution Scale على 125% أو أكثر.",
            ["Helps the game run at full framerate"] =
                "يساعد اللعبة على العمل بمعدل الإطارات الكامل.",
            ["Helps to decrease minor graphical glitches"] =
                "يساعد على تقليل الخلل الرسومي البسيط.",
            ["Helps with broken graphics."] =
                "يساعد على إصلاح الرسوم المعطلة.",
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
            ["Login required when Network Status is set to Connected. Prevents the game from hanging on boot."] =
                "يلزم تسجيل الدخول عند ضبط Network Status على Connected. يمنع ذلك تعليق اللعبة عند الإقلاع.",
            ["Lower values will cause severe, striped visual artifacting in the sky if the render resolution is upscaled beyond 100%."] =
                "تسبب القيم الأقل تشوهات بصرية شديدة ومخططة في السماء إذا رفعت دقة العرض فوق 100%.",
            ["Major performance improvement"] =
                "تحسن كبير في الأداء.",
            ["Makes smear in multiplayer look a bit better as well as fixing some incorrectly rendered graphics"] =
                "يحسن مظهر تأثير Smear في Multiplayer قليلا، ويصلح بعض الرسوم المعروضة بصورة غير صحيحة.",
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
                "مطلوب لإصلاح خلل الملعب الأسود أثناء المباريات النهارية الموضح في https://github.com/RPCS3/rpcs3/issues/6861/، والموجود في عدة ألعاب NCAA Football وMadden NFL. بعد إنشاء Shader Cache باستخدام Async with Shader Interpreter يمكن الرجوع إلى Async (multithreaded) في مرات الإقلاع التالية عند الحاجة.",
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
                "يجب تفعيله مع Write Color Buffers لإصلاح الرسوم المعطلة.",
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
            ["Prevents game from freezing."] =
                "يمنع تجمد اللعبة.",
            ["Prevents long pauses when choosing songs."] =
                "يمنع التوقفات الطويلة عند اختيار الأغاني.",
            ["Prevents vertex explosions out of the ground up to the sky"] =
                "يمنع انفجار Vertex من الأرض حتى السماء.",
            ["Prevents weird texture corruption in Tailfin Pass background and potentially other graphical issues."] =
                "يمنع تلف الخامات الغريب في خلفية Tailfin Pass وربما مشكلات رسومية أخرى.",
            ["Properly renders bloom when using Resolution Scaling."] =
                "يعرض Bloom بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales bloom and shadows when using Resolution Scaling."] =
                "يضبط Bloom والظلال بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales bloom effects when using resolution scaling."] =
                "يضبط تأثيرات Bloom بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales Depth of Field when using Resolution Scaling."] =
                "يضبط Depth of Field بصورة صحيحة عند استخدام Resolution Scale.",
            ["Properly scales lighting effects when resolution scaling is set above 100%."] =
                "يضبط تأثيرات الإضاءة بصورة صحيحة عند ضبط Resolution Scale فوق 100%.",
            ["Properly scales the depth of field effect when doing resolution scaling above 100%."] =
                "يضبط تأثير Depth of Field بصورة صحيحة عند استخدام Resolution Scale فوق 100%.",
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
            ["Required alongside Force CPU blit emulation to fix broken graphics."] =
                "مطلوب مع Force CPU Blit Emulation لإصلاح الرسوم المعطلة.",
            ["Required for 2D UI elements which are otherwise missing."] =
                "مطلوب لعناصر 2D UI التي تكون مفقودة من دونه.",
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
                "مطلوب لإقلاع اللعبة بصورة صحيحة.",
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
                "مطلوب لإقلاع اللعبة والدخول إليها.",
            ["Required to boot the game, also fixes some visual problems in-game."] =
                "مطلوب لإقلاع اللعبة، كما يصلح بعض المشكلات البصرية داخلها.",
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
            ["Required to fix black ingame graphics and broken geometry in races."] =
                "مطلوب لإصلاح الرسوم السوداء داخل اللعبة والأشكال الهندسية المعطلة في السباقات.",
            ["Required to fix black screen in-game."] =
                "مطلوب لإصلاح الشاشة السوداء داخل اللعبة.",
            ["Required to fix black screen."] =
                "مطلوب لإصلاح الشاشة السوداء.",
            ["Required to fix broken graphics when using resolution scaling. Reduces performance."] =
                "مطلوب لإصلاح الرسوم المعطلة عند استخدام Resolution Scale، ويخفض الأداء.",
            ["Required to fix broken text and graphics."] =
                "مطلوب لإصلاح النص والرسوم المعطلة.",
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
            ["Required to get past intro screen, \"Delay each odd MFC Command\" must be checked as well"] =
                "مطلوب لتجاوز شاشة المقدمة، ويجب أيضا تفعيل Delay each odd MFC Command.",
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
                "تعلق اللعبة على شاشة سوداء عند الإقلاع إذا لم يفعل هذا الخيار.",
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
        };
}

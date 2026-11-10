using System;
using System.Collections.Generic;

namespace R3Config.Svc;

public static partial class NoteSvc
{
    private static readonly Dictionary<string, string> ArByEn =
        new(StringComparer.OrdinalIgnoreCase)
        {            ["(Fire, Bomb, Car Chase and Circle Of Threat)"] =
                "(يشمل Fire وBomb وCar Chase وCircle Of Threat).",
            ["0001=Dietician (Add bonus points to an item's HP.);0002=Master (Add bonus points to an item's SP.);0003=Gladiator (Add bonus points to an item's ATK.);0004=Sentry (Add bonus points to an item's DEF.);0005=Tutor (Add bonus points to an item's INT.);0006=Coach (Add bonus points to an item's RES.);0007=Marksman (Add bonus points to an item's HIT.);0008=Physician (Add bonus points to an item's SPD.);000B=Muscleman (Add bonus points to an item's ATK and HP.);000C=Sprinter (Add bonus points to an item's ATK and SPD.);000D=Nerd (Add bonus points to an item's INT and SP.);000E=Hard Worker (Add bonus points to an item's ATK and INT.);000F=Patient (Add bonus points to an item's DEF and RES.);0010=Pugilist (Add bonus points to an item's ATK and HIT.);0011=Sniper (Add bonus points to an item's HIT and SPD.);0015=Alchemist (Inflict Poison status upon attack.);0016=Hypnotist (Inflict Sleep status upon attack.);0017=Witch Doctor (Inflict Paralysis status upon attack.);0018=Amnesiac (Inflict Amnesia status upon attack.);0019=Gangster (Inflict Depraved status upon attack.);001A=Professional (Inflict Criticals upon attack.);0029=Pharmacist (Increase Poison resistance.);002A=Coffee Maker (Increase Sleep resistance.);002B=Medicine Man (Increase Paralysis resistance.);002C=Psychologist (Increase Amnesia resistance.);002D=Social Worker (Increase Depraved resistance.);002E=Firefighter (Increase Fire resistance.);002F=Aeronaut (Increase Wind resistance.);0030=Cryophile (Increase Water resistance.) <---The developers probably meant \"Ice\" instead of \"Water\".;003C=Guardian (Gain more ability when equipping armor.);003D=Broker (Obtain more money upon defeating enemies.);003E=Statistician (Obtain more EXP upon defeating enemies.);003F=Manager (Obtain more Mana upon defeating enemies.);0040=Sparring Partner (Increase rate of Weapon Mastery.);0041=Trainer (Increase rate of Skill Mastery.);0083=Item General;0084=Item King;0085=Item God;0086=Item God 2;00C9=Kinder Gardener (Decrease damage by 25%.);00CA=Berry Picker (Raise the chances of a Team Attack to 100%.);00CB=Secret Trainer (Boost stat increase when leveling up by 10%.);00CC=Chalkboard Scraper (Decrease accuravy of enemy attacks on you by 25%.);00CD=High Spiriter (Release the target from ailment upon a normal attack.);00CE=Glutton (Damage from normal attacks converts to recover effect.);00CF=Punisher (Halve the target's HP with a normal attack from behind.);00D0=Sweet Tempter (Recover 50% of your HP at the end of your turn.);00D1=Mana Robber (Increase Mana earned by 10% with a normal attack.);00D2=Cat Burglar (20% chance to steal from defeated enemy. (Except against Senators));00D3=All Digger (Inflict Paralysis status with a normal attack from behind.);00D4=Trash Talker (Inflict Poison status with a normal attack from behind.);00D5=Rubber (Perform two normal attacks in a row.);00D6=Sensationalist (Inflict Depraved status with a normal attack from behind.);00D7=Wuss (Decrease physical attack damage by 40%.);00D8=Town Crier (Increase counter attacks by 5.);00D9=Sun Bather (Inflict Sleep status with a normal attack from behind.);00DA=Joke Slapper (Inflict Amnesia status with a normal attack from behind.);00DB=Motivator (Decrease magic attack damage by 40%.);00DC=Sumo Wrestler (Increase damage of an ally monster by 20%.);00DD=Hand Shaker (Never miss a target.);00DE=Beggar (Decrease stats of adjacent enemy by 20%.);00DF=Partier (Geo Effect and Geo Chain damage will heal instead.);00E0=Mode Changer (Odd-turn physical attacks and even-turn magic attacks will be nullified.);00E1=Eraser (50% chance to deathblow enemies under 25% HP.);00E2=Killer (Deathblow enemies under 25% SP.);00E3=Heart's Shadow (Increase stats by 1% per unit with 0 likeability.);00FA=Dengeki PS Shop (Increase EXP, Mana and HL earned by 1% for every hour played. (Max 99%));00FB=Famitsu Shop (Increase EXP, Mana and HL earned by 50% for first 30 minutes of play.);00FC=Love Love Shop (Temporarily share max likeability with units equipped with the same Innocent.);00FD=Tsun Tsun Shop (Temporarily share minimum likeability with units equipped with the same Innocent.);00FE=Revival! Shop (Revive at the Base Panel when you are defeated.);00FF=Fold Shop (Move around the map regardless of height differences and obstacles.);0100=Energy Shop (Recover 10% of your HP and SP at the end of your turn.)"] =
                "0001=Dietician (يضيف نقاطا اضافية إلى HP للعنصر.);0002=Master (يضيف نقاطا اضافية إلى SP للعنصر.);0003=Gladiator (يضيف نقاطا اضافية إلى ATK للعنصر.);0004=Sentry (يضيف نقاطا اضافية إلى DEF للعنصر.);0005=Tutor (يضيف نقاطا اضافية إلى INT للعنصر.);0006=Coach (يضيف نقاطا اضافية إلى RES للعنصر.);0007=Marksman (يضيف نقاطا اضافية إلى HIT للعنصر.);0008=Physician (يضيف نقاطا اضافية إلى SPD للعنصر.);000B=Muscleman (يضيف نقاطا اضافية إلى ATK وHP للعنصر.);000C=Sprinter (يضيف نقاطا اضافية إلى ATK وSPD للعنصر.);000D=Nerd (يضيف نقاطا اضافية إلى INT وSP للعنصر.);000E=Hard Worker (يضيف نقاطا اضافية إلى ATK وINT للعنصر.);000F=Patient (يضيف نقاطا اضافية إلى DEF وRES للعنصر.);0010=Pugilist (يضيف نقاطا اضافية إلى ATK وHIT للعنصر.);0011=Sniper (يضيف نقاطا اضافية إلى HIT وSPD للعنصر.);0015=Alchemist (يفرض حالة Poison عند الهجوم.);0016=Hypnotist (يفرض حالة Sleep عند الهجوم.);0017=Witch Doctor (يفرض حالة Paralysis عند الهجوم.);0018=Amnesiac (يفرض حالة Amnesia عند الهجوم.);0019=Gangster (يفرض حالة Depraved عند الهجوم.);001A=Professional (يسبب Criticals عند الهجوم.);0029=Pharmacist (يزيد مقاومة Poison.);002A=Coffee Maker (يزيد مقاومة Sleep.);002B=Medicine Man (يزيد مقاومة Paralysis.);002C=Psychologist (يزيد مقاومة Amnesia.);002D=Social Worker (يزيد مقاومة Depraved.);002E=Firefighter (يزيد مقاومة Fire.);002F=Aeronaut (يزيد مقاومة Wind.);0030=Cryophile (يزيد مقاومة Water.) <---يبدو أن المطورين قصدوا \"Ice\" بدلا من \"Water\".;003C=Guardian (يزيد القدرة عند تجهيز الدرع.);003D=Broker (يزيد المال المكتسب عند هزيمة الاعداء.);003E=Statistician (يزيد EXP المكتسب عند هزيمة الاعداء.);003F=Manager (يزيد Mana المكتسبة عند هزيمة الاعداء.);0040=Sparring Partner (يزيد معدل Weapon Mastery.);0041=Trainer (يزيد معدل Skill Mastery.);0083=Item General;0084=Item King;0085=Item God;0086=Item God 2;00C9=Kinder Gardener (يقلل الضرر بنسبة 25%.);00CA=Berry Picker (يرفع فرصة Team Attack إلى 100%.);00CB=Secret Trainer (يزيد نمو الاحصاءات عند رفع المستوى بنسبة 10%.);00CC=Chalkboard Scraper (يقلل دقة هجمات الاعداء عليك بنسبة 25%.);00CD=High Spiriter (يزيل الحالة السلبية عن الهدف عند هجوم عادي.);00CE=Glutton (يحول ضرر الهجمات العادية إلى تأثير استشفاء.);00CF=Punisher (يخفض HP الهدف إلى النصف بهجوم عادي من الخلف.);00D0=Sweet Tempter (يستعيد 50% من HP في نهاية دورك.);00D1=Mana Robber (يزيد Mana المكتسبة بنسبة 10% بهجوم عادي.);00D2=Cat Burglar (فرصة 20% للسرقة من العدو المهزوم. (باستثناء Senators));00D3=All Digger (يفرض Paralysis بهجوم عادي من الخلف.);00D4=Trash Talker (يفرض Poison بهجوم عادي من الخلف.);00D5=Rubber (ينفذ هجومين عاديين متتاليين.);00D6=Sensationalist (يفرض Depraved بهجوم عادي من الخلف.);00D7=Wuss (يقلل ضرر الهجمات الجسدية بنسبة 40%.);00D8=Town Crier (يزيد الهجمات المضادة بمقدار 5.);00D9=Sun Bather (يفرض Sleep بهجوم عادي من الخلف.);00DA=Joke Slapper (يفرض Amnesia بهجوم عادي من الخلف.);00DB=Motivator (يقلل ضرر الهجمات السحرية بنسبة 40%.);00DC=Sumo Wrestler (يزيد ضرر وحش حليف بنسبة 20%.);00DD=Hand Shaker (لا يخطئ الهدف ابدا.);00DE=Beggar (يقلل احصاءات العدو المجاور بنسبة 20%.);00DF=Partier (يجعل ضرر Geo Effect وGeo Chain يشفي بدلا من ذلك.);00E0=Mode Changer (يلغي الهجمات الجسدية في الادوار الفردية والهجمات السحرية في الادوار الزوجية.);00E1=Eraser (فرصة 50% لتنفيذ deathblow على الاعداء تحت 25% HP.);00E2=Killer (ينفذ deathblow على الاعداء تحت 25% SP.);00E3=Heart's Shadow (يزيد الاحصاءات بنسبة 1% لكل وحدة بقيمة likeability تساوي 0.);00FA=Dengeki PS Shop (يزيد EXP وMana وHL المكتسبة بنسبة 1% لكل ساعة لعب. (الحد الاقصى 99%));00FB=Famitsu Shop (يزيد EXP وMana وHL المكتسبة بنسبة 50% خلال اول 30 دقيقة من اللعب.);00FC=Love Love Shop (يشارك مؤقتا اعلى likeability مع الوحدات المجهزة بنفس Innocent.);00FD=Tsun Tsun Shop (يشارك مؤقتا اقل likeability مع الوحدات المجهزة بنفس Innocent.);00FE=Revival! Shop (يعيدك للحياة عند Base Panel عندما تهزم.);00FF=Fold Shop (يسمح بالحركة في الخريطة بغض النظر عن فروق الارتفاع والعوائق.);0100=Energy Shop (يستعيد 10% من HP وSP في نهاية دورك.)",
            ["0002=2x;0004=4x;0008=8x;0010=16x;0020=32x;0040=64x;0080=128x;0100=256x;0200=512x;0400=1024x;"] =
                "القيم المتاحة: 0002=2x;0004=4x;0008=8x;0010=16x;0020=32x;0040=64x;0080=128x;0100=256x;0200=512x;0400=1024x;",
            ["000A=10x;0064=100x;03E8=1,000x;2710=10,000"] =
                "القيم المتاحة: 000A=10x;0064=100x;03E8=1,000x;2710=10,000",
            ["Also affects enemies, but the Spark Shot can still kill various enemies."] =
                "يؤثر أيضا على الاعداء، لكن Spark Shot ما زالت قادرة على قتل بعض الاعداء.",
            ["Ammo still decreases. I found it for the purpose of testing a picked up weapon modifier."] =
                "تظل الذخيرة تنقص. تم العثور على هذه الشفرة لغرض اختبار معدل للسلاح الملتقط.",
            ["Do not activate with (Standalone)"] =
                "لا تفعل هذه الشفرة مع (Standalone).",
            ["If you activate codes after starting the game, trophy cheats will not work unless you go to \"Episode Menu\" or \"Options\" or \"Extras\" and then go back to main menu to activate the debug menu."] =
                "إذا فعلت الشفرات بعد بدء اللعبة، فلن تعمل شفرات trophy إلا إذا ذهبت إلى \"Episode Menu\" أو \"Options\" أو \"Extras\" ثم عدت إلى main menu لتفعيل debug menu.",
            ["It also affects rented stuff in the story, and you get to keep the extras you normally get from paying the fee if you forgot to bring the dvd or whatever back."] =
                "يؤثر أيضا على الاشياء المستأجرة في القصة، وتحتفظ بالاضافات التي تحصل عليها عادة عند دفع الرسوم إذا نسيت إعادة dvd أو أي شيء مشابه.",
            ["It gives metal dogs infinite health too, and possibly other unwanted side effects."] =
                "يمنح metal dogs صحة لا نهائية أيضا، وقد يسبب آثارا جانبية أخرى غير مرغوبة.",
            ["It has a minor visual glitch when attacked usually on the character in the left, the heart icon will appear with a half-heart near it or something, but game is playable with this active"] =
                "يوجد خلل بصري بسيط عند التعرض للهجوم، غالبا على الشخصية في الجهة اليسرى؛ قد تظهر ايقونة القلب وبجوارها نصف قلب أو شيء مشابه، لكن اللعبة تبقى قابلة للعب مع تفعيل الشفرة.",
            ["It is mainly useful for throwing spears and slings. Unfortunately it does not work if you use L1 + O to throw a weapon."] =
                "تفيد أساسا عند رمي الرماح والمقاليع. للأسف لا تعمل إذا استخدمت L1 + O لرمي سلاح.",
            ["Max All MC Social Parameters After Change"] =
                "يضبط جميع MC Social Parameters على الحد الاقصى بعد التغيير.",
            ["Money might appear as 0 in in the shop windows if you turn this on while playing. That can be fixed closing and re-entering the shop windows."] =
                "قد يظهر المال بقيمة 0 في نوافذ المتجر إذا فعلت الشفرة أثناء اللعب. يمكن إصلاح ذلك بإغلاق نوافذ المتجر ثم فتحها من جديد.",
            ["Not in championships Race"] =
                "لا يعمل في سباقات championships.",
            ["Only infinite amount, not infinite shooting per turn until enemy dies."] =
                "الكمية فقط لا نهائية، وليس إطلاقا لا نهائيا في الدور الواحد حتى يموت العدو.",
            ["Only tested to x16 times, it boosts friendship gained through events making it easier to boost arcana and vow levels faster."] =
                "تم اختباره فقط حتى x16، ويزيد الصداقة المكتسبة عبر الاحداث مما يسهل رفع مستويات arcana وvow بسرعة أكبر.",
            ["Only use it with the Coin-Op Machine on Hit Challenges, it affects the entire game."] =
                "استخدمه فقط مع Coin-Op Machine في Hit Challenges؛ فهو يؤثر على اللعبة بالكامل.",
            ["Protagonist gained exp is usually not visible after battle but added later."] =
                "عادة لا يظهر exp الذي يحصل عليه Protagonist بعد المعركة، لكنه يضاف لاحقا.",
            ["Shares memory execution cave with Have All Powers."] =
                "يشترك في memory execution cave مع Have All Powers.",
            ["Still requires Infinite Ammo."] =
                "ما زال يتطلب Infinite Ammo.",
            ["The displayed weapon name will be different than what you are actually using. This code will change your weapon type so you can use any type."] =
                "سيظهر اسم السلاح المعروض مختلفا عن السلاح المستخدم فعليا. تغير هذه الشفرة نوع السلاح لكي تتمكن من استخدام أي نوع.",
            ["There was some kind of note or warning, I cannot translate it."] =
                "كانت هناك ملاحظة أو تحذير من نوع ما، ولا يمكن ترجمتها من المصدر المتاح.",
            ["They will still attempt to attack if you get close enough to them."] =
                "سيظلون يحاولون الهجوم إذا اقتربت منهم بما يكفي.",
            ["This code helps make the date loop on the same day, so an event that happened in this day will keep repeating itself without affecting the end of the story giving you free time during afternoons or other moments to boost arcana and so on. De-activate it to continue story mode."] =
                "تجعل هذه الشفرة التاريخ يكرر اليوم نفسه، لذلك سيستمر الحدث الذي وقع في ذلك اليوم بالتكرار دون التأثير على نهاية القصة، مما يمنحك وقتا حرا بعد الظهر أو في أوقات أخرى لرفع arcana وما شابه. عطلها لمتابعة story mode.",
            ["This code helps with palace exploration, but once you are ready to send the calling card, de-activate this one or the game will lock in a loop trying to fill the detection gauge to 99 since all boss parts it needs to force detection to the highest."] =
                "تساعد هذه الشفرة في استكشاف palace، لكن عندما تصبح جاهزا لإرسال calling card عطل هذه الشفرة، وإلا ستدخل اللعبة في حلقة وهي تحاول رفع detection gauge إلى 99 لأن جميع أجزاء boss تحتاج إلى إجبار مستوى الاكتشاف على أعلى قيمة.",
            ["This does not save."] =
                "هذا لا يتم حفظه.",
            ["This is the same Stop Day Date Advance, but will only lock in the time you activated it at, like mid afternoon or evening then it will keep on a loop in this time. Both lacks a button combo to be activated or deactivated mid-game without netcheat. It is safe to use and de-activate through netcheat any time."] =
                "هذه هي نفس Stop Day Date Advance، لكنها تثبت الوقت الذي فعلتها عنده فقط، مثل منتصف بعد الظهر أو المساء، ثم تكرر هذا الوقت. كلتاهما بلا button combo للتفعيل أو التعطيل أثناء اللعب من دون netcheat. يمكن استخدامها وتعطيلها بأمان عبر netcheat في أي وقت.",
            ["This one shows the amount gained after battle(already multiplied)."] =
                "هذه تعرض المقدار المكتسب بعد المعركة (بعد تطبيق المضاعف بالفعل).",
            ["Turn OFF The Code If You Already, Reach Lv100."] =
                "عطل الشفرة إذا وصلت بالفعل إلى Lv100.",
            ["Use after max level to keep the value"] =
                "استخدمها بعد الوصول إلى max level للحفاظ على القيمة.",
            ["does have minor bar across screen"] =
                "يوجد شريط بسيط عبر الشاشة.",
            ["enable it only if your are doing Netherverse missions."] =
                "فعلها فقط عند تنفيذ مهام Netherverse.",
            ["working"] =
                "تعمل.",

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
                "يلغي قفل FPS.",
            ["Unlocks FPS"] =
                "يلغي قفل FPS.",
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
                "يصلح الرسوم المعروضة بشكل غير صحيح.",
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
                "باتش خاص يصغر بعض عناصر UI. يتطلب Rexis's 4K Mod هذا الباتش؛ لا تفعله إذا لم تكن تستخدم 4K Mod.",
            ["A collection of QoL patches to help make some game mods possible. Currently includes: Single Combat GAP Expand DLC BGM IDs Forcibly Enable motionse.dat/acb on All Enemies Disable Player Swordtrack Loading Load Proper Boss Voices"] =
                "مجموعة من باتشات QoL للمساعدة على تشغيل بعض Mods. تتضمن حاليا: Single Combat GAP؛ توسيع معرفات DLC BGM؛ فرض تفعيل motionse.dat/acb على جميع الأعداء؛ تعطيل Player Swordtrack Loading؛ وتحميل أصوات الزعماء الصحيحة.",
            ["Fixes a threading deadlock/livelock that causes the game to freeze during gameplay under RPCS3's PPU LLVM recompiler. Changes from v1.0: - Introduces use of sys_ppu_thread_yield (syscall 0x4C) Certain RPCS3 settings can reintroduce sensitivity to this issue. Avoid changing the following from their defaults where possible: - Clock speed (PPU/SPU) - VBlank frequency - Sleep timers accuracy - SPU XFloat accuracy - Accurate RSX reservations - ZCull accuracy Remove PPU cache after applying"] =
                "يصلح حالة Deadlock/Livelock في الخيوط تتسبب في تجمد اللعبة أثناء اللعب مع Recompiler (LLVM) في PPU Decoder ضمن RPCS3. التغييرات منذ v1.0: استخدام sys_ppu_thread_yield (syscall 0x4C). قد تعيد بعض إعدادات RPCS3 حساسية المشكلة؛ تجنب قدر الإمكان تغيير Clock Scale (PPU/SPU) وVBlank Frequency وSleep Timers Accuracy وSPU XFloat Accuracy وAccurate RSX Reservations وZCull Accuracy عن قيمها الافتراضية. احذف PPU cache بعد تطبيق الباتش.",
            ["Version 1.0 - Fixes flickering water reflections under Approximate ZCull, normally this effect requires Precise ZCull, but Precise ZCull has a big speed impact.\\nVersion 2.0 - Enables rendering of Heat Wave effect under Approximate ZCull, again, normally works with Precise ZCull only"] =
                "الإصدار 1.0: يصلح وميض انعكاسات الماء مع Approximate ZCull؛ يحتاج هذا التأثير عادة إلى Precise ZCull، لكن Precise ZCull يؤثر بشدة في الأداء. الإصدار 2.0: يفعل عرض Heat Wave مع Approximate ZCull، رغم أن التأثير يعمل عادة مع Precise ZCull فقط.",
            ["WARNING: Causes physics issues with harpies, you will need to disable this patch to get past some sections of the game. Unlocks internal timestep calculation and fixes IGC timing issues at 60 FPS and above. Increase vblank to match your desired framerate"] =
                "تحذير: يسبب مشكلات في الفيزياء مع Harpies، وستحتاج إلى تعطيل الباتش لتجاوز بعض أقسام اللعبة. يفتح حساب Timestep الداخلي ويصلح توقيت IGC عند 60 FPS فما فوق. ارفع VBlank Frequency ليتوافق مع معدل الإطارات المطلوب.",

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
                "موت الأعداء فور ظهورهم يكسر اللعبة بالتأكيد، كما أن قتلهم بضربة واحدة (1) يكسرها أيضا.",
            ["Enemies never know where I am making \"Hit Clueless Enemy\" very easy to do. Turrets always get you, and enemies sometimes attack during missions"] =
                "لا يعرف الأعداء مكانك، مما يجعل Hit Clueless Enemy سهلا جدا. الأبراج ترصدك دائما، وقد يهاجمك بعض الأعداء أثناء المهمات.",
            ["Enemies will not move or do anything at all, they just turn to face you but can still be killed. If they are in turrets they will still attack"] =
                "لن يتحرك الأعداء أو ينفذوا أي أفعال؛ سيكتفون بالالتفات نحوك، ومع ذلك يمكن قتلهم. إذا كانوا داخل الأبراج فسيستمرون في الهجوم.",
            ["Energy drinks, MREs, those sort of things"] =
                "يشمل مشروبات الطاقة وMREs والعناصر المشابهة.",
            ["Enter the Chapters menu to trigger it, enter the game, and trigger a save and they will stay unlocked permanently. If you do not see Very Hard or Revengeance difficulties, use the code to unlock all difficulties to make those 2 appear in the chapters menu"] =
                "ادخل قائمة Chapters لتفعيل الشفرة، ثم ادخل اللعبة وأنشئ عملية حفظ لتبقى الخيارات مفتوحة بصورة دائمة. إذا لم تظهر صعوبتا Very Hard وRevengeance، فاستخدم شفرة فتح جميع الصعوبات أولا لإظهار الصعوبتين (2) في قائمة Chapters.",
            ["Enter the Customization Menu to access everything, and save to keep them"] =
                "ادخل Customization Menu للوصول إلى كل العناصر، ثم احفظ للاحتفاظ بها.",
            ["Flares, grenades, those sort of things"] =
                "يشمل Flares والقنابل والعناصر المشابهة.",
            ["Get A Military Or Police Unit's Attention To Acquire All Upgrades + 999,999,999 EP Points, And They Will Ignore You"] =
                "اجذب انتباه وحدة عسكرية أو وحدة شرطة للحصول على جميع الترقيات و999,999,999 EP، وبعدها سيتجاهلونك.",
            ["If an enemy grabs you this will instantly complete the button masher for you. This won't work on sequences like the train in the beginning or other things like that, I was too lazy to try finding those"] =
                "إذا أمسك بك عدو، فستكمل الشفرة ضغط الأزرار المتكرر فورا. لا تعمل في مقاطع خاصة مثل القطار في بداية اللعبة أو الحالات المشابهة؛ ولم أبحث عن تلك الحالات الأخرى.",
            ["If you have no MREs and you pick up 1, you will have 1 MRE. You pick up another MRE and you will have 9"] =
                "إذا لم يكن لديك أي MRE والتقطت 1 MRE، فسيصبح لديك 1 MRE. وعند التقاط MRE آخر سيصبح العدد 9.",
            ["If you have the code to fly with L2 + X on, you will not need this and can just fly from transformer to transformer"] =
                "إذا كانت شفرة الطيران باستخدام L2 + X مفعلة، فلن تحتاج هذه الشفرة ويمكنك الطيران مباشرة من محول إلى آخر.",
            ["If you notice that magic is not full for anything other than the Fire Of Ares, it is because the other 3 elemental weapons require you to upgrade them to be able to use their magic abilities"] =
                "إذا لاحظت أن السحر ليس ممتلئا لغير Fire Of Ares، فالسبب أن الأسلحة العنصرية الثلاثة (3) الأخرى تحتاج إلى الترقية قبل أن تتمكن من استخدام قدراتها السحرية.",
            ["In Chapter 2, you will encounter a spot where you will need to control a Dwarf Gekko to continue on, and this works for it without marking the other Drawf Gekkos around"] =
                "في Chapter 2 ستصل إلى موضع يجب فيه التحكم في Dwarf Gekko للمتابعة، وتعمل الشفرة عليه دون تحديد بقية Dwarf Gekkos القريبة.",
            ["In the very beginning room in Chapter 4 after you go up the steps but before you get on the elevator, there are 2 turrets you can use on the enemies"] =
                "في الغرفة الأولى من Chapter 4، بعد صعود الدرج وقبل ركوب المصعد، توجد برجتان (2) يمكنك استخدامهما ضد الأعداء.",
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
                "مطلوب لجعل الأعداء يتجاهلونك عند الضغط على L3. فعل هذه الشفرة أولا (1st)، وإلا ستنهار اللعبة.",
            ["Requires the Button Enable Code to be activated once before this or the game will instantly freeze"] =
                "يتطلب تفعيل Button Enable Code مرة واحدة قبل هذه الشفرة، وإلا ستتجمد اللعبة فورا.",
            ["Requires the Marker codes for either the Dwarf Gekko in Chapter 2 or the Turrets in Chapter 4 or this will do nothing. The only unwanted effect of the code is that 1 enemy might not respond to damage for the 1st time they take damage, but after that the effect is gone again until you either control the Dwarf Gekko or Turrets again"] =
                "يتطلب Marker Codes الخاصة بـDwarf Gekko في Chapter 2 أو Turrets في Chapter 4، وإلا فلن تعمل الشفرة. الأثر الجانبي الوحيد هو أن عدوا واحدا (1) قد لا يستجيب للضرر في المرة الأولى (1st)؛ بعد ذلك يختفي الأثر حتى تتحكم في Dwarf Gekko أو Turrets مرة أخرى.",
            ["Requires the button enable code to be on 1st"] =
                "يتطلب تفعيل Button Enable Code أولا (1st).",
            ["Requires the button enable code to be turned on 1st"] =
                "يتطلب تفعيل Button Enable Code أولا (1st).",
            ["Requires the button enable code to be turned on 1st. People might not instantly stop, but once they do they will stay stopped until you toggle the code off"] =
                "يتطلب تفعيل Button Enable Code أولا (1st). قد لا يتوقف الأشخاص فورا، لكن بعد توقفهم سيبقون متوقفين حتى تعطل الشفرة.",
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
                "أثناء الرحلة داخل السفينة إلى الكوكب، يغادر Isaac المقعد في لحظة معينة ليتجه إلى مؤخرة السفينة ويثبت الحاويات الثلاث (3). تمنحك اللعبة عادة 60 ثانية بالضبط لإنهاء ذلك، وإذا لم تفعل فستموت فورا.",
            ["When you have the codes on to have all powers and to be able to use powers from both good and evil sides at the same time, the same still applies where you get Overload Burst with good karma and Arc Lightning with evil karma"] =
                "عند تفعيل شفرات جميع القوى وإتاحة قوى الخير والشر معا، تبقى القاعدة نفسها: تحصل على Overload Burst مع Good Karma وعلى Arc Lightning مع Evil Karma.",
            ["Without this, the game will only spawn a total of 40 treasure chests and then you will be stuck"] =
                "بدون هذه الشفرة ستنشئ اللعبة 40 صندوق كنز فقط، وبعدها سيتعذر عليك التقدم.",
            ["Works even if you do not do 1 headshot"] =
                "تعمل حتى إذا لم تنفذ Headshot واحدة (1).",
            ["Works even if you do not kill 1 enemy"] =
                "تعمل حتى إذا لم تقتل عدوا واحدا (1).",
            ["X + Left = Health Regeneration On X + Right = Health Regeneration Off X + Up = Unfreeze NPCs X + Down = Freeze NPCs"] =
                "التحكم: X + Left لتفعيل تجدد الصحة؛ X + Right لتعطيله؛ X + Up لإلغاء تجميد NPCs؛ X + Down لتجميد NPCs.",
            ["You can control the brightness from the Options Menu with this"] =
                "يمكنك التحكم في السطوع من Options Menu باستخدام هذه الشفرة.",
            ["You can not sell all your accessories or weapons. Delete the game from xmb and start from the disc, then after that just activate the code again"] =
                "لا تبع جميع الملحقات أو الأسلحة. احذف بيانات اللعبة من XMB وشغل اللعبة من القرص، ثم فعل الشفرة مجددا.",
            ["You can still be grabbed, but it is now far easier to break out of the grab and they can not damage your health"] =
                "لا يزال من الممكن الإمساك بك، لكن الإفلات أصبح أسهل بكثير، ولن يتمكنوا من خفض صحتك.",
            ["so only 1 can be active at a time"] =
                "لذلك يمكن تفعيل خيار واحد (1) فقط في الوقت نفسه.",
            // ChiDreams collection fd267a6 (2026-10-28): notes new to art.yml.
            ["Activate code, go in-game, upgrade with Gift Exp and save. DO NOT play with this enabled."] =
                "فعل الشفرة، ثم ادخل اللعبة، وقم بالترقية باستخدام Gift Exp واحفظ. لا تلعب والشفرة مفعلة.",
            ["Affects enemies too"] =
                "يؤثر على الأعداء أيضا.",
            ["Allows you to select custom camera following distances from the UI menu."] =
                "تتيح لك اختيار مسافات مخصصة لتتبع الكاميرا من قائمة UI.",
            ["Allows you to select custom game speeds from the UI menu."] =
                "تتيح لك اختيار سرعات لعب مخصصة من قائمة UI.",
            ["Allows you to select custom score/kill increment multipliers from the UI menu."] =
                "تتيح لك اختيار مضاعفات مخصصة لزيادة النقاط/القتل من قائمة UI.",
            ["Because of infinite health, some cars and some people can't be killed, it is one way out, change your weapon to a revolver-shotgun-rifle, the machine gun do not kill people and cars"] =
                "بسبب الصحة اللانهائية لا يمكن قتل بعض السيارات وبعض الأشخاص. الحل هو تغيير سلاحك إلى revolver أو shotgun أو rifle، لأن machine gun لا يقتل الأشخاص ولا السيارات.",
            ["Better Combine With Infinite Health"] =
                "يفضل استخدامها مع Infinite Health.",
            ["Beware, enemies will not drop key items with this."] =
                "تنبيه: لن يسقط الأعداء العناصر المهمة (key items) مع هذه الشفرة.",
            ["Characters can still be KO'ed if the damage is higher than their real base HP, so make sure to have reviving items."] =
                "قد تسقط الشخصيات (KO) إذا كان الضرر أعلى من HP الأساسية الحقيقية، لذا تأكد من امتلاك عناصر إحياء.",
            ["Check Albhed guy into Rin agency in Thunder Plains"] =
                "تحدث مع شخص Albhed داخل Rin agency في Thunder Plains.",
            ["Choose multiplier amount before launching"] =
                "اختر قيمة المضاعف قبل تشغيل اللعبة.",
            ["Combined code cave for Orbs, Powers, and Blade"] =
                "منطقة code cave مشتركة لشفرات Orbs وPowers وBlade.",
            ["Do not use outside of timed minigames."] =
                "لا تستخدمها خارج الألعاب المصغرة المؤقتة (timed minigames).",
            ["Do not use this code if you used Max CP Use Then Exit Crystarum or HP or Mag. You will have the Stats until you change your weapons"] =
                "لا تستخدم هذه الشفرة إذا استخدمت Max CP Use Then Exit Crystarum أو HP أو Mag. ستبقى الإحصاءات معك حتى تغير أسلحتك.",
            ["Do not use this code if you used Max CP Use Then Exit Crystarum or HP or STR. You will have the Stats until you change your weapons"] =
                "لا تستخدم هذه الشفرة إذا استخدمت Max CP Use Then Exit Crystarum أو HP أو STR. ستبقى الإحصاءات معك حتى تغير أسلحتك.",
            ["Do not use this code if you used Max CP Use Then Exit Crystarum or STR or Mag. You will have the Stats until you change your weapons"] =
                "لا تستخدم هذه الشفرة إذا استخدمت Max CP Use Then Exit Crystarum أو STR أو Mag. ستبقى الإحصاءات معك حتى تغير أسلحتك.",
            ["Dodge 1 Thunder for 150, hold L1 and then dodge again for 200."] =
                "تفاد 1 Thunder لتحصل على 150، ثم اضغط مع الاستمرار على L1 وتفاد مرة أخرى لتحصل على 200.",
            ["Doest Not Affects To Enemies"] =
                "لا يؤثر على الأعداء.",
            ["Get One of the Albhed Primer."] =
                "احصل على واحد من Albhed Primer.",
            ["Gift Debuffs Never Lost Effect On Enemy"] =
                "لا تفقد Gift Debuffs تأثيرها على العدو أبدا.",
            ["Hold Select While Damaging A Vehicle Or Object To Mark It For Invincibility, Select + L3 To Unmark"] =
                "اضغط مع الاستمرار على Select أثناء إلحاق الضرر بمركبة أو جسم لتمييزه بعدم القابلية للتدمير، واضغط Select + L3 لإزالة التمييز.",
            ["Infinite Health"] =
                "صحة لا نهائية.",
            ["Infinite Magic"] =
                "سحر لا نهائي.",
            ["Kill enemies holding R1 for Common drops."] =
                "اقتل الأعداء وأنت تضغط مع الاستمرار على R1 لتحصل على Common drops.",
            ["L1+R2 To Turn Enemies into Stone (TOC Pointer Fix)"] =
                "L1+R2 لتحويل الأعداء إلى حجر (نسخة مصححة لـ TOC Pointer).",
            ["Little Bit Buggy At Missions 1"] =
                "فيها بعض الأخطاء في Missions 1.",
            ["Master code cave. Relocated to safe executable block at 0x0075FA3C."] =
                "منطقة code cave الخاصة بـ Master code. نقلت إلى كتلة تنفيذية آمنة عند 0x0075FA3C.",
            ["Modifies AI movement speed."] =
                "يعدل سرعة حركة AI.",
            ["Modifies player jump height."] =
                "يعدل ارتفاع قفزة اللاعب.",
            ["Multiplies XP gained. Shifted for v1.04."] =
                "يضاعف XP المكتسبة. عدلت العناوين لتناسب v1.04.",
            ["NA"] =
                "غير متاح (NA).",
            ["Never go berserk in battle."] =
                "لا تدخل حالة berserk في المعركة أبدا.",
            ["Never use it in the car level."] =
                "لا تستخدمها أبدا في مرحلة السيارة.",
            ["Obtain the power to activate the code. It does infinite execution, wraith flash, combat drain, wraith burn, and fire arrow."] =
                "احصل على القدرة لتفعيل الشفرة. تمنح execution وwraith flash وcombat drain وwraith burn وfire arrow بلا حدود.",
            ["Only use at lvl 50 because this causes you to get 0 Sp on lvl up"] =
                "استخدمها فقط عند المستوى (lvl) 50 لأنها تسبب حصولك على 0 Sp عند رفع المستوى.",
            ["Only use the code after obtaining the powers or it will cause you problem. It gives max and infinite Storm Of Urfael, Flame Of Azkard, and Shadow Of Acharn."] =
                "استخدم الشفرة فقط بعد الحصول على القدرات، وإلا فستسبب لك مشاكل. تمنح Storm Of Urfael وFlame Of Azkard وShadow Of Acharn بأقصى قيمة وبلا حدود.",
            ["Only works if you have this power"] =
                "تعمل فقط إذا كانت لديك هذه القدرة.",
            ["Press L3 to end the counter if needed."] =
                "اضغط L3 لإنهاء العداد عند الحاجة.",
            ["Rage Of The Gods Instantly Refills"] =
                "يمتلئ Rage Of The Gods فورا.",
            ["Randomly Affects For Certain Enemies"] =
                "يؤثر بشكل عشوائي على بعض الأعداء.",
            ["Requires Button Enable Code to be ON. Hook branches to 0x0075FAB4."] =
                "يتطلب تفعيل Button Enable Code. يتفرع الـ hook إلى 0x0075FAB4.",
            ["Select + R1 = Item foward, Select + L1 = Item backward, Select + R2 = Set Ammo to 10, Select + L2 = Erase"] =
                "Select + R1 = العنصر التالي، Select + L1 = العنصر السابق، Select + R2 = ضبط الذخيرة على 10، Select + L2 = مسح.",
            ["Select your desired EXP multiplier from the configuration menu."] =
                "اختر مضاعف EXP الذي تريده من قائمة الإعدادات.",
            ["Sets money to 999,999,999 whenever a transaction occurs."] =
                "يضبط المال على 999,999,999 عند كل عملية شراء أو بيع.",
            ["Shoot one time to be invisible, does not work together with shield"] =
                "أطلق النار مرة واحدة لتصبح مخفيا، ولا تعمل مع الدرع (shield).",
            ["Tested On Boss Only"] =
                "جربت على الزعيم (Boss) فقط.",
            ["The AP Shown is not multiplied, but it will be as soon it gets added to your current EXP."] =
                "قيمة AP المعروضة لا تظهر مضاعفة، لكنها تتضاعف بمجرد إضافتها إلى EXP الحالية.",
            ["This code will only work for the first 10 slots in the Equipment list, put the cursor over the item, hold SELECT and press START = 4 slots and clear the abilities R3 = Maximum Weapon Power [] = Modify 1st ability /\\ = Modify 2nd ability R1 = Modify 3rd ability L1 = Modify 4th ability"] =
                "تعمل هذه الشفرة فقط مع أول 10 خانات في قائمة Equipment. ضع المؤشر فوق العنصر، واضغط مع الاستمرار على SELECT ثم اضغط START = 4 خانات ومسح القدرات، R3 = أقصى Weapon Power، [] = تعديل القدرة 1، /\\ = تعديل القدرة 2، R1 = تعديل القدرة 3، L1 = تعديل القدرة 4.",
            ["To max out in battle, you need to use an equipment for that (use all equipment code for Enterprise) I changed that because the original Max HP in battle was causing bugs."] =
                "للوصول إلى الحد الأقصى في المعركة تحتاج إلى استخدام معدات لذلك (استخدم شفرة all equipment الخاصة بـ Enterprise). غيرت ذلك لأن Max HP الأصلية في المعركة كانت تسبب أخطاء.",
            ["Turn code on, load game, save game, quit, and reload."] =
                "فعل الشفرة، ثم حمل اللعبة، واحفظها، واخرج، ثم أعد التحميل.",
            ["Turn number stays the same after player and enemy phase."] =
                "يبقى رقم الدور كما هو بعد مرحلة اللاعب ومرحلة العدو.",
            ["Use with infinite items so no item gets removed when saving."] =
                "استخدمها مع شفرة العناصر اللانهائية حتى لا يحذف أي عنصر عند الحفظ.",
            ["Used Hiei-YYH code for partner health to create a marker to separate your AI partner from enemies otherwise they would die from any damage too."] =
                "استخدمت شفرة صحة الشريك من Hiei-YYH لإنشاء Marker يفصل شريك AI عن الأعداء؛ وإلا فسيموت شريكك من أي ضرر مثلهم.",
            ["Vision Points Never Decrease Or Increase"] =
                "لا تنقص Vision Points ولا تزيد أبدا.",
            ["When in the Crystarum it will go down but just leave and it goes back to max"] =
                "داخل Crystarum ستنخفض القيمة، لكن بمجرد الخروج تعود إلى الحد الأقصى.",
            ["Will Not Work In 1 Hit Kill Mode"] =
                "لا تعمل في وضع 1 Hit Kill.",
            ["You will still see the bleeding effect, use it with the code to disable bleeding if needed."] =
                "سيظل تأثير النزيف ظاهرا، استخدمها مع شفرة تعطيل النزيف عند الحاجة.",
            ["does not Work place holder"] =
                "لا تعمل، وهي مجرد عنصر مؤقت (place holder).",
            ["not working next update"] =
                "لا تعمل حاليا، ومنتظر إصلاحها في التحديث القادم.",

        };
}

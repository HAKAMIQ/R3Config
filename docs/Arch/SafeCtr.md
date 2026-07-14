# عقود السلامة

## ملكية الملفات

| الهدف | المالك | سياسة R3Config |
|---|---|---|
| `rpcs3.exe` | RPCS3 | كشف فقط |
| game/firmware files | المستخدم/RPCS3 | metadata read فقط |
| `patches/patch.yml` | RPCS3 | read-only دائمًا |
| `patches/imported_patch.yml` | مشترك | تعديل محافظ، backup، validation |
| `patches/<TITLE_ID>_patch.yml` | R3Config فقط إذا marker صحيح | selected exact cheats للعبة واحدة؛ يرفض الملف غير المعلّم |
| `patches/r3cht.yml` | R3Config إذا marker صحيح | مكتبة مُدارة؛ لا يُعامل كملف runtime |
| `patches/r3config_cheats_patch.yml` | R3Config legacy إذا marker صحيح | قراءة للترحيل فقط؛ لا يُحذف تلقائيًا |
| `config/patch_config.yml` | مشترك | enabled state فقط مع حفظ الباقي |
| `config/custom_configs/config_<SERIAL>.yml` | مشترك | selected config paths فقط |
| `patches/r3hcan.json` | R3Config | metadata متزامن مع authored catalog |
| `patches/r3config_hcanary.json` | R3Config legacy | fallback عند غياب الاسم الجديد؛ لا يُحذف تلقائيًا |
| `config/R3Bkp/` | R3Config | manifests الجديدة والاستعادة |
| `config/R3ConfigBackups/` | R3Config legacy | العرض والاستعادة فقط |

ملف بلا owner وعقد واضح لا يجوز أن يصبح mutation target.

## بوابة كل mutation

قبل أول side effect:

1. install descriptor صالح ومبني من `rpcs3.exe`.
2. target list كاملة وغير فارغة وغير مكررة.
3. targets تحت `config` أو `patches` الصحيحين.
4. RPCS3 مغلق.
5. الخطة غير فارغة ومرتبطة بنفس install.
6. اختيار المستخدم صريح، وconfirmation موجود في presentation.
7. المحتوى النهائي بُني وتحقق منه قبل commit.

الـ UI guard للرسالة المبكرة فقط؛ `IRpcs3MutationGuard` داخل use case إلزامي.

## بروتوكول transaction

```text
normalize target paths
 -> build complete outputs in memory
 -> stage beside targets
 -> read-back + parse/schema/count validation
 -> one backup manifest for all targets
 -> atomic replace/move
 -> rollback all targets on any failure
 -> cleanup stages
 -> reload UI state from disk
```

لا backup بعد بدء الكتابة، ولا commit قبل اكتمال validation لكل الملفات.

## Config contract

- target يساوي `custom_configs/config_<SERIAL>.yml` المتوقع بالضبط.
- preserve unknown sections/keys/values.
- apply selected + different + actionable فقط.
- removal يزيل managed paths فقط.
- YAML غير المقروء يحجب الكتابة بدل استبداله بملف فارغ.
- تكرار نفس العملية idempotent.
- missing target يسجل `Existed=false`; restore يحذف الملف المنشأ.

## Patch enable contract

```text
ExplicitSelection
AND SerialCompatible
AND VersionCompatible
AND TrustedDefinitionWithValidHashKey
AND (RuntimeHashMatches OR RuntimeGuardCanDecide)
AND AuthoritativeMainPpuNeverContradicts
AND PreparedDefinitionValid
= ExactMatch OR RPCS3-runtime-guarded and enableable
```

- risk label ليس إثبات توافق.
- expert mode ليس bypass.
- `All` metadata صريحة، ولا تلغي valid hash-key requirement.
- no default selection.
- disable يسمح أيضًا `SerialVersionMatchHashUnknown` لأنه يزيل state.
- عند غياب local hash يسمح trusted serial/version match لأن RPCS3 يحسب hash وقت
  التشغيل ولا يستدعي patch block إلا بالاسم المطابق.
- بصمة PPU الرئيسية المخالفة من log/metadata أو cache حديث موثّق بعد EBOOT
  تعارض محظور. كاش PPU القديم وأدلة
  SPU/PRX/OVL جزئية: التطابق منها إثبات إيجابي، لكن الغياب أو مشاهدة وحدة أخرى
  ليسا `HashMismatch`.
- كل المصادر تستخدم `PatchCompatibilityService`. راجع
  [PatCmp.md](PatCmp.md).

## Catalog merge contract

- الهوية الحالية: `RuntimeHashKey|Title`، وتشمل `TITLE_ID` prefix إن وجد.
- resident يتقدم على bundled preview.
- comparable higher version قد تستبدل الأدنى في الرتبة نفسها.
- same identity + equivalent version + different definition = reject all
  conflicting variants مع diagnostic.
- فشل مصدر مستقل لا يمحو entries الصالحة من مصدر آخر.

## Selected patch compiler

- يقبل explicit activation-policy selections من المصدر المقصود.
- ينسخ المختار فقط مع dependencies/anchors الضرورية.
- يرفض missing dependency وdependency cycle.
- يرفض تعارض group.
- يحدد patch version المستهدف بصورة deterministic.
- يعيد قراءة الناتج ويقارن entry count.
- لا يستولي على user-owned file بلا managed marker.

## H-Canary

- analysis استشاري؛ complex/conflicting/unsupported يبقى manual review.
- لا port لتعليمات أو offsets بين نسخ دون reference/hash evidence.
- Save/Delete يعدلان `imported_patch.yml` وmetadata كمعاملة واحدة.
- كلاهما يمران بحارس UI وحارس use case.
- تحقق identities، operations، addresses، values، anchors، dependencies، cycles،
  targets، وround-trip parser.

## Untrusted input

- PARAM.SFO: magic/count/offset/length bounds.
- YAML: root/schema/version/key/type/instruction validation.
- gzip: compressed وexpanded size limits.
- cache: entry/depth limits.
- paths: لا تحول title/serial/source text إلى target path دون normalization.
- errors: لا تعرض secrets أو stack traces أو private payload.

## Failure matrix لأي writer جديد

1. existing target success.
2. missing target success.
3. invalid input = zero target changes.
4. stage failure.
5. mid-commit failure بعد ملف سابق.
6. rollback success يعيد byte-equivalent state.
7. rollback failure يعرض write + rollback errors.
8. missing-target restore يحذف created file.
9. unrelated content preserved.
10. duplicate/outside target rejected.
11. RPCS3-running rejected before file creation.
12. replay idempotent.

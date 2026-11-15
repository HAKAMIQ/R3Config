# R3Config

R3Config is a free, open-source Windows companion for RPCS3: per-game settings recommendations, Game / Canary / Artemis patches, compatibility checks, and backup before every change.

R3Config برنامج مجاني ومفتوح المصدر لـ Windows يساعد مستخدمي RPCS3 في مراجعة إعدادات كل لعبة وإدارة الباتشات، مع التحقق والنسخ الاحتياطي قبل أي تعديل.

## Download · التحميل

**[Latest release](https://github.com/HAKAMIQ/R3Config/releases/latest)**: extract the portable ZIP anywhere and run `R3Config.exe`. No installation is needed.

## How to use · طريقة الاستخدام

1. Click **Browse**, select your RPCS3 folder, then click **Analyze RPCS3 data**.
2. Pick a game, review the recommendations, and apply the settings or patches you want.
3. Launch the game from RPCS3. R3Config cannot be used while RPCS3 is running.
4. Close RPCS3.
5. Go back to R3Config and click **Analyze RPCS3 data** again.

1. اضغط «استعراض» وحدد مجلد RPCS3، ثم اضغط «تحليل بيانات RPCS3».
2. اختر اللعبة وراجع التوصيات، وطبّق الإعدادات أو الباتشات المطلوبة.
3. شغّل اللعبة من RPCS3، ولا يمكن استخدام R3Config أثناء تشغيل المحاكي.
4. أغلق المحاكي.
5. ارجع واضغط «تحليل بيانات RPCS3» من جديد.

## Requirements

- Windows 10 or 11 (x64) and a local RPCS3 installation.
- The package is not code-signed. If SmartScreen appears, verify the ZIP with its published SHA-256, then choose **More info** and **Run anyway**.

## Safety

- Game files and the official patch.yml are never modified.
- Patches are applied only after you select them and the serial, version, and executable hash match.
- Existing files are backed up before every write and can be restored.

## Build

```
.\Script\RelTest.cmd -Configuration Release
.\Script\PortPub.cmd -Rpcs3Path "C:\path\to\rpcs3"
```

Developer details: [docs/Build.md](docs/Build.md).

## License

Copyright (C) 2026 HAKAMIQ. Licensed under the [GNU GPL v3 or later](LICENSE). Bundled third-party materials keep their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

R3Config is not affiliated with the RPCS3 project and does not include RPCS3, PlayStation firmware, or games.

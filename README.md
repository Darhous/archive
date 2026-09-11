# Darhous Smart Archive

برنامج أرشفة إلكترونية Desktop لـ Windows (WPF / .NET 10 / SQLite FTS5)، يهدف لإدارة وفهرسة والبحث في حتى مليون مستند، مع نظام Plugins، Scanner/OCR، Automation، Backup، وAI اختياري.

## المراجع

- **الوثيقة الأم (المعمارية الكاملة):** [`docs/Darhous_Smart_Archive_Master_Documentation_v1.0_FINAL.md`](docs/Darhous_Smart_Archive_Master_Documentation_v1.0_FINAL.md)
- **خطة التنفيذ العملية (تتبع التقدم):** [`docs/EXECUTION_PLAN.md`](docs/EXECUTION_PLAN.md)

ابدأ دائمًا من `EXECUTION_PLAN.md` لمعرفة الحالة الحالية للمشروع.

## البناء محليًا

```bash
dotnet restore Darhous.Archive.slnx
dotnet build Darhous.Archive.slnx
dotnet test Darhous.Archive.slnx
```

المتطلبات: .NET 10 SDK (مثبَّت الإصدار في `global.json`).

## هيكل الـRepository

```text
src/            مشاريع الإنتاج (Core, Modules, Desktop, ...)
tests/          مشاريع الاختبارات
Modules/        (داخل src/) Business modules
Workers/        (داخل src/) عمليات خارجية معزولة (Scanner/OCR/Indexer/...)
OfficialPlugins/(داخل src/) Plugins رسمية تُبنى بنفس عقد Plugin SDK
tools/          أدوات تطوير مساعدة
build/          سكربتات وإعدادات البناء
installer/      حزمة التثبيت (Velopack)
docs/           التوثيق (المرجع المعماري + خطة التنفيذ)
```

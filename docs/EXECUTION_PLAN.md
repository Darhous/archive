# Darhous Smart Archive — خطة التنفيذ الكاملة (Execution Plan)

**الحالة:** Active Reference — يُحدَّث أول بأول أثناء التنفيذ
**المرجع الأساسي:** [`Darhous_Smart_Archive_Master_Documentation_v1.0_FINAL.md`](./Darhous_Smart_Archive_Master_Documentation_v1.0_FINAL.md) (كلاهما داخل `docs/`)
**آخر تحديث:** 2026-09-11

---

## 0. الغرض من هذا الملف

هذا الملف هو **المرجع العملي الوحيد** لتتبع التنفيذ. الوثيقة الأم (15,400+ سطر) هي مصدر الحقيقة المعمارية والتفصيلية، لكنها كبيرة جدًا لإعادة قراءتها كل مرة. هذا الملف:

- يلخّص كل مرحلة تنفيذ (0 → 24) في صيغة Checklist قابلة للتحديث.
- يشير لرقم القسم الدقيق في الوثيقة الأم لأي تفصيل إضافي مطلوب.
- يسجّل **القرارات المعتمدة** التي عُدِّلت أو أُضيفت أثناء المراجعة (2026-09-11) والتي لا تزال غير مدمجة بصريًا في كل زاوية من الوثيقة الأم.
- يُستخدم كنقطة استئناف فورية لو حصل Compact أو انقطاع في الجلسة — اقرأ القسم 1 (الحالة الحالية) أولًا دائمًا.

**قاعدة تحديث:** كل مرة تكتمل فيها مهمة أو مرحلة، حدّث الـCheckbox هنا وسجّل التاريخ في "سجل التقدم" (القسم 9). لا تُعِد كتابة تفاصيل معمارية هنا — فقط أشِر لرقم القسم في الوثيقة الأم.

---

## 1. الحالة الحالية (Current State)

> **حدّث هذا القسم يدويًا كل مرة تبدأ فيها جلسة عمل جديدة.**

- **المرحلة الحالية:** Phase 0 (Bootstrap) منجزة محليًا. بانتظار `git init` + أول Push لـ`main` على GitHub، وتشغيل CI فعليًا كتحقق نهائي.
- **Repository:** https://github.com/Darhous/archive (فاضي على GitHub، الكود جاهز محليًا للـPush).
- **آخر مرحلة مكتملة:** Phase 0 — Bootstrap (محليًا؛ لم يُدفع للـremote بعد).
- **العمل القادم:** دفع الكود لـGitHub، ثم البدء في Phase 1 — Core Foundation (§29).
- **عوائق مفتوحة:** لا يوجد.

---

## 2. القرارات المعتمدة أثناء المراجعة (2026-09-11)

هذه قرارات فعلية طُبِّقت على الوثيقة الأم مباشرة (وليست اقتراحات معلّقة). أُدرجها هنا لسهولة الرجوع دون البحث في 15 ألف سطر:

| # | القرار | مكانه في الوثيقة الأم |
|---|---|---|
| 1 | إصلاح تصادم ترقيم الأقسام في SAD وDatabase Spec (كان ناتجًا عن دمج Discovery لاحقًا) | SAD §47+, DB Spec §50-59+ |
| 2 | **نطاق Discovery الافتراضي:** الفحص الأول ليس "كل الأقراص" تلقائيًا — Onboarding يطلب من المستخدم اختيار مجلدات البداية، و"فحص الكمبيوتر بالكامل" خيار إضافي صريح | SAD §46.9 |
| 3 | سياسة `ON DELETE` موحّدة لكل Foreign Key (CASCADE / RESTRICT / SET NULL) | DB Spec §107.1 |
| 4 | تفعيل `prefix = '2 3 4'` في جدول `documents_fts` (كان ناقصًا رغم وعد SAD §25 بـ Prefix Search) | DB Spec §89 (FTS Table) |
| 5 | مكتبة Argon2id محددة: `Konscious.Security.Cryptography.Argon2` (Memory=64MB, Iterations=3, Parallelism=2) | DB Spec (قسم app_users / Passwords) |
| 6 | هيكل Solution النهائي = Core Implementation Plan §5، وليس SAD §83 (الأخير أقدم وغير مكتمل) | SAD §83 (ملاحظة مضافة) |
| 7 | **أول نسخة داخلية قابلة للاستخدام اليومي** = نهاية Phase 8 (Search Foundation)، قبل Scanner/OCR/Discovery/AI/Telegram | Implementation Plan §141.1 |

**فجوات لا تزال مؤجلة عمدًا (لن تعطّل البدء، تُحسم في وقتها):**
- ADRs الرسمية (Context/Decision/Consequences) — تُكتب تدريجيًا أثناء التنفيذ، ليست شرطًا لبدء Phase 0.
- كتالوج رسائل IPC التفصيلي لكل Worker — مطلوب قبل Phase 13 (Worker Infrastructure) فقط.
- Wireframes للشاشات المتبقية (~11 من 21) — تُنتج مع كل Phase تدريجيًا.
- تفاصيل شهادة التوقيع الرقمي + Velopack feed — تُحسم قبل Phase 24 (Installer) فقط.

---

## 3. الترتيب الإلزامي للتنفيذ (Mandatory Development Order)

من Implementation Plan §140 — **لا يجوز تجاوز هذا الترتيب:**

```text
Core → Persistence → Authentication → Audit → Documents → Folders →
Explorer UI → Search → Discovery → Import → Preview → Plugin Host →
Workers → Scanner → OCR → Notifications → Backup → Updates → AI →
Performance → Installer
```

**ممنوع البدء مبكرًا بـ** (§141): AI, Telegram, Fancy animations, Online plugin store, n8n, Multi-user network mode, Mobile app, Cloud sync — قبل استقرار Core (نهاية Phase 8).

---

## 4. Milestones (من §139)

| Milestone | المحتوى | يقابل المراحل |
|---|---|---|
| M0 | Foundation / Repository | Phase 0 |
| M1 | Core + DB + Login + Audit | Phase 1-4 |
| M2 | Documents + Folders + Explorer | Phase 5-7 |
| **M3** | **Search + Discovery + Continuous Indexing** ← أول نسخة استخدام يومي بعد Phase 8 | Phase 8-9 |
| M4 | Import + Preview | Phase 10-11 |
| M5 | Plugin Platform + Workers | Phase 12-13 |
| M6 | Scanner + OCR | Phase 14-15 |
| M7 | Notifications + Telegram + Reports | Phase 16-17 |
| M8 | Backup + Updates | Phase 18-19 |
| M9 | AI Foundation + Health | Phase 20-21 |
| M10 | Performance + Recovery + Installer | Phase 22-24 |
| M11 | Release Candidate | Gate §137 |
| M12 | Production v1.0 | Gate §138 |

---

## 5. تفصيل المراحل (Phase Checklist)

كل مرحلة: `[ ]` لم تبدأ · `[~]` جارية · `[x]` مكتملة (حدّثها يدويًا).

### Phase 0 — Bootstrap `[x]`
*مرجع: Implementation Plan §28*
- [x] Create repository (`github.com/Darhous/archive`، لسه ما اتـpush له من هنا)
- [x] Create solution (`Darhous.Archive.slnx` — صيغة .NET 10 الجديدة؛ الهيكل يتبع Implementation Plan §5، وليس SAD §83)
- [x] Directory.Build.props / Directory.Packages.props / global.json (SDK مثبَّت 10.0.302)
- [x] Nullable + Analyzers + Formatting (`.editorconfig` بقواعد التسمية من §19)
- [x] Serilog bootstrap (`Darhous.Archive.Core/Hosting/ArchiveHostDefaults.cs` — Console + Debug + Rolling File تحت `%ProgramData%\DarhousSmartArchive\Logs\<component>`)
- [x] DI host (Microsoft.Extensions.Hosting عبر نفس `ArchiveHostDefaults`)
- [x] CI أساسي (`.github/workflows/ci.yml` — windows-latest، restore/build/test، يرفع test results كـartifact)
- [x] `.gitignore`, `.editorconfig`, `LICENSE`, `README.md`
- **DoD:** `dotnet build` و `dotnet test` نجحا محليًا (0 Warnings / 0 Errors، 2/2 tests passed). ⏳ التحقق النهائي من "clean checkout" يتم فعليًا أول ما يشتغل CI بعد أول Push.

**ملاحظة نطاق:** Phase 0 أنشأ فقط `Darhous.Archive.Core` (كمكتبة تحمل الـHosting bootstrap) و`Darhous.Archive.Core.Tests` — الحد الأدنى لإثبات إن pipeline البناء/الاختبار/الـLogging شغّال. باقي مشاريع §5 (Contracts, Application, Persistence, Desktop, Modules, Workers...) تُضاف تباعًا بداية من Phase 1.

### Phase 1 — Core Foundation `[ ]`
*مرجع: §29-31*
- [ ] Core, Contracts, Application skeleton
- [ ] Configuration, Security abstractions
- [ ] Event Bus, Job abstractions, Health model
- [ ] Result Model (`Result` / `Result<T>` مع ErrorCode/Message/TechnicalDetails/IsTransient)
- [ ] Correlation (CorrelationId لكل Command/Job/Event/Worker/Plugin request)
- **DoD (§131):** App starts, Logging, DI, Configuration, archive.db/audit.db تفتح، migrations، login، roles، event bus، jobs، outbox، health، shutdown آمن.

### Phase 2 — Persistence Foundation `[ ]`
*مرجع: §32-33*
- [ ] archive.db, audit.db, search.db
- [ ] Migrations (FluentMigrator)
- [ ] Dapper repositories, Transactions, Write queue, Health checks
- [ ] **تطبيق سياسة ON DELETE من DB Spec §107.1 في كل Migration**
- [ ] **تفعيل `PRAGMA foreign_keys = ON`**
- [ ] First migration: roles, app_users, documents, document_versions, folders, tags, document_tags, jobs, outbox_events, app_settings, schema_migrations

### Phase 3 — Authentication & Roles `[ ]`
*مرجع: §34-35*
- [ ] Login / Logout / Switch User / Remember Me / Guest Mode
- [ ] Admin/User/ReadOnly roles + Last Admin protection
- [ ] Password hashing عبر Argon2id (`Konscious.Security.Cryptography.Argon2`)
- [ ] Login UI

### Phase 4 — Audit `[ ]`
*مرجع: §36*
- [ ] audit.db + Audit service
- [ ] Critical immediate writes + Buffered normal writes
- [ ] تسجيل من أول نسخة: login, logout, search, sort, filter, add, move, delete
- [ ] Audit viewer query + Export hooks

### Phase 5 — Document Core `[ ]`
*مرجع: §37-38*
- [ ] Document entity, Version entity, Archive number
- [ ] Managed storage + Indexed-in-place
- [ ] Hash (SHA-256) + Duplicate detection
- [ ] Recycle Bin + Restore + Version history
- [ ] `IFileStorageService`: Stage/Commit/Open/Move to recycle/Restore/Delete permanently/Hash/Validate
- **DoD (§132):** Add/version/hash/duplicate warning/move/trash/restore/permanent delete (Admin-only)/audit/indexed-in-place/managed storage.

### Phase 6 — Folder System `[ ]`
*مرجع: §39*
- [ ] Logical folders + Tree + Subfolders
- [ ] Move / Rename / Delete rules / Unclassified
- [ ] Bulk move + Undo snapshot

### Phase 7 — Archive Explorer UI `[ ]`
*مرجع: §40-41*
- [ ] Sidebar, Folder tree, Search strip, Document list
- [ ] Preview placeholder, Breadcrumb, Bulk actions
- [ ] Density modes, Theme
- **Gate إلزامي قبل الاستمرار:** UI يبقى responsive مع 100,000 fake rows (virtualization).

### Phase 8 — Search Foundation `[ ]` ⭐ (نهاية أول Milestone استخدام يومي)
*مرجع: §42-44*
- [ ] Official Plugin: `Darhous.Search.SqliteFts`
- [ ] Arabic normalization (أ/إ/آ/ٱ→ا, ى→ي, إزالة تشكيل/تطويل)
- [ ] FTS indexing مع `prefix = '2 3 4'` (مُصلَح بالفعل في DB Spec)
- [ ] Search query, Snippets, Ranking, Filters, Pagination, Rebuild
- [ ] Search debounce 250ms
- [ ] Search Audit (يسجل Query الفعلي فقط، لا keystrokes)
- **DoD (§134):** Arabic normalization, title/filename/metadata/body search, snippets, filters, sort, folder/all-archive scope, FTS rebuild, corruption recovery.

> **✅ عند اكتمال Phase 8: أول نسخة داخلية قابلة للاستخدام اليومي فعليًا (§141.1).**

### Phase 9 — Automatic Computer Discovery `[ ]`
*مرجع: §45-53*
- [ ] Module: `Darhous.Archive.Modules.Discovery`
- [ ] **تنفيذ Onboarding أولًا (SAD §46.9): اختيار مجلدات البداية قبل أي فحص كامل**
- [ ] Discovery scope: Local fixed drives + User-selected removable + watch folders
- [ ] Supported extensions: .pdf .doc .docx .xls .xlsx .ppt .pptx .msg .eml
- [ ] Technical exclusions (Windows, Program Files, System Volume Information, $Recycle.Bin, Temp, Darhous internal)
- [ ] User exclusions (Drive/Folder/Subfolder)
- [ ] Initial Discovery Flow: enumerate → exclusions → count → summary → user starts
- [ ] Continuous Indexing: FileSystemWatcher + Hourly + Daily reconciliation
- [ ] Discovery Safety: لا فتح كامل للملف أثناء enumeration (path+extension+size+timestamps فقط)
- [ ] Job model: `DiscoveryScanJob`, `FileIndexJob`, `MissingFileReconcileJob`
- [ ] "فحص الكمبيوتر بالكامل" كخيار إضافي صريح بعد Onboarding
- **DoD (§133):** drives, exclusions, count, summary, indexing start, file watcher, hourly/daily reconciliation, missing file state, no recursion loops, UI responsive.

### Phase 10 — Importers `[ ]`
*مرجع: §54-58*
- [ ] PDF Importer (text layer detection, extract, page count, metadata)
- [ ] DOCX/XLSX/PPTX عبر Open XML SDK
- [ ] Legacy DOC/XLS/PPT عبر Worker/Interop (ليس داخل UI process)
- [ ] MSG/EML Importer (Subject/From/To/CC/Date/Body/Attachments metadata)

### Phase 11 — Preview `[ ]`
*مرجع: §59-60*
- [ ] PDF Preview, Office preview fallback, Metadata preview
- [ ] Versions tab, Activity tab (lazy-loaded)
- [ ] قاعدة: فتح Preview يسجل `preview_document` وليس `open_document`

### Phase 12 — Plugin Platform `[ ]`
*مرجع: §61-62*
- [ ] Plugin Host + Manifest validation + Package import
- [ ] Signature verification + Permissions
- [ ] Install/Enable/Disable/Update/Rollback/Remove/Health/Quarantine
- [ ] Plugin Test Host (قبل السماح لـ Third-party plugins)
- **DoD (§135):** جاهز حسب Plugin SDK v1.0 end-to-end test.

### Phase 13 — Worker Infrastructure `[ ]`
*مرجع: §63-64*
- [ ] Named Pipes + Handshake + Session token
- [ ] Health, Restart policy, Crash loop detection, Quarantine
- [ ] Worker Protocol: Length-prefixed UTF-8 JSON
- [ ] ⚠️ **كتابة كتالوج رسائل IPC الفعلي هنا (فجوة كانت مؤجلة — الآن وقتها)**

### Phase 14 — Scanner `[ ]`
*مرجع: §65-67*
- [ ] Official Plugin: `Darhous.Scanner.Naps2`
- [ ] Worker: `Darhous.Archive.Scanner.Worker`
- [ ] Capabilities: ADF/Flatbed/Single/Duplex/300 DPI/Color/Grayscale/B&W/page separation
- [ ] Seed profiles: A4 Single 300 DPI OCR Arabic, A4 Every Page, A4 Every 2 Pages, Duplex, Flatbed

### Phase 15 — OCR `[ ]`
*مرجع: §68-70*
- [ ] Official Plugin: `Darhous.Ocr.Tesseract`
- [ ] Worker: `Darhous.Archive.Ocr.Worker`
- [ ] Flow: text layer? → yes: extract / no: OCR → searchable → index
- [ ] قاعدة: لا يغير الشكل البصري للمستند

### Phase 16 — Notifications `[ ]`
*مرجع: §71-73*
- [ ] In-app Notification Center + Windows Toast
- [ ] Official provider: `Darhous.Notifications.Windows`
- [ ] Official Plugin: `Darhous.Notifications.Telegram` (يستخدم Outbox)

### Phase 17 — Reports & Export `[ ]`
*مرجع: §74*
- [ ] Excel, PDF, CSV, Print
- [ ] Saved views export + Audit export

### Phase 18 — Backup & Restore `[ ]`
*مرجع: §75-77*
- [ ] Official Plugin: `Darhous.Backup.Local` (Local folder/Another drive/USB)
- [ ] Backup Flow: validate → SQLite Online Backup → copy managed files (Full) → checksums → package → verify
- [ ] Restore Flow: validate → safety backup → stop writes → restore → migrate → rebuild search → verify

### Phase 19 — Updates `[ ]`
*مرجع: §78-80*
- [ ] Check for updates, Manual install, Auto-check/install, Rollback
- [ ] Update Safety: verify signature → backup DB if migration → stage → rollback point

### Phase 20 — AI Foundation `[ ]`
*مرجع: §81-83*
- [ ] `IAiProvider` abstraction فقط أولًا + AI settings + Secrets + Worker + Privacy warning
- [ ] Official Plugins لاحقًا: OpenAI, Gemini, Claude, OpenRouter
- [ ] قاعدة: AI لا يدخل في Core Document Save transaction

### Phase 21 — System Health `[ ]`
*مرجع: §84-85*
- [ ] صفحة Health: Core/DB/Search/Scanner/OCR/Automation/Telegram/AI/Backup/Plugins
- [ ] Actions: Retry, Restart worker, Rebuild index, View logs, Disable plugin, Open settings

### Phase 22 — Performance Validation `[ ]`
*مرجع: §86-88*
- [ ] Datasets: 10k / 100k / 500k / 1M
- [ ] قياس: Startup, Folder open, Search, Sort, Filter, Bulk move, Import, Indexing, FTS rebuild, Backup, Memory, CPU, Disk IO, WAL growth
- [ ] Targets: Folder first page < 300ms، Search شائع < 1s، Metadata action < 200ms

### Phase 23 — Recovery Testing `[ ]`
*مرجع: §89*
- [ ] Power loss, Worker crash, DB lock, Search/Audit corruption
- [ ] Failed migration, Failed update, Missing file, Disconnected USB
- [ ] Scanner/OCR/Plugin crash

### Phase 24 — Installer `[ ]`
*مرجع: §90-92*
- [ ] Install app + prerequisites, Create ProgramData folders, Set ACLs
- [ ] Register app, Start Menu shortcut, Configure updater
- [ ] Technology: Velopack (أو equivalent)
- [ ] ⚠️ **حسم تفاصيل التوقيع الرقمي والـupdate feed هنا (فجوة كانت مؤجلة — الآن وقتها)**

---

## 6. Release Gates

### Release Candidate Gate (§137) — لا RC قبل:
- [ ] Core tests
- [ ] Migration tests
- [ ] Recovery tests
- [ ] 100k performance suite
- [ ] Plugin lifecycle tests
- [ ] Backup/restore tests
- [ ] Installer test

### Production Gate (§138) — لا v1.0 Production قبل:
- [ ] 1M dataset benchmark
- [ ] Search rebuild test
- [ ] Full backup/restore
- [ ] Power interruption test
- [ ] Worker crash test
- [ ] Plugin rollback test
- [ ] Update rollback test
- [ ] Long-run indexing test

---

## 7. Definition of Done — مرجع سريع

| المكوّن | القسم في الوثيقة الأم |
|---|---|
| Core Foundation | Implementation Plan §131 |
| Document Foundation | §132 |
| Discovery | §133 |
| Search | §134 |
| Plugin Platform | §135 (= Plugin SDK v1.0 e2e test) |
| UI | §136 |

---

## 8. قواعد لا يجوز كسرها أثناء التنفيذ

من "Final Rules" (§150-154) و"ما لا يجب فعله مبكرًا" (§141):

- لا AI / Telegram / Fancy animations / Online plugin store / n8n / Multi-user / Mobile / Cloud sync قبل استقرار Core.
- كل Workaround مؤقت يُسجَّل TODO رسمي (Issue/Owner/Reason/Removal condition) — لا TODO مجهولة داخل Core.
- أي Feature تتجاوز Performance Budget لا تُدمج قبل إصلاحها أو ADR واضح.
- Plugin لا يعدّل Core tables مباشرة (يستخدم Core API أو جداول خاصة `Plugin_<PluginId>_*`).
- Cloud AI = Disabled افتراضيًا دائمًا.
- Plugins غير موقعة = مرفوضة افتراضيًا (إلا في Developer Mode).

---

## 9. سجل التقدم (Progress Log)

سجّل هنا كل إنجاز فعلي بتاريخه (سطر واحد يكفي):

```text
2026-09-11 — التوثيق الأم اكتمل ومُراجَع، الفجوات التقنية والمنتجية سُدَّت، هذه الخطة أُنشئت. لم يبدأ التنفيذ البرمجي بعد.
2026-09-11 — Phase 0 (Bootstrap) مكتملة محليًا: Repository structure, .slnx solution, Directory.Build.props/Packages.props, global.json (net 10.0.302), .editorconfig, .gitignore, Darhous.Archive.Core + ArchiveHostDefaults (Serilog+Hosting bootstrap), Darhous.Archive.Core.Tests (2/2 tests passed), CI workflow (windows-latest), README, LICENSE. dotnet build/test نجحا محليًا. لسه محتاج git init + push لـ GitHub.
```

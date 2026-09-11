# Darhous Smart Archive — خطة التنفيذ الكاملة (Execution Plan)

**الحالة:** Active Reference — يُحدَّث أول بأول أثناء التنفيذ
**المرجع الأساسي:** [`Darhous_Smart_Archive_Master_Documentation_v1.0_FINAL.md`](./Darhous_Smart_Archive_Master_Documentation_v1.0_FINAL.md) (كلاهما داخل `docs/`)
**آخر تحديث:** 2026-09-11

---

## 0.1 قواعد تشغيل دائمة (2026-09-11) — Multi-Agent Orchestration Mandate

هذه قواعد تشغيل ثابتة للمشروع بالكامل، مسجّلة هنا لأنها يجب أن تنجو من أي Compact/Reset/Session جديدة. **اقرأها قبل أي استئناف عمل.**

- **الطبقة الهندسية المساعدة:** `C:\AI-AgentFlow-Test` — نظام Multi-Agent حقيقي ومُختبَر (Level 0-4 complexity-scored orchestrator في `scripts/orchestrator/Invoke-Orchestration.ps1`)، بيوجّه المهام لـ 3 CLIs: `claude` (Claude Sonnet 5)، `codex` (GPT-5.6 Sol)، `agy` (Gemini 3.1 Pro High). كل الـ3 مثبَّتين وشغّالين على الجهاز (تم التحقق 2026-09-11).
- **قاعدة التوجيه:** سهل → أنفّذه مباشرة. متوسط → Worker واحد. صعب → Workerين. حرج/معماري/أمني/DataLoss → الثلاثة. Claude Code (أنا) هو الـMaster Orchestrator وصاحب القرار النهائي دائمًا — الـWorkers يُستشارون ولا يُفوَّض لهم القرار.
- **الاستمرارية:** لا أتوقف بسبب Bugs عادية، اختلافات نماذج، Worker غير متاح مؤقتًا، أو قرارات تقنية روتينية. أسأل فقط عند: (1) قرار Product/Business حقيقي لا يُستنتج من التوثيق، (2) الحاجة لـCredentials/Authorization خارجي، (3) إجراء تدميري/غير قابل للعكس.
- **حدود حقيقية يجب الوضوح فيها:** لا أستطيع "الاستمرار تلقائيًا بعد Reset" حرفيًا لو انتهت الجلسة تمامًا (Process kill / Usage limit ينهي الجلسة) — محتاج الجلسة تتفتح تاني (بمعرفة Ahmed أو Scheduled trigger). الآلية الفعلية لضمان الاستمرارية: هذا الملف نفسه — كل استئناف عمل يبدأ بقراءته (قسم 1: الحالة الحالية) بدل الاعتماد على سجل المحادثة.
- **الهدف المتفق عليه:** تنفيذ متواصل حتى نهاية Phase 22 (Performance Validation)، بأعلى جودة وأقل أخطاء وأعلى إنتاجية وأقل استهلاك Context ممكن.

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

- **المرحلة الحالية:** Phase 3 (Authentication & Roles) مكتملة، جاري تجهيز الـCommit والدفع، ثم Phase 4 (Audit) مباشرة (راجع §0.1 قواعد التشغيل).
- **Repository:** https://github.com/Darhous/archive — Phase 0/1/2 مدفوعة على `main`، CI شغّال.
- **آخر مرحلة مكتملة:** Phase 3 — Authentication & Roles (محليًا؛ 91/91 اختبار ناجح على مستوى الحل، Release build نظيف، تحقق تشغيلي فعلي للـexe).
- **العمل القادم:** Commit + Push لـPhase 3، ثم Phase 4 — Audit (§36): audit.db، Audit service حقيقي (Critical immediate writes + Buffered normal writes)، تسجيل login/logout/search/sort/filter/add/move/delete من أول نسخة تشغيلية، Audit viewer query.
- **عوائق مفتوحة:** لا يوجد. **دين تقني متبقٍ** (§142): `outbox_events.user_id` لسه NULL دايمًا (TODO موثّق في الكود). **ملاحظة:** حساب Admin افتراضي اتنشأ فعليًا على %ProgramData% الجهاز الحقيقي أثناء اختبار التشغيل — كلمة المرور العشوائية اتعرضت مرة واحدة واتقفلت قبل الالتقاط. Ahmed يقدر يمسح `%ProgramData%\DarhousSmartArchive` لإعادة البدء من الصفر، أو يشغّل التطبيق ويلتقط الشاشة بنفسه.

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

### Phase 1 — Core Foundation `[x]`
*مرجع: §29-31*
- [x] Core, Contracts, Application skeleton — 5 مشاريع: `Darhous.Archive.Contracts` (DTOs/enums، بدون Business Logic)، `Darhous.Archive.Core` (ممتد)، `Darhous.Archive.Application` (Dispatcher + ICommandHandler/IQueryHandler عبر reflection)، `Darhous.Archive.Configuration`، `Darhous.Archive.Security`
- [x] Configuration (AppPaths per §92-93، ThemeOptions، UpdateOptions، IFeatureFlagProvider، IAppSettingsStore + In-Memory impl مؤقت لحد Persistence)
- [x] Security abstractions (IPasswordHasher + **Argon2idPasswordHasher فعلي** يطابق القرار المعتمد، ISecretProtector + **DpapiSecretProtector فعلي**، ArchivePrincipal/UserRole/Guest)
- [x] Event Bus (`IEventBus` + `InMemoryEventBus` — Transient tier فقط؛ Reliable/Critical يرميان `NotSupportedException` صراحةً لحد ما الـOutbox يتبني في Phase 2 — لا Silent downgrade)
- [x] Job abstractions (`IBackgroundJob`, `IJobContext`, `JobStatus`, `JobMetadata` في Contracts)
- [x] Health model (`IHealthContributor`, `IHealthRegistry` + `HealthRegistry` — Health check فاشل لمكوّن واحد ما بيمنعش باقي الفحوصات، اتغطى باختبار)
- [x] Result Model (`Result` / `Result<T>` مع Error{Code,Message,TechnicalDetails,IsTransient} — في `Darhous.Archive.Core.Results`)
- [x] Correlation (`CorrelationId` + `ICorrelationContextAccessor`/`AsyncLocalCorrelationContextAccessor`)
- [x] Module Registry (`IArchiveModule`, `IModuleRegistry` + `ModuleRegistry`) + Permissions abstractions (`UserRole`, `Permission`, `WellKnownPermissions` مطابقة لـSAD §75، `IPermissionEvaluator` — التطبيق الفعلي للـmatrix في Phase 3)
- [x] 33 اختبار (xUnit) عبر 4 مشاريع Tests، كلها Passed. `TreatWarningsAsErrors` مفعّل على مشاريع src (0 warnings).
- **DoD (§131) — الجزء المُغطى في Phase 1:** Logging ✅، DI ✅، Configuration ✅ (abstractions). البقية (App starts فعليًا، archive.db/audit.db، migrations، login، roles، jobs الحقيقية، outbox، shutdown آمن) تُغطى تراكميًا مع Phase 2-4 كما هو موضح أصلاً في §2 من هذا الملف (فجوة "مؤجلة عمدًا" — ليست نقص في Phase 1).
- **ملاحظة نطاق:** لم يُبنَ بعد: Plugin Host الفعلي (Phase 12)، Outbox/Reliable delivery (Phase 2)، Role→Permission matrix الفعلي (Phase 3)، DB-backed AppSettingsStore (Phase 2). كل ده متعمَّد ومُوثَّق في كل ملف كتعليق `///`.

### Phase 2 — Persistence Foundation `[x]`
*مرجع: §32-33 — القرار التصميمي راجعته الـ3 نماذج عبر AgentFlow (Level 4)، التفاصيل في `.agentflow/task-state.json` (TaskId `20260911-184256-d96307b1`) و§0.1*
- [x] archive.db, audit.db, search.db — 3 ملفات SQLite مستقلة، WAL مفعّل ومُتحقَّق منه (يرمي استثناء لو فشل)، كل واحدة عندها `SqliteDatabaseHealthContributor`
- [x] Migrations (FluentMigrator) — Runner مستقل تمامًا لكل DB (مش Runner واحد مشترك بـTags فقط)، `SchemaMigrationsMetadata` يسمّي جدول الإصدارات `schema_migrations` بدل `VersionInfo` الافتراضي
- [x] Dapper repositories (`IRoleRepository`/`RoleRepository` — الكيان الوحيد المُثبَت في Phase 2 عن قصد)، Transactions (`IUnitOfWork`/`SqliteUnitOfWork` — عملية = عنصر واحد في الـWrite Queue)، Write queue (`SqliteWriteQueue` — `System.Threading.Channels`، اتصال كتابة واحد دائم لكل DB، Connection واحدة فقط تكتب أبدًا = مفيش SQLITE_BUSY داخلي)، Health checks (`SqliteDatabaseHealthContributor` × 3، مسجَّلة كـ`IHealthContributor`)
- [x] **تطبيق سياسة ON DELETE من DB Spec §107.1 في كل Migration** — مُتحقَّق باختبارات فعلية (Folder delete → SET NULL، Document delete → CASCADE للـVersions/Tags، Role قيد الاستخدام → RESTRICT يمنع الحذف، Current Version قيد الاستخدام → RESTRICT حتى يُعاد تعيينه)
- [x] **تفعيل `PRAGMA foreign_keys = ON`** — مُتحقَّق باختبار فعلي (`PRAGMA foreign_keys` = 1)
- [x] First migration: roles, app_users, documents, document_versions, folders, tags, document_tags, jobs, outbox_events, app_settings, schema_migrations — SQL خام (مش Fluent API) لأن SQLite DDL (inline FK، لا ALTER ADD CONSTRAINT) ما بيتوافقش مع الـAbstraction الموحدة لـFluentMigrator
- [x] **`Darhous.Archive.Application/Persistence/`**: عقود مستقلة عن SQLite (`IUnitOfWork`, `IUnitOfWorkContext`, `IRoleRepository`, `IOutboxWriter`, `Role`) — تحضيرًا لهجرة PostgreSQL/Multi-user مستقبلية (SAD §97) بدون إعادة كتابة
- [x] **`OutboxEventBus`**: استبدل `InMemoryEventBus` كـ`IEventBus` المسجَّل — Transient لسه زي ما هو، لكن Reliable/Critical بقوا شغالين فعليًا (مش بيرموا `NotSupportedException` تاني) عبر outbox_events transactional
- [x] 33 اختبار جديد (66/66 على مستوى الحل كله)، Release build نظيف 0 warnings

**فجوات/قرارات وُلدت أثناء التنفيذ (اتسجلت كتعديلات في الوثيقة الأم):**
- إضافة عمود `outbox_events.delivery_level` (كان ناقص، لازم للتفرقة بين Reliable/Critical processing)
- إصلاح NULL-uniqueness gotcha لأسماء الفولدرات المتشابهة على مستوى الجذر (فهرس تعبير `COALESCE(parent_id, 0)` بدل `UNIQUE` الحرفي)
- FluentMigrator يرمي `MissingMigrationsException` لو DB معينة معندهاش أي Migration مطابق لـTag بتاعها (مش no-op زي ما كان متوقع) — `PersistenceInitializer` بقى بيشغّل الـMigrator بس للـDBs اللي فعلاً عندها Migrations (`archive.db` بس دلوقتي؛ `audit.db`/`search.db` هيتضافوا لما Phase 4/8 يضيفوا أول Migration بتاعتهم)
- مكتبة Argon2id تحتاج Persistence.Tests على `net10.0-windows` (DPAPI Windows-only بالفعل من Phase 1)
- TODO مسجَّل (Technical Debt Policy §142): `outbox_events.user_id` لسه مش متربط بـGuid uid الفعلي — محتاج `IAppUserRepository` من Phase 3

### Phase 3 — Authentication & Roles `[x]`
*مرجع: §34-35*
- [x] Login / Logout / Guest Mode — `IAuthenticationService`/`AuthenticationService` (Security project): Login بتشفير عام لرسالة الخطأ (نفس الرسالة لاسم مستخدم غير موجود أو كلمة مرور خاطئة — منع Credential Enumeration)، Lockout بعد 5 محاولات فاشلة (15 دقيقة، قابل للتعديل عبر `AuthenticationOptions`)، Session tokens عشوائية 256-bit مُخزَّنة كـSHA-256 hash فقط (مطابق DB Spec §21). Guest Mode = `ArchivePrincipal.Guest` بدون جلسة DB، يظهر فقط لو `app_settings["guest.enabled"] = true`. **Switch User** لم يُبنَ كـmethod مستقل عمدًا — هو Logout+Login مُركَّب من الـUI (نفس المنطق بالضبط، لا داعي لتكرار الكود).
- [x] Remember Me — جلسة أطول (30 يوم افتراضيًا) بدل القصيرة (8 ساعات)، عبر `rememberMe` flag في Login.
- [x] Admin/User/ReadOnly roles + Last Admin protection — `IUserManagementService`/`UserManagementService`: `CreateUserAsync`/`DeactivateUserAsync`/`ChangeRoleAsync`، كلها بترفض العملية لو هتسيب صفر Admin نشط (SAD §180، مُتحقَّق بـ4 اختبارات فعلية). `DefaultPermissionEvaluator` (Security/Permissions) — الـmatrix الفعلي المؤجل من Phase 1، مبني من SAD §13 (Admin=كل الصلاحيات، User=بدون Settings.Write، ReadOnly/Guest=قراءة فقط).
- [x] Password hashing عبر Argon2id (`Konscious.Security.Cryptography.Argon2`) — كان جاهز من Phase 1، اتربط فعليًا بمسار Login/CreateUser.
- [x] Login UI — أول مشروع WPF في الحل (`Darhous.Archive.Desktop`): شاشة Login حسب UI/UX §91 (Minimal، بدون صور خلفية)، Theme system كامل (Light/Dark tokens من UI/UX §22-27، Typography §29-31، RTL افتراضي)، `ThemeManager` بيقرأ إعداد Windows الفعلي (Registry) لما يكون Theme=System. **First-Run Bootstrap**: لو مفيش Admin نشط، بينشئ حساب "admin" بكلمة مرور عشوائية تُعرض مرة واحدة فقط (MessageBox) مع `must_change_password=true` — لأن `app_users` تبدأ فاضية بعد الـMigration ومفيش طريقة تانية لأول دخول.
- [x] **DB إضافات**: Migration جديدة `user_sessions` (DB Spec §21) + Migration Seed لـ4 الأدوار الأساسية (admin/user/readonly/guest — DB Spec §18) + `IAppUserRepository`/`AppUserRepository`، `ISessionRepository`/`SessionRepository` (نفس نمط Dual-mode بتاع RoleRepository من Phase 2) + `IUnitOfWorkContext.Users`/`.Sessions`.
- [x] 60 اختبار جديد (31 Security.Tests + تحديث/إضافة في Persistence.Tests)، **91 اختبار على مستوى الحل بالكامل**، Release build نظيف.
- **تحقق تشغيلي فعلي:** شُغِّل الـexe المبني فعليًا (مش Test فقط) — الـLog أظهر Startup ناجح، Migrations اشتغلت، First-Run Admin اتنشأ فعليًا بدون أي Exception. التحقق البصري الكامل (screenshot تفاعلي للشاشة) لم يُنفَّذ — يحتاج موافقة تفاعلية من Ahmed على الجهاز نفسه لصلاحية التحكم بالشاشة (computer-use)، وده مش مناسب لتدفق تنفيذ مستمر بدون تدخل. **موصى به:** Ahmed يشغّل `src/Darhous.Archive.Desktop/bin/Debug/net10.0-windows/Darhous.Archive.Desktop.exe` بنفسه للتأكد البصري من الشاشة عند أول فرصة.

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
2026-09-11 — Phase 0 اتدفعت على main (github.com/Darhous/archive) بعد تأكيد Ahmed. Ahmed أعطى تفويض Multi-Agent Orchestration (C:\AI-AgentFlow-Test — تم التحقق: claude/codex/agy CLIs شغّالين فعليًا) + أمر بالاستمرار المتواصل حتى Phase 22 بدون توقف للقرارات الروتينية. قواعد التشغيل اتسجلت في §0.1 من هذا الملف.
2026-09-11 — Phase 1 (Core Foundation) مكتملة: 5 مشاريع جديدة/ممتدة (Contracts, Core موسّع, Application, Configuration, Security) — Result/Error model, Correlation (AsyncLocal), IEventBus + InMemoryEventBus (Transient-only، Reliable/Critical يرفضان صراحة لحد الـOutbox)، IHealthContributor/HealthRegistry، IBackgroundJob/IJobContext، IArchiveModule/ModuleRegistry، UserRole/Permission/IPermissionEvaluator، Argon2idPasswordHasher فعلي، DpapiSecretProtector فعلي، Dispatcher (reflection-based command/query routing). 33/33 اختبار ناجح، 0 warnings (TreatWarningsAsErrors). جاري الـcommit+push، بعدها مباشرة Phase 2.
2026-09-11 — Phase 2 (Persistence Foundation) مكتملة: راجعتها الـ3 نماذج عبر AgentFlow (Level 4 — قرار: الإبقاء على تقسيم الـ3 قواعد بيانات، رفض توصية Gemini بدمجها في ملف واحد لأنه قرار معماري سابق ومعتمد في SAD/DB Spec). Darhous.Archive.Persistence مشروع جديد كامل: SqliteConnectionFactory (WAL+FK+busy_timeout)، MigrationRunnerFactory (Runner مستقل لكل DB، Tag-based)، أول Migration SQL خام (11 جدول، ON DELETE من §107.1، فهارس §99)، SqliteWriteQueue (Channel-based، اتصال كتابة واحد لكل DB)، SqliteUnitOfWork/IUnitOfWorkContext، RoleRepository (Dapper)، OutboxWriter/OutboxEventBus (استبدل InMemoryEventBus، Reliable/Critical بقوا شغّالين فعليًا)، SqliteDatabaseHealthContributor × 3. اكتُشف وأُصلح Bug حقيقي: FluentMigrator يرمي استثناء لو DB معندهاش Migrations مطابقة، فـaudit.db/search.db اتأجل تشغيل الـMigrator بتاعهم لحد Phase 4/8. عدّلت الوثيقة الأم (outbox_events.delivery_level، فهرس COALESCE لأسماء الفولدرات). 33 اختبار جديد، 66/66 على مستوى الحل، Release build نظيف. جاري commit+push، بعدها Phase 3.
2026-09-11 — Phase 3 (Authentication & Roles) مكتملة: Migrations جديدة (user_sessions، Seed لـ4 أدوار)، AppUserRepository/SessionRepository (نفس نمط Dual-mode)، AuthenticationService (Login بخطأ عام موحّد ضد Credential Enumeration، Lockout 5 محاولات/15 دقيقة، Session tokens SHA-256، Remember Me)، UserManagementService (Last Admin Protection مُتحقَّق بـ4 اختبارات)، DefaultPermissionEvaluator (الـmatrix الفعلي المؤجل من Phase 1). أول مشروع WPF في الحل: Darhous.Archive.Desktop — Login UI حسب UI/UX §91، Theme system (Light/Dark/RTL)، First-Run Bootstrap (Admin افتراضي بكلمة مرور عشوائية معروضة مرة واحدة). اكتُشفت مشكلة C# حقيقية: namespace التصادم بين "Darhous.Archive.Application" (مشروعنا) و"System.Windows.Application" (WPF) — الحل: fully-qualify صريح، مش global alias (الـalias مالوش أولوية على enclosing-namespace member lookup). 60 اختبار جديد، 91/91 على مستوى الحل. شُغِّل الـexe فعليًا (مش Tests بس) وأكَّد نجاح الـstartup من الـlogs. جاري commit+push، بعدها Phase 4.
```

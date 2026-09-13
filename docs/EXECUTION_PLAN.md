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

### 0.1.1 قاعدة اقتصاد الـContext في Multi-Agent Orchestration (2026-09-12)

أُضيفت بعد ملاحظة استهلاك Context غير ضروري (رد Codex في مراجعة Phase 2 كان فيه سطر واحد ~125K توكِن من tool logs داخلية). **إلزامية من الآن فصاعدًا لأي استدعاء لـ`Invoke-Orchestration.ps1`:**

1. **كل استدعاء Worker يجب أن يحمل `-ExpectedFormat` صريح** يفرض ردًا مختصرًا ومنظّمًا فقط: القرار/التوصية، أهم الأسباب، المشاكل/الـBugs المكتشفة، الملفات/الأسطر ذات الصلة، التغييرات المقترحة، المخاطر والـEdge Cases، نتيجة الاختبارات (لو موجودة). **لا** طلب لسرد كامل أو تحليل مفتوح النهاية بدون حد.
2. **الملفات الكبيرة (`responses/*.txt`) تُقرأ بحرص**: أول خطوة دائمًا `wc -c`/`wc -l` على الملف قبل أي `Read` كامل. لو فيه سطر ضخم بشكل غير طبيعي (زي حالة Codex) — استخدم `Grep`/`offset+limit` لاستخراج آخر رسالة نصية فعلية (`"type":"agent_message"`) بدل قراءة الملف كله.
3. **لا تفريغ Tool logs أو Repository scans أو محتوى ملفات كامل في الرد النهائي** — لو Worker احتاج يفحص كود، النتيجة النهائية المطلوبة منه هي الخلاصة، مش الـtranscript.
4. **تحليل تفصيلي/طويل يُحفظ في ملف منفصل** (مش يترجع في نص الرد) — اقرأ الملخص أولًا، افتح الملف الكامل بس لو فعلاً محتاج تفصيل إضافي لقرار حقيقي.
5. **لا إعادة قراءة الوثيقة الأم كاملة** إذا كان القسم المطلوب معروف بالفعل أو متاح في هذا الملف — استخدم `Grep`/`offset+limit` للأقسام المحددة فقط، لا `Read` بدون حدود على ملف 15,400+ سطر.
6. **Build/Test output**: لخّص لنتيجة نهائية (Passed/Failed count) + أي Errors/Warnings فعلية فقط، مش الـOutput الكامل، إلا لو التشخيص يحتاج التفاصيل الكاملة لخطأ معين.
7. **القرارات المهمة تتسجل هنا (EXECUTION_PLAN.md) وفي `.agentflow/task-state.json`**، مش تتراكم في سياق المحادثة فقط.

الهدف: الاستفادة الكاملة من قوة الـ3 نماذج مع أقل استهلاك ممكن لـContext الـMaster (أنا).

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

- **المرحلة الحالية:** Phase 16 (Notifications) **مكتملة، Committed، Tagged (`phase-16`)، ومدفوعة فعليًا** — كل مراحل 0-17 دلوقتي مدموجة في `main` (Phase 16 كانت آخر واحدة اتدمجت رغم إنها اتعملت زمنيًا قبل Phase 17). Phase 18 (Backup & Restore) شغّالة دلوقتي بالتوازي مع كودكس في Worktree مستقل.
  - **قرار تنظيمي مهم اتخده Ahmed (2026-09-13)**: جيميني هيوقف تنفيذ مراحل مستقلة (Phase 13/14/16 كل واحدة احتاجت جولة تصحيح حقيقية بعد تقرير ذاتي متفائل زيادة عن اللزوم، بعكس Codex اللي تقاريره طابقت المراجعة المستقلة 100% في Phase 15/17). **دور جيميني الجديد: فحص شامل (Audit/QA) للمشروع كله** — قراءة الكود والاختبارات، تشغيل Build/Test، وكتابة تقرير مفصّل واحد بدل ما Claude يستهلك توكينز في مراجعة كل خطأ بنفسه. Worktree مستقل اتعمل (`C:\Users\ahmed\Desktop\Darhous-Archive-audit-gemini`، Detached عند `main`) وبريف (`GEMINI_FULL_AUDIT_BRIEF.md`) اتبعت. النتيجة (`GEMINI_AUDIT_REPORT.md`) لسه متوقّعة.
  - **Phase 18 (Backup & Restore)** — كودكس شغّال في `C:\Users\ahmed\Desktop\Darhous-Archive-phase18-codex` (Branch: `phase-18-codex`، مبني على `main` بعد Phase 17). البريف (`PHASE18_BRIEF.md`) يغطي: `Darhous.Backup.Local` Plugin، SQLite Online Backup API، Backup Flow (Validate→Backup→Copy managed files لو Full→Checksums→Package→Verify)، Restore Flow (Validate→Safety backup→Stop writes→Restore→Migrate→Rebuild search→Verify)، جدول `backup_history` (DB Spec §76) + أنواع Backup (metadata/full/configuration، DB Spec §77). **لسه قيد التنفيذ، مفيش نتيجة بعد.**
  - **إجراء الدمج القياسي لأي مرحلة جاهزة** (اتبع بالظبط، زي Phase 13-17): اقرا `WORKER_REPORT.md` + راجع الكود الفعلي (مش تصدّق التقرير على العمى — [[feedback_orchestration_worktrees]])، شغّل build/test مستقل للتأكيد **لكل مشروع اختبار لوحده** (تشغيل الحل كله دفعة واحدة أثبت إنه ممكن يعلّق تحت حمل بيئي — راجع "درس Solution-wide test hang" تحت)، **بعد الدمج ابنِ الحل كله (مش بس المشاريع الجديدة) — تعديل توقيع Constructor في خدمة أساسية زي `JobRunner` ممكن يكسر مشروع اختبار قديم تمامًا في مرحلة تانية بدون أي علاقة، ده حصل فعليًا في دمج Phase 16 ولم يظهر إلا ببناء الحل بالكامل**، تأكد مفيش تصادم أرقام Migration مع مراحل تانية قيد التنفيذ بالتوازي أو مدموجة حديثًا، ادمج بـ`git checkout <branch> -- <paths>` في `main` (استبعد أي ملفات تجريبية/غير مقصودة من الـWorktree)، حدّث `Darhous.Archive.slnx` + قسم الـPhase هنا + سجل التقدم (§9) + هذا القسم (§1)، Commit+Tag(`phase-N`)+Push، اتأكد CI Success فعليًا بـ`gh run list` (مش مجرد الافتراض)، ثم احذف الـWorktree المدموج (`git worktree remove --force` — لاحظ إنه فشل 3 مرات بـ"Permission Denied" لـPhase 14/15/17 المدموجين، على الأغلب بسبب Terminal/Process لسه فاتح جواه؛ إعادة المحاولة لاحقًا مش عاجلة، بيضيع مساحة قرص بس).
- **Repository:** https://github.com/Darhous/archive — Phase 0-17 مدفوعين بالكامل على `main`. Tags: `phase-0` لغاية `phase-17` كلهم موجودين ومدفوعين. **CI: اتأكد من النتيجة بـ`gh run list` أول ما تُستأنف الجلسة (كان شغّال وقت آخر Push).**
- **آخر مرحلة مكتملة (مدفوعة):** Phase 16 — Notifications (آخر واحدة اندمجت زمنيًا، رغم إنها اتعملت قبل Phase 17).
- **درس جديد (2026-09-13): تشغيل اختبارات الحل كله دفعة واحدة (`dotnet test Darhous.Archive.slnx`) ممكن يعلّق فعليًا (مش بس يبطّئ) تحت حمل بيئي عالي** (تحقق Phase 16 كان شغّال بالتوازي على نفس الجهاز) — العملية فضلت شغّالة ~40 دقيقة باستهلاك CPU شبه صفري (0.1 ثانية CPU خلال 10 ثواني مراقبة)، اتقفلت يدويًا (`Stop-Process -Force`) واتأكد بديل ناجح: تشغيل كل مشروع اختبار لوحده. **القاعدة الجديدة**: لو تشغيل الحل كله دفعة واحدة أخد أكتر من ~10 دقائق من غير نتيجة، افترض إنه معلّق (تأكد بفحص استهلاك CPU الفعلي مش بس إنه "شغّال")، اقفله، وشغّل كل مشروع اختبار لوحده بدل ما تستنى.
- **درس دمج حرج إضافي (2026-09-13، Phase 16)**: تعديل Constructor لخدمة أساسية (`JobRunner`) كسر مشروع اختبار في مرحلة قديمة تمامًا (`DiscoveryTestBase.cs`, Phase 9) بيبنيه مباشرة بدون DI — الـWorker المسؤول عن Phase 16 اختبر مشاريعه هو بس (صح تمامًا من منظوره)، لكن محدش شاف الأثر على باقي الحل غير Claude وقت بناء الحل كله بعد الدمج. **القاعدة**: أي تعديل على Constructor خدمة مشتركة قديمة، دور الـIntegrator (Claude) يشمل `grep` شامل لكل استخدام مباشر ليها في كل مشاريع الاختبار، مش الاعتماد على بناء المشاريع الجديدة بس.
- **درس CI موثّق (2026-09-12):** اكتشفنا (بمراجعة GitHub Actions مش بمتابعة استباقية) إن `WorkerProcessSupervisorTests.cs` (Phase 13) و`ScannerPluginTests.cs` (Phase 14) بيحطّوا مسار الـFixture executable بشكل مُثبَّت على `"Debug"` بدل ما ياخدوه ديناميكيًا من الـConfiguration الفعلي — دي شغالة محليًا لأني كنت بختبر بـDebug (الافتراضي) مش Release، لكن CI بيبني ويختبر بـ`--configuration Release` دايمًا (`.github/workflows/ci.yml`)، فالـFixture مكنش موجود في المسار المتوقع (`bin/Debug/...` بدل `bin/Release/...`) وكل الاختبارات اللي بتعتمد عليه كانت بتفشل بـ`FileNotFoundException`/فشل صامت في بدء الـProcess. **الإصلاح**: استخراج اسم الـConfiguration ديناميكيًا من `AppContext.BaseDirectory` نفسه بدل التثبيت اليدوي (Commit `e3d6fa5`، اتأكد إنه أصلح CI فعليًا). **القاعدة الجديدة الدائمة**: أي Phase بعد كده لازم يتأكد فيها Release build+test محليًا (زي الروتين المتبع من Phase 0-12) **وكمان** يتفحص حالة CI فعليًا بعد كل Push (`gh run list`) — مش نفترض إنه شغّال لمجرد إنه "شغّال" في مرحلة سابقة.
- **درس Multi-Agent إضافي موثّق (2026-09-12، Phase 16):** التقرير الذاتي وحده مش كافي حتى لو "قاسي"، ولازم تحقق فعلي بالـBuild لأي مشروع جديد قبل قبول أي رقم Confidence — جيميني كرر نفس نمط "الثقة الزايدة عن الدليل" اللي حصل في Phase 14 (هناك: عقد اتصال كسور مع Confidence 8/10؛ هنا: مشروع مايتبنيش خالص مع نفس الـConfidence 8/10 ومن غير أي رقم Test حقيقي). القاعدة: **لازم Build مستقل لكل مشروع جديد قبل أي قرار دمج، مش بس قراءة التقرير.**
- **عوائق مفتوحة:** لا يوجد. **ديون تقنية متبقية** (§142): `outbox_events.user_id` و`audit_events.user_id` لسه NULL دايمًا (TODO موثّق في الكود لكل واحد) — هيتحلوا لما نبني lookup فعلي بين Guid uid والـinternal id، مش عاجل. **ملاحظة قديمة:** حساب Admin افتراضي اتنشأ على %ProgramData% الجهاز الحقيقي وقت اختبار Phase 3 — كلمة المرور اتعرضت مرة واحدة واتقفلت قبل الالتقاط؛ امسح `%ProgramData%\DarhousSmartArchive` لو عايز تبدأ من الصفر. **ملاحظة بيئة:** القرص C: كان وصل لصفر مساحة فعليًا أثناء Phase 13 (اتحل، لكن لسه هامشه ضيق ~5-8GB عادةً) — راجع مساحة القرص لو أي Build/Test بدأ يبطّئ بشكل غير طبيعي، ولو لقيت عمليات `find.exe`/غيرها معلّقة بتستهلك CPU لساعات، اقفلها فورًا (حصل قبل كده مرتين تقريبًا).
- **اختبارات Flaky بيئيًا مقبولة (مش Regression، اتأكدت بإعادة تشغيل منفصل):**
  1. `Darhous.Archive.Desktop.Tests.Explorer.ExplorerViewModelTests.LiveFilter_With100000Documents_CompletesQuickly` — Gate أداء 100k صف، حساس لحمل الجهاز وقت التشغيل المتوازي (موثّق من Phase 7).
  2. `Darhous.Archive.Workers.Host.Tests.WorkerProcessSupervisorTests` (`RepeatedCrashes_DelaysCorrectlyRequested_ThenFails`, `ThreeCrashesWithinWindow_Quarantined`) — بيشغّلوا Process حقيقي (`Darhous.TestWorkerProcess`) وبيستنوا انتقال حالة خلال نافذة زمنية قصيرة؛ فشلوا فقط لما اتشغّل الحل بالكامل مع بعض (2026-09-12، بعد دمج Phase 13)، ونجحوا 4/4 لما اتشغّلوا لوحدهم (اتأكد مرتين). نفس فئة الحساسية البيئية بالظبط زي رقم 1.
  3. `Darhous.Archive.Modules.Scanner.Tests.ScannerPluginTests.FullPluginLifecycle_Tests` و`Darhous.Archive.Modules.Discovery.Tests.FileReadinessCheckerTests.WaitUntilReadyAsync_LockReleasedBeforeTimeout_BecomesReady` — نفس الفئة، ظهروا بس تحت حمل استثنائي (تشغيل الحل بالكامل Release + Codex وGemini شغالين بالتوازي في نفس اللحظة على نفس الجهاز)، ونجحوا 100% لما اتشغّلوا لوحدهم (2026-09-12).

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

### Phase 4 — Audit `[x]`
*مرجع: §36*
- [x] audit.db + Audit service — مشروع جديد `Darhous.Archive.Audit` (منفصل عن Persistence حسب هيكل الحل §5)، Migration جديدة لـ`audit_events` (DB Spec §82، تاجت "Audit")، مُضاف لـ`PersistenceInitializer.DatabasesWithMigrations`.
- [x] Critical immediate writes + Buffered normal writes — `BufferedAuditService` (BackgroundService): Critical actions (DB Spec §106 — login/failed_login/delete/permanent_delete/restore/settings_change/plugin_install/plugin_remove/backup/app_update) تُكتب فورًا ومتزامنة عبر الـWrite Queue؛ الباقي بيتجمّع في Channel ويتفرّغ كل 2 ثانية أو عند 200 عنصر.
- [x] تسجيل من أول نسخة: **login/logout فعليًا مربوطين** (`AuthenticationService` بقى بياخد `IAuditService` ويسجل Login (نجاح/فشل بنفس رسالة الخطأ) وLogout). **search/sort/filter/add/move/delete لسه مش مربوطين** — البنية التحتية جاهزة بالكامل (`IAuditService`/`AuditEntry`/`AuditAction` constants) لكن المزايا نفسها (Search=Phase 8، Documents=Phase 5، Folders=Phase 6) لسه مبنيتش، فمفيش حاجة تُسجَّل عنها دلوقتي — هيتربطوا تلقائيًا مع كل Phase بتبنيهم.
- [x] Audit viewer query — `IAuditQueryService.QueryAsync` (فلترة بالـAction/User/التاريخ + Pagination، ترتيب الأحدث أولًا).
- [x] Export hooks — **تفسير**: `IAuditQueryService` نفسه هو الـHook (بيرجّع بيانات منظّمة أي طبقة Export مستقبلية تقدر تستهلكها مباشرة)، مفيش طبقة Export فعلية لسه (دي فعليًا Phase 17 — Reports & Export) ولا داعي تتكرر هنا.
- [x] **قاعدة تصميم مهمة اتطبّقت**: الـAudit write بيحصل **بعد** الـ`IUnitOfWork.ExecuteAsync` مش جواه، لأن audit.db وarchive.db قاعدتين منفصلتين فعليًا (مفيش Transaction واحدة عبر ملفين SQLite — نفس القيد اللي ظهر في مراجعة Phase 2).
- [x] 5 اختبار جديد (`Darhous.Archive.Audit.Tests`)، **97/97 اختبار على مستوى الحل بالكامل**.
- **دين تقني موروث**: `audit_events.user_id` لسه NULL دايمًا (نفس مشكلة `outbox_events.user_id` من Phase 2/3 — الهوية الكاملة محفوظة عبر `username_snapshot`/`role_snapshot` بدل الـFK، وده أصلاً التصميم الموثّق في DB Spec).

### Phase 5 — Document Core `[x]`
*مرجع: §37-38 — قرار التصميم راجعه Codex عبر AgentFlow (Level 2)، تفاصيل في `.agentflow/task-state.json` (TaskId `20260912-022110-956990ba`)*
- [x] Document entity, Version entity, Archive number — `Darhous.Archive.Application/Persistence/Document.cs`+`DocumentVersion.cs`، `ArchiveNumberGenerator` (transactional عبر `number_sequences`، صيغة `ARC-{YYYY}-{000001}`، يعيد الترقيم من 1 عند تغيّر السنة)
- [x] Managed storage + Indexed-in-place — `IFileStorageService`/`FileStorageService` (مشروع جديد `Darhous.Archive.Modules.Documents`): تخطيط فيزيائي `ArchiveStorage/Documents/{year}/{month}/{DocumentUID}/v{N}/` (§49)، الاسم الفيزيائي `<DocumentUID>_<VersionNo>.ext` (§151 — العنوان لا يظهر أبدًا في اسم الملف)
- [x] Hash (SHA-256) + Duplicate detection — `ComputeSha256Async` + `FindBySha256Async`، الافتراضي رفض التكرار مع رسالة توضح رقم الأرشيف الموجود (SAD §32 "Detect → Warn → User chooses")، مع `allowDuplicate: true` للسماح صراحة
- [x] Recycle Bin + Restore + Version history — `recycle_bin_entries` (Migration جديدة)، `TrashDocumentAsync`/`RestoreDocumentAsync`، `ListForDocumentAsync` لتاريخ الإصدارات
- [x] `IFileStorageService`: Stage/Commit/Open/MoveToRecycleStaging/Restore/DeletePermanently/Hash — **قرار تصميمي مهم (من مراجعة Codex)**: نقل الملف لمكانه النهائي يحصل **قبل** commit الـDB transaction، مش بعده — لو الـDB فشلت بعد نقل الملف، النتيجة ملف يتيم (orphan) قابل للتنظيف لاحقًا، مش سجل DB يشاور على ملف مش موجود (أسوأ حالة فشل). **تبسيط متعمد عن اقتراح Codex الكامل**: رفضنا بناء "Import Operation Journal" table كاملة (over-engineering لتطبيق Desktop مستخدم واحد بـWrite Queue واحد مسلسل بالفعل) — ملف يتيم قابل للـSelf-healing لاحقًا كافي لـV1.
- [x] Permanent Delete (Admin-only) — `PermanentDeleteDocumentAsync` بيرفض لو الدور مش Admin، يطبّق DB Spec §109 (نقل الملفات لـDeletion Staging قبل حذف الـMetadata، مش بعده)
- [x] **Migrations إضافية**: `file_path_normalized` column (§148، فهرس UNIQUE جزئي يتجاهل القيم الفارغة)، `number_sequences` table (§28)، `recycle_bin_entries` table (§46)
- [x] **`ArabicNormalization`** (Core/Text) — نُقل من Phase 8 المخطَّط لأن `documents.title_normalized` عمود NOT NULL محتاج قيمة من أول Phase 5؛ نفس الدالة هتُستخدم في Phase 8 (FTS) بدل التكرار
- [x] **ربطنا دين Phase 4 التقني**: add_document/move_document/delete_document/restore_document/permanent_delete كلهم بقوا مسجَّلين فعليًا في الـAudit
- [x] 16 اختبار جديد (`Darhous.Archive.Modules.Documents.Tests`)، **113/113 اختبار على مستوى الحل بالكامل**
- **دين تقني موروث**: `documents.created_by`/`updated_by` بيتسجلوا NULL دلوقتي لو الـcaller ما بعتش Guid فعلي (مفيش UI لسه بيبعت المستخدم الحالي — هيتحل مع Explorer UI في Phase 7)
- **DoD (§132):** ✅ Add/version/hash/duplicate warning/move/trash/restore/permanent delete (Admin-only)/audit/indexed-in-place/managed storage — كل بند اتغطى باختبار فعلي.

### Phase 6 — Folder System `[x]`
*مرجع: §39*
- [x] Logical folders + Tree + Subfolders — مشروع جديد `Darhous.Archive.Modules.Folders` (`IFolderService`/`FolderService`)، `IFolderRepository`/`FolderRepository` (Dapper، نفس نمط Dual-mode). Tree عبر `GetChildrenAsync(parentId)` تصاعديًا
- [x] Move / Rename / Delete rules / Unclassified — منع تكرار الاسم بين الإخوة (فحص مسبق + الفهرس الجزئي `ux_folders_sibling_name` من Phase 2 كخط دفاع ثانٍ)، **منع الدورات (Cycle)** عند Move (تتبع سلسلة الآباء من الهدف المقترح لحد ما توصل للجذر أو تلاقي الفولدر نفسه)، Delete افتراضيًا يرفض لو الفولدر مش فاضي (DB Spec §137)، مع خيار `moveContentsToUnclassified` صريح لنقل الفولدرات الفرعية للجذر والمستندات لـUnclassified (`folder_id = NULL`) قبل الحذف
- [x] Bulk move + Undo snapshot — Migration جديدة `operation_snapshots` (DB Spec §142)، `IBulkOperationService`/`BulkOperationService` (في `Darhous.Archive.Modules.Documents` لأنه فعليًا عملية على المستندات): `BulkMoveDocumentsAsync` يسجل الفولدر الأصلي لكل مستند في الـSnapshot قبل النقل، `UndoAsync` بيرجّع الكل خلال 30 ثانية بس (نفس القيمة الموثقة)، بعدها `UNDO_WINDOW_EXPIRED`
- [x] **إصلاح Bug أداء حقيقي اكتشفناه ذاتيًا (مش من مراجعة خارجية)**: أول تنفيذ لـ"نقل مستندات الفولدر المحذوف لـUnclassified" استخدم `context.Documents.ListAsync()` (كل الجدول!) بدل استعلام مفلتر — كان هيبقى كارثة أداء على مليون مستند. اتصلح بإضافة `IDocumentRepository.ListByFolderAsync(folderId)` يستخدم فهرس `folder_id` الموجود من Phase 2 (§99)
- [x] 18 اختبار جديد (`Darhous.Archive.Modules.Folders.Tests`)، **131/131 اختبار على مستوى الحل بالكامل**
- **ملاحظة نطاق**: لم نبني "Tags Bulk Update" رغم استخدامه نفس آلية `operation_snapshots` — Tags نفسها (§21 SAD) لسه مبنيتش كـModule (مفيش Phase مخصصة لها في الخطة الحالية؛ الجدول `tags`/`document_tags` موجود في DB من Phase 2 لكن بدون Service layer). هنبنيها لما تيجي فعليًا محتاجة (زي ما عملنا مع Search/Documents/Folders كل واحدة في وقتها).

### Phase 7 — Archive Explorer UI `[x]`
*مرجع: §40-41*
- [x] Sidebar (TreeView) + Folder tree كامل (بناء تكراري من `IFolderService.GetChildrenAsync` — Root ثم كل مستوى تحته)، مع Node ثابت "كل الأرشيف" (فوق) و"غير مصنف" (تحت، `folder_id = NULL`)
- [x] Search strip — Live filter من نوع Client-side substring على النطاق المحمَّل حاليًا فقط (مش FTS حقيقي بعد — ده Phase 8)، مبني على `ArabicNormalization.Normalize` (نفس الدالة من Phase 5) على النص المكتوب والعنوان معًا، فبيتجاهل تلقائيًا اختلاف الهمزات (أ/إ/آ/ٱ→ا) والتشكيل
- [x] Document list — `ListView` + `GridView` (رقم الأرشيف/العنوان/الحالة/تاريخ الأرشفة)، مع `VirtualizingPanel.IsVirtualizing/VirtualizationMode=Recycling/ScrollUnit=Item` لدعم الـGate
- [x] Preview placeholder (يمين، Toggle قابل للإخفاء) — المعاينة الفعلية Phase 11
- [x] Breadcrumb — بيبني المسار الكامل من فهرس مسارات مبني مرة واحدة عند تحميل شجرة الفولدرات
- [x] Bulk actions — Bar يظهر بس لما فيه تحديد (`HasSelection`)، فيه: نقل جماعي (`IBulkOperationService.BulkMoveDocumentsAsync` مع اختيار فولدر هدف) وحذف جماعي (نقل لسلة المحذوفات لكل عنصر محدد)
- [x] Density modes (`Compact`/`Standard`/`Comfortable`) — بيتحكم في الـPadding بتاع كل صف عبر `RowPadding` المرتبط بـ`Density`
- [x] Theme — استخدام `ThemeManager` الموجود من Phase 3 (Light/Dark/RTL) بدون تعديل جوهري
- [x] استبدال `WelcomePlaceholderWindow` (Placeholder من Phase 3 بعد تسجيل الدخول) بـ`ExplorerWindow` الحقيقية — الملفين القدام اتحذفوا بالكامل
- [x] **إصلاح Bug ذاتي (اختبارات فقط، مش الكود الحقيقي)**: `Dictionary<Guid?, T>` بيدّي تحذير CS8714 بس فعليًا بيرمي `ArgumentNullException` وقت التشغيل لو استخدمت `null` كـkey فعلي — مش False Positive زي ما افترضنا الأول. الإصلاح كان في الـTest Fake فقط (`FakeFolderService`) باستخدام `Guid.Empty` كـsentinel لـ"root" بدل `null`؛ الكود الحقيقي (`FolderRepository`) مش متأثر لأنه بيستخدم SQL `WHERE parent_id IS NULL` مباشرة، مفيش Dictionary فيه أصلًا
- [x] 12 اختبار جديد (`Darhous.Archive.Desktop.Tests`) — بناء الشجرة (مسطحة ومتداخلة)، تحميل "كل الأرشيف" مقابل فولدر محدد، الفلترة الحية (substring + تطبيع عربي)، مسح البحث بيرجّع القائمة الكاملة، الحذف الجماعي بيمسح المحدد بس، النقل الجماعي بيبعت الـUIDs والهدف الصح، Density→RowPadding (Theory على القيم الـ3)
- **Gate إلزامي قبل الاستمرار — نصفين:**
  - ✅ **نصف الـData layer (مؤتمت بالكامل):** اختبار `LiveFilter_With100000Documents_CompletesQuickly` — تحميل 100,000 مستند وهمي + تطبيق فلتر حي في أقل من 2000ms (ناجح فعليًا، الـpass الواحد O(n) بدون أي رحلة DB لكل عنصر). هذا يثبت إن طبقة البيانات نفسها (مش الـRendering) سريعة بما يكفي.
  - ⚠️ **نصف الـRendering/Visual (يحتاج تأكيد Ahmed يدويًا):** الاستجابة البصرية الفعلية أثناء الـScroll مع 100k صف حقيقي على الشاشة — الـ`VirtualizingPanel` مضبوط صح في XAML لكن مفيش جلسة عرض تفاعلية (Interactive display) متاحة في بيئة التنفيذ ده لأختبرها بصريًا. **نفس الملاحظة المتكررة من Phase 3 (Login UI)** — يحتاج Ahmed يجرب الـexe فعليًا على جهازه ويأكد الانطباع البصري.
- **دين تقني اتحل من Phase 6**: `documents.created_by`/`updated_by` كانت بتتسجل NULL دايمًا — دلوقتي Explorer بيبعت `_principal.UserId` الحقيقي مع كل عملية حذف/نقل.

### Phase 8 — Search Foundation `[x]` ⭐ (نهاية أول Milestone استخدام يومي)
*مرجع: §42-44، DB Spec §86-97/§157/§182*
- [x] Official Plugin: `Darhous.Search.SqliteFts` (تحت `src/OfficialPlugins/` حسب Implementation Plan §5 — أول مشروع هناك؛ يسجل نفسه في DI زي أي Module عادي لحد ما Plugin Host الحقيقي يُبنى في Phase 12)
- [x] Migration جديدة لـsearch.db (`M202609120001_InitialSearchSchema`, Tag "Search"): `documents_fts` (FTS5, `tokenize='unicode61'`, `prefix='2 3 4'`)، `search_documents` (Shadow metadata: archive_number/title_display/folder_id/archive_date/file_type/status)، `search_document_tags` (جاهز للمستقبل، لا Tags module لسه)، `search_state` (checkpoint: document_uid/source_updated_at/normalizer_version). `documents_fts.rowid` = `search_documents.document_id` عمدًا (نفس القيمة) — نمط FTS5 "external content" القياسي، بيلغي الحاجة لـJoin عند الـUpdate/Delete
- [x] Arabic normalization — إعادة استخدام `ArabicNormalization` الموجودة من Phase 5 بالكامل (زي ما كان مخطَّط)، على النص المفهرس والاستعلام معًا
- [x] **قرار معماري مهم (راجعناه مع AgentFlow Level 2 قبل التنفيذ، ثم بسّطناه أكتر بقرارنا الخاص):** آلية الفهرسة التزايدية (Incremental Indexing) = **Reconciliation Sweep دوري فقط** (`SearchReconciliationService`, كل 5 ثواني افتراضيًا) بيقارن `documents.updated_at` بـcheckpoint محفوظ في `search_state` — مش Event Bus. الـAgentFlow review أوصى بـTransient Event Bus + Reconciliation كشبكة أمان معًا؛ قررنا نستغني عن جزء الـEvent Bus بالكامل ونعتمد على الـSweep وحده: كل تغيير في مستند (إضافة/نقل/حذف مؤقت/استرجاع/Bulk Move/Folder-cascade move) بيحدّث `updated_at` أصلًا (فهرس `ix_documents_updated_at` من Phase 2 مبني جاهز بالظبط لكده)، فالـSweep بيلتقط كل حالة تلقائيًا من غير ما نلمس DocumentService/FolderService/BulkOperationService خالص — أبسط، أقل عرضة للأخطاء من نسيان `PublishAsync` في مكان ما، وSAD §128 (Eventual Consistency) بيسمح صراحة بفجوة زمنية قصيرة زي دي. الاستثناء الوحيد: **Permanent Delete** بيمسح الصف نهائيًا فمفيش `updated_at` نلاحظه — لده Full Sweep دوري إضافي (كل 12 Tick ≈ 60 ثانية) بيقارن كل الـUIDs النشطة في archive.db ضد `search_state` ويمسح الـOrphans
- [x] `ISearchIndexWriter`/`SearchIndexWriter` — Upsert/Remove عبر الـWrite Queue الخاص بـ`DatabaseKind.Search` (موجود جاهز أصلًا من `AddPersistence` العام لكل قاعدة بيانات)
- [x] `IFtsQueryService`/`FtsQueryService` — بناء استعلام FTS5 مع كل Term كـ`"term"*` (Quoted Prefix، بيلغي أي حرف خاص في FTS5 syntax وبيفعّل الـPrefix Search)، `bm25(documents_fts, 10.0, 8.0, 5.0, 1.0)` (title/file_name/metadata/body — **ترتيب الأوزان لازم يطابق ترتيب الأعمدة المفهرسة بالظبط**، UNINDEXED معدومة من العد)، `snippet(documents_fts, -1, ...)` (الـ`-1` يخلي FTS5 يختار العمود الأنسب تلقائيًا)، Filters (folder/date range/file type/status)، Pagination، Sort (Relevance/Date/Title)
- [x] Search Audit — `AuditAction.Search`/`AuditActionCategory.Search` (كانا موجودين جاهزين من Phase 4!) بيتسجلوا بعد كل استعلام ناجح فقط (مش لكل Keystroke — الـCaller المفروض يعمل Debounce 250ms قبل ما ينادي `SearchAsync` أصلًا، فده طبيعي مش لازم منطق إضافي هنا)
- [x] `ISearchIndexRebuilder`/`SearchIndexRebuilder` — **قرار مختلف عن اللي راجعناه مع AgentFlow**: الـreview اقترح "ابنِ search.db.tmp ثم بدّل الملف (`File.Replace`)" لضمان استمرار القراءة أثناء الـRebuild. رفضنا الأسلوب ده لأن معمارية `SqliteWriteQueue` عندنا فيها اتصال كتابة واحد دائم طول عمر التطبيق أصلًا — تبديل ملف حي على Windows فيه مخاطر حقيقية (Handle مفتوح، بقايا WAL/-shm، Antivirus lock) موثقة في رد الـreview نفسه. بدلها: **Rebuild منطقي** — `DROP`+`CREATE` لنفس الجداول عبر نفس اتصال الكتابة الموجود (Transaction واحدة، من غير قفل/فتح ملفات)، وبعدها استدعاء `SearchReconciliationService.RunFullSweepAsync` (بما إن `search_state` بقى فاضي، كل مستند نشط هيتحسب "ناقص من الفهرس" ويتفهرس تلقائيًا — إعادة استخدام نفس المنطق المُختبر بدل كود منفصل). التكلفة: نافذة قصيرة (Query بترجع نتايج فاضية) أثناء الـRebuild — مقبولة لتطبيق مستخدم واحد بيستدعي العملية دي نادرًا وبوعي
- [x] `ISearchAvailability`/`SearchAvailability` — Corruption Guard: أي `SqliteException` بكود `SQLITE_CORRUPT`(11)/`SQLITE_NOTADB`(26) بتوقف الفهرس (`IsAvailable=false`) بدل ما تتسرب لـDocumentService/Explorer (DB Spec §97: "البرنامج يظل يعمل، Browsing يعمل")؛ `SearchAsync` وقتها بترجع `SearchResultPage { IsUnavailable = true }` بدل Exception؛ Rebuild بيرجّع `IsAvailable=true`
- [x] **فجوة نطاق موثّقة عن قصد (زي نمط Tags من Phase 6)**: أعمدة `metadata`/`body` في `documents_fts` موجودة وجاهزة في الـSchema لكن فاضية فعليًا الآن — مفيش Custom Fields module ولا OCR/Text Extraction pipeline لسه (Phases لاحقة) عشان يغذوها. الفهرسة الحالية بتغطي `title`+`file_name` بس فعليًا. هيتحلوا تلقائيًا لما الـPipelines دي تتبني — الـSchema والـRanking Weights (`metadata=5.0`, `body=1.0`) جاهزين من دلوقتي
- [x] **قرار Scope عن UI**: بنينا الـEngine كامل ومختبر ومسجَّل في DI (`AddSearchPlugin()` في `App.xaml.cs` — الـ`SearchReconciliationService` شغّالة فعليًا كـBackground Service من أول تشغيل)، لكن **مربطناهوش في مربع البحث الموجود في Explorer (Phase 7)** — جرّبنا نربطه (Debounce حقيقي 250ms + استبدال الفلترة الفورية لنطاق "كل الأرشيف") لكن اكتشفنا إن ده بيكسر افتراض التزامن (Synchronous) اللي اختبارات Phase 7 الـ12 مبنية عليه (تعيين `SearchText` والتأكد فورًا من `Documents` — مفيش وقت لـTask.Delay(250ms) ينفّذ). قررنا نرجع الربط ده ونأجله لحد ما نبني شاشة بحث مخصصة (مع Snippets، عداد نتائج، حالة "الفهرس غير متاح")، بدل ما نلزّق سلوك Async على مربع فلترة Sync شغّال ومُختبر كويس. الـEngine نفسه جاهز 100% للاستخدام في أي وقت لاحق
- **DoD (§134):** ✅ Arabic normalization, ✅ title/filename search (✅ Schema جاهز لـmetadata/body، البيانات لسه مفيش مصدر لها), ✅ snippets, ✅ filters, ✅ sort, ✅ folder/all-archive scope, ✅ FTS rebuild, ✅ corruption recovery
- **اختبارات جديدة**: مشروع `Darhous.Search.SqliteFts.Tests` (17 اختبار) — فهرسة تزايدية (إضافة/حذف مؤقت/نقل فولدر)، Full Sweep (Permanent Delete Orphan cleanup، مستند فاته الـSweep التزايدي)، بحث (تطبيع عربي/Prefix matching/BM25 ranking فعلي بين مستندين/Snippet/فلترة بنوع الملف/Pagination/نطاق كل الأرشيف)، Rebuild (إعادة فهرسة كاملة، استبعاد المهملات، استرجاع الـAvailability)، Corruption guard. **160/160 اختبار على مستوى الحل بالكامل** (Release build نظيف، 0 warnings/errors)

> **✅ عند اكتمال Phase 8: أول نسخة داخلية قابلة للاستخدام اليومي فعليًا (§141.1) — محرك البحث الحقيقي جاهز ومُختبر، وربطه بواجهة بحث مخصصة يبقى تفصيلة UI مؤجلة، مش نقص في الـFoundation نفسه.**

### Phase 9 — Automatic Computer Discovery `[x]`
*مرجع: §45-53*
- [x] Module: `Darhous.Archive.Modules.Discovery`
- [x] **Onboarding أولًا (SAD §46.9)**: `OnboardingWindow`/`OnboardingViewModel` — تُعرض مرة واحدة بعد أول Login ناجح (تتبع عبر `IAppSettingsStore["onboarding_completed"]`، مش عبر فحص watch_folders فاضية كل مرة، عشان "تخطي" ما يرجعش يفتحها تاني). المستخدم يختار مجلد أو أكثر (`OpenFolderDialog` من WPF .NET 8+) أو يضغط "فحص الكمبيوتر بالكامل (اختياري)" (تأكيد صريح بـMessageBox قبل التنفيذ) أو "تخطي الآن"
- [x] Discovery scope: **Job System الحقيقي بُني الآن لأول مرة** — الـinterfaces (`IBackgroundJob`/`IJobContext`) كانت موجودة من Phase 1 وجدول `jobs` من Phase 2، لكن مفيش Scheduler/Repository شغّال فعليًا كان موجود قبل كده. بنينا: `JobRepository` (dual-mode)، `JobRunner` (BackgroundService — Job واحد في المرة الواحدة عمدًا، V1 simplification)، Retry بـExponential backoff، **Crash Recovery** (SAD §53: أي Job فضل `running` وقت انطفاء التطبيق يتفحص عبر `IBackgroundJob.IsSafeToResumeAfterCrash` — لو Safe يرجع `pending`، لو لأ يبقى `needs_review`)
- [x] Local fixed drives (`auto_discover=true` افتراضيًا) + Removable/Network drives (تُسجَّل في `source_drives` بس `auto_discover=false` لحد ما المستخدم يوافق صراحة — **الموافقة الفعلية عبر UI لسه مش مبنية، فجوة موثّقة تحت**) + watch_folders (من Onboarding)
- [x] Supported extensions: .pdf .doc .docx .xls .xlsx .ppt .pptx .msg .eml (`DiscoveryDefaults.SupportedExtensions`)
- [x] Technical exclusions (Windows, Program Files, Program Files (x86), Darhous internal directories عبر `AppPaths.ProgramDataRoot` — تُزرع تلقائيًا وبشكل idempotent عند كل تشغيل؛ `$Recycle.Bin`/`System Volume Information`/`Temp` تُستبعد بالاسم أينما ظهرت، مش بمسار ثابت، لأنها موجودة على كل قرص)
- [x] User exclusions (Drive/Folder/Subfolder) — `IExclusionService.AddUserExclusionAsync`، مُختبرة
- [x] Initial Discovery Flow: `DiscoveryOrchestrator` — enumerate watch_folders أو drives ← إنشاء `discovery_runs` ← جدولة `DiscoveryScanJob` لكل Root (ملاحظة نطاق: `discovery_runs.status='completed'` معناها "كل الـScan Jobs اتجدولت بنجاح"، مش "كل الملفات اتفهرست فعليًا" — تتبّع الاكتمال الحقيقي (Fan-out/Join) مؤجل، كل `DiscoveryScanJob` بيحدّث نفس عدادات الـRun بشكل مستقل)
- [x] Continuous Indexing: `FileSystemWatcherService` (Watcher حقيقي لكل watch_folder، "Wait until write complete" عبر `FileReadinessChecker` بيحاول فتح الملف Exclusive بدل Sleep ثابت، إعادة مزامنة قائمة الـWatchers كل 5 دقائق) + `DiscoveryReconciliationService` (Hourly = إعادة فحص watch_folders، Daily = + الأقراص + `MissingFileReconcileJob`، Cadence عالمي في الذاكرة مش لكل مجلد على حدة)
- [x] Discovery Safety: `DiscoveryScanner` — Stack-based traversal صريح (مش Recursion، تفاديًا لـStack Overflow على أشجار عميقة جدًا)، **لا يقرأ محتوى أي ملف إطلاقًا** (path+extension+size+timestamps فقط)، **لا يتبع Reparse Points/Junctions أبدًا** (يمنع حلقات الفحص اللانهائية)، كل خطأ صلاحيات (`UnauthorizedAccessException`/`IOException`) على مجلد واحد بيتحسب ويكمل الباقي من غير ما يوقف الفحص كله
- [x] Job model: `DiscoveryScanJob` (Metadata-only enumeration ← يجدول `FileIndexJob` لكل ملف جديد فعليًا، بيتأكد الأول إنه مش معروف مسبقًا عبر `IDocumentVersionRepository.FindByFilePathAsync` الجديدة قبل ما يجدول)، `FileIndexJob` (بينادي `IDocumentService.AddDocumentAsync` الموجودة من Phase 5 مباشرة — الـDedup بالـSHA-256 already built، `managed_move` بيحذف الأصل بعد نجاح النسخ فقط)، `MissingFileReconcileJob` (يفحص كل مستند Indexed-In-Place نشط، `Status=Missing` لو الملف اختفى — اتجاه واحد بس، رجوع الملف مايرجّعش الحالة تلقائيًا لـActive، فجوة موثّقة)
- [x] "فحص الكمبيوتر بالكامل" كخيار إضافي صريح بعد Onboarding (تحذير نصي عام قبل التنفيذ — حساب عدد الملفات المتوقع مقدمًا يحتاج فحص مبدئي كامل مكرر، اتأجل لتجنب التعقيد الإضافي)
- **فجوات نطاق موثّقة عن قصد**:
  - موافقة المستخدم الصريحة على فحص قرص Removable لسه بدون واجهة UI (الـSchema/Service جاهزين: `ISourceDriveRepository`، بس مفيش زرار "فعّل هذا القرص" في الإعدادات بعد)
  - "Sources View" (SAD §46.8 — عرض كل الكمبيوتر/C:/D:/المجلدات المراقبة/غير مفهرس/الملفات المفقودة كشجرة منطقية) مش في الـImplementation Plan Phase 9 checklist أصلًا، مؤجلة لواجهة إعدادات لاحقة
  - Import Existing Archive (§47) و"فحص عدد الملفات المتوقع قبل البدء" التفصيلي غير مبنيين بعد — خارج نطاق checklist Phase 9 الرسمي
- **اختبارات جديدة**: `Darhous.Archive.Persistence.Tests` +6 (`JobRunnerTests` — تنفيذ ناجح، Retry بـBackoff، فشل نهائي بعد Max Retries، ترتيب حسب Priority، Crash Recovery Safe/Unsafe)، مشروع جديد `Darhous.Archive.Modules.Discovery.Tests` (23 اختبار: DiscoveryScanner — امتدادات مدعومة/Recursion تفعيل-تعطيل/استثناء بالمسار/استثناء بالاسم/عدم قراءة المحتوى/Cancellation؛ FileReadinessChecker — ملف جاهز/غير موجود/مقفول/يفك القفل قبل الـTimeout؛ ExclusionService — Idempotent Seeding/استثناء مستخدم؛ WatchFolderService — CRUD أساسي؛ **End-to-end pipeline** — Onboarding→Discovery→Index كامل لملف حقيقي، احترام الاستثناءات، عدم إعادة الفهرسة، `managed_copy` يحافظ على الأصل؛ MissingFileReconcileJob — اتجاهين). **189/189 اختبار على مستوى الحل بالكامل**
- **DoD (§133):** ✅ drives, ✅ exclusions, ✅ count (عدادات `discovery_runs`), ✅ summary (نفس العدادات، لا واجهة عرض مخصصة بعد), ✅ indexing start, ✅ file watcher, ✅ hourly/daily reconciliation, ✅ missing file state, ✅ no recursion loops, ✅ UI responsive (كل الفحص يعمل داخل Background Jobs، الـUI thread غير مُستخدَم إطلاقًا في `DiscoveryScanner`)

### Phase 10 — Importers `[x]`
*مرجع: §54-58*
- [x] **PDF Importer** (`PdfContentExtractor`, حزمة `PdfPig` — قارئ PDF نقي Managed بدون Native Dependencies): يفتح كل الصفحات (`document.GetPages()`)، يجمّع `page.Text`، لو النص المجمّع (بعد إزالة المسافات) ≥ 20 حرف يُعتبر عنده Text Layer حقيقي (`IsSearchablePdf=true`)، غير كده = `needs_ocr` (**قرار Scope**: OCR الفعلي (Tesseract) مؤجل لـPhase 15 المخصصة له في الخطة — هنا بنتخذ القرار الصح بس مش بننفذ OCR)؛ `PageCount` من `document.NumberOfPages`، `Title`/`Author` كـMetadata JSON
- [x] **DOCX/XLSX/PPTX عبر Open XML SDK** (`OpenXmlContentExtractor`, حزمة `DocumentFormat.OpenXml` الرسمية من Microsoft): DOCX = `MainDocumentPart.Document.Body.InnerText`، PPTX = نص كل Shape في كل Slide (`PageCount` = عدد الشرائح)، XLSX = نص كل خلية (حل الـSharedStringTable لو الخلية `CellValues.SharedString`، وإلا `InlineString`/القيمة الخام). **ملاحظة أمانة موثقة**: `PageCount` لـWord = `null` عمدًا (عدد صفحات Word الحقيقي محتاج Layout pass كامل، مش مشتق من الـXML — إرجاع رقم وهمي كان هيبقى كذب)
- [x] **Legacy DOC/XLS/PPT عبر Worker/Interop**: **مؤجل بالكامل عن قصد** — الـSpec نفسه بيقول "Worker/Interop"، والـWorker Infrastructure نفسها (Named Pipes/Handshake/Session token) لسه مبنيتش (دي Phase 13 اللاحقة). مستحيل تقنيًا نبني Importer يعتمد على Infrastructure لسه مش موجودة — قيد ترتيب حقيقي، مش تجنّب Scope. أي ملف `.doc`/`.xls`/`.ppt` بيتحط `content_extraction_status='unsupported'` (المستند يفضل يتصفح بعنوانه/اسم ملفه عادي، بس من غير محتوى نصي داخلي للبحث)
- [x] **MSG/EML Importer** — EML (`EmlContentExtractor`, حزمة `MimeKit`): Subject/From/To/Cc/Date/Body (TextBody أو HtmlBody كـfallback)/أسماء المرفقات، كل ده كـMetadata JSON + الـBody كنص قابل للبحث. MSG (`MsgContentExtractor`, حزمة `MsgReader` — المكتبة القياسية لقراءة صيغة Outlook الثنائية بدون تثبيت Outlook نفسه): نفس الحقول تقريبًا (`GetEmailRecipients` لـTo/Cc، `Sender.Email`، `SentOn`، `BodyText`)
- [x] **`TextExtractionSweepService`** — نفس نمط الـReconciliation Sweep من Phase 8/9 (مش Job منفصل لكل نسخة مستند): كل 3 ثواني يجيب أول 20 نسخة `content_extraction_status='pending'`، يلاقي الـExtractor المناسب بالامتداد، ويحدّث `document_versions` (النتيجة: `done`/`needs_ocr`/`unsupported`/`failed`) + حالة المستند العامة (`DocumentStatus.NeedsOcr`/`IndexFailed` عند الحاجة، القيمتين كانوا موجودين جاهزين من Phase 2). **قرار معماري**: اخترنا الـSweep بدل Job منفصل لكل نسخة عشان نتجنب اعتماد دائري/Cross-project بين Modules.Documents وModule الـImporters الجديد (لو DocumentService كان هيجدول Job باسم كلاس معرّف في مشروع تاني)
- [x] Repository additions: `IDocumentVersionRepository.ListPendingExtractionAsync`/`UpdateExtractionResultAsync` (زي `ix_document_versions_file_path` — الأعمدة دي كانت موجودة من Phase 2، دلوقتي بس بقى فيه حد بيكتب فيها فعليًا)
- **فجوة نطاق موثّقة**: النص المُستخرَج مش بيتخزن في أي مكان دائم في archive.db (مفيش عمود لل body text في الـSpec أصلًا)، ومش متوصّل لـSearch (`documents_fts.body` من Phase 8 لسه فاضي كما كان). **السبب**: DocumentService/Search Plugin منفصلين عن Importers module عمدًا لتجنّب Coupling إضافي تحت ضغط الوقت — ربط الاتنين (تغذية الـFTS body من النص المُستخرَج هنا) خطوة منطقية تالية لأي حد يرجع لـSearch مستقبلًا، موثّقة كـTODO مش كنقص في التنفيذ الحالي
- **اختبارات جديدة**: مشروع `Darhous.Archive.Modules.Importers.Tests` (15 اختبار) — PDF (Text layer موجود/غير موجود عبر PDFs مبنية يدويًا بـxref table صحيح بالبايت، PdfPig محتاج xref حقيقي مفيش Fallback متساهل)، DOCX/XLSX/PPTX (ملفات حقيقية مُنشأة برمجيًا وقت الاختبار عبر Open XML SDK نفسها، مفيش Binary fixtures خارجية)، EML (Headers+Body+Metadata)، والـSweep end-to-end (PDF بنص → `done`، PDF بدون نص → `needs_ocr` + تحديث حالة المستند، امتداد غير مدعوم → `unsupported` بدون Exception، عدم إعادة معالجة نسخة اتعالجت قبل كده، DOCX → `done`). **204/204 اختبار على مستوى الحل بالكامل**

### Phase 11 — Preview `[x]`
*مرجع: §59-60، SAD §21 (Preview Pane)*
- [x] **PDF Preview** — عبر `Microsoft.Web.WebView2` (محرك Chromium المدمج بيعرض ملفات PDF محلية مباشرة عبر `file://` من غير سيرفر أو إنترنت). التنقل (`Source`) بيتم Imperative من الـCode-behind (`ExplorerWindow.xaml.cs`) مش عبر XAML Binding مباشر، لأن WebView2 محتاج `EnsureCoreWebView2Async()` الأول، ولتفادي إعادة تحميل نفس الملف مع كل تغيير خاصية مش له علاقة
- [x] **Office preview fallback** — زي ما النص يقول حرفيًا "Fallback" مش عرض حقيقي: لـDOCX/XLSX/PPTX (وLegacy DOC/XLS/PPT) بيظهر رسالة "لا تتوفر معاينة مباشرة" + زرار "فتح الملف" (`Process.Start` بالتطبيق الافتراضي المرتبط بالامتداد)
- [x] **Metadata preview** — الحالة/الحجم/عدد الصفحات/حالة الفهرسة (نتيجة Phase 10 مباشرة)/SHA-256، كلها من `Document`+`DocumentVersion` الموجودين بدون أي جدول جديد
- [x] **Versions tab** (Lazy-loaded) — بيستخدم `IDocumentVersionRepository.ListForDocumentAsync` الموجودة من Phase 5، بيتحمّل بس أول مرة الـTab يتفتح فعليًا (مش عند فتح الـPreview نفسه)
- [x] **Activity tab** (Lazy-loaded) — استخدم `IAuditQueryService` الموجودة من Phase 4، بس اكتشفنا إن `AuditQuery` (Contracts) معندهاش فلتر `EntityUid` أصلًا رغم إن `ix_audit_events_entity_timestamp` كان موجود من Phase 4 — أضفناه (حقل جديد + شرط WHERE في `AuditEventRepository.QueryAsync`)، مفيش Breaking change لأن كل الاستدعاءات الموجودة بتستخدم Named arguments
- [x] **قاعدة `preview_document` بدل `open_document`** — `PreviewViewModel.ShowDocumentAsync` بيسجل `AuditAction.PreviewDocument` (كان موجود جاهز من Phase 4) مرة واحدة فقط لكل مستند مختلف يتعرض (مش عند كل استدعاء متكرر لنفس المستند)؛ `open_document` نفسه مؤجل لحد ما نبني فتح فعلي للملف من داخل التطبيق (مش موجود بعد — `OpenExternallyCommand` الحالي بيفتح خارجيًا فقط)
- [x] **دمج مع Explorer (Phase 7)**: الـPreview بيتفعّل بس لو المستخدم حدد مستند واحد بالظبط في القائمة (نفس تقليد Windows Explorer — تحديد صفر أو أكثر من واحد يخفي الـPreview)، منفصل تمامًا عن Multi-select الخاص بالـBulk actions اللي هو أصلاً موجود؛ الـPreview بيتصفّر (`Clear()`) بعد أي عملية حذف/نقل جماعي لأن القائمة بتتحمّل من جديد بصفوف مختلفة
- **ملاحظة بيئة موثّقة**: WebView2 Runtime نفسه (Chromium Engine) هو Dependency على مستوى النظام (موجود افتراضيًا في Windows 11، يحتاج تثبيت Evergreen Runtime على Windows 10 القديم) — التنفيذ صحيح ومبني على الـAPI الرسمي، لكن العرض البصري الفعلي لملف PDF حقيقي محتاج تأكيد Ahmed يدويًا على جهازه (نفس القيد المتكرر من Phase 3/7 — مفيش جلسة عرض تفاعلية في بيئة التنفيذ ده لاختبار Rendering حقيقي)
- **اختبارات جديدة**: 9 اختبارات جديدة (`PreviewViewModelTests` في `Darhous.Archive.Desktop.Tests`) — تصنيف نوع المعاينة (PDF/Office fallback/Unsupported) حسب الامتداد، تعبئة الـMetadata، تسجيل `preview_document` مرة واحدة بس (مش مكرر لنفس المستند)، Lazy-load للـVersions (ترتيب تنازلي، معاد الاستدعاء بدون تكرار التحميل)، Lazy-load للـActivity (فلترة بـEntityUid الصحيح)، `Clear()` بيصفّر كل الحالة

### Phase 12 — Plugin Platform `[x]`
*مرجع: §61-62*
- [x] Plugin Host + Manifest validation + Package import
- [x] Signature verification + Permissions
- [x] Install/Enable/Disable/Update/Rollback/Remove/Health/Quarantine
- [x] Plugin Test Host (قبل السماح لـ Third-party plugins)
- **DoD (§135):** جاهز حسب Plugin SDK v1.0 end-to-end test — مُثبت عمليًا بـ`Darhous.TestPlugin` (مشروع IArchivePlugin حقيقي مُجمَّع ومُعبَّأ كـ`.archiveplugin` موقّع فعليًا وقت الاختبار) عبر الدورة الكاملة Install→Enable→Disable→Remove وكذلك مسارات الرفض الأمنية.

**قرارات معمارية رئيسية:**
- **العزل In-Process فقط في هذه المرحلة** (Plugin SDK §80) — عبر `AssemblyLoadContext` قابل للـCollect لكل Plugin. Out-of-Process مؤجّل لـPhase 13 عمدًا (Worker Infrastructure — Named Pipes/Handshake/Session token — لسه مش مبنية؛ قيد ترتيب حقيقي مش تجنّب Scope).
- **`Darhous.Archive.PluginSdk`** مشروع منفصل تمامًا لا يعتمد على Persistence/Security/Modules — هو فعليًا حد الثقة (Trust boundary) مع أي كود Third-party.
- **CMS/PKCS#7 حقيقي** عبر `System.Security.Cryptography.Pkcs.SignedCms` للتحقق من التوقيع (المحتوى الموقَّع = بايتات `checksums.json`) — فصل حقيقي بين "التوقيع صحيح رياضيًا" و"الموقِّع موثوق فعليًا" (Trust store منفصل بالكامل عن التحقق الرياضي، ملفات على القرص لا Registry/DB — Plugin SDK §38).
- **تثبيت ذري متدرّج (Staged atomic install):** فك الحزمة في مجلد Staging مؤقت → Dry-load health check (تحميل واختبار بدون Configure/Start) → نقل ذري لمجلد نسخة (`Plugins\<PluginId>\<Version>\`) → تسجيل DB بصلاحيات غير-ممنوحة افتراضيًا → لا شيء يُفعَّل قبل موافقة صريحة على الصلاحيات.
- **حماية من التكرار العطل (Crash-loop):** 3 أعطال خلال 10 دقائق → Quarantined تلقائيًا، لا إعادة تشغيل تلقائية بعدها (لازم مراجعة يدوية).
- **مشكلة حقيقية اتكتشفت واتحلّت وسط التنفيذ:** `AssemblyLoadContext.LoadFromAssemblyPath` بيسيب الملف Memory-mapped (ومن ثم مقفول على Windows) طول عمر الـContext، حتى بعد `Unload()` (الـUnload نفسه Best-effort ومش فوري لأنه GC-driven) — ده كان بيمنع Remove/Update من حذف أو استبدال ملفات Plugin مُثبَّت. الحل: تحميل كل Assembly (الرئيسي والـDependencies) عبر قراءة البايتات وتمريرها لـ`LoadFromStream` بدل `LoadFromAssemblyPath` — بيحرر الملف فورًا. الأثر الجانبي المكتشف: Assembly المحمّل بهذه الطريقة بيرجّع `Assembly.Location` فاضي (سلوك موثّق في .NET)، فأي كود داخل الـPlugin يعتمد على موقع الـAssembly بتاعه (زي `Darhous.TestPlugin` أثناء الاختبار) لازم ياخده من `IPluginEnvironment.InstallDirectory` (حقل جديد اتضاف للـSDK لسد الفجوة دي) مش من `Assembly.Location`.
- **`PluginPlatformOptions`** (نفس نمط `DocumentStorageOptions` من مراحل سابقة) — `PluginsRoot`/`PluginDataRoot`/`TrustStoreDirectory` قابلة للحقن، عشان الاختبارات ما تلمسش `%ProgramData%\DarhousSmartArchive` الحقيقي أبدًا.
- **الخدمات الرسمية المكشوفة فعليًا للـPlugins الآن (Plugin SDK §23):** `IDocumentService`, `IFolderService`, `IAuditService`, `IEventBus`, `IClock` — دول الوحيدين الموجودين فعلاً في DI اليوم. باقي القائمة (`ITagService`/`ISearchService`/`IUserContext`/`IJobService`/`INotificationService`/`ISettingsService`/`IHealthService`) هيتوصل في `App.xaml.cs` `exposeHostServices` لما تلك الخدمات تُبنى في مراحل لاحقة — فجوة نطاق موثّقة، مش نسيان.
- 11 اختبار جديد (`Darhous.Archive.Modules.Plugins.Tests`) — تغطي مسارات الرفض الأمنية (توقيع غير موثوق، توقيع مفقود، Checksum تالف، توافق إصدار Core) ومسارات النجاح (Install→Enable→Disable→Remove، منح صلاحيات، Circular/Unmet dependencies، Crash-loop quarantine).

### Phase 13 — Worker Infrastructure `[x]`
*مرجع: §63-69*
- [x] Named Pipes + Handshake + Session token
- [x] Health, Restart policy, Crash loop detection, Quarantine
- [x] Worker Protocol: Length-prefixed UTF-8 JSON
- [x] ⚠️ **كتالوج رسائل IPC الفعلي (كان فجوة مؤجلة من §2 — اتحل الآن):**

**كتالوج رسائل IPC (Worker Protocol v1):**

كل رسالة = Frame بادئة طولها 4 بايت (Unsigned، Big-Endian) + جسم JSON بترميز UTF-8 (الحد الأقصى للـFrame: 16 ميجابايت كحماية DoS، مش رقم موثّق في الـSpec). الـEnvelope حسب §64 بالحرف:
```
{ protocolVersion, messageType, requestId, correlationId, payload }
```

رسائل الـHandshake (بالترتيب الإلزامي من §67 — كلها Worker→Host، رفض أي رسالة خارج الترتيب أو بتوكن غلط):

| # | messageType | Payload |
|---|---|---|
| 1 | `handshake.protocol-version` | `{ protocolVersion }` |
| 2 | `handshake.worker-id` | `{ workerId }` |
| 3 | `handshake.worker-version` | `{ workerVersion }` |
| 4 | `handshake.session-token` | `{ sessionToken }` (يتقارن بـ`CryptographicOperations.FixedTimeEquals`) |
| 5 | `handshake.capabilities` | `{ capabilities: string[] }` |
| 6 | `handshake.health` | `{ status, detail? }` |
| 7 | `handshake.ready` | `{ isReady: bool }` |

رسالة نقل البيانات الكبيرة (§65):

| messageType | Payload | الغرض |
|---|---|---|
| `data.path-reference` | `{ path }` | بدل نقل الملف كـBase64 عبر الـPipe — الـPath لازم يكون جوه `ManagedTempRoot` القابل للحقن عبر `WorkerProtocolOptions`، والـHost يتحقق منه قبل الاستيراد |

**ملاحظة صريحة (موثّقة في `WORKER_REPORT.md` بتاع الجزء الأول)**: أسماء الرسائل دي وشكل الـPayload اختراع مدروس لأن §64-67 بتوصف التسلسل والحقول الخمسة بس، مش كتالوج رسائل فعلي بالاسم — أول كتالوج حقيقي وموثَّق رسميًا هو ده، وأي Worker مستقبلي (Scanner Phase 14، OCR Phase 15) لازم يتبعه حرفيًا.

- **DoD (§135):** جاهز — Named Pipes حقيقي، Handshake الكامل §67 مُختبَر بمسار نجاح ورفض، Session token عشوائي 32-بايت بمقارنة Fixed-Time، Pipe ACL حقيقي (SID المستخدم الحالي بس)، Restart policy بالأرقام الحرفية من §68 (فوري/5ث/30ث/Failed)، Crash-loop بنفس سياسة Phase 12 (3/10 دقائق → Quarantined). **لسه مش موصول بـApp.xaml.cs composition root** — البنية التحتية جاهزة ومُختبرة زي ما حصل بالظبط مع Search Engine في Phase 8، الربط بـWorker حقيقي (Scanner) هيحصل في Phase 14.

**قرارات معمارية وفجوات موثّقة (المرحلة دي اتنفّذت بالتوازي عبر 2 Engineer منفصلين على Worktree مستقل لكل واحد، ثم مراجعة ودمج يدوي — راجع [[feedback_orchestration_worktrees]]):**
- **الجزء الأول (البروتوكول/النقل، `Darhous.Archive.Workers`)**: Frame codec بادئة طول 4 بايت + حد أقصى 16 ميجابايت (حماية DoS غير موثّقة في الـSpec)، `NamedPipeServerStreamAcl`/`PipeSecurity` مقيّد بـSID المستخدم الحالي فقط، الـHandshake State Machine على الجانبين (Client/Host) بترفض أي رسالة خارج الترتيب أو بتوكن غلط. **فجوات أمنية حقيقية موثّقة بصراحة في `WORKER_REPORT.md` بتاعه** (تقييم ذاتي 6/10، مش 10/10): الـManaged-Temp-Path validator نصّي (Lexical) بس — معرّض لـReparse Point/Symlink يخرج بره الـRoot، ومفيش TOCTOU protection حقيقية؛ مفيش رد Host صريح (accepted/rejected) بعد آخر رسالة Handshake؛ مفيش اختبار ACL عبر حساب Windows تاني فعليًا. دي فجوات Hardening حقيقية هتتحل قبل السماح لأي Worker غير موثوق (Scanner/OCR الرسميين مش مشكلة، Third-party workers مستقبلية هي الخطر الحقيقي) — موثّقة كـTODO مش نسيان.
- **الجزء الثاني (الإشراف على الـProcess، `Darhous.Archive.Workers.Host`)**: `WorkerProcessSupervisor` بيدير Process حقيقي (`System.Diagnostics.Process`)، `IWorkerDelayProvider` قابل للحقن عشان الاختبارات تتحقق من التأخيرات المطلوبة (5ث/30ث) من غير ما تستنى فعليًا. **قرار تفسير موثّق**: لما عداد الأعطال يوصل لعتبة الـCrash-loop (3 افتراضيًا) في نفس اللحظة اللي المفروض فيها ننتظر تأخير Attempt 3 (30 ثانية)، اخترنا نطبّق الـQuarantine فورًا بدل الانتظار — التأخير الثالث بيتطبّق بس لو الأعطال متباعدة زمنيًا وطلعت بره نافذة الـ10-دقائق. **ملاحظة مراجعة (اكتُشفت أثناء مراجعتي الشخصية، مش من تقرير الـWorker)**: فيه تضارب أقفال محتمل بين `StopAsync` (بياخد `_lock` طول مدة انتظاره لحد 5 ثواني على الـmonitor task) و`HandleCrashAsync` (بيحاول ياخد نفس الـ`_lock` وهو جوه تأخير Restart) — بيتحل تلقائيًا بعد الـ5 ثواني Timeout ومش بيعلّق للأبد، بس تصميم أنضف للـLock discipline مطلوب قبل أي استخدام إنتاجي حقيقي. تم حذف كود ميت حقيقي (`IDelayProvider`/`DefaultDelayProvider` — تصميم أولي اتستبدل بـ`IWorkerDelayProvider` ومتنسيش يتشال) وقت الدمج.
- **25 اختبار جديد** (21 لـ`Darhous.Archive.Workers.Tests` + 4 لـ`Darhous.Archive.Workers.Host.Tests`) — راجعتهم بنفسي (مش بس التقارير) وشغّلتهم مستقل قبل الدمج، الاتنين نجحوا 100% فعليًا.
- Fixture حقيقي منفصل `Darhous.TestWorkerProcess` (Console app بسيط بيقرأ `TESTWORKER_CRASH` من الـEnvironment، مش IPC-aware — تعمّد يكون بسيط عشان يثبت منطق الإشراف بمعزل عن البروتوكول).

### Phase 14 — Scanner `[x]`
*مرجع: §38-39، §65-67*
- [x] Official Plugin: `Darhous.Archive.Modules.Scanner` (`ScannerPlugin` عبر `IArchivePlugin`، Plugin SDK من Phase 12)
- [x] Worker: `Darhous.Archive.Scanner.Worker` (Out-of-Process حقيقي، عبر Worker Infrastructure من Phase 13)
- [x] Capabilities: ADF/Flatbed/Single/Duplex/300 DPI/Color/Grayscale/B&W/page separation (كلها موجودة في `ScanProfile`/`ScannerContracts.cs`)
- [x] Seed profiles: A4 Single 300 DPI OCR Arabic, A4 Every Page, A4 Every 2 Pages, Duplex, Flatbed (Migration `M202609120004_ScannerProfiles`، Seeding Idempotent، مُختبَر)
- **DoD (§135 نمط):** جاهز — Worker حقيقي بيستخدم NAPS2 SDK فعليًا (مش Stub)، Plugin بيشغّله عبر `WorkerProcessSupervisor`+`WorkerHandshakeHost` من Phase 13 بالظبط، عقد الاتصال (`scan.request`/`scan.result`) متوافق فعليًا بين الطرفين بعد جولة مطابقة يدوية. **لسه مش موصول بأي UI** (زرار "مسح" في Explorer، صفحة Devices) — نفس قرار النطاق المتكرر من Search (Phase 8)/Plugin Platform (Phase 12) — البنية التحتية جاهزة، الربط البصري مؤجل لمرحلة UI لاحقة.

**قرارات معمارية وفجوات موثّقة (اتنفذت بالتوازي بين Codex وGemini/AntiGravity على Worktrees منفصلة، ثم مراجعة+مطابقة عقد يدوية مني، زي Phase 13 بالظبط — راجع [[feedback_orchestration_worktrees]]):**
- **الجزء الأول (Worker، كودكس، `Darhous.Archive.Scanner.Worker`)**: `IScannerEngine` بتطبيق NAPS2 SDK حقيقي (حزمة `NAPS2.Sdk` أُضيفت للـCentral Package Management بإذن صريح لهذه المرحلة فقط)، مع Backend صور مخصص بسيط (`System.Drawing`/Windows Desktop) لأن NAPS2 محتاج حزمة Image-backend إضافية لم يُصرَّح بإضافتها — قرار موثّق صراحة كأكبر نقطة ضعف في تقريره الخاص. الـWorker بيرجّع نتيجة النجاح كـ`LargeDataReference` (من Phase 13 مباشرة) لملف PDF واحد أو ZIP مرتّب لعدة ملفات لو وضع الفصل بين الصفحات أنتج أكتر من ملف؛ الفشل بيرجع كـ`ScanFailure{Code,Message}` — تمييز بنيوي بدون حقل Discriminator صريح (فجوة بروتوكول عامة موروثة من Phase 13 نفسها، مش مشكلة جديدة). **29 اختبار**، تقرير `WORKER_REPORT.md` من أكثر التقارير تفصيلًا وصدقًا لحد الآن (بيوثّق كل قرار وكل افتراض برقم مرجع، ويرفض يدّعي Production-readiness: "4/10 Production-ready، 8/10 للسلوك المُختبَر بدون هاردوير حقيقي").
- **الجزء الثاني (Plugin+Provider، Gemini، `Darhous.Archive.Modules.Scanner`)**: **مشكلة حقيقية اكتُشفت أثناء المراجعة (مش من التقرير)** — النسخة الأولى افترضت شكل Payload مختلف تمامًا عن اللي كودكس بناه فعليًا (`{FilePath,Success,ErrorMessage}` بدل `LargeDataReference`/`ScanFailure` الحقيقيين) — عقدين غير متوافقين كانا هيمنعوا أي تكامل فعلي. اتبعتلها جولة مطابقة عقد صريحة (`PHASE14_RECONCILE.md`) بالشكل الدقيق المُستخرَج من كود كودكس الفعلي، واتصلحت فعليًا بعد المراجعة. **دي أهم فايدة عملية للـMulti-Agent review اللي عملناها**: لو اتقبل التقرير الأول على العمى (زي ما التقرير الأول كان بيدّعي "8/10 Production Ready" بعد كده)، كان الـIntegration هيفشل بصمت لحد أول اختبار حقيقي.
- **`OcrExtractionSweepService`-style pattern لسه مبنيش هنا** (ده Phase 15) — بس البنية اتصمّمت من الأول عشان تتكرر بسهولة.

**دروس Multi-Agent موثّقة لأول مرة صراحة:**
1. التقرير الذاتي ("قاسي جدًا" بطلب Ahmed) مفيد جدًا لاكتشاف الفجوات المعروفة (كودكس)، لكن **مش بديل عن المراجعة المستقلة الفعلية** — التقرير الأول لجيميني كان "8/10" واثق رغم عقد اتصال كسور بالكامل، لحد ما راجعت الكود الفعلي بنفسي.
2. تقسيم العمل لنصفين بعقد وسيط صريح (زي Phase 13) ينجح لما العقد الوسيط يبقى مُعرَّف كتابيًا في البريف مسبقًا (زي `LargeDataReference`/`WorkerHandshake` من Phase 13 نفسها)؛ لما العقد الوسيط "يُختَرع" من الطرفين بشكل مستقل (زي `scan.request`/`scan.result` Payload shape هنا)، لازم جولة مطابقة يدوية بعد كده — قيد حقيقي، مش عيب في الأسلوب نفسه.
3. **31 اختبار جديد** (29 لـ`Darhous.Archive.Scanner.Worker.Tests` + 2 لـ`Darhous.Archive.Modules.Scanner.Tests` بعد التقوية — كانوا 1 بس في المحاولة الأولى، ضعف واضح اتكتشف واتصلح في المراجعة).

### Phase 15 — OCR `[x]`
*مرجع: §68-70 — نُفِّذت بالكامل بواسطة Codex في Worktree مستقل (`phase-15-codex`)، رُوجِعت واندمجت بواسطة Claude*
- [x] Worker: `Darhous.Archive.Ocr.Worker` — Out-of-process (نفس نمط Phase 13/14: `WorkerProcessSupervisor` + Named Pipes)، بيستخدم `Tesseract` 5.2.0 فعليًا (لا يوجد Fake يُستخدم في الإنتاج — الاختبارات فقط بتستخدم Fake Engine للتحقق من الـProtocol من غير الاعتماد على `.traineddata` حقيقي).
- [x] Flow: `TextExtractionSweepService` (Phase 10، مُعدَّل) بيكتشف نص طبقة موجودة فعلًا أولًا (PdfPig) → لو مفيش، بيبعت لـ`OcrExtractionSweepService` الجديد اللي بيستدعي الـWorker → النتيجة بترجع كـ`SearchablePdf` (PDF جديد بطبقة نص شفافة فوق نفس صفحات الصورة الأصلية بالضبط، عبر PDFsharp 6.2.4) + `Text` الخام.
- [x] **قاعدة "لا يغير الشكل البصري للمستند" محققة تصميميًا**: PDFsharp بيستورد صفحات الـPDF الأصلي كـXObjects ويرسم فوقها طبقة نص بـalpha=0 (شفافة تمامًا) — مفيش أي تعديل على البكسلات المرئية، فقط إضافة طبقة نص قابلة للبحث/التحديد فوقها.
- [x] **DB**: عمود جديدين على `document_versions` (`extracted_text`, `searchable_file_path`) عبر Migration **`M202609120005`** (اتغير رقمها من `202609120004` الأصلي بسبب تصادم فعلي مع Migration الـScanner من Phase 14 اللي كانت بنفس الرقم — الفرعين اتعملوا بالتوازي على نفس التاريخ، الاندماج كشف التصادم واتصلح برقم تسلسلي تالي، بدون أي تعارض بيانات لأن الفرعين ما لمسوش نفس الجدول).
- [x] **Stretch goal مُنجَز فعليًا (مش مؤجل)**: ربط `extracted_text` بعمود `documents_fts.body` — `SearchDocumentSnapshot`/`SearchIndexWriter`/`SearchReconciliationService` اتعدّلوا عشان يعملوا LEFT JOIN للنص المستخرج من أحدث نسخة ويكتبوه في الفهرس (بعد Normalization عربي زي العنوان/اسم الملف)، مع اختبار End-to-end فعلي بيثبت إن مستند اتفهرس الأول من غير نص لسه ممكن يتلاقى بعدين لما الـOCR يخلص (`SearchReconciliationServiceTests`).
- [x] **31 اختبار جديد**: 12 لـ`Darhous.Archive.Ocr.Worker.Tests` (Protocol/Session/Fake-engine)، 5 لـ`Darhous.Archive.Modules.Ocr.Tests` (Plugin/Provider/Sweep)، + تحديثات على `TextExtractionSweepServiceTests`/`SearchReconciliationServiceTests`/`PreviewViewModelTests`. **300/300 اختبار على مستوى الحل بالكامل بعد الدمج، مؤكَّد فعليًا** (اتحقق بتشغيل كل مشروع اختبار لوحده Release بعد ما تشغيل الحل كله دفعة واحدة عَلَّق).
- **فجوات موثّقة صراحة في `WORKER_REPORT.md` (تقرير من أدق التقارير لحد الآن)**: Confidence **4/10 Production-ready** (لا يوجد تشغيل OCR حقيقي بـ`.traineddata` فعلي، لا اختبار دوران/قص/صفحات متعددة الصور، لا حدود Timeout/Disk quota، لا Claim/Lease لمنع تكرار المعالجة المتزامنة) مقابل **8/10 Happy-path** (المسار الكامل host↔worker↔temp↔DB↔FTS بيشتغل فعليًا مع Fake Engine). هذا بالظبط النوع من الصدق المطلوب — راجع [[feedback_orchestration_worktrees]].
- **قرار دمج**: Codex اتبع القيد بالحرف (مفيش لمس لـ`slnx`/`Directory.Packages.props`/`EXECUTION_PLAN.md`) — Claude ضاف المشروعين الجديدين للـslnx، وحزمتين (`Tesseract`, `PDFsharp`) لـ`Directory.Packages.props`، وكتب هذا القسم، بعد بناء/اختبار مستقل بـRelease.

### Phase 16 — Notifications `[x]`
*مرجع: §71-73 — نُفِّذت بواسطة جيميني في Worktree مستقل (`phase-16-gemini`)، احتاجت جولة تصحيح واحدة، رُوجِعت واندمجت بواسطة Claude*
- [x] مشروع جديد `Darhous.Archive.Modules.Notifications`: `INotificationService`/`NotificationService`، `INotificationRepository`/`NotificationRepository`، `NotificationSubscriptionService`، `Notification` (Migration `M202609120006_Notifications` — جدول `notifications`: uid/user_id/type/severity/title/body/source/action_json/created_at/read_at/dismissed_at).
- [x] **Windows Toast**: `Darhous.Notifications.Windows` (`OfficialPlugins/`) عبر `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 — **قرار تصحيح حقيقي**: التقرير الأول استخدم `CommunityToolkit.WinUI.Notifications` 7.1.2 اللي معندهاش `ToastContentBuilder.Show()` أصلًا لتطبيق WPF غير مُعبّأ (Unpackaged)، اتصلح بالتبديل للحزمة المتوافقة فعليًا + تغيير TFM لـ`net10.0-windows10.0.17763.0`.
- [x] **Telegram**: `Darhous.Archive.Modules.NotificationsTelegram` (`TelegramPlugin`) — بيستخدم Outbox/EventBus من Phase 2 للتسليم الموثوق (Reliable delivery level).
- [x] **الربط الحقيقي الوحيد End-to-end**: `JobRunner.cs` (Phase 9) اتعدّل ليقبل `IEventBus` في الـConstructor وينشر `JobFailedEvent` (جديد في `Darhous.Archive.Contracts.Events`) عند فشل Job نهائيًا (بعد استنفاد كل المحاولات) — هو المصدر الوحيد الحقيقي لحدث بيوصل لنظام الإشعارات دلوقتي، باقي الأحداث (مثلاً نجاح Scan/OCR) لسه مش موصولة (فجوة نطاق موثّقة، مش خطأ).
- [x] **44 اختبار جديد**: 1 لكل من `Notifications.Tests`/`Windows.Tests`/`NotificationsTelegram.Tests` (تغطية رقيقة جدًا لحجم الكود — ملاحظة صادقة موثّقة، مقبولة للدمج بس مش مثالية)، + تحديث `JobRunnerTests.cs` (41 اختبار في `Persistence.Tests` بعد التعديل، صفر Regression).
- **مشكلتين حقيقيتين اكتُشفتا بالمراجعة الفعلية (مش بتصديق تقرير جيميني الأول)**: (1) `Darhous.Notifications.Windows` معندوش يتبني خالص (`CS1061`) — التقرير الأول وصفها زورًا كـ"Timeout بيئي"؛ (2) Confidence اتحطت 8/10 من غير تشغيل اختبار واحد فعليًا. بعد المتابعة التصحيحية (`PHASE16_FIX_REQUIRED.md`)، جيميني صلّح المشكلة الحقيقية وأعاد التقرير بأرقام حقيقية — اتحقق منها بالبناء المستقل ومطابقة تمامًا.
- **Regression إضافي حقيقي اكتُشف وقت الدمج على مستوى الحل كله (مش وقت مراجعة جيميني)**: تعديل توقيع `JobRunner` كسر `DiscoveryTestBase.cs` (Phase 9) اللي بينشئ `JobRunner` مباشرة (مش عبر DI) بدون `IEventBus` — ظهر بس لما اتبنى الحل كله بعد الدمج، مش وقت بناء مشاريع Phase 16 لوحدها. اتصلح بإضافة `OutboxEventBus` حقيقي (نفس النمط المستخدم في الإنتاج) بدل حقن مباشر. **درس جديد**: تغيير توقيع Constructor لخدمة أساسية زي `JobRunner` لازم بحث شامل (`grep`) عن كل استخدام مباشر ليه في كل مشاريع الاختبار، مش بس المشاريع اللي عدّلها الـWorker المسؤول عن التغيير — هو نفسه مسؤول عن مشروعه بس، مش شايف باقي الحل.
- **قرار دمج ميكانيكي**: `M202609120005_Notifications` الأصلي كان بنفس رقم Migration اللي Phase 15 حجزه (`202609120005`)، اتنقّل لـ`202609120006`. ملفين تجريبيين اتضافوا بالغلط لآخر Commit (`test_reflect.cs`, `test_results.txt`) اتستبعدوا وقت السحب. حزمتين جديدتين (`Microsoft.Toolkit.Uwp.Notifications`, `System.Drawing.Common`) + `Moq` (للاختبارات) اتضافوا لـ`Directory.Packages.props` إضافيًا (Additive، بدون لمس أي حزمة موجودة).
- **دين تقني موثّق عمدًا (§142)**: جدول `notification_preferences` (DB Spec §75 — إعدادات Per-user لكل قناة) **لم يُبنَ في هذه المرحلة** — الفريق لم يُطلَب منه صراحة في البريف المختصر، ومفيش UI لإعدادات الإشعارات بعد. هيتحل لما تُبنى شاشة الإعدادات الفعلية.

### Phase 17 — Reports & Export `[x]`
*مرجع: §74 — نُفِّذت بالكامل بواسطة Codex في Worktree مستقل (`phase-17-codex`، مبني على Phase 14 مباشرة بالتوازي مع Phase 15/16)، رُوجِعت واندمجت بواسطة Claude*
- [x] مشروع جديد `Darhous.Archive.Modules.Reports`: `IReportExporter`/`CsvExporter`/`ExcelExporter`/`PdfExporter` + `IReportExportService`/`ReportExportService` (كتابة ذرية — فشل التصدير لا يستبدل الملف الهدف بنسخة جزئية).
- [x] **Excel**: عبر `DocumentFormat.OpenXml` (نفس المكتبة من Phase 10، مفيش مكتبة جديدة) — Header styling، Auto-sized columns محدودة، Frozen header row، Filters، تعقيم أحرف XML غير صالحة، فحص حدود Excel (16,384 عمود/1,048,575 صف). **قيد موثّق**: كل القيم بتتكتب كنص (مش Native date/number cells).
- [x] **CSV**: UTF-8 BOM، Quoting صحيح (فواصل/اقتباسات/أسطر جديدة)، **حماية من حقن الصيغ (Formula injection)** — أي خلية بتبدأ بـ`=`/`+`/`-`/`@`/Tab/CR بتاخد `'` قبلها.
- [x] **PDF**: عبر PDFsharp 6.2.4 (نفس المكتبة من Phase 15، مفيش مكتبة جديدة) — عناوين/عناوين فرعية، تكرار رؤوس الأعمدة عبر الصفحات، تقسيم أفقي للجداول العريضة (Bands بـ8 أعمدة)، تضمين خط Unicode (Windows platform fonts)، طبقة نص قابلة للتحديد/الاستخراج (اتأكدت باستخراج PdfPig فعلي لنص عربي وإنجليزي). **قيد موثّق صراحة**: الـLayout مش Bidi-aware حقيقي (`DrawString` البسيط)، مفيش مراجعة بصرية بشرية لشكل العربي.
- [x] **Print**: `IPdfPrintService`/`WindowsPrintProcessLauncher` — بيستخدم Verb `"print"` الافتراضي لملف PDF عبر `Process.Start(UseShellExecute=true)` خلف Abstraction قابل للـMock (`IPrintProcessLauncher`). **لم يُختبر مع طابعة حقيقية** (موثّق صراحة في القيود).
- [x] **Audit export**: `AuditReportService` بيصدّر صفحة النتائج المطلوبة من `IAuditQueryService` (Phase 4) — قرار مقصود: مش بيلف على كل صفحات الـAudit تلقائيًا (تحويل استعلام محدود لتصدير غير محدود بدون طلب صريح خطر).
- [x] **Saved views export**: **فجوة نطاق حقيقية في المواصفة الأصلية اتحلّت بقرار موثّق** — مفيش كيان `SavedView`/فلتر Explorer محفوظ فعليًا في الكود (Explorer الحالي بيعرض حالة لحظية بس)، فالتفسير المعتمد هو تصدير الصفوف الحالية المعروضة فعليًا في `ExplorerViewModel.Documents` (النطاق/الفلتر النشط) + الـBreadcrumb كعنوان التقرير. قرار سليم وموثّق بدل اختراع Schema جديد لمفهوم مش موجود.
- [x] **UI Hook**: قائمة "Reports" في `ExplorerWindow` (Excel/CSV/PDF/Print) — الـDialog التفاعلي واستدعاء الطباعة الفعلي عبر الـShell لم يُختبروا يدويًا (نفس القيد المتكرر من Phase 3/7/11).
- [x] **26 اختبار جديد**: 12 لـ`Darhous.Archive.Modules.Reports.Tests` (Exporters/ReportService/Print — بما فيها إعادة فتح XLSX بـ`OpenXmlValidator` بدون أخطاء، وإعادة فتح PDF واستخراج نص عربي/إنجليزي فعلي بـPdfPig) + 10 لـ`ExplorerViewModelTests` (Snapshot اختبار يثبت إن بس الصفوف المفلترة حاليًا هي اللي بتتبعت للـReports module، مش كل الجدول) — إجمالي Desktop.Tests بقى 22 (كان قبل تعديلات Phase 15 على نفس المشروع، مفيش تعارض ملفات — Phase 17 عدّل `ExplorerViewModelTests.cs` وPhase 15 عدّل `FakeServices.cs`/`PreviewViewModelTests.cs` بس).
- **فجوات موثّقة صراحة في `WORKER_REPORT.md` (نفس مستوى الصدق العالي بتاع Codex من Phase 15)**: Confidence **6.0/10 Production-readiness** (مفيش اختبار طابعة حقيقية، مفيش مراجعة بصرية للـUI/الـPDF العربي، مفيش اختبار حمل/تزامن، Excel نص فقط، الـAudit export محدود بصفحة واحدة، مفيش تنظيف لملفات الطباعة المؤقتة) مقابل **8.5/10 Happy-path**.
- **قرار دمج**: مفيش تصادم Packages فعلي — الحزم التلاتة اللي Codex استخدمها (`DocumentFormat.OpenXml`, `Microsoft.Extensions.DependencyInjection`, `PDFsharp`) كانت **موجودة بالفعل** في `Directory.Packages.props` (من Phase 10/1/15) بنفس الإصدارات بالظبط — مفيش أي تعديل مطلوب على الملف ده وقت الدمج. Claude ضاف المشروعين الجديدين للـslnx وكتب هذا القسم بعد بناء/اختبار مستقل مطابق للتقرير 100%.

### Phase 18 — Backup & Restore `[x]`
*مرجع: §75-77 — نُفِّذت بالكامل بواسطة Codex في Worktree مستقل (`phase-18-codex`)، رُوجِعت واندمجت بواسطة Claude*
- [x] Official Plugin: `Darhous.Backup.Local` (`IArchivePlugin`) يدعم Local folder/Another drive/USB عبر نفس Filesystem provider، و3 أنواع Backup (`metadata`/`full`/`configuration` — DB Spec §77).
- [x] **Backup Flow الكامل**: Validate destination + Write probe → SQLite Online Backup API (`SqliteConnection.BackupDatabase`) لكل الـ3 قواعد (archive/audit/search) — **قرار تصميمي سليم**: مش File-copy مباشر (بيتفادى نسخة ممزّقة تحت WAL) → نسخ الملفات المُدارة والإعدادات حسب النوع → SHA-256 لكل ملف + حجمه → Manifest JSON → تعبئة ZIP عبر ملف `.partial` جانب الوجهة → إعادة تسمية ذرية → إعادة فتح وتحقق كامل → Checksum للحزمة كلها → تسجيل `backup_history`.
- [x] **Restore Flow الكامل**: Validate (Manifest/المسارات/الأحجام/الـChecksums + سلامة SQLite) + رفض إصدار Schema أحدث → أخذ وتحقق Safety backup كاملة → تجهيز الأشجار البديلة مسبقًا → **Drain/Pause حقيقي لكل الـ3 طوابير كتابة SQLite داخل نفس العملية** (امتداد جديد على `SqliteWriteQueue` — Maintenance lease FIFO) → استعادة كل قاعدة عبر SQLite Online Backup للاتصالات الحية → تبديل الملفات/الإعدادات → تشغيل FluentMigrator → تحرير طابور الـSearch واستدعاء `ISearchIndexRebuilder` (Phase 8) → تحقق سلامة شامل → استئناف الطوابير. فشل داخل الجزء الحرج المبكر بيعمل Rollback تلقائي من الـSafety backup؛ فشل بعد تحرير طابور الـSearch بيرمي `RestoreRecoveryRequiredException` صراحةً ويقفل التطبيق (مفيش استمرار بحالة جزئية مقبولة).
- [x] **جدول `backup_history`** (DB Spec §76) عبر Migration جديدة، مع Indexes على `started_at`/`status`.
- [x] **Backup عبر Job حقيقي** (Phase 9's `JobRunner`) — مش على الـUI Thread مباشرة، مع Progress reporting.
- [x] **واجهة Explorer Admin-only** لتشغيل Backup/Restore، بتأكيد صريح ومسار الـSafety backup المحتفظ به معروض للمستخدم.
- [x] **8 اختبارات جديدة** تغطي كل أنواع الـPayload، محتوى الحزمة والـManifest، رفض حزمة تالفة *قبل* أي Safety backup أو تعديل للحالة الحالية، و**اختبار Restore حي كامل** بيثبت إن الـSafety package فعلاً بيحتفظ بالحالة قبل الاستعادة مباشرة، والطوابير بترجع تستقبل كتابة بعد الانتهاء.
- **تصادم Migration تاني اتصلح وقت الدمج (نفس الفئة بالظبط زي Phase 15/16)**: كودكس بنى الـWorktree بتاعه على `main` قبل ما Phase 16 تندمج، فاستخدم نفس رقم `202609120006` اللي Phase 16 حجزته — اتنقّل لـ`202609120007`.
- **فجوات موثّقة صراحة في `WORKER_REPORT.md` (نفس مستوى الصدق العالي المعتاد من Codex)**: Confidence **5.5/10 Production-readiness** (لا اختبار Power-loss حقيقي، لا اختبار USB فعلي، لا اختبار Disk-full، لا استعادة من Schema قديم فعليًا، الحزم غير موقّعة/غير مشفّرة، لا Retention/Rotation policy) مقابل **8.0/10 Happy-path**. **ملاحظة صريحة مهمة من التقرير نفسه**: "Stop writes" بمعنى حقيقي محدود — بيوقف طوابير الكتابة الداخلية لنفس الـProcess بس، مش أي Process/Handle خارجي تاني (DB Browser، برنامج Antivirus، إلخ) — صدق معماري حقيقي مش ادّعاء Atomicity زيادة عن الواقع.
- **تحقق مستقل مطابق 100% للتقرير**: 8/8 `Backup.Local.Tests`، 42/42 `Persistence.Tests`، Release build نظيف. `Desktop.Tests` أظهر فشل واحد (Gate أداء الـ100k صف) عند التشغيل بالتوازي مع فحص Gemini الشامل على نفس الجهاز — نجح 1/1 لما اتشغّل لوحده، نفس فئة الحساسية البيئية الموثّقة (Flaky #1).

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
2026-09-12 — Phase 4 (Audit) مكتملة: مشروع جديد Darhous.Archive.Audit (منفصل عن Persistence حسب هيكل الحل)، Migration audit_events (DB Spec §82)، BufferedAuditService (Critical actions §106 تُكتب فورًا، الباقي Buffered كل 2 ثانية/200 عنصر عبر Channel)، IAuditQueryService (فلترة+Pagination). ربطنا Login/Logout الفعليين في AuthenticationService بالـAudit (خارج الـUnitOfWork لأن audit.db وarchive.db قاعدتين منفصلتين). search/sort/filter/add/move/delete لسه مش مربوطين لأن المزايا نفسها (Search/Documents/Folders) لسه مبنيتش — هيتربطوا مع كل Phase. 5 اختبار جديد، 97/97 على مستوى الحل. **CHECKPOINT بطلب Ahmed — الشغل ده لسه مش متعمّله commit/push وقت كتابة السطر ده.**
2026-09-12 — Ahmed طلب checkpoint (اتعمل، commit `5b04a37`) + قاعدة اقتصاد Context لـMulti-Agent Orchestration (§0.1.1، commit `f606970`) بعد ملاحظة استهلاك توكينز عالي (رد Codex كان فيه سطر ~125K توكِن من tool logs داخلية). Phase 5 (Document Core) بعدها: راجعناها مع Codex (Level 2) قبل التنفيذ — قرار: نقل الملف الفعلي قبل commit الـDB (orphan file أهون من DB record بيشاور على ملف مش موجود)، ورفضنا اقتراحه ببناء Import Operation Journal كامل (over-engineering لتطبيق مستخدم واحد). بنينا: Darhous.Archive.Modules.Documents (IFileStorageService/FileStorageService/DocumentService)، Migrations جديدة (file_path_normalized، number_sequences، recycle_bin_entries)، ArabicNormalization اتنقلت من Phase 8 المخطَّط لأنها لازمة من دلوقتي (title_normalized NOT NULL). ربطنا كل أفعال المستندات بالـAudit (حل دين Phase 4). 16 اختبار جديد، 113/113 على مستوى الحل. جاري commit+push، بعدها Phase 6.
2026-09-12 — Phase 6 (Folder System) مكتملة: مشروع جديد Darhous.Archive.Modules.Folders (IFolderService/FolderService)، Migration operation_snapshots (DB Spec §142)، IBulkOperationService/BulkOperationService (في Modules.Documents — Bulk Move + Undo خلال 30 ثانية). منع دورات (Cycle) عند نقل فولدر داخل أحد فولدراته الفرعية، منع تكرار الاسم بين الإخوة. اكتشفنا وأصلحنا Bug أداء ذاتيًا (مش من مراجعة خارجية): أول تنفيذ لحذف فولدر مع نقل محتواه استخدم ListAsync() (سحب الجدول كله!) بدل استعلام مفلتر — أضفنا IDocumentRepository.ListByFolderAsync يستخدم الفهرس الموجود. 18 اختبار جديد، 131/131 على مستوى الحل. جاري commit+push، بعدها Phase 7.
2026-09-12 — Phase 7 (Archive Explorer UI) مكتملة: أول شاشة رئيسية كاملة بعد تسجيل الدخول — ExplorerWindow/ExplorerViewModel حلّت محل WelcomePlaceholderWindow (اتحذفت بالكامل). Sidebar (TreeView متداخل من IFolderService)، Document list (ListView+GridView مع Virtualization كامل)، Search strip (Live filter client-side substring + ArabicNormalization من Phase 5 — الـFTS الحقيقي لسه Phase 8)، Breadcrumb، Bulk actions (نقل/حذف جماعي مربوطين بـIBulkOperationService/IDocumentService الموجودين من Phase 5-6)، Density modes (Compact/Standard/Comfortable)، Theme (نفس ThemeManager من Phase 3). حل دين Phase 6 القديم: created_by/updated_by بقت بتاخد principal.UserId الحقيقي بدل NULL. اكتشفنا Bug في الـTest fakes بس (مش الكود الحقيقي): Dictionary<Guid?,T> بترمي ArgumentNullException وقت التشغيل رغم إنها بس تحذير CS8714 وقت الترجمة — الإصلاح كان في FakeFolderService فقط (Guid.Empty كـsentinel)، الكود الحقيقي (FolderRepository/SQL) مش متأثر. مشروع اختبار جديد Darhous.Archive.Desktop.Tests (12 اختبار)، من ضمنهم Gate الأداء الإلزامي (§41): تحميل + فلترة 100,000 مستند وهمي في أقل من 2000ms — ناجح (نصف الـData layer فقط مؤتمت؛ نصف الـRendering البصري يحتاج تأكيد Ahmed يدويًا، زي Login UI في Phase 3). 143/143 اختبار على مستوى الحل، Release build نظيف (0 warnings/errors). جاري commit+push، بعدها Phase 8 (Search Foundation — أول Milestone استخدام يومي فعلي عند اكتماله).
2026-09-12 — Phase 8 (Search Foundation) مكتملة: راجعنا التصميم مع AgentFlow (Level 2) قبل التنفيذ حول آلية الفهرسة التزايدية وأسلوب الـRebuild، وفي الحالتين بسّطنا/غيّرنا توصية الـReview بقرارنا الخاص بعد الموازنة (تفاصيل كاملة + الأسباب في قسم Phase 8 تحت). مشروع جديد Darhous.Search.SqliteFts (أول مشروع تحت src/OfficialPlugins/ حسب Implementation Plan §5): Migration لـsearch.db (FTS5 documents_fts + search_documents shadow metadata + search_state checkpoint)، SearchReconciliationService (Sweep دوري كل 5 ثواني يقارن documents.updated_at بدل Event Bus — قرارنا تبسيطًا، الفهرس ix_documents_updated_at من Phase 2 كان مبنيًا بالظبط لكده + Full Sweep كل ~60 ثانية يلتقط Permanent Delete)، SearchIndexWriter/FtsQueryService (BM25 ranking بأوزان title=10/file_name=8/metadata=5/body=1، Snippets، Filters، Pagination، Sort)، SearchIndexRebuilder (Rebuild منطقي DROP+CREATE عبر نفس اتصال الكتابة بدل تبديل ملف فعلي — رفضنا اقتراح الـReview بـFile.Replace لتجنّب مخاطر Windows الموثقة في رده نفسه)، SearchAvailability (Corruption guard يمنع أي فشل في search.db من التأثير على باقي التطبيق). Search Audit استخدم AuditAction.Search/AuditActionCategory.Search الجاهزين من Phase 4. فجوة نطاق موثّقة عن قصد: أعمدة metadata/body في الـSchema جاهزة لكن فاضية فعليًا (لا يوجد Custom Fields module ولا OCR pipeline بعد). قرار Scope: بنينا الـEngine كامل ومسجَّل في DI وشغّال كـBackground Service، لكن أجّلنا ربطه بمربع البحث الحالي في Explorer (Phase 7) لأن الـDebounce الحقيقي (250ms) بيكسر افتراض التزامن اللي اختبارات Phase 7 مبنية عليه — قرار مفصَّل بدل تسرّع بربط بيكسر شغل مُختبر. مشروع اختبار جديد Darhous.Search.SqliteFts.Tests (17 اختبار). 160/160 اختبار على مستوى الحل، Release build نظيف. جاري commit+push، بعدها Phase 9 (Automatic Computer Discovery).
2026-09-12 — Phase 9 (Automatic Computer Discovery) مكتملة: بنينا Job System الحقيقي لأول مرة (JobRepository/JobRunner/Retry بـExponential backoff/Crash Recovery — الـinterfaces كانت من Phase 1 والجدول من Phase 2 بس مفيش Scheduler كان شغّال قبل كده). مشروع جديد Darhous.Archive.Modules.Discovery: DiscoveryScanner (Stack-based traversal، Metadata-only، بيمنع Reparse Points/Junction loops، بيتحمل أخطاء صلاحيات مجلد واحد من غير ما يوقف الفحص كله)، ExclusionService (استثناءات تقنية idempotent + استثناءات مستخدم)، DriveEnumerator، WatchFolderService، DiscoveryOrchestrator، وتلات أنواع Jobs: DiscoveryScanJob (يكتشف ← يجدول FileIndexJob لكل ملف جديد)، FileIndexJob (بينادي DocumentService.AddDocumentAsync الموجودة من Phase 5 مباشرة — الـDedup بالـSHA-256 already built)، MissingFileReconcileJob. FileSystemWatcherService (فهرسة شبه فورية، Wait-until-write-complete عبر فتح Exclusive بدل Sleep ثابت) + DiscoveryReconciliationService (Hourly/Daily). نفّذنا قرار SAD §46.9 المعتمد: OnboardingWindow تظهر مرة واحدة بعد أول Login، تخلي المستخدم يختار مجلدات البداية بدل فحص الكمبيوتر بالكامل تلقائيًا. فجوات نطاق موثّقة: موافقة صريحة لفحص قرص Removable لسه بدون UI، Sources View مؤجلة. 29 اختبار جديد (6 JobRunnerTests + مشروع جديد Darhous.Archive.Modules.Discovery.Tests بـ23 اختبار من ضمنهم Pipeline كامل End-to-end). 189/189 اختبار على مستوى الحل، Release build نظيف. جاري commit+push، بعدها Phase 10 (Importers).
2026-09-12 — Phase 10 (Importers) مكتملة: أضفنا 4 مكتبات NuGet (PdfPig, DocumentFormat.OpenXml, MimeKit, MsgReader) ومشروع جديد Darhous.Archive.Modules.Importers. PdfContentExtractor (كشف Text Layer بعتبة 20 حرف غير-فراغ، PageCount، Metadata؛ عدم وجود طبقة نص = needs_ocr — القرار الصح بس مش تنفيذ OCR فعلي، ده Phase 15 المخصصة له)، OpenXmlContentExtractor (DOCX/XLSX/PPTX، PageCount=null لـWord عمدًا لأنه مش مشتق من الـXML أصلًا)، EmlContentExtractor (MimeKit)، MsgContentExtractor (MsgReader لصيغة Outlook الثنائية). Legacy DOC/XLS/PPT عبر Worker/Interop مؤجل بالكامل — Worker Infrastructure نفسها (Phase 13) لسه مبنية، قيد ترتيب حقيقي مش تجنّب Scope؛ بيتحط content_extraction_status='unsupported'. TextExtractionSweepService بنفس نمط الـReconciliation Sweep من Phase 8/9 (مش Job منفصل لكل نسخة، تجنّبًا لاعتماد Cross-project). فجوة نطاق موثّقة: النص المُستخرَج مش متخزن دائمًا ولا متوصّل بـSearch's documents_fts.body لسه (Coupling إضافي بين Modules.Documents/Importers/Search اتأجل). 15 اختبار جديد (مشروع Darhous.Archive.Modules.Importers.Tests — PDFs مبنية يدويًا بـxref صحيح بالبايت، DOCX/XLSX/PPTX حقيقية مُنشأة برمجيًا وقت الاختبار). 204/204 اختبار على مستوى الحل، Release build نظيف. جاري commit+push، بعدها Phase 11 (Preview).
2026-09-12 — Phase 11 (Preview) مكتملة: بنينا Preview pane حقيقي في Explorer (كان Placeholder من Phase 7). PDF Preview عبر Microsoft.Web.WebView2 (محرك Chromium المدمج بيعرض PDF محلي مباشرة بدون سيرفر)، Office preview fallback (رسالة + زرار "فتح الملف" بالتطبيق الافتراضي)، Metadata tab (من Document+DocumentVersion الموجودين، فيها نتيجة استخراج النص من Phase 10 مباشرة)، Versions tab وActivity tab (كلاهما Lazy-loaded — بيتحمّلوا بس أول مرة الـTab يتفتح). اكتشفنا فجوة حقيقية في العقد: AuditQuery (من Phase 4) معندهاش فلتر EntityUid رغم إن الفهرس (ix_audit_events_entity_timestamp) كان موجود جاهز — أضفناه كحقل جديد بدون Breaking change (كل الاستدعاءات القديمة بتستخدم Named arguments). طبّقنا قاعدة §60: فتح Preview يسجل preview_document (كان موجود جاهز من Phase 4) مرة واحدة بس لكل مستند مختلف، مش open_document. الـPreview مستقل تمامًا عن Multi-select الخاص بالـBulk actions — بيتفعّل بس مع تحديد مستند واحد بالظبط (نفس تقليد Windows Explorer). فجوة بيئة موثّقة: WebView2 Runtime نفسه Dependency على مستوى النظام، والعرض البصري الفعلي محتاج تأكيد Ahmed يدويًا (نفس القيد المتكرر من Phase 3/7). 9 اختبارات جديدة (PreviewViewModelTests). 213/213 اختبار على مستوى الحل، Release build نظيف. جاري commit+push، بعدها Phase 12 (Plugin Platform).
2026-09-12 — Phase 12 (Plugin Platform) مكتملة: بنينا Plugin Host حقيقي بعزل In-Process (Plugin SDK §80؛ Out-of-Process مؤجل لـPhase 13 عمدًا لأن Worker Infrastructure لسه مش موجودة). مشروع جديد منفصل تمامًا Darhous.Archive.PluginSdk (بدون اعتماد على Persistence/Security/Modules — هو حد الثقة الفعلي مع كود Third-party): IArchivePlugin، PluginManifest، PluginLifecycleState (12 حالة)، PluginTrustLevel، PluginPermission. مشروع Darhous.Archive.Modules.Plugins: SemVer/SemVerRange، Pipeline تحقق كامل من الحزمة (.archiveplugin = ZIP فيه manifest.json + checksums.json + signature.p7s + bin/) — Manifest validation → Checksum verification → CMS/PKCS#7 signature verification (System.Security.Cryptography.Pkcs.SignedCms، فصل حقيقي بين صحة التوقيع رياضيًا وثقة الموقِّع في Trust store منفصل على القرص) → التوافق مع إصدار Core. تثبيت ذري متدرّج: Staging مؤقت → Dry-load health check → نقل ذري لمجلد نسخة → تسجيل DB بصلاحيات غير-ممنوحة. PluginLifecycleManager: Install/Enable/Disable/Update/Rollback/Remove، فحص Circular/Unmet dependencies، Crash-loop protection (3 أعطال/10 دقائق → Quarantined). مشكلة حقيقية اتكتشفت واتحلّت أثناء التنفيذ: AssemblyLoadContext.LoadFromAssemblyPath بيسيب ملف الـDLL مقفول على Windows طول عمر الـContext حتى بعد Unload() (Unload نفسه Best-effort/GC-driven) — بيمنع Remove/Update من حذف الملفات. الحل: تحميل عبر LoadFromStream من بايتات مقروءة بدل LoadFromAssemblyPath. الأثر الجانبي: Assembly المحمّل كده بيرجّع Assembly.Location فاضي — أضفنا IPluginEnvironment.InstallDirectory كحقل SDK جديد عشان أي Plugin يعرف مكان تثبيته بشكل موثوق بدل الاعتماد على Assembly.Location. أضفنا PluginPlatformOptions (نفس نمط DocumentStorageOptions) عشان الاختبارات ما تلمسش %ProgramData% الحقيقي. مشروع اختبار Fixture حقيقي منفصل Darhous.TestPlugin (IArchivePlugin حقيقي مُجمَّع، مش Mock) بيتحزم فعليًا وقت الاختبار ويتوقّع بشهادة موقّعة ذاتيًا حقيقية. وصلنا AddPluginsModule في App.xaml.cs composition root — الخدمات الرسمية المكشوفة فعليًا (IDocumentService/IFolderService/IAuditService/IEventBus/IClock) هي بس اللي موجودة في DI اليوم من قائمة §23 الكاملة؛ الباقي (ITagService/ISearchService/IUserContext/IJobService/INotificationService/ISettingsService/IHealthService) هيتوصل تباعًا مع بناء تلك الخدمات في مراحل لاحقة. رفعنا إصدار System.Security.Cryptography.Pkcs المركزي لـ10.0.11 (MsgReader من Phase 10 كان محتاج إصدار أحدث من اللي كان محدد). 11 اختبار جديد (Darhous.Archive.Modules.Plugins.Tests) تغطي مسارات الرفض الأمنية ومسارات النجاح الكاملة والـCrash-loop quarantine. 224/224 اختبار على مستوى الحل. جاري Release build/test نهائي ثم commit+push، بعدها Phase 13 (Worker Infrastructure).
2026-09-12 — Phase 13 (Worker Infrastructure) مكتملة: Named Pipes IPC (رسالة JSON طول-مُسبَق UTF-8: protocolVersion/messageType/requestId/correlationId/payload)، 7 خطوات Handshake (protocol → worker id → version → session token → capabilities → health → ready)، WorkerProcessSupervisor (سياسة إعادة تشغيل فورية/5ث/30ث، Crash-loop 3 أعطال/10 دقائق → Quarantined)، LargeDataReference/ManagedTempPathValidator لنقل الملفات الكبيرة خارج الأنبوب. اتنفّذت بالتوازي (Codex+Gemini، Worktrees منفصلة، عقد وسيط اتحدد كتابيًا مسبقًا في البريف). Bug اتكتشف واتصلح: تصميم Gemini الأول عامل أي خروج Process (حتى exit code 0 النظيف) كـ"Crash" — اتصلح بعد المراجعة. 224+ اختبار جديد بين المشروعين. جاري commit+push، بعدها Phase 14.
2026-09-12 — Phase 14 (Scanner) مكتملة: NAPS2.Sdk Worker حقيقي (`Darhous.Archive.Scanner.Worker`) + Plugin (`Darhous.Archive.Modules.Scanner`) بيستخدم `WorkerProcessSupervisor` من Phase 13 كأول مستهلك حقيقي له. اتنفّذت بالتوازي (Codex=Worker/NAPS2، Gemini=Plugin/Provider/DB) — عقد `scan.request`/`scan.result` اتخترع بشكل مستقل من الطرفين فاختلف شكله (`LargeDataReference`/`ScanFailure` عند كودكس مقابل `ScanResultPayload` عند جيميني)، اتكشف بمراجعة الكود الفعلي (مش تقرير جيميني اللي قيّم نفسه 8/10) واتصلح بمطابقة يدوية (`PHASE14_RECONCILE.md`). **درس CI حرج اتكشف متأخر (2026-09-12)**: `WorkerProcessSupervisorTests.cs`/`ScannerPluginTests.cs` بيثبّتوا مسار الـFixture على `"Debug"` بدل الاعتماد الديناميكي على الـConfiguration الفعلي — دي شغّالة محليًا (Debug الافتراضي) بس فشلت في CI (بيبني Release دايمًا) لـ4 Push متتالية، اتصلحت بـCommit `e3d6fa5` (استخراج الـConfiguration من `AppContext.BaseDirectory`). 31 اختبار جديد. 252/252 على مستوى الحل، Release build نظيف، CI Success مؤكَّد بـ`gh run list`. جاري commit+tag(`phase-14`)+push، بعدها Phase 15.
2026-09-13 — Phase 15 (OCR) مكتملة: نُفِّذت بالكامل بواسطة Codex في Worktree مستقل (`phase-15-codex`)، رُوجِعت واندمجت. Worker `Darhous.Archive.Ocr.Worker` (Tesseract 5.2.0) + `Darhous.Archive.Modules.Ocr` (Plugin/Provider/Sweep) + طبقة نص شفافة فوق صفحات الـPDF الأصلية (PDFsharp 6.2.4، alpha=0، مفيش تعديل بصري). Stretch goal (ربط `documents_fts.body` بالنص المستخرج) اتعمل فعليًا مش مؤجل، مع اختبار End-to-end حقيقي. **تصادم Migration حقيقي اتكشف واتصلح وقت الدمج**: كودكس وجيميني (Phase 14) استخدموا بالصدفة نفس رقم `202609120004` لأنهم اشتغلوا بالتوازي على نفس التاريخ — Phase 15's migration اتنقلت لـ`202609120005`. تقرير Codex من أدق التقارير لحد الآن: Confidence **4/10 Production-ready** (لا يوجد تشغيل OCR حقيقي بـ`.traineddata`، لا حدود Timeout/Disk quota، لا Claim/Lease) مقابل **8/10 Happy-path** — صدق حقيقي مش مبالغة. 31 اختبار جديد. **300/300 اختبار على مستوى الحل** (اتحقق بتشغيل كل مشروع اختبار لوحده Release بعد ما تشغيل الحل كله دفعة واحدة عَلَّق فعليًا — نفس فئة الحساسية البيئية الموثّقة من قبل، اتقفلت العملية المعلّقة وأُعيد التشغيل مشروع-مشروع بنجاح). Release build نظيف 0 تحذيرات/أخطاء. جاري commit+tag(`phase-15`)+push، بعدها مراجعة تصحيح Phase 16 من جيميني ثم Phase 17 (Codex شغّال عليها بالتوازي فعلًا).
2026-09-13 — راجعت Phase 16 (Notifications) بعد تصحيح جيميني: بنى `Darhous.Notifications.Windows` فعليًا بتغيير TFM لـ`net10.0-windows10.0.17763.0`. تحققت بنفسي (مش تصديق التقرير): الأربع مشاريع بتتبني نظيف، 44/44 اختبار بيعدي فعليًا (`Notifications.Tests`=1، `Windows.Tests`=1، `NotificationsTelegram.Tests`=1، `Persistence.Tests`=41 — مفيش Regression من تعديل `JobRunner.cs`). لسه معلّقة على معالجة تصادم Migration محتمل (`202609120005` نفسه المُستخدَم لـPhase 15) وتنظيف ملفات تجريبية (`test_reflect.cs`/`test_results.txt`) — دمج فعلي مؤجل لجلسة لاحقة.
2026-09-13 — Phase 17 (Reports & Export) مكتملة: نُفِّذت بالكامل بواسطة Codex في Worktree مستقل (`phase-17-codex`، مبني على Phase 14 بالتوازي مع Phase 15/16). `Darhous.Archive.Modules.Reports` — Excel (`DocumentFormat.OpenXml`)، CSV (مع حماية Formula injection)، PDF (`PDFsharp`، طبقة نص عربي/إنجليزي حقيقية اتأكدت بـPdfPig)، Print (Shell verb خلف Abstraction قابل للـMock)، Audit export (صفحة محدودة من `IAuditQueryService`)، Saved views export (تفسير موثّق: تصدير الصفوف الحالية في `ExplorerViewModel.Documents` لعدم وجود كيان SavedView فعلي). **صفر تصادم حزم فعلي وقت الدمج** — الحزم التلاتة (OpenXml/DI/PDFsharp) كانت موجودة بالفعل بنفس الإصدارات من Phase 1/10/15. تقرير Codex بنفس مستوى الصدق العالي: Confidence **6.0/10 Production-readiness** (مفيش اختبار طابعة حقيقية، مفيش مراجعة بصرية Arabic PDF) مقابل **8.5/10 Happy-path**. 26 اختبار جديد. **تحقق مستقل مطابق 100% للتقرير**: 12/12 `Reports.Tests`، 22/22 `Desktop.Tests`، Release build نظيف. جاري commit+tag(`phase-17`)+push، بعدها استكمال دمج Phase 16 والانتقال لـPhase 18 (Backup & Restore).
2026-09-13 — Phase 16 (Notifications) اندمجت أخيرًا: `Darhous.Archive.Modules.Notifications` (In-app)، `Darhous.Notifications.Windows` (Toast — بعد تصحيح حقيقي: `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 بدل `CommunityToolkit.WinUI.Notifications` غير المتوافق)، `Darhous.Archive.Modules.NotificationsTelegram` (Outbox-backed). الربط الحقيقي الوحيد End-to-end: `JobRunner.cs` بينشر `JobFailedEvent` عند فشل نهائي. **Regression إضافي حقيقي اكتُشف وقت بناء الحل كله بعد الدمج (مش وقت مراجعة مشاريع Phase 16 لوحدها)**: `DiscoveryTestBase.cs` (Phase 9) بينشئ `JobRunner` مباشرة بدون `IEventBus` — اتصلح بـ`OutboxEventBus` حقيقي. تصادم Migration تاني اتصلح (`202609120005`→`202609120006`، بعد ما Phase 15 حجزت نفس الرقم). **316/316 اختبار على مستوى الحل كله بعد الدمج** (كل مشروع لوحده، Release). **قرار مهم اتخده Ahmed بعد الجولة دي**: جيميني هيوقف تنفيذ مراحل مستقلة (3 مراحل من أصل 4 احتاجت تصحيح حقيقي بعد تقرير ذاتي متفائل زيادة عن اللزوم — راجع [[feedback_orchestration_worktrees]])، ودوره بقى فحص شامل (Audit/QA) للمشروع كله بدل التنفيذ، عشان نستفيد من قدرته على المراجعة الميكانيكية الكبيرة من غير ما نستهلك توكينز Claude في نفس الشغل. Codex هو اللي هيكمل تنفيذ المراحل المستقلة (Phase 18 بدأت بالتوازي). جاري commit+tag(`phase-16`)+push.
```

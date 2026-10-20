import { useEffect, useState } from "react";
import Link from "@/lib/rr";
import { useRouter } from "@/lib/rr";
import { motion } from "framer-motion";
import { BookOpenCheck, ChartSpline, Clock3, FilePlus2, GraduationCap, Layers, Play, RotateCcw, Sparkles, Trash2, Users } from "lucide-react";
import { PageIn, Reveal, Tilt } from "@/components/fx";
import { ProviderModal } from "@/components/ProviderSetup";
import { Badge, Button, EmptyState, ErrorBanner, Skeleton, api, cn } from "@/components/ui";
export function DashboardClient({
  user
}) {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [teacherRows, setTeacherRows] = useState([]);
  const [studentRows, setStudentRows] = useState([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [deleting, setDeleting] = useState(null);
  useEffect(() => {
    let alive = true;
    api("/api/assessments").then(res => {
      if (!alive) return;
      if (user.role === "teacher") setTeacherRows(res.assessments);else setStudentRows(res.assessments);
    }).catch(e => alive && setError(e.message)).finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, [user.role]);
  async function goAI() {
    try {
      const p = await api("/api/ai/provider");
      if (p.configured) router.push("/ai");else setModalOpen(true);
    } catch {
      setModalOpen(true);
    }
  }
  async function remove(id) {
    if (!confirm("Delete this assessment and all its attempts?")) return;
    setDeleting(id);
    try {
      await api(`/api/assessments/${id}`, {
        method: "DELETE"
      });
      setTeacherRows(rows => rows.filter(r => r.id !== id));
    } catch (e) {
      setError(e.message);
    } finally {
      setDeleting(null);
    }
  }
  return <PageIn>
      {/* header */}
      <div className="flex flex-col gap-6 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="label-caps mb-2 flex items-center gap-2">
            {user.role === "teacher" ? <BookOpenCheck className="h-3.5 w-3.5 text-acid" /> : <GraduationCap className="h-3.5 w-3.5 text-cyan2" />}
            {user.role === "teacher" ? "Teacher console" : "Student lounge"}
          </p>
          <h1 className="font-display text-4xl font-bold tracking-tight md:text-5xl">
            {greeting()}, {user.name.split(" ")[0]}.
          </h1>
          <p className="mt-2 max-w-lg text-sm leading-relaxed text-dim">
            {user.role === "teacher" ? "Compose new assessments, watch submissions land, and let the AI draft when you're short on prep time." : "Pick an assessment, beat the clock, then study the report your performance paints."}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {user.role === "teacher" && <Link href="/assessments/new" className="btn btn-ghost btn-md">
              <FilePlus2 className="h-4 w-4" /> Compose manually
            </Link>}
          <Button onClick={goAI} icon={<Sparkles className="h-4 w-4" />}>
            Generate assessment using AI
          </Button>
        </div>
      </div>

      <div className="mt-6">
        <ErrorBanner message={error} />
      </div>

      {loading ? <div className="mt-10 space-y-4">
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-28" />)}
          </div>
          {[0, 1, 2].map(i => <Skeleton key={i} className="h-32" />)}
        </div> : user.role === "teacher" ? <TeacherView rows={teacherRows} onDelete={remove} deleting={deleting} /> : <StudentView rows={studentRows} />}

      <ProviderModal open={modalOpen} onClose={() => setModalOpen(false)} onSaved={() => router.push("/ai")} />
    </PageIn>;
}
function greeting() {
  const h = new Date().getHours();
  if (h < 12) return "Good morning";
  if (h < 18) return "Good afternoon";
  return "Good evening";
}

/* ============================ TEACHER ============================ */
function TeacherView({
  rows,
  onDelete,
  deleting
}) {
  const totalSubmissions = rows.reduce((s, r) => s + r.submissionCount, 0);
  const avgs = rows.filter(r => r.avgScore !== null).map(r => r.avgScore);
  const classAvg = avgs.length ? Math.round(avgs.reduce((s, v) => s + v, 0) / avgs.length) : null;
  const stats = [{
    icon: <Layers className="h-4 w-4 text-acid" />,
    label: "Assessments",
    value: String(rows.length)
  }, {
    icon: <Users className="h-4 w-4 text-cyan2" />,
    label: "Submissions",
    value: String(totalSubmissions)
  }, {
    icon: <ChartSpline className="h-4 w-4 text-vio" />,
    label: "Class average",
    value: classAvg === null ? "—" : `${classAvg}%`
  }, {
    icon: <Sparkles className="h-4 w-4 text-mint" />,
    label: "AI authored",
    value: String(rows.filter(r => r.isAiGenerated).length)
  }];
  return <>
      <div className="mt-10 grid grid-cols-2 gap-4 lg:grid-cols-4">
        {stats.map((s, i) => <Reveal key={s.label} delay={i * 0.06}>
            <div className="glass card-hover rounded-3xl p-5">
              <div className="flex items-center justify-between">
                <span className="label-caps">{s.label}</span>
                {s.icon}
              </div>
              <p className="font-display mt-4 text-3xl font-bold tracking-tight">{s.value}</p>
            </div>
          </Reveal>)}
      </div>

      {rows.length === 0 ? <div className="mt-10">
          <EmptyState icon={<FilePlus2 className="h-6 w-6" />} title="No assessments yet" body="Compose your first exam by hand, or let the AI research a topic and draft it for you." action={<Link href="/assessments/new" className="btn btn-primary btn-md">
                <FilePlus2 className="h-4 w-4" /> Create the first one
              </Link>} />
        </div> : <div className="mt-10 grid gap-4 md:grid-cols-2">
          {rows.map((r, i) => <Reveal key={r.id} delay={Math.min(i * 0.06, 0.4)}>
              <Tilt max={4}>
                <div onClick={() => window.location.href = `/assessments/${r.id}`} className="glass card-hover group relative cursor-pointer overflow-hidden rounded-3xl p-6">
                  <div className="flex items-start justify-between gap-4">
                    <div className="min-w-0">
                      <div className="mb-2 flex flex-wrap items-center gap-2">
                        <Badge tone="vio">{r.subject}</Badge>
                        {r.isAiGenerated && <Badge tone="mint"><Sparkles className="h-3 w-3" /> AI</Badge>}
                      </div>
                      <h3 className="font-display truncate text-lg font-bold transition-colors group-hover:text-acid">
                        {r.title}
                      </h3>
                      <p className="mt-1 line-clamp-2 text-[13px] leading-relaxed text-dim">
                        {r.description || "No description provided."}
                      </p>
                    </div>
                    <button onClick={e => {
                e.stopPropagation();
                onDelete(r.id);
              }} disabled={deleting === r.id} className="grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-white/8 text-faint transition-all hover:border-rose2/40 hover:bg-rose2/10 hover:text-rose2" title="Delete">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                  <div className="mt-5 flex items-center gap-5 border-t border-white/6 pt-4 text-[12px] text-dim">
                    <span className="flex items-center gap-1.5"><Layers className="h-3.5 w-3.5" /> {r.questionCount} questions</span>
                    <span className="flex items-center gap-1.5"><Clock3 className="h-3.5 w-3.5" /> {r.durationMinutes} min</span>
                    <span className="flex items-center gap-1.5"><Users className="h-3.5 w-3.5" /> {r.submissionCount} subs</span>
                    {r.avgScore !== null && <span className={cn("ml-auto font-display font-bold", r.avgScore >= 70 ? "text-acid" : r.avgScore >= 45 ? "text-cyan2" : "text-rose2")}>
                        {r.avgScore}% avg
                      </span>}
                  </div>
                </div>
              </Tilt>
            </Reveal>)}
        </div>}
    </>;
}

/* ============================ STUDENT ============================ */
function StudentView({
  rows
}) {
  const router = useRouter();
  const attempted = rows.filter(r => r.attemptCount > 0).length;
  const bests = rows.filter(r => r.bestScore !== null).map(r => r.bestScore);
  const avg = bests.length ? Math.round(bests.reduce((s, v) => s + v, 0) / bests.length) : null;
  async function start(r) {
    if (r.inProgressAttemptId) {
      router.push(`/exam/${r.inProgressAttemptId}`);
      return;
    }
    const res = await api("/api/attempts", {
      method: "POST",
      body: JSON.stringify({
        assessmentId: r.id
      })
    });
    router.push(`/exam/${res.attemptId}`);
  }
  const stats = [{
    icon: <Layers className="h-4 w-4 text-acid" />,
    label: "Open assessments",
    value: String(rows.length)
  }, {
    icon: <GraduationCap className="h-4 w-4 text-cyan2" />,
    label: "Attempted",
    value: String(attempted)
  }, {
    icon: <ChartSpline className="h-4 w-4 text-vio" />,
    label: "Average score",
    value: avg === null ? "—" : `${avg}%`
  }, {
    icon: <Clock3 className="h-4 w-4 text-rose2" />,
    label: "In progress",
    value: String(rows.filter(r => r.inProgressAttemptId).length)
  }];
  return <>
      <div className="mt-10 grid grid-cols-2 gap-4 lg:grid-cols-4">
        {stats.map((s, i) => <Reveal key={s.label} delay={i * 0.06}>
            <div className="glass card-hover rounded-3xl p-5">
              <div className="flex items-center justify-between">
                <span className="label-caps">{s.label}</span>
                {s.icon}
              </div>
              <p className="font-display mt-4 text-3xl font-bold tracking-tight">{s.value}</p>
            </div>
          </Reveal>)}
      </div>

      {rows.length === 0 ? <div className="mt-10">
          <EmptyState icon={<BookOpenCheck className="h-6 w-6" />} title="Nothing assigned yet" body="When your teachers publish assessments they appear here. Meanwhile, generate a practice exam with AI and test yourself." />
        </div> : <div className="mt-10 space-y-4">
          {rows.map((r, i) => <motion.div key={r.id} initial={{
        opacity: 0,
        y: 20
      }} animate={{
        opacity: 1,
        y: 0
      }} transition={{
        delay: Math.min(i * 0.05, 0.35),
        duration: 0.6,
        ease: [0.22, 1, 0.36, 1]
      }}>
              <div className="glass card-hover flex flex-col gap-5 rounded-3xl p-6 md:flex-row md:items-center">
                <div className="min-w-0 flex-1">
                  <div className="mb-2 flex flex-wrap items-center gap-2">
                    <Badge tone="cyan">{r.subject}</Badge>
                    {r.isAiGenerated && <Badge tone="mint"><Sparkles className="h-3 w-3" /> AI</Badge>}
                    <span className="text-[12px] text-faint">by {r.teacherName}</span>
                  </div>
                  <h3 className="font-display text-lg font-bold">{r.title}</h3>
                  <p className="mt-1 line-clamp-1 text-[13px] text-dim">{r.description}</p>
                  <div className="mt-3 flex items-center gap-4 text-[12px] text-dim">
                    <span className="flex items-center gap-1.5"><Layers className="h-3.5 w-3.5" /> {r.questionCount} questions</span>
                    <span className="flex items-center gap-1.5"><Clock3 className="h-3.5 w-3.5" /> {r.durationMinutes} min</span>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  {r.bestScore !== null && <div className="mr-1 text-right">
                      <p className={cn("font-display text-2xl font-bold", r.bestScore >= 70 ? "text-acid" : r.bestScore >= 45 ? "text-cyan2" : "text-rose2")}>
                        {r.bestScore}%
                      </p>
                      <p className="text-[11px] text-faint">best of {r.attemptCount}</p>
                    </div>}
                  <Button onClick={() => start(r)} variant={r.attemptCount > 0 && !r.inProgressAttemptId ? "ghost" : "primary"} icon={r.inProgressAttemptId ? <RotateCcw className="h-4 w-4" /> : <Play className="h-4 w-4" />}>
                    {r.inProgressAttemptId ? "Resume" : r.attemptCount > 0 ? "Retake" : "Start"}
                  </Button>
                  {r.lastAttemptId && <Link href={`/results/${r.lastAttemptId}`} className="btn btn-ghost btn-md">
                      Report
                    </Link>}
                </div>
              </div>
            </motion.div>)}
        </div>}
    </>;
}

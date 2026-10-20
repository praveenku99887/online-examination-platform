import { useEffect, useState } from "react";
import Link from "@/lib/rr";
import { useParams, useRouter } from "@/lib/rr";
import { ArrowLeft, ChartSpline, CheckCircle2, Clock3, Layers, Play, RotateCcw, Sparkles, Users } from "lucide-react";
import { PageIn, Reveal } from "@/components/fx";
import { Badge, Button, ErrorBanner, Skeleton, api, cn } from "@/components/ui";
export function DetailClient({
  user
}) {
  const params = useParams();
  const router = useRouter();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [starting, setStarting] = useState(false);
  useEffect(() => {
    api(`/api/assessments/${params.id}`).then(setData).catch(e => setError(e.message));
  }, [params.id]);
  async function start() {
    setStarting(true);
    try {
      const res = await api("/api/attempts", {
        method: "POST",
        body: JSON.stringify({
          assessmentId: params.id
        })
      });
      router.push(`/exam/${res.attemptId}`);
    } catch (e) {
      setError(e.message);
      setStarting(false);
    }
  }
  if (error) return <div className="space-y-6">
        <Link href="/dashboard" className="btn btn-ghost btn-sm">
          <ArrowLeft className="h-3.5 w-3.5" /> Back
        </Link>
        <ErrorBanner message={error} />
      </div>;
  if (!data) return <div className="space-y-5">
        <Skeleton className="h-40" />
        <Skeleton className="h-72" />
      </div>;
  const a = data.assessment;
  const submitted = data.attempts.filter(t => t.status === "submitted");
  const inProgress = data.attempts.find(t => t.status === "in_progress");
  return <PageIn>
      <Link href="/dashboard" className="btn btn-ghost btn-sm mb-6">
        <ArrowLeft className="h-3.5 w-3.5" /> Dashboard
      </Link>

      <Reveal>
        <header className="glass relative overflow-hidden rounded-3xl p-7 md:p-9">
          <div className="pointer-events-none absolute -right-24 -top-24 h-64 w-64 rounded-full bg-vio/15 blur-3xl" />
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <Badge tone="vio">{a.subject}</Badge>
            {a.isAiGenerated && <Badge tone="mint"><Sparkles className="h-3 w-3" /> AI authored</Badge>}
            <span className="text-[12px] text-faint">by {a.teacherName}</span>
          </div>
          <h1 className="font-display max-w-3xl text-3xl font-bold tracking-tight md:text-5xl">
            {a.title}
          </h1>
          {a.description && <p className="mt-3 max-w-2xl text-sm leading-relaxed text-dim">{a.description}</p>}
          <div className="mt-6 flex flex-wrap items-center gap-6 border-t border-white/8 pt-5 text-[13px] text-dim">
            <span className="flex items-center gap-2"><Layers className="h-4 w-4 text-acid" /> {a.questions.length} questions</span>
            <span className="flex items-center gap-2"><Clock3 className="h-4 w-4 text-cyan2" /> {a.durationMinutes} minutes</span>
            <span className="flex items-center gap-2"><Users className="h-4 w-4 text-vio" /> {submitted.length} submissions</span>
          </div>

          {!data.isOwner && user.role === "student" && <div className="mt-7 flex flex-wrap items-center gap-3">
              <Button size="lg" onClick={start} loading={starting} icon={inProgress ? <RotateCcw className="h-4 w-4" /> : <Play className="h-4 w-4" />}>
                {inProgress ? "Resume attempt" : submitted.length ? "Retake assessment" : "Begin assessment"}
              </Button>
              <p className="text-[12px] text-faint">
                The clock starts when you do. Server-enforced, auto-submit at zero.
              </p>
            </div>}
        </header>
      </Reveal>

      {/* teacher: submissions */}
      {data.isOwner && <Reveal delay={0.1}>
          <section className="mt-8">
            <h2 className="font-display mb-4 flex items-center gap-2 text-xl font-bold">
              <ChartSpline className="h-5 w-5 text-acid" /> Submissions
            </h2>
            {submitted.length === 0 ? <div className="glass rounded-3xl px-6 py-10 text-center text-sm text-dim">
                No submissions yet — share the assessment with your students.
              </div> : <div className="glass overflow-hidden rounded-3xl">
                {submitted.sort((x, y) => new Date(y.submittedAt ?? 0).getTime() - new Date(x.submittedAt ?? 0).getTime()).map((t, i) => {
            const pct = t.totalQuestions ? Math.round(t.correctCount / t.totalQuestions * 100) : 0;
            return <Link key={t.id} href={`/results/${t.id}`} className={cn("group flex items-center justify-between gap-4 px-6 py-4 transition-colors hover:bg-white/4", i > 0 && "border-t border-white/6")}>
                        <div className="flex items-center gap-4">
                          <span className="grid h-9 w-9 place-items-center rounded-full bg-gradient-to-br from-vio to-cyan2 font-display text-xs font-bold text-[#0a0a10]">
                            {(t.studentName ?? "ST").slice(0, 2).toUpperCase()}
                          </span>
                          <div>
                            <p className="text-sm font-semibold">{t.studentName}</p>
                            <p className="text-[12px] text-faint">
                              {t.submittedAt ? new Date(t.submittedAt).toLocaleString() : ""}
                              {t.timeTakenSeconds ? ` · ${Math.floor(t.timeTakenSeconds / 60)}m ${t.timeTakenSeconds % 60}s` : ""}
                            </p>
                          </div>
                        </div>
                        <div className="flex items-center gap-4">
                          <div className="w-28 max-w-[30vw]">
                            <div className="h-1.5 overflow-hidden rounded-full bg-white/8">
                              <div className={cn("h-full rounded-full", pct >= 70 ? "bg-acid" : pct >= 45 ? "bg-cyan2" : "bg-rose2")} style={{
                      width: `${pct}%`
                    }} />
                            </div>
                          </div>
                          <span className={cn("font-display w-14 text-right text-lg font-bold", pct >= 70 ? "text-acid" : pct >= 45 ? "text-cyan2" : "text-rose2")}>
                            {pct}%
                          </span>
                        </div>
                      </Link>;
          })}
              </div>}
          </section>
        </Reveal>}

      {/* question preview */}
      <Reveal delay={0.16}>
        <section className="mt-10">
          <h2 className="font-display mb-4 text-xl font-bold">
            {data.isOwner ? "Question paper" : "What awaits inside"}
          </h2>
          <div className="space-y-4">
            {a.questions.map((q, qi) => <div key={q.id} className="glass rounded-3xl p-6">
                <div className="flex items-start gap-4">
                  <span className="font-display grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-white/8 text-[13px] font-bold">
                    {qi + 1}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="text-[15px] font-medium leading-relaxed">{q.text}</p>
                    <ul className="mt-4 grid gap-2 sm:grid-cols-2">
                      {q.options.map((o, oi) => <li key={o.id} className="flex items-center gap-2.5 rounded-xl border border-white/8 bg-white/2 px-3.5 py-2.5 text-[13px] text-dim">
                          <span className="font-display text-[11px] font-bold text-faint">
                            {String.fromCharCode(65 + oi)}
                          </span>
                          <span className="truncate">{o.text}</span>
                        </li>)}
                    </ul>
                    {data.isOwner && <p className="mt-3 flex items-center gap-1.5 text-[12px] text-faint">
                        <CheckCircle2 className="h-3.5 w-3.5 text-acid" />
                        Correct answers & explanations are visible inside each student report.
                      </p>}
                  </div>
                </div>
              </div>)}
          </div>
        </section>
      </Reveal>
    </PageIn>;
}

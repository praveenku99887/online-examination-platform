import { useEffect, useMemo, useState } from "react";
import Link from "@/lib/rr";
import { useParams } from "@/lib/rr";
import { motion } from "framer-motion";
import { AlarmClockOff, ArrowLeft, CheckCircle2, ChevronDown, CircleSlash2, Clock3, Flame, Lightbulb, Sparkles, Target, TrendingUp, Trophy, Users, XCircle, Zap } from "lucide-react";
import { PageIn, Reveal, Tilt } from "@/components/fx";
import { ScoreRing, TopicRadar, MomentumArea, TimeBars, MasteryBars } from "@/components/charts";
import { Badge, Button, ErrorBanner, Skeleton, api, cn } from "@/components/ui";
export function ReportClient() {
  const params = useParams();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [openQ, setOpenQ] = useState(null);
  useEffect(() => {
    api(`/api/attempts/${params.attemptId}/report`).then(setData).catch(e => setError(e.message));
  }, [params.attemptId]);
  const derived = useMemo(() => {
    if (!data) return null;
    const qs = data.questions;
    // topics
    const topics = new Map();
    for (const q of qs) {
      const t = topics.get(q.topic) ?? {
        correct: 0,
        total: 0
      };
      t.total++;
      if (q.isCorrect) t.correct++;
      topics.set(q.topic, t);
    }
    const topicList = [...topics.entries()].map(([topic, v]) => ({
      topic,
      ...v,
      accuracy: Math.round(v.correct / v.total * 100)
    }));
    // momentum
    let run = 0;
    const momentum = qs.map((q, i) => {
      if (q.isCorrect) run++;
      return {
        q: `Q${i + 1}`,
        cumulative: Math.round(run / (i + 1) * 100)
      };
    });
    // time
    const timed = qs.filter(q => q.timeSeconds !== null && q.timeSeconds > 0);
    const avgTime = timed.length ? timed.reduce((s, q) => s + (q.timeSeconds ?? 0), 0) / timed.length : 0;
    const timeBars = qs.map((q, i) => ({
      q: `Q${i + 1}`,
      seconds: q.timeSeconds && q.timeSeconds > 0 ? q.timeSeconds : Math.round(avgTime),
      correct: q.isCorrect,
      estimated: !q.timeSeconds || q.timeSeconds <= 0
    }));
    // streak
    let best = 0;
    let cur = 0;
    for (const q of qs) {
      cur = q.isCorrect ? cur + 1 : 0;
      best = Math.max(best, cur);
    }
    const skipped = qs.filter(q => !q.chosenId).length;
    return {
      topicList,
      momentum,
      timeBars,
      avgTime: Math.round(avgTime),
      bestStreak: best,
      skipped
    };
  }, [data]);
  if (error) return <div className="space-y-6">
        <Link href="/dashboard" className="btn btn-ghost btn-sm"><ArrowLeft className="h-3.5 w-3.5" /> Dashboard</Link>
        <ErrorBanner message={error} />
      </div>;
  if (!data || !derived) return <div className="space-y-5">
        <Skeleton className="h-72" />
        <div className="grid gap-4 md:grid-cols-2"><Skeleton className="h-80" /><Skeleton className="h-80" /></div>
      </div>;
  const {
    attempt,
    assessment,
    peers,
    questions
  } = data;
  const verdict = attempt.pct >= 90 ? "Flawless command of the material." : attempt.pct >= 75 ? "Strong performance — polish the edges." : attempt.pct >= 55 ? "Solid ground. A few concepts to revisit." : attempt.pct >= 35 ? "Foundations forming — review the misses below." : "A starting point. Study the explanations and retake.";
  const statCards = [{
    icon: <Target className="h-4 w-4 text-acid" />,
    label: "Accuracy",
    value: `${attempt.correctCount}/${attempt.totalQuestions}`
  }, {
    icon: <Clock3 className="h-4 w-4 text-cyan2" />,
    label: "Time used",
    value: `${Math.floor(attempt.timeTakenSeconds / 60)}m ${attempt.timeTakenSeconds % 60}s`,
    sub: `of ${assessment.durationMinutes}m allowed`
  }, {
    icon: <Flame className="h-4 w-4 text-rose2" />,
    label: "Best streak",
    value: `${derived.bestStreak}`,
    sub: "consecutive correct"
  }, peers.percentile !== null ? {
    icon: <Trophy className="h-4 w-4 text-vio" />,
    label: "Peer percentile",
    value: `${peers.percentile}th`,
    sub: `vs ${peers.count} other attempts`
  } : {
    icon: <Zap className="h-4 w-4 text-vio" />,
    label: "Avg per question",
    value: `${derived.avgTime}s`,
    sub: derived.skipped ? `${derived.skipped} skipped` : "all answered"
  }];
  return <PageIn>
      <Link href="/dashboard" className="btn btn-ghost btn-sm mb-6">
        <ArrowLeft className="h-3.5 w-3.5" /> Dashboard
      </Link>

      {/* ------- hero ------- */}
      <Reveal>
        <section className="glass relative overflow-hidden rounded-3xl p-7 md:p-10">
          <div className="pointer-events-none absolute -left-24 -top-24 h-72 w-72 rounded-full bg-vio/12 blur-3xl" />
          <div className="pointer-events-none absolute -bottom-32 -right-20 h-72 w-72 rounded-full bg-acid/10 blur-3xl" />
          <div className="relative flex flex-col items-center gap-10 lg:flex-row">
            <ScoreRing pct={attempt.pct} size={210} />
            <div className="flex-1 text-center lg:text-left">
              <div className="mb-3 flex flex-wrap items-center justify-center gap-2 lg:justify-start">
                <Badge tone="vio">{assessment.subject}</Badge>
                {assessment.isAiGenerated && <Badge tone="mint"><Sparkles className="h-3 w-3" /> AI authored</Badge>}
                {attempt.autoSubmitted && <Badge tone="rose"><AlarmClockOff className="h-3 w-3" /> auto-submitted at zero</Badge>}
              </div>
              <h1 className="font-display text-3xl font-bold tracking-tight md:text-4xl">
                {assessment.title}
              </h1>
              <p className="serif-accent mt-3 text-xl text-dim">{verdict}</p>
              <p className="mt-2 text-[13px] text-faint">
                {attempt.studentName} ·{" "}
                {attempt.submittedAt ? new Date(attempt.submittedAt).toLocaleString() : ""}
              </p>
            </div>
            <div className="grid w-full max-w-sm grid-cols-2 gap-3">
              {statCards.map((s, i) => <motion.div key={s.label} initial={{
              opacity: 0,
              y: 16
            }} animate={{
              opacity: 1,
              y: 0
            }} transition={{
              delay: 0.3 + i * 0.09,
              duration: 0.6,
              ease: [0.22, 1, 0.36, 1]
            }} className="rounded-2xl border border-white/8 bg-white/3 p-4">
                  <div className="flex items-center gap-2">
                    {s.icon}
                    <span className="label-caps">{s.label}</span>
                  </div>
                  <p className="font-display mt-2.5 text-2xl font-bold">{s.value}</p>
                  {s.sub && <p className="mt-0.5 text-[11px] text-faint">{s.sub}</p>}
                </motion.div>)}
            </div>
          </div>
        </section>
      </Reveal>

      {/* ------- charts bento ------- */}
      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <Reveal delay={0.05}>
          <Tilt max={3} className="h-full">
            <ChartCard icon={<Target className="h-4 w-4 text-acid" />} title="Topic mastery" sub="How each concept held up under pressure">
              {derived.topicList.length > 2 ? <TopicRadar data={derived.topicList.map(t => ({
              topic: t.topic,
              accuracy: t.accuracy
            }))} /> : <div className="py-4">
                  <MasteryBars data={derived.topicList} />
                </div>}
            </ChartCard>
          </Tilt>
        </Reveal>
        <Reveal delay={0.1}>
          <Tilt max={3} className="h-full">
            <ChartCard icon={<TrendingUp className="h-4 w-4 text-vio" />} title="Momentum curve" sub="Running accuracy as the paper unfolded">
              <MomentumArea data={derived.momentum} />
            </ChartCard>
          </Tilt>
        </Reveal>
        <Reveal delay={0.14}>
          <Tilt max={3} className="h-full">
            <ChartCard icon={<Clock3 className="h-4 w-4 text-cyan2" />} title="Time heat" sub="Seconds per question · green correct, red missed">
              <TimeBars data={derived.timeBars} avg={derived.avgTime} />
            </ChartCard>
          </Tilt>
        </Reveal>
        <Reveal delay={0.18}>
          <Tilt max={3} className="h-full">
            <ChartCard icon={<Users className="h-4 w-4 text-mint" />} title="Field comparison" sub={peers.count ? `You against ${peers.count} other attempt${peers.count > 1 ? "s" : ""} on this paper` : "Performance breakdown"}>
              {peers.count > 0 ? <div className="flex h-[260px] flex-col justify-center gap-7">
                  <CompareBar label="You" pct={attempt.pct} accent />
                  <CompareBar label="Cohort average" pct={peers.avgPct ?? 0} />
                  <div className="rounded-2xl border border-vio/25 bg-vio/8 px-5 py-4 text-center">
                    <p className="font-display text-3xl font-bold text-vio">
                      {peers.percentile}
                      <span className="text-lg">th</span>
                    </p>
                    <p className="text-[12px] text-dim">percentile — you outscored ~{peers.percentile}% of attempts</p>
                  </div>
                </div> : <div className="py-2">
                  <MasteryBars data={derived.topicList} />
                  <p className="mt-5 rounded-2xl border border-white/8 bg-white/3 px-4 py-3 text-center text-[12px] text-dim">
                    First attempt on record — cohort stats appear once more students sit this paper.
                  </p>
                </div>}
            </ChartCard>
          </Tilt>
        </Reveal>
      </div>

      {/* ------- question review ------- */}
      <Reveal delay={0.1}>
        <h2 className="font-display mt-14 mb-2 text-2xl font-bold tracking-tight">
          Question autopsy
        </h2>
        <p className="mb-6 text-sm text-dim">
          Every option — right or wrong — carries its reasoning. Study the misses hardest.
        </p>
      </Reveal>
      <div className="space-y-4">
        {questions.map((q, qi) => {
        const chosen = q.options.find(o => o.id === q.chosenId);
        const correct = q.options.find(o => o.isCorrect);
        const isOpen = openQ === q.id;
        return <motion.div key={q.id} initial={{
          opacity: 0,
          y: 18
        }} whileInView={{
          opacity: 1,
          y: 0
        }} viewport={{
          once: true,
          margin: "-40px"
        }} transition={{
          duration: 0.5,
          ease: [0.22, 1, 0.36, 1]
        }} className={cn("glass overflow-hidden rounded-3xl border-l-4", q.isCorrect ? "border-l-acid" : q.chosenId ? "border-l-rose2" : "border-l-faint")}>
              <button onClick={() => setOpenQ(isOpen ? null : q.id)} className="flex w-full items-start gap-4 p-6 text-left">
                <span className={cn("mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-lg", q.isCorrect ? "bg-acid/15 text-acid" : q.chosenId ? "bg-rose2/15 text-rose2" : "bg-white/8 text-faint")}>
                  {q.isCorrect ? <CheckCircle2 className="h-4.5 w-4.5" /> : q.chosenId ? <XCircle className="h-4.5 w-4.5" /> : <CircleSlash2 className="h-4.5 w-4.5" />}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="label-caps">Q{qi + 1}</span>
                    <Badge tone="neutral">{q.topic}</Badge>
                    {q.timeSeconds ? <span className="flex items-center gap-1 text-[11px] text-faint">
                        <Clock3 className="h-3 w-3" /> {q.timeSeconds}s
                      </span> : null}
                  </div>
                  <p className="mt-1.5 font-display text-[15px] font-semibold leading-snug md:text-base">
                    {q.text}
                  </p>
                </div>
                <ChevronDown className={cn("mt-1 h-5 w-5 shrink-0 text-faint transition-transform duration-300", isOpen && "rotate-180")} />
              </button>

              <motion.div initial={false} animate={{
            height: isOpen ? "auto" : 0,
            opacity: isOpen ? 1 : 0
          }} transition={{
            duration: 0.4,
            ease: [0.22, 1, 0.36, 1]
          }} className="overflow-hidden">
                <div className="border-t border-white/6 px-6 pb-6 pt-5">
                  <div className="grid gap-2.5 sm:grid-cols-2">
                    {q.options.map((o, oi) => {
                  const isChosen = o.id === q.chosenId;
                  return <div key={o.id} className={cn("flex items-start gap-3 rounded-2xl border px-4 py-3 text-[13.5px] leading-relaxed", o.isCorrect ? "border-acid/45 bg-acid/8" : isChosen ? "border-rose2/45 bg-rose2/8" : "border-white/8 bg-white/2 opacity-75")}>
                          <span className={cn("font-display mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-md text-[11px] font-bold", o.isCorrect ? "bg-acid text-[#0a0a10]" : isChosen ? "bg-rose2 text-[#0a0a10]" : "bg-white/10 text-dim")}>
                            {String.fromCharCode(65 + oi)}
                          </span>
                          <div>
                            <p>{o.text}</p>
                            <p className="mt-1 text-[11px] font-semibold uppercase tracking-[0.14em]">
                              {o.isCorrect && <span className="text-acid">Correct answer</span>}
                              {isChosen && !o.isCorrect && <span className="text-rose2">Your choice</span>}
                              {isChosen && o.isCorrect && <span className="text-acid"> · you got it</span>}
                            </p>
                          </div>
                        </div>;
                })}
                  </div>

                  <div className="mt-5 space-y-3">
                    {q.correctExplanation && <div className="flex gap-3 rounded-2xl border border-acid/25 bg-acid/5 p-4">
                        <Lightbulb className="mt-0.5 h-4.5 w-4.5 shrink-0 text-acid" />
                        <div>
                          <p className="label-caps mb-1 !text-acid">Why the correct answer holds</p>
                          <p className="text-[13.5px] leading-relaxed text-ink/90">{q.correctExplanation}</p>
                        </div>
                      </div>}
                    {chosen && !q.isCorrect && chosen.wrongExplanation && <div className="flex gap-3 rounded-2xl border border-rose2/25 bg-rose2/5 p-4">
                        <XCircle className="mt-0.5 h-4.5 w-4.5 shrink-0 text-rose2" />
                        <div>
                          <p className="label-caps mb-1 !text-rose2">Why your choice fails</p>
                          <p className="text-[13.5px] leading-relaxed text-ink/90">{chosen.wrongExplanation}</p>
                        </div>
                      </div>}
                    {!q.isCorrect && correct && <p className="text-[13px] text-dim">
                        The correct answer was{" "}
                        <span className="font-semibold text-acid">
                          {String.fromCharCode(65 + q.options.findIndex(o => o.isCorrect))} — {correct.text}
                        </span>
                      </p>}
                    {/* all distractor reasoning */}
                    {q.options.some(o => !o.isCorrect && o.wrongExplanation && o.id !== q.chosenId) && <details className="group rounded-2xl border border-white/8 bg-white/2">
                        <summary className="cursor-pointer list-none px-4 py-3 text-[13px] font-semibold text-dim transition-colors hover:text-ink">
                          Autopsy of the remaining distractors
                        </summary>
                        <div className="space-y-2.5 border-t border-white/6 px-4 py-4">
                          {q.options.filter(o => !o.isCorrect && o.wrongExplanation && o.id !== q.chosenId).map(o => <div key={o.id} className="flex gap-2.5 text-[13px] leading-relaxed">
                                <span className="font-display mt-0.5 grid h-5 w-5 shrink-0 place-items-center rounded bg-white/10 text-[10px] font-bold text-dim">
                                  {String.fromCharCode(65 + q.options.indexOf(o))}
                                </span>
                                <p className="text-dim"><span className="text-ink/80">{o.text}:</span> {o.wrongExplanation}</p>
                              </div>)}
                        </div>
                      </details>}
                  </div>
                </div>
              </motion.div>
            </motion.div>;
      })}
      </div>

      <div className="mt-12 flex flex-wrap justify-center gap-3">
        <Link href="/dashboard" className="btn btn-ghost btn-lg">
          Back to dashboard
        </Link>
        <Link href={`/assessments/${assessment.id}`} className="btn btn-primary btn-lg">
          Retake this assessment
        </Link>
      </div>
    </PageIn>;
}
function ChartCard({
  icon,
  title,
  sub,
  children
}) {
  return <div className="glass card-hover h-full rounded-3xl p-6">
      <div className="mb-5 flex items-center gap-3">
        <span className="grid h-9 w-9 place-items-center rounded-xl bg-white/5">{icon}</span>
        <div>
          <h3 className="font-display text-base font-bold">{title}</h3>
          <p className="text-[12px] text-faint">{sub}</p>
        </div>
      </div>
      {children}
    </div>;
}
function CompareBar({
  label,
  pct,
  accent
}) {
  return <div>
      <div className="mb-2 flex justify-between text-[13px]">
        <span className={accent ? "font-semibold text-ink" : "text-dim"}>{label}</span>
        <span className={cn("font-display font-bold", accent ? "text-acid" : "text-dim")}>{pct}%</span>
      </div>
      <div className="h-4 overflow-hidden rounded-full bg-white/6">
        <motion.div initial={{
        width: 0
      }} animate={{
        width: `${pct}%`
      }} transition={{
        duration: 1.3,
        ease: [0.22, 1, 0.36, 1],
        delay: accent ? 0.2 : 0.45
      }} className={cn("h-full rounded-full", accent ? "bg-gradient-to-r from-vio to-acid" : "bg-white/25")} />
      </div>
    </div>;
}

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useParams, useRouter } from "@/lib/rr";
import { AnimatePresence, motion } from "framer-motion";
import { AlertTriangle, ArrowLeft, ArrowRight, CheckCircle2, Clock3, Flag, Send } from "lucide-react";
import { Button, Logo, Skeleton, api, cn } from "@/components/ui";
export function ExamClient() {
  const params = useParams();
  const router = useRouter();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [current, setCurrent] = useState(0);
  const [answers, setAnswers] = useState({});
  const [flags, setFlags] = useState(new Set());
  const [secondsLeft, setSecondsLeft] = useState(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const timingsRef = useRef({});
  const activeSinceRef = useRef(Date.now());
  const offsetRef = useRef(0);
  const expiresRef = useRef(0);
  const submittedRef = useRef(false);
  const answersRef = useRef({});
  const currentRef = useRef(0);
  const now = useCallback(() => Date.now() + offsetRef.current, []);

  /* ---- load ---- */
  useEffect(() => {
    let alive = true;
    api(`/api/attempts/${params.attemptId}`).then(res => {
      if (!alive) return;
      setData(res);
      setAnswers(res.attempt.answers ?? {});
      answersRef.current = res.attempt.answers ?? {};
      timingsRef.current = {
        ...(res.attempt.timings ?? {})
      };
      offsetRef.current = new Date(res.attempt.serverNow).getTime() - Date.now();
      expiresRef.current = new Date(res.attempt.expiresAt).getTime();
    }).catch(e => {
      if (!alive) return;
      if (/already submitted/i.test(e.message)) router.replace(`/results/${params.attemptId}`);else setError(e.message);
    });
    return () => {
      alive = false;
    };
  }, [params.attemptId, router]);
  const questions = useMemo(() => data?.assessment.questions ?? [], [data]);
  const q = questions[current];

  /* ---- time tracking ---- */
  const flushActive = useCallback(() => {
    if (!q) return;
    const elapsed = Math.max(0, Math.round((Date.now() - activeSinceRef.current) / 1000));
    timingsRef.current[q.id] = (timingsRef.current[q.id] ?? 0) + elapsed;
    activeSinceRef.current = Date.now();
  }, [q]);
  const go = useCallback(idx => {
    if (idx < 0 || idx >= questions.length) return;
    flushActive();
    setCurrent(idx);
    currentRef.current = idx;
  }, [flushActive, questions.length]);

  /* ---- submit ---- */
  const doSubmit = useCallback(async auto => {
    if (submittedRef.current) return;
    submittedRef.current = true;
    flushActive();
    setSubmitting(true);
    try {
      await api(`/api/attempts/${params.attemptId}/submit`, {
        method: "POST",
        body: JSON.stringify({
          answers: answersRef.current,
          timings: timingsRef.current,
          auto
        })
      });
      router.replace(`/results/${params.attemptId}`);
    } catch {
      submittedRef.current = false;
      setSubmitting(false);
      setError("Submission failed — check your connection and try again.");
    }
  }, [flushActive, params.attemptId, router]);

  /* ---- countdown tick ---- */
  useEffect(() => {
    if (!data) return;
    const tick = () => {
      const left = Math.max(0, Math.round((expiresRef.current - now()) / 1000));
      setSecondsLeft(left);
      if (left <= 0) doSubmit(true);
    };
    tick();
    const iv = setInterval(tick, 1000);
    return () => clearInterval(iv);
  }, [data, doSubmit, now]);

  /* ---- beforeunload guard ---- */
  useEffect(() => {
    const handler = e => {
      if (!submittedRef.current) {
        e.preventDefault();
        e.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, []);

  /* ---- keyboard controls ---- */
  useEffect(() => {
    const handler = e => {
      if (!questions.length) return;
      const cur = questions[currentRef.current];
      if (["1", "2", "3", "4", "5", "6"].includes(e.key)) {
        const idx = Number(e.key) - 1;
        const opt = cur?.options[idx];
        if (opt) select(opt.id);
      }
      if (e.key === "ArrowRight") go(currentRef.current + 1);
      if (e.key === "ArrowLeft") go(currentRef.current - 1);
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [questions, go]);
  function select(optionId) {
    if (!q) return;
    setAnswers(prev => {
      const next = {
        ...prev,
        [q.id]: optionId
      };
      answersRef.current = next;
      return next;
    });
  }
  function toggleFlag() {
    if (!q) return;
    setFlags(prev => {
      const next = new Set(prev);
      if (next.has(q.id)) next.delete(q.id);else next.add(q.id);
      return next;
    });
  }
  if (error) return <div className="grid min-h-screen place-items-center px-5">
        <div className="glass max-w-md rounded-3xl p-8 text-center">
          <AlertTriangle className="mx-auto mb-4 h-8 w-8 text-rose2" />
          <p className="font-display text-lg font-semibold">{error}</p>
          <Button className="mt-6" onClick={() => router.push("/dashboard")}>Back to dashboard</Button>
        </div>
      </div>;
  if (!data || !q) return <div className="mx-auto max-w-6xl space-y-5 px-5 pt-28">
        <Skeleton className="h-16" />
        <Skeleton className="h-96" />
      </div>;
  const answeredCount = Object.keys(answers).length;
  const progress = Math.round(answeredCount / questions.length * 100);
  const mm = secondsLeft === null ? "--" : String(Math.floor(secondsLeft / 60)).padStart(2, "0");
  const ss = secondsLeft === null ? "--" : String(secondsLeft % 60).padStart(2, "0");
  const critical = secondsLeft !== null && secondsLeft <= 60;
  return <div className="min-h-screen">
      {/* top bar */}
      <header className="fixed inset-x-0 top-0 z-50 border-b border-white/8 bg-bg/80 backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-5">
          <div className="flex min-w-0 items-center gap-3">
            <Logo size="sm" />
            <span className="hidden h-4 w-px bg-white/15 sm:block" />
            <div className="min-w-0">
              <p className="truncate font-display text-sm font-semibold">{data.assessment.title}</p>
              <p className="text-[11px] text-faint">{data.assessment.subject}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <div className={cn("flex items-center gap-2 rounded-full border border-white/10 bg-white/3 px-4 py-2 font-mono text-sm font-bold tabular-nums transition-colors", critical && "timer-critical")}>
              <Clock3 className="h-4 w-4" />
              {mm}:{ss}
            </div>
            <Button size="sm" onClick={() => setConfirmOpen(true)} icon={<Send className="h-3.5 w-3.5" />}>
              Submit
            </Button>
          </div>
        </div>
        <div className="h-[2px] w-full bg-white/6">
          <motion.div className="h-full bg-gradient-to-r from-vio via-cyan2 to-acid" animate={{
          width: `${progress}%`
        }} transition={{
          duration: 0.5,
          ease: [0.22, 1, 0.36, 1]
        }} />
        </div>
      </header>

      <main className="mx-auto grid max-w-6xl gap-6 px-5 pb-20 pt-28 lg:grid-cols-[1fr_280px]">
        {/* question panel */}
        <section>
          <AnimatePresence mode="wait">
            <motion.div key={q.id} initial={{
            opacity: 0,
            x: 32,
            filter: "blur(4px)"
          }} animate={{
            opacity: 1,
            x: 0,
            filter: "blur(0px)"
          }} exit={{
            opacity: 0,
            x: -32,
            filter: "blur(4px)"
          }} transition={{
            duration: 0.35,
            ease: [0.22, 1, 0.36, 1]
          }} className="glass rounded-3xl p-7 md:p-9">
              <div className="mb-6 flex items-center justify-between">
                <span className="label-caps">
                  Question {current + 1} <span className="text-faint">/ {questions.length}</span>
                </span>
                <button onClick={toggleFlag} className={cn("flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[12px] font-semibold transition-all", flags.has(q.id) ? "border-rose2/50 bg-rose2/10 text-rose2" : "border-white/10 text-faint hover:border-white/25 hover:text-ink")}>
                  <Flag className="h-3.5 w-3.5" />
                  {flags.has(q.id) ? "Flagged" : "Flag"}
                </button>
              </div>

              <h2 className="font-display text-xl font-semibold leading-snug md:text-2xl">
                {q.text}
              </h2>

              <div className="mt-8 space-y-3">
                {q.options.map((o, oi) => {
                const selected = answers[q.id] === o.id;
                return <motion.button key={o.id} onClick={() => select(o.id)} whileTap={{
                  scale: 0.985
                }} className={cn("group flex w-full items-start gap-4 rounded-2xl border p-4 text-left transition-all duration-300", selected ? "border-acid/60 bg-acid/10 ring-glow" : "border-white/10 bg-white/2 hover:border-white/25 hover:bg-white/4")}>
                      <span className={cn("font-display grid h-8 w-8 shrink-0 place-items-center rounded-lg border text-[13px] font-bold transition-all duration-300", selected ? "border-acid bg-acid text-[#0a0a10]" : "border-white/15 text-dim group-hover:border-white/30")}>
                        {String.fromCharCode(65 + oi)}
                      </span>
                      <span className={cn("pt-1 text-[15px] leading-relaxed", selected ? "text-ink" : "text-dim group-hover:text-ink")}>
                        {o.text}
                      </span>
                      {selected && <CheckCircle2 className="ml-auto mt-1 h-5 w-5 shrink-0 text-acid" />}
                    </motion.button>;
              })}
              </div>

              <div className="mt-8 flex items-center justify-between">
                <Button variant="ghost" onClick={() => go(current - 1)} disabled={current === 0} icon={<ArrowLeft className="h-4 w-4" />}>
                  Previous
                </Button>
                <p className="hidden text-[11px] text-faint md:block">
                  keys 1–{q.options.length} to answer · arrows to move
                </p>
                {current === questions.length - 1 ? <Button onClick={() => setConfirmOpen(true)} icon={<Send className="h-4 w-4" />}>
                    Finish
                  </Button> : <Button onClick={() => go(current + 1)}>
                    Next <ArrowRight className="h-4 w-4" />
                  </Button>}
              </div>
            </motion.div>
          </AnimatePresence>
        </section>

        {/* palette */}
        <aside className="h-fit lg:sticky lg:top-28">
          <div className="glass rounded-3xl p-5">
            <div className="mb-4 flex items-center justify-between">
              <span className="label-caps">Palette</span>
              <span className="font-display text-sm font-bold text-acid">{progress}%</span>
            </div>
            <div className="grid grid-cols-6 gap-2 lg:grid-cols-5">
              {questions.map((qq, i) => {
              const answered = !!answers[qq.id];
              const isCurrent = i === current;
              const flagged = flags.has(qq.id);
              return <button key={qq.id} onClick={() => go(i)} className={cn("relative grid h-10 place-items-center rounded-xl border font-display text-[13px] font-bold transition-all duration-300", isCurrent ? "border-acid bg-acid text-[#0a0a10] scale-110" : answered ? "border-cyan2/40 bg-cyan2/12 text-cyan2" : "border-white/10 text-faint hover:border-white/30 hover:text-ink")}>
                    {i + 1}
                    {flagged && <span className="absolute -right-0.5 -top-0.5 h-2 w-2 rounded-full bg-rose2" />}
                  </button>;
            })}
            </div>
            <div className="mt-5 space-y-2 border-t border-white/8 pt-4 text-[12px] text-dim">
              <p className="flex items-center gap-2"><span className="h-2.5 w-2.5 rounded bg-cyan2/60" /> Answered</p>
              <p className="flex items-center gap-2"><span className="h-2.5 w-2.5 rounded bg-acid" /> Current</p>
              <p className="flex items-center gap-2"><span className="h-2.5 w-2.5 rounded bg-rose2" /> Flagged for review</p>
            </div>
            <Button className="mt-5 w-full" variant="ghost" onClick={() => setConfirmOpen(true)} loading={submitting}>
              {answeredCount}/{questions.length} answered — submit
            </Button>
          </div>
        </aside>
      </main>

      {/* confirm modal */}
      <AnimatePresence>
        {confirmOpen && <motion.div initial={{
        opacity: 0
      }} animate={{
        opacity: 1
      }} exit={{
        opacity: 0
      }} className="fixed inset-0 z-[100] grid place-items-center bg-black/70 p-4 backdrop-blur-sm" onClick={() => setConfirmOpen(false)}>
            <motion.div initial={{
          scale: 0.94,
          y: 24
        }} animate={{
          scale: 1,
          y: 0
        }} exit={{
          scale: 0.95,
          y: 12
        }} transition={{
          type: "spring",
          stiffness: 300,
          damping: 26
        }} onClick={e => e.stopPropagation()} className="glass-deep w-full max-w-md rounded-3xl p-8 text-center">
              <span className="mx-auto mb-5 grid h-14 w-14 place-items-center rounded-2xl bg-acid/12 text-acid">
                <Send className="h-6 w-6" />
              </span>
              <h3 className="font-display text-2xl font-bold">Submit your attempt?</h3>
              <p className="mt-2 text-sm leading-relaxed text-dim">
                You answered <span className="font-bold text-ink">{answeredCount}</span> of{" "}
                <span className="font-bold text-ink">{questions.length}</span> questions.
                {answeredCount < questions.length && " Unanswered questions score zero."}
              </p>
              <div className="mt-7 grid grid-cols-2 gap-3">
                <Button variant="ghost" onClick={() => setConfirmOpen(false)}>Keep going</Button>
                <Button onClick={() => doSubmit(false)} loading={submitting}>Submit now</Button>
              </div>
            </motion.div>
          </motion.div>}
      </AnimatePresence>
    </div>;
}

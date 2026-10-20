import { useRef, useState } from "react";
import Link from "@/lib/rr";
import { useRouter } from "@/lib/rr";
import { motion, useScroll, useTransform } from "framer-motion";
import { ArrowRight, ArrowUpRight, BrainCircuit, ChartSpline, FileUp, FlaskConical, GraduationCap, KeyRound, Layers, Loader2, PenLine, ShieldCheck, Sparkles, Target, Timer } from "lucide-react";
import { AuroraCanvas, CursorGlow, Magnetic, Marquee, Reveal, Tilt, Counter } from "@/components/fx";
import { Logo, api } from "@/components/ui";
export default function LandingPage() {
  const router = useRouter();
  const [demoBusy, setDemoBusy] = useState(null);
  const heroRef = useRef(null);
  const {
    scrollYProgress
  } = useScroll({
    target: heroRef,
    offset: ["start start", "end start"]
  });
  const heroY = useTransform(scrollYProgress, [0, 1], [0, 180]);
  const heroOpacity = useTransform(scrollYProgress, [0, 0.75], [1, 0]);
  const card1Y = useTransform(scrollYProgress, [0, 1], [0, -120]);
  const card2Y = useTransform(scrollYProgress, [0, 1], [0, -220]);
  const card3Y = useTransform(scrollYProgress, [0, 1], [0, -60]);
  async function demoLogin(role) {
    setDemoBusy(role);
    try {
      await api("/api/auth/demo", {
        method: "POST",
        body: JSON.stringify({
          role
        })
      });
      router.push("/dashboard");
    } catch {
      setDemoBusy(null);
    }
  }
  return <div className="relative min-h-screen overflow-hidden">
      <CursorGlow />

      {/* ---------- NAV ---------- */}
      <motion.header initial={{
      y: -60,
      opacity: 0
    }} animate={{
      y: 0,
      opacity: 1
    }} transition={{
      duration: 0.9,
      ease: [0.22, 1, 0.36, 1]
    }} className="fixed inset-x-0 top-0 z-50">
        <div className="mx-auto mt-4 flex max-w-6xl items-center justify-between rounded-full border border-white/8 bg-bg/60 px-5 py-2.5 backdrop-blur-xl mx-4 lg:mx-auto">
          <Link href="/">
            <Logo size="sm" />
          </Link>
          <div className="hidden items-center gap-7 text-[13px] font-medium text-dim md:flex">
            <a href="#features" className="transition-colors hover:text-ink">Features</a>
            <a href="#flow" className="transition-colors hover:text-ink">Flow</a>
            <a href="#analytics" className="transition-colors hover:text-ink">Analytics</a>
            <a href="#ai" className="transition-colors hover:text-ink">AI Studio</a>
          </div>
          <div className="flex items-center gap-2">
            <Link href="/login" className="btn btn-ghost btn-sm">Sign in</Link>
            <button onClick={() => demoLogin("teacher")} className="btn btn-primary btn-sm hidden sm:inline-flex" disabled={demoBusy !== null}>
              {demoBusy === "teacher" ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
              Live demo
            </button>
          </div>
        </div>
      </motion.header>

      {/* ---------- HERO ---------- */}
      <section ref={heroRef} className="relative min-h-screen">
        <div className="absolute inset-0">
          <AuroraCanvas className="h-full w-full opacity-70" />
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,transparent_0%,#07070c_78%)]" />
        </div>

        <motion.div style={{
        y: heroY,
        opacity: heroOpacity
      }} className="relative z-10 mx-auto flex min-h-screen max-w-6xl flex-col items-center justify-center px-5 pt-28 pb-16 text-center">
          <Reveal delay={0.1}>
            <div className="glass mb-8 inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-[12px] font-medium tracking-wide text-dim">
              <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-acid" />
              The assessment platform for teachers & students
            </div>
          </Reveal>

          <Reveal delay={0.2}>
            <h1 className="font-display max-w-5xl text-[clamp(3rem,9vw,7.5rem)] leading-[0.95] font-bold tracking-[-0.03em]">
              Assessment,
              <br />
              <span className="serif-accent bg-gradient-to-r from-acid via-cyan2 to-vio bg-clip-text font-normal text-transparent">
                beautifully
              </span>{" "}
              measured.
            </h1>
          </Reveal>

          <Reveal delay={0.35}>
            <p className="mt-7 max-w-xl text-base leading-relaxed text-dim md:text-lg">
              Teachers architect exams with rich explanations. Students sit timed
              attempts and receive cinematic reports. And when you&apos;re short on
              time — <span className="text-ink font-medium">your AI writes the whole paper.</span>
            </p>
          </Reveal>

          <Reveal delay={0.5} className="mt-10 flex flex-col items-center gap-4 sm:flex-row">
            <Magnetic>
              <button onClick={() => demoLogin("teacher")} disabled={demoBusy !== null} className="btn btn-primary btn-lg group">
                {demoBusy === "teacher" ? <Loader2 className="h-4 w-4 animate-spin" /> : <PenLine className="h-4 w-4 transition-transform duration-300 group-hover:rotate-12" />}
                Explore as Teacher
                <ArrowRight className="h-4 w-4 transition-transform duration-300 group-hover:translate-x-1" />
              </button>
            </Magnetic>
            <Magnetic>
              <button onClick={() => demoLogin("student")} disabled={demoBusy !== null} className="btn btn-ghost btn-lg group">
                {demoBusy === "student" ? <Loader2 className="h-4 w-4 animate-spin" /> : <GraduationCap className="h-4 w-4 text-cyan2" />}
                Explore as Student
              </button>
            </Magnetic>
          </Reveal>
          <Reveal delay={0.6}>
            <p className="mt-4 text-[12px] text-faint">
              No sign-up needed — one-click demo accounts seeded with real data.
            </p>
          </Reveal>
        </motion.div>

        {/* floating artifacts */}
        <motion.div style={{
        y: card1Y
      }} className="absolute left-[4%] top-[30%] z-0 hidden w-60 lg:block">
          <div className="glass floaty rounded-2xl p-4 [animation-delay:1s]">
            <div className="mb-3 flex items-center justify-between">
              <span className="label-caps">Exam live</span>
              <span className="flex items-center gap-1 font-mono text-[13px] font-bold text-rose2">
                <Timer className="h-3.5 w-3.5" /> 07:42
              </span>
            </div>
            <div className="space-y-2">
              {[0, 1, 2].map(i => <div key={i} className={`rounded-xl border px-3 py-2 text-[11px] ${i === 1 ? "border-acid/50 bg-acid/10 text-acid" : "border-white/8 text-dim"}`}>
                  {["A discrete quantum of light", "γ = 1/√(1−v²/c²)", "Entropy never decreases"][i]}
                </div>)}
            </div>
            <div className="mt-3 h-1 overflow-hidden rounded-full bg-white/8">
              <div className="h-full w-[62%] rounded-full bg-gradient-to-r from-vio to-acid" />
            </div>
          </div>
        </motion.div>
        <motion.div style={{
        y: card2Y
      }} className="absolute right-[3%] top-[22%] z-0 hidden w-64 lg:block">
          <div className="glass floaty rounded-2xl p-4">
            <div className="mb-2 flex items-center justify-between">
              <span className="label-caps">Report</span>
              <ChartSpline className="h-4 w-4 text-vio" />
            </div>
            <div className="flex items-end gap-1.5">
              {[55, 80, 40, 92, 66, 88, 74].map((h, i) => <motion.div key={i} initial={{
              height: 4
            }} animate={{
              height: h * 0.7
            }} transition={{
              duration: 1.2,
              delay: 0.4 + i * 0.12,
              ease: [0.22, 1, 0.36, 1]
            }} className={`w-full rounded-t-md ${i === 2 ? "bg-rose2/80" : "bg-gradient-to-t from-vio/60 to-cyan2/80"}`} />)}
            </div>
            <p className="mt-3 font-display text-2xl font-bold text-acid">86<span className="text-sm text-dim">/100</span></p>
          </div>
        </motion.div>
        <motion.div style={{
        y: card3Y
      }} className="absolute bottom-[12%] left-[10%] z-0 hidden lg:block">
          <div className="glass floaty flex items-center gap-3 rounded-2xl px-4 py-3 [animation-delay:2s]">
            <span className="grid h-9 w-9 place-items-center rounded-xl bg-vio/15 text-vio">
              <BrainCircuit className="h-4.5 w-4.5" />
            </span>
            <div>
              <p className="text-[12px] font-semibold">AI generated 12 questions</p>
              <p className="text-[11px] text-faint">from “Thermodynamics II”</p>
            </div>
            <Sparkles className="h-4 w-4 text-acid" />
          </div>
        </motion.div>
      </section>

      {/* ---------- MARQUEE ---------- */}
      <div className="relative border-y border-white/6 bg-panel/60 py-5 backdrop-blur-sm">
        <Marquee items={["Timed attempts", "AI-generated exams", "Per-option explanations", "Mastery radar", "Peer percentile", "Topic research", "Material upload", "Momentum curves", "Built for classrooms"]} />
      </div>

      {/* ---------- STATS ---------- */}
      <section className="mx-auto grid max-w-6xl grid-cols-2 gap-px overflow-hidden bg-white/6 md:grid-cols-4 md:rounded-3xl md:border md:border-white/8 md:mx-5 lg:mx-auto my-20">
        {[{
        icon: <Layers className="h-4 w-4 text-acid" />,
        value: 4,
        suffix: "",
        label: "Option diagnostic answers"
      }, {
        icon: <Timer className="h-4 w-4 text-cyan2" />,
        value: 100,
        suffix: "%",
        label: "Timer-enforced attempts"
      }, {
        icon: <ChartSpline className="h-4 w-4 text-vio" />,
        value: 6,
        suffix: "+",
        label: "Report visualisations"
      }, {
        icon: <BrainCircuit className="h-4 w-4 text-mint" />,
        value: 1,
        suffix: " click",
        label: "AI exam authoring"
      }].map((s, i) => <div key={i} className="bg-bg px-6 py-10 text-center">
            <Reveal delay={i * 0.08}>
              <div className="mx-auto mb-3 flex justify-center">{s.icon}</div>
              <p className="font-display text-4xl font-bold tracking-tight">
                <Counter to={s.value} suffix={s.suffix} />
              </p>
              <p className="mt-2 text-[12px] font-medium tracking-wide text-dim">{s.label}</p>
            </Reveal>
          </div>)}
      </section>

      {/* ---------- FEATURES BENTO ---------- */}
      <section id="features" className="mx-auto max-w-6xl px-5 py-24">
        <Reveal>
          <p className="label-caps mb-4 flex items-center gap-2">
            <span className="h-px w-8 bg-acid" /> The toolkit
          </p>
          <h2 className="font-display max-w-2xl text-4xl font-bold tracking-tight md:text-6xl">
            Everything an exam <span className="serif-accent font-normal text-acid">deserves.</span>
          </h2>
        </Reveal>

        <div className="mt-14 grid gap-4 md:grid-cols-3">
          {[{
          icon: <PenLine className="h-5 w-5" />,
          tone: "text-acid bg-acid/10",
          title: "Author with intent",
          body: "Teachers compose questions with a correct answer, a teaching explanation, and a reason every wrong option fails — turning each exam into feedback.",
          span: "md:col-span-2"
        }, {
          icon: <Timer className="h-5 w-5" />,
          tone: "text-rose2 bg-rose2/10",
          title: "Timed & tamper-aware",
          body: "Server-enforced countdowns, auto-submit, resume-in-progress, and per-question time tracking.",
          span: ""
        }, {
          icon: <BrainCircuit className="h-5 w-5" />,
          tone: "text-vio bg-vio/10",
          title: "AI that researches",
          body: "Hand the AI a topic — it investigates the domain and writes a complete, misconception-aware exam.",
          span: ""
        }, {
          icon: <FileUp className="h-5 w-5" />,
          tone: "text-cyan2 bg-cyan2/10",
          title: "Your material, its questions",
          body: "Upload notes or readings and the AI builds the paper strictly from your source — nothing invented.",
          span: ""
        }, {
          icon: <ChartSpline className="h-5 w-5" />,
          tone: "text-mint bg-mint/10",
          title: "Reports that read like art",
          body: "Score rings, mastery radars, momentum curves, time-per-question heat, and peer percentile — a full after-action review.",
          span: "md:col-span-2"
        }].map((f, i) => <Reveal key={i} delay={0.08 * i} className={f.span}>
              <Tilt className="h-full">
                <div className="glass card-hover group relative h-full overflow-hidden rounded-3xl p-7">
                  <div className="absolute -right-10 -top-10 h-36 w-36 rounded-full bg-gradient-to-br from-white/6 to-transparent opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
                  <span className={`mb-5 grid h-12 w-12 place-items-center rounded-2xl ${f.tone}`}>
                    {f.icon}
                  </span>
                  <h3 className="font-display text-xl font-semibold">{f.title}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-dim">{f.body}</p>
                </div>
              </Tilt>
            </Reveal>)}
        </div>
      </section>

      {/* ---------- FLOW ---------- */}
      <section id="flow" className="relative border-y border-white/6 bg-panel/40 py-24">
        <div className="mx-auto max-w-6xl px-5">
          <Reveal>
            <p className="label-caps mb-4 flex items-center gap-2">
              <span className="h-px w-8 bg-cyan2" /> The flow
            </p>
            <h2 className="font-display text-4xl font-bold tracking-tight md:text-6xl">
              Three moves. <span className="serif-accent font-normal text-cyan2">One ritual.</span>
            </h2>
          </Reveal>
          <div className="mt-16 grid gap-12 md:grid-cols-3 md:gap-6">
            {[{
            n: "01",
            icon: <PenLine className="h-5 w-5" />,
            title: "Compose",
            body: "Build the exam by hand — or brief the AI with a topic or your own material. Every option carries its rationale."
          }, {
            n: "02",
            icon: <Timer className="h-5 w-5" />,
            title: "Attempt",
            body: "Students enter a distraction-free arena: live countdown, question palette, flag-and-return, auto-submit at zero."
          }, {
            n: "03",
            icon: <Target className="h-5 w-5" />,
            title: "Understand",
            body: "The report dissects performance per topic, per second, per choice — and explains the why behind every mark."
          }].map((s, i) => <Reveal key={i} delay={0.12 * i}>
                <div className="group relative">
                  <p className="text-outline font-display text-[6rem] font-bold leading-none transition-all duration-500 group-hover:[-webkit-text-stroke-color:rgba(216,255,61,0.6)]">
                    {s.n}
                  </p>
                  <div className="mt-4 flex items-center gap-3">
                    <span className="grid h-10 w-10 place-items-center rounded-xl bg-white/6 text-acid">
                      {s.icon}
                    </span>
                    <h3 className="font-display text-2xl font-semibold">{s.title}</h3>
                  </div>
                  <p className="mt-3 max-w-xs text-sm leading-relaxed text-dim">{s.body}</p>
                </div>
              </Reveal>)}
          </div>
        </div>
      </section>

      {/* ---------- ANALYTICS SHOWCASE ---------- */}
      <section id="analytics" className="mx-auto max-w-6xl px-5 py-24">
        <div className="grid items-center gap-14 lg:grid-cols-2">
          <Reveal>
            <p className="label-caps mb-4 flex items-center gap-2">
              <span className="h-px w-8 bg-vio" /> After-action review
            </p>
            <h2 className="font-display text-4xl font-bold tracking-tight md:text-5xl">
              A report card that thinks like a <span className="serif-accent font-normal text-vio">coach.</span>
            </h2>
            <ul className="mt-8 space-y-5">
              {[{
              icon: <Target className="h-4 w-4 text-acid" />,
              t: "Mastery radar by topic",
              d: "See exactly which concepts held and which collapsed."
            }, {
              icon: <ChartSpline className="h-4 w-4 text-cyan2" />,
              t: "Momentum curve",
              d: "Running accuracy across the paper — where focus dipped."
            }, {
              icon: <Timer className="h-4 w-4 text-rose2" />,
              t: "Time heat per question",
              d: "Slowest questions vs your accuracy, side by side."
            }, {
              icon: <ShieldCheck className="h-4 w-4 text-mint" />,
              t: "Peer percentile",
              d: "Your score stacked against everyone who sat the same exam."
            }].map((it, i) => <Reveal key={i} delay={0.08 * i}>
                  <li className="flex gap-4">
                    <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-white/5">
                      {it.icon}
                    </span>
                    <div>
                      <p className="font-semibold">{it.t}</p>
                      <p className="text-sm text-dim">{it.d}</p>
                    </div>
                  </li>
                </Reveal>)}
            </ul>
          </Reveal>
          <Reveal delay={0.15}>
            <Tilt max={5}>
              <div className="glass-deep relative overflow-hidden rounded-3xl p-7">
                <div className="mb-6 flex items-center justify-between">
                  <div>
                    <p className="label-caps">Performance report</p>
                    <p className="font-display mt-1 text-lg font-bold">Foundations of Modern Physics</p>
                  </div>
                  <span className="rounded-full bg-acid/10 px-3 py-1 font-display text-sm font-bold text-acid">A−</span>
                </div>
                <div className="flex items-center gap-8">
                  <div className="relative h-32 w-32 shrink-0">
                    <svg viewBox="0 0 100 100" className="h-full w-full -rotate-90">
                      <circle cx="50" cy="50" r="42" fill="none" stroke="rgba(255,255,255,0.07)" strokeWidth="9" />
                      <motion.circle cx="50" cy="50" r="42" fill="none" stroke="#D8FF3D" strokeWidth="9" strokeLinecap="round" strokeDasharray={2 * Math.PI * 42} initial={{
                      strokeDashoffset: 2 * Math.PI * 42
                    }} whileInView={{
                      strokeDashoffset: 2 * Math.PI * 42 * 0.14
                    }} viewport={{
                      once: true
                    }} transition={{
                      duration: 1.6,
                      ease: [0.22, 1, 0.36, 1]
                    }} />
                    </svg>
                    <div className="absolute inset-0 grid place-items-center">
                      <span className="font-display text-3xl font-bold text-acid">86%</span>
                    </div>
                  </div>
                  <div className="flex-1 space-y-3">
                    {[{
                    t: "Relativity",
                    v: 100,
                    c: "bg-acid"
                  }, {
                    t: "Quantum",
                    v: 75,
                    c: "bg-cyan2"
                  }, {
                    t: "Thermo",
                    v: 67,
                    c: "bg-vio"
                  }].map((r, i) => <div key={r.t}>
                        <div className="mb-1 flex justify-between text-[11px] font-medium text-dim">
                          <span>{r.t}</span><span>{r.v}%</span>
                        </div>
                        <div className="h-1.5 overflow-hidden rounded-full bg-white/8">
                          <motion.div className={`h-full rounded-full ${r.c}`} initial={{
                        width: 0
                      }} whileInView={{
                        width: `${r.v}%`
                      }} viewport={{
                        once: true
                      }} transition={{
                        duration: 1.1,
                        delay: 0.2 + i * 0.15
                      }} />
                        </div>
                      </div>)}
                  </div>
                </div>
                <div className="mt-6 grid grid-cols-3 gap-3 border-t border-white/8 pt-5">
                  {[{
                  l: "Percentile",
                  v: "92nd"
                }, {
                  l: "Avg / question",
                  v: "48s"
                }, {
                  l: "Rank",
                  v: "3 / 41"
                }].map(s => <div key={s.l} className="text-center">
                      <p className="font-display text-lg font-bold">{s.v}</p>
                      <p className="text-[11px] text-faint">{s.l}</p>
                    </div>)}
                </div>
              </div>
            </Tilt>
          </Reveal>
        </div>
      </section>

      {/* ---------- AI SECTION ---------- */}
      <section id="ai" className="relative overflow-hidden border-t border-white/6 py-24">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(139,124,255,0.12),transparent_60%)]" />
        <div className="relative mx-auto max-w-6xl px-5">
          <Reveal className="text-center">
            <p className="label-caps mb-4 inline-flex items-center gap-2">
              <Sparkles className="h-3.5 w-3.5 text-acid" /> AI Studio
            </p>
            <h2 className="font-display mx-auto max-w-3xl text-4xl font-bold tracking-tight md:text-6xl">
              Your provider. Your key. <span className="serif-accent font-normal text-acid">Infinite question banks.</span>
            </h2>
            <p className="mx-auto mt-5 max-w-xl text-sm leading-relaxed text-dim md:text-base">
              Plug in any OpenAI-compatible API — test the connection, save it, and
              generate exams from a research topic or your own uploaded material.
            </p>
          </Reveal>
          <div className="mt-14 grid gap-4 md:grid-cols-3">
            {[{
            icon: <KeyRound className="h-5 w-5 text-acid" />,
            t: "Bring your own AI",
            d: "Base URL + API key, one click to test, one to save. OpenAI, Groq, OpenRouter, local models — anything OpenAI-compatible."
          }, {
            icon: <FlaskConical className="h-5 w-5 text-vio" />,
            t: "Topic research mode",
            d: "Name any subject — the AI investigates it and drafts a rigorous, misconception-targeted exam in seconds."
          }, {
            icon: <FileUp className="h-5 w-5 text-cyan2" />,
            t: "Material mode",
            d: "Upload lecture notes or readings. Questions come strictly from your source, with teaching-grade explanations."
          }].map((c, i) => <Reveal key={i} delay={0.1 * i}>
                <Tilt className="h-full">
                  <div className="glass card-hover h-full rounded-3xl p-7">
                    <span className="mb-5 grid h-12 w-12 place-items-center rounded-2xl bg-white/5">{c.icon}</span>
                    <h3 className="font-display text-lg font-semibold">{c.t}</h3>
                    <p className="mt-2 text-sm leading-relaxed text-dim">{c.d}</p>
                  </div>
                </Tilt>
              </Reveal>)}
          </div>
        </div>
      </section>

      {/* ---------- FINAL CTA ---------- */}
      <section className="border-t border-white/6 px-5 py-28 text-center">
        <Reveal>
          <h2 className="font-display mx-auto max-w-3xl text-4xl font-bold tracking-tight md:text-6xl">
            The next exam you run could be <span className="serif-accent font-normal text-acid">this good.</span>
          </h2>
          <div className="mt-10 flex flex-col items-center justify-center gap-4 sm:flex-row">
            <Magnetic>
              <button onClick={() => demoLogin("teacher")} disabled={demoBusy !== null} className="btn btn-primary btn-lg group">
                {demoBusy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                Start with a demo <ArrowUpRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5" />
              </button>
            </Magnetic>
            <Magnetic>
              <Link href="/login" className="btn btn-ghost btn-lg">Create a free account</Link>
            </Magnetic>
          </div>
        </Reveal>
      </section>

      <footer className="border-t border-white/6 py-10">
        <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 px-5 text-[13px] text-faint md:flex-row">
          <Logo size="sm" />
          <p>Assessment intelligence for modern classrooms.</p>
          <p>© 2026 Ascend Labs</p>
        </div>
      </footer>
    </div>;
}

import { useEffect, useRef, useState } from "react";
import { useRouter } from "@/lib/rr";
import { AnimatePresence, motion } from "framer-motion";
import { BookMarked, BrainCircuit, CheckCircle2, ChevronDown, Clock3, FileText, FileUp, FlaskConical, Layers, RefreshCw, Save, Settings2, Sparkles, Trash2, Upload, Wand2, X } from "lucide-react";
import { PageIn, Reveal } from "@/components/fx";
import { ProviderForm, GeneratingOverlay } from "@/components/ProviderSetup";
import { Badge, Button, ErrorBanner, Field, Input, Textarea, api, cn } from "@/components/ui";
const STAGES = ["Surveying the domain landscape…", "Identifying core concepts & misconceptions…", "Drafting diagnostic questions…", "Engineering plausible distractors…", "Writing explanations for every option…", "Calibrating difficulty balance…"];
const TOPIC_SUGGESTIONS = ["Photosynthesis & cellular energy", "The French Revolution", "Thermodynamics", "Data structures & algorithms", "Macroeconomics: inflation & policy", "Organic chemistry: reaction mechanisms"];
export function AiClient({
  user
}) {
  const router = useRouter();
  const [provider, setProvider] = useState({
    kind: "loading"
  });
  const [reconfigure, setReconfigure] = useState(false);
  const [mode, setMode] = useState("topic");
  const [topic, setTopic] = useState("");
  const [material, setMaterial] = useState("");
  const [fileName, setFileName] = useState(null);
  const [count, setCount] = useState(8);
  const [difficulty, setDifficulty] = useState("intermediate");
  const [duration, setDuration] = useState(15);
  const [generating, setGenerating] = useState(false);
  const [stageIdx, setStageIdx] = useState(0);
  const [draft, setDraft] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const fileRef = useRef(null);
  async function loadProvider() {
    try {
      const p = await api("/api/ai/provider");
      setProvider(p.configured ? {
        kind: "ready",
        baseUrl: p.baseUrl,
        model: p.model,
        maskedKey: p.maskedKey
      } : {
        kind: "none"
      });
    } catch {
      setProvider({
        kind: "none"
      });
    }
  }
  useEffect(() => {
    loadProvider();
  }, []);
  useEffect(() => {
    if (!generating) return;
    const iv = setInterval(() => setStageIdx(i => (i + 1) % STAGES.length), 2600);
    return () => clearInterval(iv);
  }, [generating]);
  async function generate() {
    setError(null);
    setGenerating(true);
    setStageIdx(0);
    try {
      const res = await api("/api/ai/generate", {
        method: "POST",
        body: JSON.stringify({
          mode,
          topic,
          material,
          count,
          difficulty,
          durationMinutes: duration
        })
      });
      setDraft(res.draft);
      setTimeout(() => {
        document.getElementById("draft-preview")?.scrollIntoView({
          behavior: "smooth"
        });
      }, 100);
    } catch (e) {
      setError(e.message);
    } finally {
      setGenerating(false);
    }
  }
  async function saveDraft() {
    if (!draft) return;
    setSaving(true);
    setError(null);
    try {
      const res = await api("/api/assessments", {
        method: "POST",
        body: JSON.stringify({
          ...draft,
          durationMinutes: draft.durationMinutes,
          isAiGenerated: true
        })
      });
      router.push(`/assessments/${res.id}`);
    } catch (e) {
      setError(e.message);
      setSaving(false);
    }
  }
  async function removeProvider() {
    await api("/api/ai/provider", {
      method: "DELETE"
    });
    setProvider({
      kind: "none"
    });
    setDraft(null);
  }
  function onFile(e) {
    const f = e.target.files?.[0];
    if (!f) return;
    setFileName(f.name);
    const reader = new FileReader();
    reader.onload = () => {
      setMaterial(String(reader.result ?? "").slice(0, 16000));
    };
    reader.readAsText(f);
  }

  /* ---------- onboarding: provider not connected ---------- */
  if (provider.kind !== "ready" || reconfigure) {
    return <PageIn>
        <Reveal>
          <p className="label-caps mb-2 flex items-center gap-2">
            <Sparkles className="h-3.5 w-3.5 text-acid" /> AI Studio · step 1
          </p>
          <h1 className="font-display max-w-2xl text-4xl font-bold tracking-tight md:text-5xl">
            Connect your <span className="serif-accent font-normal text-acid">AI provider.</span>
          </h1>
          <p className="mt-3 max-w-xl text-sm leading-relaxed text-dim">
            Ascend never touches a model bill — you plug in your own OpenAI-compatible
            endpoint, test it live, and every generation flows through your key.
          </p>
        </Reveal>
        <Reveal delay={0.12}>
          <div className="glass-deep relative mt-10 max-w-xl overflow-hidden rounded-3xl p-7 md:p-9">
            <div className="pointer-events-none absolute -right-20 -top-20 h-56 w-56 rounded-full bg-vio/15 blur-3xl" />
            {provider.kind === "ready" && reconfigure && <button onClick={() => setReconfigure(false)} className="absolute right-5 top-5 grid h-8 w-8 place-items-center rounded-full border border-white/10 text-dim hover:bg-white/6 hover:text-ink">
                <X className="h-4 w-4" />
              </button>}
            {provider.kind === "loading" ? <p className="py-10 text-center text-sm text-dim">Checking your connection…</p> : <ProviderForm initialBaseUrl={provider.kind === "ready" ? provider.baseUrl : ""} initialModel={provider.kind === "ready" ? provider.model : "gpt-4o-mini"} maskedKey={provider.kind === "ready" ? provider.maskedKey : undefined} onSaved={() => {
            loadProvider();
            setReconfigure(false);
          }} onDelete={provider.kind === "ready" ? removeProvider : undefined} />}
          </div>
        </Reveal>
      </PageIn>;
  }

  /* ---------- studio ---------- */
  return <PageIn>
      <Reveal>
        <div className="flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="label-caps mb-2 flex items-center gap-2">
              <Sparkles className="h-3.5 w-3.5 text-acid" /> AI Studio
            </p>
            <h1 className="font-display text-4xl font-bold tracking-tight md:text-5xl">
              Brief the <span className="serif-accent font-normal text-acid">examiner.</span>
            </h1>
          </div>
          <button onClick={() => setReconfigure(true)} className="flex items-center gap-2 self-start rounded-full border border-mint/25 bg-mint/8 px-4 py-2 text-[12px] font-medium text-mint transition-all hover:bg-mint/12 md:self-auto">
            <CheckCircle2 className="h-3.5 w-3.5" />
            <span className="font-mono">{provider.maskedKey}</span> · {provider.model}
            <Settings2 className="h-3.5 w-3.5 opacity-70" />
          </button>
        </div>
      </Reveal>

      <div className="mt-6"><ErrorBanner message={error} /></div>

      {/* mode tabs */}
      <Reveal delay={0.08}>
        <div className="mt-8 grid grid-cols-2 gap-2 md:max-w-md">
          {[{
          key: "topic",
          label: "Topic research",
          icon: <BrainCircuit className="h-4 w-4" />,
          d: "AI investigates"
        }, {
          key: "material",
          label: "From material",
          icon: <FileText className="h-4 w-4" />,
          d: "your content"
        }].map(m => <button key={m.key} onClick={() => setMode(m.key)} className={cn("rounded-2xl border p-4 text-left transition-all duration-300", mode === m.key ? "border-acid/50 bg-acid/8" : "border-white/10 bg-white/2 hover:border-white/25")}>
              <span className={cn("mb-2 grid h-9 w-9 place-items-center rounded-xl", mode === m.key ? "bg-acid text-[#0a0a10]" : "bg-white/8 text-dim")}>
                {m.icon}
              </span>
              <p className="font-display text-sm font-bold">{m.label}</p>
              <p className="text-[11px] text-faint">{m.d}</p>
            </button>)}
        </div>
      </Reveal>

      {/* brief form */}
      <Reveal delay={0.12}>
        <section className="glass mt-5 rounded-3xl p-6 md:p-8">
          <AnimatePresence mode="wait">
            {mode === "topic" ? <motion.div key="topic" initial={{
            opacity: 0,
            x: -18
          }} animate={{
            opacity: 1,
            x: 0
          }} exit={{
            opacity: 0,
            x: 18
          }} transition={{
            duration: 0.35
          }}>
                <Field label="Topic to research" hint="the AI studies it, then writes the paper">
                  <Input value={topic} onChange={e => setTopic(e.target.value)} placeholder="e.g. Bayesian statistics, World War II, Photosynthesis…" className="font-display text-[15px] font-medium" />
                </Field>
                <div className="mt-3.5 flex flex-wrap gap-2">
                  {TOPIC_SUGGESTIONS.map(t => <button key={t} onClick={() => setTopic(t)} className="rounded-full border border-white/10 bg-white/2 px-3 py-1.5 text-[12px] text-dim transition-all hover:border-acid/40 hover:text-acid">
                      {t}
                    </button>)}
                </div>
              </motion.div> : <motion.div key="material" initial={{
            opacity: 0,
            x: 18
          }} animate={{
            opacity: 1,
            x: 0
          }} exit={{
            opacity: 0,
            x: -18
          }} transition={{
            duration: 0.35
          }}>
                <div className="mb-4 flex flex-wrap items-center gap-3">
                  <input ref={fileRef} type="file" accept=".txt,.md,.csv,.json,.text,.log" onChange={onFile} className="hidden" />
                  <Button variant="ghost" size="sm" onClick={() => fileRef.current?.click()} icon={<Upload className="h-3.5 w-3.5" />}>
                    Upload material
                  </Button>
                  {fileName && <span className="flex items-center gap-1.5 rounded-full bg-cyan2/10 px-3 py-1.5 text-[12px] text-cyan2">
                      <FileUp className="h-3.5 w-3.5" /> {fileName}
                    </span>}
                  <span className="text-[12px] text-faint">.txt / .md / .csv · up to 16k characters</span>
                </div>
                <Field label="Source material" hint={`${material.length} / 16000 characters`}>
                  <Textarea value={material} onChange={e => setMaterial(e.target.value.slice(0, 16000))} placeholder="Paste lecture notes, a chapter, an article, documentation — the AI will question only from this text." className="min-h-[200px] font-mono text-[13px] leading-relaxed" />
                </Field>
              </motion.div>}
          </AnimatePresence>

          <div className="mt-7 grid gap-7 border-t border-white/8 pt-7 md:grid-cols-3">
            <Field label={`Questions — ${count}`}>
              <input type="range" min={3} max={20} value={count} onChange={e => {
              setCount(Number(e.target.value));
              setDuration(Math.max(5, Math.round(Number(e.target.value) * 1.5)));
              e.target.style.setProperty("--fill", `${(Number(e.target.value) - 3) / 17 * 100}%`);
            }} style={{
              ["--fill"]: `${(count - 3) / 17 * 100}%`
            }} className="mt-3 w-full" />
            </Field>
            <Field label="Difficulty">
              <div className="mt-1.5 grid grid-cols-3 gap-1 rounded-2xl border border-white/10 bg-white/3 p-1">
                {["foundational", "intermediate", "advanced"].map(d => <button key={d} onClick={() => setDifficulty(d)} className={cn("rounded-xl py-2 text-[11.5px] font-semibold capitalize transition-all duration-300", difficulty === d ? "bg-acid text-[#0a0a10]" : "text-dim hover:text-ink")}>
                    {d.slice(0, 4) === "foun" ? "Basic" : d === "intermediate" ? "Medium" : "Hard"}
                  </button>)}
              </div>
            </Field>
            <Field label={`Time limit — ${duration} min`}>
              <div className="mt-3 flex items-center gap-3">
                <Clock3 className="h-4 w-4 text-faint" />
                <input type="range" min={5} max={120} step={5} value={duration} onChange={e => {
                setDuration(Number(e.target.value));
                e.target.style.setProperty("--fill", `${(Number(e.target.value) - 5) / 115 * 100}%`);
              }} style={{
                ["--fill"]: `${(duration - 5) / 115 * 100}%`
              }} className="w-full" />
              </div>
            </Field>
          </div>

          <div className="mt-8 flex flex-wrap items-center gap-4">
            <Button size="lg" onClick={generate} loading={generating} icon={<Wand2 className="h-4 w-4" />}>
              {draft ? "Regenerate assessment" : "Generate assessment"}
            </Button>
            <p className="flex items-center gap-2 text-[12px] text-faint">
              <FlaskConical className="h-3.5 w-3.5 text-vio" />
              Drafts are editable after generation — nothing is published until you save.
            </p>
          </div>
        </section>
      </Reveal>

      {/* ---------- draft preview ---------- */}
      <AnimatePresence>
        {draft && <motion.section id="draft-preview" initial={{
        opacity: 0,
        y: 40
      }} animate={{
        opacity: 1,
        y: 0
      }} exit={{
        opacity: 0
      }} transition={{
        duration: 0.7,
        ease: [0.22, 1, 0.36, 1]
      }} className="mt-12">
            <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
              <div>
                <p className="label-caps mb-1.5 flex items-center gap-2">
                  <Sparkles className="h-3.5 w-3.5 text-mint" /> Draft ready
                </p>
                <h2 className="font-display text-3xl font-bold tracking-tight">Review & publish</h2>
              </div>
              <div className="flex gap-2">
                <Button variant="ghost" onClick={generate} loading={generating} icon={<RefreshCw className="h-4 w-4" />}>
                  Regenerate
                </Button>
                <Button onClick={saveDraft} loading={saving} icon={<Save className="h-4 w-4" />}>
                  Save assessment
                </Button>
              </div>
            </div>

            <div className="glass rounded-3xl p-6 md:p-8">
              <div className="grid gap-5 md:grid-cols-[1fr_180px]">
                <Field label="Title">
                  <Input value={draft.title} onChange={e => setDraft({
                ...draft,
                title: e.target.value
              })} className="font-display text-lg font-bold" />
                </Field>
                <Field label="Subject">
                  <Input value={draft.subject} onChange={e => setDraft({
                ...draft,
                subject: e.target.value
              })} />
                </Field>
                <Field label="Description" className="md:col-span-2">
                  <Textarea value={draft.description} onChange={e => setDraft({
                ...draft,
                description: e.target.value
              })} className="!min-h-[60px]" />
                </Field>
              </div>
              <div className="mt-4 flex items-center gap-5 text-[12px] text-dim">
                <span className="flex items-center gap-1.5"><Layers className="h-3.5 w-3.5 text-acid" /> {draft.questions.length} questions</span>
                <span className="flex items-center gap-1.5"><Clock3 className="h-3.5 w-3.5 text-cyan2" /> {draft.durationMinutes} min</span>
                <span className="flex items-center gap-1.5"><BookMarked className="h-3.5 w-3.5 text-vio" /> every option explained</span>
              </div>
            </div>

            <div className="mt-5 space-y-4">
              {draft.questions.map((q, qi) => <DraftQuestionCard key={qi} index={qi} question={q} onDelete={() => setDraft({
            ...draft,
            questions: draft.questions.filter((_, i) => i !== qi)
          })} />)}
            </div>

            <div className="sticky bottom-5 mt-8 flex justify-center">
              <div className="glass-deep flex items-center gap-4 rounded-full py-2.5 pl-6 pr-2.5 shadow-2xl">
                <p className="text-[13px] text-dim">
                  <span className="font-display font-bold text-ink">{draft.questions.length}</span> questions ready
                </p>
                <Button onClick={saveDraft} loading={saving} icon={<Save className="h-4 w-4" />}>
                  Save & publish
                </Button>
              </div>
            </div>
          </motion.section>}
      </AnimatePresence>

      <GeneratingOverlay visible={generating} stage={STAGES[stageIdx]} />
    </PageIn>;
}
function DraftQuestionCard({
  index,
  question,
  onDelete
}) {
  const [open, setOpen] = useState(false);
  return <div className="glass rounded-3xl p-6">
      <div className="flex items-start gap-4">
        <span className="font-display grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-mint/15 text-[13px] font-bold text-mint">
          {index + 1}
        </span>
        <div className="min-w-0 flex-1">
          <div className="mb-1.5 flex flex-wrap items-center gap-2">
            <Badge tone="vio">{question.topic}</Badge>
          </div>
          <p className="font-display text-[15px] font-semibold leading-snug">{question.text}</p>
          <ul className="mt-4 grid gap-2 sm:grid-cols-2">
            {question.options.map((o, oi) => <li key={oi} className={cn("flex items-start gap-2.5 rounded-xl border px-3.5 py-2.5 text-[13px] leading-relaxed", o.correct ? "border-acid/45 bg-acid/8" : "border-white/8 bg-white/2 text-dim")}>
                <span className={cn("font-display mt-0.5 text-[11px] font-bold", o.correct ? "text-acid" : "text-faint")}>
                  {String.fromCharCode(65 + oi)}
                </span>
                {o.text}
                {o.correct && <CheckCircle2 className="ml-auto mt-0.5 h-4 w-4 shrink-0 text-acid" />}
              </li>)}
          </ul>
          <button onClick={() => setOpen(!open)} className="mt-3 flex items-center gap-1.5 text-[12px] font-semibold text-dim transition-colors hover:text-ink">
            Explanations
            <ChevronDown className={cn("h-3.5 w-3.5 transition-transform", open && "rotate-180")} />
          </button>
          <AnimatePresence initial={false}>
            {open && <motion.div initial={{
            height: 0,
            opacity: 0
          }} animate={{
            height: "auto",
            opacity: 1
          }} exit={{
            height: 0,
            opacity: 0
          }} transition={{
            duration: 0.35,
            ease: [0.22, 1, 0.36, 1]
          }} className="overflow-hidden">
                <div className="mt-3 space-y-2">
                  {question.explanation && <p className="rounded-xl border border-acid/25 bg-acid/5 px-3.5 py-2.5 text-[12.5px] leading-relaxed">
                      <span className="font-semibold text-acid">Correct — </span>
                      {question.explanation}
                    </p>}
                  {question.options.filter(o => !o.correct && o.whyWrong).map((o, i) => <p key={i} className="rounded-xl border border-white/8 bg-white/2 px-3.5 py-2.5 text-[12.5px] leading-relaxed text-dim">
                      <span className="font-semibold text-rose2">✕ {o.text.slice(0, 44)}{o.text.length > 44 ? "…" : ""} — </span>
                      {o.whyWrong}
                    </p>)}
                </div>
              </motion.div>}
          </AnimatePresence>
        </div>
        <button onClick={onDelete} className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-white/10 text-faint transition-all hover:border-rose2/40 hover:bg-rose2/10 hover:text-rose2" title="Remove question">
          <Trash2 className="h-3.5 w-3.5" />
        </button>
      </div>
    </div>;
}

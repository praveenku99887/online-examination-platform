import { useState } from "react";
import { useRouter } from "@/lib/rr";
import { AnimatePresence, motion } from "framer-motion";
import { ArrowDown, ArrowUp, CheckCircle2, Clock3, FilePlus2, Plus, Save, Trash2 } from "lucide-react";
import { PageIn, Reveal } from "@/components/fx";
import { Button, ErrorBanner, Field, Input, Textarea, api, cn } from "@/components/ui";
let uid = 0;
const nid = () => `k${++uid}`;
function blankOption() {
  return {
    key: nid(),
    text: "",
    correct: false,
    whyWrong: ""
  };
}
function blankQuestion(correctCount = 0) {
  void correctCount;
  return {
    key: nid(),
    text: "",
    topic: "",
    explanation: "",
    options: [{
      ...blankOption(),
      correct: true
    }, blankOption(), blankOption(), blankOption()]
  };
}
export function Builder() {
  const router = useRouter();
  const [title, setTitle] = useState("");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [duration, setDuration] = useState(30);
  const [questions, setQuestions] = useState([blankQuestion()]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  function patchQuestion(key, patch) {
    setQuestions(qs => qs.map(q => q.key === key ? {
      ...q,
      ...patch
    } : q));
  }
  function patchOption(qKey, oKey, patch) {
    setQuestions(qs => qs.map(q => q.key === qKey ? {
      ...q,
      options: q.options.map(o => o.key === oKey ? {
        ...o,
        ...patch
      } : o)
    } : q));
  }
  function setCorrect(qKey, oKey) {
    setQuestions(qs => qs.map(q => q.key === qKey ? {
      ...q,
      options: q.options.map(o => ({
        ...o,
        correct: o.key === oKey
      }))
    } : q));
  }
  function move(index, dir) {
    setQuestions(qs => {
      const next = [...qs];
      const j = index + dir;
      if (j < 0 || j >= next.length) return next;
      [next[index], next[j]] = [next[j], next[index]];
      return next;
    });
  }
  async function save() {
    setSaving(true);
    setError(null);
    try {
      const payload = {
        title,
        description,
        subject: subject || "General",
        durationMinutes: duration,
        questions: questions.map(q => ({
          text: q.text,
          topic: q.topic || "General",
          explanation: q.explanation,
          options: q.options.map(o => ({
            text: o.text,
            correct: o.correct,
            whyWrong: o.whyWrong
          }))
        }))
      };
      const res = await api("/api/assessments", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      router.push(`/assessments/${res.id}`);
    } catch (e) {
      setError(e.message);
      setSaving(false);
      window.scrollTo({
        top: 0,
        behavior: "smooth"
      });
    }
  }
  return <PageIn>
      <Reveal>
        <p className="label-caps mb-2 flex items-center gap-2">
          <FilePlus2 className="h-3.5 w-3.5 text-acid" /> Assessment studio
        </p>
        <h1 className="font-display text-4xl font-bold tracking-tight md:text-5xl">
          Compose an <span className="serif-accent font-normal text-acid">assessment.</span>
        </h1>
        <p className="mt-2 max-w-xl text-sm leading-relaxed text-dim">
          Every question carries its own pedagogy — explain the right answer, and
          explain why each wrong answer fails.
        </p>
      </Reveal>

      <div className="mt-8">
        <ErrorBanner message={error} />
      </div>

      {/* meta */}
      <Reveal delay={0.08}>
        <section className="glass mt-8 rounded-3xl p-6 md:p-8">
          <div className="grid gap-5 md:grid-cols-2">
            <Field label="Title" className="md:col-span-2">
              <Input value={title} onChange={e => setTitle(e.target.value)} placeholder="e.g. Cellular Respiration — Midterm Check" className="font-display text-lg font-semibold" />
            </Field>
            <Field label="Subject">
              <Input value={subject} onChange={e => setSubject(e.target.value)} placeholder="Biology, History, Physics…" />
            </Field>
            <Field label={`Time limit — ${duration} min`} hint="students see a live countdown">
              <div className="flex items-center gap-4 pt-1">
                <Clock3 className="h-4 w-4 shrink-0 text-faint" />
                <input type="range" min={5} max={180} step={5} value={duration} onChange={e => {
                setDuration(Number(e.target.value));
                e.target.style.setProperty("--fill", `${(Number(e.target.value) - 5) / 175 * 100}%`);
              }} style={{
                ["--fill"]: `${(duration - 5) / 175 * 100}%`
              }} className="w-full" />
                <span className="font-display w-16 text-right text-lg font-bold text-acid">
                  {duration}m
                </span>
              </div>
            </Field>
            <Field label="Description" className="md:col-span-2" hint="shown to students before they start">
              <Textarea value={description} onChange={e => setDescription(e.target.value)} placeholder="What does this assessment cover? Any rules or notes?" />
            </Field>
          </div>
        </section>
      </Reveal>

      {/* questions */}
      <div className="mt-8 space-y-5">
        <AnimatePresence initial={false}>
          {questions.map((q, qi) => <motion.section key={q.key} layout initial={{
          opacity: 0,
          y: 30,
          scale: 0.98
        }} animate={{
          opacity: 1,
          y: 0,
          scale: 1
        }} exit={{
          opacity: 0,
          scale: 0.96,
          transition: {
            duration: 0.25
          }
        }} transition={{
          duration: 0.5,
          ease: [0.22, 1, 0.36, 1]
        }} className="glass group relative overflow-hidden rounded-3xl p-6 md:p-7">
              <div className="absolute left-0 top-0 h-full w-1 bg-gradient-to-b from-acid/70 to-vio/70 opacity-60" />
              <div className="mb-5 flex items-center justify-between gap-3">
                <div className="flex items-center gap-3">
                  <span className="font-display grid h-9 w-9 place-items-center rounded-xl bg-acid text-sm font-bold text-[#0a0a10]">
                    {qi + 1}
                  </span>
                  <Input value={q.topic} onChange={e => patchQuestion(q.key, {
                topic: e.target.value
              })} placeholder="Topic tag (e.g. Mitosis)" className="w-52 !py-2 text-[12px]" />
                </div>
                <div className="flex items-center gap-1.5 opacity-60 transition-opacity group-hover:opacity-100">
                  <button onClick={() => move(qi, -1)} disabled={qi === 0} className="icon-btn" title="Move up">
                    <ArrowUp className="h-4 w-4" />
                  </button>
                  <button onClick={() => move(qi, 1)} disabled={qi === questions.length - 1} className="icon-btn" title="Move down">
                    <ArrowDown className="h-4 w-4" />
                  </button>
                  <button onClick={() => setQuestions(qs => qs.filter(x => x.key !== q.key))} disabled={questions.length === 1} className="icon-btn hover:!border-rose2/40 hover:!bg-rose2/10 hover:!text-rose2" title="Remove question">
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>

              <Field label="Question">
                <Textarea value={q.text} onChange={e => patchQuestion(q.key, {
              text: e.target.value
            })} placeholder="Ask something precise and unambiguous…" />
              </Field>

              <div className="mt-5 space-y-3">
                {q.options.map((o, oi) => <div key={o.key} className={cn("rounded-2xl border p-3.5 transition-all duration-300", o.correct ? "border-acid/45 bg-acid/6" : "border-white/8 bg-white/2 hover:border-white/16")}>
                    <div className="flex items-start gap-3">
                      <button type="button" onClick={() => setCorrect(q.key, o.key)} title="Mark as correct" className={cn("mt-1 grid h-6 w-6 shrink-0 place-items-center rounded-full border transition-all duration-300", o.correct ? "border-acid bg-acid text-[#0a0a10]" : "border-white/20 text-transparent hover:border-acid/60")}>
                        <CheckCircle2 className="h-4 w-4" />
                      </button>
                      <div className="flex-1 space-y-2.5">
                        <Input value={o.text} onChange={e => patchOption(q.key, o.key, {
                    text: e.target.value
                  })} placeholder={`Option ${String.fromCharCode(65 + oi)}`} className="!border-transparent !bg-transparent !px-2 !py-1.5 font-medium" />
                        {o.correct ? <p className="px-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-acid">
                            Correct answer
                          </p> : <Input value={o.whyWrong} onChange={e => patchOption(q.key, o.key, {
                    whyWrong: e.target.value
                  })} placeholder="Why is this option wrong? (shown in the report)" className="!py-2 text-[12px] italic text-dim" />}
                      </div>
                      {q.options.length > 2 && <button onClick={() => patchQuestion(q.key, {
                  options: q.options.filter(x => x.key !== o.key)
                })} className="mt-1 text-faint transition-colors hover:text-rose2" title="Remove option">
                          <Trash2 className="h-4 w-4" />
                        </button>}
                    </div>
                  </div>)}
                {q.options.length < 6 && <button onClick={() => patchQuestion(q.key, {
              options: [...q.options, blankOption()]
            })} className="flex w-full items-center justify-center gap-2 rounded-2xl border border-dashed border-white/15 py-3 text-[13px] font-medium text-dim transition-all hover:border-acid/50 hover:text-acid">
                    <Plus className="h-4 w-4" /> Add option
                  </button>}
              </div>

              <div className="mt-5">
                <Field label="Why the correct answer is correct" hint="the teaching moment">
                  <Textarea value={q.explanation} onChange={e => patchQuestion(q.key, {
                explanation: e.target.value
              })} placeholder="Explain the concept behind the correct option…" className="!min-h-[70px] text-[13px]" />
                </Field>
              </div>
            </motion.section>)}
        </AnimatePresence>

        <motion.button layout onClick={() => setQuestions(qs => [...qs, blankQuestion()])} className="flex w-full items-center justify-center gap-2 rounded-3xl border border-dashed border-white/15 py-6 font-display text-sm font-semibold text-dim transition-all hover:border-acid/60 hover:bg-acid/5 hover:text-acid">
          <Plus className="h-4 w-4" /> Add another question
        </motion.button>
      </div>

      {/* sticky footer */}
      <div className="fixed inset-x-0 bottom-0 z-40 border-t border-white/8 bg-bg/80 backdrop-blur-xl">
        <div className="mx-auto flex max-w-4xl items-center justify-between gap-4 px-5 py-3.5">
          <p className="text-[13px] text-dim">
            <span className="font-display font-bold text-ink">{questions.length}</span> questions ·{" "}
            <span className="font-display font-bold text-ink">{duration}</span> min · publish instantly
          </p>
          <Button onClick={save} loading={saving} size="lg" icon={<Save className="h-4 w-4" />}>
            Publish assessment
          </Button>
        </div>
      </div>
    </PageIn>;
}

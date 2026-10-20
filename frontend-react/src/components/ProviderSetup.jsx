import { useEffect, useState } from "react";
import { AnimatePresence, motion } from "framer-motion";
import { Cable, CheckCircle2, FlaskConical, KeyRound, Link2, Loader2, Save, Sparkles, Trash2, X, XCircle } from "lucide-react";
import { Button, ErrorBanner, Field, Input, api, cn } from "@/components/ui";
export function ProviderForm({
  initialBaseUrl = "",
  initialModel = "gpt-4o-mini",
  maskedKey,
  onSaved,
  onDelete,
  compact = false
}) {
  const [baseUrl, setBaseUrl] = useState(initialBaseUrl);
  const [apiKey, setApiKey] = useState("");
  const [model, setModel] = useState(initialModel);
  const [test, setTest] = useState({
    kind: "idle"
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  useEffect(() => {
    setBaseUrl(initialBaseUrl);
    setModel(initialModel || "gpt-4o-mini");
  }, [initialBaseUrl, initialModel]);
  async function runTest() {
    if (!baseUrl.trim() || !apiKey.trim()) {
      setError("Enter both the base URL and API key before testing.");
      return;
    }
    setError(null);
    setTest({
      kind: "testing"
    });
    try {
      const res = await api("/api/ai/test", {
        method: "POST",
        body: JSON.stringify({
          baseUrl,
          apiKey,
          model
        })
      });
      setTest(res.ok ? {
        kind: "ok",
        detail: res.detail,
        latencyMs: res.latencyMs
      } : {
        kind: "fail",
        detail: res.detail
      });
    } catch (e) {
      setTest({
        kind: "fail",
        detail: e.message
      });
    }
  }
  async function save() {
    if (!baseUrl.trim() || !apiKey.trim()) {
      setError("Base URL and API key are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await api("/api/ai/provider", {
        method: "POST",
        body: JSON.stringify({
          baseUrl,
          apiKey,
          model
        })
      });
      onSaved();
    } catch (e) {
      setError(e.message);
    } finally {
      setSaving(false);
    }
  }
  return <div className={cn("space-y-5", compact && "space-y-4")}>
      <ErrorBanner message={error} />
      {maskedKey && <div className="flex items-center justify-between rounded-2xl border border-mint/25 bg-mint/8 px-4 py-3">
          <p className="flex items-center gap-2 text-[13px] text-mint">
            <CheckCircle2 className="h-4 w-4" />
            Connected · <span className="font-mono">{maskedKey}</span> · {initialModel}
          </p>
          {onDelete && <button onClick={onDelete} className="flex items-center gap-1 text-[12px] text-faint transition-colors hover:text-rose2">
              <Trash2 className="h-3.5 w-3.5" /> Remove
            </button>}
        </div>}
      <Field label="Base URL" hint="OpenAI-compatible endpoint">
        <div className="relative">
          <Link2 className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-faint" />
          <Input value={baseUrl} onChange={e => setBaseUrl(e.target.value)} placeholder="https://api.openai.com/v1" className="pl-11 font-mono text-[13px]" spellCheck={false} />
        </div>
      </Field>
      <Field label="API Key" hint="stored securely, never shared">
        <div className="relative">
          <KeyRound className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-faint" />
          <Input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)} placeholder={maskedKey ? "••••••••••••  (enter to replace)" : "sk-••••••••••••••••"} className="pl-11 font-mono text-[13px]" spellCheck={false} />
        </div>
      </Field>
      <Field label="Model" hint="optional">
        <Input value={model} onChange={e => setModel(e.target.value)} placeholder="gpt-4o-mini" className="font-mono text-[13px]" spellCheck={false} />
      </Field>

      <AnimatePresence mode="wait">
        {test.kind === "ok" && <motion.div initial={{
        opacity: 0,
        y: 6
      }} animate={{
        opacity: 1,
        y: 0
      }} exit={{
        opacity: 0
      }} className="flex items-start gap-2.5 rounded-2xl border border-mint/25 bg-mint/8 px-4 py-3 text-[13px] text-mint">
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              Provider is live — {test.latencyMs}ms. {test.detail}
            </span>
          </motion.div>}
        {test.kind === "fail" && <motion.div initial={{
        opacity: 0,
        y: 6
      }} animate={{
        opacity: 1,
        y: 0
      }} exit={{
        opacity: 0
      }} className="flex items-start gap-2.5 rounded-2xl border border-rose2/30 bg-rose2/10 px-4 py-3 text-[13px] text-rose2">
            <XCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{test.detail}</span>
          </motion.div>}
      </AnimatePresence>

      <div className="flex flex-col gap-3 sm:flex-row">
        <Button variant="ghost" size="lg" className="flex-1" onClick={runTest} loading={test.kind === "testing"} icon={<FlaskConical className="h-4 w-4" />}>
          Test provider
        </Button>
        <Button size="lg" className="flex-1" onClick={save} loading={saving} icon={<Save className="h-4 w-4" />}>
          Save provider
        </Button>
      </div>
      <p className="text-center text-[12px] leading-relaxed text-faint">
        Works with any OpenAI-compatible API — OpenAI, Anthropic proxies, Azure,
        OpenRouter, Groq, local Ollama, and more. Your key is used only for your
        own generations.
      </p>
    </div>;
}
export function ProviderModal({
  open,
  onClose,
  onSaved
}) {
  return <AnimatePresence>
      {open && <motion.div initial={{
      opacity: 0
    }} animate={{
      opacity: 1
    }} exit={{
      opacity: 0
    }} className="fixed inset-0 z-[100] grid place-items-center overflow-y-auto bg-black/70 p-4 backdrop-blur-sm" onClick={onClose}>
          <motion.div initial={{
        opacity: 0,
        y: 34,
        scale: 0.96
      }} animate={{
        opacity: 1,
        y: 0,
        scale: 1
      }} exit={{
        opacity: 0,
        y: 20,
        scale: 0.97
      }} transition={{
        type: "spring",
        stiffness: 260,
        damping: 24
      }} onClick={e => e.stopPropagation()} className="glass-deep relative w-full max-w-lg rounded-3xl p-7 shadow-2xl">
            <button onClick={onClose} className="absolute right-5 top-5 grid h-8 w-8 place-items-center rounded-full border border-white/10 text-dim transition-colors hover:bg-white/6 hover:text-ink">
              <X className="h-4 w-4" />
            </button>
            <div className="mb-6 flex items-center gap-3">
              <span className="grid h-11 w-11 place-items-center rounded-2xl bg-vio/15 text-vio">
                <Sparkles className="h-5 w-5" />
              </span>
              <div>
                <h3 className="font-display text-lg font-bold">Connect your AI provider</h3>
                <p className="text-[13px] text-dim">
                  You bring the brain — we architect the assessment.
                </p>
              </div>
            </div>
            <ProviderForm onSaved={() => {
          onClose();
          onSaved();
        }} />
            <div className="mt-5 flex items-center gap-2 rounded-2xl border border-white/8 bg-white/3 px-4 py-2.5 text-[12px] text-faint">
              <Cable className="h-3.5 w-3.5 shrink-0 text-cyan2" />
              Keys are stored server-side against your account and only used when you
              request a generation.
            </div>
          </motion.div>
        </motion.div>}
    </AnimatePresence>;
}
export function GeneratingOverlay({
  visible,
  stage
}) {
  return <AnimatePresence>
      {visible && <motion.div initial={{
      opacity: 0
    }} animate={{
      opacity: 1
    }} exit={{
      opacity: 0
    }} className="fixed inset-0 z-[100] grid place-items-center bg-black/80 backdrop-blur-md">
          <div className="flex flex-col items-center gap-8">
            <div className="relative h-28 w-28">
              <motion.div className="absolute inset-0 rounded-full border border-vio/40" animate={{
            scale: [1, 1.55],
            opacity: [0.8, 0]
          }} transition={{
            duration: 1.8,
            repeat: Infinity,
            ease: "easeOut"
          }} />
              <motion.div className="absolute inset-0 rounded-full border border-cyan2/40" animate={{
            scale: [1, 1.55],
            opacity: [0.8, 0]
          }} transition={{
            duration: 1.8,
            repeat: Infinity,
            ease: "easeOut",
            delay: 0.6
          }} />
              <motion.div className="absolute inset-0 rounded-full border border-acid/40" animate={{
            scale: [1, 1.55],
            opacity: [0.8, 0]
          }} transition={{
            duration: 1.8,
            repeat: Infinity,
            ease: "easeOut",
            delay: 1.2
          }} />
              <div className="absolute inset-0 grid place-items-center">
                <Loader2 className="h-9 w-9 animate-spin text-acid" />
              </div>
            </div>
            <div className="text-center">
              <p className="font-display text-xl font-semibold">Composing your assessment</p>
              <AnimatePresence mode="wait">
                <motion.p key={stage} initial={{
              opacity: 0,
              y: 8
            }} animate={{
              opacity: 1,
              y: 0
            }} exit={{
              opacity: 0,
              y: -8
            }} className="mt-2 text-sm text-dim">
                  {stage}
                </motion.p>
              </AnimatePresence>
            </div>
          </div>
        </motion.div>}
    </AnimatePresence>;
}

import { Suspense, useState } from "react";
import Link from "@/lib/rr";
import { useRouter } from "@/lib/rr";
import { AnimatePresence, motion } from "framer-motion";
import { ArrowLeft, ArrowRight, BookOpenCheck, GraduationCap, Loader2, Lock, Mail, User } from "lucide-react";
import { AuroraCanvas } from "@/components/fx";
import { Button, ErrorBanner, Field, Input, Logo, api, cn } from "@/components/ui";
function AuthInner() {
  const router = useRouter();
  const [mode, setMode] = useState("signin");
  const [role, setRole] = useState("student");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [demoBusy, setDemoBusy] = useState(null);
  const [error, setError] = useState(null);
  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (mode === "signup") {
        await api("/api/auth/signup", {
          method: "POST",
          body: JSON.stringify({
            name,
            email,
            password,
            role
          })
        });
      } else {
        await api("/api/auth/login", {
          method: "POST",
          body: JSON.stringify({
            email,
            password
          })
        });
      }
      router.push("/dashboard");
      router.refresh();
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  }
  async function demo(r) {
    setDemoBusy(r);
    setError(null);
    try {
      await api("/api/auth/demo", {
        method: "POST",
        body: JSON.stringify({
          role: r
        })
      });
      router.push("/dashboard");
      router.refresh();
    } catch (err) {
      setError(err.message);
      setDemoBusy(null);
    }
  }
  return <div className="grid min-h-screen lg:grid-cols-[1.05fr_1fr]">
      {/* left brand panel */}
      <div className="relative hidden overflow-hidden lg:block">
        <AuroraCanvas className="absolute inset-0 h-full w-full opacity-60" />
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,transparent_0%,#07070c_75%)]" />
        <div className="relative z-10 flex h-full flex-col justify-between p-12">
          <Link href="/"><Logo /></Link>
          <div>
            <motion.p initial={{
            opacity: 0,
            y: 24
          }} animate={{
            opacity: 1,
            y: 0
          }} transition={{
            delay: 0.2,
            duration: 0.9,
            ease: [0.22, 1, 0.36, 1]
          }} className="serif-accent text-xl text-dim">
              where exams become insight
            </motion.p>
            <motion.h2 initial={{
            opacity: 0,
            y: 30
          }} animate={{
            opacity: 1,
            y: 0
          }} transition={{
            delay: 0.35,
            duration: 0.9,
            ease: [0.22, 1, 0.36, 1]
          }} className="font-display mt-3 max-w-md text-5xl font-bold leading-[1.02] tracking-tight">
              The arena for
              <span className="text-acid"> teachers</span> and
              <span className="text-cyan2"> students.</span>
            </motion.h2>
            <motion.div initial={{
            opacity: 0
          }} animate={{
            opacity: 1
          }} transition={{
            delay: 0.6,
            duration: 1
          }} className="mt-8 flex gap-6 text-[13px] text-dim">
              <span className="flex items-center gap-2"><BookOpenCheck className="h-4 w-4 text-acid" /> Author exams</span>
              <span className="flex items-center gap-2"><GraduationCap className="h-4 w-4 text-cyan2" /> Sit timed attempts</span>
            </motion.div>
          </div>
          <p className="text-[12px] text-faint">Demo accounts are one click — no password needed.</p>
        </div>
      </div>

      {/* right form panel */}
      <div className="relative flex items-center justify-center px-5 py-16">
        <Link href="/" className="absolute left-6 top-6 flex items-center gap-1.5 text-[13px] text-dim transition-colors hover:text-ink">
          <ArrowLeft className="h-3.5 w-3.5" /> Home
        </Link>
        <motion.div initial={{
        opacity: 0,
        y: 26
      }} animate={{
        opacity: 1,
        y: 0
      }} transition={{
        duration: 0.8,
        ease: [0.22, 1, 0.36, 1]
      }} className="w-full max-w-md">
          <div className="mb-8 lg:hidden"><Logo /></div>

          <div className="glass-deep rounded-3xl p-7 md:p-8">
            {/* mode tabs */}
            <div className="mb-7 grid grid-cols-2 gap-1 rounded-full border border-white/8 bg-white/3 p-1">
              {["signin", "signup"].map(m => <button key={m} onClick={() => {
              setMode(m);
              setError(null);
            }} className={cn("relative rounded-full py-2 text-[13px] font-semibold transition-colors", mode === m ? "text-[#0a0a10]" : "text-dim hover:text-ink")}>
                  {mode === m && <motion.span layoutId="modePill" className="absolute inset-0 rounded-full bg-acid" transition={{
                type: "spring",
                stiffness: 350,
                damping: 30
              }} />}
                  <span className="relative z-10">{m === "signin" ? "Sign in" : "Create account"}</span>
                </button>)}
            </div>

            <AnimatePresence mode="wait">
              <motion.form key={mode} initial={{
              opacity: 0,
              x: mode === "signup" ? 24 : -24
            }} animate={{
              opacity: 1,
              x: 0
            }} exit={{
              opacity: 0,
              x: mode === "signup" ? -24 : 24
            }} transition={{
              duration: 0.35,
              ease: [0.22, 1, 0.36, 1]
            }} onSubmit={submit} className="space-y-5">
                {mode === "signup" && <>
                    <div className="grid grid-cols-2 gap-2">
                      {["student", "teacher"].map(r => <button type="button" key={r} onClick={() => setRole(r)} className={cn("flex items-center justify-center gap-2 rounded-2xl border px-4 py-3 text-[13px] font-semibold transition-all duration-300", role === r ? r === "teacher" ? "border-acid/60 bg-acid/10 text-acid" : "border-cyan2/60 bg-cyan2/10 text-cyan2" : "border-white/10 text-dim hover:border-white/25")}>
                          {r === "teacher" ? <BookOpenCheck className="h-4 w-4" /> : <GraduationCap className="h-4 w-4" />}
                          {r === "teacher" ? "Teacher" : "Student"}
                        </button>)}
                    </div>
                    <Field label="Full name">
                      <div className="relative">
                        <User className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-faint" />
                        <Input value={name} onChange={e => setName(e.target.value)} placeholder="Marie Curie" className="pl-11" required />
                      </div>
                    </Field>
                  </>}
                <Field label="Email">
                  <div className="relative">
                    <Mail className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-faint" />
                    <Input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="you@school.edu" className="pl-11" required />
                  </div>
                </Field>
                <Field label="Password" hint={mode === "signup" ? "min 6 chars" : undefined}>
                  <div className="relative">
                    <Lock className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-faint" />
                    <Input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="••••••••" className="pl-11" required />
                  </div>
                </Field>

                <ErrorBanner message={error} />

                <Button size="lg" className="w-full" loading={busy} icon={<ArrowRight className="h-4 w-4" />}>
                  {mode === "signin" ? "Enter the arena" : "Create my account"}
                </Button>
              </motion.form>
            </AnimatePresence>

            <div className="my-6 flex items-center gap-3 text-[11px] uppercase tracking-[0.2em] text-faint">
              <span className="h-px flex-1 bg-white/8" /> or try instantly <span className="h-px flex-1 bg-white/8" />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => demo("teacher")} disabled={demoBusy !== null} className="btn btn-ghost btn-md group">
                {demoBusy === "teacher" ? <Loader2 className="h-4 w-4 animate-spin" /> : <BookOpenCheck className="h-4 w-4 text-acid" />}
                Teacher demo
              </button>
              <button onClick={() => demo("student")} disabled={demoBusy !== null} className="btn btn-ghost btn-md">
                {demoBusy === "student" ? <Loader2 className="h-4 w-4 animate-spin" /> : <GraduationCap className="h-4 w-4 text-cyan2" />}
                Student demo
              </button>
            </div>
          </div>
        </motion.div>
      </div>
    </div>;
}
export default function LoginPage() {
  return <Suspense>
      <AuthInner />
    </Suspense>;
}

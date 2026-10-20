import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { onSessionRefresh } from "@/lib/rr";
import { getSessionUser } from "@/lib/session";
import { Nav } from "@/components/Nav";

// pages (byte-identical copies of the Next.js app, imports only changed)
import LandingPage from "@/app/page.jsx";
import LoginPage from "@/app/login/page.jsx";
import { DashboardClient } from "@/app/dashboard/dashboard-client.jsx";
import { AiClient } from "@/app/ai/ai-client.jsx";
import { Builder } from "@/app/assessments/new/builder.jsx";
import { DetailClient } from "@/app/assessments/[id]/detail-client.jsx";
import { ExamClient } from "@/app/exam/[attemptId]/exam-client.jsx";
import { ReportClient } from "@/app/results/[attemptId]/report-client.jsx";

/* ---------- session (replaces Next server-side getSessionUser) ---------- */
const SessionCtx = createContext(null);
export function useSession() {
  return useContext(SessionCtx);
}

function SessionProvider({ children }) {
  // undefined = checking, null = signed out, object = signed in
  const [user, setUser] = useState(undefined);
  const refresh = useCallback(() => {
    setUser(undefined);
    getSessionUser().then(u => setUser(u)).catch(() => setUser(null));
  }, []);
  useEffect(() => {
    refresh();
  }, [refresh]);
  useEffect(() => onSessionRefresh(refresh), [refresh]);
  return <SessionCtx.Provider value={{ user, refresh }}>{children}</SessionCtx.Provider>;
}

/* ---------- guard: was `if (!user) redirect("/login")` on the server ---------- */
function Guard({ role, children }) {
  const { user } = useSession();
  if (user === undefined) return null; // session resolving — same blank flash as SSR
  if (!user) return <Navigate to="/login" replace />;
  if (role && user.role !== role) return <Navigate to="/dashboard" replace />;
  return children;
}

/* ---------- page shells (exact JSX of the old server components) ---------- */
function DashboardShell({ user }) {
  return <div className="min-h-screen">
      <Nav user={user} />
      <main className="mx-auto max-w-6xl px-5 pb-24 pt-28 md:pt-32">
        <DashboardClient user={user} />
      </main>
    </div>;
}

function AiShell({ user }) {
  return <div className="min-h-screen">
      <Nav user={user} />
      <main className="mx-auto max-w-5xl px-5 pb-40 pt-28 md:pt-32">
        <AiClient user={user} />
      </main>
    </div>;
}

function NewAssessmentShell({ user }) {
  return <div className="min-h-screen">
      <Nav user={user} />
      <main className="mx-auto max-w-4xl px-5 pb-40 pt-28 md:pt-32">
        <Builder />
      </main>
    </div>;
}

function AssessmentDetailShell({ user }) {
  return <div className="min-h-screen">
      <Nav user={user} />
      <main className="mx-auto max-w-5xl px-5 pb-24 pt-28 md:pt-32">
        <DetailClient user={user} />
      </main>
    </div>;
}

function ResultsShell({ user }) {
  return <div className="min-h-screen">
      <Nav user={user} />
      <main className="mx-auto max-w-6xl px-5 pb-24 pt-28 md:pt-32">
        <ReportClient />
      </main>
    </div>;
}

export default function App() {
  return <SessionProvider>
      {/* fixed grain layer, formerly in app/layout.jsx body */}
      <div className="grain" aria-hidden />
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/dashboard" element={<Guard><Dashboard /></Guard>} />
        <Route path="/ai" element={<Guard><AiStudio /></Guard>} />
        <Route path="/assessments/new" element={<Guard><NewAssessment /></Guard>} />
        <Route path="/assessments/:id" element={<Guard><AssessmentDetail /></Guard>} />
        <Route path="/exam/:attemptId" element={<Guard role="student"><Exam /></Guard>} />
        <Route path="/results/:attemptId" element={<Guard><Results /></Guard>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </SessionProvider>;
}

/* shells resolve user at render time so router.refresh() re-renders them */
function Dashboard() {
  const { user } = useSession();
  return <DashboardShell user={user} />;
}
function AiStudio() {
  const { user } = useSession();
  return <AiShell user={user} />;
}
function NewAssessment() {
  const { user } = useSession();
  return <NewAssessmentShell user={user} />;
}
function AssessmentDetail() {
  const { user } = useSession();
  return <AssessmentDetailShell user={user} />;
}
function Exam() {
  return <ExamClient />;
}
function Results() {
  const { user } = useSession();
  return <ResultsShell user={user} />;
}
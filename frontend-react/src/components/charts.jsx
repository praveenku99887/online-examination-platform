import { motion } from "framer-motion";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, PolarAngleAxis, PolarGrid, PolarRadiusAxis, Radar, RadarChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";

/* ---------------- Score ring ---------------- */
export function ScoreRing({
  pct,
  size = 200,
  stroke = 12
}) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const color = pct >= 75 ? "#D8FF3D" : pct >= 50 ? "#8B7CFF" : "#FF6B8A";
  const grade = pct >= 90 ? "S" : pct >= 75 ? "A" : pct >= 60 ? "B" : pct >= 45 ? "C" : pct >= 30 ? "D" : "F";
  return <div className="relative" style={{
    width: size,
    height: size
  }}>
      <svg width={size} height={size} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="rgba(255,255,255,0.07)" strokeWidth={stroke} />
        <motion.circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={color} strokeWidth={stroke} strokeLinecap="round" strokeDasharray={c} initial={{
        strokeDashoffset: c
      }} animate={{
        strokeDashoffset: c - c * pct / 100
      }} transition={{
        duration: 1.6,
        ease: [0.22, 1, 0.36, 1],
        delay: 0.3
      }} style={{
        filter: `drop-shadow(0 0 14px ${color}66)`
      }} />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="font-display text-5xl font-bold tracking-tight" style={{
        color
      }}>
          {pct}
          <span className="text-2xl">%</span>
        </span>
        <span className="mt-1 text-[11px] font-semibold uppercase tracking-[0.25em] text-faint">
          Grade {grade}
        </span>
      </div>
    </div>;
}
const tooltipStyle = {
  background: "rgba(12,12,20,0.95)",
  border: "1px solid rgba(255,255,255,0.1)",
  borderRadius: 12,
  fontSize: 12,
  color: "#ecedf3",
  boxShadow: "0 12px 40px rgba(0,0,0,0.6)"
};

/* ---------------- Topic radar ---------------- */
export function TopicRadar({
  data
}) {
  return <ResponsiveContainer width="100%" height={300}>
      <RadarChart data={data} outerRadius="72%">
        <PolarGrid stroke="rgba(255,255,255,0.09)" />
        <PolarAngleAxis dataKey="topic" tick={{
        fill: "#9a9aad",
        fontSize: 11,
        fontWeight: 600
      }} />
        <PolarRadiusAxis domain={[0, 100]} tick={false} axisLine={false} />
        <Radar name="Accuracy %" dataKey="accuracy" stroke="#D8FF3D" fill="#D8FF3D" fillOpacity={0.22} strokeWidth={2} isAnimationActive animationDuration={1200} dot={{
        r: 3,
        fill: "#D8FF3D",
        strokeWidth: 0
      }} />
        <Tooltip contentStyle={tooltipStyle} formatter={v => [`${v}%`, "Accuracy"]} />
      </RadarChart>
    </ResponsiveContainer>;
}

/* ---------------- Momentum area ---------------- */
export function MomentumArea({
  data
}) {
  return <ResponsiveContainer width="100%" height={260}>
      <AreaChart data={data} margin={{
      top: 12,
      right: 12,
      left: -18,
      bottom: 0
    }}>
        <defs>
          <linearGradient id="momGrad" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#8B7CFF" stopOpacity={0.5} />
            <stop offset="100%" stopColor="#8B7CFF" stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <CartesianGrid stroke="rgba(255,255,255,0.06)" vertical={false} />
        <XAxis dataKey="q" tick={{
        fill: "#63637a",
        fontSize: 11
      }} axisLine={false} tickLine={false} />
        <YAxis domain={[0, 100]} tick={{
        fill: "#63637a",
        fontSize: 11
      }} axisLine={false} tickLine={false} unit="%" />
        <Tooltip contentStyle={tooltipStyle} formatter={v => [`${v}%`, "Running accuracy"]} />
        <ReferenceLine y={50} stroke="rgba(255,255,255,0.18)" strokeDasharray="4 6" />
        <Area type="monotone" dataKey="cumulative" stroke="#8B7CFF" strokeWidth={2.5} fill="url(#momGrad)" animationDuration={1400} />
      </AreaChart>
    </ResponsiveContainer>;
}

/* ---------------- Time per question bars ---------------- */
export function TimeBars({
  data,
  avg
}) {
  return <ResponsiveContainer width="100%" height={260}>
      <BarChart data={data} margin={{
      top: 12,
      right: 12,
      left: -18,
      bottom: 0
    }}>
        <CartesianGrid stroke="rgba(255,255,255,0.06)" vertical={false} />
        <XAxis dataKey="q" tick={{
        fill: "#63637a",
        fontSize: 11
      }} axisLine={false} tickLine={false} />
        <YAxis tick={{
        fill: "#63637a",
        fontSize: 11
      }} axisLine={false} tickLine={false} unit="s" />
        <Tooltip contentStyle={tooltipStyle} formatter={(v, _n, item) => [`${v}s — ${item.payload?.correct ? "correct" : "incorrect"}`, "Time"]} />
        <ReferenceLine y={Math.round(avg)} stroke="#6FE3FF" strokeDasharray="4 6" label={{
        value: "avg",
        fill: "#6FE3FF",
        fontSize: 10,
        position: "right"
      }} />
        <Bar dataKey="seconds" radius={[6, 6, 0, 0]} animationDuration={1200}>
          {data.map((d, i) => <Cell key={i} fill={d.correct ? "#D8FF3D" : "#FF6B8A"} fillOpacity={0.85} />)}
        </Bar>
      </BarChart>
    </ResponsiveContainer>;
}

/* ---------------- Mastery spectrum (horizontal stacked per topic) ---------------- */
export function MasteryBars({
  data
}) {
  return <div className="space-y-4">
      {data.map((t, i) => {
      const pct = t.total ? Math.round(t.correct / t.total * 100) : 0;
      return <div key={i}>
            <div className="mb-1.5 flex items-baseline justify-between">
              <span className="text-[13px] font-semibold">{t.topic}</span>
              <span className="text-[12px] text-dim">
                {t.correct}/{t.total} · {pct}%
              </span>
            </div>
            <div className="h-2.5 overflow-hidden rounded-full bg-white/6">
              <motion.div className="h-full rounded-full" initial={{
            width: 0
          }} animate={{
            width: `${pct}%`
          }} transition={{
            duration: 1.1,
            delay: 0.15 * i,
            ease: [0.22, 1, 0.36, 1]
          }} style={{
            background: pct >= 70 ? "linear-gradient(90deg,#D8FF3D,#58FFC8)" : pct >= 40 ? "linear-gradient(90deg,#8B7CFF,#6FE3FF)" : "linear-gradient(90deg,#FF6B8A,#FF9A6B)",
            boxShadow: "0 0 16px rgba(216,255,61,0.25)"
          }} />
            </div>
          </div>;
    })}
    </div>;
}

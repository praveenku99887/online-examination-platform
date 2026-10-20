import { motion, useInView, useMotionValue, useSpring, useTransform } from "framer-motion";
import { useEffect, useRef } from "react";

/* ---------------- Reveal: scroll-triggered entrance ---------------- */
export function Reveal({
  children,
  delay = 0,
  y = 28,
  className = "",
  once = true
}) {
  return <motion.div initial={{
    opacity: 0,
    y,
    filter: "blur(6px)"
  }} whileInView={{
    opacity: 1,
    y: 0,
    filter: "blur(0px)"
  }} viewport={{
    once,
    margin: "-80px"
  }} transition={{
    duration: 0.9,
    delay,
    ease: [0.22, 1, 0.36, 1]
  }} className={className}>
      {children}
    </motion.div>;
}

/* ---------------- Magnetic: elements gravitate to cursor ---------------- */
export function Magnetic({
  children,
  strength = 0.35,
  className = ""
}) {
  const ref = useRef(null);
  const x = useMotionValue(0);
  const y = useMotionValue(0);
  const sx = useSpring(x, {
    stiffness: 180,
    damping: 14,
    mass: 0.4
  });
  const sy = useSpring(y, {
    stiffness: 180,
    damping: 14,
    mass: 0.4
  });
  function onMove(e) {
    const el = ref.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    x.set((e.clientX - (r.left + r.width / 2)) * strength);
    y.set((e.clientY - (r.top + r.height / 2)) * strength);
  }
  function onLeave() {
    x.set(0);
    y.set(0);
  }
  return <motion.div ref={ref} onMouseMove={onMove} onMouseLeave={onLeave} style={{
    x: sx,
    y: sy
  }} className={className}>
      {children}
    </motion.div>;
}

/* ---------------- Tilt: pointer-tracked 3D card ---------------- */
export function Tilt({
  children,
  className = "",
  max = 8,
  glare = true
}) {
  const ref = useRef(null);
  const rx = useMotionValue(0);
  const ry = useMotionValue(0);
  const srx = useSpring(rx, {
    stiffness: 220,
    damping: 18
  });
  const sry = useSpring(ry, {
    stiffness: 220,
    damping: 18
  });
  const glareX = useMotionValue(50);
  const glareY = useMotionValue(50);
  function onMove(e) {
    const r = ref.current?.getBoundingClientRect();
    if (!r) return;
    const px = (e.clientX - r.left) / r.width;
    const py = (e.clientY - r.top) / r.height;
    ry.set((px - 0.5) * max * 2);
    rx.set((0.5 - py) * max * 2);
    glareX.set(px * 100);
    glareY.set(py * 100);
  }
  function onLeave() {
    rx.set(0);
    ry.set(0);
    glareX.set(50);
    glareY.set(50);
  }
  const glareBg = useTransform([glareX, glareY], ([gx, gy]) => `radial-gradient(420px circle at ${gx}% ${gy}%, rgba(255,255,255,0.07), transparent 60%)`);
  return <motion.div ref={ref} onMouseMove={onMove} onMouseLeave={onLeave} style={{
    rotateX: srx,
    rotateY: sry,
    transformStyle: "preserve-3d",
    perspective: 900
  }} className={`relative ${className}`}>
      {children}
      {glare && <motion.div aria-hidden className="pointer-events-none absolute inset-0 rounded-[inherit]" style={{
      background: glareBg
    }} />}
    </motion.div>;
}

/* ---------------- CursorGlow: ambient light following the pointer ---------------- */
export function CursorGlow() {
  const ref = useRef(null);
  useEffect(() => {
    let raf = 0;
    let tx = -400;
    let ty = -400;
    let cxv = tx;
    let cyv = ty;
    const onMove = e => {
      tx = e.clientX;
      ty = e.clientY;
    };
    const loop = () => {
      cxv += (tx - cxv) * 0.08;
      cyv += (ty - cyv) * 0.08;
      if (ref.current) ref.current.style.transform = `translate(${cxv - 300}px, ${cyv - 300}px)`;
      raf = requestAnimationFrame(loop);
    };
    window.addEventListener("pointermove", onMove);
    raf = requestAnimationFrame(loop);
    return () => {
      window.removeEventListener("pointermove", onMove);
      cancelAnimationFrame(raf);
    };
  }, []);
  return <div ref={ref} aria-hidden className="pointer-events-none fixed left-0 top-0 z-[1] h-[600px] w-[600px] rounded-full opacity-[0.16] mix-blend-screen" style={{
    background: "radial-gradient(circle, rgba(139,124,255,0.9) 0%, rgba(111,227,255,0.35) 40%, transparent 70%)",
    filter: "blur(60px)"
  }} />;
}

/* ---------------- Aurora: living gradient canvas ---------------- */
export function AuroraCanvas({
  className = ""
}) {
  const canvasRef = useRef(null);
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    let raf = 0;
    let w = 0;
    let h = 0;
    const dpr = Math.min(window.devicePixelRatio || 1, 1.5);
    const blobs = [{
      c: [139, 124, 255],
      r: 0.55,
      sx: 0.24,
      sy: 0.3,
      dx: 0.14,
      dy: 0.11,
      ph: 0
    }, {
      c: [216, 255, 61],
      r: 0.4,
      sx: 0.78,
      sy: 0.24,
      dx: 0.1,
      dy: 0.15,
      ph: 2.1
    }, {
      c: [111, 227, 255],
      r: 0.48,
      sx: 0.6,
      sy: 0.75,
      dx: 0.13,
      dy: 0.09,
      ph: 4.2
    }, {
      c: [255, 107, 138],
      r: 0.34,
      sx: 0.2,
      sy: 0.82,
      dx: 0.09,
      dy: 0.12,
      ph: 5.4
    }];
    function resize() {
      const rect = canvas.parentElement.getBoundingClientRect();
      w = rect.width;
      h = rect.height;
      canvas.width = w * dpr;
      canvas.height = h * dpr;
      canvas.style.width = `${w}px`;
      canvas.style.height = `${h}px`;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }
    resize();
    const ro = new ResizeObserver(resize);
    ro.observe(canvas.parentElement);
    let t = 0;
    function draw() {
      t += 0.0035;
      ctx.clearRect(0, 0, w, h);
      ctx.globalCompositeOperation = "lighter";
      for (const b of blobs) {
        const x = (b.sx + Math.sin(t * 2 + b.ph) * b.dx) * w;
        const y = (b.sy + Math.cos(t * 1.6 + b.ph * 1.3) * b.dy) * h;
        const rad = b.r * Math.min(w, h);
        const g = ctx.createRadialGradient(x, y, 0, x, y, rad);
        g.addColorStop(0, `rgba(${b.c[0]},${b.c[1]},${b.c[2]},0.5)`);
        g.addColorStop(0.55, `rgba(${b.c[0]},${b.c[1]},${b.c[2]},0.12)`);
        g.addColorStop(1, "rgba(0,0,0,0)");
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(x, y, rad, 0, Math.PI * 2);
        ctx.fill();
      }
      raf = requestAnimationFrame(draw);
    }
    raf = requestAnimationFrame(draw);
    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
    };
  }, []);
  return <canvas ref={canvasRef} className={className} aria-hidden />;
}

/* ---------------- Counter: numbers that count up in view ---------------- */
export function Counter({
  to,
  suffix = "",
  duration = 1.6,
  className = ""
}) {
  const ref = useRef(null);
  const inView = useInView(ref, {
    once: true,
    margin: "-40px"
  });
  useEffect(() => {
    if (!inView || !ref.current) return;
    const start = performance.now();
    let raf = 0;
    const tick = now => {
      const p = Math.min(1, (now - start) / (duration * 1000));
      const eased = 1 - Math.pow(1 - p, 4);
      if (ref.current) ref.current.textContent = `${Math.round(to * eased)}${suffix}`;
      if (p < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [inView, to, suffix, duration]);
  return <span ref={ref} className={className}>
      0{suffix}
    </span>;
}

/* ---------------- Marquee ---------------- */
export function Marquee({
  items,
  className = ""
}) {
  const row = [...items, ...items];
  return <div className={`relative overflow-hidden ${className}`}>
      <div className="marquee-track flex w-max items-center gap-10 pr-10">
        {row.map((it, i) => <span key={i} className="flex items-center gap-10 whitespace-nowrap">
            <span className="font-display text-sm font-medium tracking-[0.2em] text-dim uppercase">
              {it}
            </span>
            <span className="h-1.5 w-1.5 rounded-full bg-acid/70" />
          </span>)}
      </div>
    </div>;
}

/* ---------------- Page transition wrapper ---------------- */
export function PageIn({
  children,
  className = ""
}) {
  return <motion.div initial={{
    opacity: 0,
    y: 14
  }} animate={{
    opacity: 1,
    y: 0
  }} transition={{
    duration: 0.7,
    ease: [0.22, 1, 0.36, 1]
  }} className={className}>
      {children}
    </motion.div>;
}
export function useScrollProgress() {
  const ref = useRef(0);
  const v = useMotionValue(0);
  useEffect(() => {
    const onScroll = () => {
      const max = document.documentElement.scrollHeight - window.innerHeight;
      ref.current = max > 0 ? window.scrollY / max : 0;
      v.set(ref.current);
    };
    onScroll();
    window.addEventListener("scroll", onScroll, {
      passive: true
    });
    return () => window.removeEventListener("scroll", onScroll);
  }, [v]);
  return v;
}

import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { formatDateTime } from "../utils/format";

// Chart elements are SVG attributes, which cannot read CSS variables directly,
// so we read the current theme colors from the page.
function readCssVariable(name) {
  return getComputedStyle(document.documentElement)
    .getPropertyValue(name)
    .trim();
}

function ChartTooltip({ active, payload }) {
  if (!active || !payload?.length) {
    return null;
  }

  const point = payload[0].payload;

  return (
    <div className="card" style={{ padding: 12 }}>
      <strong>ציון: {point.score}</strong>
      <div className="muted">{point.title}</div>
      <div className="muted">{point.date}</div>
    </div>
  );
}

export function ScoreChart({ attempts }) {
  const primary = readCssVariable("--color-primary");
  const border = readCssVariable("--color-border");
  const muted = readCssVariable("--color-muted");

  // The list arrives newest first; the chart reads oldest to newest.
  const data = [...attempts].reverse().map((attempt, index) => ({
    index: index + 1,
    score: Math.round(attempt.score),
    title: attempt.quizTitle,
    date: formatDateTime(attempt.completedAt),
  }));

  return (
    <div style={{ width: "100%", height: 280 }}>
      <ResponsiveContainer>
        <LineChart
          data={data}
          margin={{ top: 10, right: 10, left: 10, bottom: 10 }}
        >
          <CartesianGrid stroke={border} strokeDasharray="3 3" />
          <XAxis dataKey="index" reversed stroke={muted} tickLine={false} />
          <YAxis
            domain={[0, 100]}
            orientation="right"
            stroke={muted}
            tickLine={false}
            width={40}
          />
          <Tooltip content={<ChartTooltip />} />
          <Line
            type="monotone"
            dataKey="score"
            stroke={primary}
            strokeWidth={3}
            dot={{ r: 4, fill: primary }}
            activeDot={{ r: 6 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

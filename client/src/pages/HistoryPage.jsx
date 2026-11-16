import { useEffect, useState } from "react";
import { Link } from "react-router";
import { attemptsApi } from "../services/api";
import { Loading } from "../components/Loading";
import { ScoreChart } from "../components/ScoreChart";
import { formatDateTime } from "../utils/format";

function StatCard({ label, value }) {
  return (
    <div className="card" style={{ textAlign: "center" }}>
      <div style={{ fontSize: "2rem", fontWeight: 700 }}>{value}</div>
      <div className="muted">{label}</div>
    </div>
  );
}

function getScoreBadgeClass(score) {
  if (score >= 70) return "badge badge-success";
  if (score >= 50) return "badge";
  return "badge badge-danger";
}

export function HistoryPage() {
  const [attempts, setAttempts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    attemptsApi
      .list()
      .then((data) => {
        if (!ignore) setAttempts(data);
      })
      .catch((err) => {
        if (!ignore) setError(err.message);
      })
      .finally(() => {
        if (!ignore) setIsLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, []);

  if (isLoading) {
    return <Loading text="טוען היסטוריה..." />;
  }

  if (error) {
    return <div className="alert alert-error">{error}</div>;
  }

  if (attempts.length === 0) {
    return (
      <div className="card stack">
        <h1>ההתקדמות שלי</h1>
        <p className="muted">
          עוד לא פתרת בחנים. אחרי הבוחן הראשון, כאן תראה את הציונים שלך לאורך
          זמן.
        </p>
        <div>
          <Link to="/" className="btn btn-primary">
            למסמכים שלי
          </Link>
        </div>
      </div>
    );
  }

  const scores = attempts.map((a) => a.score);
  const average = Math.round(
    scores.reduce((sum, score) => sum + score, 0) / scores.length,
  );
  const best = Math.round(Math.max(...scores));
  const latest = Math.round(attempts[0].score);

  return (
    <div className="stack">
      <h1>ההתקדמות שלי</h1>

      <div
        className="grid"
        style={{ gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))" }}
      >
        <StatCard label="בחנים שנפתרו" value={attempts.length} />
        <StatCard label="ציון ממוצע" value={average} />
        <StatCard label="הציון הטוב ביותר" value={best} />
        <StatCard label="הציון האחרון" value={latest} />
      </div>

      {attempts.length > 1 && (
        <div className="card stack">
          <h2>ציונים לאורך זמן</h2>
          <ScoreChart attempts={attempts} />
        </div>
      )}

      <section className="stack">
        <h2>כל הניסיונות</h2>

        {attempts.map((attempt) => (
          <Link
            key={attempt.id}
            to={`/attempts/${attempt.id}`}
            className="card card-link"
          >
            <div className="row row-between">
              <div>
                <h3 style={{ marginBottom: 4 }}>{attempt.quizTitle}</h3>
                <div className="muted">
                  {attempt.documentTitle} ·{" "}
                  {formatDateTime(attempt.completedAt)}
                </div>
              </div>
              <span className={getScoreBadgeClass(attempt.score)}>
                {Math.round(attempt.score)}
              </span>
            </div>
          </Link>
        ))}
      </section>
    </div>
  );
}

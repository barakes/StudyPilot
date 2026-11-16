import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { documentsApi, quizzesApi } from "../services/api";
import { Loading } from "../components/Loading";
import { formatCount, formatDate, formatNumber } from "../utils/format";

const MAX_MULTIPLE_CHOICE = 15;
const MAX_OPEN = 10;

export function DocumentPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [document, setDocument] = useState(null);
  const [quizzes, setQuizzes] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  const [multipleChoiceCount, setMultipleChoiceCount] = useState(5);
  const [openCount, setOpenCount] = useState(2);
  const [isGenerating, setIsGenerating] = useState(false);
  const [generateError, setGenerateError] = useState("");

  useEffect(() => {
    let ignore = false;

    Promise.all([documentsApi.get(id), quizzesApi.list(id)])
      .then(([documentData, quizzesData]) => {
        if (!ignore) {
          setDocument(documentData);
          setQuizzes(quizzesData);
        }
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
  }, [id]);

  async function handleGenerate(event) {
    event.preventDefault();

    if (multipleChoiceCount + openCount === 0) {
      setGenerateError("יש לבקש לפחות שאלה אחת.");
      return;
    }

    setGenerateError("");
    setIsGenerating(true);

    try {
      const quiz = await quizzesApi.generate(
        Number(id),
        multipleChoiceCount,
        openCount,
      );
      navigate(`/quizzes/${quiz.id}`);
    } catch (err) {
      setGenerateError(err.message);
      setIsGenerating(false);
    }
  }

  async function handleDelete() {
    const confirmed = window.confirm(
      "למחוק את המסמך? כל הבחנים והתוצאות שלו יימחקו גם הם, ואין אפשרות לשחזר.",
    );

    if (!confirmed) {
      return;
    }

    try {
      await documentsApi.remove(id);
      navigate("/", { replace: true });
    } catch (err) {
      setError(err.message);
    }
  }

  if (isLoading) {
    return <Loading text="טוען מסמך..." />;
  }

  if (!document) {
    return (
      <div className="stack">
        <div className="alert alert-error">{error || "המסמך לא נמצא."}</div>
        <Link to="/">חזרה לרשימת המסמכים</Link>
      </div>
    );
  }

  return (
    <div className="stack">
      <Link to="/">→ חזרה לרשימת המסמכים</Link>

      <div className="row row-between">
        <div>
          <h1>{document.title}</h1>
          <p className="muted">
            {document.originalFileName} · {formatDate(document.uploadedAt)} ·{" "}
            {formatNumber(document.extractedText.length)} תווים
          </p>
        </div>
        <button type="button" className="btn btn-danger" onClick={handleDelete}>
          מחיקת מסמך
        </button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <form className="card stack" onSubmit={handleGenerate}>
        <h2>בוחן חדש</h2>

        <div className="row">
          <div className="field">
            <label htmlFor="multipleChoiceCount">שאלות אמריקאיות</label>
            <input
              id="multipleChoiceCount"
              type="number"
              min={0}
              max={MAX_MULTIPLE_CHOICE}
              value={multipleChoiceCount}
              onChange={(e) => setMultipleChoiceCount(Number(e.target.value))}
              disabled={isGenerating}
            />
          </div>

          <div className="field">
            <label htmlFor="openCount">שאלות פתוחות</label>
            <input
              id="openCount"
              type="number"
              min={0}
              max={MAX_OPEN}
              value={openCount}
              onChange={(e) => setOpenCount(Number(e.target.value))}
              disabled={isGenerating}
            />
          </div>
        </div>

        {generateError && (
          <div className="alert alert-error">{generateError}</div>
        )}

        <div className="row">
          <button
            type="submit"
            className="btn btn-primary"
            disabled={isGenerating}
          >
            {isGenerating ? "יוצר שאלות..." : "יצירת בוחן"}
          </button>
          {isGenerating && (
            <span className="muted">זה יכול לקחת עד חצי דקה.</span>
          )}
        </div>
      </form>

      <section className="stack">
        <h2>בחנים</h2>

        {quizzes.length === 0 && (
          <div className="card">
            <p className="muted">עוד לא נוצרו בחנים מהמסמך הזה.</p>
          </div>
        )}

        <div className="grid">
          {quizzes.map((quiz) => (
            <Link
              key={quiz.id}
              to={`/quizzes/${quiz.id}`}
              className="card card-link stack"
            >
              <h3>{quiz.title}</h3>
              <p className="muted">
                {formatDate(quiz.createdAt)} ·{" "}
                {formatCount(quiz.questionCount, "שאלה", "שאלות", null, true)}
              </p>
              <div className="row">
                <span className="badge">
                  {formatCount(
                    quiz.attemptCount,
                    "ניסיון",
                    "ניסיונות",
                    "טרם נפתר",
                  )}
                </span>
                {quiz.bestScore !== null && (
                  <span className="badge badge-success">
                    שיא: {Math.round(quiz.bestScore)}
                  </span>
                )}
              </div>
            </Link>
          ))}
        </div>
      </section>

      <details className="card">
        <summary>הטקסט שחולץ מהקובץ</summary>
        <pre
          style={{
            whiteSpace: "pre-wrap",
            maxHeight: 400,
            overflow: "auto",
            fontFamily: "inherit",
          }}
        >
          {document.extractedText}
        </pre>
      </details>
    </div>
  );
}

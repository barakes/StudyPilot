import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { attemptsApi, quizzesApi } from "../services/api";
import { Loading } from "../components/Loading";
import { formatCount } from "../utils/format";

const OPTION_LETTERS = ["א", "ב", "ג", "ד", "ה", "ו"];
const MAX_ANSWER_LENGTH = 2000;

export function QuizPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [quiz, setQuiz] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  const [answers, setAnswers] = useState({});
  const [currentIndex, setCurrentIndex] = useState(0);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");

  useEffect(() => {
    let ignore = false;

    quizzesApi
      .get(id)
      .then((data) => {
        if (!ignore) setQuiz(data);
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

  if (isLoading) {
    return <Loading text="טוען בוחן..." />;
  }

  if (!quiz) {
    return (
      <div className="stack">
        <div className="alert alert-error">{error || "הבוחן לא נמצא."}</div>
        <Link to="/">חזרה לעמוד הראשי</Link>
      </div>
    );
  }

  const questions = quiz.questions;
  const question = questions[currentIndex];
  const isFirst = currentIndex === 0;
  const isLast = currentIndex === questions.length - 1;
  const currentAnswer = answers[question.id] ?? "";
  const answeredCount = questions.filter(
    (q) => (answers[q.id] ?? "").trim() !== "",
  ).length;
  const progressPercent = ((currentIndex + 1) / questions.length) * 100;

  function setAnswer(value) {
    setAnswers((current) => ({ ...current, [question.id]: value }));
  }

  async function handleSubmit() {
    const unansweredCount = questions.length - answeredCount;

    if (unansweredCount > 0) {
      const confirmed = window.confirm(
        `יש ${formatCount(unansweredCount, "שאלה", "שאלות", null, true)} בלי תשובה. להגיש בכל זאת?`,
      );

      if (!confirmed) {
        return;
      }
    }

    setSubmitError("");
    setIsSubmitting(true);

    try {
      const attempt = await attemptsApi.submit(
        quiz.id,
        questions.map((q) => ({
          questionId: q.id,
          answer: answers[q.id] ?? "",
        })),
      );
      navigate(`/attempts/${attempt.id}`);
    } catch (err) {
      setSubmitError(err.message);
      setIsSubmitting(false);
    }
  }

  return (
    <div className="stack">
      <Link to={`/documents/${quiz.documentId}`}>
        → חזרה אל {quiz.documentTitle}
      </Link>

      <div>
        <h1>{quiz.title}</h1>
        <p className="muted">
          שאלה {currentIndex + 1} מתוך {questions.length} · ענית על{" "}
          {answeredCount}
        </p>
      </div>

      <div
        className="progress"
        role="progressbar"
        aria-valuenow={currentIndex + 1}
        aria-valuemin={1}
        aria-valuemax={questions.length}
      >
        <div
          className="progress-bar"
          style={{ width: `${progressPercent}%` }}
        />
      </div>

      <div className="card stack">
        <div>
          <span className="badge">
            {question.type === "MultipleChoice"
              ? "שאלה אמריקאית"
              : "שאלה פתוחה"}
          </span>
        </div>

        <h2>{question.text}</h2>

        {question.type === "MultipleChoice" ? (
          <div className="stack">
            {question.options.map((option, index) => (
              <button
                key={option}
                type="button"
                className={`option ${currentAnswer === option ? "option-selected" : ""}`}
                aria-pressed={currentAnswer === option}
                onClick={() => setAnswer(option)}
                disabled={isSubmitting}
              >
                <strong>{OPTION_LETTERS[index]}.</strong>
                <span>{option}</span>
              </button>
            ))}
          </div>
        ) : (
          <div className="field">
            <label htmlFor="openAnswer">התשובה שלך</label>
            <textarea
              id="openAnswer"
              maxLength={MAX_ANSWER_LENGTH}
              placeholder="כתוב את התשובה במילים שלך..."
              value={currentAnswer}
              onChange={(e) => setAnswer(e.target.value)}
              disabled={isSubmitting}
            />
            <span className="muted">
              {currentAnswer.length} / {MAX_ANSWER_LENGTH}
            </span>
          </div>
        )}
      </div>

      {submitError && <div className="alert alert-error">{submitError}</div>}

      <div className="row row-between">
        <button
          type="button"
          className="btn btn-secondary"
          onClick={() => setCurrentIndex((i) => i - 1)}
          disabled={isFirst || isSubmitting}
        >
          → הקודמת
        </button>

        {isLast ? (
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleSubmit}
            disabled={isSubmitting}
          >
            {isSubmitting ? "בודק תשובות..." : "הגשה ✓"}
          </button>
        ) : (
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => setCurrentIndex((i) => i + 1)}
          >
            הבאה ←
          </button>
        )}
      </div>
    </div>
  );
}

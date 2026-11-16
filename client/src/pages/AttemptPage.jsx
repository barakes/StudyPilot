import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { attemptsApi } from "../services/api";
import { Loading } from "../components/Loading";
import { formatDateTime } from "../utils/format";

const OPTION_LETTERS = ["א", "ב", "ג", "ד", "ה", "ו"];

function getScoreMessage(score) {
  if (score >= 90) return "מצוין! שולט בחומר 🏆";
  if (score >= 70) return "יפה מאוד, עוד קצת ואתה שם 💪";
  if (score >= 50) return "התחלה טובה. כדאי לעבור על ההסברים 📚";
  return "שווה לחזור על החומר ולנסות שוב 🔄";
}

function getAnswerBadge(answer) {
  if (answer.score === 1) {
    return { className: "badge badge-success", text: "נכון" };
  }

  if (answer.isCorrect) {
    return { className: "badge badge-success", text: "נכון ברובו" };
  }

  if (answer.score > 0) {
    return { className: "badge", text: "נכון חלקית" };
  }

  return { className: "badge badge-danger", text: "לא נכון" };
}

function getOptionClassName(option, answer) {
  if (option === answer.correctAnswer) {
    return "option option-correct";
  }

  if (option === answer.userAnswer) {
    return "option option-wrong";
  }

  return "option";
}

export function AttemptPage() {
  const { id } = useParams();

  const [attempt, setAttempt] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    attemptsApi
      .get(id)
      .then((data) => {
        if (!ignore) setAttempt(data);
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
    return <Loading text="טוען תוצאות..." />;
  }

  if (!attempt) {
    return (
      <div className="stack">
        <div className="alert alert-error">{error || "התוצאות לא נמצאו."}</div>
        <Link to="/">חזרה לעמוד הראשי</Link>
      </div>
    );
  }

  const score = Math.round(attempt.score);
  const correctCount = attempt.answers.filter((a) => a.isCorrect).length;

  return (
    <div className="stack">
      <div className="card stack" style={{ textAlign: "center" }}>
        <p className="muted">{attempt.quizTitle}</p>
        <div className="score-circle">{score}</div>
        <h1>{getScoreMessage(score)}</h1>
        <p className="muted">
          {correctCount} מתוך {attempt.answers.length} תשובות נכונות ·{" "}
          {formatDateTime(attempt.completedAt)}
        </p>
        <div className="row" style={{ justifyContent: "center" }}>
          <Link to={`/quizzes/${attempt.quizId}`} className="btn btn-primary">
            לתרגל שוב
          </Link>
          <Link to="/" className="btn btn-secondary">
            לעמוד הראשי
          </Link>
        </div>
      </div>

      <h2>פירוט התשובות</h2>

      {attempt.answers.map((answer) => {
        const badge = getAnswerBadge(answer);

        return (
          <div key={answer.questionId} className="card stack">
            <div className="row row-between">
              <span className="muted">שאלה {answer.order}</span>
              <span className={badge.className}>{badge.text}</span>
            </div>

            <h3>{answer.questionText}</h3>

            {answer.type === "MultipleChoice" ? (
              <div className="stack">
                {answer.options.map((option, index) => (
                  <div
                    key={option}
                    className={getOptionClassName(option, answer)}
                  >
                    <strong>{OPTION_LETTERS[index]}.</strong>
                    <span>{option}</span>
                    {option === answer.correctAnswer && <span>✓</span>}
                    {option === answer.userAnswer &&
                      option !== answer.correctAnswer && <span>✗</span>}
                  </div>
                ))}
                {!answer.userAnswer && <p className="muted">לא נבחרה תשובה.</p>}
              </div>
            ) : (
              <div className="stack">
                <div>
                  <strong>התשובה שלך:</strong>
                  <p>{answer.userAnswer || "לא נכתבה תשובה."}</p>
                </div>

                {answer.feedback && (
                  <div className="alert alert-info">
                    <strong>משוב ({Math.round(answer.score * 100)}%):</strong>{" "}
                    {answer.feedback}
                  </div>
                )}

                <div>
                  <strong>תשובה לדוגמה:</strong>
                  <p>{answer.correctAnswer}</p>
                </div>
              </div>
            )}

            <p className="muted">
              <strong>הסבר:</strong> {answer.explanation}
            </p>
          </div>
        );
      })}
    </div>
  );
}

import { useEffect, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../hooks/useAuth";
import { documentsApi } from "../services/api";
import { Loading } from "../components/Loading";
import { UploadDocumentForm } from "../components/UploadDocumentForm";
import { formatCount, formatDate, formatNumber } from "../utils/format";

export function DashboardPage() {
  const { user } = useAuth();
  const [documents, setDocuments] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let ignore = false;

    documentsApi
      .list()
      .then((data) => {
        if (!ignore) setDocuments(data);
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

  function handleUploaded(document) {
    const summary = {
      id: document.id,
      title: document.title,
      originalFileName: document.originalFileName,
      uploadedAt: document.uploadedAt,
      characterCount: document.extractedText.length,
      quizCount: 0,
    };

    setDocuments((current) => [summary, ...current]);
  }

  return (
    <div className="stack">
      <div>
        <h1>שלום, {user.displayName} 👋</h1>
        <p className="muted">
          העלה חומר לימוד, ותקבל ממנו בוחן תרגול תוך כמה שניות.
        </p>
      </div>

      <UploadDocumentForm onUploaded={handleUploaded} />

      <section className="stack">
        <h2>המסמכים שלי</h2>

        {isLoading && <Loading text="טוען מסמכים..." />}

        {error && <div className="alert alert-error">{error}</div>}

        {!isLoading && !error && documents.length === 0 && (
          <div className="card">
            <p className="muted">
              עוד לא העלית מסמכים. התחל עם סיכום משיעור אחרון.
            </p>
          </div>
        )}

        <div className="grid">
          {documents.map((document) => (
            <Link
              key={document.id}
              to={`/documents/${document.id}`}
              className="card card-link stack"
            >
              <h3>{document.title}</h3>
              <div className="muted">
                <div>{document.originalFileName}</div>
                <div>
                  {formatDate(document.uploadedAt)} ·{" "}
                  {formatNumber(document.characterCount)} תווים
                </div>
              </div>
              <div>
                <span className="badge">
                  {formatCount(
                    document.quizCount,
                    "בוחן",
                    "בחנים",
                    "אין בחנים עדיין",
                  )}
                </span>
              </div>
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}

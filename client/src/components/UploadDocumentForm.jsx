import { useRef, useState } from "react";
import { documentsApi } from "../services/api";

const MAX_FILE_SIZE_MB = 10;
const ACCEPTED_TYPES = ".txt,.docx,.pdf";

export function UploadDocumentForm({ onUploaded }) {
  const fileInputRef = useRef(null);
  const [file, setFile] = useState(null);
  const [title, setTitle] = useState("");
  const [error, setError] = useState("");
  const [isUploading, setIsUploading] = useState(false);

  function handleFileChange(event) {
    setError("");
    const selected = event.target.files[0] ?? null;

    if (selected && selected.size > MAX_FILE_SIZE_MB * 1024 * 1024) {
      setError(`הקובץ גדול מדי. הגודל המקסימלי הוא ${MAX_FILE_SIZE_MB} מגה.`);
      event.target.value = "";
      setFile(null);
      return;
    }

    setFile(selected);
  }

  async function handleSubmit(event) {
    event.preventDefault();

    if (!file) {
      setError("יש לבחור קובץ.");
      return;
    }

    setError("");
    setIsUploading(true);

    try {
      const document = await documentsApi.upload(file, title.trim());
      setFile(null);
      setTitle("");
      fileInputRef.current.value = "";
      onUploaded(document);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsUploading(false);
    }
  }

  return (
    <form className="card stack" onSubmit={handleSubmit}>
      <div>
        <h2>העלאת חומר לימוד</h2>
        <p className="muted">
          קבצי טקסט, מעבד תמלילים או מסמך נייד, עד {MAX_FILE_SIZE_MB} מגה.
        </p>
      </div>

      <div className="field">
        <label htmlFor="file">קובץ</label>
        <input
          id="file"
          ref={fileInputRef}
          type="file"
          accept={ACCEPTED_TYPES}
          onChange={handleFileChange}
          disabled={isUploading}
        />
      </div>

      <div className="field">
        <label htmlFor="title">כותרת (לא חובה)</label>
        <input
          id="title"
          maxLength={200}
          placeholder="אם לא תמלא, נשתמש בשם הקובץ"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          disabled={isUploading}
        />
      </div>

      <div className="alert alert-info">
        הטקסט נשלח לשירות בינה מלאכותית חיצוני כדי ליצור שאלות. אל תעלו מסמכים
        עם מידע אישי או רגיש.
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div>
        <button
          type="submit"
          className="btn btn-primary"
          disabled={isUploading || !file}
        >
          {isUploading ? "מעלה וקורא את הקובץ..." : "העלאה"}
        </button>
      </div>
    </form>
  );
}

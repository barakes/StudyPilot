const TOKEN_KEY = "studypilot.token";

// ===== Token storage =====
// Wrapped in try/catch because browser storage can be blocked (for example in private mode).

export function getToken() {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function setToken(token) {
  try {
    localStorage.setItem(TOKEN_KEY, token);
  } catch {
    // Ignore: the user will simply need to log in again next time.
  }
}

export function clearToken() {
  try {
    localStorage.removeItem(TOKEN_KEY);
  } catch {
    // Ignore
  }
}

// ===== Errors =====

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

let unauthorizedHandler = null;

// Lets the app react when the server rejects the token (for example, when it expires).
export function onUnauthorized(handler) {
  unauthorizedHandler = handler;
}

const STATUS_MESSAGES = {
  401: "יש להתחבר מחדש.",
  403: "אין לך הרשאה לפעולה הזו.",
  404: "הפריט המבוקש לא נמצא.",
  409: "כבר קיים חשבון עם כתובת הדואר הזו.",
  413: "הקובץ גדול מדי.",
  429: "הגעת למגבלת השימוש היומית. נסה שוב מחר.",
  502: "שירות הבינה המלאכותית לא זמין כרגע. נסה שוב בעוד כמה דקות.",
};

function getErrorMessage(status, data) {
  if (STATUS_MESSAGES[status]) {
    return STATUS_MESSAGES[status];
  }

  // Validation errors come back as { errors: { Field: ["message"] } }.
  if (data?.errors) {
    const firstError = Object.values(data.errors).flat()[0];
    if (firstError) {
      return `הבקשה לא תקינה: ${firstError}`;
    }
  }

  if (data?.message) {
    return `הבקשה לא תקינה: ${data.message}`;
  }

  return "אירעה שגיאה לא צפויה. נסה שוב.";
}

// ===== Core request function =====

async function request(path, { method = "GET", body, isForm = false } = {}) {
  const headers = {};
  const token = getToken();

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  if (body && !isForm) {
    headers["Content-Type"] = "application/json";
  }

  let response;

  try {
    response = await fetch(`/api${path}`, {
      method,
      headers,
      body: body ? (isForm ? body : JSON.stringify(body)) : undefined,
    });
  } catch {
    throw new ApiError(0, "לא ניתן להתחבר לשרת. בדוק את החיבור ונסה שוב.");
  }

  if (response.status === 401 && token) {
    clearToken();
    unauthorizedHandler?.();
  }

  if (response.status === 204) {
    return null;
  }

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    throw new ApiError(response.status, getErrorMessage(response.status, data));
  }

  return data;
}

// ===== Endpoints =====

export const authApi = {
  login: (email, password) =>
    request("/auth/login", { method: "POST", body: { email, password } }),

  register: (email, password, displayName) =>
    request("/auth/register", {
      method: "POST",
      body: { email, password, displayName },
    }),

  me: () => request("/auth/me"),
};

export const documentsApi = {
  list: () => request("/documents"),

  get: (id) => request(`/documents/${id}`),

  upload: (file, title) => {
    const form = new FormData();
    form.append("File", file);
    if (title) {
      form.append("Title", title);
    }
    return request("/documents", { method: "POST", body: form, isForm: true });
  },

  remove: (id) => request(`/documents/${id}`, { method: "DELETE" }),
};

export const quizzesApi = {
  list: (documentId) =>
    request(documentId ? `/quizzes?documentId=${documentId}` : "/quizzes"),

  get: (id) => request(`/quizzes/${id}`),

  generate: (documentId, multipleChoiceCount, openCount) =>
    request("/quizzes", {
      method: "POST",
      body: { documentId, multipleChoiceCount, openCount },
    }),

  remove: (id) => request(`/quizzes/${id}`, { method: "DELETE" }),
};

export const attemptsApi = {
  submit: (quizId, answers) =>
    request(`/quizzes/${quizId}/attempts`, {
      method: "POST",
      body: { answers },
    }),

  list: (quizId) =>
    request(quizId ? `/attempts?quizId=${quizId}` : "/attempts"),

  get: (id) => request(`/attempts/${id}`),
};

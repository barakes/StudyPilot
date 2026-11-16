import { lazy, Suspense } from "react";
import { BrowserRouter, Link, Route, Routes } from "react-router";
import { AuthProvider } from "./context/AuthProvider";
import { Layout } from "./components/Layout";
import { Loading } from "./components/Loading";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { DashboardPage } from "./pages/DashboardPage";
import { DocumentPage } from "./pages/DocumentPage";
import { QuizPage } from "./pages/QuizPage";
import { AttemptPage } from "./pages/AttemptPage";

// The history page uses the chart library, which is large.
// Loading it lazily keeps it out of the main bundle until the user opens that page.
const HistoryPage = lazy(() =>
  import("./pages/HistoryPage").then((module) => ({
    default: module.HistoryPage,
  })),
);

function NotFoundPage() {
  return (
    <div className="card stack">
      <h1>העמוד לא נמצא</h1>
      <p className="muted">ייתכן שהקישור שגוי או שהעמוד נמחק.</p>
      <Link to="/">חזרה לעמוד הראשי</Link>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />

          <Route element={<ProtectedRoute />}>
            <Route element={<Layout />}>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/documents/:id" element={<DocumentPage />} />
              <Route path="/quizzes/:id" element={<QuizPage />} />
              <Route path="/attempts/:id" element={<AttemptPage />} />
              <Route
                path="/history"
                element={
                  <Suspense fallback={<Loading />}>
                    <HistoryPage />
                  </Suspense>
                }
              />
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}

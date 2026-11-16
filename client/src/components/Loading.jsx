export function Loading({ text = "טוען..." }) {
  return (
    <div className="loading" role="status">
      <div className="spinner" />
      <span>{text}</span>
    </div>
  );
}

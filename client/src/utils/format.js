const dateFormatter = new Intl.DateTimeFormat("he-IL", {
  day: "numeric",
  month: "short",
  year: "numeric",
});

const dateTimeFormatter = new Intl.DateTimeFormat("he-IL", {
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

export function formatDate(value) {
  return dateFormatter.format(new Date(value));
}

export function formatDateTime(value) {
  return dateTimeFormatter.format(new Date(value));
}

export function formatNumber(value) {
  return value.toLocaleString("he-IL");
}

// Hebrew counts: "בוחן אחד", "שאלה אחת", "2 בחנים"
export function formatCount(count, singular, plural, none, feminine = false) {
  if (count === 0 && none) {
    return none;
  }

  if (count === 1) {
    return `${singular} ${feminine ? "אחת" : "אחד"}`;
  }

  return `${formatNumber(count)} ${plural}`;
}

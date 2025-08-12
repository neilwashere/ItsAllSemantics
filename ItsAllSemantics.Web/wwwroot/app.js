window.ias = window.ias || {};

// Smooth scroll helper
window.ias.scrollToBottom = (selector) => {
  try {
    const el = document.querySelector(selector);
    if (!el) return;
    el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' });
  } catch { /* no-op */ }
};

// Autosize helper for textareas
window.ias.autosize = (el) => {
  if (!el) return;
  el.style.height = 'auto';
  el.style.height = Math.min(el.scrollHeight, 320) + 'px'; // cap to ~max-h-40
};

// Delegate autosize on input/focus for any [data-autosize]
document.addEventListener('input', (e) => {
  if (e.target instanceof HTMLTextAreaElement && e.target.matches('[data-autosize]')) {
    window.ias.autosize(e.target);
  }
}, true);
document.addEventListener('focus', (e) => {
  if (e.target instanceof HTMLTextAreaElement && e.target.matches('[data-autosize]')) {
    window.ias.autosize(e.target);
  }
}, true);

// Enter to send; Shift+Enter for newline for [data-chat-input]
document.addEventListener('keydown', (e) => {
  const target = e.target;
  if (!(target instanceof HTMLTextAreaElement)) return;
  if (!target.matches('[data-chat-input]')) return;
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    const form = target.closest('form');
    const send = form?.querySelector('[data-ias-send]');
    if (send instanceof HTMLButtonElement && !send.disabled) {
      send.click();
    }
  }
}, true);

// Settings page: load/edit/save PC-side behaviour via the REST API.
// The Replace/Enter toggles live on the phone page itself (a per-visit
// choice stored in that browser's localStorage), not here.
(function () {
  const $ = (id) => document.getElementById(id);

  function toast(text, kind) {
    const el = $("toast");
    el.textContent = text;
    el.className = "toast " + (kind || "");
    setTimeout(() => { el.textContent = ""; el.className = "toast"; }, 2500);
  }

  async function load() {
    if (!window.Pairing.hasToken) {
      toast("Missing pairing code — open this page from the PC tray icon", "err");
      return;
    }
    try {
      const res = await fetch("/api/config", { headers: { "X-VibeCast-Token": window.Pairing.token } });
      if (res.status === 401) { toast("Pairing code rejected", "err"); return; }
      const cfg = await res.json();
      $("notify").checked = cfg.notifyOnInject !== false;
      $("keyDelay").value = cfg.keyDelayMs ?? 25;
    } catch {
      toast("Failed to load settings", "err");
    }
  }

  async function save() {
    const body = {
      notifyOnInject: $("notify").checked,
      keyDelayMs: parseInt($("keyDelay").value, 10) || 0,
    };
    try {
      const res = await fetch("/api/config", {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-VibeCast-Token": window.Pairing.token },
        body: JSON.stringify(body),
      });
      const out = await res.json();
      toast(out.ok ? "Saved ✓" : (out.error || "Save failed"), out.ok ? "ok" : "err");
    } catch {
      toast("Save failed", "err");
    }
  }

  $("saveBtn").addEventListener("click", save);

  load();
})();

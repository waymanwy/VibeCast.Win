// VibeCast mobile client.
// Voice input comes from the phone's own keyboard (its mic button) — this page
// only relays whatever text ends up in the textarea to the PC over WebSocket.
// No target picker: text always goes to whatever's focused on the PC, using the
// small Replace/Enter toggles below the textarea (remembered per browser).
(function () {
  const $ = (id) => document.getElementById(id);

  const els = {
    statusDot: $("statusDot"),
    statusText: $("statusText"),
    text: $("text"),
    clearBtn: $("clearBtn"),
    sendBtn: $("sendBtn"),
    undoBtn: $("undoBtn"),
    toast: $("toast"),
    focusPreview: $("focusPreview"),
    uiLangBtn: $("uiLangBtn"),
    replaceToggle: $("replaceToggle"),
    submitToggle: $("submitToggle"),
  };

  const t = (k) => window.I18N.t(k);

  // ---------------- Replace/Enter toggles (remembered per browser) ----------------
  const TOGGLE_KEY = "vc_toggles";
  let toggles = { replace: false, submit: false };
  try {
    const saved = JSON.parse(localStorage.getItem(TOGGLE_KEY) || "{}");
    toggles = { replace: !!saved.replace, submit: !!saved.submit };
  } catch {}

  function renderToggles() {
    els.replaceToggle.setAttribute("aria-pressed", String(toggles.replace));
    els.submitToggle.setAttribute("aria-pressed", String(toggles.submit));
  }

  function saveToggles() {
    localStorage.setItem(TOGGLE_KEY, JSON.stringify(toggles));
  }

  els.replaceToggle.addEventListener("click", () => {
    toggles.replace = !toggles.replace;
    renderToggles();
    saveToggles();
  });
  els.submitToggle.addEventListener("click", () => {
    toggles.submit = !toggles.submit;
    renderToggles();
    saveToggles();
  });
  renderToggles();

  // ---------------- WebSocket ----------------
  let ws = null;
  let reconnectTimer = null;
  let connected = false;

  function wsUrl() {
    const proto = location.protocol === "https:" ? "wss:" : "ws:";
    return `${proto}//${location.host}/ws?token=${encodeURIComponent(window.Pairing.token)}`;
  }

  function setConnected(state) {
    connected = state;
    els.statusDot.className = "dot " + (state ? "connected" : "disconnected");
    els.statusText.textContent = t(state ? "connected" : "disconnected");
    els.sendBtn.disabled = !state;
  }

  function connect() {
    try {
      ws = new WebSocket(wsUrl());
    } catch (e) {
      scheduleReconnect();
      return;
    }

    ws.onopen = () => {
      setConnected(true);
      send({ type: "hello", client: navigator.userAgent });
    };

    ws.onmessage = (ev) => {
      let msg;
      try { msg = JSON.parse(ev.data); } catch { return; }
      if (msg.type === "ack") handleAck(msg);
      else if (msg.type === "undoAck") handleUndoAck(msg);
      else if (msg.type === "target") updateFocusPreview(msg.title);
      else if (msg.type === "error") handleServerError(msg);
    };

    ws.onclose = () => { setConnected(false); scheduleReconnect(); };
    ws.onerror = () => { try { ws.close(); } catch {} };
  }

  function scheduleReconnect() {
    if (reconnectTimer) return;
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null;
      connect();
    }, 1500);
  }

  function send(obj) {
    if (ws && ws.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify(obj));
      return true;
    }
    return false;
  }

  function handleServerError(msg) {
    toast(msg.message === "unauthorized" ? t("bad_pairing") : (msg.message || t("send_failed")), "err");
  }

  // ---------------- Focus preview ----------------
  let lastFocusTitle = null;
  function updateFocusPreview(title) {
    lastFocusTitle = title;
    if (title) {
      els.focusPreview.textContent = t("typing_into") + title;
      els.focusPreview.classList.add("known");
    } else {
      els.focusPreview.textContent = t("no_focus_info");
      els.focusPreview.classList.remove("known");
    }
  }

  // ---------------- Send / Undo ----------------
  let pendingSend = false;
  let pendingUndo = false;

  function doSend() {
    const text = els.text.value;
    if (!text.trim()) { toast(t("nothing"), "err"); return; }
    if (!connected) { toast(t("not_connected"), "err"); return; }

    pendingSend = true;
    const ok = send({
      type: "inject",
      text,
      mode: toggles.replace ? "dialog" : "editor",
      submit: toggles.submit,
    });
    if (!ok) { pendingSend = false; toast(t("send_failed"), "err"); }
  }

  function handleAck(msg) {
    if (!pendingSend) return;
    pendingSend = false;
    if (msg.ok) {
      toast(t("sent") + (msg.target ? " → " + msg.target : ""), "ok");
      els.text.value = "";
      els.undoBtn.hidden = toggles.submit; // submitted text likely already committed
    } else {
      toast((msg.message ? msg.message : t("send_failed")), "err");
    }
  }

  function doUndo() {
    if (!connected) { toast(t("not_connected"), "err"); return; }
    pendingUndo = true;
    const ok = send({ type: "undo" });
    if (!ok) { pendingUndo = false; toast(t("undo_failed"), "err"); }
  }

  function handleUndoAck(msg) {
    if (!pendingUndo) return;
    pendingUndo = false;
    toast(msg.ok ? t("undone") : (msg.message || t("undo_failed")), msg.ok ? "ok" : "err");
    els.undoBtn.hidden = true;
  }

  let toastTimer = null;
  function toast(text, kind) {
    els.toast.textContent = text;
    els.toast.className = "toast " + (kind || "");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
      els.toast.textContent = "";
      els.toast.className = "toast";
    }, 2500);
  }

  // ---------------- Add-to-home-screen hint ----------------
  const INSTALL_DISMISSED_KEY = "vc_install_dismissed";
  function maybeShowInstallBanner() {
    const isStandalone = window.matchMedia("(display-mode: standalone)").matches || window.navigator.standalone;
    if (isStandalone) return;
    if (localStorage.getItem(INSTALL_DISMISSED_KEY)) return;
    $("installBanner").hidden = false;
  }
  $("installDismiss").addEventListener("click", () => {
    $("installBanner").hidden = true;
    localStorage.setItem(INSTALL_DISMISSED_KEY, "1");
  });

  // ---------------- Wire up ----------------
  els.sendBtn.addEventListener("click", doSend);
  els.undoBtn.addEventListener("click", doUndo);
  els.clearBtn.addEventListener("click", () => { els.text.value = ""; });
  els.uiLangBtn.addEventListener("click", () => {
    window.I18N.toggle();
    setConnected(connected);
    updateFocusPreview(lastFocusTitle);
  });

  setConnected(false);
  updateFocusPreview(null);
  maybeShowInstallBanner();
  if (!window.Pairing.hasToken) {
    toast(t("no_pairing"), "err");
    els.statusText.textContent = t("disconnected");
  } else {
    connect();
  }
})();

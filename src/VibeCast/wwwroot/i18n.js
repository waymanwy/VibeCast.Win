// Minimal i18n for the mobile page. Extend the dictionaries to add languages.
(function () {
  const dict = {
    en: {
      connecting: "connecting…",
      connected: "connected",
      disconnected: "disconnected",
      placeholder: "Use your keyboard's mic button to dictate, or type here…",
      toggle_replace: "Replace",
      toggle_submit: "↵ Enter",
      clear: "Clear",
      send: "Send to PC",
      sent: "Sent ✓",
      undo: "Undo",
      undone: "Undone ✓",
      undo_failed: "Undo failed",
      nothing: "Nothing to send",
      send_failed: "Send failed",
      not_connected: "Not connected to PC",
      no_pairing: "This link is missing a pairing code — scan the QR code from the PC again",
      bad_pairing: "Pairing code rejected — scan the QR code from the PC again",
      typing_into: "→ Typing into: ",
      no_focus_info: "→ Target unknown — click the input box on the PC first",
      install_hint: "Add this page to your home screen for one-tap access.",
      help_title: "Not working?",
      help_voice: "Tap the microphone icon on your own keyboard (Gboard etc.) to dictate — VibeCast just relays the text.",
      help_focus: "On the PC, click the target input box before pressing Send.",
      help_toggles: "\"Replace\" clears the field first instead of inserting; \"Enter\" submits right after sending.",
      help_pair: "Opened without a code? Scan the QR code from the PC tray icon again."
    },
    zh: {
      connecting: "连接中…",
      connected: "已连接",
      disconnected: "已断开",
      placeholder: "用手机输入法的麦克风按钮口述，或直接在此输入…",
      toggle_replace: "替换",
      toggle_submit: "↵ 回车",
      clear: "清空",
      send: "发送到电脑",
      sent: "已发送 ✓",
      undo: "撤销",
      undone: "已撤销 ✓",
      undo_failed: "撤销失败",
      nothing: "没有内容可发送",
      send_failed: "发送失败",
      not_connected: "未连接到电脑",
      no_pairing: "这个链接缺少配对码——请重新扫描电脑上的二维码",
      bad_pairing: "配对码不正确——请重新扫描电脑上的二维码",
      typing_into: "→ 将输入到：",
      no_focus_info: "→ 未知目标——请先在电脑上点好输入框",
      install_hint: "把这个页面添加到主屏幕，以后一键打开。",
      help_title: "无法使用？",
      help_voice: "点击手机输入法自带的麦克风图标（如 Gboard）口述，VibeCast 只负责把文字传过去。",
      help_focus: "在电脑上，先点好要接收文字的输入框，再按发送。",
      help_toggles: "「替换」会先清空输入框再写入，不勾选就是在光标处插入；「回车」是发送后自动按一次回车。",
      help_pair: "打开的链接没带配对码？请重新扫描电脑托盘图标弹出的二维码。"
    }
  };

  // Pick default from browser language, but let the user toggle.
  let lang = (navigator.language || "en").toLowerCase().startsWith("zh") ? "zh" : "en";

  function t(key) {
    return (dict[lang] && dict[lang][key]) || dict.en[key] || key;
  }

  function apply() {
    document.querySelectorAll("[data-i18n]").forEach((el) => {
      el.textContent = t(el.getAttribute("data-i18n"));
    });
    document.querySelectorAll("[data-i18n-ph]").forEach((el) => {
      el.setAttribute("placeholder", t(el.getAttribute("data-i18n-ph")));
    });
    document.documentElement.lang = lang === "zh" ? "zh-CN" : "en";
  }

  function toggle() {
    lang = lang === "zh" ? "en" : "zh";
    apply();
  }

  window.I18N = { t, apply, toggle, get lang() { return lang; } };
  document.addEventListener("DOMContentLoaded", apply);
})();

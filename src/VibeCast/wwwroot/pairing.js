// Reads the pairing token from the URL (put there by the PC's QR code / tray
// link), remembers it in localStorage so future visits don't need the query
// string, and strips it out of the visible address bar.
(function () {
  const KEY = "vc_token";

  const params = new URLSearchParams(location.search);
  const fromUrl = params.get("token");
  if (fromUrl) {
    localStorage.setItem(KEY, fromUrl);
    params.delete("token");
    const rest = params.toString();
    const clean = location.pathname + (rest ? "?" + rest : "") + location.hash;
    history.replaceState(null, "", clean);
  }

  window.Pairing = {
    get token() { return localStorage.getItem(KEY) || ""; },
    get hasToken() { return !!localStorage.getItem(KEY); },
  };
})();

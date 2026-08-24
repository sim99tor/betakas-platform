/* =============================================================
   Betakas — sunucu köprüsü

   Kalıcılık, kimlik ve İŞ KURALLARI sunucudadır. Tarayıcı ham state yazamaz;
   her değişiklik kendi dar ucundan geçer ve sunucu kuralını doğrular:

     • oturum   → POST /api/auth/login       (kısa ömürlü JWT + yenileme jetonu)
     • tazeleme → POST /api/auth/refresh     (jeton döndürme / rotation)
     • okuma    → GET  /api/state            (sunucu veritabanından derler)
     • eylem    → POST /api/requests, /api/sessions/{id}/accept, …
                  başarılı her eylem GÜNCEL STATE'i geri döndürür
     • yoklama  → GET  /api/state/rev        (başkası yazdıysa yeniden yükle)

   Erişim jetonu 20 dakikada dolar; 401 alındığında yenileme jetonuyla sessizce
   tazelenir ve istek bir kez otomatik tekrarlanır.
   ============================================================= */

var Api = (function () {
  var TOKEN_KEY = "betakas_token";
  var REFRESH_KEY = "betakas_refresh";

  function get(k) { try { return localStorage.getItem(k) || null; } catch (e) { return null; } }
  function set(k, v) {
    try { if (v) localStorage.setItem(k, v); else localStorage.removeItem(k); } catch (e) { /* yok say */ }
  }

  function token() { return get(TOKEN_KEY); }
  function refreshToken() { return get(REFRESH_KEY); }

  function storePair(res) {
    set(TOKEN_KEY, res.token);
    set(REFRESH_KEY, res.refreshToken);
    return res;
  }

  function clearTokens() { set(TOKEN_KEY, null); set(REFRESH_KEY, null); }

  // Aynı anda birden çok istek 401 alırsa tek bir tazeleme yapılsın diye paylaşılan söz.
  var refreshing = null;

  function doRefresh() {
    if (refreshing) return refreshing;
    var rt = refreshToken();
    if (!rt) return Promise.reject(new Error("no-refresh-token"));

    refreshing = raw("POST", "/api/auth/refresh", { refreshToken: rt })
      .then(function (res) { refreshing = null; return storePair(res); },
            function (err) { refreshing = null; clearTokens(); throw err; });
    return refreshing;
  }

  // Tek seferlik istek — tazeleme denemeden.
  function raw(method, path, body) {
    var opts = { method: method, headers: {} };
    var t = token();
    if (t) opts.headers["Authorization"] = "Bearer " + t;
    if (body !== undefined) {
      opts.headers["Content-Type"] = "application/json";
      opts.body = JSON.stringify(body);
    }

    return fetch(path, opts).then(function (res) {
      if (res.status === 204) return null;
      return res.text().then(function (text) {
        var data = null;
        if (text) { try { data = JSON.parse(text); } catch (e) { data = { error: text }; } }
        if (res.ok) return data;
        var err = new Error((data && data.error) || ("HTTP " + res.status));
        err.status = res.status;
        err.data = data || {};
        throw err;
      });
    });
  }

  // 401 alırsa bir kez tazeleyip tekrar dener.
  function request(method, path, body) {
    return raw(method, path, body).catch(function (err) {
      if (err.status !== 401 || !refreshToken()) throw err;
      return doRefresh().then(function () { return raw(method, path, body); });
    });
  }

  return {
    token: token,
    isAuthed: function () { return !!token(); },
    clearTokens: clearTokens,

    // --- Oturum ---
    login: function (email, pw, role) {
      return raw("POST", "/api/auth/login", { email: email, password: pw, role: role }).then(storePair);
    },
    register: function (payload) { return raw("POST", "/api/auth/register", payload); },
    logout: function () {
      var rt = refreshToken();
      clearTokens();
      // Sunucudaki yenileme jetonunu da iptal et; hata olsa da oturum yerelde kapanmış olur.
      return rt ? raw("POST", "/api/auth/logout", { refreshToken: rt }).catch(function () {})
                : Promise.resolve();
    },
    logoutAll: function () { return request("POST", "/api/auth/logout-all"); },

    // --- Okuma ---
    getState: function () { return request("GET", "/api/state"); },
    getPublicState: function () { return raw("GET", "/api/public/state"); },
    getRev: function () { return request("GET", "/api/state/rev"); },
    reset: function () { return request("POST", "/api/admin/reset"); },

    // --- Talepler ---
    createRequest: function (dto) { return request("POST", "/api/requests", dto); },
    boostRequest: function (id) { return request("POST", "/api/requests/" + id + "/boost"); },
    closeRequest: function (id) { return request("POST", "/api/requests/" + id + "/close"); },

    // --- Ürün sürümleri ---
    createVersion: function (dto) { return request("POST", "/api/versions", dto); },
    saveFixes: function (id, fixes) { return request("PUT", "/api/versions/" + id + "/fixes", { fixes: fixes }); },

    // --- Oturumlar: testçi ---
    apply: function (requestId) { return request("POST", "/api/requests/" + requestId + "/apply"); },
    submitFeedback: function (id, dto) { return request("POST", "/api/sessions/" + id + "/submit", dto); },
    rateOwner: function (id, rating) { return request("POST", "/api/sessions/" + id + "/rate-owner", { rating: rating }); },

    // --- Oturumlar: kurucu ---
    approveApp: function (id) { return request("POST", "/api/sessions/" + id + "/approve"); },
    rejectApp: function (id) { return request("POST", "/api/sessions/" + id + "/reject"); },
    acceptFeedback: function (id, rating) { return request("POST", "/api/sessions/" + id + "/accept", { rating: rating }); },
    disputeFeedback: function (id, note) { return request("POST", "/api/sessions/" + id + "/dispute", { note: note }); },

    // --- Faturalama ---
    buyPackage: function (packageId, card) { return request("POST", "/api/billing/buy", { packageId: packageId, card: card }); },
    subscribe: function (planId, card) { return request("POST", "/api/billing/subscribe", { planId: planId, card: card }); },
    cancelSubscription: function () { return request("POST", "/api/billing/cancel"); },
    renewSubscription: function () { return request("POST", "/api/billing/renew"); },

    // --- Çekimler ---
    requestWithdrawal: function (amount, iban) { return request("POST", "/api/withdrawals", { amount: amount, iban: iban }); },
    resolveWithdrawal: function (id, outcome, note) {
      return request("POST", "/api/withdrawals/" + id + "/resolve", { outcome: outcome, note: note });
    },

    // --- Profil ---
    saveExpertise: function (categories, other) {
      return request("PUT", "/api/me/expertise", { categories: categories, other: other });
    },

    // --- Yönetim ---
    resolveDispute: function (id, outcome) {
      return request("POST", "/api/sessions/" + id + "/resolve-dispute", { outcome: outcome });
    },
    approveUser: function (id) { return request("POST", "/api/admin/users/" + id + "/approve"); },
    rejectUser: function (id) { return request("POST", "/api/admin/users/" + id + "/reject"); },
    setFeePct: function (value) { return request("PUT", "/api/admin/settings/fee", { value: value }); },
    setTokenPrice: function (value) { return request("PUT", "/api/admin/settings/token-price", { value: value }); }
  };
})();

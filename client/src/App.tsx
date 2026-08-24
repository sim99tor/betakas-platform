import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { Shell } from "./components/Shell";
import { Toasts } from "./components/Toasts";
import { Login } from "./pages/Login";
import { Dashboard } from "./pages/Dashboard";
import { Discover } from "./pages/Discover";
import { Tasks } from "./pages/Tasks";
import { Versions } from "./pages/Versions";
import { VersionDetail } from "./pages/VersionDetail";
import { NewRequest } from "./pages/NewRequest";
import { BuyTokens } from "./pages/BuyTokens";
import { Ledger } from "./pages/Ledger";
import { Radar } from "./pages/Radar";
import { RequestDetail } from "./pages/RequestDetail";
import { Profile } from "./pages/Profile";
import { Admin } from "./pages/Admin";
import { Revenue } from "./pages/Revenue";
import { Landing } from "./pages/Landing";
import { PublicProfile } from "./pages/PublicProfile";
import { useBetakas } from "./state/BetakasProvider";
import type { Role } from "./lib/types";

/** Giriş kapısı: oturum yoksa giriş ekranına, rol yetkisi yoksa kendi paneline yollar. */
function Protected({ roles, children }: { roles?: Role[]; children: ReactNode }) {
  const { isLoggedIn, me, booting } = useBetakas();
  const location = useLocation();

  if (booting) return <Splash />;
  if (!isLoggedIn || !me) return <Navigate to="/giris" state={{ from: location }} replace />;
  if (roles && !roles.includes(me.role)) {
    return <Navigate to={me.role === "admin" ? "/yonetim" : "/panel"} replace />;
  }

  return <Shell>{children}</Shell>;
}

function Splash() {
  return (
    <div className="splash">
      <div className="brand"><span className="logo">B</span> Betakas</div>
      <p>Yükleniyor…</p>
    </div>
  );
}

export default function App() {
  const { booting, isLoggedIn, me } = useBetakas();

  return (
    <>
      <Routes>
        {/* Giriş gerektirmeyen sayfalar */}
        <Route
          path="/giris"
          element={
            booting ? <Splash />
              : isLoggedIn && me ? <Navigate to={me.role === "admin" ? "/yonetim" : "/panel"} replace />
                : <Login />
          }
        />
        <Route path="/tanitim" element={<Landing />} />
        <Route path="/t/:userId" element={<PublicProfile />} />

        {/* Kurucu + testçi */}
        <Route path="/panel" element={<Protected roles={["founder", "tester"]}><Dashboard /></Protected>} />
        <Route path="/kesfet" element={<Protected roles={["founder", "tester"]}><Discover /></Protected>} />
        <Route path="/talep/:requestId" element={<Protected><RequestDetail /></Protected>} />
        <Route path="/surum/:versionId" element={<Protected><VersionDetail /></Protected>} />
        <Route path="/profil" element={<Protected><Profile /></Protected>} />
        <Route path="/profil/:userId" element={<Protected><Profile /></Protected>} />
        <Route path="/defter" element={<Protected><Ledger /></Protected>} />
        <Route path="/radar" element={<Protected><Radar /></Protected>} />

        {/* Yalnızca kurucu */}
        <Route path="/surumler" element={<Protected roles={["founder"]}><Versions /></Protected>} />
        <Route path="/yeni-talep" element={<Protected roles={["founder"]}><NewRequest /></Protected>} />
        <Route path="/token-satin-al" element={<Protected roles={["founder"]}><BuyTokens /></Protected>} />

        {/* Yalnızca testçi */}
        <Route path="/gorevlerim" element={<Protected roles={["tester"]}><Tasks /></Protected>} />

        {/* Yalnızca yönetim */}
        <Route path="/yonetim" element={<Protected roles={["admin"]}><Admin /></Protected>} />
        <Route path="/gelir" element={<Protected roles={["admin"]}><Revenue /></Protected>} />

        <Route path="*" element={<Navigate to={isLoggedIn ? "/panel" : "/giris"} replace />} />
      </Routes>

      <Toasts />
    </>
  );
}

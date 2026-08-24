import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { BetakasProvider } from "./state/BetakasProvider";
import "./styles/betakas.css";
import "./styles/app.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <BetakasProvider>
        <App />
      </BetakasProvider>
    </BrowserRouter>
  </StrictMode>,
);

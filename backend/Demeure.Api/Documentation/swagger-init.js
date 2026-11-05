window.addEventListener("load", () => {
  SwaggerUIBundle({
    url: "/openapi/v1.json",
    dom_id: "#swagger-ui",
    validatorUrl: null,
    persistAuthorization: false,
    parameterMacro: (_operation, parameter) =>
      parameter.name === "X-CSRF-TOKEN" ? "automatically supplied on execution" : undefined,
    requestInterceptor: async (request) => {
      request.credentials = "same-origin";
      const target = new URL(request.url, window.location.origin);
      if (target.origin === window.location.origin &&
          target.pathname.startsWith("/api/") &&
          !["GET", "HEAD", "OPTIONS"].includes(request.method.toUpperCase())) {
        const response = await fetch("/api/v1/auth/csrf", {
          credentials: "same-origin",
          cache: "no-store"
        });
        if (!response.ok) throw new Error("Unable to obtain a CSRF token.");
        const { token } = await response.json();
        if (typeof token !== "string" || token.length === 0) {
          throw new Error("The CSRF response did not contain a token.");
        }
        request.headers["X-CSRF-TOKEN"] = token;
      }
      return request;
    }
  });
});

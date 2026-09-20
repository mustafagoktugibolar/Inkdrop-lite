# Inkdrop Lite Client

Vue 3 client for the Inkdrop Lite ASP.NET Core API.

## Backend connection

Development requests to `/api` and `/health` are proxied to
`http://localhost:5208`. Protected API calls acquire the configured Entra ID
scope with MSAL and send the access token as a bearer token.

Copy `.env.example` to `.env.development` and fill in the Entra values. The
repository already contains a local development file for the current app
registration; it is excluded from Git.

In Microsoft Entra admin center, add this exact SPA redirect URI to the client
app registration:

```text
http://localhost:52364/auth-redirect.html
```

The API app registration must expose the scope configured by `VITE_API_SCOPE`,
currently `access_as_user`.

Run the backend and client in separate terminals:

```sh
dotnet run --project ../Inkdrop-lite/Inkdrop-lite.csproj --launch-profile https
npm run dev
```

## Recommended IDE Setup

[VS Code](https://code.visualstudio.com/) + [Vue (Official)](https://marketplace.visualstudio.com/items?itemName=Vue.volar) (and disable Vetur).

## Recommended Browser Setup

- Chromium-based browsers (Chrome, Edge, Brave, etc.):
  - [Vue.js devtools](https://chromewebstore.google.com/detail/vuejs-devtools/nhdogjmejiglipccpnnnanhbledajbpd)
  - [Turn on Custom Object Formatter in Chrome DevTools](http://bit.ly/object-formatters)
- Firefox:
  - [Vue.js devtools](https://addons.mozilla.org/en-US/firefox/addon/vue-js-devtools/)
  - [Turn on Custom Object Formatter in Firefox DevTools](https://fxdx.dev/firefox-devtools-custom-object-formatters/)

## Type Support for `.vue` Imports in TS

TypeScript cannot handle type information for `.vue` imports by default, so we replace the `tsc` CLI with `vue-tsc` for type checking. In editors, we need [Volar](https://marketplace.visualstudio.com/items?itemName=Vue.volar) to make the TypeScript language service aware of `.vue` types.

## Customize configuration

See [Vite Configuration Reference](https://vite.dev/config/).

## Project Setup

```sh
npm install
```

### Compile and Hot-Reload for Development

```sh
npm run dev
```

### Type-Check, Compile and Minify for Production

```sh
npm run build
```

### Lint with [ESLint](https://eslint.org/)

```sh
npm run lint
```

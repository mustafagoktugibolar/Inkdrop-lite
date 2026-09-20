import {
  BrowserCacheLocation,
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
} from '@azure/msal-browser'

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID
const apiScope = import.meta.env.VITE_API_SCOPE
const redirectUri = `${window.location.origin}/auth-redirect.html`

if (!clientId || !tenantId || !apiScope) {
  throw new Error(
    'Missing Entra configuration. Set VITE_ENTRA_CLIENT_ID, VITE_ENTRA_TENANT_ID and VITE_API_SCOPE.',
  )
}

const msal = new PublicClientApplication({
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: BrowserCacheLocation.LocalStorage,
  },
})

let initialization: Promise<void> | undefined

export function initializeAuth(): Promise<void> {
  initialization ??= (async () => {
    await msal.initialize()

    const accounts = msal.getAllAccounts()
    if (!msal.getActiveAccount() && accounts.length > 0) {
      msal.setActiveAccount(accounts[0] ?? null)
    }
  })()

  return initialization
}

export async function signIn(): Promise<AccountInfo> {
  await initializeAuth()

  const response = await msal.loginPopup({
    scopes: [apiScope],
    redirectUri,
    prompt: 'select_account',
  })

  msal.setActiveAccount(response.account)
  return response.account
}

export async function signOut(): Promise<void> {
  await initializeAuth()

  const account = msal.getActiveAccount()
  if (!account) {
    return
  }

  await msal.logoutPopup({
    account,
    postLogoutRedirectUri: window.location.origin,
  })
}

export function getCurrentAccount(): AccountInfo | null {
  return msal.getActiveAccount()
}

export async function getAccessToken(): Promise<string> {
  await initializeAuth()

  const account = msal.getActiveAccount()
  if (!account) {
    throw new Error('Sign in before calling the API.')
  }

  const request = {
    account,
    scopes: [apiScope],
    redirectUri,
  }

  try {
    const response = await msal.acquireTokenSilent(request)
    return response.accessToken
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError)) {
      throw error
    }

    const response = await msal.acquireTokenPopup(request)
    return response.accessToken
  }
}

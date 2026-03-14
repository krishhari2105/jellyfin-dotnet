const KEYS = {
  serverUrl: 'jf.serverUrl',
  userId: 'jf.userId',
  accessToken: 'jf.accessToken',
  userName: 'jf.userName',
};

export const storage = {
  get serverUrl() {
    return localStorage.getItem(KEYS.serverUrl) ?? '';
  },
  set serverUrl(value) {
    localStorage.setItem(KEYS.serverUrl, value);
  },
  get userId() {
    return localStorage.getItem(KEYS.userId) ?? '';
  },
  set userId(value) {
    localStorage.setItem(KEYS.userId, value);
  },
  get accessToken() {
    return localStorage.getItem(KEYS.accessToken) ?? '';
  },
  set accessToken(value) {
    localStorage.setItem(KEYS.accessToken, value);
  },
  get userName() {
    return localStorage.getItem(KEYS.userName) ?? '';
  },
  set userName(value) {
    localStorage.setItem(KEYS.userName, value);
  },
  clearAuth() {
    localStorage.removeItem(KEYS.accessToken);
    localStorage.removeItem(KEYS.userId);
    localStorage.removeItem(KEYS.userName);
  },
};

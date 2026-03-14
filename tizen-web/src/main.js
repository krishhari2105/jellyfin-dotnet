import { jellyfinApi } from './api/jellyfinApi.js';
import { storage } from './core/storage.js';
import { templates } from './views/templates.js';

const app = document.getElementById('app');

function render(html) {
  app.innerHTML = html;
}

function withServerUrl(items) {
  return items.map((item) => ({ ...item, serverUrl: storage.serverUrl }));
}

function showServerSetup() {
  render(templates.serverSetup(storage.serverUrl));
  document.getElementById('server-form').addEventListener('submit', async (event) => {
    event.preventDefault();
    const input = document.getElementById('server-url');
    storage.serverUrl = input.value.trim();

    render(templates.loading('Verifying server...'));
    try {
      await jellyfinApi.detectServerVersion();
      showLogin();
    } catch (error) {
      render(templates.error(`Unable to connect to server. ${error.message}`));
      setTimeout(showServerSetup, 1400);
    }
  });
}

async function showLogin() {
  render(templates.loading('Loading users...'));

  try {
    const users = await jellyfinApi.getPublicUsers();
    const defaultUser = users[0]?.Name ?? storage.userName;
    render(templates.login(defaultUser));
  } catch {
    render(templates.login(storage.userName));
  }

  document.getElementById('login-form').addEventListener('submit', async (event) => {
    event.preventDefault();

    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;

    render(templates.loading('Signing in...'));

    try {
      await jellyfinApi.login(username, password);
      showHome();
    } catch (error) {
      render(templates.error(`Login failed. ${error.message}`));
      setTimeout(showLogin, 1400);
    }
  });
}

async function showHome() {
  render(templates.loading('Loading home...'));

  try {
    const [resumeItems, latestItems] = await Promise.all([
      jellyfinApi.getResumeItems(),
      jellyfinApi.getLibraryContinueWatching(),
    ]);

    render(
      templates.home({
        userName: storage.userName,
        resumeItems: withServerUrl(resumeItems),
        latestItems: withServerUrl(latestItems),
      }),
    );

    document.getElementById('logout').addEventListener('click', () => {
      storage.clearAuth();
      showLogin();
    });
  } catch (error) {
    render(templates.error(`Failed to load home screen. ${error.message}`));
    setTimeout(showLogin, 1600);
  }
}

function boot() {
  if (!storage.serverUrl) {
    showServerSetup();
    return;
  }

  if (!storage.accessToken) {
    showLogin();
    return;
  }

  showHome();
}

boot();

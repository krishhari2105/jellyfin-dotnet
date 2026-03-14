import { storage } from '../core/storage.js';

const DEVICE_PROFILE = {
  Name: 'Jellyfin Tizen Web',
  Id: 'tizen-web-port',
  Version: '0.1.0',
  DeviceName: 'Samsung Tizen TV',
  DeviceId: 'tizen-web-device',
  SupportedCommands: [],
  SupportsMediaControl: false,
};

function trimSlash(value) {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}

function buildAuthHeader() {
  const token = storage.accessToken;
  const parts = [
    `MediaBrowser Client="Jellyfin Tizen Web"`,
    'Device="Samsung TV"',
    'DeviceId="tizen-web-device"',
    'Version="0.1.0"',
  ];

  if (token) {
    parts.push(`Token="${token}"`);
  }

  return parts.join(', ');
}

async function request(path, init = {}) {
  if (!storage.serverUrl) {
    throw new Error('Server URL not configured.');
  }

  const url = `${trimSlash(storage.serverUrl)}${path}`;
  const response = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      'X-Emby-Authorization': buildAuthHeader(),
      ...(init.headers ?? {}),
    },
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

export const jellyfinApi = {
  async getPublicUsers() {
    return request('/Users/Public');
  },

  async login(username, password) {
    const payload = {
      Username: username,
      Pw: password,
    };

    const result = await request('/Users/AuthenticateByName', {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    storage.accessToken = result.AccessToken;
    storage.userId = result.User?.Id ?? '';
    storage.userName = result.User?.Name ?? username;

    return result;
  },

  async getResumeItems() {
    if (!storage.userId) {
      throw new Error('User must be authenticated first.');
    }

    const params = new URLSearchParams({
      Limit: '24',
      Recursive: 'true',
      Fields: 'PrimaryImageAspectRatio,Overview,RunTimeTicks',
      IncludeItemTypes: 'Movie,Episode',
      SortBy: 'DatePlayed',
      SortOrder: 'Descending',
    });

    const data = await request(`/Users/${storage.userId}/Items/Resume?${params.toString()}`);
    return data.Items ?? [];
  },

  async getLibraryContinueWatching() {
    if (!storage.userId) {
      throw new Error('User must be authenticated first.');
    }

    const params = new URLSearchParams({
      Limit: '48',
      Recursive: 'true',
      EnableUserData: 'true',
      Fields: 'Overview,PrimaryImageAspectRatio',
      IncludeItemTypes: 'Movie,Series',
      SortBy: 'DateCreated',
      SortOrder: 'Descending',
    });

    const data = await request(`/Users/${storage.userId}/Items?${params.toString()}`);
    return data.Items ?? [];
  },

  async detectServerVersion() {
    return request('/System/Info/Public');
  },

  getDeviceProfile() {
    return DEVICE_PROFILE;
  },
};

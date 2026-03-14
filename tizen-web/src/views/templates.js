function itemCard(item) {
  const imageTag = item.ImageTags?.Primary
    ? `<img src="${item.serverUrl}/Items/${item.Id}/Images/Primary?tag=${item.ImageTags.Primary}" alt="${item.Name}" />`
    : '<div class="card-placeholder">No Image</div>';

  return `
    <article class="card" tabindex="0">
      <div class="card-image">${imageTag}</div>
      <div class="card-meta">
        <h3>${item.Name}</h3>
        <p>${item.Overview ?? 'No description available.'}</p>
      </div>
    </article>
  `;
}

export const templates = {
  serverSetup(current) {
    return `
      <section class="panel">
        <h1>Jellyfin Tizen Web</h1>
        <p>Port starter: configure your Jellyfin server URL.</p>
        <form id="server-form">
          <label for="server-url">Server URL</label>
          <input id="server-url" name="server-url" value="${current}" placeholder="http://192.168.1.10:8096" required />
          <button type="submit">Save & Continue</button>
        </form>
      </section>
    `;
  },

  login(defaultUser) {
    return `
      <section class="panel">
        <h2>Sign In</h2>
        <form id="login-form">
          <label for="username">Username</label>
          <input id="username" value="${defaultUser}" required />
          <label for="password">Password</label>
          <input id="password" type="password" required />
          <button type="submit">Login</button>
        </form>
      </section>
    `;
  },

  home({ userName, resumeItems, latestItems }) {
    return `
      <header class="topbar">
        <h1>Welcome, ${userName}</h1>
        <button id="logout">Logout</button>
      </header>
      <section>
        <h2>Continue Watching</h2>
        <div class="card-grid">${resumeItems.map(itemCard).join('')}</div>
      </section>
      <section>
        <h2>Latest Library Items</h2>
        <div class="card-grid">${latestItems.map(itemCard).join('')}</div>
      </section>
    `;
  },

  loading(message = 'Loading...') {
    return `<section class="panel"><p>${message}</p></section>`;
  },

  error(message) {
    return `<section class="panel error"><p>${message}</p></section>`;
  },
};

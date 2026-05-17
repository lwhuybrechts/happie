// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
// Push notification handling (push, notificationclick) and push subscription renewal
// (pushsubscriptionchange) are handled in service-worker.published.js only.
self.addEventListener('fetch', () => { });

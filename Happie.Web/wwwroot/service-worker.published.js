// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('push', event => event.waitUntil(onPush(event)));
self.addEventListener('notificationclick', event => event.waitUntil(onNotificationClick(event)));
self.addEventListener('pushsubscriptionchange', event => event.waitUntil(onPushSubscriptionChange(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}


// Handle incoming push notifications.
async function onPush(event) {
    let data = { title: 'Happie', body: '', data: { url: '/' } };

    try {
        if (event.data)
            data = event.data.json();
    } catch (error) {
        console.error('Service worker: Failed to parse push payload:', error);
    }

    const title = data.title || 'Happie';
    const options = {
        body: data.body || '',
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        data: { url: data.data?.url || '/' }
    };

    await self.registration.showNotification(title, options);
}

// Handle notification click — navigate to the relevant day plan.
async function onNotificationClick(event) {
    event.notification.close();
    const url = event.notification.data?.url || '/';

    const windowClients = await clients.matchAll({ type: 'window', includeUncontrolled: true });

    // Try to focus an existing window.
    for (const client of windowClients) {
        if (client.url.includes(self.location.origin) && 'focus' in client) {
            client.navigate(url);
            return client.focus();
        }
    }

    // No existing window — open a new one.
    return clients.openWindow(url);
}

// Convert a URL-safe base64 string to a Uint8Array for PushManager.subscribe().
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

// Read push credentials from IndexedDB (stored by the main app after subscription).
async function getPushCredentials() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open('happie-push', 1);
        request.onupgradeneeded = (e) => {
            e.target.result.createObjectStore('credentials');
        };
        request.onsuccess = (e) => {
            const db = e.target.result;
            const tx = db.transaction('credentials', 'readonly');
            const store = tx.objectStore('credentials');
            const results = {};
            store.get('vapidPublicKey').onsuccess = (ev) => { results.vapidPublicKey = ev.target.result; };
            store.get('jwt').onsuccess = (ev) => { results.jwt = ev.target.result; };
            store.get('housemateId').onsuccess = (ev) => { results.housemateId = ev.target.result; };
            tx.oncomplete = () => {
                db.close();
                resolve(results);
            };
            tx.onerror = () => {
                db.close();
                reject(tx.error);
            };
        };
        request.onerror = (e) => reject(e.target.error);
    });
}

// Handle push subscription renewal when the browser rotates subscription keys.
async function onPushSubscriptionChange(event) {
    try {
        const credentials = await getPushCredentials();

        if (!credentials.vapidPublicKey || !credentials.jwt || !credentials.housemateId) {
            console.warn('Service worker: Missing push credentials for subscription renewal.');
            return;
        }

        // Re-subscribe with the same VAPID public key.
        const newSubscription = await self.registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(credentials.vapidPublicKey)
        });

        const subscriptionJson = newSubscription.toJSON();

        // Register the new subscription with the backend.
        const response = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + credentials.jwt,
                'X-Housemate-Id': credentials.housemateId
            },
            body: JSON.stringify({
                endpoint: subscriptionJson.endpoint,
                p256dhKey: subscriptionJson.keys.p256dh,
                authKey: subscriptionJson.keys.auth,
                locale: 'nl'
            })
        });

        if (!response.ok)
            console.error('Service worker: Failed to renew push subscription. Status:', response.status);
        else
            console.info('Service worker: Push subscription renewed successfully.');
    } catch (error) {
        console.error('Service worker: Error during push subscription renewal:', error);
    }
}

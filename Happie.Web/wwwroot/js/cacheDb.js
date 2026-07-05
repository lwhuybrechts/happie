// IndexedDB cache module for Happie PWA.
// Database: happie-cache, version 1.
// Object stores: dayPlanCache, calendarCache, mutationQueue.

(function () {
    "use strict";

    const DB_NAME = "happie-cache";
    const DB_VERSION = 1;

    const STORE_DAY_PLAN = "dayPlanCache";
    const STORE_CALENDAR = "calendarCache";
    const STORE_MUTATION = "mutationQueue";

    let _db = null;

    // Opens (or upgrades) the database. No-op if already open.
    function initialize() {
        if (_db)
            return Promise.resolve();

        return new Promise(function (resolve, reject) {
            var request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onupgradeneeded = function (e) {
                var db = e.target.result;

                if (!db.objectStoreNames.contains(STORE_DAY_PLAN)) {
                    var dayPlanStore = db.createObjectStore(STORE_DAY_PLAN, { keyPath: "key" });
                    dayPlanStore.createIndex("byHousehold", "householdId", { unique: false });
                }

                if (!db.objectStoreNames.contains(STORE_CALENDAR)) {
                    var calendarStore = db.createObjectStore(STORE_CALENDAR, { keyPath: "key" });
                    calendarStore.createIndex("byHousehold", "householdId", { unique: false });
                }

                if (!db.objectStoreNames.contains(STORE_MUTATION)) {
                    var mutationStore = db.createObjectStore(STORE_MUTATION, { keyPath: "id", autoIncrement: true });
                    mutationStore.createIndex("byHousehold", "householdId", { unique: false });
                }
            };

            request.onsuccess = function (e) {
                _db = e.target.result;
                resolve();
            };

            request.onerror = function (e) {
                reject(e.target.error);
            };
        });
    }

    // Returns the cached DayPlan entry or null. Updates the timestamp (LRU touch).
    function getDayPlan(householdId, date) {
        var key = householdId + "_" + date;
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_DAY_PLAN, "readwrite");
            var store = tx.objectStore(STORE_DAY_PLAN);
            var request = store.get(key);

            request.onsuccess = function () {
                var entry = request.result;
                if (!entry) {
                    resolve(null);
                    return;
                }
                // LRU touch: update timestamp.
                entry.timestamp = Date.now();
                store.put(entry);
                resolve(entry);
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Stores or overwrites a DayPlan entry.
    function putDayPlan(householdId, date, responseJson, timestamp) {
        var key = householdId + "_" + date;
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_DAY_PLAN, "readwrite");
            var store = tx.objectStore(STORE_DAY_PLAN);
            store.put({
                key: key,
                householdId: householdId,
                date: date,
                responseJson: responseJson,
                timestamp: timestamp
            });
            tx.oncomplete = function () { resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }

    // Deletes a DayPlan entry by key.
    function deleteDayPlan(householdId, date) {
        var key = householdId + "_" + date;
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_DAY_PLAN, "readwrite");
            var store = tx.objectStore(STORE_DAY_PLAN);
            store.delete(key);
            tx.oncomplete = function () { resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }

    // Returns the count of DayPlan entries for the given householdId.
    function getDayPlanCount(householdId) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_DAY_PLAN, "readonly");
            var store = tx.objectStore(STORE_DAY_PLAN);
            var index = store.index("byHousehold");
            var request = index.count(IDBKeyRange.only(householdId));

            request.onsuccess = function () {
                resolve(request.result);
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Returns the key of the oldest DayPlan entry (lowest timestamp) for the given householdId.
    function getOldestDayPlanKey(householdId) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_DAY_PLAN, "readonly");
            var store = tx.objectStore(STORE_DAY_PLAN);
            var index = store.index("byHousehold");
            var request = index.openCursor(IDBKeyRange.only(householdId));

            var oldestKey = null;
            var oldestTimestamp = Infinity;

            request.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    if (cursor.value.timestamp < oldestTimestamp) {
                        oldestTimestamp = cursor.value.timestamp;
                        oldestKey = cursor.value.key;
                    }
                    cursor.continue();
                } else {
                    resolve(oldestKey);
                }
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Returns the cached Calendar entry or null.
    function getCalendar(householdId, cacheKey) {
        var key = householdId + "_" + cacheKey;
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_CALENDAR, "readonly");
            var store = tx.objectStore(STORE_CALENDAR);
            var request = store.get(key);

            request.onsuccess = function () {
                resolve(request.result || null);
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Stores or overwrites a Calendar entry.
    function putCalendar(householdId, cacheKey, responseJson, timestamp) {
        var key = householdId + "_" + cacheKey;
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_CALENDAR, "readwrite");
            var store = tx.objectStore(STORE_CALENDAR);
            store.put({
                key: key,
                householdId: householdId,
                month: cacheKey,
                responseJson: responseJson,
                timestamp: timestamp
            });
            tx.oncomplete = function () { resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }

    // Deletes a Calendar entry by key.
    function deleteCalendar(householdId, cacheKey) {
        var key = householdId + "_" + cacheKey;
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_CALENDAR, "readwrite");
            var store = tx.objectStore(STORE_CALENDAR);
            store.delete(key);
            tx.oncomplete = function () { resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }

    // Computes month offsets for cluster calculation.
    // Returns {year, month} from a "yyyy-MM" string.
    function parseMonth(monthStr) {
        var parts = monthStr.split("-");
        return { year: parseInt(parts[0], 10), month: parseInt(parts[1], 10) };
    }

    // Converts {year, month} to absolute months for distance calculation.
    function toAbsoluteMonths(parsed) {
        return parsed.year * 12 + (parsed.month - 1);
    }

    // Adds an offset to a "yyyy-MM" string and returns a new "yyyy-MM" string.
    function addMonths(monthStr, offset) {
        var parsed = parseMonth(monthStr);
        var total = toAbsoluteMonths(parsed) + offset;
        var year = Math.floor(total / 12);
        var month = (total % 12) + 1;
        return year.toString().padStart(4, "0") + "-" + month.toString().padStart(2, "0");
    }

    // Returns the key of the calendar entry farthest from viewedMonth
    // that is NOT in the today cluster or viewed cluster.
    // Returns null if no eligible entry exists.
    function getEvictableCalendarKey(householdId, todayMonth, viewedMonth) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_CALENDAR, "readonly");
            var store = tx.objectStore(STORE_CALENDAR);
            var index = store.index("byHousehold");
            var request = index.openCursor(IDBKeyRange.only(householdId));

            // Build protected sets.
            var todayCluster = {};
            todayCluster[todayMonth] = true;
            todayCluster[addMonths(todayMonth, -1)] = true;
            todayCluster[addMonths(todayMonth, 1)] = true;

            var viewedCluster = {};
            viewedCluster[viewedMonth] = true;
            viewedCluster[addMonths(viewedMonth, -1)] = true;
            viewedCluster[addMonths(viewedMonth, 1)] = true;

            var viewedAbsolute = toAbsoluteMonths(parseMonth(viewedMonth));

            var farthestKey = null;
            var farthestDistance = -1;

            request.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    var entryMonth = cursor.value.month;
                    // Skip entries in either protected cluster.
                    if (!todayCluster[entryMonth] && !viewedCluster[entryMonth]) {
                        var entryAbsolute = toAbsoluteMonths(parseMonth(entryMonth));
                        var distance = Math.abs(entryAbsolute - viewedAbsolute);
                        if (distance > farthestDistance) {
                            farthestDistance = distance;
                            farthestKey = cursor.value.key;
                        }
                    }
                    cursor.continue();
                } else {
                    resolve(farthestKey);
                }
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Returns all calendar keys for the given householdId.
    function getCalendarKeys(householdId) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_CALENDAR, "readonly");
            var store = tx.objectStore(STORE_CALENDAR);
            var index = store.index("byHousehold");
            var request = index.openCursor(IDBKeyRange.only(householdId));

            var keys = [];

            request.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    keys.push(cursor.value.key);
                    cursor.continue();
                } else {
                    resolve(keys);
                }
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Adds a mutation to the queue with auto-increment id.
    function enqueueMutation(householdId, mutation) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_MUTATION, "readwrite");
            var store = tx.objectStore(STORE_MUTATION);
            var entry = {
                householdId: householdId,
                method: mutation.method,
                url: mutation.url,
                headers: mutation.headers,
                body: mutation.body || null,
                createdAt: mutation.createdAt,
                date: mutation.date,
                mutationType: mutation.mutationType
            };
            store.add(entry);
            tx.oncomplete = function () { resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }

    // Removes and returns the first mutation for the given householdId (FIFO order by id).
    function dequeueMutation(householdId) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_MUTATION, "readwrite");
            var store = tx.objectStore(STORE_MUTATION);
            var index = store.index("byHousehold");
            var request = index.openCursor(IDBKeyRange.only(householdId));

            request.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    var value = cursor.value;
                    cursor.delete();
                    resolve(value);
                } else {
                    resolve(null);
                }
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Returns all mutations for the given householdId without removing them.
    function peekAllMutations(householdId) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction(STORE_MUTATION, "readonly");
            var store = tx.objectStore(STORE_MUTATION);
            var index = store.index("byHousehold");
            var request = index.openCursor(IDBKeyRange.only(householdId));

            var mutations = [];

            request.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    mutations.push(cursor.value);
                    cursor.continue();
                } else {
                    resolve(mutations);
                }
            };

            request.onerror = function () {
                reject(request.error);
            };
        });
    }

    // Deletes all entries across all stores for the given householdId.
    function clearAll(householdId) {
        return new Promise(function (resolve, reject) {
            var tx = _db.transaction([STORE_DAY_PLAN, STORE_CALENDAR, STORE_MUTATION], "readwrite");

            // Clear dayPlanCache entries.
            var dayPlanStore = tx.objectStore(STORE_DAY_PLAN);
            var dayPlanIndex = dayPlanStore.index("byHousehold");
            var dayPlanRequest = dayPlanIndex.openCursor(IDBKeyRange.only(householdId));
            dayPlanRequest.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    cursor.delete();
                    cursor.continue();
                }
            };

            // Clear calendarCache entries.
            var calendarStore = tx.objectStore(STORE_CALENDAR);
            var calendarIndex = calendarStore.index("byHousehold");
            var calendarRequest = calendarIndex.openCursor(IDBKeyRange.only(householdId));
            calendarRequest.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    cursor.delete();
                    cursor.continue();
                }
            };

            // Clear mutationQueue entries.
            var mutationStore = tx.objectStore(STORE_MUTATION);
            var mutationIndex = mutationStore.index("byHousehold");
            var mutationRequest = mutationIndex.openCursor(IDBKeyRange.only(householdId));
            mutationRequest.onsuccess = function (e) {
                var cursor = e.target.result;
                if (cursor) {
                    cursor.delete();
                    cursor.continue();
                }
            };

            tx.oncomplete = function () { resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }

    // Returns true if IndexedDB is accessible, false otherwise.
    function isAvailable() {
        try {
            if (!window.indexedDB)
                return false;
            return true;
        } catch (e) {
            return false;
        }
    }

    // Expose the API on window.happieCache.
    window.happieCache = {
        initialize: initialize,
        getDayPlan: getDayPlan,
        putDayPlan: putDayPlan,
        deleteDayPlan: deleteDayPlan,
        getDayPlanCount: getDayPlanCount,
        getOldestDayPlanKey: getOldestDayPlanKey,
        getCalendar: getCalendar,
        putCalendar: putCalendar,
        deleteCalendar: deleteCalendar,
        getEvictableCalendarKey: getEvictableCalendarKey,
        getCalendarKeys: getCalendarKeys,
        enqueueMutation: enqueueMutation,
        dequeueMutation: dequeueMutation,
        peekAllMutations: peekAllMutations,
        clearAll: clearAll,
        isAvailable: isAvailable
    };
})();

(function(root) {
    'use strict';
    const saveKey = 'LastExitCampaignV1';
    const localPrefix = 'LastExit.Progress.v1.';
    let sdk = null;
    let player = null;
    let storage = null;
    let owner = 'guest';
    let target = null;
    let send = null;
    let levelCount = 15;
    let progress = null;
    let localReadState = 'unread';
    let loaded = false;
    let cloudReadable = false;
    let cloudWriting = false;
    let pendingCloud = null;
    let cloudRevision = 0;
    let sceneReady = false;
    let loaderGone = false;
    let readySent = false;
    let wantsGameplay = false;
    let gameplaySent = false;
    let sdkResumesGameplay = false;
    let adOperation = 0;
    let adPending = false;
    const reasons = new Set();
    const diagnostics = { sdk: 'initializing', storage: 'initializing', ad: 'idle', ready: false,
        sceneReady: false, loaderGone: false, requestedLanguage: null, language: 'ru',
        localReadState: 'unread', localWriteStatus: 'idle' };

    function notify(method, payload) {
        if (send && target) send(target, method, String(payload));
    }

    function reportStorage(status) {
        diagnostics.storage = status;
        notify('ReceiveStorageStatus', status);
    }

    function setReason(reason, paused) {
        const previous = reasons.size > 0;
        if (paused) reasons.add(reason); else reasons.delete(reason);
        if (previous !== (reasons.size > 0)) notify('ReceivePause', reasons.size > 0 ? '1' : '0');
    }

    function decode(value) {
        if (typeof value === 'string') {
            try { value = JSON.parse(value); } catch (_) { return null; }
        }
        if (!value || value.version !== 1 || !Number.isInteger(value.currentLevel) ||
            value.currentLevel < 0 || value.currentLevel >= levelCount || !Array.isArray(value.completedLevels)) {
            return null;
        }
        const completed = value.completedLevels;
        if (completed.some(index => !Number.isInteger(index) || index < 0 || index >= levelCount) ||
            new Set(completed).size !== completed.length) return null;
        const results = value.bestResults === undefined || value.bestResults === null ? [] : value.bestResults;
        if (!Array.isArray(results) || results.some(result => !result || !completed.includes(result.levelIndex) ||
            !Number.isInteger(result.levelIndex) || !Number.isInteger(result.stars) || result.stars < 1 || result.stars > 3 ||
            !Number.isInteger(result.cctvUses) || result.cctvUses < 0 || !Number.isInteger(result.emergencyResets) ||
            result.emergencyResets < 0 || !Number.isInteger(result.lostSeats) || result.lostSeats < 0) ||
            new Set(results.map(result => result.levelIndex)).size !== results.length) return null;
        return { version: 1, currentLevel: value.currentLevel, completedLevels: completed.slice().sort((a, b) => a - b),
            bestResults: results.map(result => ({ levelIndex: result.levelIndex, stars: result.stars,
                cctvUses: result.cctvUses, emergencyResets: result.emergencyResets, lostSeats: result.lostSeats })) };
    }

    function isBetter(result, previous) {
        if (result.stars !== previous.stars) return result.stars > previous.stars;
        if (result.emergencyResets !== previous.emergencyResets) return result.emergencyResets < previous.emergencyResets;
        if (result.cctvUses !== previous.cctvUses) return result.cctvUses < previous.cctvUses;
        return result.lostSeats < previous.lostSeats;
    }

    // Completion flags are monotonic; merging them is safe because this schema contains no spendable resources.
    function merge(local, cloud) {
        if (!local) return cloud;
        if (!cloud) return local;
        const completed = Array.from(new Set(local.completedLevels.concat(cloud.completedLevels))).sort((a, b) => a - b);
        const results = new Map();
        for (const result of local.bestResults.concat(cloud.bestResults)) {
            const previous = results.get(result.levelIndex);
            if (!previous || isBetter(result, previous)) results.set(result.levelIndex, result);
        }
        return { version: 1, currentLevel: Math.max(local.currentLevel, cloud.currentLevel), completedLevels: completed,
            bestResults: Array.from(results.values()).sort((a, b) => a.levelIndex - b.levelIndex) };
    }

    function readLocal() {
        try {
            if (!storage) throw new Error('Storage unavailable');
            const raw = storage.getItem(localPrefix + owner);
            const decoded = raw === null ? null : decode(raw);
            localReadState = raw === null ? 'absent' : decoded ? 'valid' : 'rejected';
            diagnostics.localReadState = localReadState;
            if (raw !== null && !decoded) reportStorage('local-invalid-preserved');
            return decoded;
        } catch (_) {
            localReadState = 'read-failed';
            diagnostics.localReadState = localReadState;
            reportStorage('local-read-failed');
            return null;
        }
    }

    function writeLocal() {
        // Rejected/future or unreadable bytes belong to their original schema/owner; never erase them on fallback.
        if (localReadState !== 'valid' && localReadState !== 'absent') {
            diagnostics.localWriteStatus = 'blocked-' + localReadState;
            reportStorage('local-' + localReadState + '-preserved');
            return false;
        }
        try {
            if (!storage) throw new Error('Storage unavailable');
            storage.setItem(localPrefix + owner, JSON.stringify(progress));
            localReadState = 'valid';
            diagnostics.localReadState = localReadState;
            diagnostics.localWriteStatus = 'saved';
            return true;
        } catch (_) {
            diagnostics.localWriteStatus = 'failed';
            reportStorage('local-write-failed');
            return false;
        }
    }

    function flushCloud() {
        if (!cloudReadable || cloudWriting || !pendingCloud) return;
        const snapshot = pendingCloud;
        const revision = cloudRevision;
        pendingCloud = null;
        cloudWriting = true;
        reportStorage('cloud-saving');
        Promise.resolve().then(() => player.setData({ [saveKey]: snapshot }, true)).then(() => {
            cloudWriting = false;
            if (revision === cloudRevision) reportStorage('cloud-saved');
            flushCloud();
        }).catch(() => {
            cloudWriting = false;
            // Keep the most recent snapshot pending; retry at the next meaningful checkpoint only.
            if (!pendingCloud) pendingCloud = snapshot;
            reportStorage('cloud-write-failed-local-pending');
        });
    }

    function bounded(promise, milliseconds) {
        return new Promise((resolve, reject) => {
            const timer = root.setTimeout(() => reject(new Error('Platform unavailable')), milliseconds);
            Promise.resolve(promise).then(value => {
                root.clearTimeout(timer);
                resolve(value);
            }, error => { root.clearTimeout(timer); reject(error); });
        });
    }

    function markReadyIfPossible() {
        if (!sdk || !sceneReady || !loaderGone || readySent) return;
        if (sdk.features && sdk.features.LoadingAPI) sdk.features.LoadingAPI.ready();
        readySent = true;
        diagnostics.ready = true;
        applyGameplay();
    }

    function applyGameplay() {
        if (!sdk || !readySent) return;
        const active = wantsGameplay && reasons.size === 0;
        if (active === gameplaySent) return;
        const api = sdk.features && sdk.features.GameplayAPI;
        if (api) { if (active) api.start(); else api.stop(); }
        gameplaySent = active;
    }

    async function initialize(loadSdk) {
        try { storage = root.localStorage; } catch (_) { /* Browsers may prohibit storage. */ }
        try {
            sdk = await bounded(loadSdk(), 5000);
            diagnostics.sdk = 'ready';
            // This release declares Russian only; read the SDK locale during startup even for one language.
            const requestedLanguage = sdk.environment && sdk.environment.i18n ? sdk.environment.i18n.lang : 'ru';
            const supportedLanguages = ['ru'];
            diagnostics.requestedLanguage = requestedLanguage;
            diagnostics.language = supportedLanguages.includes(requestedLanguage) ? requestedLanguage : 'ru';
            if (root.document.documentElement) root.document.documentElement.lang = diagnostics.language;
            sdk.on('game_api_pause', () => {
                if (reasons.has('sdk')) return;
                // SDK already stops its gameplay markup; mirror it before Unity feeds back effective pause.
                sdkResumesGameplay = gameplaySent;
                gameplaySent = false;
                setReason('sdk', true);
            });
            sdk.on('game_api_resume', () => {
                if (!reasons.has('sdk')) return;
                // SDK resumes only the gameplay it stopped. Unity may still hold another pause cause.
                gameplaySent = sdkResumesGameplay;
                sdkResumesGameplay = false;
                setReason('sdk', false);
                applyGameplay();
            });
            markReadyIfPossible();
        } catch (_) { diagnostics.sdk = 'unavailable-local-only'; }

        if (sdk) {
            try {
                player = await bounded(sdk.getPlayer(), 4000);
                owner = 'player.' + player.getUniqueID();
            } catch (_) { player = null; }
        }

        let local = readLocal();
        let cloud = null;
        if (player) {
            try {
                const data = await bounded(player.getData([saveKey]), 4000);
                const raw = data[saveKey];
                cloud = raw === undefined ? null : decode(raw);
                cloudReadable = raw === undefined || cloud !== null;
                reportStorage(cloudReadable ? 'cloud-loaded' : 'cloud-invalid-preserved');
            } catch (_) { reportStorage('cloud-read-failed-local-only'); }
        } else { reportStorage('local-only'); }

        progress = merge(local, cloud) || { version: 1, currentLevel: 0, completedLevels: [], bestResults: [] };
        // Cache valid cloud recovery only when the primary local key is safe to write.
        if (!local && cloud) writeLocal();
        loaded = true;
        notify('ReceiveProgress', JSON.stringify(progress));
    }

    root.LastExitPlatform = {
        diagnostics,
        boot(loadSdk) {
            if (!this.bootPromise) this.bootPromise = initialize(loadSdk);
            return this.bootPromise;
        },
        bind(object, count, sender) {
            target = object;
            send = sender;
            levelCount = count;
            notify('ReceivePause', reasons.size > 0 ? '1' : '0');
            notify('ReceiveStorageStatus', diagnostics.storage);
            if (loaded) notify('ReceiveProgress', JSON.stringify(progress));
        },
        markReady() { sceneReady = true; diagnostics.sceneReady = true; markReadyIfPossible(); },
        loaderRemoved() {
            // Reveal Unity's own splash as soon as its instance is created; it must not be covered by HTML.
            if (root.document.getElementById) {
                const loading = root.document.getElementById('loading');
                if (loading) loading.remove();
            }
            loaderGone = true;
            diagnostics.loaderGone = true;
            markReadyIfPossible();
        },
        setGameplay(active) { wantsGameplay = active; applyGameplay(); },
        save(json) {
            const next = decode(json);
            if (!loaded || !next) return;
            progress = merge(progress, next);
            // currentLevel is an explicit checkpoint; the completion set alone uses monotonic merge.
            progress.currentLevel = next.currentLevel;
            const localSaved = writeLocal();
            cloudRevision++;
            pendingCloud = JSON.parse(JSON.stringify(progress));
            if (cloudReadable) flushCloud();
            else if (localSaved) reportStorage('local-saved-cloud-unavailable');
        },
        showAd() {
            if (adPending) return;
            if (!sdk || !sdk.adv) { notify('ReceiveAdResult', 'unavailable'); return; }
            adPending = true;
            const operation = ++adOperation;
            setReason('ad', true);
            applyGameplay();
            const finish = result => {
                if (!adPending || operation !== adOperation) return;
                adPending = false;
                diagnostics.ad = result;
                setReason('ad', false);
                notify('ReceiveAdResult', result);
            };
            try {
                sdk.adv.showFullscreenAdv({ callbacks: {
                    onOpen: () => { if (adPending && operation === adOperation) diagnostics.ad = 'open'; },
                    onClose: shown => finish(shown ? 'closed' : 'not-shown'),
                    onError: () => finish('error')
                }});
            } catch (_) { finish('error'); }
        }
    };

    root.addEventListener('blur', () => setReason('focus', true));
    root.addEventListener('focus', () => setReason('focus', false));
    root.document.addEventListener('visibilitychange', () => setReason('hidden', root.document.hidden));
    setReason('hidden', root.document.hidden);
})(window);

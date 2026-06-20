/* Movie Wishlist DVR — Shared JavaScript */
/* global ApiClient, Dashboard */

const WishlistApp = (() => {

    // -------------------------------------------------------------------------
    // Private: API helpers
    // -------------------------------------------------------------------------

    function apiBase() {
        return ApiClient.serverAddress() + '/api/moviewishlist';
    }

    function authHeaders() {
        return { 'Authorization': 'MediaBrowser Token="' + ApiClient.accessToken() + '"' };
    }

    async function apiGet(path) {
        const res = await fetch(apiBase() + path, { headers: authHeaders() });
        if (!res.ok) {
            const text = await res.text().catch(() => res.statusText);
            throw new Error('GET ' + path + ' failed (' + res.status + '): ' + text);
        }
        return res.json();
    }

    async function apiPost(path, body) {
        const res = await fetch(apiBase() + path, {
            method: 'POST',
            headers: { ...authHeaders(), 'Content-Type': 'application/json' },
            body: body !== undefined ? JSON.stringify(body) : undefined
        });
        if (!res.ok) {
            const text = await res.text().catch(() => res.statusText);
            throw new Error('POST ' + path + ' failed (' + res.status + '): ' + text);
        }
        const ct = res.headers.get('content-type') || '';
        if (ct.includes('application/json')) return res.json();
        return null;
    }

    async function apiDelete(path) {
        const res = await fetch(apiBase() + path, {
            method: 'DELETE',
            headers: authHeaders()
        });
        if (!res.ok) {
            const text = await res.text().catch(() => res.statusText);
            throw new Error('DELETE ' + path + ' failed (' + res.status + '): ' + text);
        }
        return null;
    }

    async function apiPut(path, body) {
        const res = await fetch(apiBase() + path, {
            method: 'PUT',
            headers: { ...authHeaders(), 'Content-Type': 'application/json' },
            body: body !== undefined ? JSON.stringify(body) : undefined
        });
        if (!res.ok) {
            const text = await res.text().catch(() => res.statusText);
            throw new Error('PUT ' + path + ' failed (' + res.status + '): ' + text);
        }
        const ct = res.headers.get('content-type') || '';
        if (ct.includes('application/json')) return res.json();
        return null;
    }

    // -------------------------------------------------------------------------
    // Private: Image helpers
    // -------------------------------------------------------------------------

    function posterUrl(path) {
        return path ? 'https://image.tmdb.org/t/p/w300' + path : null;
    }

    function backdropUrl(path) {
        return path ? 'https://image.tmdb.org/t/p/w1280' + path : null;
    }

    function posterImgOrPlaceholder(path, title, cssClass) {
        const url = posterUrl(path);
        if (url) {
            return '<img src="' + escHtml(url) + '" alt="' + escHtml(title || '') + '" loading="lazy">';
        }
        return '<div class="mw-card-placeholder">' + escHtml(title || 'No poster') + '</div>';
    }

    // -------------------------------------------------------------------------
    // Private: Status helpers
    // -------------------------------------------------------------------------

    // Status enum values from plugin
    // 0=WatchingEpg, 1=Scheduled, 2=Recorded, 3=AlreadyInLibrary, 4=Missed, 5=NeedsConfirmation
    const STATUS_LABELS = {
        0: 'Watching EPG',
        1: 'Scheduled',
        2: 'Recorded',
        3: 'In Library',
        4: 'Missed',
        5: 'Needs Confirmation'
    };

    const STATUS_CLASSES = {
        0: 'mw-badge-watchingepg',
        1: 'mw-badge-scheduled',
        2: 'mw-badge-recorded',
        3: 'mw-badge-alreadyinlibrary',
        4: 'mw-badge-missed',
        5: 'mw-badge-needsconfirmation'
    };

    function statusLabel(status) {
        return STATUS_LABELS[status] !== undefined ? STATUS_LABELS[status] : 'Unknown';
    }

    function statusClass(status) {
        return STATUS_CLASSES[status] !== undefined ? STATUS_CLASSES[status] : 'mw-badge-watchingepg';
    }

    // -------------------------------------------------------------------------
    // Private: Formatting helpers
    // -------------------------------------------------------------------------

    function formatDateTime(isoString) {
        if (!isoString) return '';
        try {
            const d = new Date(isoString);
            return d.toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
                + ' ' + d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        } catch (e) {
            return isoString;
        }
    }

    function escHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function renderStars(voteAverage) {
        // Convert 0-10 TMDB rating to 0-5 stars
        const score = Math.round((voteAverage || 0) / 2);
        let html = '<span class="mw-stars">';
        for (let i = 1; i <= 5; i++) {
            html += i <= score ? '★' : '<span class="mw-stars-empty">★</span>';
        }
        html += '</span>';
        if (voteAverage) {
            html += ' <span style="color:#aaa;font-size:0.8rem;">(' + Number(voteAverage).toFixed(1) + ')</span>';
        }
        return html;
    }

    function showError(containerId, message) {
        const el = document.getElementById(containerId);
        if (el) {
            el.textContent = '⚠ ' + message;
            el.style.display = 'block';
        }
    }

    function hideError(containerId) {
        const el = document.getElementById(containerId);
        if (el) el.style.display = 'none';
    }

    // -------------------------------------------------------------------------
    // Private: Detail overlay
    // -------------------------------------------------------------------------

    async function showDetailOverlay(tmdbId, wishlistItems) {
        const overlay = document.getElementById('mw-detail-overlay');
        const content = document.getElementById('mw-detail-content');
        if (!overlay || !content) return;

        overlay.classList.remove('hidden');
        content.innerHTML = '<div class="mw-loading">Loading details…</div>';

        try {
            const movie = await apiGet('/movie/' + tmdbId);
            const isInWatchlist = Array.isArray(wishlistItems)
                && wishlistItems.some(i => String(i.tmdbId) === String(tmdbId));

            const backdrop = backdropUrl(movie.backdropPath);
            const poster = posterUrl(movie.posterPath);

            const year = movie.releaseDate ? movie.releaseDate.substring(0, 4) : '';
            const runtime = movie.runtime ? movie.runtime + ' min' : '';
            const genres = Array.isArray(movie.genres)
                ? movie.genres.map(g => '<span class="mw-genre-tag">' + escHtml(g.name || g) + '</span>').join('')
                : '';
            const cast = Array.isArray(movie.cast)
                ? movie.cast.slice(0, 5).map(c => '<span class="mw-cast-item">' + escHtml(c.name || c) + '</span>').join('')
                : '';

            let addBtn;
            if (isInWatchlist) {
                addBtn = '<button class="mw-btn mw-btn-muted" disabled>✓ In Watchlist</button>';
            } else {
                addBtn = '<button class="mw-btn mw-btn-primary" id="mw-add-btn">+ Add to Watchlist</button>';
            }

            content.innerHTML = `
                ${backdrop
                    ? '<img class="mw-detail-backdrop" src="' + escHtml(backdrop) + '" alt="">'
                    : '<div class="mw-detail-backdrop-placeholder"></div>'}
                <div class="mw-detail-body">
                    <div class="mw-detail-poster-row">
                        <div class="mw-detail-poster">
                            ${poster
                                ? '<img src="' + escHtml(poster) + '" alt="' + escHtml(movie.title) + '">'
                                : '<div class="mw-detail-poster-placeholder">' + escHtml(movie.title || '') + '</div>'}
                        </div>
                        <div class="mw-detail-info">
                            <div class="mw-detail-title">${escHtml(movie.title || '')}</div>
                            <div class="mw-detail-meta">
                                ${year ? '<span>' + escHtml(year) + '</span>' : ''}
                                ${runtime ? '<span>⏱ ' + escHtml(runtime) + '</span>' : ''}
                                ${movie.voteAverage ? '<span>' + renderStars(movie.voteAverage) + '</span>' : ''}
                            </div>
                            ${genres ? '<div class="mw-detail-genres">' + genres + '</div>' : ''}
                        </div>
                    </div>
                    ${movie.overview
                        ? '<p class="mw-detail-overview">' + escHtml(movie.overview) + '</p>'
                        : ''}
                    ${cast
                        ? '<div class="mw-detail-cast"><h4>Cast</h4><div class="mw-cast-list">' + cast + '</div></div>'
                        : ''}
                    <div class="mw-detail-actions" id="mw-detail-actions">
                        ${addBtn}
                        <button class="mw-btn mw-btn-secondary" id="mw-overlay-close-btn">Close</button>
                    </div>
                    <div id="mw-detail-error" class="mw-error" style="display:none;margin-top:10px;"></div>
                </div>
            `;

            // Wire up close button inside panel
            document.getElementById('mw-overlay-close-btn').addEventListener('click', closeDetailOverlay);

            // Wire up Add button if present
            const addBtnEl = document.getElementById('mw-add-btn');
            if (addBtnEl) {
                addBtnEl.addEventListener('click', async () => {
                    addBtnEl.disabled = true;
                    addBtnEl.textContent = 'Adding…';
                    try {
                        await apiPost('/items', {
                            tmdbId: movie.id || tmdbId,
                            title: movie.title,
                            year: year ? parseInt(year, 10) : null,
                            posterPath: movie.posterPath || null,
                            overview: movie.overview || null
                        });
                        addBtnEl.textContent = '✓ In Watchlist';
                        addBtnEl.className = 'mw-btn mw-btn-muted';
                        // Push into wishlist so re-open reflects state
                        if (Array.isArray(wishlistItems)) {
                            wishlistItems.push({ tmdbId: String(tmdbId) });
                        }
                    } catch (err) {
                        document.getElementById('mw-detail-error').textContent = err.message;
                        document.getElementById('mw-detail-error').style.display = 'block';
                        addBtnEl.disabled = false;
                        addBtnEl.textContent = '+ Add to Watchlist';
                    }
                });
            }

        } catch (err) {
            content.innerHTML = '<div class="mw-error" style="margin:20px;">' + escHtml(err.message) + '</div>';
        }
    }

    function closeDetailOverlay() {
        const overlay = document.getElementById('mw-detail-overlay');
        if (overlay) overlay.classList.add('hidden');
    }

    // -------------------------------------------------------------------------
    // Private: Render a movie card for search results
    // -------------------------------------------------------------------------

    function renderSearchCard(movie, wishlistItems) {
        const year = movie.releaseDate ? movie.releaseDate.substring(0, 4) : '';
        const inWatchlist = Array.isArray(wishlistItems)
            && wishlistItems.some(i => String(i.tmdbId) === String(movie.id));

        const wrapper = document.createElement('div');
        wrapper.className = 'mw-card-wrapper';

        wrapper.innerHTML = `
            <div class="mw-card" data-tmdb-id="${escHtml(String(movie.id))}">
                ${posterImgOrPlaceholder(movie.posterPath, movie.title)}
                <div class="mw-card-body">
                    <div class="mw-card-title" title="${escHtml(movie.title || '')}">${escHtml(movie.title || 'Untitled')}</div>
                    <div class="mw-card-meta">${escHtml(year)}${movie.voteAverage ? ' · ' + Number(movie.voteAverage).toFixed(1) + '★' : ''}</div>
                </div>
            </div>
            ${inWatchlist ? '<div class="mw-badge mw-badge-watchingepg">Added</div>' : ''}
        `;

        wrapper.querySelector('.mw-card').addEventListener('click', () => {
            showDetailOverlay(movie.id, wishlistItems);
        });

        return wrapper;
    }

    // -------------------------------------------------------------------------
    // Private: Render a watchlist card
    // -------------------------------------------------------------------------

    function renderWatchlistCard(item, onRemove) {
        const year = item.year ? String(item.year) : '';
        const status = item.status !== undefined ? item.status : 0;
        const badgeClass = statusClass(status);
        const badgeText = statusLabel(status);

        let scheduledHtml = '';
        if (status === 1 && item.channelName) {
            const dt = item.scheduledTime ? formatDateTime(item.scheduledTime) : '';
            const hd = item.isHd ? '<span class="mw-hd-badge">HD</span>' : '';
            scheduledHtml = `<div class="mw-card-scheduled">📡 ${escHtml(item.channelName)}${dt ? ' · ' + escHtml(dt) : ''} ${hd}</div>`;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'mw-card-wrapper';
        wrapper.dataset.itemId = item.id;

        wrapper.innerHTML = `
            <div class="mw-card">
                ${posterImgOrPlaceholder(item.posterPath, item.title)}
                <div class="mw-card-body">
                    <div class="mw-card-title" title="${escHtml(item.title || '')}">${escHtml(item.title || 'Untitled')}</div>
                    <div class="mw-card-meta">${escHtml(year)}</div>
                    ${scheduledHtml}
                    <button class="mw-card-remove" data-id="${escHtml(String(item.id))}">✕ Remove</button>
                </div>
            </div>
            <div class="mw-badge ${escHtml(badgeClass)}">${escHtml(badgeText)}</div>
        `;

        wrapper.querySelector('.mw-card-remove').addEventListener('click', (e) => {
            e.stopPropagation();
            if (confirm('Remove "' + (item.title || 'this movie') + '" from your watchlist?')) {
                onRemove(item.id, wrapper);
            }
        });

        return wrapper;
    }

    // -------------------------------------------------------------------------
    // Page: Discover
    // -------------------------------------------------------------------------

    async function initDiscover() {
        let wishlistItems = [];
        let debounceTimer = null;

        // Pre-load user's watchlist so we can mark already-added items
        try {
            wishlistItems = await apiGet('/items');
        } catch (e) {
            // Non-fatal: we'll just not show "added" badges
        }

        const input = document.getElementById('mw-search-input');
        const btn = document.getElementById('mw-search-btn');
        const area = document.getElementById('mw-results-area');

        if (!input || !area) return;

        // Close overlay on background click
        const overlay = document.getElementById('mw-detail-overlay');
        if (overlay) {
            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) closeDetailOverlay();
            });
        }
        const closeBtn = document.getElementById('mw-close-btn');
        if (closeBtn) closeBtn.addEventListener('click', closeDetailOverlay);

        async function doSearch(query) {
            if (!query || query.length < 2) return;
            hideError('mw-error-box');
            area.innerHTML = '<div class="mw-loading">Searching…</div>';
            try {
                const results = await apiGet('/search?query=' + encodeURIComponent(query));
                if (!results || results.length === 0) {
                    area.innerHTML = '<div class="mw-empty">No results found for <strong>' + escHtml(query) + '</strong>.</div>';
                    return;
                }
                area.innerHTML = '';
                const grid = document.createElement('div');
                grid.className = 'mw-grid';
                results.forEach(movie => {
                    grid.appendChild(renderSearchCard(movie, wishlistItems));
                });
                area.appendChild(grid);
            } catch (err) {
                area.innerHTML = '';
                showError('mw-error-box', err.message);
            }
        }

        input.addEventListener('input', () => {
            clearTimeout(debounceTimer);
            const q = input.value.trim();
            if (q.length < 2) return;
            debounceTimer = setTimeout(() => doSearch(q), 400);
        });

        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                clearTimeout(debounceTimer);
                doSearch(input.value.trim());
            }
        });

        btn.addEventListener('click', () => {
            clearTimeout(debounceTimer);
            doSearch(input.value.trim());
        });
    }

    // -------------------------------------------------------------------------
    // Page: Watchlist
    // -------------------------------------------------------------------------

    async function initWatchlist() {
        const area = document.getElementById('mw-watchlist-area');
        if (!area) return;

        async function loadAndRender() {
            hideError('mw-error-box');
            area.innerHTML = '<div class="mw-loading">Loading your watchlist…</div>';
            try {
                const items = await apiGet('/items');
                renderWatchlistGrid(items);
            } catch (err) {
                area.innerHTML = '';
                showError('mw-error-box', err.message);
            }
        }

        function renderWatchlistGrid(items) {
            if (!items || items.length === 0) {
                area.innerHTML = `
                    <div class="mw-empty">
                        <div style="font-size:2.5rem;margin-bottom:12px;">📋</div>
                        <strong>Your watchlist is empty.</strong>
                        <p>Discover movies to add them to your watchlist!</p>
                        <a href="/web/configurationpage?name=MovieWishlistDiscover" class="mw-btn mw-btn-primary" style="margin-top:16px;display:inline-block;text-decoration:none;">🎬 Browse Movies</a>
                    </div>
                `;
                return;
            }

            area.innerHTML = '';
            const grid = document.createElement('div');
            grid.className = 'mw-grid';

            items.forEach(item => {
                const card = renderWatchlistCard(item, async (id, cardEl) => {
                    try {
                        await apiDelete('/items/' + id);
                        cardEl.remove();
                        // Check if grid is now empty
                        if (grid.children.length === 0) {
                            loadAndRender();
                        }
                    } catch (err) {
                        showError('mw-error-box', 'Failed to remove item: ' + err.message);
                    }
                });
                grid.appendChild(card);
            });

            area.appendChild(grid);
        }

        await loadAndRender();
    }

    // -------------------------------------------------------------------------
    // Page: Confirmations
    // -------------------------------------------------------------------------

    async function initConfirmations() {
        const area = document.getElementById('mw-confirmations-area');
        if (!area) return;

        async function loadAndRender() {
            hideError('mw-error-box');
            area.innerHTML = '<div class="mw-loading">Loading…</div>';
            try {
                const items = await apiGet('/items');
                const pending = (items || []).filter(i => i.status === 5);

                const uncertainMatches = pending.filter(i => i.pendingEpgProgramId && !i.conflictInfo);
                const conflicts = pending.filter(i => i.conflictInfo);

                if (pending.length === 0) {
                    area.innerHTML = `
                        <div class="mw-empty">
                            <div style="font-size:2.5rem;margin-bottom:12px;">🎉</div>
                            <strong>Nothing to confirm right now!</strong>
                            <p>All items are being handled automatically.</p>
                        </div>
                    `;
                    return;
                }

                area.innerHTML = '';

                // Section: Uncertain Matches
                if (uncertainMatches.length > 0) {
                    const sectionHeader = document.createElement('div');
                    sectionHeader.className = 'mw-section-header';
                    sectionHeader.textContent = '⚠️ Confirm Match';
                    area.appendChild(sectionHeader);

                    const subtitle = document.createElement('p');
                    subtitle.className = 'mw-subtitle';
                    subtitle.textContent = 'These items have a possible EPG match — please confirm if they are correct.';
                    area.appendChild(subtitle);

                    uncertainMatches.forEach(item => {
                        area.appendChild(renderConfirmCard(item, loadAndRender));
                    });
                }

                // Section: Conflicts
                if (conflicts.length > 0) {
                    const sectionHeader = document.createElement('div');
                    sectionHeader.className = 'mw-section-header';
                    sectionHeader.textContent = '🔴 Recording Conflict';
                    area.appendChild(sectionHeader);

                    const subtitle = document.createElement('p');
                    subtitle.className = 'mw-subtitle';
                    subtitle.textContent = 'Multiple broadcasts found for these items. Choose which one to record.';
                    area.appendChild(subtitle);

                    conflicts.forEach(item => {
                        area.appendChild(renderConflictCard(item, loadAndRender));
                    });
                }

            } catch (err) {
                area.innerHTML = '';
                showError('mw-error-box', err.message);
            }
        }

        function renderConfirmCard(item, onAction) {
            const box = document.createElement('div');
            box.className = 'mw-confirm-box';

            const poster = posterUrl(item.posterPath);
            const pendingTitle = item.pendingEpgTitle || 'Unknown Title';
            const pendingYear = item.pendingEpgYear ? ' (' + item.pendingEpgYear + ')' : '';

            box.innerHTML = `
                <div class="mw-confirm-movie-row">
                    <div class="mw-confirm-poster">
                        ${poster
                            ? '<img src="' + escHtml(poster) + '" alt="' + escHtml(item.title || '') + '">'
                            : '<div class="mw-confirm-poster-placeholder">' + escHtml(item.title || '') + '</div>'}
                    </div>
                    <div class="mw-confirm-info">
                        <h3>${escHtml(item.title || 'Untitled')}${item.year ? ' <span style="color:#888;font-weight:400;">(' + escHtml(String(item.year)) + ')</span>' : ''}</h3>
                        <p>EPG found: <strong>${escHtml(pendingTitle + pendingYear)}</strong></p>
                        <p class="mw-confirm-question">Is this the right film?</p>
                        <div class="mw-confirm-actions">
                            <button class="mw-btn mw-btn-success confirm-yes-btn" data-id="${escHtml(String(item.id))}">✅ Yes, schedule it</button>
                            <button class="mw-btn mw-btn-secondary confirm-no-btn" data-id="${escHtml(String(item.id))}">❌ No, keep watching</button>
                        </div>
                        <div class="mw-error confirm-err" style="display:none;margin-top:8px;"></div>
                    </div>
                </div>
            `;

            box.querySelector('.confirm-yes-btn').addEventListener('click', async (e) => {
                const btn = e.currentTarget;
                btn.disabled = true;
                btn.textContent = 'Confirming…';
                try {
                    await apiPost('/items/' + item.id + '/confirm');
                    box.remove();
                    onAction();
                } catch (err) {
                    box.querySelector('.confirm-err').textContent = err.message;
                    box.querySelector('.confirm-err').style.display = 'block';
                    btn.disabled = false;
                    btn.textContent = '✅ Yes, schedule it';
                }
            });

            box.querySelector('.confirm-no-btn').addEventListener('click', async (e) => {
                const btn = e.currentTarget;
                btn.disabled = true;
                btn.textContent = 'Rejecting…';
                try {
                    await apiPost('/items/' + item.id + '/reject');
                    box.remove();
                    onAction();
                } catch (err) {
                    box.querySelector('.confirm-err').textContent = err.message;
                    box.querySelector('.confirm-err').style.display = 'block';
                    btn.disabled = false;
                    btn.textContent = '❌ No, keep watching';
                }
            });

            return box;
        }

        function renderConflictCard(item, onAction) {
            const box = document.createElement('div');
            box.className = 'mw-conflict-box';

            let conflictData = { conflicts: [] };
            try {
                conflictData = JSON.parse(item.conflictInfo);
            } catch (e) {
                // leave empty
            }

            const conflicts = Array.isArray(conflictData.conflicts) ? conflictData.conflicts : [];

            const optionsHtml = conflicts.map(c => {
                const startTime = c.startTime ? formatDateTime(c.startTime) : '';
                const hdBadge = c.isHd ? '<span class="mw-hd-badge">HD</span>' : '';
                return `
                    <div class="mw-conflict-option">
                        <div class="mw-conflict-option-info">
                            <strong>${escHtml(c.title || 'Unknown')} ${hdBadge}</strong>
                            ${c.channel ? escHtml(c.channel) : ''}${startTime ? ' · ' + escHtml(startTime) : ''}
                        </div>
                        <button class="mw-btn mw-btn-primary resolve-btn" data-program-id="${escHtml(String(c.programId || ''))}">
                            Record this one
                        </button>
                    </div>
                `;
            }).join('');

            const poster = posterUrl(item.posterPath);

            box.innerHTML = `
                <div class="mw-confirm-movie-row" style="margin-bottom:12px;">
                    <div class="mw-confirm-poster">
                        ${poster
                            ? '<img src="' + escHtml(poster) + '" alt="' + escHtml(item.title || '') + '">'
                            : '<div class="mw-confirm-poster-placeholder">' + escHtml(item.title || '') + '</div>'}
                    </div>
                    <div class="mw-confirm-info">
                        <h3>${escHtml(item.title || 'Untitled')}${item.year ? ' <span style="color:#888;font-weight:400;">(' + escHtml(String(item.year)) + ')</span>' : ''}</h3>
                        <p>Multiple broadcasts found. Choose which one to record:</p>
                    </div>
                </div>
                ${optionsHtml || '<p style="color:#888;">No conflict details available.</p>'}
                <div class="mw-error conflict-err" style="display:none;margin-top:8px;"></div>
            `;

            box.querySelectorAll('.resolve-btn').forEach(btn => {
                btn.addEventListener('click', async (e) => {
                    const programId = e.currentTarget.dataset.programId;
                    e.currentTarget.disabled = true;
                    e.currentTarget.textContent = 'Scheduling…';
                    try {
                        await apiPost('/items/' + item.id + '/resolve', { chosenProgramId: programId });
                        box.remove();
                        onAction();
                    } catch (err) {
                        box.querySelector('.conflict-err').textContent = err.message;
                        box.querySelector('.conflict-err').style.display = 'block';
                        e.currentTarget.disabled = false;
                        e.currentTarget.textContent = 'Record this one';
                    }
                });
            });

            return box;
        }

        await loadAndRender();
    }

    // -------------------------------------------------------------------------
    // Private: Admin check
    // -------------------------------------------------------------------------

    async function isCurrentUserAdmin() {
        try {
            const user = await Dashboard.getCurrentUser();
            return user && user.Policy && user.Policy.IsAdministrator === true;
        } catch (e) {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Page: Settings
    // -------------------------------------------------------------------------

    async function initSettings() {
        const area = document.getElementById('mw-settings-area');
        if (!area) return;

        const admin = await isCurrentUserAdmin();
        if (!admin) {
            area.innerHTML = '<div class="mw-admin-only">⚙️ Settings are only available to administrators.</div>';
            return;
        }

        area.innerHTML = '<div class="mw-loading">Loading settings…</div>';
        hideError('mw-error-box');

        let settings = {};
        try {
            settings = await apiGet('/settings');
        } catch (err) {
            area.innerHTML = '';
            showError('mw-error-box', 'Failed to load settings: ' + err.message);
            return;
        }

        area.innerHTML = `
            <form class="mw-settings-form" id="mw-settings-form" autocomplete="off">
                <div class="mw-form-group">
                    <label for="mw-tmdb-key">TMDB API Key</label>
                    <input type="password" id="mw-tmdb-key" placeholder="Enter your TMDB API key" value="${escHtml(settings.tmdbApiKey || '')}">
                    <div class="mw-hint">Get a free key at <a href="https://www.themoviedb.org/settings/api" target="_blank" rel="noopener" style="color:#e50914;">themoviedb.org</a></div>
                </div>

                <div class="mw-form-group">
                    <label for="mw-epg-interval">EPG Scan Interval</label>
                    <select id="mw-epg-interval">
                        <option value="1"  ${settings.scanIntervalHours === 1  ? 'selected' : ''}>Every 1 hour</option>
                        <option value="3"  ${settings.scanIntervalHours === 3  ? 'selected' : ''}>Every 3 hours</option>
                        <option value="6"  ${settings.scanIntervalHours === 6  ? 'selected' : ''}>Every 6 hours</option>
                        <option value="12" ${settings.scanIntervalHours === 12 ? 'selected' : ''}>Every 12 hours</option>
                        <option value="24" ${settings.scanIntervalHours === 24 ? 'selected' : ''}>Every 24 hours</option>
                    </select>
                </div>

                <div class="mw-form-group">
                    <label for="mw-days-ahead">Days Ahead to Scan</label>
                    <input type="number" id="mw-days-ahead" min="1" max="14" value="${escHtml(String(settings.daysAheadToScan || 7))}">
                </div>

                <div class="mw-form-group">
                    <label class="mw-checkbox-label">
                        <input type="checkbox" id="mw-remove-after-record" ${settings.removeAfterRecorded ? 'checked' : ''}>
                        Remove from Watchlist After Recorded
                    </label>
                </div>

                <div class="mw-form-group">
                    <label class="mw-checkbox-label">
                        <input type="checkbox" id="mw-skip-in-library" ${settings.skipIfInLibrary ? 'checked' : ''}>
                        Skip If Film Already in Library
                    </label>
                </div>

                <div class="mw-form-group">
                    <label class="mw-checkbox-label">
                        <input type="checkbox" id="mw-enable-notifications" ${settings.enableNotifications ? 'checked' : ''}>
                        Enable Notifications
                    </label>
                </div>

                <div class="mw-settings-actions">
                    <button type="submit" class="mw-btn mw-btn-primary" id="mw-save-btn">💾 Save Settings</button>
                    <button type="button" class="mw-btn mw-btn-secondary" id="mw-scan-btn">🔍 Scan Now</button>
                </div>
                <div id="mw-settings-feedback" class="mw-feedback" style="display:none;"></div>
                <div class="mw-error" id="mw-settings-error" style="display:none;"></div>
            </form>
        `;

        const form = document.getElementById('mw-settings-form');
        const feedback = document.getElementById('mw-settings-feedback');
        const settingsError = document.getElementById('mw-settings-error');

        function showFeedback(msg, isError) {
            if (isError) {
                feedback.style.display = 'none';
                settingsError.textContent = msg;
                settingsError.style.display = 'block';
            } else {
                settingsError.style.display = 'none';
                feedback.textContent = msg;
                feedback.style.display = 'block';
                setTimeout(() => { feedback.style.display = 'none'; }, 3000);
            }
        }

        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const saveBtn = document.getElementById('mw-save-btn');
            saveBtn.disabled = true;
            saveBtn.textContent = 'Saving…';

            const payload = {
                tmdbApiKey: document.getElementById('mw-tmdb-key').value,
                scanIntervalHours: parseInt(document.getElementById('mw-epg-interval').value, 10),
                daysAheadToScan: parseInt(document.getElementById('mw-days-ahead').value, 10),
                removeAfterRecorded: document.getElementById('mw-remove-after-record').checked,
                skipIfInLibrary: document.getElementById('mw-skip-in-library').checked,
                enableNotifications: document.getElementById('mw-enable-notifications').checked
            };

            try {
                await apiPut('/settings', payload);
                showFeedback('✅ Settings saved successfully.', false);
            } catch (err) {
                showFeedback('Failed to save: ' + err.message, true);
            } finally {
                saveBtn.disabled = false;
                saveBtn.textContent = '💾 Save Settings';
            }
        });

        document.getElementById('mw-scan-btn').addEventListener('click', async (e) => {
            const btn = e.currentTarget;
            btn.disabled = true;
            btn.textContent = 'Scanning…';
            try {
                await apiPost('/scan');
                showFeedback('✅ Scan triggered successfully.', false);
            } catch (err) {
                showFeedback('Scan failed: ' + err.message, true);
            } finally {
                btn.disabled = false;
                btn.textContent = '🔍 Scan Now';
            }
        });
    }

    // -------------------------------------------------------------------------
    // Page: Activity Log
    // -------------------------------------------------------------------------

    async function initActivity() {
        const area = document.getElementById('mw-activity-area');
        if (!area) return;

        const admin = await isCurrentUserAdmin();
        if (!admin) {
            area.innerHTML = '<div class="mw-admin-only">📜 The activity log is only available to administrators.</div>';
            return;
        }

        const EVENT_CLASSES = {
            'scan':     'mw-evt-scan',
            'match':    'mw-evt-match',
            'schedule': 'mw-evt-schedule',
            'record':   'mw-evt-record',
            'error':    'mw-evt-error',
            'skip':     'mw-evt-skip',
            'conflict': 'mw-evt-conflict',
            'confirm':  'mw-evt-confirm'
        };

        function eventClass(type) {
            if (!type) return 'mw-evt-default';
            return EVENT_CLASSES[type.toLowerCase()] || 'mw-evt-default';
        }

        async function loadAndRender() {
            hideError('mw-error-box');
            area.innerHTML = '<div class="mw-loading">Loading activity log…</div>';
            try {
                const entries = await apiGet('/activity');

                const refreshBtn = document.createElement('button');
                refreshBtn.className = 'mw-btn mw-btn-secondary';
                refreshBtn.style.marginBottom = '16px';
                refreshBtn.textContent = '🔄 Refresh';
                refreshBtn.addEventListener('click', loadAndRender);

                if (!entries || entries.length === 0) {
                    area.innerHTML = '';
                    area.appendChild(refreshBtn);
                    const empty = document.createElement('div');
                    empty.className = 'mw-empty';
                    empty.innerHTML = '<div style="font-size:2rem;margin-bottom:8px;">📜</div><strong>No activity yet.</strong><p>Activity will appear here as the plugin scans and schedules recordings.</p>';
                    area.appendChild(empty);
                    return;
                }

                area.innerHTML = '';
                area.appendChild(refreshBtn);

                const list = document.createElement('ul');
                list.className = 'mw-activity-list';

                entries.forEach(entry => {
                    const li = document.createElement('li');
                    li.className = 'mw-activity-item';
                    const evtClass = eventClass(entry.eventType);
                    li.innerHTML = `
                        <span class="mw-activity-time">${escHtml(formatDateTime(entry.timestamp))}</span>
                        <span class="mw-activity-type ${escHtml(evtClass)}">${escHtml(entry.eventType || 'Info')}</span>
                        <span class="mw-activity-msg">${escHtml(entry.message || '')}</span>
                    `;
                    list.appendChild(li);
                });

                area.appendChild(list);
            } catch (err) {
                area.innerHTML = '';
                showError('mw-error-box', err.message);

                const refreshBtn = document.createElement('button');
                refreshBtn.className = 'mw-btn mw-btn-secondary';
                refreshBtn.style.marginTop = '12px';
                refreshBtn.textContent = '🔄 Try Again';
                refreshBtn.addEventListener('click', loadAndRender);
                area.appendChild(refreshBtn);
            }
        }

        await loadAndRender();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    return {
        initDiscover,
        initWatchlist,
        initConfirmations,
        initSettings,
        initActivity
    };

})();

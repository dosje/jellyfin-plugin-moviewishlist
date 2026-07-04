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

    // Status enum values from plugin. The server serializes enums as strings
    // ("WatchingEpg", ...); numeric values are also handled as a fallback.
    // 0=WatchingEpg, 1=Scheduled, 2=Recorded, 3=AlreadyInLibrary, 4=Missed, 5=NeedsConfirmation
    const STATUS_NAMES = ['WatchingEpg', 'Scheduled', 'Recorded', 'AlreadyInLibrary', 'Missed', 'NeedsConfirmation'];

    const STATUS_LABELS = {
        WatchingEpg: 'Watching EPG',
        Scheduled: 'Scheduled',
        Recorded: 'Recorded',
        AlreadyInLibrary: 'In Library',
        Missed: 'Missed',
        NeedsConfirmation: 'Needs Confirmation'
    };

    const STATUS_CLASSES = {
        WatchingEpg: 'mw-badge-watchingepg',
        Scheduled: 'mw-badge-scheduled',
        Recorded: 'mw-badge-recorded',
        AlreadyInLibrary: 'mw-badge-alreadyinlibrary',
        Missed: 'mw-badge-missed',
        NeedsConfirmation: 'mw-badge-needsconfirmation'
    };

    function statusKey(status) {
        if (typeof status === 'number') return STATUS_NAMES[status];
        return status;
    }

    function statusLabel(status) {
        const key = statusKey(status);
        return STATUS_LABELS[key] !== undefined ? STATUS_LABELS[key] : 'Unknown';
    }

    function statusClass(status) {
        const key = statusKey(status);
        return STATUS_CLASSES[key] !== undefined ? STATUS_CLASSES[key] : 'mw-badge-watchingepg';
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
                && wishlistItems.some(i => String(i.TmdbId) === String(tmdbId));

            // TMDB DTOs are serialized with their snake_case JsonPropertyName values
            const backdrop = backdropUrl(movie.backdrop_path);
            const poster = posterUrl(movie.poster_path);

            const year = movie.release_date ? movie.release_date.substring(0, 4) : '';
            const runtime = movie.runtime ? movie.runtime + ' min' : '';
            const genres = Array.isArray(movie.genres)
                ? movie.genres.map(g => '<span class="mw-genre-tag">' + escHtml(g.name || g) + '</span>').join('')
                : '';
            const castList = movie.credits && Array.isArray(movie.credits.cast) ? movie.credits.cast : null;
            const cast = castList
                ? castList.slice(0, 5).map(c => '<span class="mw-cast-item">' + escHtml(c.name || c) + '</span>').join('')
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
                                ${movie.vote_average ? '<span>' + renderStars(movie.vote_average) + '</span>' : ''}
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
                            posterPath: movie.poster_path || null,
                            overview: movie.overview || null
                        });
                        addBtnEl.textContent = '✓ In Watchlist';
                        addBtnEl.className = 'mw-btn mw-btn-muted';
                        // Push into wishlist so re-open reflects state
                        if (Array.isArray(wishlistItems)) {
                            wishlistItems.push({ TmdbId: String(tmdbId) });
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
        const year = movie.release_date ? movie.release_date.substring(0, 4) : '';
        const inWatchlist = Array.isArray(wishlistItems)
            && wishlistItems.some(i => String(i.TmdbId) === String(movie.id));

        const wrapper = document.createElement('div');
        wrapper.className = 'mw-card-wrapper';

        wrapper.innerHTML = `
            <div class="mw-card" data-tmdb-id="${escHtml(String(movie.id))}">
                ${posterImgOrPlaceholder(movie.poster_path, movie.title)}
                <div class="mw-card-body">
                    <div class="mw-card-title" title="${escHtml(movie.title || '')}">${escHtml(movie.title || 'Untitled')}</div>
                    <div class="mw-card-meta">${escHtml(year)}${movie.vote_average ? ' · ' + Number(movie.vote_average).toFixed(1) + '★' : ''}</div>
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
        const year = item.Year ? String(item.Year) : '';
        const status = item.Status !== undefined ? item.Status : 'WatchingEpg';
        const badgeClass = statusClass(status);
        const badgeText = statusLabel(status);

        let scheduledHtml = '';
        if (statusKey(status) === 'Scheduled' && item.ScheduledChannel) {
            const dt = item.ScheduledAt ? formatDateTime(item.ScheduledAt) : '';
            const hd = item.IsHd ? '<span class="mw-hd-badge">HD</span>' : '';
            scheduledHtml = `<div class="mw-card-scheduled">📡 ${escHtml(item.ScheduledChannel)}${dt ? ' · ' + escHtml(dt) : ''} ${hd}</div>`;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'mw-card-wrapper';
        wrapper.dataset.itemId = item.Id;

        wrapper.innerHTML = `
            <div class="mw-card">
                ${posterImgOrPlaceholder(item.PosterPath, item.Title)}
                <div class="mw-card-body">
                    <div class="mw-card-title" title="${escHtml(item.Title || '')}">${escHtml(item.Title || 'Untitled')}</div>
                    <div class="mw-card-meta">${escHtml(year)}</div>
                    ${scheduledHtml}
                    <button class="mw-card-remove" data-id="${escHtml(String(item.Id))}">✕ Remove</button>
                </div>
            </div>
            <div class="mw-badge ${escHtml(badgeClass)}">${escHtml(badgeText)}</div>
        `;

        wrapper.querySelector('.mw-card-remove').addEventListener('click', (e) => {
            e.stopPropagation();
            if (confirm('Remove "' + (item.Title || 'this movie') + '" from your watchlist?')) {
                onRemove(item.Id, wrapper);
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
                // Response is a TmdbSearchResponse object: { "results": [...], "total_results": n }
                const data = await apiGet('/search?query=' + encodeURIComponent(query));
                const results = data && Array.isArray(data.results) ? data.results : [];
                if (results.length === 0) {
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
                const pending = (items || []).filter(i => statusKey(i.Status) === 'NeedsConfirmation');

                const uncertainMatches = pending.filter(i => i.PendingEpgProgramId && !i.ConflictInfo);
                const conflicts = pending.filter(i => i.ConflictInfo);

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

            const poster = posterUrl(item.PosterPath);
            const pendingTitle = item.PendingEpgTitle || 'Unknown Title';
            const pendingYear = item.PendingEpgYear ? ' (' + item.PendingEpgYear + ')' : '';

            box.innerHTML = `
                <div class="mw-confirm-movie-row">
                    <div class="mw-confirm-poster">
                        ${poster
                            ? '<img src="' + escHtml(poster) + '" alt="' + escHtml(item.Title || '') + '">'
                            : '<div class="mw-confirm-poster-placeholder">' + escHtml(item.Title || '') + '</div>'}
                    </div>
                    <div class="mw-confirm-info">
                        <h3>${escHtml(item.Title || 'Untitled')}${item.Year ? ' <span style="color:#888;font-weight:400;">(' + escHtml(String(item.Year)) + ')</span>' : ''}</h3>
                        <p>EPG found: <strong>${escHtml(pendingTitle + pendingYear)}</strong></p>
                        <p class="mw-confirm-question">Is this the right film?</p>
                        <div class="mw-confirm-actions">
                            <button class="mw-btn mw-btn-success confirm-yes-btn" data-id="${escHtml(String(item.Id))}">✅ Yes, schedule it</button>
                            <button class="mw-btn mw-btn-secondary confirm-no-btn" data-id="${escHtml(String(item.Id))}">❌ No, keep watching</button>
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
                    await apiPost('/items/' + item.Id + '/confirm');
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
                    await apiPost('/items/' + item.Id + '/reject');
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
                conflictData = JSON.parse(item.ConflictInfo);
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

            const poster = posterUrl(item.PosterPath);

            box.innerHTML = `
                <div class="mw-confirm-movie-row" style="margin-bottom:12px;">
                    <div class="mw-confirm-poster">
                        ${poster
                            ? '<img src="' + escHtml(poster) + '" alt="' + escHtml(item.Title || '') + '">'
                            : '<div class="mw-confirm-poster-placeholder">' + escHtml(item.Title || '') + '</div>'}
                    </div>
                    <div class="mw-confirm-info">
                        <h3>${escHtml(item.Title || 'Untitled')}${item.Year ? ' <span style="color:#888;font-weight:400;">(' + escHtml(String(item.Year)) + ')</span>' : ''}</h3>
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
                        await apiPost('/items/' + item.Id + '/resolve', { chosenProgramId: programId });
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
                    <input type="password" id="mw-tmdb-key" placeholder="Enter your TMDB API key" value="${escHtml(settings.TmdbApiKey || '')}">
                    <div class="mw-hint">Get a free key at <a href="https://www.themoviedb.org/settings/api" target="_blank" rel="noopener" style="color:#e50914;">themoviedb.org</a></div>
                </div>

                <div class="mw-form-group">
                    <label for="mw-epg-interval">EPG Scan Interval</label>
                    <select id="mw-epg-interval">
                        <option value="1"  ${settings.ScanIntervalHours === 1  ? 'selected' : ''}>Every 1 hour</option>
                        <option value="3"  ${settings.ScanIntervalHours === 3  ? 'selected' : ''}>Every 3 hours</option>
                        <option value="6"  ${settings.ScanIntervalHours === 6  ? 'selected' : ''}>Every 6 hours</option>
                        <option value="12" ${settings.ScanIntervalHours === 12 ? 'selected' : ''}>Every 12 hours</option>
                        <option value="24" ${settings.ScanIntervalHours === 24 ? 'selected' : ''}>Every 24 hours</option>
                    </select>
                </div>

                <div class="mw-form-group">
                    <label for="mw-days-ahead">Days Ahead to Scan</label>
                    <input type="number" id="mw-days-ahead" min="1" max="14" value="${escHtml(String(settings.DaysAheadToScan || 7))}">
                </div>

                <div class="mw-form-group">
                    <label class="mw-checkbox-label">
                        <input type="checkbox" id="mw-remove-after-record" ${settings.RemoveAfterRecorded ? 'checked' : ''}>
                        Remove from Watchlist After Recorded
                    </label>
                </div>

                <div class="mw-form-group">
                    <label class="mw-checkbox-label">
                        <input type="checkbox" id="mw-skip-in-library" ${settings.SkipIfInLibrary ? 'checked' : ''}>
                        Skip If Film Already in Library
                    </label>
                </div>

                <div class="mw-form-group">
                    <label class="mw-checkbox-label">
                        <input type="checkbox" id="mw-enable-notifications" ${settings.EnableNotifications ? 'checked' : ''}>
                        Enable Notifications
                    </label>
                </div>

                <div class="mw-form-group">
                    <label for="mw-sub-days-ahead">Channel Reminder Look-ahead (days)</label>
                    <input type="number" id="mw-sub-days-ahead" min="1" max="7" value="${escHtml(String(settings.SubscriptionDaysAhead ?? 1))}">
                    <div class="mw-hint">How many days ahead the channel reminder task scans for upcoming shows.</div>
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
                enableNotifications: document.getElementById('mw-enable-notifications').checked,
                subscriptionDaysAhead: parseInt(document.getElementById('mw-sub-days-ahead').value, 10)
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
                    const evtClass = eventClass(entry.EventType);
                    li.innerHTML = `
                        <span class="mw-activity-time">${escHtml(formatDateTime(entry.Timestamp))}</span>
                        <span class="mw-activity-type ${escHtml(evtClass)}">${escHtml(entry.EventType || 'Info')}</span>
                        <span class="mw-activity-msg">${escHtml(entry.Message || '')}</span>
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
    // Page: Subscriptions
    // -------------------------------------------------------------------------

    const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

    function dayBit(index) { return 1 << index; }

    function daysLabel(mask) {
        if (mask === 127) return 'Every day';
        if (mask === 0) return 'None';
        return DAY_NAMES.filter((_, i) => (mask & dayBit(i)) !== 0).join(', ');
    }

    function renderDayCheckboxes(id, currentMask) {
        return DAY_NAMES.map((day, i) => {
            const checked = (currentMask & dayBit(i)) !== 0 ? 'checked' : '';
            return `<label class="mw-day-label"><input type="checkbox" class="mw-day-cb" data-bit="${dayBit(i)}" ${checked}> ${day}</label>`;
        }).join('');
    }

    function readDayMask(container) {
        let mask = 0;
        container.querySelectorAll('.mw-day-cb').forEach(cb => {
            if (cb.checked) mask |= parseInt(cb.dataset.bit, 10);
        });
        return mask;
    }

    async function initSubscriptions() {
        const area = document.getElementById('mw-subscriptions-area');
        if (!area) return;

        area.innerHTML = '<div class="mw-loading">Loading…</div>';
        hideError('mw-error-box');

        let channels = [];
        let subscriptions = [];

        try {
            [channels, subscriptions] = await Promise.all([
                apiGet('/channels'),
                apiGet('/subscriptions')
            ]);
        } catch (err) {
            area.innerHTML = '';
            showError('mw-error-box', err.message);
            return;
        }

        renderSubscriptionsPage(channels, subscriptions);

        function renderSubscriptionsPage(chList, subList) {
            area.innerHTML = '';

            // ---- Add subscription form ----
            const addSection = document.createElement('div');
            addSection.className = 'mw-settings-form';
            addSection.style.marginBottom = '28px';

            const channelOptions = chList.length === 0
                ? '<option value="">No Live TV channels found</option>'
                : '<option value="">— select a channel —</option>' +
                  chList.map(c => `<option value="${escHtml(c.id)}" data-name="${escHtml(c.name)}">${escHtml(c.name)}</option>`).join('');

            addSection.innerHTML = `
                <h3 style="margin-top:0;">Add Channel Subscription</h3>
                <div class="mw-form-group">
                    <label for="mw-sub-channel">Channel</label>
                    <select id="mw-sub-channel">${channelOptions}</select>
                </div>
                <div class="mw-form-group">
                    <label>Notify on days</label>
                    <div id="mw-sub-days" class="mw-days-row">${renderDayCheckboxes('add', 127)}</div>
                </div>
                <div class="mw-settings-actions">
                    <button class="mw-btn mw-btn-primary" id="mw-sub-add-btn">+ Subscribe</button>
                </div>
                <div class="mw-error" id="mw-sub-add-error" style="display:none;margin-top:8px;"></div>
            `;
            area.appendChild(addSection);

            // ---- Existing subscriptions list ----
            const listSection = document.createElement('div');
            listSection.id = 'mw-sub-list';
            area.appendChild(listSection);
            renderSubList(listSection, subList);

            // Wire add button
            document.getElementById('mw-sub-add-btn').addEventListener('click', async () => {
                const sel = document.getElementById('mw-sub-channel');
                const channelId = sel.value;
                const channelName = sel.options[sel.selectedIndex]?.dataset?.name || channelId;
                const daysContainer = document.getElementById('mw-sub-days');
                const daysOfWeek = readDayMask(daysContainer);
                const errEl = document.getElementById('mw-sub-add-error');
                errEl.style.display = 'none';

                if (!channelId) {
                    errEl.textContent = 'Please select a channel.';
                    errEl.style.display = 'block';
                    return;
                }
                if (daysOfWeek === 0) {
                    errEl.textContent = 'Please select at least one day.';
                    errEl.style.display = 'block';
                    return;
                }

                const btn = document.getElementById('mw-sub-add-btn');
                btn.disabled = true;
                btn.textContent = 'Subscribing…';
                try {
                    await apiPost('/subscriptions', { channelId, channelName, daysOfWeek });
                    subscriptions = await apiGet('/subscriptions');
                    renderSubList(listSection, subscriptions);
                } catch (err) {
                    errEl.textContent = err.message;
                    errEl.style.display = 'block';
                } finally {
                    btn.disabled = false;
                    btn.textContent = '+ Subscribe';
                }
            });
        }

        function renderSubList(container, subList) {
            if (!subList || subList.length === 0) {
                container.innerHTML = `
                    <div class="mw-empty">
                        <div style="font-size:2rem;margin-bottom:8px;">📺</div>
                        <strong>No subscriptions yet.</strong>
                        <p>Subscribe to a channel above to receive reminders when shows air on your chosen days.</p>
                    </div>
                `;
                return;
            }

            container.innerHTML = '<h3>Your Subscriptions</h3>';

            const list = document.createElement('div');
            list.className = 'mw-sub-list';

            subList.forEach(sub => {
                const row = document.createElement('div');
                row.className = 'mw-sub-row';
                row.dataset.id = sub.Id;

                row.innerHTML = `
                    <div class="mw-sub-info">
                        <strong>${escHtml(sub.ChannelName || sub.ChannelId)}</strong>
                        <span class="mw-sub-days-label" id="mw-days-label-${escHtml(String(sub.Id))}">${escHtml(daysLabel(sub.DaysOfWeek))}</span>
                    </div>
                    <div class="mw-sub-day-edit" id="mw-day-edit-${escHtml(String(sub.Id))}" style="display:none;">
                        <div class="mw-days-row">${renderDayCheckboxes('edit-' + sub.Id, sub.DaysOfWeek)}</div>
                        <button class="mw-btn mw-btn-success mw-sub-save-btn" data-id="${escHtml(String(sub.Id))}">Save</button>
                        <button class="mw-btn mw-btn-secondary mw-sub-cancel-btn" data-id="${escHtml(String(sub.Id))}">Cancel</button>
                    </div>
                    <div class="mw-sub-actions">
                        <button class="mw-btn mw-btn-secondary mw-sub-edit-btn" data-id="${escHtml(String(sub.Id))}">Edit days</button>
                        <button class="mw-btn mw-btn-danger mw-sub-del-btn" data-id="${escHtml(String(sub.Id))}">Remove</button>
                    </div>
                    <div class="mw-error mw-sub-err" data-id="${escHtml(String(sub.Id))}" style="display:none;margin-top:4px;"></div>
                `;

                list.appendChild(row);
            });

            container.appendChild(list);

            // Wire edit / save / cancel / delete
            container.querySelectorAll('.mw-sub-edit-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const id = btn.dataset.id;
                    document.getElementById('mw-day-edit-' + id).style.display = '';
                    btn.style.display = 'none';
                });
            });

            container.querySelectorAll('.mw-sub-cancel-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const id = btn.dataset.id;
                    document.getElementById('mw-day-edit-' + id).style.display = 'none';
                    container.querySelector(`.mw-sub-edit-btn[data-id="${id}"]`).style.display = '';
                });
            });

            container.querySelectorAll('.mw-sub-save-btn').forEach(btn => {
                btn.addEventListener('click', async () => {
                    const id = btn.dataset.id;
                    const editDiv = document.getElementById('mw-day-edit-' + id);
                    const daysOfWeek = readDayMask(editDiv);
                    const errEl = container.querySelector(`.mw-sub-err[data-id="${id}"]`);
                    errEl.style.display = 'none';

                    if (daysOfWeek === 0) {
                        errEl.textContent = 'Select at least one day.';
                        errEl.style.display = 'block';
                        return;
                    }

                    btn.disabled = true;
                    try {
                        await apiPut('/subscriptions/' + id, { daysOfWeek });
                        document.getElementById('mw-days-label-' + id).textContent = daysLabel(daysOfWeek);
                        editDiv.style.display = 'none';
                        container.querySelector(`.mw-sub-edit-btn[data-id="${id}"]`).style.display = '';
                    } catch (err) {
                        errEl.textContent = err.message;
                        errEl.style.display = 'block';
                    } finally {
                        btn.disabled = false;
                    }
                });
            });

            container.querySelectorAll('.mw-sub-del-btn').forEach(btn => {
                btn.addEventListener('click', async () => {
                    const id = btn.dataset.id;
                    const row = container.querySelector(`.mw-sub-row[data-id="${id}"]`);
                    const channelName = row?.querySelector('strong')?.textContent || 'this channel';
                    if (!confirm('Remove subscription for "' + channelName + '"?')) return;

                    btn.disabled = true;
                    try {
                        await apiDelete('/subscriptions/' + id);
                        row?.remove();
                        subscriptions = subscriptions.filter(s => String(s.Id) !== String(id));
                        if (list.children.length === 0) {
                            renderSubList(container, []);
                        }
                    } catch (err) {
                        const errEl = container.querySelector(`.mw-sub-err[data-id="${id}"]`);
                        if (errEl) { errEl.textContent = err.message; errEl.style.display = 'block'; }
                        btn.disabled = false;
                    }
                });
            });
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    return {
        initDiscover,
        initWatchlist,
        initConfirmations,
        initSettings,
        initActivity,
        initSubscriptions
    };

})();

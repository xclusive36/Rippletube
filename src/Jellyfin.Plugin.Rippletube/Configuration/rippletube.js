(function () {
    const page = document.querySelector('#rippletubePage:not(.hide)') || document.querySelector('#rippletubePage');
    if (!page) {
        return;
    }

    if (page.dataset.rippletubeInitialized === 'true') {
        return;
    }

    page.dataset.rippletubeInitialized = 'true';
    let refreshTimer = null;

    const api = {
        getConfiguration: () => ApiClient.ajax({ type: 'GET', url: noCacheUrl('Rippletube/Configuration') }),
        saveConfiguration: (data) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Configuration'), data: JSON.stringify(data), contentType: 'application/json' }),
        validate: () => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Validate') }),
        preview: (url) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Preview'), data: JSON.stringify({ url }), contentType: 'application/json' }),
        submit: (data) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Jobs'), data: JSON.stringify(data), contentType: 'application/json' }),
        jobs: () => ApiClient.ajax({ type: 'GET', url: noCacheUrl('Rippletube/Jobs') }),
        cancel: (id) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(`Rippletube/Jobs/${id}/Cancel`) }),
        retry: (id) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(`Rippletube/Jobs/${id}/Retry`) })
    };

    function noCacheUrl(path) {
        const separator = path.indexOf('?') === -1 ? '?' : '&';
        return ApiClient.getUrl(`${path}${separator}_=${Date.now()}`);
    }

    function value(id) {
        return page.querySelector(`#${id}`).value;
    }

    function setValue(id, val) {
        page.querySelector(`#${id}`).value = val == null ? '' : val;
    }

    function checked(id) {
        return page.querySelector(`#${id}`).checked;
    }

    function setChecked(id, val) {
        page.querySelector(`#${id}`).checked = !!val;
    }

    function message(text) {
        page.querySelector('#messagePanel').textContent = text || '';
    }

    function clearPreview() {
        page.querySelector('#previewPanel').innerHTML = '';
    }

    function asObject(response) {
        if (typeof response !== 'string') {
            return response || {};
        }

        try {
            return JSON.parse(response);
        } catch {
            return {};
        }
    }

    function errorMessage(error) {
        if (!error) {
            return 'Unknown error.';
        }

        if (typeof error === 'string') {
            return error;
        }

        const responseText = error.responseText || error.responseJSON?.error || error.responseJSON?.Error;
        if (responseText) {
            try {
                const parsed = JSON.parse(responseText);
                return parsed.error || parsed.Error || parsed.title || responseText;
            } catch {
                return responseText;
            }
        }

        return error.message || error.statusText || 'Request failed.';
    }

    function formData() {
        return {
            ytDlpPath: value('ytDlpPath'),
            ffmpegPath: value('ffmpegPath'),
            destinationFolder: value('destinationFolder'),
            cookiesFilePath: value('cookiesFilePath'),
            formatPreset: parseInt(value('formatPreset') || '0', 10),
            namingTemplate: parseInt(value('namingTemplate') || '0', 10),
            maxPlaylistItems: parseInt(value('maxPlaylistItems') || '25', 10),
            minimumFreeSpaceGb: parseInt(value('minimumFreeSpaceGb') || '5', 10),
            historyRetention: parseInt(value('historyRetention') || '100', 10),
            autoScanLibrary: checked('autoScanLibrary')
        };
    }

    function renderPreview(preview) {
        preview = asObject(preview);
        const panel = page.querySelector('#previewPanel');
        const title = preview.title || preview.Title || 'Untitled';
        const uploader = preview.uploader || preview.Uploader || '';
        const duration = preview.duration || preview.Duration || '';
        const playlistCount = preview.playlistCount || preview.PlaylistCount || '';
        const thumbnailUrl = preview.thumbnailUrl || preview.ThumbnailUrl || '';
        const thumb = thumbnailUrl ? `<img src="${escapeHtml(thumbnailUrl)}" alt="">` : '';
        panel.innerHTML = `<div class="rippletube-preview">${thumb}<div>
            <h3>${escapeHtml(title)}</h3>
            <p>${escapeHtml(uploader)}</p>
            <p>${escapeHtml(duration)}${playlistCount ? ` · ${playlistCount} items` : ''}</p>
        </div></div>`;
    }

    function renderQueue(snapshot) {
        snapshot = asObject(snapshot);
        const jobs = snapshot.jobs || snapshot.Jobs || [];
        const panel = page.querySelector('#queuePanel');
        if (!jobs.length) {
            panel.innerHTML = '<div class="fieldDescription">No jobs have been submitted yet.</div>';
            return;
        }

        panel.innerHTML = jobs.map(job => {
            const id = job.id || job.Id;
            const status = normalizeStatus(job.status ?? job.Status);
            const progress = job.progressPercent || job.ProgressPercent || 0;
            const progressText = job.progressText || job.ProgressText || '';
            const url = job.url || job.Url || '';
            const error = job.errorSummary || job.ErrorSummary || '';
            const log = job.logTail || job.LogTail || '';
            const createdAt = job.createdAt || job.CreatedAt || '';
            const startedAt = job.startedAt || job.StartedAt || '';
            const finishedAt = job.finishedAt || job.FinishedAt || '';
            const canCancel = status === 'Pending' || status === 'Running';
            return `<div class="rippletube-job">
                <div class="rippletube-job-header">
                    <strong>${escapeHtml(status)}</strong>
                    <span>${progress}%</span>
                </div>
                <div class="rippletube-progress"><span style="width:${Math.max(0, Math.min(100, progress))}%"></span></div>
                ${progressText ? `<div class="fieldDescription">${escapeHtml(progressText)}</div>` : ''}
                <div class="fieldDescription">${escapeHtml(url)}</div>
                <div class="fieldDescription">${escapeHtml(formatJobTimes(createdAt, startedAt, finishedAt))}</div>
                ${error ? `<div class="fieldDescription">${escapeHtml(error)}</div>` : ''}
                ${log ? `<pre class="rippletube-log">${escapeHtml(log)}</pre>` : ''}
                <div class="rippletube-actions">
                    ${canCancel ? `<button is="emby-button" type="button" data-cancel="${id}" class="raised"><span>Cancel</span></button>` : ''}
                    <button is="emby-button" type="button" data-retry="${id}" class="raised"><span>Retry</span></button>
                </div>
            </div>`;
        }).join('');
    }

    function prependJob(job) {
        job = asObject(job);
        if (!job.id && !job.Id) {
            return;
        }

        renderQueue({ jobs: [job] });
    }

    function escapeHtml(value) {
        return String(value || '').replace(/[&<>"']/g, char => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        }[char]));
    }

    async function load() {
        try {
            message('Loading Rippletube configuration...');
            const config = asObject(await api.getConfiguration());
            setValue('ytDlpPath', config.ytDlpPath || config.YtDlpPath || 'yt-dlp');
            setValue('ffmpegPath', config.ffmpegPath || config.FfmpegPath || 'ffmpeg');
            setValue('destinationFolder', config.destinationFolder || config.DestinationFolder || '');
            setValue('cookiesFilePath', config.cookiesFilePath || config.CookiesFilePath || '');
            setValue('formatPreset', normalizeEnum(config.formatPreset ?? config.FormatPreset, { CompatibleMp4: 0, BestAvailable: 1, AudioOnly: 2, Capped1080p: 3 }));
            setValue('namingTemplate', normalizeEnum(config.namingTemplate ?? config.NamingTemplate, { UploaderTitleWithId: 0, PlaylistIndexTitleWithId: 1, FlatTitleWithId: 2 }));
            setValue('maxPlaylistItems', config.maxPlaylistItems || config.MaxPlaylistItems || 25);
            setValue('minimumFreeSpaceGb', config.minimumFreeSpaceGb || config.MinimumFreeSpaceGb || 5);
            setValue('historyRetention', config.historyRetention || config.HistoryRetention || 100);
            setChecked('autoScanLibrary', config.autoScanLibrary ?? config.AutoScanLibrary ?? true);
            await refreshQueue();
            message('');
        } catch (error) {
            message(`Unable to load configuration: ${errorMessage(error)}`);
        }
    }

    async function refreshQueue() {
        try {
            renderQueue(await api.jobs());
        } catch (error) {
            message(`Unable to refresh queue: ${errorMessage(error)}`);
        }
    }

    page.querySelector('#rippletubeConfigForm').addEventListener('submit', async (event) => {
        event.preventDefault();
        try {
            message('Saving configuration...');
            await api.saveConfiguration(formData());
            message('Configuration saved.');
        } catch (error) {
            message(`Save failed: ${errorMessage(error)}`);
        }
    });

    page.querySelector('#validateButton').addEventListener('click', async () => {
        try {
            message('Validating yt-dlp and ffmpeg...');
            const result = asObject(await api.validate());
            const errors = result.errors || result.Errors || [];
            const warnings = result.warnings || result.Warnings || [];
            message(errors.length ? errors.join(' ') : `Validation passed.${warnings.length ? ` ${warnings.join(' ')}` : ''}`);
        } catch (error) {
            message(`Validation failed: ${errorMessage(error)}`);
        }
    });

    page.querySelector('#previewButton').addEventListener('click', async () => {
        try {
            const url = value('downloadUrl').trim();
            if (!url) {
                message('Enter a video or playlist URL first.');
                return;
            }

            clearPreview();
            message('Saving configuration and previewing URL with yt-dlp...');
            await api.saveConfiguration(formData());
            const preview = await api.preview(url);
            renderPreview(preview);
            message('Preview loaded.');
        } catch (error) {
            clearPreview();
            message(`Preview failed: ${errorMessage(error)}`);
        }
    });

    page.querySelector('#submitButton').addEventListener('click', async () => {
        try {
            const url = value('downloadUrl').trim();
            if (!url) {
                message('Enter a video or playlist URL first.');
                return;
            }

            message('Saving configuration and submitting job...');
            await api.saveConfiguration(formData());
            const job = await api.submit({
                url,
                isPlaylist: checked('isPlaylist'),
                formatPreset: parseInt(value('formatPreset') || '0', 10),
                namingTemplate: parseInt(value('namingTemplate') || '0', 10)
            });
            message('Job submitted. The queue should switch to Running within a few seconds.');
            prependJob(job);
        } catch (error) {
            message(`Submit failed: ${errorMessage(error)}`);
        }
    });

    page.querySelector('#refreshQueueButton').addEventListener('click', async () => {
        message('Refreshing queue...');
        await refreshQueue();
        message('Queue refreshed.');
    });

    page.querySelector('#queuePanel').addEventListener('click', async (event) => {
        const cancelId = event.target.closest('[data-cancel]')?.getAttribute('data-cancel');
        const retryId = event.target.closest('[data-retry]')?.getAttribute('data-retry');
        if (cancelId) {
            try {
                message('Canceling job...');
                await api.cancel(cancelId);
                await refreshQueue();
                message('Job canceled.');
            } catch (error) {
                message(`Cancel failed: ${errorMessage(error)}`);
            }
        }
        if (retryId) {
            try {
                message('Retrying job...');
                await api.retry(retryId);
                await refreshQueue();
                message('Job queued for retry.');
            } catch (error) {
                message(`Retry failed: ${errorMessage(error)}`);
            }
        }
    });

    page.addEventListener('viewshow', () => {
        load();
        startRefreshTimer();
    });

    page.addEventListener('viewhide', stopRefreshTimer);

    load();
    startRefreshTimer();

    function normalizeEnum(value, map) {
        if (typeof value === 'number') {
            return value;
        }

        return map[value] ?? 0;
    }

    function normalizeStatus(status) {
        const map = {
            0: 'Pending',
            1: 'Previewed',
            2: 'Running',
            3: 'Completed',
            4: 'Failed',
            5: 'Canceled',
            6: 'Duplicate skipped'
        };

        return map[status] || status || 'Unknown';
    }

    function formatJobTimes(createdAt, startedAt, finishedAt) {
        const parts = [];
        if (createdAt) {
            parts.push(`Created ${formatDate(createdAt)}`);
        }
        if (startedAt) {
            parts.push(`Started ${formatDate(startedAt)}`);
        }
        if (finishedAt) {
            parts.push(`Finished ${formatDate(finishedAt)}`);
        }

        return parts.join(' · ');
    }

    function formatDate(value) {
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
    }

    function startRefreshTimer() {
        stopRefreshTimer();
        refreshTimer = window.setInterval(refreshQueue, 5000);
    }

    function stopRefreshTimer() {
        if (refreshTimer) {
            window.clearInterval(refreshTimer);
            refreshTimer = null;
        }
    }
}());

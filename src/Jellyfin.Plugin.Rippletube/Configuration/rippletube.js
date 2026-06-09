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
        getConfiguration: () => ajaxJson({ type: 'GET', url: noCacheUrl('Rippletube/Configuration') }),
        saveConfiguration: (data) => ajaxJson({ type: 'POST', url: ApiClient.getUrl('Rippletube/Configuration'), data: JSON.stringify(data) }),
        validate: () => ajaxJson({ type: 'POST', url: ApiClient.getUrl('Rippletube/Validate') }),
        preview: (url) => ajaxJson({ type: 'POST', url: ApiClient.getUrl('Rippletube/Preview'), data: JSON.stringify({ url }) }),
        submit: (data) => ajaxJson({ type: 'POST', url: ApiClient.getUrl('Rippletube/Jobs'), data: JSON.stringify(data) }),
        jobs: () => ajaxJson({ type: 'GET', url: noCacheUrl('Rippletube/Jobs') }),
        cancel: (id) => ajaxJson({ type: 'POST', url: ApiClient.getUrl(`Rippletube/Jobs/${id}/Cancel`) }),
        retry: (id) => ajaxJson({ type: 'POST', url: ApiClient.getUrl(`Rippletube/Jobs/${id}/Retry`) })
    };

    async function ajaxJson(options) {
        const response = await ApiClient.ajax(Object.assign({
            contentType: 'application/json',
            dataType: 'json',
            headers: {
                Accept: 'application/json'
            }
        }, options));

        return asObject(response);
    }

    function noCacheUrl(path) {
        const url = ApiClient.getUrl(path);
        const separator = url.indexOf('?') === -1 ? '?' : '&';
        return `${url}${separator}_=${Date.now()}`;
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

    function message(text, kind) {
        const panel = page.querySelector('#messagePanel');
        panel.textContent = text || '';
        panel.classList.toggle('rippletube-message-error', kind === 'error');
        panel.classList.toggle('rippletube-message-success', kind === 'success');
    }

    function clearPreview() {
        page.querySelector('#previewPanel').innerHTML = '';
    }

    function renderQueueError(text) {
        page.querySelector('#queuePanel').innerHTML = `<div class="rippletube-error">${escapeHtml(text)}</div>`;
    }

    function asObject(response) {
        if (response?.responseJSON) {
            return response.responseJSON;
        }

        if (response?.responseText) {
            return parseJsonText(response.responseText);
        }

        if (response?.data) {
            return asObject(response.data);
        }

        if (typeof response !== 'string') {
            return response || {};
        }

        return parseJsonText(response);
    }

    function parseJsonText(text) {
        try {
            return JSON.parse(text);
        } catch {
            return {
                _rippletubeInvalidResponse: true,
                _rippletubeSnippet: truncate(String(text || ''), 500)
            };
        }
    }

    function describeResponse(response) {
        response = asObject(response);
        if (response._rippletubeInvalidResponse) {
            return response._rippletubeSnippet
                ? ` Response was not JSON: ${response._rippletubeSnippet}`
                : ' Response was empty or not JSON.';
        }

        const keys = Object.keys(response);
        return keys.length ? ` Response keys: ${keys.join(', ')}.` : ' Response was empty or not JSON.';
    }

    function truncate(value, maxLength) {
        return value.length > maxLength ? `${value.slice(0, maxLength)}...` : value;
    }

    function errorMessage(error) {
        if (!error) {
            return 'Unknown error.';
        }

        if (typeof error === 'string') {
            return error;
        }

        const status = error.status ? `HTTP ${error.status}${error.statusText ? ` ${error.statusText}` : ''}` : '';
        const responseJsonText = describeErrorPayload(error.responseJSON);
        if (responseJsonText) {
            return status ? `${status}: ${responseJsonText}` : responseJsonText;
        }

        if (error.responseText) {
            try {
                const parsed = JSON.parse(error.responseText);
                const text = describeErrorPayload(parsed) || error.responseText;
                return status ? `${status}: ${text}` : text;
            } catch {
                return status ? `${status}: ${error.responseText}` : error.responseText;
            }
        }

        const text = error.message || error.statusText || 'Request failed.';
        return status ? `${status}: ${text}` : text;
    }

    function describeErrorPayload(payload) {
        if (!payload) {
            return '';
        }

        const errors = payload.errors || payload.Errors;
        if (Array.isArray(errors) && errors.length) {
            return errors.join(' ');
        }

        if (errors && typeof errors === 'object') {
            const messages = Object.values(errors).flat().filter(Boolean);
            if (messages.length) {
                return messages.join(' ');
            }
        }

        return payload.error || payload.Error || payload.title || payload.message || payload.Message || '';
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
        const panel = page.querySelector('#queuePanel');
        const jobs = snapshot.jobs || snapshot.Jobs;
        if (!Array.isArray(jobs)) {
            panel.innerHTML = `<div class="rippletube-error">Queue response did not include a jobs list.${escapeHtml(describeResponse(snapshot))}</div>`;
            return false;
        }

        if (!jobs.length) {
            panel.innerHTML = '<div class="fieldDescription">No jobs have been submitted yet.</div>';
            return true;
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
        return true;
    }

    function prependJob(job) {
        job = asObject(job);
        if (!job.id && !job.Id) {
            throw new Error(`Submit response did not include a job id.${describeResponse(job)}`);
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
            const queueLoaded = await refreshQueue({ silent: true });
            if (queueLoaded) {
                message('');
            }
        } catch (error) {
            message(`Unable to load configuration: ${errorMessage(error)}`, 'error');
        }
    }

    async function refreshQueue(options) {
        try {
            const rendered = renderQueue(await api.jobs());
            if (!rendered) {
                message('Unable to refresh queue: invalid response from Rippletube API.', 'error');
                return false;
            }

            if (!options?.silent) {
                message('Queue refreshed.', 'success');
            }

            return true;
        } catch (error) {
            const text = `Unable to refresh queue: ${errorMessage(error)}`;
            renderQueueError(text);
            message(text, 'error');
            return false;
        }
    }

    page.querySelector('#rippletubeConfigForm').addEventListener('submit', async (event) => {
        event.preventDefault();
        try {
            message('Saving configuration...');
            await api.saveConfiguration(formData());
            message('Configuration saved.', 'success');
        } catch (error) {
            message(`Save failed: ${errorMessage(error)}`, 'error');
        }
    });

    page.querySelector('#validateButton').addEventListener('click', async () => {
        try {
            message('Validating yt-dlp and ffmpeg...');
            const result = asObject(await api.validate());
            const errors = result.errors || result.Errors || [];
            const warnings = result.warnings || result.Warnings || [];
            message(errors.length ? errors.join(' ') : `Validation passed.${warnings.length ? ` ${warnings.join(' ')}` : ''}`, errors.length ? 'error' : 'success');
        } catch (error) {
            message(`Validation failed: ${errorMessage(error)}`, 'error');
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
            message('Saving configuration...');
            try {
                await api.saveConfiguration(formData());
            } catch (error) {
                message(`Save failed before preview: ${errorMessage(error)}`, 'error');
                return;
            }

            message('Previewing URL with yt-dlp...');
            const preview = await api.preview(url);
            renderPreview(preview);
            message('Preview loaded.', 'success');
        } catch (error) {
            clearPreview();
            message(`Preview failed: ${errorMessage(error)}`, 'error');
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
            try {
                await api.saveConfiguration(formData());
            } catch (error) {
                message(`Save failed before submit: ${errorMessage(error)}`, 'error');
                return;
            }

            message('Submitting job...');
            const job = await api.submit({
                url,
                isPlaylist: checked('isPlaylist'),
                formatPreset: parseInt(value('formatPreset') || '0', 10),
                namingTemplate: parseInt(value('namingTemplate') || '0', 10)
            });
            message('Job submitted. The queue should switch to Running within a few seconds.', 'success');
            prependJob(job);
        } catch (error) {
            message(`Submit failed: ${errorMessage(error)}`, 'error');
        }
    });

    page.querySelector('#refreshQueueButton').addEventListener('click', async () => {
        message('Refreshing queue...');
        await refreshQueue();
    });

    page.querySelector('#queuePanel').addEventListener('click', async (event) => {
        const cancelId = event.target.closest('[data-cancel]')?.getAttribute('data-cancel');
        const retryId = event.target.closest('[data-retry]')?.getAttribute('data-retry');
        if (cancelId) {
            try {
                message('Canceling job...');
                await api.cancel(cancelId);
                await refreshQueue();
                message('Job canceled.', 'success');
            } catch (error) {
                message(`Cancel failed: ${errorMessage(error)}`, 'error');
            }
        }
        if (retryId) {
            try {
                message('Retrying job...');
                await api.retry(retryId);
                await refreshQueue();
                message('Job queued for retry.', 'success');
            } catch (error) {
                message(`Retry failed: ${errorMessage(error)}`, 'error');
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

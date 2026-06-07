(function () {
    const page = document.querySelector('#rippletubePage');
    if (!page) {
        return;
    }

    const api = {
        getConfiguration: () => ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('Rippletube/Configuration') }),
        saveConfiguration: (data) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Configuration'), data: JSON.stringify(data), contentType: 'application/json' }),
        validate: () => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Validate') }),
        preview: (url) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Preview'), data: JSON.stringify({ url }), contentType: 'application/json' }),
        submit: (data) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Rippletube/Jobs'), data: JSON.stringify(data), contentType: 'application/json' }),
        jobs: () => ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('Rippletube/Jobs') }),
        cancel: (id) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(`Rippletube/Jobs/${id}/Cancel`) }),
        retry: (id) => ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(`Rippletube/Jobs/${id}/Retry`) })
    };

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
        const panel = page.querySelector('#previewPanel');
        const thumb = preview.thumbnailUrl ? `<img src="${escapeHtml(preview.thumbnailUrl)}" alt="">` : '';
        panel.innerHTML = `<div class="rippletube-preview">${thumb}<div>
            <h3>${escapeHtml(preview.title || 'Untitled')}</h3>
            <p>${escapeHtml(preview.uploader || '')}</p>
            <p>${escapeHtml(preview.duration || '')}${preview.playlistCount ? ` · ${preview.playlistCount} items` : ''}</p>
        </div></div>`;
    }

    function renderQueue(snapshot) {
        const jobs = snapshot.jobs || snapshot.Jobs || [];
        page.querySelector('#queuePanel').innerHTML = jobs.map(job => {
            const id = job.id || job.Id;
            const status = job.status || job.Status;
            const progress = job.progressPercent || job.ProgressPercent || 0;
            const url = job.url || job.Url || '';
            const error = job.errorSummary || job.ErrorSummary || '';
            const log = job.logTail || job.LogTail || '';
            const canCancel = status === 'Pending' || status === 'Running' || status === 0 || status === 2;
            return `<div class="rippletube-job">
                <div class="rippletube-job-header">
                    <strong>${escapeHtml(status.toString())}</strong>
                    <span>${progress}%</span>
                </div>
                <div class="rippletube-progress"><span style="width:${Math.max(0, Math.min(100, progress))}%"></span></div>
                <div class="fieldDescription">${escapeHtml(url)}</div>
                ${error ? `<div class="fieldDescription">${escapeHtml(error)}</div>` : ''}
                ${log ? `<pre class="rippletube-log">${escapeHtml(log)}</pre>` : ''}
                <div class="rippletube-actions">
                    ${canCancel ? `<button is="emby-button" type="button" data-cancel="${id}" class="raised"><span>Cancel</span></button>` : ''}
                    <button is="emby-button" type="button" data-retry="${id}" class="raised"><span>Retry</span></button>
                </div>
            </div>`;
        }).join('');
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
        const config = await api.getConfiguration();
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
    }

    async function refreshQueue() {
        renderQueue(await api.jobs());
    }

    page.querySelector('#rippletubeConfigForm').addEventListener('submit', async (event) => {
        event.preventDefault();
        await api.saveConfiguration(formData());
        message('Configuration saved.');
    });

    page.querySelector('#validateButton').addEventListener('click', async () => {
        const result = await api.validate();
        const errors = result.errors || result.Errors || [];
        const warnings = result.warnings || result.Warnings || [];
        message(errors.length ? errors.join(' ') : `Validation passed.${warnings.length ? ` ${warnings.join(' ')}` : ''}`);
    });

    page.querySelector('#previewButton').addEventListener('click', async () => {
        renderPreview(await api.preview(value('downloadUrl')));
    });

    page.querySelector('#submitButton').addEventListener('click', async () => {
        await api.submit({
            url: value('downloadUrl'),
            isPlaylist: checked('isPlaylist'),
            formatPreset: parseInt(value('formatPreset') || '0', 10),
            namingTemplate: parseInt(value('namingTemplate') || '0', 10)
        });
        message('Job submitted.');
        await refreshQueue();
    });

    page.querySelector('#queuePanel').addEventListener('click', async (event) => {
        const cancelId = event.target.closest('[data-cancel]')?.getAttribute('data-cancel');
        const retryId = event.target.closest('[data-retry]')?.getAttribute('data-retry');
        if (cancelId) {
            await api.cancel(cancelId);
            await refreshQueue();
        }
        if (retryId) {
            await api.retry(retryId);
            await refreshQueue();
        }
    });

    document.addEventListener('pageshow', (event) => {
        if (event.target && event.target.id === 'rippletubePage') {
            load();
        }
    });
    load();
    setInterval(refreshQueue, 5000);

    function normalizeEnum(value, map) {
        if (typeof value === 'number') {
            return value;
        }

        return map[value] ?? 0;
    }
}());

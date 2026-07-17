<script lang="ts">
	import * as api from '$lib/index';
	import ProgressBar from '$lib/components/elements/progress-bar.svelte';
	import { slideshowStore } from '$lib/stores/slideshow.store';
	import {
		clientIdentifierStore,
		clientNameStore,
		authSecretStore
	} from '$lib/stores/persist.store';
	import { onDestroy, onMount, setContext, tick } from 'svelte';
	import OverlayControls from '../elements/overlay-controls.svelte';
	import AssetComponent from '../elements/asset-component.svelte';
	import type AssetComponentInstance from '../elements/asset-component.svelte';
	import { configStore } from '$lib/stores/config.store';
	import ErrorElement from '../elements/error-element.svelte';
	import Clock from '../elements/clock.svelte';
	import Appointments from '../elements/appointments.svelte';
	import Weather from '../elements/weather.svelte';
	import MetadataStack from '../elements/metadata-stack.svelte';
	import LoadingElement from '../elements/LoadingElement.svelte';
	import { page } from '$app/state';
	import { ProgressBarLocation, ProgressBarStatus } from '../elements/progress-bar.types';
	import { isImageAsset, isVideoAsset } from '$lib/constants/asset-type';
	import {
		acknowledgeFrameSessionCommand,
		disconnectFrameSession,
		getFrameSessionCommands,
		putFrameSessionSnapshot,
		reportFrameClientLog,
		sendBeaconFrameSessionDisconnect
	} from '$lib/frameSessionApi';
	import {
		getCornerDockClass,
		getWidgetStyle,
		normalizeWidgetPosition,
		normalizeWidgetStackOrder,
		type WidgetKey,
		type WidgetPosition
	} from '$lib/widget-layout';

	interface AssetsState {
		assets: [string, api.AssetResponseDto, api.AssetFaceResponseDto[], api.AlbumResponseDto[]][];
		error: boolean;
		loaded: boolean;
		split: boolean;
		hasBday: boolean;
	}

	interface SessionDisplayEvent {
		displayedAtUtc: string;
		durationSeconds: number;
		assets: api.AssetResponseDto[];
	}

	type FrameConnectivityState = 'loading' | 'ready' | 'reconnecting' | 'auth_error' | 'fatal_error';
	type FrameFailureKind = 'retryable' | 'auth' | 'fatal';

	interface FrameRequestError extends Error {
		status?: number;
		kind?: FrameFailureKind;
	}

	interface AssetLoadError extends FrameRequestError {
		assetUrlToRevoke?: string;
	}

	api.init();

	const PRELOAD_ASSETS = 5;
	const HEARTBEAT_INTERVAL_MS = 10_000;
	const COMMAND_POLL_INTERVAL_MS = 2_000;
	const MAX_DISPLAY_HISTORY = 50;
	const RECONNECT_PROBE_INTERVAL_MS = 1_500;
	const TRANSITION_WATCHDOG_MS = 10000;
	const VIDEO_STALL_MS = 15000;
	const CURSOR_HIDE_MS = 2000;

	let assetHistory: api.AssetResponseDto[] = [];
	let assetBacklog: api.AssetResponseDto[] = [];
	let displayEventHistory: SessionDisplayEvent[] = [];

	let displayingAssets: api.AssetResponseDto[] = $state([]);
	let currentDisplayStartedAt: string | null = $state(null);
	let currentDisplayDurationSeconds: number = $state($configStore.interval ?? 20);
	let currentDisplayPausedAtMs: number | null = $state(null);
	let adminStopped: boolean = $state(false);

	const { restartProgress, stopProgress, instantTransition } = slideshowStore;

	let progressBarStatus: ProgressBarStatus = $state(ProgressBarStatus.Playing);
	let progressBar: ProgressBar = $state() as ProgressBar;
	let assetComponent: AssetComponentInstance = $state() as AssetComponentInstance;
	let currentDuration: number = $state($configStore.interval ?? 20);
	let activeDisplayGeneration: number = $state(0);
	let consecutiveErrorSkips = 0;
	let errorSkipScheduled = false;
	let watchdogTimer: number | undefined;
	let videoStallTimeout: number | undefined;
	let userPaused: boolean = $state(false);

	let infoVisible: boolean = $state(false);
	let connectivityState: FrameConnectivityState = $state('loading');
	let reconnectMessage: string = $state('Reconnecting to Immich... retrying automatically.');
	let fatalErrorMessage: string = $state('');
	let assetsState: AssetsState = $state({
		assets: [],
		error: false,
		loaded: false,
		split: false,
		hasBday: false
	});
	let assetPromisesDict: Record<
		string,
		Promise<[string, api.AssetResponseDto, api.AssetFaceResponseDto[], api.AlbumResponseDto[]]>
	> = {};

	let unsubscribeRestart: () => void;
	let unsubscribeStop: () => void;

	let cursorVisible = $state(true);
	// Shared by every Immich-backed API call below. When the transition watchdog force-resets a
	// hung attempt, this gets aborted (cancelling whatever requests it was still waiting on) and
	// replaced, so retries don't pile up abandoned-but-still-running requests on top of each
	// other and exhaust the browser's per-origin connection limit.
	let currentAbortController = new AbortController();
	let timeoutId: number;
	let heartbeatIntervalId: number | undefined;
	let commandPollIntervalId: number | undefined;
	let isPollingCommands = false;
	let isHandlingRemoteCommand = false;
	let isSyncInFlight = false;
	let pendingSync = false;
	let pendingSyncStatus: 'Active' | 'Stopped' | null = null;
	let pendingDisplayNameSync = $state(false);
	let overlayPausedPlayback = $state(false);
	let reconnectPausedPlayback = $state(false);
	let lastAppliedClientIdentifierFromUrl: string | null | undefined = $state(undefined);
	let lastAppliedClientNameFromUrl: string | null | undefined = $state(undefined);
	let lastAppliedAuthSecretFromUrl: string | null | undefined = $state(undefined);
	let handledCommandIds = new Set<number>();
	let reconnectTimeoutId: number | undefined;
	let isReconnectProbeInFlight = false;
	let reconnectProbeEpoch = 0;
	let reconnectWatchdogTimer: number | undefined;
	const widgetStackOrder = $derived(
		normalizeWidgetStackOrder($configStore.widgetStackOrder)
	);

	function getWidgetPosition(widget: WidgetKey): WidgetPosition {
		switch (widget) {
			case 'clock':
				return normalizeWidgetPosition($configStore.clockPosition, 'bottom-left');
			case 'weather':
				return normalizeWidgetPosition($configStore.weatherPosition, 'bottom-left');
			case 'metadata':
				return normalizeWidgetPosition($configStore.metadataPosition, 'bottom-right');
			case 'calendar':
				return normalizeWidgetPosition($configStore.calendarPosition, 'top-right');
		}
	}

	function getWidgetFontStyle(widget: WidgetKey) {
		switch (widget) {
			case 'clock':
				return getWidgetStyle($configStore.clockFontSize);
			case 'weather':
				return getWidgetStyle($configStore.weatherFontSize);
			case 'metadata':
				return getWidgetStyle($configStore.metadataFontSize);
			case 'calendar':
				return getWidgetStyle($configStore.calendarFontSize);
		}
	}

	function renderWidgetInCorner(widget: WidgetKey, corner: WidgetPosition) {
		return getWidgetPosition(widget) === corner;
	}

	function createFrameRequestError(
		kind: FrameFailureKind,
		message: string,
		status?: number
	): FrameRequestError {
		const error = new Error(message) as FrameRequestError;
		error.kind = kind;
		error.status = status;
		return error;
	}

	function getErrorStatus(error: unknown): number | undefined {
		if (
			typeof error === 'object' &&
			error != null &&
			'status' in error &&
			typeof (error as { status?: unknown }).status === 'number'
		) {
			return (error as { status: number }).status;
		}

		return undefined;
	}

	function getErrorKind(error: unknown): FrameFailureKind | undefined {
		if (
			typeof error === 'object' &&
			error != null &&
			'kind' in error &&
			typeof (error as { kind?: unknown }).kind === 'string'
		) {
			return (error as { kind: FrameFailureKind }).kind;
		}

		return undefined;
	}

	function getAssetUrlToRevoke(error: unknown): string | undefined {
		if (
			typeof error === 'object' &&
			error != null &&
			'assetUrlToRevoke' in error &&
			typeof (error as { assetUrlToRevoke?: unknown }).assetUrlToRevoke === 'string'
		) {
			return (error as { assetUrlToRevoke: string }).assetUrlToRevoke;
		}

		return undefined;
	}

	function statusToFailureKind(status: number): FrameFailureKind {
		if (status === 401 || status === 403) {
			return 'auth';
		}

		if ([408, 425, 429].includes(status) || status >= 500) {
			return 'retryable';
		}

		return 'fatal';
	}

	function classifyFrameFailure(error: unknown): { kind: FrameFailureKind; message: string } {
		const explicitKind = getErrorKind(error);
		if (explicitKind) {
			return {
				kind: explicitKind,
				message:
					error instanceof Error && error.message
						? error.message
						: explicitKind === 'auth'
							? 'Could not authenticate client'
							: explicitKind === 'fatal'
								? 'Failed to load assets from Immich.'
								: 'Reconnecting to Immich... retrying automatically.'
			};
		}

		const status = getErrorStatus(error);
		if (status != null) {
			if (statusToFailureKind(status) === 'auth') {
				return { kind: 'auth', message: 'Could not authenticate client' };
			}

			if (statusToFailureKind(status) === 'retryable') {
				return {
					kind: 'retryable',
					message: 'Reconnecting to Immich... retrying automatically.'
				};
			}

			return {
				kind: 'fatal',
				message: 'Looks like your immich-server is offline or you misconfigured immichFrame, check the container logs'
			};
		}

		return {
			kind: 'retryable',
			message: 'Reconnecting to Immich... retrying automatically.'
		};
	}

	function hasDisplayedAsset() {
		return displayingAssets.length > 0 && assetsState.loaded && assetsState.assets.length > 0;
	}

	function isActiveVideoDisplay(displayGeneration: number) {
		return (
			displayGeneration === activeDisplayGeneration &&
			displayingAssets.some((asset) => isVideoAsset(asset))
		);
	}

	function clearReconnectRetry() {
		if (reconnectTimeoutId != null) {
			clearTimeout(reconnectTimeoutId);
			reconnectTimeoutId = undefined;
		}
		isReconnectProbeInFlight = false;
	}

	$effect(() => {
		const clientIdentifierFromUrl = page.url.searchParams.get('client');
		if (clientIdentifierFromUrl !== lastAppliedClientIdentifierFromUrl) {
			lastAppliedClientIdentifierFromUrl = clientIdentifierFromUrl;
			if (clientIdentifierFromUrl && clientIdentifierFromUrl !== $clientIdentifierStore) {
				clientIdentifierStore.set(clientIdentifierFromUrl);
			}
		}

		const clientNameFromUrl = page.url.searchParams.get('name');
		if (clientNameFromUrl !== lastAppliedClientNameFromUrl) {
			lastAppliedClientNameFromUrl = clientNameFromUrl;
			if (clientNameFromUrl && clientNameFromUrl !== $clientNameStore) {
				clientNameStore.set(clientNameFromUrl);
				pendingDisplayNameSync = true;
			}
		}

		const authSecretFromUrl = page.url.searchParams.get('authsecret');
		if (authSecretFromUrl !== lastAppliedAuthSecretFromUrl) {
			lastAppliedAuthSecretFromUrl = authSecretFromUrl;
			if (authSecretFromUrl && authSecretFromUrl !== $authSecretStore) {
				authSecretStore.set(authSecretFromUrl);
				api.init();
			}
		}
	});

	const hideCursor = () => {
		cursorVisible = false;
	};

	setContext('close', provideClose);

	async function provideClose() {
		if (overlayPausedPlayback) {
			overlayPausedPlayback = false;
			await resumePlayback();
			return;
		}

		infoVisible = false;
	}

	const showCursor = () => {
		cursorVisible = true;
		clearTimeout(timeoutId);
		timeoutId = window.setTimeout(hideCursor, CURSOR_HIDE_MS);
	};

	function toSessionDisplayEvent(
		assets: api.AssetResponseDto[],
		displayedAtUtc: string,
		durationSeconds: number
	): SessionDisplayEvent {
		return {
			displayedAtUtc,
			durationSeconds,
			assets: [...assets]
		};
	}

	function archiveCurrentDisplay() {
		if (!displayingAssets.length || !currentDisplayStartedAt) {
			return;
		}

		displayEventHistory = [
			toSessionDisplayEvent(displayingAssets, currentDisplayStartedAt, currentDisplayDurationSeconds),
			...displayEventHistory
		].slice(0, MAX_DISPLAY_HISTORY);
	}

	function setCurrentDisplay(assets: api.AssetResponseDto[]) {
		currentDisplayStartedAt = assets.length ? new Date().toISOString() : null;
		currentDisplayPausedAtMs = null;
	}

	function pauseCurrentDisplayClock() {
		if (currentDisplayPausedAtMs == null) {
			currentDisplayPausedAtMs = Date.now();
		}
	}

	function resumeCurrentDisplayClock() {
		if (currentDisplayPausedAtMs == null || !currentDisplayStartedAt) {
			currentDisplayPausedAtMs = null;
			return;
		}

		const pausedDurationMs = Date.now() - currentDisplayPausedAtMs;
		currentDisplayStartedAt = new Date(
			new Date(currentDisplayStartedAt).getTime() + pausedDurationMs
		).toISOString();
		currentDisplayPausedAtMs = null;
	}

	function toDisplayedAssetDto(asset: api.AssetResponseDto) {
		return {
			id: asset.id,
			originalFileName: asset.originalFileName,
			type: asset.type,
			immichServerUrl: asset.immichServerUrl ?? null,
			localDateTime: asset.localDateTime,
			description: asset.exifInfo?.description ?? null,
			thumbhash: asset.thumbhash ?? null
		};
	}

	function buildCurrentDisplay() {
		if (!displayingAssets.length || !currentDisplayStartedAt) {
			return null;
		}

		return {
			displayedAtUtc: currentDisplayStartedAt,
			durationSeconds: currentDisplayDurationSeconds,
			assets: displayingAssets.map(toDisplayedAssetDto)
		};
	}

	function buildHistory() {
		return displayEventHistory.map((displayEvent) => ({
			displayedAtUtc: displayEvent.displayedAtUtc,
			durationSeconds: displayEvent.durationSeconds,
			assets: displayEvent.assets.map(toDisplayedAssetDto)
		}));
	}

	async function syncFrameSession(status: 'Active' | 'Stopped' = adminStopped ? 'Stopped' : 'Active') {
		if (!$clientIdentifierStore) {
			return;
		}

		if (isSyncInFlight) {
			pendingSync = true;
			pendingSyncStatus =
				status === 'Stopped' || pendingSyncStatus == null ? status : pendingSyncStatus;
			return;
		}

		const shouldSyncDisplayName = pendingDisplayNameSync;
		isSyncInFlight = true;
		try {
			await putFrameSessionSnapshot($clientIdentifierStore, {
				playbackState:
					adminStopped || progressBarStatus === ProgressBarStatus.Paused ? 'Paused' : 'Playing',
				status,
				displayName: shouldSyncDisplayName ? ($clientNameStore ?? null) : undefined,
				currentDisplay: buildCurrentDisplay(),
				history: buildHistory()
			});
			if (shouldSyncDisplayName) {
				pendingDisplayNameSync = false;
			}
		} catch (err) {
			console.warn('Failed to sync frame session:', err);
		} finally {
			isSyncInFlight = false;
			if (pendingSync) {
				const retryStatus = pendingSyncStatus ?? (adminStopped ? 'Stopped' : 'Active');
				pendingSync = false;
				pendingSyncStatus = null;
				await syncFrameSession(retryStatus);
			}
		}
	}

	function startSessionLoops() {
		stopSessionLoops();

		heartbeatIntervalId = window.setInterval(() => {
			void syncFrameSession();
		}, HEARTBEAT_INTERVAL_MS);

		commandPollIntervalId = window.setInterval(() => {
			void processPendingCommands();
		}, COMMAND_POLL_INTERVAL_MS);
	}

	function stopSessionLoops() {
		if (heartbeatIntervalId) {
			clearInterval(heartbeatIntervalId);
			heartbeatIntervalId = undefined;
		}

		if (commandPollIntervalId) {
			clearInterval(commandPollIntervalId);
			commandPollIntervalId = undefined;
		}
	}

	function ensureAssetPromise(asset: api.AssetResponseDto) {
		if (!(asset.id in assetPromisesDict)) {
			assetPromisesDict[asset.id] = loadAsset(asset).catch((err) => {
				const assetUrlToRevoke = getAssetUrlToRevoke(err);
				if (assetUrlToRevoke) {
					revokeObjectUrl(assetUrlToRevoke);
				}

				delete assetPromisesDict[asset.id];
				throw err;
			});
		}

		return assetPromisesDict[asset.id];
	}

	function primeAssetPromises(assets: api.AssetResponseDto[]) {
		for (const asset of assets) {
			ensureAssetPromise(asset);
		}
	}

	async function updateAssetPromises(
		currentAssets: api.AssetResponseDto[] = displayingAssets,
		backlogAssets: api.AssetResponseDto[] = assetBacklog
	) {
		for (let asset of currentAssets) {
			ensureAssetPromise(asset);
		}

		if (connectivityState !== 'reconnecting') {
			for (let i = 0; i < PRELOAD_ASSETS; i++) {
				if (i >= backlogAssets.length) {
					break;
				}

				void ensureAssetPromise(backlogAssets[i]).catch(() => undefined);
			}
		}

		const retainedAssetIds = new Set([
			...currentAssets.map((item) => item.id),
			...backlogAssets.map((item) => item.id)
		]);

		const keysToRemove = Object.keys(assetPromisesDict).filter(
			(key) => !retainedAssetIds.has(key)
		);

		for (const key of keysToRemove) {
			try {
				const [url] = await assetPromisesDict[key];
				revokeObjectUrl(url);
			} catch (err) {
				console.warn('Failed to resolve asset during cleanup:', err);
			} finally {
				delete assetPromisesDict[key];
			}
		}
	}

	async function loadAssets() {
		if (adminStopped) {
			return;
		}

		const assetRequest = await api.getAssets({}, { signal: currentAbortController.signal });

		if (assetRequest.status !== 200) {
			throw createFrameRequestError(
				statusToFailureKind(assetRequest.status),
				`Failed to load asset list: status ${assetRequest.status}`,
				assetRequest.status
			);
		}

		assetBacklog = assetRequest.data.filter((asset) => isImageAsset(asset) || isVideoAsset(asset));
	}

	async function pauseForReconnect() {
		if (reconnectPausedPlayback || adminStopped || !hasDisplayedAsset()) {
			return;
		}

		if (progressBarStatus !== ProgressBarStatus.Paused) {
			reconnectPausedPlayback = true;
			pauseCurrentDisplayClock();
			await assetComponent?.pause?.();
			await progressBar.pause();
			await syncFrameSession();
		}
	}

	async function scheduleReconnectRetry() {
		if (adminStopped || reconnectTimeoutId != null) {
			return;
		}

		reconnectTimeoutId = window.setTimeout(() => {
			reconnectTimeoutId = undefined;
			void retryAssetRecovery();
		}, RECONNECT_PROBE_INTERVAL_MS);
	}

	async function handleAssetPipelineFailure(error: unknown) {
		if (error instanceof DOMException && error.name === 'AbortError') {
			// A deliberately cancelled request (the transition watchdog aborting an abandoned
			// attempt) isn't a real connectivity failure - a fresh attempt is already underway,
			// so there's nothing to surface here.
			return false;
		}

		const failure = classifyFrameFailure(error);

		if (failure.kind === 'auth') {
			clearReconnectRetry();
			reconnectPausedPlayback = false;
			connectivityState = 'auth_error';
			fatalErrorMessage = failure.message;
			return false;
		}

		if (failure.kind === 'fatal') {
			clearReconnectRetry();
			reconnectPausedPlayback = false;
			connectivityState = 'fatal_error';
			fatalErrorMessage = failure.message;
			return false;
		}

		reconnectMessage = failure.message;
		connectivityState = 'reconnecting';
		await pauseForReconnect();
		await scheduleReconnectRetry();
		return false;
	}

	async function retryAssetRecovery() {
		if (
			adminStopped ||
			connectivityState === 'auth_error' ||
			connectivityState === 'fatal_error' ||
			isHandlingAssetTransition ||
			isReconnectProbeInFlight
		) {
			return;
		}

		const currentProbeEpoch = ++reconnectProbeEpoch;
		isReconnectProbeInFlight = true;

		clearTimeout(reconnectWatchdogTimer);
		// Watchdog: unlike handleDone's transition path, nothing else re-arms this loop if it
		// hangs (rather than rejects) - handleAssetPipelineFailure/scheduleReconnectRetry are
		// only reached on a rejection, so a stuck loadAssets()/getNextAssets() would otherwise
		// freeze the frame forever.
		reconnectWatchdogTimer = window.setTimeout(() => {
			if (currentProbeEpoch !== reconnectProbeEpoch || !isReconnectProbeInFlight) {
				return;
			}
			console.error('Reconnect watchdog triggered: force-resetting reconnect probe due to hang');
			if ($clientIdentifierStore) {
				reportFrameClientLog(
					$clientIdentifierStore,
					'Reconnect watchdog triggered: force-resetting reconnect probe due to hang'
				);
			}
			isReconnectProbeInFlight = false;
			currentAbortController.abort();
			currentAbortController = new AbortController();
			reconnectProbeEpoch++;
			void scheduleReconnectRetry();
		}, TRANSITION_WATCHDOG_MS);

		try {
			await loadAssets();

			const recovered = await getNextAssets();
			if (
				currentProbeEpoch !== reconnectProbeEpoch ||
				!recovered ||
				!displayingAssets.length ||
				adminStopped
			) {
				return;
			}

			await tick();
			reconnectPausedPlayback = false;
			await progressBar?.restart?.(false);
			await assetComponent?.play?.();
			void progressBar?.play?.();
			await syncFrameSession();
		} catch (error) {
			if (currentProbeEpoch === reconnectProbeEpoch) {
				await handleAssetPipelineFailure(error);
			}
		} finally {
			if (currentProbeEpoch === reconnectProbeEpoch) {
				isReconnectProbeInFlight = false;
				clearTimeout(reconnectWatchdogTimer);
			}
		}
	}

	let isHandlingAssetTransition = $state(false);
	let transitionEpoch = 0;
	let pendingTransition: { previous: boolean; instant: boolean; isErrorRecovery: boolean } | null =
		$state(null);

	const handleDone = async (
		previous: boolean = false,
		instant: boolean = false,
		isErrorRecovery: boolean = false
	) => {
		if (adminStopped || connectivityState === 'reconnecting') {
			return;
		}

		if (isHandlingAssetTransition) {
			pendingTransition = { previous, instant, isErrorRecovery };
			return;
		}

		const currentEpoch = ++transitionEpoch;
		isHandlingAssetTransition = true;

		clearTimeout(watchdogTimer);
		clearTimeout(videoStallTimeout);
		// Watchdog: If the transition (fetching/loading assets) hangs, force-release the lock.
		watchdogTimer = window.setTimeout(() => {
			if (currentEpoch === transitionEpoch && isHandlingAssetTransition) {
				console.error('Transition watchdog triggered: Force-resetting lock due to hang');
				if ($clientIdentifierStore) {
					reportFrameClientLog(
						$clientIdentifierStore,
						'Transition watchdog triggered: force-resetting lock due to hang'
					);
				}
				isHandlingAssetTransition = false;

				// Cancel whatever this attempt was still waiting on and hand the retry a fresh
				// controller, so the abandoned request doesn't keep occupying a connection
				// alongside the retry's own requests.
				currentAbortController.abort();
				currentAbortController = new AbortController();

				// Bump the epoch so the original (still-awaiting) transition becomes a no-op
				// when/if it eventually resolves, and force a fresh advance.
				transitionEpoch++;
				const next = pendingTransition ?? { previous: false, instant: true, isErrorRecovery: false };
				pendingTransition = null;
				handleDone(next.previous, next.instant, next.isErrorRecovery).catch((err) => {
					console.error('handleDone failed:', err);
					isHandlingAssetTransition = false;
				});
			}
		}, TRANSITION_WATCHDOG_MS);

		try {
			userPaused = false;
			await progressBar.restart(false);
			await assetComponent?.pause?.();
			$instantTransition = instant;
			const transitioned = previous ? await getPreviousAssets() : await getNextAssets();
			if (!transitioned) {
				return;
			}
			await tick();

			if (currentEpoch !== transitionEpoch) return;

			await assetComponent?.play?.();
			void progressBar.play();
			if (!isErrorRecovery) {
				consecutiveErrorSkips = 0;
			}
			await syncFrameSession();
		} finally {
			if (currentEpoch === transitionEpoch) {
				isHandlingAssetTransition = false;
				clearTimeout(watchdogTimer);

				if (pendingTransition) {
					const next = pendingTransition;
					pendingTransition = null;
					handleDone(next.previous, next.instant, next.isErrorRecovery).catch((err) => {
						console.error('handleDone failed:', err);
						isHandlingAssetTransition = false;
					});
				}
			}
		}
	};

	async function markAssetsReady() {
		clearReconnectRetry();
		connectivityState = 'ready';
		fatalErrorMessage = '';
	}

	async function prepareAssetsState(assets: api.AssetResponseDto[]) {
		updateCurrentDuration(assets);
		primeAssetPromises(assets);

		const newAssets: [
			string,
			api.AssetResponseDto,
			api.AssetFaceResponseDto[],
			api.AlbumResponseDto[]
		][] = [];
		for (const asset of assets) {
			newAssets.push(await ensureAssetPromise(asset));
		}

		return {
			assets: newAssets,
			error: false,
			loaded: true,
			split: assets.length === 2 && assets.every(isImageAsset),
			hasBday: hasBirthday(assets)
		} satisfies AssetsState;
	}

	async function getNextAssets() {
		try {
			await getNextAssetsCore();
			await markAssetsReady();
			return true;
		} catch (error) {
			return await handleAssetPipelineFailure(error);
		}
	}

	async function getNextAssetsCore() {
		if (!assetBacklog.length) {
			await loadAssets();
		}

		if (!assetBacklog.length) {
			throw createFrameRequestError(
				'fatal',
				'No assets were found! Check your configuration.'
			);
		}

		const useSplit = shouldUseSplitView(assetBacklog);
		const next = assetBacklog.slice(0, useSplit ? 2 : 1);
		const nextAssetsState = await prepareAssetsState(next);
		const nextBacklog = assetBacklog.slice(next.length);

		if (displayingAssets.length) {
			assetHistory.push(...displayingAssets);
			archiveCurrentDisplay();
		}

		if (assetHistory.length > 250) {
			assetHistory = assetHistory.slice(-250);
		}

		assetBacklog = nextBacklog;
		displayingAssets = next;
		activeDisplayGeneration += 1;
		setCurrentDisplay(next);
		assetsState = nextAssetsState;
		currentDisplayDurationSeconds = currentDuration;
		await updateAssetPromises();
	}

	async function getPreviousAssets() {
		try {
			await getPreviousAssetsCore();
			await markAssetsReady();
			return true;
		} catch (error) {
			return await handleAssetPipelineFailure(error);
		}
	}

	async function getPreviousAssetsCore() {
		if (!assetHistory.length) {
			if (displayingAssets.length) {
				setCurrentDisplay(displayingAssets);
				currentDisplayDurationSeconds = currentDuration;
			}
			return;
		}

		const useSplit = shouldUseSplitView(assetHistory.slice(-2));
		const next = assetHistory.slice(useSplit ? -2 : -1);
		const nextAssetsState = await prepareAssetsState(next);
		const nextHistory = assetHistory.slice(0, useSplit ? -2 : -1);

		if (displayingAssets.length) {
			archiveCurrentDisplay();
		}

		assetHistory = nextHistory;
		if (displayingAssets.length) {
			assetBacklog = [...displayingAssets, ...assetBacklog];
		}

		displayingAssets = next;
		activeDisplayGeneration += 1;
		setCurrentDisplay(next);
		assetsState = nextAssetsState;
		currentDisplayDurationSeconds = currentDuration;
		await updateAssetPromises();
	}

	function isPortrait(asset: api.AssetResponseDto) {
		if (isVideoAsset(asset)) {
			return false;
		}

		const isFlipped = (orientation: number) => [5, 6, 7, 8].includes(orientation);
		let assetHeight = asset.exifInfo?.exifImageHeight ?? 0;
		let assetWidth = asset.exifInfo?.exifImageWidth ?? 0;
		if (isFlipped(Number(asset.exifInfo?.orientation ?? 0))) {
			[assetHeight, assetWidth] = [assetWidth, assetHeight];
		}
		return assetHeight > assetWidth;
	}

	function shouldUseSplitView(assets: api.AssetResponseDto[]): boolean {
		return (
			$configStore.layout?.trim().toLowerCase() === 'splitview' &&
			assets.length > 1 &&
			isImageAsset(assets[0]) &&
			isImageAsset(assets[1]) &&
			isPortrait(assets[0]) &&
			isPortrait(assets[1])
		);
	}

	function hasBirthday(assets: api.AssetResponseDto[]) {
		let today = new Date();
		let hasBday: boolean = false;

		for (let asset of assets) {
			for (let person of asset.people ?? []) {
				let birthdate = new Date(person.birthDate ?? '');
				if (birthdate.getDate() === today.getDate() && birthdate.getMonth() === today.getMonth()) {
					hasBday = true;
					break;
				}
			}
			if (hasBday) break;
		}

		return hasBday;
	}

	function updateCurrentDuration(assets: api.AssetResponseDto[]) {
		const durations = assets
			.map((asset) => getAssetDurationSeconds(asset))
			.filter((value) => value > 0);
		const fallback = $configStore.interval ?? 20;
		currentDuration = durations.length ? Math.max(...durations) : fallback;
	}

	function getAssetDurationSeconds(asset: api.AssetResponseDto) {
		if (isVideoAsset(asset)) {
			const parsed = parseAssetDuration(asset.duration);
			const fallback = $configStore.interval ?? 20;
			return parsed > 0 ? parsed : fallback;
		}
		return $configStore.interval ?? 20;
	}

	function parseAssetDuration(duration?: number | null) {
		if (!duration || duration <= 0) {
			return 0;
		}
		return duration / 1000; // milliseconds → seconds
	}

	async function loadAsset(assetResponse: api.AssetResponseDto) {
		let assetUrl: string | undefined;

		try {
			if (isVideoAsset(assetResponse)) {
				assetUrl = api.getAssetStreamUrl(
					assetResponse.id,
					$clientIdentifierStore,
					assetResponse.type
				);
			} else {
				const req = await api.getAsset(
					assetResponse.id,
					{
						clientIdentifier: $clientIdentifierStore,
						assetType: assetResponse.type
					},
					{ signal: currentAbortController.signal }
				);
				if (req.status != 200) {
					throw createFrameRequestError(
						statusToFailureKind(req.status),
						`Failed to load asset ${assetResponse.id}: status ${req.status}`,
						req.status
					);
				}
				assetUrl = getObjectUrl(req.data);
			}

			let album: api.AlbumResponseDto[] | null = null;
			if ($configStore.showAlbumName) {
				const albumReq = await api.getAlbumInfo(
					assetResponse.id,
					{ clientIdentifier: $clientIdentifierStore },
					{ signal: currentAbortController.signal }
				);
				if (albumReq.status !== 200) {
					throw createFrameRequestError(
						statusToFailureKind(albumReq.status),
						`Failed to load album info for asset ${assetResponse.id}: status ${albumReq.status}`,
						albumReq.status
					);
				}
				album = albumReq.data ?? [];
			}

			if ($configStore.showPeopleDesc && (assetResponse.people ?? []).length == 0) {
				const assetInfoRequest = await api.getAssetInfo(
					assetResponse.id,
					{ clientIdentifier: $clientIdentifierStore },
					{ signal: currentAbortController.signal }
				);
				if (assetInfoRequest.status !== 200) {
					throw createFrameRequestError(
						statusToFailureKind(assetInfoRequest.status),
						`Failed to load asset info for asset ${assetResponse.id}: status ${assetInfoRequest.status}`,
						assetInfoRequest.status
					);
				}
				assetResponse.people = assetInfoRequest.data.people;
			}

			let faces: api.AssetFaceResponseDto[] = [];
			if (!isVideoAsset(assetResponse) && ($configStore.imageZoom || $configStore.imagePan)) {
				const facesRequest = await api.getAssetFaces(
					assetResponse.id,
					{ clientIdentifier: $clientIdentifierStore },
					{ signal: currentAbortController.signal }
				);
				if (facesRequest.status === 200) {
					faces = facesRequest.data;
				}
			}

			return [assetUrl, assetResponse, faces, album] as [
				string,
				api.AssetResponseDto,
				api.AssetFaceResponseDto[],
				api.AlbumResponseDto[]
			];
		} catch (error) {
			if (assetUrl?.startsWith('blob:')) {
				(error as AssetLoadError).assetUrlToRevoke = assetUrl;
			}

			throw error;
		}
	}

	function getObjectUrl(image: Blob) {
		return URL.createObjectURL(image);
	}

	function revokeObjectUrl(url: string) {
		if (!url.startsWith('blob:')) return;
		try {
			URL.revokeObjectURL(url);
		} catch {
			console.warn('Failed to revoke object URL:', url);
		}
	}

	async function resumePlayback() {
		if (adminStopped || connectivityState === 'reconnecting') {
			return;
		}

		overlayPausedPlayback = false;
		infoVisible = false;
		userPaused = false;
		resumeCurrentDisplayClock();
		await assetComponent?.play?.();
		void progressBar.play();
		await syncFrameSession();
	}

	async function pausePlayback() {
		if (adminStopped) {
			return;
		}

		infoVisible = false;
		userPaused = true;
		pauseCurrentDisplayClock();
		await assetComponent?.pause?.();
		await progressBar.pause();
		await syncFrameSession();
	}

	async function togglePlayback() {
		if (progressBarStatus == ProgressBarStatus.Paused) {
			await resumePlayback();
		} else {
			await pausePlayback();
		}
	}

	async function toggleInfo() {
		if (adminStopped) {
			return;
		}

		if (infoVisible) {
			if (overlayPausedPlayback) {
				overlayPausedPlayback = false;
				await resumePlayback();
			} else {
				infoVisible = false;
			}
		} else {
			infoVisible = true;
			overlayPausedPlayback = progressBarStatus !== ProgressBarStatus.Paused;
			if (overlayPausedPlayback) {
				userPaused = true;
				pauseCurrentDisplayClock();
				await assetComponent?.pause?.();
				await progressBar.pause();
				await syncFrameSession();
			}
		}
	}

	async function shutdownFromAdmin() {
		if (adminStopped) {
			return;
		}

		adminStopped = true;
		clearReconnectRetry();
		connectivityState = 'ready';
		reconnectPausedPlayback = false;
		overlayPausedPlayback = false;
		infoVisible = false;
		userPaused = true;
		stopSessionLoops();
		progressBar.restart(false);
		await assetComponent?.pause?.();
		await progressBar.pause();
		await syncFrameSession('Stopped');
	}

	async function processPendingCommands() {
		if (adminStopped || isPollingCommands || isHandlingRemoteCommand || !$clientIdentifierStore) {
			return;
		}

		isPollingCommands = true;
		try {
			const commands = await getFrameSessionCommands($clientIdentifierStore);
			for (const command of commands) {
				if (handledCommandIds.has(command.commandId)) {
					continue;
				}

				handledCommandIds.add(command.commandId);
				isHandlingRemoteCommand = true;
				try {
					switch (command.commandType) {
						case 'Previous':
							await handleDone(true, true);
							infoVisible = false;
							break;
						case 'Play':
							if (progressBarStatus === ProgressBarStatus.Paused || infoVisible || userPaused) {
								await resumePlayback();
							}
							break;
						case 'Pause':
							if (progressBarStatus !== ProgressBarStatus.Paused) {
								await pausePlayback();
							}
							break;
						case 'Next':
							await handleDone(false, true);
							infoVisible = false;
							break;
						case 'Refresh':
							await acknowledgeFrameSessionCommand($clientIdentifierStore, command.commandId);
							handledCommandIds.delete(command.commandId);
							window.location.reload();
							return;
						case 'Shutdown':
							await acknowledgeFrameSessionCommand($clientIdentifierStore, command.commandId);
							handledCommandIds.delete(command.commandId);
							await shutdownFromAdmin();
							return;
					}

					await acknowledgeFrameSessionCommand($clientIdentifierStore, command.commandId);
					handledCommandIds.delete(command.commandId);
				} catch (err) {
					console.warn('Failed to handle remote command:', err);
				} finally {
					isHandlingRemoteCommand = false;
				}
			}
		} finally {
			isPollingCommands = false;
		}
	}

	onMount(() => {
		window.addEventListener('mousemove', showCursor);
		window.addEventListener('click', showCursor);
		window.addEventListener('beforeunload', handleBeforeUnload);

		if ($configStore.primaryColor) {
			document.documentElement.style.setProperty('--primary-color', $configStore.primaryColor);
		}

		if ($configStore.secondaryColor) {
			document.documentElement.style.setProperty('--secondary-color', $configStore.secondaryColor);
		}

		if ($configStore.baseFontSize) {
			document.documentElement.style.fontSize = $configStore.baseFontSize;
		} else {
			document.documentElement.style.removeProperty('font-size');
		}

		unsubscribeRestart = restartProgress.subscribe((value) => {
			if (value) {
				progressBar.restart(value);
				assetComponent?.play?.();
				void syncFrameSession();
			}
		});

		unsubscribeStop = stopProgress.subscribe((value) => {
			if (value) {
				progressBar.restart(false);
				assetComponent?.pause?.();
				void syncFrameSession();
			}
		});

		startSessionLoops();
		// Routed through retryAssetRecovery() (rather than calling getNextAssets() directly) so
		// the very first load gets the same watchdog protection as every later reconnect
		// attempt - otherwise a hang on this first fetch has nothing to ever retry it.
		void retryAssetRecovery();

		return () => {
			window.removeEventListener('mousemove', showCursor);
			window.removeEventListener('click', showCursor);
			window.removeEventListener('beforeunload', handleBeforeUnload);
			window.clearTimeout(timeoutId);
			window.clearTimeout(videoStallTimeout);
			window.clearTimeout(watchdogTimer);
		};
	});

	async function handleBeforeUnload() {
		if (!$clientIdentifierStore) {
			return;
		}

		if (sendBeaconFrameSessionDisconnect($clientIdentifierStore, $authSecretStore)) {
			return;
		}

		try {
			await disconnectFrameSession($clientIdentifierStore, true, $authSecretStore);
		} catch (err) {
			console.warn('Failed to disconnect frame session during unload:', err);
		}
	}

	onDestroy(async () => {
		clearReconnectRetry();
		stopSessionLoops();

		if (unsubscribeRestart) {
			unsubscribeRestart();
		}

		if (unsubscribeStop) {
			unsubscribeStop();
		}

		const revokes = Object.values(assetPromisesDict).map(async (p) => {
			try {
				const [url] = await p;
				revokeObjectUrl(url);
			} catch (err) {
				console.warn('Failed to resolve asset during destroy cleanup:', err);
			}
		});
		await Promise.allSettled(revokes);
		assetPromisesDict = {};
	});
</script>

<section class="fixed grid h-dvh-safe w-screen bg-black" class:cursor-none={!cursorVisible}>
	{#if adminStopped}
		<div class="place-self-center w-full max-w-2xl px-8">
			<div
				class="rounded-[2rem] border border-white/10 bg-white/10 p-10 text-center text-white shadow-2xl backdrop-blur"
			>
				<p class="text-xs uppercase tracking-[0.45em] text-white/60">Remote Control</p>
				<h1 class="mt-4 text-4xl font-semibold">Frame Stopped</h1>
				<p class="mt-4 text-lg text-white/70">
					This frame session was stopped from the admin dashboard. Refresh the page to reconnect.
				</p>
				{#if $clientIdentifierStore}
					<p class="mt-6 font-mono text-sm text-white/50">Session: {$clientIdentifierStore}</p>
				{/if}
			</div>
		</div>
	{:else if connectivityState === 'auth_error'}
		<ErrorElement authError />
	{:else if connectivityState === 'fatal_error'}
		<ErrorElement message={fatalErrorMessage} />
	{:else if displayingAssets.length}
		<div class="absolute h-screen w-screen">
			<AssetComponent
				showLocation={$configStore.showImageLocation}
				displayGeneration={activeDisplayGeneration}
				interval={currentDuration}
				showPhotoDate={$configStore.showPhotoDate}
				showImageDesc={$configStore.showImageDesc}
				showPeopleDesc={$configStore.showPeopleDesc}
				showTagsDesc={$configStore.showTagsDesc}
				showAlbumName={$configStore.showAlbumName}
				{...assetsState}
				imageFill={$configStore.imageFill}
				imageZoom={$configStore.imageZoom}
				imagePan={$configStore.imagePan}
				bind:this={assetComponent}
				bind:showInfo={infoVisible}
				playAudio={$configStore.playAudio}
				onVideoWaiting={async (displayGeneration) => {
					if (
						adminStopped ||
						reconnectPausedPlayback ||
						connectivityState === 'reconnecting' ||
						!isActiveVideoDisplay(displayGeneration)
					) {
						return;
					}

					pauseCurrentDisplayClock();
					await progressBar.pause();
					await syncFrameSession();

					clearTimeout(videoStallTimeout);
					if (userPaused) return;

					videoStallTimeout = window.setTimeout(
						() => {
							if (!userPaused) {
								console.warn('Video stalled, skipping...');
								if ($clientIdentifierStore) {
									reportFrameClientLog($clientIdentifierStore, 'Video stalled, skipping');
								}
								handleDone(false, true);
							}
						},
						Math.max(5000, Math.min(VIDEO_STALL_MS, currentDuration * 1000))
					);
				}}
				onVideoPlaying={async (displayGeneration) => {
					if (
						adminStopped ||
						reconnectPausedPlayback ||
						connectivityState === 'reconnecting' ||
						userPaused ||
						!isActiveVideoDisplay(displayGeneration)
					) {
						return;
					}

					clearTimeout(videoStallTimeout);
					resumeCurrentDisplayClock();
					await progressBar.play();
					await syncFrameSession();
				}}
				onAssetError={async () => {
					if (errorSkipScheduled) return;
					errorSkipScheduled = true;

					consecutiveErrorSkips++;
					if (consecutiveErrorSkips > 10) {
						connectivityState = 'fatal_error';
						fatalErrorMessage =
							'Too many consecutive asset load failures. Please check your network or server connection.';
						errorSkipScheduled = false;
						return;
					}

					try {
						await handleDone(false, true, true);
					} finally {
						errorSkipScheduled = false;
					}
				}}
			/>
		</div>

		{#if connectivityState === 'reconnecting'}
			<div class="pointer-events-none fixed inset-x-0 top-4 z-20 flex justify-center px-4">
				<div
					class="rounded-full border border-white/15 bg-black/65 px-4 py-2 text-sm text-white shadow-lg backdrop-blur"
				>
					{reconnectMessage}
				</div>
			</div>
		{/if}

		{#each ['top-left', 'top-right', 'bottom-left', 'bottom-right'] as corner}
			<div
				class={`pointer-events-none fixed z-10 flex w-[min(24rem,calc(100vw-1.5rem))] max-w-full gap-1 ${
					corner.startsWith('bottom') ? 'flex-col-reverse' : 'flex-col'
				} ${getCornerDockClass(corner as WidgetPosition)}`}
			>
				{#each widgetStackOrder as widget}
					{#if renderWidgetInCorner(widget, corner as WidgetPosition)}
						<div
							class={`pointer-events-auto ${
								corner.endsWith('right') ? 'self-end text-right' : 'self-start text-left'
							}`}
							style={getWidgetFontStyle(widget)}
						>
							{#if widget === 'clock' && $configStore.showClock}
								<Clock />
							{:else if widget === 'weather' && $configStore.showWeather}
								<Weather />
							{:else if widget === 'metadata' && $configStore.showMetadata}
								<MetadataStack
									entries={assetsState.assets.map(([, asset, , albums]) => ({ asset, albums }))}
									split={assetsState.split}
								/>
							{:else if widget === 'calendar' && $configStore.showCalendar}
								<Appointments />
							{/if}
						</div>
					{/if}
				{/each}
			</div>
		{/each}

		<OverlayControls
			next={async () => {
				await handleDone(false, true);
				infoVisible = false;
			}}
			back={async () => {
				await handleDone(true, true);
				infoVisible = false;
			}}
			pause={togglePlayback}
			showInfo={toggleInfo}
			bind:status={progressBarStatus}
			bind:infoVisible
			overlayVisible={cursorVisible}
		/>

		<ProgressBar
			duration={currentDuration}
			hidden={!$configStore.showProgressBar}
			location={ProgressBarLocation.Bottom}
			bind:this={progressBar}
			bind:status={progressBarStatus}
			onDone={handleDone}
		/>
	{:else}
		<div class="grid absolute h-dvh-safe w-screen place-items-center px-6">
			<div class="flex max-w-md flex-col items-center gap-6 text-center text-white">
				<LoadingElement />
				<p class="text-sm text-white/70">
					{connectivityState === 'reconnecting'
						? reconnectMessage
						: 'Loading frame...'}
				</p>
			</div>
		</div>
	{/if}
</section>

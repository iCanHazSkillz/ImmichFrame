<script lang="ts">
	import { beforeNavigate } from '$app/navigation';
	import { mdiArrowLeft, mdiContentSave, mdiEyeOffOutline, mdiEyeOutline, mdiLogout } from '@mdi/js';
	import { onMount } from 'svelte';
	import ColorInput from '$lib/components/admin-settings/ColorInput.svelte';
	import PresetSelect from '$lib/components/admin-settings/PresetSelect.svelte';
	import SettingLabel from '$lib/components/admin-settings/SettingLabel.svelte';
	import TokenListEditor from '$lib/components/admin-settings/TokenListEditor.svelte';
	import Icon from '$lib/components/elements/icon.svelte';
	import {
		FrameSessionApiError,
		getAdminAuthSession,
		getAdminSettings,
		logoutAdmin,
		validateAdminAlbums,
		type AdminAlbumValidationResultDto,
		type AdminAuthSessionDto,
		type AdminManagedAccountSettings,
		type AdminManagedGeneralSettings,
		type AdminSettingsResponseDto,
		updateAdminSettings
	} from '$lib/frameSessionApi';
	import {
		widgetPositionOptions,
		widgetStackDefaults,
		type WidgetKey
	} from '$lib/widget-layout';

	const inputClass =
		'w-full rounded-2xl border border-white/12 bg-stone-950/85 px-4 py-3 text-sm text-stone-100 outline-none transition placeholder:text-stone-500 focus:border-[color:var(--primary-color)]/75';
	const sectionClass =
		'rounded-[2rem] border border-white/10 bg-black/30 p-5 shadow-2xl backdrop-blur sm:p-6';
	const subSectionClass =
		'rounded-[1.5rem] border border-white/10 bg-white/[0.03] p-4 shadow-lg sm:p-5';
	const toggleCardClass =
		'flex items-start justify-between gap-4 rounded-[1.25rem] border border-white/10 bg-white/[0.03] px-4 py-4';

	const UUID_PATTERN =
		/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

	type AlbumListKey = 'albums' | 'excludedAlbums';
	type AlbumTokenStatus = {
		status: 'valid' | 'notFoundOrNoAccess' | 'error' | 'pending';
		label: string;
		message?: string | null;
		statusCode?: number | null;
	};
	type AccountAlbumValidationState = Record<AlbumListKey, Record<string, AlbumTokenStatus>>;

	const clockFormatOptions = [
		{ value: 'hh:mm', label: '12-hour, leading zero', hint: 'Example: 08:30' },
		{ value: 'h:mm a', label: '12-hour with AM/PM', hint: 'Example: 8:30 PM' },
		{ value: 'hh:mm a', label: '12-hour padded with AM/PM', hint: 'Example: 08:30 PM' },
		{ value: 'HH:mm', label: '24-hour', hint: 'Example: 20:30' },
		{ value: 'HH:mm:ss', label: '24-hour with seconds', hint: 'Example: 20:30:45' }
	];

	const clockDateFormatOptions = [
		{ value: 'eee, MMM d', label: 'Short weekday and month', hint: 'Example: Tue, Apr 1' },
		{ value: 'EEEE, MMMM d', label: 'Full weekday and month', hint: 'Example: Tuesday, April 1' },
		{ value: 'MMM d', label: 'Short month and day', hint: 'Example: Apr 1' },
		{ value: 'PP', label: 'Localized long date', hint: 'Example: Apr 1, 2026' },
		{ value: 'yyyy-MM-dd', label: 'ISO date', hint: 'Example: 2026-04-01' }
	];

	const photoDateFormatOptions = [
		{ value: 'MM/dd/yyyy', label: 'Month / day / year', hint: 'Example: 04/01/2026' },
		{ value: 'dd/MM/yyyy', label: 'Day / month / year', hint: 'Example: 01/04/2026' },
		{ value: 'yyyy-MM-dd', label: 'ISO date', hint: 'Example: 2026-04-01' },
		{ value: 'MMM d, yyyy', label: 'Short month name', hint: 'Example: Apr 1, 2026' },
		{ value: 'dd MMM yyyy', label: 'Day then month name', hint: 'Example: 01 Apr 2026' }
	];

	const imageLocationFormatOptions = [
		{ value: 'City,State,Country', label: 'City, state, country', hint: 'Example: Edmonton,Alberta,Canada' },
		{ value: 'City,Country', label: 'City and country', hint: 'Example: Edmonton,Canada' },
		{ value: 'State,Country', label: 'State and country', hint: 'Example: Alberta,Canada' },
		{ value: 'City', label: 'City only', hint: 'Example: Edmonton' },
		{ value: 'Country', label: 'Country only', hint: 'Example: Canada' }
	];

	const baseFontSizeOptions = [
		{ value: 'xx-small', label: 'xx-small', hint: 'Very small text.' },
		{ value: 'x-small', label: 'x-small', hint: 'Extra small text.' },
		{ value: 'small', label: 'small', hint: 'Smaller than the default size.' },
		{ value: 'medium', label: 'medium', hint: 'Browser standard medium text.' },
		{ value: 'large', label: 'large', hint: 'Larger than the default size.' },
		{ value: 'x-large', label: 'x-large', hint: 'Extra large text.' },
		{ value: 'xx-large', label: 'xx-large', hint: 'Very large text.' },
		{ value: 'xxx-large', label: 'xxx-large', hint: 'Maximum preset size in this list.' }
	];

	const widgetStyleOptions = [
		{ value: 'none', label: 'none', hint: 'No background treatment.' },
		{ value: 'solid', label: 'solid', hint: 'Solid secondary color backing.' },
		{ value: 'transition', label: 'transition', hint: 'Directional gradient backing.' },
		{ value: 'blur', label: 'blur', hint: 'Blurred translucent backdrop.' }
	];
	const calendarSortDirectionOptions = [
		{ value: 'ascending', label: 'Ascending' },
		{ value: 'descending', label: 'Descending' }
	] as const;
	const widgetLabels: Record<WidgetKey, string> = {
		clock: 'Clock',
		weather: 'Weather',
		metadata: 'Metadata',
		calendar: 'Calendar'
	};

	const imageToggleFields = [
		{
			key: 'imageZoom',
			label: 'Image Zoom',
			description: 'Adds a gentle zoom effect to still images so the slideshow feels less static.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Enabled for still-photo slideshows.'
		},
		{
			key: 'imagePan',
			label: 'Image Pan',
			description: 'Pans across still images in a random direction while they are on screen.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Enable this instead of, or alongside, zoom for a stronger motion effect.'
		},
		{
			key: 'imageFill',
			label: 'Image Fill',
			description: 'Fills the available frame space and may crop the image instead of letterboxing it.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Enable it on landscape TVs to avoid black bars.'
		}
	] as const;

	const metadataToggleFields = [
		{
			key: 'showPhotoDate',
			label: 'Show Photo Date',
			description: 'Displays the date associated with the current asset in the metadata overlay.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Useful for travel or memory-focused albums.'
		},
		{
			key: 'showPhotoTimeAgo',
			label: 'Show Photo Time Ago',
			description: 'Adds a separate metadata line showing how long ago the asset was captured.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Example display: 14 years ago.'
		},
		{
			key: 'showImageDesc',
			label: 'Show Image Description',
			description: 'Displays the image description stored in Immich metadata when present.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Good for curated albums with captions.'
		},
		{
			key: 'showPeopleDesc',
			label: 'Show People',
			description: 'Shows the people identified on the current asset as a comma-separated list.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Helpful when using person-based filters or family frames.'
		},
		{
			key: 'showPeopleAge',
			label: 'Show People Age',
			description: 'Adds each person\'s age next to their name when Immich has a date of birth for them.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Example display: Jane (12).'
		},
		{
			key: 'showTagsDesc',
			label: 'Show Tags',
			description: 'Shows the tags attached to the current asset.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Useful when tags represent events, places, or collections.'
		},
		{
			key: 'showAlbumName',
			label: 'Show Album Name',
			description: 'Displays the album or albums that contain the current asset.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Helpful when multiple themed albums feed the same frame.'
		},
		{
			key: 'showImageLocation',
			label: 'Show Image Location',
			description: 'Displays the asset location using the configured location format.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Example display: Edmonton,Alberta,Canada.'
		}
	] as const;

	const frameUiToggleFields = [
		{
			key: 'showProgressBar',
			label: 'Show Progress Bar',
			description: 'Displays the slideshow progress bar at the bottom of the frame.',
			defaultValue: 'true',
			options: 'true or false.',
			example: 'Turn it off for a cleaner, poster-like presentation.'
		}
	] as const;

	const accountToggleFields = [
		{
			key: 'showMemories',
			label: 'Show Memories',
			description: 'Includes memory-style assets when Immich has them available for the account.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Use this for a "this day in previous years" style frame.'
		},
		{
			key: 'showFavorites',
			label: 'Show Favorites',
			description: 'Restricts the account to assets that are marked as favorites in Immich.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Enable it for a highlights-only frame.'
		},
		{
			key: 'showArchived',
			label: 'Show Archived',
			description: 'Includes archived assets in the slideshow results for this account.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Turn it on when archived content should still appear on the frame.'
		},
		{
			key: 'showVideos',
			label: 'Show Videos',
			description: 'Includes video assets in addition to still images for this account.',
			defaultValue: 'false',
			options: 'true or false.',
			example: 'Enable it when short clips should play in the slideshow.'
		}
	] as const;

	type RequiredGeneralIntField =
		| 'interval'
		| 'renewImagesDuration'
		| 'refreshAlbumPeopleInterval'
		| 'calendarLookaheadDays'
		| 'calendarMaxEvents';
	type RequiredGeneralFloatField = 'transitionDuration';
	type RequiredGeneralNumberField = RequiredGeneralIntField | RequiredGeneralFloatField;

	let adminSession: AdminAuthSessionDto | null = $state(null);
	let authLoading = $state(true);
	let pageLoading = $state(false);
	let fatalErrorMessage = $state('');
	let saveErrorMessage = $state('');
	let saveSuccessMessage = $state('');
	let savePending = $state(false);
	let draft: AdminSettingsResponseDto | null = $state(null);
	let savedSettingsState = $state('');
	let weatherApiKeyInput = $state('');
	let showWeatherApiKey = $state(false);
	let weatherApiKeyFieldError = $state('');
	let webcalendarsFieldError = $state('');
	let pendingGeneralNumberInputs = $state<Partial<Record<RequiredGeneralNumberField, string>>>({});
	let albumValidationState = $state<Record<string, AccountAlbumValidationState>>({});
	let albumValidationPending = $state<Record<string, boolean>>({});
	let albumValidationGeneration = $state<Record<string, number>>({});
	let albumValidationTimer: ReturnType<typeof setTimeout> | null = null;

	const serializedDraft = $derived(draft ? serializeEditableSettings(draft) : '');
	const trimmedWeatherApiKeyInput = $derived(weatherApiKeyInput.trim());
	const isDirty = $derived(
		Boolean(draft) &&
			(serializedDraft !== savedSettingsState || trimmedWeatherApiKeyInput.length > 0)
	);
	const widgetStackOrder = $derived.by(() =>
		draft ? normalizeWidgetStackOrder(draft.general.widgetStackOrder) : [...widgetStackDefaults]
	);
	const calendarNeedsFeed = $derived.by(
		() => Boolean(draft && draft.general.showCalendar && (draft.general.webcalendars?.length ?? 0) === 0)
	);
	const sortedAvailableTimeZones = $derived.by(() =>
		(draft?.availableTimeZones ?? []).slice().sort((a, b) => a.localeCompare(b))
	);

	const firstVideoAccountIndex = $derived.by(() => {
		if (!draft) {
			return -1;
		}

		return draft.accounts.findIndex((account) => account.showVideos);
	});

	function deepCloneSettings(settings: AdminSettingsResponseDto) {
		return JSON.parse(JSON.stringify(settings)) as AdminSettingsResponseDto;
	}

	function serializeEditableSettings(settings: AdminSettingsResponseDto) {
		return JSON.stringify({
			general: settings.general,
			customCss: settings.customCss,
			accounts: settings.accounts.map((account) => ({
				accountIdentifier: account.accountIdentifier,
				showMemories: account.showMemories,
				showFavorites: account.showFavorites,
				showArchived: account.showArchived,
				showVideos: account.showVideos,
				imagesFromDays: account.imagesFromDays,
				imagesFromDate: account.imagesFromDate,
				imagesUntilDate: account.imagesUntilDate,
				albums: account.albums,
				excludedAlbums: account.excludedAlbums,
				people: account.people,
				tags: account.tags,
				rating: account.rating
			}))
		});
	}

	function applyLoadedSettings(settings: AdminSettingsResponseDto) {
		const nextDraft = deepCloneSettings(settings);
		delete (nextDraft.general as unknown as { calendarDateFormat?: unknown }).calendarDateFormat;
		draft = nextDraft;
		savedSettingsState = serializeEditableSettings(nextDraft);
		weatherApiKeyInput = '';
		showWeatherApiKey = false;
		weatherApiKeyFieldError = '';
		webcalendarsFieldError = '';
		pendingGeneralNumberInputs = {};
	}

	function formatDateInput(value?: string | null) {
		return value ? value.slice(0, 10) : '';
	}

	function parseNullableInt(value: string) {
		const normalized = value.trim();
		if (!normalized) return null;
		const parsed = Number.parseInt(normalized, 10);
		return Number.isFinite(parsed) ? parsed : null;
	}

	function parseNullableDate(value: string) {
		const normalized = value.trim();
		return normalized.length ? normalized : null;
	}

	function parseRequiredInt(value: string, fallback: number, min = 0, max?: number) {
		const parsed = Number.parseInt(value, 10);
		if (!Number.isFinite(parsed)) {
			return fallback;
		}

		const minValue = Math.max(min, parsed);
		return max == null ? minValue : Math.min(max, minValue);
	}

	function parseRequiredFloat(value: string, fallback: number, min = 0) {
		const parsed = Number.parseFloat(value);
		if (!Number.isFinite(parsed)) {
			return fallback;
		}

		return Math.max(min, parsed);
	}

	function getRequiredGeneralNumberValue(key: RequiredGeneralNumberField) {
		if (!draft) {
			return '';
		}

		return pendingGeneralNumberInputs[key] ?? String(draft.general[key]);
	}

	function setPendingGeneralNumberInput(key: RequiredGeneralNumberField, value: string) {
		pendingGeneralNumberInputs = {
			...pendingGeneralNumberInputs,
			[key]: value
		};
		saveErrorMessage = '';
	}

	function clearPendingGeneralNumberInput(key: RequiredGeneralNumberField) {
		const nextInputs = { ...pendingGeneralNumberInputs };
		delete nextInputs[key];
		pendingGeneralNumberInputs = nextInputs;
	}

	function updateRequiredGeneralIntInput(
		key: RequiredGeneralIntField,
		value: string,
		min = 0,
		max?: number
	) {
		setPendingGeneralNumberInput(key, value);
		const normalized = value.trim();
		if (!normalized.length) {
			return;
		}

		const parsed = Number.parseInt(normalized, 10);
		if (!Number.isFinite(parsed)) {
			return;
		}

		const minValue = Math.max(min, parsed);
		updateGeneral(key, (max == null ? minValue : Math.min(max, minValue)) as AdminManagedGeneralSettings[typeof key]);
	}

	function updateRequiredGeneralFloatInput(
		key: RequiredGeneralFloatField,
		value: string,
		min = 0
	) {
		setPendingGeneralNumberInput(key, value);
		const normalized = value.trim();
		if (!normalized.length) {
			return;
		}

		const parsed = Number.parseFloat(normalized);
		if (!Number.isFinite(parsed)) {
			return;
		}

		updateGeneral(key, Math.max(min, parsed));
	}

	function commitRequiredGeneralIntInput(
		key: RequiredGeneralIntField,
		fallback: number,
		min = 0,
		max?: number
	) {
		if (!draft) {
			return;
		}

		const pendingValue = pendingGeneralNumberInputs[key];
		if (pendingValue == null) {
			return;
		}

		const nextValue = parseRequiredInt(pendingValue, fallback, min, max);
		updateGeneral(key, nextValue as AdminManagedGeneralSettings[typeof key]);
		clearPendingGeneralNumberInput(key);
	}

	function commitRequiredGeneralFloatInput(
		key: RequiredGeneralFloatField,
		fallback: number,
		min = 0
	) {
		if (!draft) {
			return;
		}

		const pendingValue = pendingGeneralNumberInputs[key];
		if (pendingValue == null) {
			return;
		}

		const nextValue = parseRequiredFloat(pendingValue, fallback, min);
		updateGeneral(key, nextValue);
		clearPendingGeneralNumberInput(key);
	}

	function isUuid(value: string) {
		return UUID_PATTERN.test(value);
	}

	function normalizeUuid(value: string) {
		return value.trim().toLowerCase();
	}

	function emptyAlbumValidationState(): AccountAlbumValidationState {
		return {
			albums: {},
			excludedAlbums: {}
		};
	}

	function getAlbumTokenStatuses(accountIdentifier: string, listKey: AlbumListKey) {
		return albumValidationState[accountIdentifier]?.[listKey] ?? {};
	}

	function hasAlbumValidationPending(accountIdentifier: string) {
		return albumValidationPending[accountIdentifier] ?? false;
	}

	function nextAlbumValidationGeneration(accountIdentifier: string) {
		const generation = (albumValidationGeneration[accountIdentifier] ?? 0) + 1;
		albumValidationGeneration = {
			...albumValidationGeneration,
			[accountIdentifier]: generation
		};
		return generation;
	}

	function isCurrentAlbumValidation(accountIdentifier: string, generation: number) {
		return albumValidationGeneration[accountIdentifier] === generation;
	}

	function albumValidationStatusFromResult(
		result: AdminAlbumValidationResultDto
	): AlbumTokenStatus {
		if (result.status === 'valid') {
			return {
				status: 'valid',
				label: 'Valid'
			};
		}

		if (result.status === 'notFoundOrNoAccess') {
			return {
				status: 'notFoundOrNoAccess',
				label: result.statusCode ? `${result.statusCode}` : 'Failed',
				message: result.message,
				statusCode: result.statusCode
			};
		}

		return {
			status: 'error',
			label: result.statusCode ? `Error ${result.statusCode}` : 'Error',
			message: result.message,
			statusCode: result.statusCode
		};
	}

	function mapAlbumValidationResults(results: AdminAlbumValidationResultDto[]) {
		return Object.fromEntries(
			results.map((result) => [normalizeUuid(result.albumId), albumValidationStatusFromResult(result)])
		);
	}

	function markAlbumValidationFailed(account: AdminManagedAccountSettings, message: string) {
		const toErrorMap = (values: string[]) =>
			Object.fromEntries(
				values.map((value) => [
					normalizeUuid(value),
					{
						status: 'error',
						label: 'Error',
						message
					} satisfies AlbumTokenStatus
				])
			);

		albumValidationState = {
			...albumValidationState,
			[account.accountIdentifier]: {
				albums: toErrorMap(account.albums),
				excludedAlbums: toErrorMap(account.excludedAlbums)
			}
		};
	}

	async function validateAccountAlbums(account: AdminManagedAccountSettings) {
		const accountIdentifier = account.accountIdentifier;
		const validationGeneration = nextAlbumValidationGeneration(accountIdentifier);
		const albums = account.albums.filter(isUuid).map(normalizeUuid);
		const excludedAlbums = account.excludedAlbums.filter(isUuid).map(normalizeUuid);

		if (albums.length === 0 && excludedAlbums.length === 0) {
			albumValidationState = {
				...albumValidationState,
				[accountIdentifier]: emptyAlbumValidationState()
			};
			albumValidationPending = {
				...albumValidationPending,
				[accountIdentifier]: false
			};
			return;
		}

		albumValidationPending = {
			...albumValidationPending,
			[accountIdentifier]: true
		};

		try {
			const result = await validateAdminAlbums({
				accountIdentifier,
				albums,
				excludedAlbums
			});

			if (!isCurrentAlbumValidation(accountIdentifier, validationGeneration)) {
				return;
			}

			albumValidationState = {
				...albumValidationState,
				[accountIdentifier]: {
					albums: mapAlbumValidationResults(result.albums),
					excludedAlbums: mapAlbumValidationResults(result.excludedAlbums)
				}
			};
		} catch (error) {
			if (!isCurrentAlbumValidation(accountIdentifier, validationGeneration)) {
				return;
			}

			console.warn('Failed to validate album UUIDs:', error);
			markAlbumValidationFailed(account, 'Album validation failed. Try again in a moment.');
		} finally {
			if (!isCurrentAlbumValidation(accountIdentifier, validationGeneration)) {
				return;
			}

			albumValidationPending = {
				...albumValidationPending,
				[accountIdentifier]: false
			};
		}
	}

	async function validateAllAlbumLists() {
		if (!draft || !adminSession?.isAuthenticated) {
			return;
		}

		await Promise.all(draft.accounts.map((account) => validateAccountAlbums(account)));
	}

	function scheduleAlbumValidation() {
		if (albumValidationTimer) {
			clearTimeout(albumValidationTimer);
		}

		albumValidationTimer = setTimeout(() => {
			void validateAllAlbumLists();
		}, 500);
	}

	function handleAlbumValuesChanged() {
		saveErrorMessage = '';
		scheduleAlbumValidation();
	}

	function updateGeneral<K extends keyof AdminManagedGeneralSettings>(
		key: K,
		value: AdminManagedGeneralSettings[K]
	) {
		if (!draft) return;

		const nextGeneral = {
			...draft.general,
			[key]: value
		};

		if (key === 'showPeopleDesc' && value === false) {
			nextGeneral.showPeopleAge = false;
		}

		draft = {
			...draft,
			general: nextGeneral
		};
		saveErrorMessage = '';
		if (key === 'showWeather' || key === 'showCalendar') {
			weatherApiKeyFieldError = '';
			webcalendarsFieldError = '';
		}
	}

	function isMetadataToggleDisabled(key: keyof AdminManagedGeneralSettings) {
		return key === 'showPeopleAge' && !(draft?.general.showPeopleDesc ?? true);
	}

	function getMetadataToggleDescription(field: (typeof metadataToggleFields)[number]) {
		if (field.key === 'showPeopleAge' && draft && !draft.general.showPeopleDesc) {
			return `${field.description} Enable Show People first to turn this on.`;
		}

		return field.description;
	}

	function updateCustomCss(value: string) {
		if (!draft) return;
		draft = {
			...draft,
			customCss: value
		};
		saveErrorMessage = '';
	}

	function updateAccount<K extends keyof AdminManagedAccountSettings>(
		accountIndex: number,
		key: K,
		value: AdminManagedAccountSettings[K]
	) {
		if (!draft) return;
		draft = {
			...draft,
			accounts: draft.accounts.map((account, index) =>
				index === accountIndex ? { ...account, [key]: value } : account
			)
		};
		saveErrorMessage = '';
	}

	function normalizeWidgetStackOrder(value?: string[] | null): WidgetKey[] {
		const ordered: WidgetKey[] = [];

		for (const widget of value ?? []) {
			const normalized = widget?.trim().toLowerCase();
			if (
				(normalized === 'clock' ||
					normalized === 'weather' ||
					normalized === 'metadata' ||
					normalized === 'calendar') &&
				!ordered.includes(normalized)
			) {
				ordered.push(normalized);
			}
		}

		for (const widget of widgetStackDefaults) {
			if (!ordered.includes(widget)) {
				ordered.push(widget);
			}
		}

		return ordered;
	}

	function moveWidgetInStack(widget: WidgetKey, direction: -1 | 1) {
		if (!draft) return;

		const nextOrder = [...normalizeWidgetStackOrder(draft.general.widgetStackOrder)];
		const currentIndex = nextOrder.indexOf(widget);
		if (currentIndex === -1) {
			return;
		}

		const targetIndex = currentIndex + direction;
		if (targetIndex < 0 || targetIndex >= nextOrder.length) {
			return;
		}

		[nextOrder[currentIndex], nextOrder[targetIndex]] = [
			nextOrder[targetIndex],
			nextOrder[currentIndex]
		];

		updateGeneral('widgetStackOrder', nextOrder);
	}

	function isUnauthorizedError(error: unknown) {
		return error instanceof FrameSessionApiError && error.status === 401;
	}

	function toggleAllHelp(expanded: boolean) {
		if (typeof window === 'undefined') {
			return;
		}

		window.dispatchEvent(
			new CustomEvent('immichframe-admin-help-toggle', {
				detail: { expanded }
			})
		);
	}

	async function initializePage() {
		authLoading = true;
		pageLoading = false;
		fatalErrorMessage = '';
		saveErrorMessage = '';
		saveSuccessMessage = '';

		try {
			const session = await getAdminAuthSession();
			adminSession = session;

			if (!session.isAuthenticated) {
				return;
			}

			pageLoading = true;
			const settings = await getAdminSettings();
			applyLoadedSettings(settings);
			scheduleAlbumValidation();
		} catch (error) {
			console.warn('Failed to initialize admin settings page:', error);
			fatalErrorMessage =
				'The admin settings page could not reach the server. Check that the backend is running and refresh.';
		} finally {
			authLoading = false;
			pageLoading = false;
		}
	}

	async function saveSettings() {
		if (!draft) return;

		if (draft.general.showCalendar && (draft.general.webcalendars?.length ?? 0) === 0) {
			webcalendarsFieldError = 'This field is required before the calendar widget can be enabled.';
			saveErrorMessage = 'Add at least one webcalendar before enabling the calendar widget.';
			saveSuccessMessage = '';
			document.getElementById('webcalendars-nested')?.focus();
			return;
		}

		if (draft.general.showWeather && !draft.weatherApiKeyConfigured && trimmedWeatherApiKeyInput.length === 0) {
			weatherApiKeyFieldError = 'This field is required before the weather widget can be enabled.';
			saveErrorMessage =
				'Add a weather API key before enabling the weather widget, or keep weather disabled.';
			saveSuccessMessage = '';
			document.getElementById('weatherApiKey-nested')?.focus();
			return;
		}

		savePending = true;
		saveErrorMessage = '';
		saveSuccessMessage = '';

		try {
			const updated = await updateAdminSettings({
				general: draft.general,
				customCss: draft.customCss,
				weatherApiKey: trimmedWeatherApiKeyInput.length > 0 ? trimmedWeatherApiKeyInput : undefined,
				accounts: draft.accounts.map((account) => ({
					accountIdentifier: account.accountIdentifier,
					showMemories: account.showMemories,
					showFavorites: account.showFavorites,
					showArchived: account.showArchived,
					showVideos: account.showVideos,
					imagesFromDays: account.imagesFromDays,
					imagesFromDate: account.imagesFromDate,
					imagesUntilDate: account.imagesUntilDate,
					albums: account.albums,
					excludedAlbums: account.excludedAlbums,
					people: account.people,
					tags: account.tags,
					rating: account.rating
				}))
			});

			applyLoadedSettings(updated);
			scheduleAlbumValidation();
			saveSuccessMessage =
				'Settings saved. Active frames were asked to refresh so they can pull the latest runtime configuration.';
		} catch (error) {
			if (isUnauthorizedError(error)) {
				adminSession = {
					isConfigured: adminSession?.isConfigured ?? true,
					isAuthenticated: false,
					username: null
				};
				saveErrorMessage = 'Your admin session expired. Sign in again from the dashboard.';
			} else if (error instanceof FrameSessionApiError) {
				saveErrorMessage = `Saving settings failed with status ${error.status}.`;
			} else {
				saveErrorMessage = 'Saving settings failed. Try again in a moment.';
			}
		} finally {
			savePending = false;
		}
	}

	async function handleLogout() {
		if (isDirty && typeof window !== 'undefined') {
			const shouldDiscard = window.confirm(
				'You have unsaved settings. Log out and discard those changes?'
			);

			if (!shouldDiscard) {
				return;
			}
		}

		try {
			await logoutAdmin();
		} catch (error) {
			if (!isUnauthorizedError(error)) {
				console.warn('Failed to log out admin session:', error);
			}
		}

		adminSession = {
			isConfigured: adminSession?.isConfigured ?? true,
			isAuthenticated: false,
			username: null
		};
	}

	$effect(() => {
		if (isDirty && saveSuccessMessage) {
			saveSuccessMessage = '';
		}
	});

	$effect(() => {
		if (!draft) {
			return;
		}

		if (
			!draft.general.showWeather ||
			draft.weatherApiKeyConfigured ||
			trimmedWeatherApiKeyInput.length > 0
		) {
			weatherApiKeyFieldError = '';
		}

		if (!draft.general.showCalendar || (draft.general.webcalendars?.length ?? 0) > 0) {
			webcalendarsFieldError = '';
		}
	});

	beforeNavigate((navigation) => {
		if (!isDirty || savePending || typeof window === 'undefined') {
			return;
		}

		if (navigation.type === 'leave' || navigation.to) {
			const shouldDiscard = window.confirm(
				'You have unsaved settings. Leave this page and discard them?'
			);

			if (!shouldDiscard) {
				navigation.cancel();
			}
		}
	});

	onMount(() => {
		const handleBeforeUnload = (event: BeforeUnloadEvent) => {
			if (!isDirty || savePending) {
				return;
			}

			event.preventDefault();
			event.returnValue = '';
		};

		window.addEventListener('beforeunload', handleBeforeUnload);
		void initializePage();

		return () => {
			window.removeEventListener('beforeunload', handleBeforeUnload);
			if (albumValidationTimer) {
				clearTimeout(albumValidationTimer);
			}
		};
	});
</script>

<svelte:head>
	<title>immichFrame Settings</title>
</svelte:head>

{#if fatalErrorMessage}
	<section class="min-h-dvh bg-[#10100e] px-4 py-6 text-stone-100 sm:px-6 lg:px-10">
		<div class="mx-auto max-w-3xl rounded-[2rem] border border-rose-400/25 bg-rose-400/10 px-6 py-8">
			<h1 class="text-3xl font-semibold tracking-tight">Admin settings are unavailable</h1>
			<p class="mt-4 text-sm text-rose-100/90">{fatalErrorMessage}</p>
		</div>
	</section>
{:else if authLoading}
	<section class="min-h-dvh bg-[radial-gradient(circle_at_top,_rgba(245,222,179,0.16),_transparent_34%),linear-gradient(180deg,#14130f_0%,#0f100d_48%,#0b0c09_100%)] px-4 py-6 text-stone-100 sm:px-6 lg:px-10">
		<div class="mx-auto max-w-3xl rounded-[2rem] border border-white/10 bg-white/5 px-6 py-12 text-center shadow-2xl backdrop-blur">
			Checking admin session...
		</div>
	</section>
{:else if !adminSession?.isConfigured}
	<section class="min-h-dvh bg-[radial-gradient(circle_at_top,_rgba(245,222,179,0.16),_transparent_34%),linear-gradient(180deg,#14130f_0%,#0f100d_48%,#0b0c09_100%)] px-4 py-6 text-stone-100 sm:px-6 lg:px-10">
		<div class="mx-auto flex max-w-3xl flex-col gap-5 rounded-[2rem] border border-white/10 bg-white/5 px-6 py-8 shadow-2xl backdrop-blur">
			<p class="text-xs uppercase tracking-[0.45em] text-[color:var(--primary-color)]">
				Admin Settings
			</p>
			<h1 class="text-3xl font-semibold tracking-tight">Admin access is not configured.</h1>
			<p class="text-sm text-stone-300">
				Add at least one IMMICHFRAME_AUTH_BASIC user and matching hash to your environment file, then restart the app.
			</p>
		</div>
	</section>
{:else if !adminSession.isAuthenticated}
	<section class="min-h-dvh bg-[radial-gradient(circle_at_top,_rgba(245,222,179,0.16),_transparent_34%),linear-gradient(180deg,#14130f_0%,#0f100d_48%,#0b0c09_100%)] px-4 py-6 text-stone-100 sm:px-6 lg:px-10">
		<div class="mx-auto flex max-w-3xl flex-col gap-5 rounded-[2rem] border border-white/10 bg-white/5 px-6 py-8 shadow-2xl backdrop-blur">
			<h1 class="text-3xl font-semibold tracking-tight">Sign in to manage runtime settings</h1>
			<p class="text-sm text-stone-300">
				Use the admin login page on the dashboard to establish a session before editing live configuration.
			</p>
			<div class="flex flex-wrap gap-3">
				<a
					class="inline-flex items-center gap-2 rounded-full border border-[color:var(--primary-color)]/40 bg-[color:var(--primary-color)]/15 px-5 py-3 text-sm font-medium text-[color:var(--primary-color)] transition hover:bg-[color:var(--primary-color)]/25"
					href="/admin"
				>
					<Icon path={mdiArrowLeft} title="Back to admin" size="1rem" />
					Go To Dashboard
				</a>
			</div>
			{#if saveErrorMessage}
				<div class="rounded-2xl border border-rose-400/25 bg-rose-400/10 px-4 py-3 text-sm text-rose-100">
					{saveErrorMessage}
				</div>
			{/if}
		</div>
	</section>
{:else}
	<section class="min-h-dvh bg-[radial-gradient(circle_at_top,_rgba(245,222,179,0.16),_transparent_34%),linear-gradient(180deg,#14130f_0%,#0f100d_48%,#0b0c09_100%)] text-stone-100">
		<div class="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-10">
			<header class="rounded-[2rem] border border-white/10 bg-white/5 px-5 py-6 shadow-2xl backdrop-blur sm:px-6">
				<div class="flex flex-col gap-5">
					<div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
						<div class="min-w-0">
							<p class="text-xs uppercase tracking-[0.45em] text-[color:var(--primary-color)]">
								Admin Settings
							</p>
							<h1 class="mt-3 text-3xl font-semibold tracking-tight sm:text-4xl">
								Runtime Configuration
							</h1>
							<p class="mt-3 max-w-3xl text-sm text-stone-300">
								Use this page to control what your frame shows, where each widget appears, and how the live display feels across desktop and mobile screens.
							</p>
						</div>

						<div class="flex flex-wrap items-center gap-3">
							<a
								class="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-4 py-2 text-sm text-stone-100 transition hover:bg-white/10"
								href="/admin"
							>
								<Icon path={mdiArrowLeft} title="Back to dashboard" size="1rem" />
								Dashboard
							</a>
							<span class="rounded-full border border-white/12 bg-black/25 px-4 py-2 text-sm text-stone-300">
								Signed in as {adminSession.username}
							</span>
							<button
								type="button"
								class="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-4 py-2 text-sm text-stone-100 transition hover:bg-white/10"
								onclick={() => void handleLogout()}
							>
								<Icon path={mdiLogout} title="Log out" size="1rem" />
								Log Out
							</button>
						</div>
					</div>

					<div class="flex flex-wrap gap-3">
						<button
							type="button"
							class="rounded-full border border-[#d9b865]/40 bg-[#6d5417]/15 px-4 py-2 text-sm text-[#f6d987] transition hover:bg-[#6d5417]/25"
							onclick={() => toggleAllHelp(true)}
						>
							Expand All Help
						</button>
						<button
							type="button"
							class="rounded-full border border-[#d9b865]/40 bg-[#6d5417]/15 px-4 py-2 text-sm text-[#f6d987] transition hover:bg-[#6d5417]/25"
							onclick={() => toggleAllHelp(false)}
						>
							Collapse All Help
						</button>
					</div>
				</div>
			</header>

			{#if pageLoading || !draft}
				<div class="rounded-[2rem] border border-white/10 bg-white/5 px-6 py-12 text-center text-stone-300">
					Loading runtime settings...
				</div>
			{:else}
				{#if saveErrorMessage}
					<div class="rounded-[2rem] border border-rose-400/25 bg-rose-400/10 px-5 py-4 text-sm text-rose-100">
						{saveErrorMessage}
					</div>
				{/if}

				{#if saveSuccessMessage}
					<div class="rounded-[2rem] border border-emerald-400/25 bg-emerald-400/10 px-5 py-4 text-sm text-emerald-100">
						{saveSuccessMessage}
					</div>
				{/if}

				<div class="space-y-6">
					<section class={sectionClass}>
						<div class="flex flex-col gap-1">
							<h2 class="text-2xl font-semibold tracking-tight">Image Properties</h2>
							<p class="text-sm text-stone-400">
								Control the slideshow pacing, layout, and motion.
							</p>
						</div>

						<div class="mt-6 grid gap-4 md:grid-cols-2">
							<div class="space-y-2">
								<SettingLabel
									fieldId="interval"
									label="Interval"
									description="How long each image stays visible before the slideshow advances."
									defaultValue="45 seconds"
									options="Any positive whole number of seconds."
									example="45"
								/>
								<input
									id="interval"
									class={inputClass}
									type="number"
									min="1"
									value={getRequiredGeneralNumberValue('interval')}
									oninput={(event) =>
										updateRequiredGeneralIntInput(
											'interval',
											(event.currentTarget as HTMLInputElement).value,
											1
										)}
									onblur={() => commitRequiredGeneralIntInput('interval', 45, 1)}
								/>
							</div>

							<div class="space-y-2">
								<SettingLabel
									fieldId="transitionDuration"
									label="Transition Duration"
									description="How long the transition animation lasts when the frame moves to the next asset."
									defaultValue="2 seconds"
									options="Zero or any positive decimal number of seconds."
									example="1.5"
								/>
								<input
									id="transitionDuration"
									class={inputClass}
									type="number"
									min="0"
									step="0.1"
									value={getRequiredGeneralNumberValue('transitionDuration')}
									oninput={(event) =>
										updateRequiredGeneralFloatInput(
											'transitionDuration',
											(event.currentTarget as HTMLInputElement).value,
											0
										)}
									onblur={() => commitRequiredGeneralFloatInput('transitionDuration', 2, 0)}
								/>
							</div>

							<div class="space-y-2">
								<SettingLabel
									fieldId="layout"
									label="Layout"
									description="Controls whether ImmichFrame can place two portrait images side by side or only show one asset at a time."
									defaultValue="splitview"
									options="single or splitview."
									example="splitview"
								/>
								<select
									id="layout"
									class={inputClass}
									value={draft.general.layout}
									onchange={(event) =>
										updateGeneral('layout', (event.currentTarget as HTMLSelectElement).value)}
								>
									<option value="splitview">splitview</option>
									<option value="single">single</option>
								</select>
							</div>
						</div>

						<div class="mt-6 grid gap-3 lg:grid-cols-3">
							{#each imageToggleFields as field}
								{@const fieldId = `general-${field.key}`}
								<div class={toggleCardClass}>
									<div class="min-w-0">
										<SettingLabel
											fieldId={fieldId}
											label={field.label}
											description={field.description}
											defaultValue={field.defaultValue}
											options={field.options}
											example={field.example}
										/>
									</div>
									<input
										id={fieldId}
										class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
										type="checkbox"
										checked={Boolean(draft.general[field.key])}
										onchange={(event) =>
											updateGeneral(
												field.key as keyof AdminManagedGeneralSettings,
												(event.currentTarget as HTMLInputElement).checked as never
											)}
									/>
								</div>
							{/each}
						</div>

					</section>

					<section class={sectionClass}>
						<div class="flex flex-col gap-1">
							<h2 class="text-2xl font-semibold tracking-tight">Filtering Properties</h2>
							<p class="text-sm text-stone-400">
								Runtime account filters determine which assets are eligible for the slideshow.
							</p>
						</div>

						<div class="mt-6 space-y-5">
							{#each draft.accounts as account, accountIndex (account.accountIndex)}
								<div class={subSectionClass}>
									<div class="flex flex-col gap-2">
										<div class="flex flex-wrap items-center gap-3">
											<h3 class="text-xl font-semibold">{account.accountLabel}</h3>
											<span class="rounded-full border border-white/10 bg-black/25 px-3 py-1 text-xs text-stone-300">
												{account.immichServerUrl}
											</span>
										</div>
									</div>

									<div class="mt-5 grid gap-3 lg:grid-cols-2">
										{#each accountToggleFields as field}
											{@const fieldId = `account-${accountIndex}-${field.key}`}
											<div class={toggleCardClass}>
												<div class="min-w-0">
													<SettingLabel
														fieldId={fieldId}
														label={field.label}
														description={field.description}
														defaultValue={field.defaultValue}
														options={field.options}
														example={field.example}
													/>
												</div>
												<input
													id={fieldId}
													class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
													type="checkbox"
													checked={Boolean(account[field.key])}
													onchange={(event) =>
														updateAccount(
															accountIndex,
															field.key as keyof AdminManagedAccountSettings,
															(event.currentTarget as HTMLInputElement).checked as never
														)}
												/>
											</div>
										{/each}
									</div>

									{#if account.showVideos && accountIndex === firstVideoAccountIndex}
										<div class={`mt-4 ${toggleCardClass}`}>
											<div class="min-w-0">
												<SettingLabel
													fieldId={`playAudio-account-${accountIndex}`}
													label="Play Audio"
													description="Allows audio tracks to play for video assets from any enabled account."
													defaultValue="false"
													options="true or false."
													example="Leave this off for silent digital-photo frames."
												/>
												<p class="mt-2 text-xs text-stone-500">
													This is a frame-wide setting, so changing it affects all accounts that include videos.
												</p>
											</div>
											<input
												id={`playAudio-account-${accountIndex}`}
												class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
												type="checkbox"
												checked={draft.general.playAudio}
												onchange={(event) =>
													updateGeneral(
														'playAudio',
														(event.currentTarget as HTMLInputElement).checked
													)}
											/>
										</div>
									{/if}

									<div class="mt-5 grid gap-4 md:grid-cols-2">
										<div class="space-y-2">
											<SettingLabel
												fieldId={`imagesFromDays-${accountIndex}`}
												label="Images From Days"
												description="Only include assets from the last X days for this account. When set, it is overridden by Images From Date."
												defaultValue="No limit"
												options="Any whole number of days, or leave blank."
												example="365"
											/>
											<input
												id={`imagesFromDays-${accountIndex}`}
												class={inputClass}
												type="number"
												min="0"
												value={account.imagesFromDays ?? ''}
												oninput={(event) =>
													updateAccount(
														accountIndex,
														'imagesFromDays',
														parseNullableInt((event.currentTarget as HTMLInputElement).value)
													)}
											/>
										</div>

										<div class="space-y-2">
											<SettingLabel
												fieldId={`rating-${accountIndex}`}
												label="Rating"
												description="Filters the account to assets with the exact star rating you choose."
												defaultValue="Any rating"
												options="Blank, or an exact rating from -1 through 5."
												example="5"
											/>
											<select
												id={`rating-${accountIndex}`}
												class={inputClass}
												value={account.rating == null ? '' : String(account.rating)}
												onchange={(event) =>
													updateAccount(
														accountIndex,
														'rating',
														parseNullableInt((event.currentTarget as HTMLSelectElement).value)
													)}
											>
												<option value="">Any rating</option>
												<option value="-1">-1</option>
												<option value="0">0</option>
												<option value="1">1</option>
												<option value="2">2</option>
												<option value="3">3</option>
												<option value="4">4</option>
												<option value="5">5</option>
											</select>
										</div>

										<div class="space-y-2">
											<SettingLabel
												fieldId={`imagesFromDate-${accountIndex}`}
												label="Images From Date"
												description="Only include assets on or after this date for the account."
												defaultValue="No lower date bound"
												options="Any valid date, or leave blank."
												example="2026-01-01"
											/>
											<input
												id={`imagesFromDate-${accountIndex}`}
												class={inputClass}
												type="date"
												value={formatDateInput(account.imagesFromDate)}
												onchange={(event) =>
													updateAccount(
														accountIndex,
														'imagesFromDate',
														parseNullableDate((event.currentTarget as HTMLInputElement).value)
													)}
											/>
										</div>

										<div class="space-y-2">
											<SettingLabel
												fieldId={`imagesUntilDate-${accountIndex}`}
												label="Images Until Date"
												description="Only include assets on or before this date for the account."
												defaultValue="No upper date bound"
												options="Any valid date, or leave blank."
												example="2026-12-31"
											/>
											<input
												id={`imagesUntilDate-${accountIndex}`}
												class={inputClass}
												type="date"
												value={formatDateInput(account.imagesUntilDate)}
												onchange={(event) =>
													updateAccount(
														accountIndex,
														'imagesUntilDate',
														parseNullableDate((event.currentTarget as HTMLInputElement).value)
													)}
											/>
										</div>
									</div>

									<div class="mt-5 grid gap-4 xl:grid-cols-2">
										<div class="space-y-2">
											<SettingLabel
												label="Albums"
												description="Restrict this account to one or more album UUIDs."
												defaultValue="No album restriction"
												options="One or more UUIDs."
												example="00000000-0000-0000-0000-000000000001"
											/>
											<TokenListEditor
												id={`albums-${accountIndex}`}
												bind:values={draft.accounts[accountIndex].albums}
												placeholder="Add an album UUID"
												emptyState="No album UUIDs have been added."
												validator={isUuid}
												invalidMessage="Album values must be valid UUIDs."
												normalize={normalizeUuid}
												tokenStatuses={getAlbumTokenStatuses(account.accountIdentifier, 'albums')}
												enableSelection={true}
												onValuesChanged={handleAlbumValuesChanged}
											/>
											{#if hasAlbumValidationPending(account.accountIdentifier)}
												<p class="text-xs text-stone-400">Checking album UUIDs...</p>
											{/if}
										</div>

										<div class="space-y-2">
											<SettingLabel
												label="Excluded Albums"
												description="Exclude any assets that belong to these album UUIDs."
												defaultValue="No excluded albums"
												options="One or more UUIDs."
												example="00000000-0000-0000-0000-000000000002"
											/>
											<TokenListEditor
												id={`excluded-albums-${accountIndex}`}
												bind:values={draft.accounts[accountIndex].excludedAlbums}
												placeholder="Add an excluded album UUID"
												emptyState="No excluded album UUIDs have been added."
												validator={isUuid}
												invalidMessage="Excluded album values must be valid UUIDs."
												normalize={normalizeUuid}
												tokenStatuses={getAlbumTokenStatuses(
													account.accountIdentifier,
													'excludedAlbums'
												)}
												enableSelection={true}
												onValuesChanged={handleAlbumValuesChanged}
											/>
											{#if hasAlbumValidationPending(account.accountIdentifier)}
												<p class="text-xs text-stone-400">Checking excluded album UUIDs...</p>
											{/if}
										</div>

										<div class="space-y-2">
											<SettingLabel
												label="People"
												description="Restrict this account to assets that match one or more person UUIDs."
												defaultValue="No person restriction"
												options="One or more UUIDs."
												example="00000000-0000-0000-0000-000000000003"
											/>
											<TokenListEditor
												id={`people-${accountIndex}`}
												bind:values={draft.accounts[accountIndex].people}
												placeholder="Add a person UUID"
												emptyState="No people UUIDs have been added."
												validator={isUuid}
												invalidMessage="People values must be valid UUIDs."
												normalize={normalizeUuid}
											/>
										</div>

										<div class="space-y-2">
											<SettingLabel
												label="Tags"
												description="Restrict this account to one or more exact hierarchical tag values."
												defaultValue="No tag restriction"
												options="Any exact tag path."
												example="Travel/Europe"
											/>
											<TokenListEditor
												id={`tags-${accountIndex}`}
												bind:values={draft.accounts[accountIndex].tags}
												placeholder="Add a tag path"
												emptyState="No tag filters have been added."
											/>
										</div>
									</div>
								</div>
							{/each}
						</div>
					</section>

					<section class={sectionClass}>
						<div class="flex flex-col gap-1">
							<h2 class="text-2xl font-semibold tracking-tight">Frame UI Properties</h2>
							<p class="text-sm text-stone-400">
								Set the global widget appearance, shared colors, and base sizing used by the frame interface.
							</p>
						</div>

						<div class="mt-6 grid gap-3 lg:grid-cols-3">
							{#each frameUiToggleFields as field}
								{@const fieldId = `frame-ui-${field.key}`}
								<div class={toggleCardClass}>
									<div class="min-w-0">
										<SettingLabel
											fieldId={fieldId}
											label={field.label}
											description={field.description}
											defaultValue={field.defaultValue}
											options={field.options}
											example={field.example}
										/>
									</div>
									<input
										id={fieldId}
										class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
										type="checkbox"
										checked={Boolean(draft.general[field.key])}
										onchange={(event) =>
											updateGeneral(
												field.key as keyof AdminManagedGeneralSettings,
												(event.currentTarget as HTMLInputElement).checked as never
											)}
									/>
								</div>
							{/each}
						</div>

						<div class="mt-6 grid gap-4 xl:grid-cols-2">
							<div class="space-y-2">
								<SettingLabel
									label="Primary Color"
									description="Sets the primary accent color used by the frame UI and admin pages."
									defaultValue="#f5deb3"
									options="Hex colors such as #RRGGBB or #RRGGBBAA."
									example="#f5deb3"
								/>
								<ColorInput bind:value={draft.general.primaryColor} fallback="#f5deb3" />
							</div>

							<div class="space-y-2">
								<SettingLabel
									label="Secondary Color"
									description="Sets the backing color used by solid and transition overlay styles."
									defaultValue="#000000"
									options="Hex colors such as #RRGGBB or #RRGGBBAA."
									example="#000000"
								/>
								<ColorInput bind:value={draft.general.secondaryColor} fallback="#000000" />
							</div>

							<div class="space-y-2">
								<SettingLabel
									fieldId="style"
									label="Global Widget Style"
									description="Sets the default appearance used by all widgets unless a widget-specific style override is set."
									defaultValue="none"
									options="none, solid, transition, or blur."
									example="blur"
								/>
								<select
									id="style"
									class={inputClass}
									value={draft.general.style}
									onchange={(event) =>
										updateGeneral('style', (event.currentTarget as HTMLSelectElement).value)}
								>
									<option value="none">none</option>
									<option value="solid">solid</option>
									<option value="transition">transition</option>
									<option value="blur">blur</option>
								</select>
							</div>

							<div class="space-y-2">
								<SettingLabel
									label="Global Font Size"
									description="Sets the root font size used by the frame UI. This accepts standard CSS font-size values."
									defaultValue="17px"
									options="Preset CSS sizes or a custom CSS size value."
									example="18px"
								/>
								<PresetSelect
									bind:value={draft.general.baseFontSize}
									options={baseFontSizeOptions}
									allowEmpty
									emptyLabel="Use app default"
									customPlaceholder="Enter a custom CSS font size"
								/>
							</div>
						</div>

						<div class="mt-8 grid gap-5 xl:grid-cols-2">
							<div class={subSectionClass}>
								<div class="flex flex-col gap-1">
									<h3 class="text-xl font-semibold">Metadata</h3>
									<p class="text-sm text-stone-400">
										Choose which metadata fields appear on the frame and how they are arranged.
									</p>
								</div>

								<div class="mt-5 space-y-5">
									<div class="space-y-4">
										<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Properties</p>
										<div class={toggleCardClass}>
											<div class="min-w-0">
												<SettingLabel
													fieldId="showMetadata-nested"
													label="Show Metadata"
													description="Displays the metadata stack on the frame."
													defaultValue="true"
													options="true or false."
													example="Disable it for a photo-only presentation."
												/>
											</div>
											<input
												id="showMetadata-nested"
												class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
												type="checkbox"
												checked={draft.general.showMetadata}
												onchange={(event) =>
													updateGeneral('showMetadata', (event.currentTarget as HTMLInputElement).checked)}
											/>
										</div>

										{#if draft.general.showMetadata}
											<div class="grid gap-4 md:grid-cols-2">
												<div class="space-y-2">
													<SettingLabel
														label="Photo Date Format"
														description="Formats the captured date shown for each asset. This uses date-fns format tokens."
														defaultValue="MM/dd/yyyy"
														options="Preset date-fns formats or a custom date-fns format string."
														example="MMM d, yyyy"
													/>
													<PresetSelect
														bind:value={draft.general.photoDateFormat}
														options={photoDateFormatOptions}
														customPlaceholder="Enter a custom photo date format"
													/>
												</div>

												<div class="space-y-2">
													<SettingLabel
														label="Image Location Format"
														description="Controls which location parts appear when image locations are shown."
														defaultValue="City,State,Country"
														options="Preset combinations or a custom token order."
														example="City,Country"
													/>
													<PresetSelect
														bind:value={draft.general.imageLocationFormat}
														options={imageLocationFormatOptions}
														customPlaceholder="Enter a custom location format"
													/>
												</div>
											</div>

											<div class="grid gap-3 lg:grid-cols-2">
												{#each metadataToggleFields as field}
													{@const fieldId = `metadata-nested-${field.key}`}
													<div class={toggleCardClass}>
														<div class="min-w-0">
															<SettingLabel
																fieldId={fieldId}
																label={field.label}
																description={getMetadataToggleDescription(field)}
																defaultValue={field.defaultValue}
																options={field.options}
																example={field.example}
															/>
														</div>
														<input
															id={fieldId}
															class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
															type="checkbox"
															checked={Boolean(draft.general[field.key])}
															disabled={isMetadataToggleDisabled(
																field.key as keyof AdminManagedGeneralSettings
															)}
															onchange={(event) =>
																updateGeneral(
																	field.key as keyof AdminManagedGeneralSettings,
																	(event.currentTarget as HTMLInputElement).checked as never
																)}
														/>
													</div>
												{/each}
											</div>
										{/if}
									</div>

									{#if draft.general.showMetadata}
										<div class="space-y-4">
											<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Size And Layout</p>
											<div class="grid gap-4 md:grid-cols-3">
												<div class="space-y-2">
													<SettingLabel
														label="Corner Position"
														description="Sets the corner used by the metadata stack."
														defaultValue="bottom-right"
														options="top-left, top-right, bottom-left, or bottom-right."
														example="bottom-left"
													/>
													<select
														class={inputClass}
														value={draft.general.metadataPosition}
														onchange={(event) =>
															updateGeneral(
																'metadataPosition',
																(event.currentTarget as HTMLSelectElement).value
															)}
													>
														{#each widgetPositionOptions as option}
															<option value={option.value}>{option.label}</option>
														{/each}
													</select>
												</div>

												<div class="space-y-2">
													<SettingLabel
														label="Widget Font Size"
														description="Overrides the global font size only for the metadata stack."
														defaultValue="Uses global font size"
														options="Preset CSS sizes or a custom CSS font-size value."
														example="large"
													/>
													<PresetSelect
														bind:value={draft.general.metadataFontSize}
														options={baseFontSizeOptions}
														allowEmpty
														emptyLabel="Use global font size"
														customPlaceholder="Enter a custom CSS font size"
													/>
												</div>

												<div class="space-y-2">
													<SettingLabel
														label="Widget Style"
														description="Overrides the global widget style only for the metadata stack."
														defaultValue="Uses global widget style"
														options="none, solid, transition, blur, or blank to inherit the global style."
														example="blur"
													/>
													<PresetSelect
														bind:value={draft.general.metadataStyle}
														options={widgetStyleOptions}
														allowEmpty
														allowCustom={false}
														emptyLabel="Use global widget style"
													/>
												</div>
											</div>
										</div>
									{/if}
								</div>
							</div>

							<div class={subSectionClass}>
								<div class="flex flex-col gap-1">
									<h3 class="text-xl font-semibold">Clock</h3>
									<p class="text-sm text-stone-400">
										Turn the clock on or off, then tune its formatting, size, and placement.
									</p>
								</div>

								<div class="mt-5 space-y-5">
									<div class="space-y-4">
										<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Properties</p>
										<div class={toggleCardClass}>
											<div class="min-w-0">
												<SettingLabel
													fieldId="showClock-nested"
													label="Show Clock"
													description="Displays the time and clock date overlay on the frame."
													defaultValue="true"
													options="true or false."
													example="Disable it for an art-only frame."
												/>
											</div>
											<input
												id="showClock-nested"
												class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
												type="checkbox"
												checked={draft.general.showClock}
												onchange={(event) =>
													updateGeneral('showClock', (event.currentTarget as HTMLInputElement).checked)}
											/>
										</div>

										{#if draft.general.showClock}
											<div class="grid gap-4 md:grid-cols-2">
												<div class="space-y-2">
													<SettingLabel
														label="Clock Format"
														description="Formats the time shown by the clock overlay. This uses date-fns format tokens."
														defaultValue="hh:mm"
														options="Preset date-fns time formats or a custom date-fns format string."
														example="h:mm a"
													/>
													<PresetSelect
														bind:value={draft.general.clockFormat}
														options={clockFormatOptions}
														customPlaceholder="Enter a custom clock format"
													/>
												</div>

												<div class="space-y-2">
													<SettingLabel
														label="Clock Date Format"
														description="Formats the date shown with the clock overlay. This uses date-fns format tokens."
														defaultValue="eee, MMM d"
														options="Preset date-fns date formats or a custom date-fns format string."
														example="EEEE, MMMM d"
													/>
													<PresetSelect
														bind:value={draft.general.clockDateFormat}
														options={clockDateFormatOptions}
														customPlaceholder="Enter a custom clock date format"
													/>
												</div>
											</div>
										{/if}
									</div>

									{#if draft.general.showClock}
										<div class="space-y-4">
											<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Size And Layout</p>
											<div class="grid gap-4 md:grid-cols-3">
												<div class="space-y-2">
													<SettingLabel
														label="Corner Position"
														description="Sets the corner used by the clock widget."
														defaultValue="bottom-left"
														options="top-left, top-right, bottom-left, or bottom-right."
														example="top-left"
													/>
													<select
														class={inputClass}
														value={draft.general.clockPosition}
														onchange={(event) =>
															updateGeneral(
																'clockPosition',
																(event.currentTarget as HTMLSelectElement).value
															)}
													>
														{#each widgetPositionOptions as option}
															<option value={option.value}>{option.label}</option>
														{/each}
													</select>
												</div>

												<div class="space-y-2">
													<SettingLabel
														label="Widget Font Size"
														description="Overrides the global font size only for the clock widget."
														defaultValue="Uses global font size"
														options="Preset CSS sizes or a custom CSS font-size value."
														example="xx-large"
													/>
													<PresetSelect
														bind:value={draft.general.clockFontSize}
														options={baseFontSizeOptions}
														allowEmpty
														emptyLabel="Use global font size"
														customPlaceholder="Enter a custom CSS font size"
													/>
												</div>

												<div class="space-y-2">
													<SettingLabel
														label="Widget Style"
														description="Overrides the global widget style only for the clock."
														defaultValue="Uses global widget style"
														options="none, solid, transition, blur, or blank to inherit the global style."
														example="solid"
													/>
													<PresetSelect
														bind:value={draft.general.clockStyle}
														options={widgetStyleOptions}
														allowEmpty
														allowCustom={false}
														emptyLabel="Use global widget style"
													/>
												</div>
											</div>
										</div>
									{/if}
								</div>
							</div>
						</div>

						<div class="mt-5 grid gap-5 xl:grid-cols-2">
							<div class={`min-w-0 ${subSectionClass}`}>
								<div class="flex flex-col gap-1">
									<h3 class="text-xl font-semibold">Weather</h3>
									<p class="text-sm text-stone-400">
										Weather can run independently from the clock and requires an API key whenever it is enabled.
									</p>
								</div>

								<div class="mt-5 space-y-5">
									<div class="space-y-4">
										<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Properties</p>
										<div class={toggleCardClass}>
											<div class="min-w-0">
												<SettingLabel
													fieldId="showWeather-nested"
													label="Show Weather"
													description="Displays the weather block on the frame. A weather API key must already be configured before this can be enabled."
													defaultValue="true"
													options="true or false."
													example="Disable it for frames that should only show photos and metadata."
												/>
											</div>
											<input
												id="showWeather-nested"
												class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
												type="checkbox"
												checked={draft.general.showWeather}
												onchange={(event) =>
													updateGeneral('showWeather', (event.currentTarget as HTMLInputElement).checked)}
											/>
										</div>

										{#if draft.general.showWeather}
										<div class="space-y-2">
											<SettingLabel
												fieldId="weatherApiKey-nested"
												label="Weather API Key"
												description="Write-only OpenWeatherMap API key used for weather requests. Leave it blank to keep the currently configured key unchanged."
												defaultValue="Blank"
												options="Any valid OpenWeatherMap API key."
												example="abc123..."
											/>
											<div class="relative">
												<input
													id="weatherApiKey-nested"
													class={`${inputClass} pr-14 ${weatherApiKeyFieldError ? 'border-rose-400/70 focus:border-rose-300' : ''}`}
													type={showWeatherApiKey ? 'text' : 'password'}
													value={weatherApiKeyInput}
													placeholder={draft.weatherApiKeyConfigured
														? 'Weather API key already configured'
														: 'Enter a weather API key'}
													oninput={(event) => {
														weatherApiKeyInput = (event.currentTarget as HTMLInputElement).value;
														saveErrorMessage = '';
													}}
												/>
												<button
													class="absolute inset-y-0 right-0 inline-flex items-center justify-center px-4 text-stone-300 transition hover:text-stone-100"
													type="button"
													aria-label={showWeatherApiKey ? 'Hide weather API key' : 'Show weather API key'}
													aria-pressed={showWeatherApiKey}
													onclick={() => {
														showWeatherApiKey = !showWeatherApiKey;
													}}
												>
													<Icon
														path={showWeatherApiKey ? mdiEyeOffOutline : mdiEyeOutline}
														title={showWeatherApiKey ? 'Hide weather API key' : 'Show weather API key'}
														size="1.1rem"
													/>
												</button>
											</div>
											<p class="text-xs text-stone-500">
												{#if draft.weatherApiKeyConfigured}
													A weather API key is already configured. Saving a new value replaces it; leaving this blank keeps the current key.
												{:else}
													No weather API key is configured yet. Add one before turning on the weather widget.
												{/if}
											</p>
											{#if weatherApiKeyFieldError}
												<p class="text-xs text-rose-300">{weatherApiKeyFieldError}</p>
											{/if}
										</div>

										<div class="grid gap-4 md:grid-cols-2">
											<div class="space-y-2">
												<SettingLabel
													fieldId="unitSystem-nested"
													label="Unit System"
													description="Controls whether temperatures are returned in imperial or metric units."
													defaultValue="imperial"
													options="imperial or metric."
													example="metric"
												/>
												<select
													id="unitSystem-nested"
													class={inputClass}
													value={draft.general.unitSystem ?? 'imperial'}
													onchange={(event) =>
														updateGeneral(
															'unitSystem',
															(event.currentTarget as HTMLSelectElement).value
														)}
												>
													<option value="imperial">imperial</option>
													<option value="metric">metric</option>
												</select>
											</div>

											<div class="space-y-2">
												<SettingLabel
													fieldId="language-nested"
													label="Language"
													description="Two-letter ISO language code used for the weather description text and locale-sensitive formatting."
													defaultValue="en"
													options="A two-letter ISO code such as en, fr, or de."
													example="en"
												/>
												<input
													id="language-nested"
													class={inputClass}
													type="text"
													value={draft.general.language}
													oninput={(event) =>
														updateGeneral(
															'language',
															(event.currentTarget as HTMLInputElement).value.toLowerCase()
														)}
												/>
											</div>

											<div class="space-y-2">
												<SettingLabel
													fieldId="weatherLatLong-nested"
													label="Weather Latitude / Longitude"
													description="Sets the location used for weather requests."
													defaultValue="40.730610,-73.935242"
													options="Latitude and longitude as comma-separated decimals."
													example="53.5461,-113.4938"
												/>
												<input
													id="weatherLatLong-nested"
													class={inputClass}
													type="text"
													value={draft.general.weatherLatLong ?? ''}
													oninput={(event) =>
														updateGeneral(
															'weatherLatLong',
															(event.currentTarget as HTMLInputElement).value
														)}
												/>
											</div>

											<div class="space-y-2 md:col-span-2">
												<SettingLabel
													fieldId="weatherIconUrl-nested"
													label="Weather Icon URL"
													description={"Template URL used to load the weather icon. Use {IconId} where the weather provider should insert the icon code."}
													defaultValue={"https://openweathermap.org/img/wn/{IconId}.png"}
													options={"Any URL template containing {IconId}."}
													example={"https://openweathermap.org/img/wn/{IconId}.png"}
												/>
												<input
													id="weatherIconUrl-nested"
													class={inputClass}
													type="text"
													value={draft.general.weatherIconUrl ?? ''}
													oninput={(event) =>
														updateGeneral(
															'weatherIconUrl',
															(event.currentTarget as HTMLInputElement).value
														)}
												/>
											</div>
										</div>

										<div class="grid gap-3 lg:grid-cols-2">
											<div class={toggleCardClass}>
												<div class="min-w-0">
													<SettingLabel
														fieldId="showWeatherLocation-nested"
														label="Show Weather Location"
														description="Displays the weather location text next to the temperature."
														defaultValue="true"
														options="true or false."
														example="Disable it if the location is already obvious."
													/>
												</div>
												<input
													id="showWeatherLocation-nested"
													class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
													type="checkbox"
													checked={draft.general.showWeatherLocation}
													onchange={(event) =>
														updateGeneral(
															'showWeatherLocation',
															(event.currentTarget as HTMLInputElement).checked
														)}
												/>
											</div>

											<div class={toggleCardClass}>
												<div class="min-w-0">
													<SettingLabel
														fieldId="showWeatherDescription-nested"
														label="Show Weather Description"
														description="Displays the text description returned by the weather provider, such as cloudy or light rain."
														defaultValue="true"
														options="true or false."
														example="Disable it if you only want the icon and temperature."
													/>
												</div>
												<input
													id="showWeatherDescription-nested"
													class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
													type="checkbox"
													checked={draft.general.showWeatherDescription}
													onchange={(event) =>
														updateGeneral(
															'showWeatherDescription',
															(event.currentTarget as HTMLInputElement).checked
														)}
												/>
											</div>
										</div>
										{/if}
									</div>

									{#if draft.general.showWeather}
									<div class="space-y-4">
										<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Size And Layout</p>
										<div class="grid gap-4 md:grid-cols-3">
											<div class="space-y-2">
												<SettingLabel
													label="Corner Position"
													description="Sets the corner used by the weather widget."
													defaultValue="bottom-left"
													options="top-left, top-right, bottom-left, or bottom-right."
													example="bottom-right"
												/>
												<select
													class={inputClass}
													value={draft.general.weatherPosition}
													onchange={(event) =>
														updateGeneral(
															'weatherPosition',
															(event.currentTarget as HTMLSelectElement).value
														)}
												>
													{#each widgetPositionOptions as option}
														<option value={option.value}>{option.label}</option>
													{/each}
												</select>
											</div>

											<div class="space-y-2">
												<SettingLabel
													label="Widget Font Size"
													description="Overrides the global font size only for the weather widget."
													defaultValue="Uses global font size"
													options="Preset CSS sizes or a custom CSS font-size value."
													example="large"
												/>
												<PresetSelect
													bind:value={draft.general.weatherFontSize}
													options={baseFontSizeOptions}
													allowEmpty
													emptyLabel="Use global font size"
													customPlaceholder="Enter a custom CSS font size"
												/>
											</div>

											<div class="space-y-2">
												<SettingLabel
													label="Widget Style"
													description="Overrides the global widget style only for the weather widget."
													defaultValue="Uses global widget style"
													options="none, solid, transition, blur, or blank to inherit the global style."
													example="transition"
												/>
												<PresetSelect
													bind:value={draft.general.weatherStyle}
													options={widgetStyleOptions}
													allowEmpty
													allowCustom={false}
													emptyLabel="Use global widget style"
												/>
											</div>
										</div>
									</div>
									{/if}
								</div>
							</div>

							<div class={`min-w-0 ${subSectionClass}`}>
								<div class="flex flex-col gap-1">
									<h3 class="text-xl font-semibold">Calendar</h3>
									<p class="text-sm text-stone-400">
										Add calendar feeds, choose its corner, and match its sizing to the rest of the frame.
									</p>
								</div>

								<div class="mt-5 space-y-5">
									<div class="space-y-4">
										<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Properties</p>
										<div class={toggleCardClass}>
											<div class="min-w-0">
												<SettingLabel
													fieldId="showCalendar-nested"
													label="Show Calendar"
													description="Displays the calendar widget on the frame. At least one calendar feed is required before the setting can be saved."
													defaultValue="true"
													options="true or false."
													example="Disable it for a simpler photo-only layout."
												/>
											</div>
											<input
												id="showCalendar-nested"
												class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
												type="checkbox"
												checked={draft.general.showCalendar}
												onchange={(event) =>
													updateGeneral('showCalendar', (event.currentTarget as HTMLInputElement).checked)}
											/>
										</div>

										{#if draft.general.showCalendar}
										<div class="space-y-2">
												<SettingLabel
													label="Webcalendars"
													description="A list of calendar feed URLs ending in .ics. Basic-auth URLs are supported when needed."
												defaultValue="No calendars"
												options="One or more calendar feed URLs."
												example="https://calendar.example.com/public/basic.ics"
											/>
											<TokenListEditor
												id="webcalendars-nested"
												bind:values={draft.general.webcalendars}
												placeholder="Add a calendar feed URL"
												emptyState="No calendar feeds have been added."
												inputType="url"
												hasError={Boolean(webcalendarsFieldError)}
												errorMessage={webcalendarsFieldError}
											/>
											{#if calendarNeedsFeed}
												<p class="text-xs text-amber-300">
													Add at least one calendar feed while Show Calendar is enabled.
												</p>
											{/if}
										</div>
										{/if}

										{#if draft.general.showCalendar}
										<div class="space-y-2">
											<SettingLabel
												fieldId="calendarTimeZone-nested"
												label="Calendar Timezone"
												description="Choose which timezone defines calendar event days and display times. Calendar events are shown as time-only ranges using the selected clock format."
												defaultValue="Use server timezone"
												options="Any available IANA timezone identifier."
												example="America/Edmonton"
											/>
											<select
												id="calendarTimeZone-nested"
												class={inputClass}
												value={draft.general.calendarTimeZone ?? ''}
												onchange={(event) =>
													updateGeneral(
														'calendarTimeZone',
														(event.currentTarget as HTMLSelectElement).value || null
													)}
											>
												<option value="">
													Use server timezone ({draft.serverTimeZone})
												</option>
												{#each sortedAvailableTimeZones as timeZone}
													<option value={timeZone}>{timeZone}</option>
												{/each}
											</select>
										</div>

										<div class="grid gap-4 md:grid-cols-2">
											<div class="space-y-2">
												<SettingLabel
													fieldId="calendarLookaheadDays-nested"
													label="Lookahead Days"
													description="Shows events from today plus this many additional days."
													defaultValue="0"
													options="Whole number from 0 to 7."
													example="1"
												/>
												<input
													id="calendarLookaheadDays-nested"
													class={inputClass}
													type="number"
													min="0"
													max="7"
													value={getRequiredGeneralNumberValue('calendarLookaheadDays')}
													oninput={(event) =>
														updateRequiredGeneralIntInput(
															'calendarLookaheadDays',
															(event.currentTarget as HTMLInputElement).value,
															0,
															7
														)}
													onblur={() => commitRequiredGeneralIntInput('calendarLookaheadDays', 0, 0, 7)}
												/>
											</div>

											<div class="space-y-2">
												<SettingLabel
													fieldId="calendarMaxEvents-nested"
													label="Max Events"
													description="Limits how many calendar events the frame receives at once."
													defaultValue="5"
													options="Whole number from 1 to 10."
													example="5"
												/>
												<input
													id="calendarMaxEvents-nested"
													class={inputClass}
													type="number"
													min="1"
													max="10"
													value={getRequiredGeneralNumberValue('calendarMaxEvents')}
													oninput={(event) =>
														updateRequiredGeneralIntInput(
															'calendarMaxEvents',
															(event.currentTarget as HTMLInputElement).value,
															1,
															10
														)}
													onblur={() => commitRequiredGeneralIntInput('calendarMaxEvents', 5, 1, 10)}
												/>
											</div>
										</div>

										<div class="space-y-2">
											<SettingLabel
												label="Sort Direction"
												description="Controls whether calendar events are ordered earliest-first or latest-first."
												defaultValue="Ascending"
												options="ascending or descending."
												example="descending"
											/>
											<select
												class={inputClass}
												value={draft.general.calendarSortDirection ?? 'ascending'}
												onchange={(event) =>
													updateGeneral(
														'calendarSortDirection',
														(event.currentTarget as HTMLSelectElement).value
													)}
											>
												{#each calendarSortDirectionOptions as option}
													<option value={option.value}>{option.label}</option>
												{/each}
											</select>
										</div>
										{/if}
									</div>

									{#if draft.general.showCalendar}
									<div class="space-y-4">
										<p class="text-xs uppercase tracking-[0.3em] text-stone-500">Widget Size And Layout</p>
										<div class="grid gap-4 md:grid-cols-3">
											<div class="space-y-2">
												<SettingLabel
													label="Corner Position"
													description="Sets the corner used by the calendar widget."
													defaultValue="top-right"
													options="top-left, top-right, bottom-left, or bottom-right."
													example="top-left"
												/>
												<select
													class={inputClass}
													value={draft.general.calendarPosition}
													onchange={(event) =>
														updateGeneral(
															'calendarPosition',
															(event.currentTarget as HTMLSelectElement).value
														)}
												>
													{#each widgetPositionOptions as option}
														<option value={option.value}>{option.label}</option>
													{/each}
												</select>
											</div>

											<div class="space-y-2">
												<SettingLabel
													label="Widget Font Size"
													description="Overrides the global font size only for the calendar widget."
													defaultValue="Uses global font size"
													options="Preset CSS sizes or a custom CSS font-size value."
													example="medium"
												/>
												<PresetSelect
													bind:value={draft.general.calendarFontSize}
													options={baseFontSizeOptions}
													allowEmpty
													emptyLabel="Use global font size"
													customPlaceholder="Enter a custom CSS font size"
												/>
											</div>

											<div class="space-y-2">
												<SettingLabel
													label="Widget Style"
													description="Overrides the global widget style only for the calendar widget."
													defaultValue="Uses global widget style"
													options="none, solid, transition, blur, or blank to inherit the global style."
													example="blur"
												/>
												<PresetSelect
													bind:value={draft.general.calendarStyle}
													options={widgetStyleOptions}
													allowEmpty
													allowCustom={false}
													emptyLabel="Use global widget style"
												/>
											</div>
										</div>
									</div>
									{/if}
								</div>
							</div>
						</div>

						<div class={`mt-5 ${subSectionClass}`}>
							<div class="flex flex-col gap-1">
								<h3 class="text-xl font-semibold">Widget Stacking</h3>
								<p class="text-sm text-stone-400">
									Choose which widget sits closest to a shared corner. The first item in the order is closest to the corner, and the last sits farthest away.
								</p>
							</div>

							<div class="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
								{#each widgetStackOrder as widget, index}
									<div class="rounded-[1.25rem] border border-white/10 bg-white/[0.03] p-4">
										<div class="flex items-start justify-between gap-3">
											<div>
												<p class="text-sm font-semibold text-stone-100">{widgetLabels[widget]}</p>
												<p class="mt-1 text-xs text-stone-400">Stack position {index + 1} of {widgetStackOrder.length}</p>
											</div>
											<span class="rounded-full border border-[color:var(--primary-color)]/30 bg-[color:var(--primary-color)]/10 px-2.5 py-1 text-xs text-[color:var(--primary-color)]">
												{index === 0 ? 'Closest' : index === widgetStackOrder.length - 1 ? 'Farthest' : `#${index + 1}`}
											</span>
										</div>

										<div class="mt-4 grid grid-cols-2 gap-2">
											<button
												type="button"
												class="rounded-xl border border-white/10 bg-black/20 px-3 py-3 text-sm text-stone-100 transition hover:bg-white/10 disabled:cursor-not-allowed disabled:opacity-40"
												disabled={index === 0}
												onclick={() => moveWidgetInStack(widget, -1)}
											>
												Move Up
											</button>
											<button
												type="button"
												class="rounded-xl border border-white/10 bg-black/20 px-3 py-3 text-sm text-stone-100 transition hover:bg-white/10 disabled:cursor-not-allowed disabled:opacity-40"
												disabled={index === widgetStackOrder.length - 1}
												onclick={() => moveWidgetInStack(widget, 1)}
											>
												Move Down
											</button>
										</div>
									</div>
								{/each}
							</div>
						</div>
					</section>

					<section class={sectionClass}>
						<div class="flex flex-col gap-1">
							<h2 class="text-2xl font-semibold tracking-tight">Caching</h2>
							<p class="text-sm text-stone-400">
								Runtime server-side cache controls that can be adjusted without editing bootstrap files.
							</p>
						</div>

						<div class="mt-6 grid gap-4 md:grid-cols-2">
							<div class="space-y-2">
								<SettingLabel
									fieldId="renewImagesDuration"
									label="Renew Images Duration"
									description="How many days cached images can stay on disk before they are downloaded again."
									defaultValue="30 days"
									options="Any whole number of days."
									example="30"
								/>
								<input
									id="renewImagesDuration"
									class={inputClass}
									type="number"
									min="0"
									value={getRequiredGeneralNumberValue('renewImagesDuration')}
									oninput={(event) =>
										updateRequiredGeneralIntInput(
											'renewImagesDuration',
											(event.currentTarget as HTMLInputElement).value,
											0
										)}
									onblur={() => commitRequiredGeneralIntInput('renewImagesDuration', 30, 0)}
								/>
							</div>

							<div class="space-y-2">
								<SettingLabel
									fieldId="refreshAlbumPeopleInterval"
									label="Refresh Album / People Interval"
									description="How often person and album feeds should be refreshed from Immich."
									defaultValue="12 hours"
									options="Any whole number of hours."
									example="12"
								/>
								<input
									id="refreshAlbumPeopleInterval"
									class={inputClass}
									type="number"
									min="0"
									value={getRequiredGeneralNumberValue('refreshAlbumPeopleInterval')}
									oninput={(event) =>
										updateRequiredGeneralIntInput(
											'refreshAlbumPeopleInterval',
											(event.currentTarget as HTMLInputElement).value,
											0
										)}
									onblur={() =>
										commitRequiredGeneralIntInput('refreshAlbumPeopleInterval', 12, 0)}
								/>
							</div>
						</div>

						<div class={`mt-6 ${toggleCardClass}`}>
							<div class="min-w-0">
								<SettingLabel
									fieldId="downloadImages"
									label="Download Images"
									description="Downloads image assets to the server cache instead of streaming them every time. This is used by clients that rely on the cache."
									defaultValue="false"
									options="true or false."
									example="Enable it when the frame should keep local cached copies."
								/>
							</div>
							<input
								id="downloadImages"
								class="mt-1 h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 text-[color:var(--primary-color)]"
								type="checkbox"
								checked={draft.general.downloadImages}
								onchange={(event) =>
									updateGeneral(
										'downloadImages',
										(event.currentTarget as HTMLInputElement).checked
									)}
							/>
						</div>
					</section>

					<section class={sectionClass}>
						<div class="flex flex-col gap-1">
							<h2 class="text-2xl font-semibold tracking-tight">Custom CSS</h2>
							<p class="text-sm text-stone-400">
								Add custom CSS for browser and WebView clients. The CSS is stored in App_Data and served from /static/custom.css automatically.
							</p>
						</div>

						<div class="mt-6 space-y-2">
							<SettingLabel
								fieldId="customCss"
								label="Custom CSS"
								description="Paste any CSS rules you want applied on top of the built-in styles."
								defaultValue="Blank"
								options="Any valid CSS."
								example={"#progressbar { visibility: hidden; }"}
							/>
							<textarea
								id="customCss"
								class={`${inputClass} min-h-64 font-mono`}
								spellcheck="false"
								value={draft.customCss}
								oninput={(event) =>
									updateCustomCss((event.currentTarget as HTMLTextAreaElement).value)}
							></textarea>
							<p class="text-xs text-stone-500">
								Example: hide the progress bar with <code>#progressbar {'{ visibility: hidden; }'}</code>.
							</p>
						</div>
					</section>
				</div>

				<div class="sticky bottom-4 z-10">
					<div class="rounded-[2rem] border border-white/10 bg-[#0e0f0c]/90 px-5 py-4 shadow-2xl backdrop-blur">
						<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
							<p class="text-sm text-stone-300">
								{#if isDirty}
									You have unsaved runtime settings.
								{:else}
									All runtime settings are saved.
								{/if}
							</p>
							<button
								type="button"
								class="inline-flex items-center justify-center gap-2 rounded-full border border-[color:var(--primary-color)]/40 bg-[color:var(--primary-color)]/15 px-5 py-3 text-sm font-medium text-[color:var(--primary-color)] transition hover:bg-[color:var(--primary-color)]/25 disabled:cursor-not-allowed disabled:opacity-50"
								disabled={savePending || !isDirty}
								onclick={() => void saveSettings()}
							>
								<Icon path={mdiContentSave} title="Save settings" size="1rem" />
								{savePending ? 'Saving...' : 'Save Settings'}
							</button>
						</div>
					</div>
				</div>
			{/if}
		</div>
	</section>
{/if}

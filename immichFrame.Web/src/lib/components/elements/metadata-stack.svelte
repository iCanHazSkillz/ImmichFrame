<script lang="ts">
	import {
		type AlbumResponseDto,
		type AssetResponseDto,
		type PersonWithFacesResponseDto
	} from '$lib/immichFrameApi';
	import {
		differenceInDays,
		differenceInHours,
		differenceInMinutes,
		differenceInMonths,
		differenceInWeeks,
		differenceInYears,
		format,
		isValid
	} from 'date-fns';
	import * as locale from 'date-fns/locale';
	import { configStore } from '$lib/stores/config.store';
	import Icon from './icon.svelte';
	import { mdiCalendar, mdiMapMarker, mdiAccount, mdiText, mdiImageAlbum, mdiTag } from '@mdi/js';
	import {
		normalizeWidgetPosition,
		resolveWidgetStyle,
		getWidgetSurfaceClass
	} from '$lib/widget-layout';

	interface MetadataEntry {
		asset: AssetResponseDto;
		albums: AlbumResponseDto[];
	}

	interface Props {
		entries?: MetadataEntry[];
		split?: boolean;
	}

	let { entries = [], split = false }: Props = $props();

	function formatLocation(formatPattern: string, city?: string, state?: string, country?: string) {
		const locationParts: string[] = [];

		formatPattern.split(',').forEach((part) => {
			const trimmedPart = part.trim().toLowerCase();
			if (trimmedPart === 'city' && city) {
				locationParts.push(city);
			} else if (trimmedPart === 'state' && state) {
				locationParts.push(state);
			} else if (trimmedPart === 'country' && country) {
				locationParts.push(country);
			}
		});

		return Array.from(locationParts).join(', ');
	}

	const selectedLocale = $derived(locale[$configStore.language as keyof typeof locale] ?? locale.enUS);

	function getAssetCaptureDate(asset: AssetResponseDto) {
		const assetDate = asset.exifInfo?.dateTimeOriginal;
		if (!assetDate) {
			return null;
		}

		const parsedDate = new Date(assetDate);
		return isValid(parsedDate) ? parsedDate : null;
	}

	function getFormattedDate(asset: AssetResponseDto) {
		const parsedDate = getAssetCaptureDate(asset);
		if (!parsedDate) {
			return null;
		}

		return format(parsedDate, $configStore.photoDateFormat ?? 'MM/dd/yyyy', {
			locale: selectedLocale
		});
	}

	function getPersonAge(birthDate?: string | null, capturedAt?: Date | null) {
		if (!birthDate) {
			return null;
		}

		if (!capturedAt) {
			return null;
		}

		const parsedBirthDate = new Date(birthDate);
		if (!isValid(parsedBirthDate)) {
			return null;
		}

		if (parsedBirthDate > capturedAt) {
			return null;
		}

		return differenceInYears(capturedAt, parsedBirthDate);
	}

	function formatPerson(person: PersonWithFacesResponseDto, asset: AssetResponseDto) {
		const name = person.name?.trim();
		if (!name) {
			return null;
		}

		if (!$configStore.showPeopleAge) {
			return name;
		}

		const age = getPersonAge(person.birthDate, getAssetCaptureDate(asset));
		return age === null ? name : `${name} (${age})`;
	}

	function getRelativePhotoAge(asset: AssetResponseDto) {
		const parsedDate = getAssetCaptureDate(asset);
		if (!parsedDate) {
			return null;
		}

		const now = new Date();
		if (parsedDate > now) {
			return null;
		}

		const relativeUnits = [
			{ label: 'year', value: differenceInYears(now, parsedDate) },
			{ label: 'month', value: differenceInMonths(now, parsedDate) },
			{ label: 'week', value: differenceInWeeks(now, parsedDate) },
			{ label: 'day', value: differenceInDays(now, parsedDate) },
			{ label: 'hour', value: differenceInHours(now, parsedDate) }
		];

		for (const unit of relativeUnits) {
			if (unit.value > 0) {
				return `${unit.value} ${unit.label}${unit.value === 1 ? '' : 's'} ago`;
			}
		}

		const minutes = Math.max(1, differenceInMinutes(now, parsedDate));
		return `${minutes} minute${minutes === 1 ? '' : 's'} ago`;
	}

	function getLocation(asset: AssetResponseDto) {
		return formatLocation(
			$configStore.imageLocationFormat ?? 'City,State,Country',
			asset.exifInfo?.city ?? '',
			asset.exifInfo?.state ?? '',
			asset.exifInfo?.country ?? ''
		);
	}

	function getVisibleEntries() {
		return entries.filter(({ asset, albums }) => {
			const people = asset.people?.filter((person) => person.name) ?? [];
			const tags = asset.tags?.filter((tag) => tag.name) ?? [];
			return Boolean(
				($configStore.showPhotoDate && getFormattedDate(asset)) ||
				($configStore.showPhotoTimeAgo && getRelativePhotoAge(asset)) ||
				($configStore.showImageDesc && asset.exifInfo?.description) ||
				($configStore.showAlbumName && albums.length) ||
				($configStore.showPeopleDesc && people.length) ||
				($configStore.showTagsDesc && tags.length) ||
				($configStore.showImageLocation && getLocation(asset))
			);
		});
	}

	const visibleEntries = $derived(getVisibleEntries());
	const alignRight = $derived(($configStore.metadataPosition ?? 'bottom-right').includes('right'));
	const resolvedStyle = $derived(
		resolveWidgetStyle($configStore.metadataStyle, $configStore.style)
	);
	const resolvedPosition = $derived(
		normalizeWidgetPosition($configStore.metadataPosition, 'bottom-right')
	);
</script>

{#if $configStore.showMetadata && visibleEntries.length}
	<div id="imageinfo" class="w-max max-w-none text-primary text-shadow-sm">
		<div class="space-y-2">
			{#each visibleEntries as entry}
				{@const formattedDate = getFormattedDate(entry.asset)}
				{@const relativePhotoAge = getRelativePhotoAge(entry.asset)}
				{@const location = getLocation(entry.asset)}
				{@const availablePeople = entry.asset.people?.filter((person: { name?: string | null }) => person.name) ?? []}
				{@const availableTags = entry.asset.tags?.filter((tag: { name?: string | null }) => tag.name) ?? []}

				<div
					class={`rounded-2xl p-3 ${getWidgetSurfaceClass(resolvedStyle, resolvedPosition)} ${
						alignRight ? 'text-right' : 'text-left'
					}`}
				>
					{#if $configStore.showPhotoDate && formattedDate}
						<p id="photodate" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiCalendar} class="info-icon" />
							<span class="info-text" class:short-text={split}>{formattedDate}</span>
						</p>
					{/if}
					{#if $configStore.showPhotoTimeAgo && relativePhotoAge}
						<p id="phototimeago" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiCalendar} class="info-icon" />
							<span class="info-text" class:short-text={split}>{relativePhotoAge}</span>
						</p>
					{/if}
					{#if $configStore.showImageDesc && entry.asset.exifInfo?.description}
						<p id="imagedescription" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiText} class="info-icon" />
							<span class="info-text" class:short-text={split}>{entry.asset.exifInfo.description}</span>
						</p>
					{/if}
					{#if $configStore.showAlbumName && entry.albums.length > 0}
						<p id="imagealbums" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiImageAlbum} class="info-icon" />
							<span class="info-text" class:short-text={split}>
								{entry.albums.map((album: AlbumResponseDto) => album.albumName).join(', ')}
							</span>
						</p>
					{/if}
					{#if $configStore.showPeopleDesc && availablePeople.length > 0}
						<p id="peopledescription" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiAccount} class="info-icon" />
							<span class="info-text" class:short-text={split}>
								{availablePeople
									.map((person: PersonWithFacesResponseDto) => formatPerson(person, entry.asset))
									.filter(Boolean)
									.join(', ')}
							</span>
						</p>
					{/if}
					{#if $configStore.showTagsDesc && availableTags.length > 0}
						<p id="tagsdescription" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiTag} class="info-icon" />
							<span class="info-text" class:short-text={split}>
								{availableTags.map((tag: { name?: string | null }) => tag.name).join(', ')}
							</span>
						</p>
					{/if}
					{#if $configStore.showImageLocation && location}
						<p id="imagelocation" class:align-left={!alignRight} class="info-item">
							<Icon path={mdiMapMarker} class="info-icon" />
							<span class="info-text" class:short-text={split}>{location}</span>
						</p>
					{/if}
				</div>
			{/each}
		</div>
	</div>
{/if}

<style>
	.info-item {
		display: flex;
		align-items: center;
		justify-content: flex-end;
		gap: 0.5rem;
		margin: 0.2rem 0;
	}

	.align-left {
		justify-content: flex-start;
	}

	.info-icon {
		filter: drop-shadow(0 2px 6px rgb(0 0 0 / 0.45));
	}

	.info-text {
		max-width: none;
		overflow: visible;
		white-space: nowrap;
	}

	.short-text {
		max-width: none;
	}
</style>

<script lang="ts">
	import { type AssetResponseDto } from '$lib/immichFrameApi';
	import { format } from 'date-fns';
	import * as locale from 'date-fns/locale';
	import { configStore } from '$lib/stores/config.store';

	export let asset: AssetResponseDto;
	export let showLocation: boolean;
	export let showPhotoDate: boolean;
	export let showImageDesc: boolean;
	export let showPeopleDesc: boolean;

	$: assetDate = asset.exifInfo?.dateTimeOriginal;
	$: desc = asset.exifInfo?.description ?? '';

	$: time = assetDate ? new Date(assetDate) : null;

	const selectedLocale = $configStore.language;

	const localeToUse =
		(selectedLocale && locale[selectedLocale as keyof typeof locale]) || locale.enUS;

	$: formattedDate = time
		? format(time, $configStore.photoDateFormat ?? 'dd.MM.yyyy', { locale: localeToUse })
		: null;

	$: location = formatLocation(
		$configStore.imageLocationFormat ?? 'City,State,Country',
		asset.exifInfo?.city ?? '',
		asset.exifInfo?.state ?? '',
		asset.exifInfo?.country ?? ''
	);

	$: availablePeople = asset.people?.filter((x) => x.name);

	function formatLocation(format: string, city?: string, state?: string, country?: string) {
		const locationParts: Array<string> = new Array();

		format.split(',').forEach((part) => {
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
</script>

{#if showPhotoDate || showPhotoDate || showImageDesc || showPeopleDesc}
	<div
		class="absolute bottom-0 right-0 z-100 text-primary p-1 text-right
		{$configStore.style == 'solid' ? 'bg-secondary rounded-tl-2xl' : ''}
		{$configStore.style == 'transition' ? 'bg-gradient-to-l from-secondary from-0% pl-10' : ''}
		{$configStore.style == 'blur' ? 'backdrop-blur-lg rounded-tl-2xl' : ''}	"
	>
		{#if showPhotoDate && formattedDate}
			<p class="text-sm font-thin">{formattedDate}</p>
		{/if}
		{#if showImageDesc && desc}
			<p class="text-base font-light">{desc}</p>
		{/if}
		{#if showPeopleDesc && availablePeople}
			<p class="text-sm font-light">
				{availablePeople.map((x) => x.name).join(', ')}
			</p>
		{/if}
		{#if showLocation && location}
			<p class="text-base font-light">{location}</p>
		{/if}
	</div>
{/if}

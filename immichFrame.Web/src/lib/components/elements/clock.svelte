<script lang="ts">
	import * as api from '$lib/immichFrameApi';
	import { onMount } from 'svelte';
	import { format } from 'date-fns';
	import * as locale from 'date-fns/locale';
	import { configStore } from '$lib/stores/config.store';

	let time = new Date();
	let weather: api.IWeather;

	const selectedLocale = $configStore.language;

	const localeToUse =
		(selectedLocale && locale[selectedLocale as keyof typeof locale]) || locale.enUS;

	$: formattedDate = format(time, $configStore.photoDateFormat ?? 'dd.MM.yyyy', {
		locale: localeToUse
	});
	$: timePortion = format(time, $configStore.clockFormat ?? 'HH:mm:ss');

	onMount(() => {
		const interval = setInterval(() => {
			time = new Date();
		}, 1000);

		GetWeather();
		const weatherInterval = setInterval(() => GetWeather, 1 * 60 * 10000);

		return () => {
			clearInterval(interval);
			clearInterval(weatherInterval);
		};
	});

	async function GetWeather() {
		let weatherRequest = await api.getWeather();
		if (weatherRequest.status == 200) {
			weather = weatherRequest.data;
		}
	}
</script>

<div
	class="absolute bottom-0 left-0 z-10 text-center text-primary
	{$configStore.style == 'solid' ? 'bg-secondary rounded-tr-2xl' : ''}
	{$configStore.style == 'transition' ? 'bg-gradient-to-r from-secondary from-0% pr-10' : ''}
	{$configStore.style == 'blur' ? 'backdrop-blur-lg rounded-tr-2xl' : ''}	
	drop-shadow-2xl p-3"
>
	<p class="mt-2 text-sm sm:text-sm md:text-md lg:text-xl font-thin">{formattedDate}</p>
	<p class="mt-2 text-4xl sm:text-4xl md:text-6xl lg:text-8xl font-bold">{timePortion}</p>
	{#if weather}
		<div>
			<div class="text-xl sm:text-xl md:text-2xl lg:text-3xl font-semibold">
				{weather.location},
				{weather.temperature?.toFixed(1)}
				{weather.unit}
			</div>
			{#if $configStore.showWeatherDescription}
				<p class="text-sm sm:text-sm md:text-md lg:text-xl">{weather.description}</p>
			{/if}
		</div>
	{/if}
</div>

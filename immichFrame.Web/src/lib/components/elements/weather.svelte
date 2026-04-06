<script lang="ts">
	import * as api from '$lib/index';
	import { configStore } from '$lib/stores/config.store';
	import { clientIdentifierStore } from '$lib/stores/persist.store';
	import {
		normalizeWidgetPosition,
		resolveWidgetStyle,
		getWidgetSurfaceClass
	} from '$lib/widget-layout';

	api.init();

	let weather = $state<api.IWeather | null>(null);

	const primaryIconId = $derived(() => {
		if (!weather?.iconId) return null;
		const firstId = weather.iconId.split(',')[0].trim();
		return firstId || null;
	});
	const resolvedStyle = $derived(
		resolveWidgetStyle($configStore.weatherStyle, $configStore.style)
	);
	const resolvedPosition = $derived(
		normalizeWidgetPosition($configStore.weatherPosition, 'bottom-left')
	);

	$effect(() => {
		if (!$configStore.showWeather) {
			weather = null;
			return;
		}

		void getWeather();
		const weatherInterval = window.setInterval(() => {
			void getWeather();
		}, 10 * 60 * 1000);

		return () => {
			clearInterval(weatherInterval);
		};
	});

	async function getWeather() {
		try {
			const weatherRequest = await api.getWeather({ clientIdentifier: $clientIdentifierStore });
			if (weatherRequest.status === 200) {
				weather = weatherRequest.data;
			} else {
				weather = null;
				console.warn('Unexpected weather status:', weatherRequest.status);
			}
		} catch (err) {
			weather = null;
			console.error('Error fetching weather:', err);
		}
	}
</script>

{#if $configStore.showWeather && weather}
	<div
		id="weather"
		class={`rounded-[1.75rem] px-3 py-0 text-primary drop-shadow-2xl ${getWidgetSurfaceClass(
			resolvedStyle,
			resolvedPosition
		)}`}
	>
		{#if $configStore.showWeatherLocation}
			<p class="weather-location break-words font-semibold text-shadow-sm">{weather.location}</p>
		{/if}

		<div
			id="weatherinfo"
			class="weather-info weather-main mt-1 flex items-center gap-3 font-semibold text-shadow-sm"
		>
			{#if $configStore.weatherIconUrl && primaryIconId()}
				<img
					src={$configStore.weatherIconUrl.replace('{IconId}', encodeURIComponent(primaryIconId()!))}
					class="icon-weather shrink-0 object-contain"
					alt={weather.description ?? 'Current weather'}
				/>
			{/if}

			<div class="min-w-0">
				<div class="flex flex-wrap items-baseline gap-x-2 gap-y-1">
					<div class="weather-temperature">
						{#if weather.temperature != null}{Math.round(weather.temperature)}°{/if}
					</div>
					{#if $configStore.showWeatherDescription}
						<p
							id="weatherdesc"
							class="weather-description break-words text-shadow-sm"
						>
							{weather.description}
						</p>
					{/if}
				</div>
			</div>
		</div>
	</div>
{/if}

<style>
	.weather-location {
		font-size: 0.95em;
		line-height: 1.2;
	}

	.weather-main {
		font-size: 1.45em;
		line-height: 1.15;
	}

	.icon-weather {
		width: 1.9em;
		height: 1.9em;
	}

	.weather-temperature {
		line-height: 1;
	}

	.weather-description {
		font-size: 0.72em;
		line-height: 1.2;
	}
</style>

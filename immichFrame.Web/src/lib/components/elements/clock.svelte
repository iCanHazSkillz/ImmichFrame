<script lang="ts">
	import { onMount } from 'svelte';
	import { format } from 'date-fns';
	import * as locale from 'date-fns/locale';
	import { configStore } from '$lib/stores/config.store';
	import {
		normalizeWidgetPosition,
		resolveWidgetStyle,
		getWidgetSurfaceClass
	} from '$lib/widget-layout';

	let now = $state(new Date());

	const localeToUse = $derived(
		() => locale[$configStore.language as keyof typeof locale] ?? locale.enUS
	);

	const formattedDate = $derived(() =>
		format(now, $configStore.clockDateFormat ?? 'eee, MMM d', {
			locale: localeToUse()
		})
	);

	const timePortion = $derived(() => format(now, $configStore.clockFormat ?? 'HH:mm:ss'));
	const resolvedStyle = $derived(
		resolveWidgetStyle($configStore.clockStyle, $configStore.style)
	);
	const resolvedPosition = $derived(
		normalizeWidgetPosition($configStore.clockPosition, 'bottom-left')
	);
	const timeMeasure = $derived(
		format(new Date('2000-12-31T23:59:59'), $configStore.clockFormat ?? 'HH:mm:ss')
	);
	const clockMinWidth = $derived(`${Math.max(timeMeasure.length, timePortion().length)}ch`);

	onMount(() => {
		const clockInterval = setInterval(() => {
			now = new Date();
		}, 1000);

		return () => {
			clearInterval(clockInterval);
		};
	});
</script>

{#if $configStore.showClock}
	<div
		id="clock"
		class={`rounded-[1.75rem] p-3 text-center text-primary drop-shadow-2xl ${getWidgetSurfaceClass(
			resolvedStyle,
			resolvedPosition
		)}`}
		style={`min-width: ${clockMinWidth};`}
	>
		<p
			id="clockdate"
			class="clock-date text-shadow-sm"
		>
			{formattedDate()}
		</p>
		<p
			id="clocktime"
			class="clock-time mt-1 inline-block whitespace-nowrap font-bold tabular-nums text-shadow-lg"
		>
			{timePortion()}
		</p>
	</div>
{/if}

<style>
	.clock-date {
		font-size: 0.95em;
		line-height: 1.2;
	}

	.clock-time {
		font-size: 2.8em;
		line-height: 1;
	}
</style>

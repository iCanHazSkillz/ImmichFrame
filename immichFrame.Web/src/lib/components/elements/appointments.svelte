<script lang="ts">
	import * as api from '$lib/index';
	import { isValid } from 'date-fns';
	import { formatInTimeZone } from 'date-fns-tz';
	import { onDestroy } from 'svelte';
	import { configStore } from '$lib/stores/config.store';
	import { clientIdentifierStore } from '$lib/stores/persist.store';
	import {
		normalizeWidgetPosition,
		resolveWidgetStyle,
		getWidgetSurfaceClass
	} from '$lib/widget-layout';

	api.init();

	function getCalendarTimeZone() {
		return $configStore.calendarTimeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone;
	}

	function formatInCalendarTimeZone(date: Date, formatString: string, timeZone: string) {
		try {
			return formatInTimeZone(date, timeZone, formatString);
		} catch {
			return '';
		}
	}

	function formatTimeRange(startTime: string, endTime: string) {
		const startDate = new Date(startTime);
		const endDate = new Date(endTime);
		if (!isValid(startDate) || !isValid(endDate)) {
			return '';
		}

		const timeZone = getCalendarTimeZone();
		const clockFormat = $configStore.clockFormat ?? 'HH:mm';
		return `${formatInCalendarTimeZone(startDate, clockFormat, timeZone)} - ${formatInCalendarTimeZone(endDate, clockFormat, timeZone)}`;
	}

	function truncateAppointmentTitle(summary: string | null | undefined) {
		if (!summary) {
			return '';
		}

		return summary.length > 100 ? `${summary.slice(0, 100)}...` : summary;
	}

	function isTitleTruncated(summary: string | null | undefined) {
		return (summary?.length ?? 0) > 100;
	}

	function getAppointmentKey(appointment: api.IAppointment, index: number) {
		return `${appointment.startTime ?? 'no-start'}-${appointment.summary ?? 'no-summary'}-${index}`;
	}

	let appointments = $state<api.IAppointment[]>([]);
	let expandedAppointmentKey = $state<string | null>(null);
	let expandedAppointmentTimeout: ReturnType<typeof setTimeout> | null = null;
	const resolvedStyle = $derived(
		resolveWidgetStyle($configStore.calendarStyle, $configStore.style)
	);
	const resolvedPosition = $derived(
		normalizeWidgetPosition($configStore.calendarPosition, 'top-right')
	);
	const alignRight = $derived(resolvedPosition.endsWith('right'));

	function clearExpandedAppointmentTimeout() {
		if (expandedAppointmentTimeout) {
			clearTimeout(expandedAppointmentTimeout);
			expandedAppointmentTimeout = null;
		}
	}

	function expandAppointment(key: string) {
		clearExpandedAppointmentTimeout();
		expandedAppointmentKey = key;
	}

	function collapseAppointment(key: string) {
		if (expandedAppointmentKey === key) {
			expandedAppointmentKey = null;
		}

		clearExpandedAppointmentTimeout();
	}

	function handleAppointmentTap(key: string, summary: string | null | undefined) {
		if (!isTitleTruncated(summary)) {
			return;
		}

		expandAppointment(key);
		expandedAppointmentTimeout = setTimeout(() => {
			if (expandedAppointmentKey === key) {
				expandedAppointmentKey = null;
			}
			expandedAppointmentTimeout = null;
		}, 5000);
	}

	onDestroy(() => {
		clearExpandedAppointmentTimeout();
	});

	$effect(() => {
		if (!$configStore.showCalendar || ($configStore.webcalendars?.length ?? 0) === 0) {
			appointments = [];
			expandedAppointmentKey = null;
			clearExpandedAppointmentTimeout();
			return;
		}

		void GetAppointments();
		const appointmentInterval = setInterval(() => void GetAppointments(), 10 * 60 * 1000);

		return () => {
			clearInterval(appointmentInterval);
		};
	});

	async function GetAppointments() {
		let appointmentRequest = await api.getAppointments({
			clientIdentifier: $clientIdentifierStore
		});
		if (appointmentRequest.status == 200) {
			appointments = appointmentRequest.data.sort((a, b) => {
				return new Date(a.startTime ?? '').getTime() - new Date(b.startTime ?? '').getTime();
			});
		}
	}
</script>

{#if $configStore.showCalendar && appointments.length > 0}
	<div
		id="appointments"
		class={`max-w-sm text-primary text-shadow-sm ${alignRight ? 'ml-auto w-3/4' : 'w-3/4'}`}
	>
		<div class="space-y-2">
			{#each appointments as appointment, index (getAppointmentKey(appointment, index))}
				<div
					class={`cursor-pointer rounded-2xl p-3 text-left ${getWidgetSurfaceClass(
						resolvedStyle,
						resolvedPosition
					)}`}
					onmouseenter={() => {
						if (isTitleTruncated(appointment.summary)) {
							expandAppointment(getAppointmentKey(appointment, index));
						}
					}}
					onmouseleave={() => collapseAppointment(getAppointmentKey(appointment, index))}
					onclick={() =>
						handleAppointmentTap(getAppointmentKey(appointment, index), appointment.summary)}
				>
					<p class="appointment-date">
						{formatTimeRange(appointment.startTime ?? '', appointment.endTime ?? '')}
					</p>
					<p class="appointment-title">
						{expandedAppointmentKey === getAppointmentKey(appointment, index)
							? appointment.summary
							: truncateAppointmentTitle(appointment.summary)}
					</p>
					{#if appointment.description}
						<p class="appointment-description font-light">{appointment.description}</p>
					{/if}
				</div>
			{/each}
		</div>
	</div>
{/if}

<style>
	.appointment-title,
	.appointment-date,
	.appointment-description {
		font-size: 0.78em;
		line-height: 1.25;
	}
</style>

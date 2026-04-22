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

	function getCalendarLocale() {
		return $configStore.language || undefined;
	}

	function formatInCalendarTimeZone(date: Date, formatString: string, timeZone: string) {
		try {
			return formatInTimeZone(date, timeZone, formatString);
		} catch {
			return '';
		}
	}

	function getCalendarDayKey(date: Date, timeZone: string) {
		return formatInCalendarTimeZone(date, 'yyyy-MM-dd', timeZone);
	}

	function formatRelativeCalendarDay(dayOffset: 0 | 1) {
		try {
			return new Intl.RelativeTimeFormat(getCalendarLocale(), { numeric: 'auto' }).format(
				dayOffset,
				'day'
			);
		} catch {
			return dayOffset === 0 ? 'Today' : 'Tomorrow';
		}
	}

	function formatUndatedLabel() {
		const language = ($configStore.language ?? 'en').split('-')[0]?.toLowerCase();
		const labels: Record<string, string> = {
			de: 'Ohne Datum',
			en: 'Undated',
			es: 'Sin fecha',
			fr: 'Sans date',
			it: 'Senza data',
			nl: 'Zonder datum',
			pt: 'Sem data'
		};

		return labels[language] ?? labels.en;
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

	function formatDayHeader(startTime: string) {
		const startDate = new Date(startTime);
		if (!isValid(startDate)) {
			return formatUndatedLabel();
		}

		const timeZone = getCalendarTimeZone();
		const today = new Date();
		const tomorrow = new Date(today);
		tomorrow.setDate(today.getDate() + 1);
		const eventDayKey = getCalendarDayKey(startDate, timeZone);

		if (eventDayKey === getCalendarDayKey(today, timeZone)) {
			return formatRelativeCalendarDay(0);
		}

		if (eventDayKey === getCalendarDayKey(tomorrow, timeZone)) {
			return formatRelativeCalendarDay(1);
		}

		return formatInCalendarTimeZone(startDate, 'EEEE, MMM d', timeZone);
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
	const groupedAppointments = $derived.by(() => {
		const groups: Array<{ day: string; appointments: api.IAppointment[] }> = [];

		for (const appointment of appointments) {
			const day = formatDayHeader(appointment.startTime ?? '');
			const currentGroup = groups.at(-1);
			if (currentGroup?.day === day) {
				currentGroup.appointments.push(appointment);
				continue;
			}

			groups.push({ day, appointments: [appointment] });
		}

		return groups;
	});
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
			appointments = appointmentRequest.data;
		}
	}
</script>

{#if $configStore.showCalendar && appointments.length > 0}
	<div
		id="appointments"
		class={`max-w-sm text-primary text-shadow-sm ${alignRight ? 'ml-auto w-3/4' : 'w-3/4'}`}
	>
		<div class="space-y-2">
			{#each groupedAppointments as group}
				<div class="space-y-1.5">
					<p class="appointment-day">{group.day}</p>
					{#each group.appointments as appointment, index (getAppointmentKey(appointment, index))}
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

	.appointment-day {
		font-size: 0.68em;
		font-weight: 700;
		letter-spacing: 0.08em;
		line-height: 1.2;
		opacity: 0.85;
		text-transform: uppercase;
	}
</style>

<script lang="ts">
	import * as api from '$lib/index';
	import { isValid } from 'date-fns';
	import { formatInTimeZone } from 'date-fns-tz';
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

	let appointments = $state<api.IAppointment[]>([]);
	const resolvedStyle = $derived(
		resolveWidgetStyle($configStore.calendarStyle, $configStore.style)
	);
	const resolvedPosition = $derived(
		normalizeWidgetPosition($configStore.calendarPosition, 'top-right')
	);
	const alignRight = $derived(resolvedPosition.endsWith('right'));

	$effect(() => {
		if (!$configStore.showCalendar || ($configStore.webcalendars?.length ?? 0) === 0) {
			appointments = [];
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
			{#each appointments as appointment}
				<div
					class={`rounded-2xl p-3 text-left ${getWidgetSurfaceClass(
						resolvedStyle,
						resolvedPosition
					)}`}
				>
					<p class="appointment-date">
						{formatTimeRange(appointment.startTime ?? '', appointment.endTime ?? '')}
					</p>
					{truncateAppointmentTitle(appointment.summary)}
					{#if appointment.description}
						<p class="appointment-description font-light">{appointment.description}</p>
					{/if}
				</div>
			{/each}
		</div>
	</div>
{/if}

<style>
	.appointment-date,
	.appointment-description {
		font-size: 0.78em;
		line-height: 1.25;
	}
</style>

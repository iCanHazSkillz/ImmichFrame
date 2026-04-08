<script lang="ts">
	import * as api from '$lib/index';
	import { format, isValid } from 'date-fns';
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

	function getZonedFormatter(timeZone: string) {
		return new Intl.DateTimeFormat('en-CA', {
			timeZone,
			year: 'numeric',
			month: '2-digit',
			day: '2-digit',
			hour: '2-digit',
			minute: '2-digit',
			second: '2-digit',
			hourCycle: 'h23'
		});
	}

	function getZonedDisplayDate(date: Date, timeZone: string) {
		const parts = getZonedFormatter(timeZone).formatToParts(date);

		const getPart = (type: Intl.DateTimeFormatPartTypes) =>
			Number.parseInt(parts.find((part) => part.type === type)?.value ?? '', 10);

		const year = getPart('year');
		const month = getPart('month');
		const day = getPart('day');
		const hour = getPart('hour');
		const minute = getPart('minute');
		const second = getPart('second');

		if (
			!Number.isFinite(year) ||
			!Number.isFinite(month) ||
			!Number.isFinite(day) ||
			!Number.isFinite(hour) ||
			!Number.isFinite(minute) ||
			!Number.isFinite(second)
		) {
			return null;
		}

		return new Date(year, month - 1, day, hour, minute, second);
	}

	function formatInCalendarTimeZone(date: Date, formatString: string, timeZone: string) {
		const zonedDisplayDate = getZonedDisplayDate(date, timeZone);
		if (!zonedDisplayDate) {
			return '';
		}

		try {
			return format(zonedDisplayDate, formatString);
		} catch {
			return format(zonedDisplayDate, 'yyyy-MM-dd HH:mm');
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

	let appointments = $state<api.IAppointment[]>([]);
	const resolvedStyle = $derived(
		resolveWidgetStyle($configStore.calendarStyle, $configStore.style)
	);
	const resolvedPosition = $derived(
		normalizeWidgetPosition($configStore.calendarPosition, 'top-right')
	);

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
		class="w-full max-w-sm text-primary text-shadow-sm"
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
					{appointment.summary}
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

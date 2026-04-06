<script lang="ts">
	import * as api from '$lib/index';
	import { format, isSameDay, isToday, isValid } from 'date-fns';
	import { configStore } from '$lib/stores/config.store';
	import { clientIdentifierStore } from '$lib/stores/persist.store';
	import {
		normalizeWidgetPosition,
		resolveWidgetStyle,
		getWidgetSurfaceClass
	} from '$lib/widget-layout';

	api.init();

	function formatTimeRange(startTime: string, endTime: string) {
		const startDate = new Date(startTime);
		const endDate = new Date(endTime);
		if (!isValid(startDate) || !isValid(endDate)) {
			return '';
		}

		const clockFormat = $configStore.clockFormat ?? 'HH:mm';
		if (isSameDay(startDate, endDate) && isToday(startDate)) {
			return `${format(startDate, clockFormat)} - ${format(endDate, clockFormat)}`;
		}

		const dateFormat = $configStore.photoDateFormat ?? 'yyyy-MM-dd';
		const dateTimeFormat = `${dateFormat} ${clockFormat}`;
		return `${format(startDate, dateTimeFormat)} - ${format(endDate, dateTimeFormat)}`;
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
			appointments = appointmentRequest.data;

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

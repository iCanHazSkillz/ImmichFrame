<script lang="ts">
	import * as api from '$lib/immichFrameApi';
	import { onMount } from 'svelte';
	import { format, formatDate } from 'date-fns';
	import { configStore } from '$lib/stores/config.store';

	function formattedDate(time: string) {
		let date = new Date(time);
		return format(date, $configStore.clockFormat ?? 'HH:mm');
	}

	let appointments: api.IAppointment[];

	onMount(() => {
		GetAppointments();
		const appointmentInterval = setInterval(() => GetAppointments, 1 * 60 * 10000);

		return () => {
			clearInterval(appointmentInterval);
		};
	});

	async function GetAppointments() {
		let appointmentRequest = await api.getAppointments();
		if (appointmentRequest.status == 200) {
			appointments = appointmentRequest.data;

			appointments = appointmentRequest.data.sort((a, b) => {
				return new Date(a.startTime ?? '').getTime() - new Date(b.startTime ?? '').getTime();
			});
		}
	}
</script>

{#if appointments}
	<div
		class="absolute top-0 right-0 w-auto z-10 text-center text-primary m-5 max-w-[20%] hidden lg:block md:min-w-[10%]"
	>
		<!-- <div class="text-4xl mx-8 font-bold">Appointments</div> -->
		<div class="">
			{#each appointments as appointment}
				<div class="bg-gray-600 bg-opacity-90 mb-2 text-left rounded-md p-3">
					<p class="text-xs">
						{formattedDate(appointment.startTime ?? '')} - {formattedDate(
							appointment.endTime ?? ''
						)}
					</p>
					{appointment.summary}
					{#if appointment.description}
						<p class="text-xs font-light">{appointment.description}</p>
					{/if}
				</div>
			{/each}
		</div>
	</div>
{/if}

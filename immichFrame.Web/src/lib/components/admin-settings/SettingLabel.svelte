<script lang="ts">
	import { onMount } from 'svelte';

	interface Props {
		label: string;
		description: string;
		defaultValue?: string;
		options?: string;
		example?: string;
		fieldId?: string;
		globalHelpVersion?: number;
		globalHelpExpanded?: boolean;
	}

	let {
		label,
		description,
		defaultValue = '',
		options = '',
		example = '',
		fieldId = undefined,
		globalHelpVersion = 0,
		globalHelpExpanded = false
	}: Props = $props();

	let expanded = $state(false);
	let lastAppliedHelpVersion = $state(0);

	$effect(() => {
		if (globalHelpVersion !== lastAppliedHelpVersion) {
			lastAppliedHelpVersion = globalHelpVersion;
			expanded = globalHelpExpanded;
		}
	});

	onMount(() => {
		const handleToggleAll = (event: Event) => {
			const customEvent = event as CustomEvent<{ expanded?: boolean }>;
			expanded = Boolean(customEvent.detail?.expanded);
		};

		window.addEventListener('immichframe-admin-help-toggle', handleToggleAll as EventListener);

		return () => {
			window.removeEventListener(
				'immichframe-admin-help-toggle',
				handleToggleAll as EventListener
			);
		};
	});
</script>

<div class="space-y-2">
	<div class="flex items-center gap-2">
		{#if fieldId}
			<label for={fieldId} class="text-sm font-medium text-stone-200">{label}</label>
		{:else}
			<span class="text-sm font-medium text-stone-200">{label}</span>
		{/if}

		<button
			type="button"
			class={`text-sm font-semibold leading-none transition focus-visible:outline-none ${
				expanded ? 'text-[#f0cf7f]' : 'text-stone-500'
			}`}
			aria-expanded={expanded}
			aria-label={`${expanded ? 'Hide' : 'Show'} help for ${label}`}
			onclick={() => (expanded = !expanded)}
		>
			?
		</button>
	</div>

	{#if expanded}
		<div class="rounded-[1.1rem] border border-[#d9b865]/40 bg-[#6d5417]/20 px-4 py-3 text-left">
			<p class="text-sm leading-6 text-[#fff2cc]">{description}</p>

			{#if defaultValue}
				<p class="mt-3 text-xs leading-5 text-[#f7e4af]">
					<span class="font-semibold uppercase tracking-[0.2em] text-[#e9c66f]">Default</span>
					<span class="ml-2 font-mono text-[#fff7dd]">{defaultValue}</span>
				</p>
			{/if}

			{#if options}
				<p class="mt-2 text-xs leading-5 text-[#f7e4af]">
					<span class="font-semibold uppercase tracking-[0.2em] text-[#e9c66f]"
						>Possible Values</span
					>
					<span class="ml-2">{options}</span>
				</p>
			{/if}

			{#if example}
				<p class="mt-2 text-xs leading-5 text-[#f7e4af]">
					<span class="font-semibold uppercase tracking-[0.2em] text-[#e9c66f]">Example</span>
					<span class="ml-2 font-mono text-[#fff7dd]">{example}</span>
				</p>
			{/if}
		</div>
	{/if}
</div>

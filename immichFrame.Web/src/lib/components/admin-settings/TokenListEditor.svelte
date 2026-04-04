<script lang="ts">
	import { mdiClose, mdiPlus } from '@mdi/js';
	import type { HTMLInputTypeAttribute } from 'svelte/elements';
	import Icon from '$lib/components/elements/icon.svelte';

	interface Props {
		id?: string;
		values?: string[];
		placeholder?: string;
		addLabel?: string;
		emptyState?: string;
		inputType?: HTMLInputTypeAttribute;
		validator?: ((value: string) => boolean) | null;
		invalidMessage?: string;
		normalize?: (value: string) => string;
	}

	let {
		id = undefined,
		values = $bindable<string[]>([]),
		placeholder = 'Add a value',
		addLabel = 'Add',
		emptyState = 'No entries added yet.',
		inputType = 'text',
		validator = null,
		invalidMessage = 'Please enter a valid value.',
		normalize = (value: string) => value
	}: Props = $props();

	let pendingValue = $state('');
	let feedback = $state('');

	function addValue() {
		const trimmed = pendingValue.trim();
		if (!trimmed) {
			return;
		}

		const normalized = normalize(trimmed);
		if (validator && !validator(normalized)) {
			feedback = invalidMessage;
			return;
		}

		if (values.includes(normalized)) {
			feedback = 'That value is already in the list.';
			return;
		}

		values = [...values, normalized];
		pendingValue = '';
		feedback = '';
	}

	function removeValue(valueToRemove: string) {
		values = values.filter((value) => value !== valueToRemove);
		feedback = '';
	}
</script>

<div class="space-y-3">
	<div class="flex flex-col gap-3 sm:flex-row">
		<input
			{id}
			type={inputType}
			class="min-w-0 flex-1 rounded-2xl border border-white/12 bg-stone-950/85 px-4 py-3 text-sm text-stone-100 outline-none transition placeholder:text-stone-500 focus:border-[color:var(--primary-color)]/75"
			bind:value={pendingValue}
			{placeholder}
			onkeydown={(event) => {
				if (event.key === 'Enter') {
					event.preventDefault();
					addValue();
				}
			}}
		/>

		<button
			type="button"
			class="inline-flex items-center justify-center gap-2 rounded-full border border-[color:var(--primary-color)]/40 bg-[color:var(--primary-color)]/15 px-4 py-3 text-sm font-medium text-[color:var(--primary-color)] transition hover:bg-[color:var(--primary-color)]/25"
			onclick={addValue}
		>
			<Icon path={mdiPlus} size="1rem" />
			{addLabel}
		</button>
	</div>

	{#if feedback}
		<p class="text-xs text-rose-300">{feedback}</p>
	{/if}

	{#if values.length}
		<div class="flex flex-wrap gap-2">
			{#each values as value}
				<div class="inline-flex max-w-full items-center gap-2 rounded-full border border-white/10 bg-white/[0.06] px-3 py-2 text-sm text-stone-200">
					<span class="break-all">{value}</span>
					<button
						type="button"
						class="inline-flex h-5 w-5 items-center justify-center rounded-full border border-white/10 bg-black/20 text-stone-400 transition hover:border-rose-400/40 hover:text-rose-200"
						aria-label={`Remove ${value}`}
						onclick={() => removeValue(value)}
					>
						<Icon path={mdiClose} size="0.85rem" />
					</button>
				</div>
			{/each}
		</div>
	{:else}
		<p class="text-xs text-stone-500">{emptyState}</p>
	{/if}
</div>

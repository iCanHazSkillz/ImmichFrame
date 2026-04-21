<script lang="ts">
	import { mdiClose, mdiPlus } from '@mdi/js';
	import type { HTMLInputTypeAttribute } from 'svelte/elements';
	import Icon from '$lib/components/elements/icon.svelte';

	type TokenStatus = {
		status: 'valid' | 'notFoundOrNoAccess' | 'error' | 'pending';
		label?: string;
		message?: string | null;
		statusCode?: number | null;
	};

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
		maxVisibleItems?: number;
		hasError?: boolean;
		errorMessage?: string;
		tokenStatuses?: Record<string, TokenStatus>;
		enableSelection?: boolean;
		onValuesChanged?: () => void;
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
		normalize = (value: string) => value,
		maxVisibleItems = 5,
		hasError = false,
		errorMessage = '',
		tokenStatuses = {},
		enableSelection = false,
		onValuesChanged = undefined
	}: Props = $props();

	let pendingValue = $state('');
	let feedback = $state('');
	let selectedValues = $state<string[]>([]);

	const visibleSelectedValues = $derived(selectedValues.filter((value) => values.includes(value)));
	const failedValues = $derived(
		values.filter((value) => {
			const status = tokenStatuses[value]?.status;
			return status === 'notFoundOrNoAccess' || status === 'error';
		})
	);

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
		onValuesChanged?.();
	}

	function removeValue(valueToRemove: string) {
		values = values.filter((value) => value !== valueToRemove);
		selectedValues = selectedValues.filter((value) => value !== valueToRemove);
		feedback = '';
		onValuesChanged?.();
	}

	function toggleSelected(value: string, checked: boolean) {
		selectedValues = checked
			? [...new Set([...selectedValues, value])]
			: selectedValues.filter((selectedValue) => selectedValue !== value);
	}

	function selectAll() {
		selectedValues = [...values];
	}

	function selectFailed() {
		selectedValues = [...failedValues];
	}

	function clearSelection() {
		selectedValues = [];
	}

	function removeSelected() {
		const selected = new Set(visibleSelectedValues);
		values = values.filter((value) => !selected.has(value));
		selectedValues = [];
		feedback = '';
		onValuesChanged?.();
	}

	function rowClassFor(value: string) {
		const status = tokenStatuses[value]?.status;
		if (status === 'valid') {
			return 'border-emerald-400/25 bg-emerald-400/[0.08] text-emerald-50';
		}

		if (status === 'notFoundOrNoAccess') {
			return 'border-rose-400/50 bg-rose-500/[0.12] text-rose-50';
		}

		if (status === 'error') {
			return 'border-amber-300/45 bg-amber-400/[0.10] text-amber-50';
		}

		return 'border-white/10 bg-white/[0.06] text-stone-200';
	}

	function badgeClassFor(value: string) {
		const status = tokenStatuses[value]?.status;
		if (status === 'valid') {
			return 'border-emerald-400/30 bg-emerald-400/10 text-emerald-200';
		}

		if (status === 'notFoundOrNoAccess') {
			return 'border-rose-400/40 bg-rose-500/15 text-rose-200';
		}

		if (status === 'error') {
			return 'border-amber-300/40 bg-amber-400/15 text-amber-100';
		}

		return 'border-white/10 bg-white/[0.06] text-stone-300';
	}
</script>

<div class="space-y-3">
	<div class="flex flex-col gap-3 sm:flex-row">
		<input
			{id}
			type={inputType}
			class={`min-w-0 flex-1 rounded-2xl border bg-stone-950/85 px-4 py-3 text-sm text-stone-100 outline-none transition placeholder:text-stone-500 focus:border-[color:var(--primary-color)]/75 ${
				hasError ? 'border-rose-400/70' : 'border-white/12'
			}`}
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

	{#if errorMessage}
		<p class="text-xs text-rose-300">{errorMessage}</p>
	{/if}

	{#if values.length}
		{#if enableSelection}
			<div class="flex flex-wrap items-center gap-2 text-xs">
				<button
					type="button"
					class="rounded-full border border-white/10 px-3 py-1.5 text-stone-300 transition hover:border-white/25 hover:text-stone-100"
					onclick={selectAll}
				>
					Select all
				</button>
				<button
					type="button"
					class="rounded-full border border-white/10 px-3 py-1.5 text-stone-300 transition hover:border-white/25 hover:text-stone-100 disabled:cursor-not-allowed disabled:opacity-40"
					disabled={failedValues.length === 0}
					onclick={selectFailed}
				>
					Select failed
				</button>
				<button
					type="button"
					class="rounded-full border border-white/10 px-3 py-1.5 text-stone-300 transition hover:border-white/25 hover:text-stone-100 disabled:cursor-not-allowed disabled:opacity-40"
					disabled={visibleSelectedValues.length === 0}
					onclick={clearSelection}
				>
					Clear
				</button>
				<button
					type="button"
					class="rounded-full border border-rose-400/35 px-3 py-1.5 text-rose-200 transition hover:bg-rose-500/10 disabled:cursor-not-allowed disabled:opacity-40"
					disabled={visibleSelectedValues.length === 0}
					onclick={removeSelected}
				>
					Remove selected ({visibleSelectedValues.length})
				</button>
			</div>
		{/if}

		<div
			class="flex flex-col gap-2 overflow-y-auto pr-1"
			style={`max-height: ${maxVisibleItems * 3.25}rem;`}
		>
			{#each values as value}
				<div class={`flex min-w-0 w-full items-center gap-2 overflow-hidden rounded-2xl border px-3 py-2 text-sm transition ${rowClassFor(value)}`}>
					{#if enableSelection}
						<input
							type="checkbox"
							class="h-4 w-4 shrink-0 rounded border-white/20 bg-stone-950 accent-[color:var(--primary-color)]"
							aria-label={`Select ${value}`}
							checked={visibleSelectedValues.includes(value)}
							onchange={(event) =>
								toggleSelected(value, (event.currentTarget as HTMLInputElement).checked)}
						/>
					{/if}
					<span class="min-w-0 flex-1 truncate" title={value}>{value}</span>
					{#if tokenStatuses[value]}
						<span
							class={`shrink-0 rounded-full border px-2 py-0.5 text-[0.68rem] font-medium ${badgeClassFor(value)}`}
							title={tokenStatuses[value].message ?? undefined}
						>
							{tokenStatuses[value].label ?? tokenStatuses[value].status}
						</span>
					{/if}
					<button
						type="button"
						class="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full border border-white/10 bg-black/20 text-stone-400 transition hover:border-rose-400/40 hover:text-rose-200"
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

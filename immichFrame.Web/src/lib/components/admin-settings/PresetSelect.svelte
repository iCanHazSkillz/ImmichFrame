<script lang="ts">
	interface SelectOption {
		value: string;
		label: string;
		hint?: string;
	}

	interface Props {
		id?: string;
		value?: string | null;
		options: SelectOption[];
		allowEmpty?: boolean;
		allowCustom?: boolean;
		emptyLabel?: string;
		customPlaceholder?: string;
	}

	const CUSTOM_VALUE = '__custom__';

	let {
		id = undefined,
		value = $bindable<string | null>(null),
		options,
		allowEmpty = false,
		allowCustom = true,
		emptyLabel = 'Use current default',
		customPlaceholder = 'Enter a custom value'
	}: Props = $props();

	let customRequested = $state(false);

	function normalizeValue(next: string | null | undefined) {
		const normalized = next?.trim() ?? '';
		return normalized.length ? normalized : null;
	}

	const normalizedValue = $derived(normalizeValue(value));

	const hasPresetMatch = $derived(options.some((option) => option.value === (normalizedValue ?? '')));

	const showCustomInput = $derived(
		allowCustom && (customRequested || (Boolean(normalizedValue) && !hasPresetMatch))
	);

	const selectedValue = $derived.by(() => {
		if (showCustomInput) {
			return CUSTOM_VALUE;
		}

		return normalizedValue ?? '';
	});

	const selectedHint = $derived.by(() => {
		const current = options.find((option) => option.value === (normalizedValue ?? ''));
		return current?.hint ?? '';
	});

	$effect(() => {
		if (hasPresetMatch) {
			customRequested = false;
		}
	});

	function handleSelectChange(next: string) {
		if (next === CUSTOM_VALUE) {
			customRequested = true;
			if (hasPresetMatch) {
				value = null;
			}
			return;
		}

		customRequested = false;
		value = normalizeValue(next);
	}
</script>

<div class="space-y-2">
	<select
		{id}
		class="w-full rounded-2xl border border-white/12 bg-stone-950/85 px-4 py-3 text-sm text-stone-100 outline-none transition focus:border-[color:var(--primary-color)]/75"
		value={selectedValue}
		onchange={(event) => handleSelectChange((event.currentTarget as HTMLSelectElement).value)}
	>
		{#if allowEmpty}
			<option value="">{emptyLabel}</option>
		{/if}

		{#each options as option}
			<option value={option.value}>{option.label}</option>
		{/each}

		{#if allowCustom}
			<option value={CUSTOM_VALUE}>Custom value</option>
		{/if}
	</select>

	{#if showCustomInput}
		<input
			type="text"
			class="w-full rounded-2xl border border-white/12 bg-stone-950/85 px-4 py-3 text-sm text-stone-100 outline-none transition placeholder:text-stone-500 focus:border-[color:var(--primary-color)]/75"
			value={normalizedValue ?? ''}
			placeholder={customPlaceholder}
			oninput={(event) => (value = normalizeValue((event.currentTarget as HTMLInputElement).value))}
		/>
	{:else if selectedHint}
		<p class="text-xs text-stone-500">{selectedHint}</p>
	{/if}
</div>

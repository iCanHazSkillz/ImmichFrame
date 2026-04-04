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

	const hasPresetMatch = $derived(options.some((option) => option.value === (value ?? '').trim()));

	const showCustomInput = $derived(
		allowCustom && (customRequested || (Boolean((value ?? '').trim()) && !hasPresetMatch))
	);

	const selectedValue = $derived.by(() => {
		if (showCustomInput) {
			return CUSTOM_VALUE;
		}

		return (value ?? '').trim();
	});

	const selectedHint = $derived.by(() => {
		const current = options.find((option) => option.value === (value ?? '').trim());
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
				value = '';
			}
			return;
		}

		customRequested = false;
		value = next.length ? next : null;
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
			value={value ?? ''}
			placeholder={customPlaceholder}
			oninput={(event) => (value = (event.currentTarget as HTMLInputElement).value)}
		/>
	{:else if selectedHint}
		<p class="text-xs text-stone-500">{selectedHint}</p>
	{/if}
</div>

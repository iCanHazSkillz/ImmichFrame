<script lang="ts">
	const HEX_COLOR_PATTERN = /^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;
	const HEX_WITH_ALPHA_PATTERN = /^#[0-9a-fA-F]{8}$/;

	interface Props {
		id?: string;
		value?: string | null;
		fallback?: string;
	}

	let { id = undefined, value = $bindable<string | null>(null), fallback = '#f5deb3' }: Props = $props();

	function getBaseHex(color: string | null | undefined) {
		const normalized = (color ?? '').trim();
		if (HEX_COLOR_PATTERN.test(normalized)) {
			return normalized.slice(0, 7);
		}

		return fallback;
	}

	function getOpacity(color: string | null | undefined) {
		const normalized = (color ?? '').trim();
		if (HEX_WITH_ALPHA_PATTERN.test(normalized)) {
			const alpha = Number.parseInt(normalized.slice(7), 16);
			return Math.round((alpha / 255) * 100);
		}

		return 100;
	}

	const pickerValue = $derived.by(() => {
		return getBaseHex(value);
	});
	const opacityValue = $derived.by(() => getOpacity(value));

	function updateFromPicker(next: string) {
		setColor(next, opacityValue);
	}

	function updateFromSlider(next: number) {
		setColor(getBaseHex(value), next);
	}

	function setColor(baseHex: string, opacityPercent: number) {
		const clamped = Math.max(0, Math.min(100, opacityPercent));
		if (clamped >= 100) {
			value = baseHex;
			return;
		}

		const alphaHex = Math.round((clamped / 100) * 255)
			.toString(16)
			.padStart(2, '0')
			.toUpperCase();
		value = `${baseHex}${alphaHex}`;
	}
</script>

<div class="space-y-2">
	<div class="flex items-center gap-3">
		<input
			{id}
			type="color"
			class="h-12 w-16 rounded-xl border border-white/12 bg-stone-950/85 p-1"
			value={pickerValue}
			oninput={(event) => updateFromPicker((event.currentTarget as HTMLInputElement).value)}
		/>
		<input
			type="text"
			class="min-w-0 flex-1 rounded-2xl border border-white/12 bg-stone-950/85 px-4 py-3 text-sm text-stone-100 outline-none transition placeholder:text-stone-500 focus:border-[color:var(--primary-color)]/75"
			value={value ?? ''}
			placeholder={fallback}
			oninput={(event) => (value = (event.currentTarget as HTMLInputElement).value)}
		/>
	</div>
	<div class="space-y-2">
		<div class="flex items-center justify-between text-xs text-stone-500">
			<span>Opacity</span>
			<span>{opacityValue}%</span>
		</div>
		<input
			type="range"
			min="0"
			max="100"
			value={opacityValue}
			class="w-full accent-[color:var(--primary-color)]"
			oninput={(event) =>
				updateFromSlider(Number.parseInt((event.currentTarget as HTMLInputElement).value, 10))}
		/>
	</div>
	<p class="text-xs text-stone-500">Supports #RRGGBB and #RRGGBBAA values.</p>
</div>

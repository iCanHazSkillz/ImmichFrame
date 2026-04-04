export const widgetPositionOptions = [
	{ value: 'top-left', label: 'Top left' },
	{ value: 'top-right', label: 'Top right' },
	{ value: 'bottom-left', label: 'Bottom left' },
	{ value: 'bottom-right', label: 'Bottom right' }
] as const;

export const widgetStyleOptions = [
	{ value: 'none', label: 'None' },
	{ value: 'solid', label: 'Solid' },
	{ value: 'transition', label: 'Transition' },
	{ value: 'blur', label: 'Blur' }
] as const;

export const widgetStackDefaults = ['clock', 'weather', 'metadata', 'calendar'] as const;
export const widgetStackOptions = [
	{ value: 'clock', label: 'Clock' },
	{ value: 'weather', label: 'Weather' },
	{ value: 'metadata', label: 'Metadata' },
	{ value: 'calendar', label: 'Calendar' }
] as const;

export type WidgetPosition = (typeof widgetPositionOptions)[number]['value'];
export type WidgetStyle = (typeof widgetStyleOptions)[number]['value'];
export type WidgetKey = (typeof widgetStackDefaults)[number];

export function normalizeWidgetPosition(value: string | null | undefined, fallback: WidgetPosition) {
	const normalized = value?.trim().toLowerCase();
	return normalized === 'top-left' ||
		normalized === 'top-right' ||
		normalized === 'bottom-left' ||
		normalized === 'bottom-right'
		? normalized
		: fallback;
}

export function getCornerDockClass(position: WidgetPosition) {
	switch (position) {
		case 'top-left':
			return 'top-3 left-3 items-start sm:top-5 sm:left-5';
		case 'top-right':
			return 'top-3 right-3 items-end sm:top-5 sm:right-5';
		case 'bottom-left':
			return 'bottom-3 left-3 items-start sm:bottom-5 sm:left-5';
		case 'bottom-right':
			return 'bottom-3 right-3 items-end sm:bottom-5 sm:right-5';
	}
}

export function getWidgetStyle(fontSize?: string | null) {
	const normalized = fontSize?.trim();
	return normalized ? `font-size: ${normalized};` : undefined;
}

export function normalizeWidgetStackOrder(
	value: readonly string[] | null | undefined
): WidgetKey[] {
	const ordered: WidgetKey[] = [];

	for (const widget of value ?? []) {
		const normalized = widget?.trim().toLowerCase();
		if (
			(normalized === 'clock' ||
				normalized === 'weather' ||
				normalized === 'metadata' ||
				normalized === 'calendar') &&
			!ordered.includes(normalized)
		) {
			ordered.push(normalized);
		}
	}

	for (const widget of widgetStackDefaults) {
		if (!ordered.includes(widget)) {
			ordered.push(widget);
		}
	}

	return ordered;
}

export function normalizeWidgetStyle(
	value: string | null | undefined,
	fallback: WidgetStyle = 'none'
) {
	const normalized = value?.trim().toLowerCase();
	return normalized === 'none' ||
		normalized === 'solid' ||
		normalized === 'transition' ||
		normalized === 'blur'
		? normalized
		: fallback;
}

export function resolveWidgetStyle(
	overrideStyle: string | null | undefined,
	globalStyle: string | null | undefined
) {
	const normalizedOverride = overrideStyle?.trim();
	return normalizedOverride?.length
		? normalizeWidgetStyle(normalizedOverride)
		: normalizeWidgetStyle(globalStyle);
}

export function getWidgetSurfaceClass(style: WidgetStyle, position: WidgetPosition) {
	const direction = position.endsWith('right') ? 'bg-gradient-to-l' : 'bg-gradient-to-r';

	switch (style) {
		case 'solid':
			return 'border border-white/10 bg-secondary';
		case 'transition':
			return `border border-white/10 ${direction} from-secondary from-0%`;
		case 'blur':
			return 'border border-white/10 bg-black/15 backdrop-blur-lg';
		case 'none':
			return 'border border-transparent bg-transparent';
	}
}

<script lang="ts">
	import { onDestroy, onMount, untrack } from 'svelte';
	import { ProgressBarLocation, ProgressBarStatus } from './progress-bar.types';

	interface Props {
		autoplay?: boolean;
		status?: ProgressBarStatus;
		location?: ProgressBarLocation;
		hidden?: boolean;
		duration?: number;
		onDone: () => void;
		onPlaying?: () => void;
		onPaused?: () => void;
	}

	let {
		autoplay = false,
		status = $bindable(ProgressBarStatus.Paused),
		location = ProgressBarLocation.Bottom,
		hidden = false,
		duration = 5,
		onDone,
		onPlaying = () => {},
		onPaused = () => {}
	}: Props = $props();

	let progress = $state(0);
	let animationFrameId: number | null = null;
	let elapsedMs = 0;
	let runStartedAtMs: number | null = null;
	let playbackGeneration = 0;
	let previousDuration: number | null = null;

	onMount(async () => {
		if (autoplay) {
			await play();
		}
	});

	onDestroy(() => {
		stopAnimation();
	});

	$effect(() => {
		if (previousDuration == null) {
			previousDuration = duration;
			return;
		}

		if (duration === previousDuration) {
			return;
		}

		const currentProgress = untrack(() => progress);
		previousDuration = duration;
		elapsedMs = Math.min(currentProgress, 1) * getDurationMs();

		if (status !== ProgressBarStatus.Playing) {
			return;
		}

		scheduleAnimation();
	});

	export const play = async () => {
		elapsedMs = Math.min(progress, 1) * getDurationMs();
		if (progress >= 1) {
			progress = 0;
			elapsedMs = 0;
		}

		status = ProgressBarStatus.Playing;
		onPlaying();
		scheduleAnimation();
	};

	export const pause = async () => {
		elapsedMs = Math.min(progress, 1) * getDurationMs();
		stopAnimation();
		status = ProgressBarStatus.Paused;
		onPaused();
	};

	export const restart = async (shouldAutoplay: boolean) => {
		stopAnimation();
		playbackGeneration += 1;
		progress = 0;
		elapsedMs = 0;
		status = ProgressBarStatus.Paused;

		if (shouldAutoplay) {
			await play();
		}
	};

	export const reset = async () => {
		stopAnimation();
		playbackGeneration += 1;
		status = ProgressBarStatus.Paused;
		progress = 0;
		elapsedMs = 0;
	};

	function getDurationMs() {
		return Math.max(duration, 0) * 1000;
	}

	function stopAnimation() {
		if (animationFrameId != null) {
			cancelAnimationFrame(animationFrameId);
			animationFrameId = null;
		}

		runStartedAtMs = null;
	}

	function scheduleAnimation() {
		stopAnimation();

		const durationMs = getDurationMs();
		if (durationMs <= 0) {
			progress = 1;
			elapsedMs = 0;
			untrack(() => onDone());
			return;
		}

		runStartedAtMs = performance.now();
		const generation = ++playbackGeneration;
		animationFrameId = window.requestAnimationFrame((now) => {
			step(now, generation);
		});
	}

	function step(now: number, generation: number) {
		if (generation !== playbackGeneration) {
			return;
		}

		const durationMs = getDurationMs();
		if (durationMs <= 0) {
			progress = 1;
			elapsedMs = 0;
			stopAnimation();
			untrack(() => onDone());
			return;
		}

		const startedAtMs = runStartedAtMs ?? now;
		const totalElapsedMs = elapsedMs + (now - startedAtMs);
		progress = Math.min(totalElapsedMs / durationMs, 1);

		if (progress >= 1) {
			elapsedMs = durationMs;
			stopAnimation();
			untrack(() => onDone());
			return;
		}

		animationFrameId = window.requestAnimationFrame((nextNow) => {
			step(nextNow, generation);
		});
	}
</script>

{#if !hidden}
	<span
		id="progressbar"
		class="fixed left-0 h-[3px] bg-primary z-[1000]
		{location == ProgressBarLocation.Top ? 'top-0' : 'bottom-0'}"
		style:width={`${progress * 100}%`}
	></span>
{/if}

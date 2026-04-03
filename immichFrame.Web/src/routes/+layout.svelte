<script lang="ts">
	import '../app.css';
	import { onMount } from 'svelte';
	import { useViewportHeight } from '$lib/hooks/useViewportHeight';

	interface Props {
		children?: import('svelte').Snippet;
	}

	let { children }: Props = $props();
	let customCssHref = $state('/static/custom.css');

	onMount(() => {
		useViewportHeight();
		customCssHref = `/static/custom.css?v=${Date.now()}`;
	});
</script>

<link rel="stylesheet" href={customCssHref} />

{@render children?.()}

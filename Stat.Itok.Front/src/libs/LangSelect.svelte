<script type="ts">
    import { _, locale } from "svelte-i18n";
    import { stored_locale } from "../model";

    function update_stored_locale() {
        console.log($locale);
        stored_locale.set($locale);
    }
</script>

<select
    bind:value={$locale}
    on:change={update_stored_locale}
    class="field select is-small is-link"
>
    <option value="en-US">EN</option>
    <option value="zh-CN">中文</option>
</select>

<script type="ts">
    import { _, locale } from "svelte-i18n";
    import { stored_locale } from "../model";

    function update_stored_locale() {
        console.log($locale);
        stored_locale.set($locale);
    }
</script>

<select
    on:change={() => update_stored_locale()}
    class="field select is-small is-link"
    bind:value={$locale}
>
    <option value="en-US">EN</option>
    <option value="zh-CN">中文</option>
</select>

<!-- svelte-ignore a11y-click-events-have-key-events -->
<script type="ts">
    import LangSelect from "./LangSelect.svelte";
    import {_} from "svelte-i18n";
    import AppLogo from "../assets/icon-512.png";
    import { stored_nin_user } from "../model";
    let AppName = "stat.itok";

    function clearLoginInfo()
    {
        stored_nin_user.set(null);
    }
</script>

<nav class="navbar is-dark" aria-label="main navigation">
    <div class="navbar-brand">
        <a
            class="navbar-item has-text-weight-bold is-size-5"
            rel="noreferrer"
            href="https://github.com/Itoktsnhc/stat.itok"
            target="_blank"
        >
            <img style="height:40px" src={AppLogo} alt={AppName} />
            &nbsp;
            {AppName}
        </a>
    </div>
    <div class="navbar-end">
        <div class="navbar-item level level-right">
            <div class="level-item"><LangSelect /></div>
            <div class="button level-item is-small is-link" on:click={clearLoginInfo}>{$_('btn_logout')}</div>
        </div>
    </div>
    
</nav>

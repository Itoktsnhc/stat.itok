<!-- svelte-ignore a11y-click-events-have-key-events -->

<script type="ts">
    import LangSelect from "./LangSelect.svelte";
    import AppLogo from "../assets/icon-512.png";
    let AppName = "stat.itok";
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
        <div class="navbar-item">
            <LangSelect />
            <!-- <div class="buttons">
                <a
                    class="button is-link"
                    rel="noreferrer"
                    href="https://github.com/Itoktsnhc"
                    target="_blank"
                >
                    <div style="position:relative">
                        <span class="icon"
                            ><i class="fa-brands fa-github" /></span
                        >
                        <span>GitHub</span>
                    </div>
                </a>
                <a
                    class="button is-link"
                    rel="noreferrer"
                    href="https://twitter.com/Itoktsnhc"
                    target="_blank"
                >
                    <div style="position:relative">
                        <span class="icon"
                            ><i class="fa-brands fa-twitter" /></span
                        >
                        <span>Twitter</span>
                    </div>
                </a>
                <a
                    class="button is-link"
                    rel="noreferrer"
                    href="https://space.bilibili.com/784032"
                    target="_blank"
                >
                    <div style="position:relative">
                        <span class="icon"
                            ><i class="fa-brands fa-bilibili" /></span
                        >
                        <span>Bilibili</span>
                    </div>
                </a>
            </div> -->
        </div>
    </div>
</nav>

<script lang="ts">
  import appLogo from './assets/icon-512.png'
  let appName = "stat.itok"
</script>


<main>
  <nav class="navbar is-dark" aria-label="main navigation">
    <div class="navbar-brand">
        <a class="navbar-item has-text-weight-bold is-size-5" rel="noreferrer" href="https://github.com/Itoktsnhc/stat.itok" target="_blank">
            <img style="height:40px" src={appLogo} alt="{appName}" />
            &nbsp;
            {appName}
        </a>
    </div>
    <div class="navbar-end">
        <div class="navbar-item">
            <div class="buttons">
                <a class="button is-link" rel="noreferrer"  href="https://github.com/Itoktsnhc" target="_blank">
                    <div style="position:relative">
                        <span class="icon"><i class="fa-brands fa-github"></i></span>
                        <span>GitHub</span>
                    </div>
                </a>
                <a class="button is-link" rel="noreferrer"  href="https://twitter.com/Itoktsnhc" target="_blank">
                    <div style="position:relative">
                        <span class="icon"><i class="fa-brands fa-twitter"></i></span>
                        <span>Twitter</span>
                    </div>
                </a>
                <a class="button is-link" rel="noreferrer"  href="https://space.bilibili.com/784032" target="_blank">
                    <div style="position:relative">
                        <span class="icon"><i class="fa-brands fa-bilibili"></i></span>
                        <span>Bilibili</span>
                    </div>
                </a>
            </div>
        </div>
    </div>
</nav>
</main>

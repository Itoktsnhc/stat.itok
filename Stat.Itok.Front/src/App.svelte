<!-- svelte-ignore a11y-click-events-have-key-events -->
<script lang="ts">
  import { onMount } from "svelte";
  import LangSelect from "./libs/LangSelect.svelte";
  import { get } from "svelte/store";
  import Login from "./libs/Login.svelte";
  import NavBar from "./libs/NavBar.svelte";
  import History from "./libs/History.svelte";
  import authContext from "./libs/Login.svelte";
  import {
    ApiResp,
    NinTokenCopyInfo,
    NinAuthContext,
    stored_nin_user,
    stored_locale,
  } from "./model";
  let loginKey = Date.now();
  import { addMessages, getLocaleFromNavigator, _, init } from "svelte-i18n";
  import en from "./lang/en.json";
  import cn from "./lang/cn.json";
  import Profile from "./libs/Profile.svelte";
  import Footer from "./libs/Footer.svelte";
  addMessages("en-US", en);
  addMessages("zh-CN", cn);
  init({
    fallbackLocale: "en-US",
    initialLocale: get(stored_locale) || getLocaleFromNavigator(),
  });
  let needAuth = true;
  async function activeAsync() {
    fetch("/api/active_func");
  }
  onMount(async () => {
    activeAsync().then(() => {
      console.log("active_func call finished");
    });
    stored_nin_user.subscribe(async (context) => {
      if (
        context != null &&
        context.sessionToken !== null &&
        context.sessionToken !== undefined &&
        context.sessionToken !== ""
      ) {
        needAuth = false;
      } else {
        needAuth = true;
        loginKey = Date.now();
      }
    });
  });
  let curTab = "Profile";
</script>

<main>
  <NavBar />
  <!--Login first with Collapse-->
  <div class="columns is-centered is-narrow">
    <div class="column is-half ">
      <section class="section">
        {#key loginKey}
          <div class="modal {needAuth ? 'is-active' : null} is-large">
            <div class="modal-background" />
            <div class="modal-content" style="width:auto;">
              <div class="box has-background-light">
                <Login />
              </div>
            </div>
          </div>
        {/key}
        <div class="tabs borders">
          <ul>
            <li
              on:click={(event) => (curTab = "Profile")}
              class={curTab === "Profile" ? "is-active" : null}
            >
              <a href="#/">{$_("profile.tab_name")}</a>
            </li>
            <li
              on:click={(event) => (curTab = "History")}
              class={curTab === "History" ? "is-active" : null}
            >
              <a href="#/">{$_("history.tab_name")}</a>
            </li>
          </ul>
        </div>

        {#if !needAuth}
          <div class={curTab !== "Profile" ? "is-hidden" : ""}>
            <Profile />
          </div>
          <div class={curTab !== "History" ? "is-hidden" : ""}>
            <History />
          </div>
        {/if}
      </section>
    </div>
  </div>
  <Footer />
</main>

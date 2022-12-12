<!-- svelte-ignore a11y-click-events-have-key-events -->
<script lang="ts">
    import { onMount } from "svelte";
    import LangSelect from "./libs/LangSelect.svelte";
    import { get } from "svelte/store";
    import Login from "./libs/Login.svelte";
    import NavBar from "./libs/NavBar.svelte";
    import authContext from "./libs/Login.svelte";
    import {
        ApiResp,
        NinTokenCopyInfo,
        NinAuthContext,
        stored_nin_user,
        stored_locale,
    } from "./model";
    let nickname = "";
    import { addMessages, getLocaleFromNavigator, _, init } from "svelte-i18n";
    import en from "./lang/en.json";
    import cn from "./lang/cn.json";
    import Profile from "./libs/Profile.svelte";
    import Footer from "./libs/Footer.svelte";
    let unique = {};
    addMessages("en-US", en);
    addMessages("zh-CN", cn);
    init({
        fallbackLocale: "en-US",
        initialLocale: get(stored_locale) || getLocaleFromNavigator(),
    });
    let showLoginModal = true;
    onMount(async () => {
        stored_nin_user.subscribe(async (context) => {
            if (
                context != null &&
                context.sessionToken !== null &&
                context.sessionToken !== undefined &&
                context.sessionToken !== ""
            ) {
                showLoginModal = false;
                nickname = context.userInfo.nickname;
            } else {
                showLoginModal = true;
                unique = {};
            }
        });
    });

</script>

<main>
    <NavBar />
    <!--Login first with Collapse-->
    <div class="columns is-centered is-narrow">
        <div class="column is-half ">
            <section class="section">
                {#key unique}
                    <div
                        class="modal {showLoginModal
                            ? 'is-active'
                            : null} is-large"
                    >
                        <div class="modal-background" />
                        <div class="modal-content" style="width:auto;">
                            <div class="box has-background-light">
                                <Login />
                            </div>
                        </div>
                    </div>
                {/key}
                {#key unique}
                    <div class="tabs borders">
                        <ul>
                            <li class="is-active">
                                <a href="#/"
                                    >{$_("profile.tab_name")}[{nickname}]</a
                                >
                            </li>
                        </ul>
                    </div>
                    <Profile />
                {/key}
            </section>
        </div>
    </div>
    <Footer />
</main>

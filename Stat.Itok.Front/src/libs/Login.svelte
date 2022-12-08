<!-- eslint-disable a11y-click-events-have-key-events -->
<script type="ts">
    import LangSelect from "./LangSelect.svelte";
    import { _ } from "svelte-i18n";
    import {
        ApiResp,
        NinTokenCopyInfo,
        NinAuthContext,
        stored_nin_user,
    } from "../model";
    export let isGettingNewAuthUrl = false;
    export let isAuthingNinAccount = false;

    export let redirectUrl = "";
    export let authContext = new NinAuthContext();
    async function getNewAuthUrl() {
        isGettingNewAuthUrl = true;
        authContext = new NinAuthContext();
        try {
            var res = await fetch("/api/nin/verify_url");
            var resp = (await res.json()) as ApiResp<NinTokenCopyInfo>;
            if (resp.result === true) {
                authContext.tokenCopyInfo = resp.data;
            } else {
                throw "Failed to get new auth url:" + resp.msg;
            }
        } catch (error) {
            console.log(error);
        }
        isGettingNewAuthUrl = false;
    }
    function isTokenCopyInfoValid(_info: any, _rUrl: any) {
        return (
            authContext.tokenCopyInfo !== undefined &&
            authContext.tokenCopyInfo !== null &&
            authContext.tokenCopyInfo.authCodeVerifier !== undefined &&
            authContext.tokenCopyInfo.authCodeVerifier !== null &&
            authContext.tokenCopyInfo.authCodeVerifier !== "" &&
            redirectUrl !== undefined &&
            redirectUrl !== null &&
            redirectUrl !== "" &&
            authContext.tokenCopyInfo.copyRedirectionUrl !== undefined &&
            authContext.tokenCopyInfo.copyRedirectionUrl !== null &&
            authContext.tokenCopyInfo.copyRedirectionUrl !== ""
        );
    }
    async function loginNinAccount() {
        isAuthingNinAccount = true;
        authContext.tokenCopyInfo.redirectUrl = redirectUrl;
        try {
            var res = await fetch("/api/nin/auth_account", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(authContext.tokenCopyInfo),
            });
            var resp = (await res.json()) as ApiResp<NinAuthContext>;
            if (resp.result === true) {
                authContext = resp.data;
                await new Promise((resolve) => setTimeout(resolve, 2000));
                stored_nin_user.set(authContext);
            } else {
                throw "Failed to get new auth url:" + resp.msg;
            }
        } catch (error) {
            console.log(error);
        }
        isAuthingNinAccount = false;
    }
</script>

<!-- svelte-ignore a11y-click-events-have-key-events -->


<div class="label level">
    {$_("login.block_name")}
    <div  class="level-right"><LangSelect /> </div>
</div>
<div class="field">
    <div class="py-1">
        1. {$_("login.nintendo_login_intro")}
    </div>
    <div class="py-1">
        2. {$_("login.click_and_get_auth_template.seg_1")}
        <span class="has-background-primary"
            >{$_("login.btn_get_auth_url")}</span
        >{$_("login.click_and_get_auth_template.seg_2")}
        <span class="has-background-primary"
            >{$_("login.link_to_copy_redirection")}</span
        >{$_("login.click_and_get_auth_template.seg_3")}
    </div>
    <div class="py-1">
        3. {$_("login.new_tab_copy_template.seg_1")}<span
            class="has-background-danger-light"
            >{$_("login.new_tab_copy_template.select_this_one")}</span
        >
        {$_("login.new_tab_copy_template.seg_2")}
        <span class="has-background-warning"
            >{$_("login.new_tab_copy_template.copy_link")}</span
        >
    </div>
    <div class="py-2 level-left px-1">
        <p
            on:click={getNewAuthUrl}
            on:keydown={getNewAuthUrl}
            disabled={isAuthingNinAccount || null}
            class="button is-link level-item {isGettingNewAuthUrl
                ? 'is-loading'
                : ''}"
        >
            {$_("login.btn_get_auth_url")}
        </p>
        {#if authContext.tokenCopyInfo.copyRedirectionUrl}
            <a
                id="to_copy_red"
                class="level-item"
                href={authContext.tokenCopyInfo.copyRedirectionUrl}
                rel="noreferrer"
                target="_blank">{$_('login.link_to_copy_redirection')}</a
            >
        {/if}
    </div>
    <div class="py-1">
        4. {$_("login.paste_redirection_template.seg_1")}<span
            class="has-background-primary"
            >{$_("login.label_redirection_link")}</span
        >
        {$_("login.paste_redirection_template.seg_2")}
        <span class="has-background-primary"
            >{$_("login.btn_login_account")}</span
        >
        {$_("login.paste_redirection_template.seg_3")}
    </div>
    <div class="py-3">
        <div class="field label">{$_("login.label_redirection_link")}</div>
        <input
            class="input"
            bind:value={redirectUrl}
            type="text"
            disabled={isAuthingNinAccount || null}
            placeholder={$_("login.link_to_copy_redirection_placeholder")}
        />
        <br />
        <br />
        <button
            on:click={loginNinAccount}
            on:keydown={loginNinAccount}
            class="button py-1 is-link {isAuthingNinAccount
                ? 'is-loading'
                : ''}"
            disabled={!isTokenCopyInfoValid(
                authContext.tokenCopyInfo,
                redirectUrl
            ) || null}>{$_("login.btn_login_account")}</button
        >
        <br />
        <br />
        {#if authContext.userInfo.id}
            <table class="table is-narrow is-bordered is-success">
                <thead
                    ><tr
                        ><td colspan="2" class="is-success"
                            >{$_("login.label_login_success")}</td
                        ></tr
                    ></thead
                >
                <tbody>
                    <tr
                        ><td>{$_("login.label_login_success_id")}</td><td
                            >{authContext.userInfo.id}</td
                        ></tr
                    >
                    <tr
                        ><td>{$_("login.label_login_success_nickname")}</td><td
                            >{authContext.userInfo.nickname}</td
                        ></tr
                    >
                    <tr
                        ><td>{$_("login.label_login_success_country")}</td><td
                            >{authContext.userInfo.country}</td
                        ></tr
                    >
                </tbody>
            </table>
        {/if}
    </div>
</div>

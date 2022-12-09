<script lang="ts">
    import { _ } from "svelte-i18n";
    import { locale } from "svelte-i18n";
    import * as messenger from "bulma-toast";
    import { get } from "svelte/store";
    import {
        ApiResp,
        NinTokenCopyInfo,
        NinAuthContext,
        JobConfigLite,
        stored_nin_user,
    } from "../model";

    export let isTurfWarSelected = true;
    export let isRankBattleSelected = true;
    export let isXMatchSelected = true;
    export let forcedUserLang = "zh-CN";
    if ($locale == "en-US") forcedUserLang = "en-US";
    export let statInkApiKey = "";
    let isSubmittingJobConfig = false;
    function isFormOk(p1: any, p2: any) {
        return (
            forcedUserLang !== null &&
            forcedUserLang !== undefined &&
            forcedUserLang != "" &&
            statInkApiKey !== null &&
            statInkApiKey !== undefined &&
            statInkApiKey !== ""
        );
    }
    async function upsertJobConfigAsync() {
        isSubmittingJobConfig = true;
        try {
            if (!isFormOk("", "")) {
                messenger.toast({
                    message: $_("profile.form_not_ok") as string,
                    type: "is-warning",
                    position: "bottom-center"
                });
                return;
            }
            let jobConfig = new JobConfigLite();
            let authContext = get(stored_nin_user);
            if (
                authContext == null ||
                authContext == undefined ||
                authContext.sessionToken == null ||
                authContext.sessionToken == undefined ||
                authContext.sessionToken == ""
            ) {
                messenger.toast({
                    message: $_("error_info.context_not_ok") as string,
                    type: "is-warning",
                    position: "bottom-center"
                });
                stored_nin_user.set(null);
                return;
            }

            jobConfig.ninAuthContext = authContext;
            if (isTurfWarSelected)
                jobConfig.enabledQueries.push("RegularBattleHistories");
            if (isRankBattleSelected)
                jobConfig.enabledQueries.push("BankaraBattleHistories");
            if (isXMatchSelected)
                jobConfig.enabledQueries.push("XBattleHistories");
            jobConfig.forceUserLang = forcedUserLang;
            jobConfig.statInkApiKey = statInkApiKey;
            var res = await fetch("/api/job_config/upsert", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(jobConfig),
            });
            if (!res.ok) {
                throw new Error("Response is not OK: " + res.statusText);
            }
            var resp = (await res.json()) as ApiResp<NinAuthContext>;
            if (resp.result === true) {
                messenger.toast({
                    message: $_("error_info.upsert_success"),
                    type: "is-success",
                    position: "bottom-center",
                });
            } else {
                throw $_("error_info.upsert_error") + resp.msg;
            }
        } catch (e: unknown) {
            let msg = "";
            if (typeof e === "string") {
                msg = e;
            } else if (e instanceof Error) {
                msg = e.message;
            }
            messenger.toast({
                message: msg,
                type: "is-warning",
                position: "bottom-center",
            });
        } finally {
            isSubmittingJobConfig = false;
        }
    }
</script>

<div class="box has-background-light">
    <div class="title is-4 level">
        {$_("profile.tab_intro")}
    </div>
    <div class="field">
        <div class="level level-left has-text-weight-medium">
            <div class="py-1 ">
                1. {$_("profile.lang_override")}: &nbsp; &nbsp;&nbsp;&nbsp;
            </div>
            <div class=" field select is-small">
                <select bind:value={forcedUserLang}>
                    <option value="zh-CN">简体中文</option>
                    <option value="zh-TW">繁体中文</option>
                    <option value="en-US">English</option>
                </select>
            </div>
        </div>

        <div class="level level-left has-text-weight-medium">
            <div class="py-1 ">
                2. {$_("profile.mode_select")}: &nbsp;
            </div>
            <div>
                <label class="checkbox px-2"
                    ><input bind:checked={isTurfWarSelected} type="checkbox" />
                    {$_("profile.mode_label_turf_war")}</label
                >
                <label class="checkbox px-2"
                    ><input
                        bind:checked={isRankBattleSelected}
                        type="checkbox"
                    />
                    {$_("profile.mode_label_ranked")}</label
                >
                <label class="checkbox px-2"
                    ><input bind:checked={isXMatchSelected} type="checkbox" />
                    {$_("profile.mode_label_x_match")}</label
                >
            </div>
            <div class="py-1 is-size-7 has-background-warning" hidden={isTurfWarSelected||isRankBattleSelected||isXMatchSelected||null}>
                {$_("profile.mode_none_warning")} &nbsp;
            </div>
        </div>


        <div class="level-left">
            <div class=" py-1 level-item has-text-weight-medium">
                3. {$_("profile.label_stat_ink_api")}
            </div>
            <br />
            <br />
            <a
                class="is-link level-item is-size-7"
                target="_blank"
                rel="noreferrer"
                href="https://stat.ink/profile"
                >{$_("profile.link_stat_ink_api")}</a
            >
        </div>
        <input
            class="input"
            type="text"
            bind:value={statInkApiKey}
            placeholder={$_("profile.placeholder_stat_ink_api")}
        />
        <hr />
        <div class="level">
            <div class="level-left">
                <button
                    on:click={upsertJobConfigAsync}
                    class="level-item button is-success {isSubmittingJobConfig
                        ? 'is-loading'
                        : ''}"
                    disabled={!isFormOk(forcedUserLang, statInkApiKey)}
                    >{$_("profile.btn_update")}</button
                >
            </div>
        </div>
    </div>
</div>

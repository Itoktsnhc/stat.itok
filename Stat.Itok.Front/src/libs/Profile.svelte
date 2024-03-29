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
  import { onMount } from "svelte";
  let isLoadingProfile = true;

  export let isTurfWarSelected = true;
  export let isRankBattleSelected = true;
  export let isXMatchSelected = true;
  export let isSalmonSelected = true;
  export let isEventBattleSelected = true;
  export let forcedUserLang = "zh-CN";
  if ($locale == "en-US") forcedUserLang = "en-US";
  export let statInkApiKey = "";
  export let notificationEmail = "";
  export let authErrorLimit = 120;
  const re =
    /^(([^<>()[\]\.,;:\s@\"]+(\.[^<>()[\]\.,;:\s@\"]+)*)|(\".+\"))@(([^<>()[\]\.,;:\s@\"]+\.)+[^<>()[\]\.,;:\s@\"]{2,})$/i;
  let isSubmittingJobConfig = false;
  function isFormOk(p1: any, p2: any, p3: any, p4: any) {
    return (
      forcedUserLang !== null &&
      forcedUserLang !== undefined &&
      forcedUserLang != "" &&
      statInkApiKey !== null &&
      statInkApiKey !== undefined &&
      statInkApiKey !== "" &&
      (notificationEmail === null ||
        notificationEmail === undefined ||
        notificationEmail == "" ||
        notificationEmail.match(re) !== null) &&
      authErrorLimit >= 12
    );
  }
  async function upsertJobConfigAsync() {
    isSubmittingJobConfig = true;
    try {
      if (!isFormOk("", "", "", "")) {
        messenger.toast({
          message: $_("error_info.form_not_ok") as string,
          type: "is-warning",
          position: "top-center",
          duration: 5000,
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
          position: "top-center",
          duration: 5000,
        });
        stored_nin_user.set(null);
        return;
      }

      jobConfig.ninAuthContext = authContext;
      if (isTurfWarSelected)
        jobConfig.enabledQueries.push("RegularBattleHistories");
      if (isRankBattleSelected)
        jobConfig.enabledQueries.push("BankaraBattleHistories");
      if (isXMatchSelected) jobConfig.enabledQueries.push("XBattleHistories");
      if (isSalmonSelected) jobConfig.enabledQueries.push("CoopHistory");
      if (isEventBattleSelected) jobConfig.enabledQueries.push("EventBattleHistories");
      jobConfig.forcedUserLang = forcedUserLang;
      jobConfig.statInkApiKey = statInkApiKey;
      jobConfig.notificationEmail = notificationEmail;
      jobConfig.enabled = true;
      jobConfig.needBuildFromBeginLimit = authErrorLimit;
      var res = await fetch("/api/job_config/upsert", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(jobConfig),
      });
      if (!res.ok && res.status !== 400) {
        throw new Error($_("error_info.upsert_error") + "||" + res.statusText);
      }
      var resp = (await res.json()) as ApiResp<NinAuthContext>;
      if (resp.result === true) {
        messenger.toast({
          message: $_("error_info.upsert_success"),
          type: "is-success",
          position: "top-center",
          duration: 5000,
        });
      } else {
        throw $_("error_info.upsert_error_ex") + resp.msg;
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
        position: "top-center",
        duration: 5000,
      });
    } finally {
      isSubmittingJobConfig = false;
    }
  }
  async function fetchStoredConfigAsync() {
    isLoadingProfile = true;
    let authCtx = get(stored_nin_user);
    if (authCtx === null && authCtx === undefined) return;
    try {
      let res = await fetch("/api/get_job_config_stored", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(authCtx),
      });
      if (!res.ok) {
        if (res.status === 400 || res.status === 401) {
          throw new Error($_("error_info.profile_validation_failed"));
        } else if (res.status == 404) {
          messenger.toast({
            message: $_("error_info.no_exist_profile_found"),
            type: "is-info",
            position: "top-center",
            duration: 5000,
          });
          return;
        } else {
          throw new Error("Response is not OK: " + res.statusText);
        }
      }
      let resp = (await res.json()) as ApiResp<JobConfigLite>;
      if (resp.result === true) {
        let jobConfig = resp.data;
        isTurfWarSelected = jobConfig.enabledQueries.includes(
          "RegularBattleHistories"
        );
        isRankBattleSelected = jobConfig.enabledQueries.includes(
          "BankaraBattleHistories"
        );
        isXMatchSelected =
          jobConfig.enabledQueries.includes("XBattleHistories");
        isSalmonSelected = jobConfig.enabledQueries.includes("CoopHistory");
        isEventBattleSelected = jobConfig.enabledQueries.includes("EventBattleHistories");
        forcedUserLang = jobConfig.forcedUserLang;
        statInkApiKey = jobConfig.statInkApiKey;
        notificationEmail = jobConfig.notificationEmail;
        authErrorLimit = jobConfig.needBuildFromBeginLimit;
      } else {
        throw new Error($_("error_info.no_exist_profile_found"));
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
        position: "top-center",
        duration: 5000,
      });
    } finally {
      isLoadingProfile = false;
    }
  }
  onMount(async () => {
    await fetchStoredConfigAsync();
  });
</script>

{#if isLoadingProfile}
  <br />
  <br />
  <br />
  <br />
  <div class="title level level-item">{$_("profile.loading_label")}</div>
  <progress class="progress is-large is-info" max="100">15%</progress>
{:else}
  <div class="box has-background-light">
    <div class="title is-4 level">
      {$_("profile.tab_intro")}
    </div>
    <div class="field">
      <div class="level level-left has-text-weight-medium">
        <div class="py-1 ">
          1. {$_("profile.lang_override")}: &nbsp; &nbsp;
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
            ><input bind:checked={isRankBattleSelected} type="checkbox" />
            {$_("profile.mode_label_ranked")}</label
          >
          <label class="checkbox px-2"
            ><input bind:checked={isXMatchSelected} type="checkbox" />
            {$_("profile.mode_label_x_match")}</label
          >
          <label class="checkbox px-2"
            ><input bind:checked={isSalmonSelected} type="checkbox" />
            {$_("profile.mode_label_salmon")}</label
          >
          <label class="checkbox px-2"
          ><input bind:checked={isEventBattleSelected} type="checkbox" />
          {$_("profile.mode_label_event_battle")}</label
        >
        </div>
        <div
          class="py-1 is-size-7 has-background-warning {isTurfWarSelected ||
          isRankBattleSelected ||
          isXMatchSelected ||
          isSalmonSelected
            ? 'is-hidden'
            : ''}"
        >
          {$_("profile.mode_none_warning")} &nbsp;
        </div>
      </div>
      <div class=" py-1 level-left has-text-weight-medium">
        3. {$_("profile.label_stat_ink_api")}:
      </div>
      <input
        class="input"
        type="text"
        bind:value={statInkApiKey}
        placeholder={$_("profile.placeholder_stat_ink_api")}
      />
      <br />

      <a
        class="is-link  level-left is-size-7"
        target="_blank"
        rel="noreferrer"
        href="https://stat.ink/profile">{$_("profile.link_stat_ink_api")}</a
      >
      <br />
      <div class="is-italic is-size-7 py-1 level-left has-text-weight-medium">
        {$_("profile.notification_email_label")}
      </div>

      <input
        class="input"
        type="text"
        bind:value={notificationEmail}
        placeholder={$_("profile.placeholder_notification_email")}
      />
      <div class="is-italic is-size-7 py-1 level-left has-text-weight-medium">
        {$_("profile.auth _error_limit_label")}
      </div>
      <input
        class="input"
        type="text"
        bind:value={authErrorLimit}
        placeholder={$_("profile.placeholder_error_limit")}
      />

      <br />
      <hr />
      <div class="level">
        <div class="level-left">
          <button
            on:click={upsertJobConfigAsync}
            class="level-item button is-success {isSubmittingJobConfig
              ? 'is-loading'
              : ''}"
            disabled={!isFormOk(
              forcedUserLang,
              statInkApiKey,
              notificationEmail,
              authErrorLimit
            )}>{$_("profile.btn_update")}</button
          >
        </div>
      </div>
    </div>
  </div>
{/if}

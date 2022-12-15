<script lang="ts">
import { _ } from "svelte-i18n";
import { locale } from "svelte-i18n";
import * as messenger from "bulma-toast";
import type { JobRunHistoryItem, TrackedJobEntity } from "../model";
import { JobState } from "../model";
import { get } from "svelte/store";
import {
  ApiResp,
  NinTokenCopyInfo,
  NinAuthContext,
  JobConfigLite,
  stored_nin_user,
} from "../model";
import { onMount } from "svelte";
import { format, parseISO, intervalToDuration, formatDuration } from "date-fns";

let isLoadingSummary = false;
let continuationToken: string = null;
let historyItems: JobRunHistoryItem[] = [];
async function fetchHistoryAsync() {
  isLoadingSummary = true;
  let authCtx = get(stored_nin_user);
  if (authCtx === null && authCtx === undefined) return;
  try {
    let res = await fetch("/api/get_job_history_stored", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-stat-itok-continuation": continuationToken,
      },
      body: JSON.stringify(authCtx),
    });
    if (!res.ok) {
      throw new Error("Response is not OK: " + res.statusText);
    }
    let resp = (await res.json()) as ApiResp<JobRunHistoryItem[]>;
    if (resp.result === true) {
      let tmpItems = historyItems;
      tmpItems.push(...resp.data);
      historyItems = tmpItems;
      if (res.headers.has("x-stat-itok-continuation")) {
        continuationToken = res.headers.get("x-stat-itok-continuation");
      } else {
        continuationToken = null;
      }
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
    isLoadingSummary = false;
  }
}

function getDurationStr(DateTimeStart: string, DateTimeEnd: string) {
  let start = parseISO(DateTimeStart);
  let end = parseISO(DateTimeEnd);
  let duration = intervalToDuration({
    start,
    end,
  });
  var retStr ="";
  if(duration.days > 0){
    retStr += duration.days + "d ";
  }
  if(duration.hours > 0){
    retStr += duration.hours + "h ";
  }
  if(duration.minutes > 0){
    retStr += duration.minutes + "m ";
  }
  if(duration.seconds > 0){
    retStr += duration.seconds + "s ";
  }
  return retStr;
}
</script>

<div class="box has-background-light">
  <div class="title is-4 level">
    {$_("history.tab_intro")}
  </div>
  <div class="table-container">
    <table
      class="table is-bordered is-striped is-narrow is-hoverable is-fullwidth">
      <thead>
        <tr class="has-background-info-light">
          <th><abbr title="Id">{$_("history.th_id")}</abbr></th>
          <th
            ><abbr title="CreateTime">{$_("history.th_create_time")}</abbr></th>
          <th
            ><abbr title="Execution Cost">{$_("history.th_exec_cost")}</abbr
            ></th>
          <th><abbr title="Status">{$_("history.th_status")}</abbr></th>
          <th
            ><abbr title="statInkLink">{$_("history.th_stat_ink_link")}</abbr
            ></th>
        </tr>
      </thead>
      <tbody>
        {#each historyItems as his}
          <tr>
            <td>{his.trackedId}</td>
            {#if his.trackedJobEntity !== null && his.trackedJobEntity !== undefined}
              <td
                >{format(
                  parseISO(his.trackedJobEntity.createTime),
                  "yyyy-MM-dd HH:mm:ss"
                )}</td>
              {#if his.trackedJobEntity.endTime !== null && his.trackedJobEntity.endTime !== undefined && his.trackedJobEntity.startTime !== null && his.trackedJobEntity.startTime !== undefined}
                <td
                  >{getDurationStr(
                    his.trackedJobEntity.startTime,
                    his.trackedJobEntity.endTime
                  )}</td>
              {:else}
                <td>N/A</td>
              {/if}

              <td
                class="{his.trackedJobEntity.currentJobState ==
                JobState.RanToCompletion
                  ? 'has-background-success-light'
                  : his.trackedJobEntity.currentJobState == JobState.Faulted
                  ? 'has-background-danger-light'
                  : ''}">{JobState[his.trackedJobEntity.currentJobState]}</td>
            {:else}
              <td class="is-warning" colspan="4">ERROR</td>
            {/if}
            {#if his.statInkLink !== null && his.statInkLink !== undefined && his.statInkLink !== ""}
              <td>
                <a
                  class="is-link"
                  rel="noreferrer"
                  href="{his.statInkLink}"
                  target="_blank">Link</a>
              </td>
            {:else}
              <td class="is-info" colspan="4">Not Available</td>
            {/if}
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
  <div
    class="level level-item level-right {continuationToken == null &&
    historyItems.length > 0
      ? 'is-hidden'
      : ''}">
    <div
      on:click="{fetchHistoryAsync}"
      class="button is-small is-info {isLoadingSummary ? 'is-loading' : ''}">
      {$_("history.load_more")}
    </div>
  </div>
</div>

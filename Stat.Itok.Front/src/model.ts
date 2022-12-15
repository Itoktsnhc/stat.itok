import { writable } from "svelte-local-storage-store";

export class ApiResp<T> {
  data: T;
  msg: string;
  result: boolean;
}

export class NinTokenCopyInfo {
  authCodeVerifier: string = "";
  copyRedirectionUrl: string = "";
  redirectUrl: string = "";
}
export class NinUserInfo {
  id: string = "";
  nickname: string = "";
  lang: string = "";
  country: string = "";
  birthday: string = "";
}

export class NinAccessTokenInfo {
  accessToken: string = "";
  idToken: string = "";
}

export class NinAuthContext {
  tokenCopyInfo: NinTokenCopyInfo = new NinTokenCopyInfo();
  sessionToken: string = "";
  preGameToken: string = "";
  gameToken: string = "";
  bulletToken: string = "";
  userInfo: NinUserInfo = new NinUserInfo();
  AccessTokenInfo: NinAccessTokenInfo = new NinAccessTokenInfo();
}

export class JobConfigLite {
  ninAuthContext: NinAuthContext = new NinAuthContext();
  enabledQueries: string[] = [];
  forcedUserLang: string;
  statInkApiKey: string;
}

export enum JobState {
  WaitingForActivation = 0,
  WaitingToRun = 1,
  Faulted = 2,
  RanToCompletion = 3,
  Running = 4,
  WaitingForChildrenToComplete = 5,
  Warning = 7,
}

export class TrackedJobEntity {
  jobId: number;
  options: string;
  currentJobState: JobState;
  createTime: string;
  startTime: string;
  endTime: string;
}

export class JobRunHistoryItem {
  trackedId: number;
  statInkLink: string;
  trackedJobEntity: TrackedJobEntity;
}

export const stored_nin_user = writable("nin_user", new NinAuthContext());
export const stored_locale = writable("user_locale", "");

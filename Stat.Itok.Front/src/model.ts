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

export const stored_nin_user = writable("nin_user", new NinAuthContext());
export const stored_locale = writable("user_locale", "");

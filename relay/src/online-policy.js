export const ROOM_MS = 4 * 60 * 60 * 1000;
export const DAILY_FRAMES = 1400000;
export const INITIAL_FRAMES = 512;
export const GRANT_FRAMES = 2048;
export const MAX_FRAME = 16384;
export const MAX_ACTIVE = 16;
export const dayOf = now => Math.floor(now / 86400000);
export function allocate(state, ip, now, id) {
  const day=dayOf(now);
  if(!state || state.day!==day) state={day,remaining:DAILY_FRAMES,created:0,ips:{},rooms:(state?.rooms||[]).filter(r=>r.expires>now)};
  state.rooms=state.rooms.filter(r=>r.expires>now);
  const bucket=state.ips[ip] || {day:0,recent:[]};
  bucket.recent=bucket.recent.filter(t=>t>now-60000);
  if(state.created>=200 || state.rooms.length>=MAX_ACTIVE || state.remaining<INITIAL_FRAMES || bucket.day>=32 || bucket.recent.length>=2)
    return {state,allowed:false};
  bucket.day++; bucket.recent.push(now); state.ips[ip]=bucket;
  state.created++; state.remaining-=INITIAL_FRAMES;
  const expires=now+ROOM_MS; state.rooms.push({id,expires});
  return {state,allowed:true,expires};
}
export function grant(state,id,now) {
  if(!state || state.day!==dayOf(now)) state={day:dayOf(now),remaining:DAILY_FRAMES,created:0,ips:{},rooms:(state?.rooms||[]).filter(r=>r.expires>now)};
  // A room crossing UTC midnight can request a bounded new-day grant; its
  // signed capability is checked at the edge and expiry again in the room.
  const frames=Math.min(GRANT_FRAMES,state.remaining);
  state.remaining-=frames;
  return {state,frames};
}
const bytesToText = bytes => btoa(String.fromCharCode(...bytes)).replaceAll('+','-').replaceAll('/','_').replaceAll('=','');
const textToBytes = text => Uint8Array.from(atob(text.replaceAll('-','+').replaceAll('_','/')),c=>c.charCodeAt(0));
async function mac(secret,data) {
  const key=await crypto.subtle.importKey('raw',new TextEncoder().encode(secret),{name:'HMAC',hash:'SHA-256'},false,['sign']);
  return new Uint8Array(await crypto.subtle.sign('HMAC',key,new TextEncoder().encode(data)));
}
export async function issue(secret,id,expires,role) {
  const prefix=new Uint8Array(12); prefix.set(Uint8Array.from(id.match(/../g),x=>parseInt(x,16)));
  new DataView(prefix.buffer).setUint32(8,Math.floor(expires/1000));
  const signature=await mac(secret,`playground-online/1/${role}/${bytesToText(prefix)}`);
  const token=new Uint8Array(24); token.set(prefix); token.set(signature.subarray(0,12),12);
  return bytesToText(token);
}
export async function verify(secret,token,role,now) {
  if(!['host','guest'].includes(role) || !/^[A-Za-z0-9_-]{32}$/.test(token||'')) return null;
  const bytes=textToBytes(token);
  const expires=new DataView(bytes.buffer).getUint32(8)*1000;
  if(expires<=now || expires>now+ROOM_MS+1000) return null;
  const expected=await mac(secret,`playground-online/1/${role}/${bytesToText(bytes.subarray(0,12))}`);
  let difference=0; for(let i=0;i<12;i++) difference|=bytes[12+i]^expected[i];
  if(difference!==0) return null;
  return {id:Array.from(bytes.subarray(0,8),b=>b.toString(16).padStart(2,'0')).join(''),expires};
}
export async function ipBucket(secret,ip,now) {
  const digest=await mac(secret,`allocation/${dayOf(now)}/${ip}`);
  return bytesToText(digest.subarray(0,16));
}

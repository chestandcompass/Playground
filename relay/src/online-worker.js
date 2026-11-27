import {DurableObject} from 'cloudflare:workers';
import {ROOM_MS,INITIAL_FRAMES,MAX_FRAME,allocate,grant,issue,verify,ipBucket} from './online-policy.js';
import {chargeWindow} from './policy.js';
const json=(data,status=200)=>Response.json(data,{status,headers:{'Cache-Control':'no-store'}});
const internal=(path,body)=>new Request('https://internal/'+path,{method:'POST',body:JSON.stringify(body)});
const protocolOf=r=>r.headers.get('X-Playground-Protocol')||'';
const validProtocol=p=>/^playground\/1\/[0-9.]{1,20}\/[0-9]{1,8}$/.test(p);
const randomId=()=>Array.from(crypto.getRandomValues(new Uint8Array(8)),b=>b.toString(16).padStart(2,'0')).join('');
export default {
  async fetch(request,env) {
    const path=new URL(request.url).pathname;
    if(path==='/health') return json({service:'Playground online relay',experimental:true,players:2});
    if(!env.ROOM_KEY) return json({error:'Unavailable'},503);
    if(request.method==='POST' && path==='/rooms') {
      const protocol=protocolOf(request);
      if(!validProtocol(protocol)) return json({error:'Invalid protocol'},400);
      const ip=request.headers.get('CF-Connecting-IP');
      if(!ip) return json({error:'Unavailable'},503);
      const id=randomId();
      const bucket=await ipBucket(env.ROOM_KEY,ip,Date.now());
      const allocated=await env.GATE.get(env.GATE.idFromName('budget')).fetch(internal('allocate',{id,ip:bucket}));
      if(!allocated.ok) return allocated;
      const {expires}=await allocated.json();
      const room=env.ROOMS.get(env.ROOMS.idFromName(id));
      const created=await room.fetch(internal('create',{id,expires,protocol}));
      if(!created.ok) return created;
      return json({host:await issue(env.ROOM_KEY,id,expires,'host'),code:'PGO1-'+await issue(env.ROOM_KEY,id,expires,'guest'),expires});
    }
    if(path!=='/connect' && path!=='/check') return json({error:'Not found'},404);
    if(path==='/connect' && request.headers.get('Upgrade')?.toLowerCase()!=='websocket') return json({error:'WebSocket required'},426);
    if(path==='/check' && request.method!=='POST') return json({error:'Not found'},404);
    const role=request.headers.get('X-Room-Role');
    const capability=await verify(env.ROOM_KEY,request.headers.get('X-Room-Token'),role,Date.now());
    // Invalid tokens never reach a Durable Object or create a room namespace.
    if(!capability) return json({error:'Invalid or expired code'},403);
    const headers=new Headers(request.headers); headers.set('X-Verified-Role',role);
    return env.ROOMS.get(env.ROOMS.idFromName(capability.id)).fetch(new Request(request,{headers}));
  }
};
export class LobbyGate extends DurableObject {
  async fetch(request) {
    return this.ctx.blockConcurrencyWhile(async()=>{
      const path=new URL(request.url).pathname;
      const body=await request.json();
      const previous=await this.ctx.storage.get('budget');
      if(path==='/allocate') {
        const result=allocate(previous,body.ip,Date.now(),body.id);
        await this.ctx.storage.put('budget',result.state);
        if(!result.allowed) return json({error:'Relay is busy. Try later.'},429);
        return json({expires:result.expires});
      }
      if(path==='/grant') {
        const result=grant(previous,body.id,Date.now());
        await this.ctx.storage.put('budget',result.state);
        return json({frames:result.frames});
      }
      if(path==='/release') {
        if(previous) {previous.rooms=previous.rooms.filter(r=>r.id!==body.id);await this.ctx.storage.put('budget',previous);}
        return json({ok:true});
      }
      return json({error:'Not found'},404);
    });
  }
}
export class OnlineRoom extends DurableObject {
  async fetch(request) {
    return this.ctx.blockConcurrencyWhile(()=>this.handleFetch(request));
  }
  async handleFetch(request) {
    const path=new URL(request.url).pathname;
    if(path==='/create') {
      if(await this.ctx.storage.get('room')) return json({error:'Room exists'},409);
      const room=await request.json();
      await this.ctx.storage.put('room',room); await this.ctx.storage.setAlarm(Date.now()+60000);
      return json({ok:true});
    }
    const room=await this.ctx.storage.get('room');
    if(!room || room.expires<=Date.now()) return json({error:'Room expired'},410);
    if(protocolOf(request)!==room.protocol) return json({error:'Install the same Playground version.'},409);
    const role=request.headers.get('X-Verified-Role');
    if(!['host','guest'].includes(role)) return json({error:'Unauthorized'},403);
    if(role==='guest' && !this.ctx.getWebSockets('host').length) return json({error:'Host is not online'},409);
    if(path==='/check') return json({ok:true,expires:room.expires});
    if(this.ctx.getWebSockets(role).length) return json({error:'Player already connected'},409);
    if(role==='guest' && room.guestUsed) return json({error:'Lobby ended. Ask the host for a new code.'},410);
    if(role==='host' && room.hostUsed) return json({error:'Host lobby ended'},410);
    if((room.connections||0)>=64) return json({error:'Too many reconnects'},429);
    let credits=INITIAL_FRAMES/2;
    if(room[role+'Used']) {
      const response=await this.env.GATE.get(this.env.GATE.idFromName('budget')).fetch(internal('grant',{id:room.id}));
      if(!response.ok) return json({error:'Relay unavailable'},503);
      credits=(await response.json()).frames;
      if(credits<=0) return json({error:'Free relay capacity reached'},429);
    }
    room[role+'Used']=true; room.connections=(room.connections||0)+1;
    await this.ctx.storage.put('room',room);
    if(role==='host') await this.ctx.storage.setAlarm(room.expires);
    const [client,server]=Object.values(new WebSocketPair());
    this.ctx.acceptWebSocket(server,[role]);
    server.serializeAttachment({role,id:room.id,expires:room.expires,credits,window:null});
    return new Response(null,{status:101,webSocket:client});
  }
  async webSocketMessage(ws,message) {
    let state=ws.deserializeAttachment();
    if(!state || Date.now()>=state.expires) {this.closeRoom(1008,'Room expired');return;}
    if(typeof message==='string' || message.byteLength<6 || message.byteLength>MAX_FRAME) {this.closeRoom(1009,'Invalid frame');return;}
    const header=new Uint8Array(message);
    if(header[0]!==80 || header[1]!==71 || header[2]!==68 || header[3]!==1) {this.closeRoom(1008,'Invalid frame');return;}
    state.window=chargeWindow(state.window,message.byteLength,Date.now());
    if(!state.window.allowed) {this.closeRoom(1008,'Rate limit');return;}
    if(state.credits<=0) {
      ws.serializeAttachment(state);
      // Serialize grants against other socket events; unused credits are retained
      // in the hibernation attachment, never recreated on object wake-up.
      await this.ctx.blockConcurrencyWhile(async()=>{
        state=ws.deserializeAttachment();
        if(state.credits>0) return;
        const response=await this.env.GATE.get(this.env.GATE.idFromName('budget')).fetch(internal('grant',{id:state.id}));
        if(!response.ok) throw new Error('Budget unavailable');
        state.credits=(await response.json()).frames;
        ws.serializeAttachment(state);
      });
      state=ws.deserializeAttachment();
      if(state.credits<=0) {this.closeRoom(1008,'Free relay capacity reached');return;}
    }
    state.credits--; ws.serializeAttachment(state);
    for(const peer of this.ctx.getWebSockets(state.role==='host'?'guest':'host')) {
      try {peer.send(message);} catch {this.closeRoom(1011,'Connection ended');return;}
    }
  }
  closeRoom(code,reason) {for(const socket of this.ctx.getWebSockets()) {try{socket.close(code,reason);}catch{}}}
  async webSocketClose(ws) {
    const state=ws.deserializeAttachment();
    const role=state?.role;
    try{ws.close();}catch{}
    // Packet ordering and retransmission belong to one pair of connections.
    // End both sides rather than reuse the virtual peer for a different guest.
    if(role==='host' || role==='guest') {this.closeRoom(1001,'Session ended');await this.release(state.id);}
  }
  webSocketError(ws) {return this.webSocketClose(ws);}
  async release(id) {await this.env.GATE.get(this.env.GATE.idFromName('budget')).fetch(internal('release',{id}));}
  async alarm() {
    const room=await this.ctx.storage.get('room');
    this.closeRoom(1001,'Room expired');
    if(room) await this.release(room.id);
    await this.ctx.storage.deleteAll();
  }
}

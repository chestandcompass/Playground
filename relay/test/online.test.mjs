import test from 'node:test';
import assert from 'node:assert/strict';
import {allocate,grant,issue,verify,DAILY_FRAMES,ROOM_MS,MAX_ACTIVE} from '../src/online-policy.js';
test('capabilities authenticate their role, room, expiry and version',async()=>{
  const secret='test secret that is never deployed';const now=1760000000000;
  const token=await issue(secret,'0102030405060708',now+ROOM_MS,'guest');
  assert.equal(token.length,32);
  assert.equal((await verify(secret,token,'guest',now)).id,'0102030405060708');
  assert.equal(await verify(secret,token,'host',now),null);
  assert.equal(await verify('wrong secret',token,'guest',now),null);
  assert.equal(await verify(secret,token,'guest',now+ROOM_MS),null);
  assert.equal(await verify(secret,token.slice(0,-1)+'!', 'guest',now),null);
});
test('room allocation is bounded per IP and globally',()=>{
  let state;const now=1760000000000;
  for(let i=0;i<MAX_ACTIVE;i++) {const result=allocate(state,'ip'+i,now,''+i);assert.ok(result.allowed);state=result.state;}
  assert.equal(allocate(state,'other',now,'overflow').allowed,false);
  let a=allocate(null,'one',now,'1');a=allocate(a.state,'one',now,'2');
  assert.ok(a.allowed);assert.equal(allocate(a.state,'one',now,'3').allowed,false);
});
test('traffic credits cannot exceed the daily frame allowance',()=>{
  const now=1760000000000;let state;let charged=0;
  for(let i=0;i<1000;i++){const result=grant(state,'test',now);state=result.state;charged+=result.frames;}
  assert.equal(charged,DAILY_FRAMES);assert.equal(grant(state,'test',now).frames,0);
  assert.ok(grant(state,'test',now+86400000).frames>0);
});
test('active room limits survive UTC midnight and budget renewal',()=>{
  const now=86400000*20000-1000;
  let state;
  for(let i=0;i<MAX_ACTIVE;i++) state=allocate(state,'ip'+i,now,''+i).state;
  const renewed=grant(state,'0',now+2000);
  assert.equal(renewed.state.rooms.length,MAX_ACTIVE);
  assert.equal(allocate(renewed.state,'new-ip',now+2000,'new-room').allowed,false);
  assert.ok(allocate(renewed.state,'new-ip',now+ROOM_MS+1,'new-room').allowed);
});

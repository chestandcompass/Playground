import {test} from 'node:test';
import assert from 'node:assert/strict';
import {chargeWindow, capacity} from '../src/policy.js';
test('rate and byte limits reset at one second',()=>{
  let state;
  for(let i=0;i<120;i++) state=chargeWindow(state,10,0);
  assert.equal(state.allowed,true);
  assert.equal(chargeWindow(state,10,1).allowed,false);
  assert.equal(chargeWindow(state,10,1000).allowed,true);
  assert.equal(chargeWindow(null,524289,0).allowed,false);
});
test('two player request budget counts both directions',()=>{
  assert.equal(capacity(20).requestsPerHour,7200);
  assert.ok(Math.abs(capacity(20).roomHoursByRequests-13.8889)<0.001);
  assert.ok(capacity(20).roomHoursByDurationWorstCase<29);
});

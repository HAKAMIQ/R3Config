// Read-only: SettingLifecycleEvidence for settings whose display name is no longer declared at the frozen HEAD.
const fs = require('fs');
const cp = require('child_process');
const [outCsv, repo] = process.argv.slice(2);
if (!outCsv || !repo) {
  console.error('Usage: node setting-lifecycle.js <out.csv> <frozen-rpcs3-repo>');
  process.exit(2);
}
const git = (...a) => cp.execFileSync('git', ['-C', repo, ...a], { maxBuffer: 1 << 26 }).toString().trim();
const settings = [
  { wiki: 'tsx instructions', name: 'Enable TSX' },
  { wiki: 'xfloat accuracy / accurate xfloat', name: 'Accurate xfloat' },
  { wiki: 'lower spu thread priority', name: 'Lower SPU thread priority' },
  { wiki: 'disable native float16 support', name: 'Disable Native float16 Support' },
  { wiki: 'enable spu events busy loop', name: 'Enable SPU Events Busy Loop' },
  { wiki: 'delay each odd mfc command', name: 'Delay Each Odd MFC' },
  { wiki: 'bind spu threads to secondary cores', name: 'Bind SPU threads to secondary cores' },
];
const head = git('rev-parse', 'HEAD');
const out = ['WikiKey,ConfigDisplayName,DeclaredAtFrozenHead,LastChangeCommit,LastChangeDate,LastChangeBuild,Evidence'];
for (const s of settings) {
  let present = false;
  try { present = git('grep', '-c', '-i', '-F', s.name, 'HEAD', '--', 'rpcs3/Emu/system_config.h', 'rpcs3/Emu/system_config_types.cpp').length > 0; } catch { present = false; }
  const line = git('log', '-S' + s.name, '-i', '--format=%H %cs', '--', 'rpcs3/Emu/system_config.h').split('\n')[0];
  if (!line) { out.push([s.wiki, s.name, present, '', '', '', 'NO_HISTORY_FOUND_UNDER_THIS_NAME'].join(',')); continue; }
  const [commit, date] = line.split(' ');
  out.push([s.wiki, s.name, present, commit, date, git('rev-list', '--count', commit), present ? 'STILL_DECLARED' : 'LAST_TOUCH_IS_REMOVAL_COMMIT'].join(','));
}
fs.writeFileSync(outCsv, out.join('\n') + '\n');
console.log('frozen HEAD ' + head + '\n' + out.join('\n'));

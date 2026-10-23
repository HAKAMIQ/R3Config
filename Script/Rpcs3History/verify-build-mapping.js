// Read-only: verifies for every shipped rpcs3-builds.json row that
// `git rev-list --count <commitFull>` in the frozen RPCS3 repository equals the row's build number.
const fs = require('fs');
const cp = require('child_process');
const path = require('path');
const [outCsv, repo] = process.argv.slice(2);
if (!outCsv || !repo) {
  console.error('Usage: node verify-build-mapping.js <out.csv> <frozen-rpcs3-repo>');
  process.exit(2);
}
const data = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '..', 'App', 'Data', 'rpcs3-builds.json'), 'utf8'));
const rows = data.builds;
const out = ['sourceClass,version,build,commitFull,commitCount,result'];
let i = 0, active = 0, done = 0;
const tally = {};
function next() {
  while (active < 6 && i < rows.length) {
    const r = rows[i++];
    active++;
    cp.execFile('git', ['-C', repo, 'rev-list', '--count', r.commitFull], (err, stdout) => {
      active--; done++;
      let count = '', result;
      if (err) result = 'COMMIT_NOT_IN_FROZEN_REPO';
      else { count = stdout.trim(); result = Number(count) === r.build ? 'MATCH' : 'MISMATCH'; }
      const k = r.sourceClass + ':' + result; tally[k] = (tally[k] || 0) + 1;
      out.push([r.sourceClass, r.version, r.build, r.commitFull, count, result].join(','));
      if (done === rows.length) {
        out.sort();
        fs.writeFileSync(outCsv, out.join('\n') + '\n');
        fs.writeFileSync(outCsv + '.summary.json', JSON.stringify({ rows: rows.length, tally }, null, 2));
        console.log(JSON.stringify(tally));
      } else next();
    });
  }
}
next();

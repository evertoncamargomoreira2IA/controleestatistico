'use strict';
// Lógica de execução do Codex. Recarregada a cada chamada: editar este arquivo NÃO exige reiniciar o Claude.
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');
const TIMEOUT_MS = 20 * 60 * 1000;
const LOG_FILE = path.join(__dirname, 'codex-bridge.log');
function log(...a) {
  const line = `[${new Date().toISOString()}] ` + a.join(' ') + '\n';
  process.stderr.write('[codex-bridge] ' + line);
  try { if (fs.existsSync(LOG_FILE) && fs.statSync(LOG_FILE).size > 1e6) fs.unlinkSync(LOG_FILE); fs.appendFileSync(LOG_FILE, line); } catch {}
}

function findCodex() {
  if (process.env.CODEX_EXE && fs.existsSync(process.env.CODEX_EXE)) return process.env.CODEX_EXE;
  const local = process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local');
  const binDir = path.join(local, 'OpenAI', 'Codex', 'bin');
  let best = null, bestTime = -1;
  try {
    for (const d of fs.readdirSync(binDir)) {
      const exe = path.join(binDir, d, process.platform === 'win32' ? 'codex.exe' : 'codex');
      try { const t = fs.statSync(exe).mtimeMs; if (t > bestTime) { best = exe; bestTime = t; } } catch {}
    }
  } catch {}
  return best || 'codex';
}

// Alguns clientes MCP descartam os parâmetros opcionais. Por isso também aceitamos
// marcadores no início do prompt:  [session_id: <uuid>]  [sandbox: workspace-write]  [cwd: C:\pasta]
function parseMarkers(args) {
  args = Object.assign({}, args);
  let p = String(args.prompt || '');
  const re = /^\s*\[(session_id|sandbox|cwd|model)\s*:\s*([^\]]+)\]/i;
  let m;
  while ((m = p.match(re))) {
    const k = m[1].toLowerCase(), v = m[2].trim();
    if (!args[k]) args[k] = v;
    p = p.slice(m[0].length);
  }
  args.prompt = p.trim();
  return args;
}

function runCodex(args) {
  args = parseMarkers(args);
  log('args', JSON.stringify(Object.assign({}, args, { prompt: String(args.prompt || '').slice(0, 80) })));
  return new Promise((resolve) => {
    const exe = findCodex();
    const outFile = path.join(os.tmpdir(), `codex-last-${process.pid}-${Date.now()}.txt`);
    const cwd = args.cwd && fs.existsSync(args.cwd) ? args.cwd : os.homedir();
    const sandbox = args.sandbox || 'read-only';
    // `codex exec resume` só aceita as opções DEPOIS de `resume <id>` e não tem --sandbox/--cd.
    const a = args.session_id
      ? ['exec', 'resume', String(args.session_id), '--skip-git-repo-check', '--output-last-message', outFile,
         '-c', `sandbox_mode="${sandbox}"`]
      : ['exec', '--skip-git-repo-check', '--output-last-message', outFile, '--sandbox', sandbox, '--cd', cwd];
    if (args.model) a.push('--model', String(args.model));
    a.push('-');
    log('spawn', exe, a.join(' '));
    let stdout = '', stderr = '';
    let child;
    try {
      child = spawn(exe, a, { cwd, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    } catch (e) {
      return resolve({ ok: false, text: `Não consegui iniciar o Codex (${exe}): ${e.message}` });
    }
    const timer = setTimeout(() => { try { child.kill(); } catch {} }, TIMEOUT_MS);
    child.stdout.on('data', d => { stdout += d; if (stdout.length > 2e6) stdout = stdout.slice(-1e6); });
    child.stderr.on('data', d => { stderr += d; if (stderr.length > 2e6) stderr = stderr.slice(-1e6); });
    child.on('error', e => { clearTimeout(timer); resolve({ ok: false, text: `Erro ao executar o Codex (${exe}): ${e.message}` }); });
    child.on('close', code => {
      clearTimeout(timer);
      log('exit', code, 'stderr:', stderr.slice(0, 1500).replace(/\s+/g, ' '));
      let last = '';
      try { last = fs.readFileSync(outFile, 'utf8').trim(); fs.unlinkSync(outFile); } catch {}
      const all = stdout + '\n' + stderr;
      const m = all.match(/session id:\s*([0-9a-f-]{36})/i);
      const sid = m ? m[1] : (args.session_id || '');
      if (code === 0 && last) {
        resolve({ ok: true, text: last + (sid ? `\n\n[session_id: ${sid}]` : '') });
      } else {
        const tail = all.trim().slice(-4000);
        resolve({ ok: code === 0, text: (last || '') + `\n\n[codex saiu com código ${code}]\n${tail}` });
      }
    });
    child.stdin.end(String(args.prompt));
  });
}

module.exports = { runCodex, findCodex };

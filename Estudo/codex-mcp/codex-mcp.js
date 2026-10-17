#!/usr/bin/env node
// Ponte MCP (stdio) entre o Claude Desktop e o Codex CLI instalado neste PC.
// Expõe a ferramenta "codex", que roda `codex exec` e devolve a resposta final.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const SERVER = { name: 'codex-bridge', version: '1.0.0' };

function log(...a) { process.stderr.write('[codex-bridge] ' + a.join(' ') + '\n'); }

const TOOLS = [{
  name: 'codex',
  description: 'Envia uma tarefa ao OpenAI Codex (agente de código do ChatGPT instalado neste PC) e devolve a resposta final dele. ' +
    'Use para pedir ao Codex que escreva, revise, explique ou corrija código/arquivos. ' +
    'Para continuar uma conversa anterior, comece o prompt com o marcador [session_id: <id>] devolvido na resposta anterior. ' +
    'Para permitir que o Codex edite arquivos, comece com [sandbox: workspace-write] [cwd: C:\\caminho\\da\\pasta]. ' +
    'Os marcadores funcionam mesmo se os parâmetros opcionais forem descartados pelo cliente.',
  inputSchema: {
    type: 'object',
    properties: {
      prompt: { type: 'string', description: 'Instrução para o Codex.' },
      cwd: { type: 'string', description: 'Pasta de trabalho (caminho absoluto no Windows). Padrão: pasta do usuário.' },
      sandbox: { type: 'string', enum: ['read-only', 'workspace-write', 'danger-full-access'],
        description: 'Permissão do Codex. read-only (padrão) só lê; workspace-write pode editar arquivos dentro de cwd.' },
      model: { type: 'string', description: 'Modelo opcional (ex.: o padrão configurado no Codex é usado se omitido).' },
      session_id: { type: 'string', description: 'Opcional: id de sessão retornado numa chamada anterior, para continuar a mesma conversa.' }
    },
    required: ['prompt']
  }
}];

function loadRunner() {
  const p = require.resolve('./codex-run.js');
  delete require.cache[p];
  return require(p);
}

function send(msg) { process.stdout.write(JSON.stringify(msg) + '\n'); }

async function handle(msg) {
  const { id, method, params } = msg;
  if (method === 'initialize') {
    return send({ jsonrpc: '2.0', id, result: {
      protocolVersion: (params && params.protocolVersion) || '2025-06-18',
      capabilities: { tools: {} },
      serverInfo: SERVER,
      instructions: 'Use a ferramenta "codex" para delegar tarefas de código ao OpenAI Codex local.'
    }});
  }
  if (method === 'ping') return send({ jsonrpc: '2.0', id, result: {} });
  if (method === 'tools/list') return send({ jsonrpc: '2.0', id, result: { tools: TOOLS } });
  if (method === 'tools/call') {
    const name = params && params.name;
    const args = (params && params.arguments) || {};
    if (name !== 'codex' || !args.prompt) {
      return send({ jsonrpc: '2.0', id, result: { isError: true, content: [{ type: 'text', text: 'Ferramenta desconhecida ou prompt vazio.' }] } });
    }
    const r = await loadRunner().runCodex(args);
    return send({ jsonrpc: '2.0', id, result: { isError: !r.ok, content: [{ type: 'text', text: r.text }] } });
  }
  if (id !== undefined && id !== null) {
    if (method === 'resources/list') return send({ jsonrpc: '2.0', id, result: { resources: [] } });
    if (method === 'prompts/list') return send({ jsonrpc: '2.0', id, result: { prompts: [] } });
    return send({ jsonrpc: '2.0', id, error: { code: -32601, message: `Method not found: ${method}` } });
  }
}

let buf = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => {
  buf += chunk;
  let i;
  while ((i = buf.indexOf('\n')) >= 0) {
    const line = buf.slice(0, i).trim(); buf = buf.slice(i + 1);
    if (!line) continue;
    let msg; try { msg = JSON.parse(line); } catch { log('JSON inválido'); continue; }
    handle(msg).catch(e => { log('erro', e.stack); if (msg.id != null) send({ jsonrpc: '2.0', id: msg.id, error: { code: -32603, message: e.message } }); });
  }
});
process.stdin.on('end', () => process.exit(0));
log('pronto; codex =', loadRunner().findCodex());

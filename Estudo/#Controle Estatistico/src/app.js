import { METHOD_VERSION, CONSTANTS_SOURCE } from "./constants.js";
import { parseMeasurement, validateConfig, computeXbarR, computeCapability, assessment } from "./spc.js";
import { generateSampleData } from "./sample-data.js";

const $ = selector => document.querySelector(selector);
const form = $("#config-form");
const textarea = $("#measurements");
const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, character => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;"
}[character]));

$("#method").textContent = METHOD_VERSION;
$("#source").textContent = CONSTANTS_SOURCE;

function parseGroups(text) {
  const errors = [];
  const groups = [];
  text.split(/\r?\n/).forEach((line, index) => {
    if (!line.trim()) return;
    const tokens = line.trim().split(/[;\t ]+/);
    const values = tokens.map(token => parseMeasurement(token));
    values.forEach((value, position) => {
      if (value.error) errors.push(`Linha ${index + 1}, medição ${position + 1}: ${value.error}`);
    });
    if (!values.some(value => value.error)) groups.push(values.map(value => value.value));
  });
  return { groups, errors };
}

const fmt = value => Number.isFinite(value)
  ? new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 4 }).format(value)
  : "—";

function chart(title, values, limits, specs) {
  const width = 600;
  const height = 280;
  const left = 55;
  const right = 54;
  const top = 20;
  const bottom = 35;
  const plotW = width - left - right;
  const plotH = height - top - bottom;
  const min = Math.min(...values, limits.lcl, ...(specs || []));
  const max = Math.max(...values, limits.ucl, ...(specs || []));
  const pad = (max - min || 1) * 0.14;
  const low = min - pad;
  const high = max + pad;
  const x = index => left + (values.length === 1 ? plotW / 2 : index * plotW / (values.length - 1));
  const y = value => top + (high - value) * plotH / (high - low);
  let svg = `<svg class="chart" viewBox="0 0 ${width} ${height}" role="img" aria-label="${escapeHtml(title)}">`;
  for (let tick = 0; tick < 5; tick += 1) {
    const value = high - (high - low) * tick / 4;
    const yy = y(value);
    svg += `<line x1="${left}" y1="${yy}" x2="${width - right}" y2="${yy}" stroke="#e5ebef"/><text x="3" y="${yy + 4}">${fmt(value)}</text>`;
  }
  const line = (value, color, dashed, label, side) => {
    const labelX = side === "left" ? left - 5 : width - right + 5;
    const anchor = side === "left" ? "end" : "start";
    return `<line x1="${left}" y1="${y(value)}" x2="${width - right}" y2="${y(value)}" stroke="${color}" stroke-width="2" ${dashed ? 'stroke-dasharray="7 5"' : ""}/><text x="${labelX}" y="${y(value) - 4}" text-anchor="${anchor}" fill="${color}">${label}</text>`;
  };
  svg += line(limits.ucl, "#b42318", true, "LSC", "right");
  svg += line(limits.cl, "#334155", false, "LC", "right");
  svg += line(limits.lcl, "#b42318", true, "LIC", "right");
  if (specs) {
    svg += line(specs[0], "#7c3aed", true, "LSL", "left");
    svg += line(specs[1], "#c2410c", true, "USL", "left");
  }
  svg += `<polyline fill="none" stroke="#087e8b" stroke-width="2" points="${values.map((value, index) => `${x(index)},${y(value)}`).join(" ")}"/>`;
  const labelEvery = values.length > 15 ? 5 : 1;
  values.forEach((value, index) => {
    const out = value < limits.lcl || value > limits.ucl;
    svg += `<circle cx="${x(index)}" cy="${y(value)}" r="${out ? 6 : 4}" fill="${out ? "#c62828" : "#087e8b"}"/>`;
    if (index % labelEvery === 0 || index === values.length - 1) {
      svg += `<text x="${x(index) - 5}" y="${height - 10}">${index + 1}</text>`;
    }
    if (out) svg += `<text x="${x(index) - 5}" y="${y(value) - 9}" fill="#b42318">!</text>`;
  });
  return `${svg}</svg>`;
}

function metric(label, value) {
  return `<div class="metric"><span>${escapeHtml(label)}</span><strong>${value?.value == null ? "N/D" : fmt(value.value)}</strong></div>${value?.reason ? `<small>${escapeHtml(value.reason)}</small>` : ""}`;
}

function calculate() {
  const configValues = Object.fromEntries(new FormData(form));
  const validation = validateConfig(configValues);
  const parsed = parseGroups(textarea.value);
  const errors = [...validation.errors, ...parsed.errors];
  if (validation.valid && parsed.groups.length < 2) errors.push("Informe pelo menos 2 subgrupos.");
  if (validation.valid) {
    parsed.groups.forEach((group, index) => {
      if (group.length !== validation.config.subgroupSize) errors.push(`Subgrupo ${index + 1}: esperado ${validation.config.subgroupSize} valores; recebido ${group.length}.`);
    });
  }
  const errorBox = $("#errors");
  if (errors.length) {
    errorBox.innerHTML = `<div class="errorbox" role="alert"><strong>Corrija os itens abaixo:</strong><ul>${errors.map(error => `<li>${escapeHtml(error)}</li>`).join("")}</ul></div>`;
    $("#results").hidden = true;
    return;
  }
  errorBox.innerHTML = "";
  const xr = computeXbarR(parsed.groups, validation.config.subgroupSize);
  const capability = computeCapability(parsed.groups, validation.config, xr);
  const status = assessment(xr, capability);
  $("#results").hidden = false;
  $("#assessment").innerHTML = status.messages.map(message => `<p>${escapeHtml(message)}</p>`).join("");
  const specs = $("#spec-toggle").checked ? [validation.config.lsl, validation.config.usl] : null;
  $("#spec-legend").hidden = !specs;
  $("#xchart").innerHTML = `${chart("Carta X̄", xr.means, xr.xbarLimits, specs)}<p class="chart-legend">Linha central · limites de controle tracejados · ponto fora de controle em vermelho</p>`;
  $("#rchart").innerHTML = `${chart("Carta R", xr.ranges, xr.rangeLimits, null)}<p class="chart-legend">Linha central · limites de controle tracejados · ponto fora de controle em vermelho</p>`;
  const bad = new Set([...xr.outOfControl.xbar, ...xr.outOfControl.range]);
  $("#rows").innerHTML = parsed.groups.map((group, index) => `<tr class="${bad.has(index) ? "out" : ""}"><td>${index + 1}</td><td>${group.map(fmt).join("; ")}</td><td>${fmt(xr.means[index])}</td><td>${fmt(xr.ranges[index])}</td></tr>`).join("");
  $("#sw").textContent = fmt(capability.sigmaWithin);
  $("#so").textContent = fmt(capability.sigmaOverall);
  $("#within").innerHTML = metric("Cp", capability.within.cp) + metric("Cpk", capability.within.cpk) + metric("Cpu", capability.cpu) + metric("Cpl", capability.cpl);
  $("#overall").innerHTML = metric("Pp", capability.overall.cp) + metric("Ppk", capability.overall.cpk) + metric("Ppu", capability.ppu) + metric("Ppl", capability.ppl);
  $("#ppm").textContent = `Observações: ${capability.totalObservations} | Fora da especificação observado: ${capability.observedOutOfSpecCount} (${fmt(capability.observedPPM)} PPM)`;
  $("#control-limits").innerHTML = `<p>X̄: LIC ${fmt(xr.xbarLimits.lcl)} · LC ${fmt(xr.xbarLimits.cl)} · LSC ${fmt(xr.xbarLimits.ucl)}</p><p>R: LIC ${fmt(xr.rangeLimits.lcl)} · LC ${fmt(xr.rangeLimits.cl)} · LSC ${fmt(xr.rangeLimits.ucl)}</p>`;
  const target = validation.config.target == null ? "não informado" : fmt(validation.config.target);
  $("#spec-limits").innerHTML = `<p>LSL ${fmt(validation.config.lsl)} · Alvo ${target} · USL ${fmt(validation.config.usl)} ${escapeHtml(validation.config.unit)}</p><p>Característica: ${escapeHtml(validation.config.name)}</p>`;
}

$("#calculate").addEventListener("click", calculate);
$("#spec-toggle").addEventListener("change", () => {
  if (!$("#results").hidden) calculate();
});
$("#clear").addEventListener("click", () => {
  textarea.value = "";
  $("#errors").innerHTML = "";
  $("#results").hidden = true;
});
$("#example").addEventListener("click", () => {
  form.elements.subgroupSize.value = "5";
  textarea.value = generateSampleData().map(group => group.join("; ")).join("\n");
  calculate();
});

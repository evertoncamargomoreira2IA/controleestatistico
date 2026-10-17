import { parseMeasurement, validateConfig, validateSubgroups, computeXbarR, computeCapability } from "../src/spc.js";

const cases = [];

function test(name, fn) {
  try {
    fn();
    cases.push({ name, ok: true });
  } catch (error) {
    cases.push({ name, ok: false, error: error.message });
  }
}

function near(actual, expected) {
  if (Math.abs(actual - expected) > 1e-9) throw new Error(`esperado ${expected}, recebido ${actual}`);
}

function assert(condition, message = "assertiva falhou") {
  if (!condition) throw new Error(message);
}

const groups = [[1, 2, 3], [2, 3, 4], [3, 4, 5]];
const config = { name: "X", unit: "", lsl: 0, target: null, usl: 6, subgroupSize: 3 };

// Soma dos quadrados dos desvios em torno de 3: 12; variância amostral: 12/8 = 1,5.
test("estatísticas Xbar-R e índices conferidos à mão", () => {
  const xr = computeXbarR(groups, 3);
  const capability = computeCapability(groups, config, xr);
  [2, 3, 4].forEach((value, index) => near(xr.means[index], value));
  [2, 2, 2].forEach((value, index) => near(xr.ranges[index], value));
  near(xr.grandMean, 3);
  near(xr.meanRange, 2);
  near(xr.xbarLimits.lcl, 0.954);
  near(xr.xbarLimits.ucl, 5.046);
  near(xr.rangeLimits.lcl, 0);
  near(xr.rangeLimits.ucl, 5.148);
  near(capability.sigmaWithin, 2 / 1.693);
  near(capability.within.cp.value, 1.693 / 2);
  near(capability.within.cpk.value, 1.693 / 2);
  near(capability.sigmaOverall, Math.sqrt(1.5));
  near(capability.overall.cp.value, 1 / Math.sqrt(1.5));
  near(capability.overall.cpk.value, 1 / Math.sqrt(1.5));
});

test("parseMeasurement rejeita entradas inválidas e infinitas", () => {
  ["", "Infinity", "1e999", "12abc"].forEach(value => assert(parseMeasurement(value).error, `aceitou ${value}`));
});

test("aceita vírgula decimal", () => near(parseMeasurement("1,25").value, 1.25));
test("rejeita subgrupo desigual", () => assert(!validateSubgroups([[1, 2], [1]], 2).valid));
test("rejeita n fora de 2..10", () => assert(!validateConfig({ ...config, subgroupSize: 11 }).valid));
test("rejeita LSL >= USL", () => assert(!validateConfig({ ...config, lsl: 6, usl: 6 }).valid));
test("rejeita alvo fora de [LSL, USL]", () => assert(!validateConfig({ ...config, target: 7 }).valid));

test("identifica subgrupo fora de controle no índice esperado", () => {
  const sample = [...Array.from({ length: 10 }, () => [0, 1, 2]), [10, 11, 12]];
  const result = computeXbarR(sample, 3);
  assert(result.outOfControl.xbar.length === 1);
  assert(result.outOfControl.xbar[0] === 10, "esperado índice zero-based 10 (subgrupo 11)");
});

test("PPM observado conta valores fora da especificação", () => {
  const sample = [[-1, 1, 7], [2, 3, 4]];
  const sampleConfig = { ...config, subgroupSize: 3 };
  const xr = computeXbarR(sample, 3);
  const capability = computeCapability(sample, sampleConfig, xr);
  assert(capability.observedOutOfSpecCount === 2);
  near(capability.observedPPM, 333333.3333333333);
});

test("R̄ zero retorna Cp/Cpk nulos", () => {
  const sample = [[2, 2, 2], [2, 2, 2]];
  const result = computeCapability(sample, config, computeXbarR(sample, 3));
  assert(result.within.cp.value === null);
  assert(result.within.cpk.value === null);
});

test("menos de dois subgrupos não calcula", () => {
  let caught = false;
  try {
    computeXbarR([[1, 2, 3]], 3);
  } catch {
    caught = true;
  }
  assert(caught);
});

const passed = cases.filter(item => item.ok).length;
const failed = cases.length - passed;
window.__TEST_RESULTS__ = { passed, failed, failures: cases.filter(item => !item.ok).map(item => ({ name: item.name, error: item.error })) };
document.querySelector("#summary").textContent = `${passed} passaram; ${failed} falharam; total ${cases.length}.`;
document.querySelector("#results").innerHTML = cases.map(item => `<li style="color:${item.ok ? "#176b35" : "#a11b12"}">${item.ok ? "PASSOU" : "FALHOU"} — ${item.name}${item.error ? `: ${item.error}` : ""}</li>`).join("");

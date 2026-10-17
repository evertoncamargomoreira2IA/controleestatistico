import { CONSTANTS } from "./constants.js";

export function parseMeasurement(texto) {
  const value = String(texto ?? "").trim();
  const pattern = /^[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?$/;
  if (!value || !pattern.test(value)) {
    return { value: null, error: "Informe um número válido (use vírgula ou ponto decimal)." };
  }
  const number = Number(value.replace(",", "."));
  if (!Number.isFinite(number)) {
    return { value: null, error: "O valor deve ser finito." };
  }
  return { value: number, error: null };
}

function numeric(value) {
  return value === "" || value == null ? null : Number(String(value).replace(",", "."));
}

export function validateConfig({ name, unit, lsl, target, usl, subgroupSize } = {}) {
  const errors = [];
  const low = numeric(lsl);
  const high = numeric(usl);
  const aim = numeric(target);
  const n = Number(subgroupSize);
  if (!String(name ?? "").trim()) errors.push("Informe o nome da característica.");
  if (low === null || !Number.isFinite(low)) errors.push("LSL é obrigatório e deve ser numérico.");
  if (high === null || !Number.isFinite(high)) errors.push("USL é obrigatório e deve ser numérico.");
  if (Number.isFinite(low) && Number.isFinite(high) && low >= high) errors.push("LSL deve ser menor que USL.");
  if (target !== "" && target != null && (!Number.isFinite(aim) || (Number.isFinite(low) && Number.isFinite(high) && (aim < low || aim > high)))) {
    errors.push("O alvo deve ser numérico e estar entre LSL e USL.");
  }
  if (!Number.isInteger(n) || n < 2 || n > 10) errors.push("O tamanho do subgrupo deve ser um inteiro entre 2 e 10.");
  return {
    valid: errors.length === 0,
    errors,
    config: { name: String(name ?? "").trim(), unit: String(unit ?? "").trim(), lsl: low, target: aim, usl: high, subgroupSize: n }
  };
}

export function validateSubgroups(subgroups, n) {
  const errors = [];
  if (!Array.isArray(subgroups)) return { valid: false, errors: ["Informe os subgrupos em uma lista."] };
  subgroups.forEach((group, i) => {
    if (!Array.isArray(group) || group.length !== n) errors.push(`Subgrupo ${i + 1}: esperado tamanho ${n}; recebido ${Array.isArray(group) ? group.length : 0}.`);
    (Array.isArray(group) ? group : []).forEach((value, j) => {
      if (!Number.isFinite(value)) errors.push(`Subgrupo ${i + 1}, posição ${j + 1}: valor deve ser finito.`);
    });
  });
  return { valid: errors.length === 0, errors };
}

export function computeXbarR(subgroups, n) {
  const checked = validateSubgroups(subgroups, n);
  if (!checked.valid) throw new Error(checked.errors.join("\n"));
  if (subgroups.length < 2) throw new Error("São necessários pelo menos 2 subgrupos para calcular limites.");
  const constants = CONSTANTS[n];
  const means = subgroups.map(group => group.reduce((sum, value) => sum + value, 0) / n);
  const ranges = subgroups.map(group => Math.max(...group) - Math.min(...group));
  const grandMean = means.reduce((sum, value) => sum + value, 0) / means.length;
  const meanRange = ranges.reduce((sum, value) => sum + value, 0) / ranges.length;
  const xbarLimits = {
    lcl: grandMean - constants.A2 * meanRange,
    cl: grandMean,
    ucl: grandMean + constants.A2 * meanRange
  };
  const rangeLimits = {
    lcl: constants.D3 * meanRange,
    cl: meanRange,
    ucl: constants.D4 * meanRange
  };
  const outOfControl = {
    xbar: means.flatMap((value, index) => value < xbarLimits.lcl || value > xbarLimits.ucl ? [index] : []),
    range: ranges.flatMap((value, index) => value < rangeLimits.lcl || value > rangeLimits.ucl ? [index] : [])
  };
  const warnings = [];
  if (subgroups.length < 20) warnings.push("Menos de 20 subgrupos: limites preliminares.");
  if (subgroups.length < 25) warnings.push("Recomenda-se avaliar pelo menos 25 subgrupos.");
  return { means, ranges, grandMean, meanRange, xbarLimits, rangeLimits, outOfControl, subgroupCount: subgroups.length, warnings };
}

function nullMetric(reason) {
  return { value: null, reason };
}

export function computeCapability(subgroups, config, xr) {
  const data = subgroups.flat();
  const count = data.length;
  const mean = data.reduce((sum, value) => sum + value, 0) / count;
  const variance = count > 1 ? data.reduce((sum, value) => sum + (value - mean) ** 2, 0) / (count - 1) : 0;
  const sigmaOverall = Math.sqrt(variance);
  const sigmaWithin = xr.meanRange / CONSTANTS[config.subgroupSize].d2;
  const { lsl, usl } = config;
  const metrics = (sigma, center, label) => {
    if (sigma === 0) {
      return {
        sigma,
        cp: nullMetric(`${label} é zero; índices não definidos.`),
        cpk: nullMetric(`${label} é zero; índices não definidos.`),
        cpu: nullMetric(`${label} é zero.`),
        cpl: nullMetric(`${label} é zero.`)
      };
    }
    const cpu = (usl - center) / (3 * sigma);
    const cpl = (center - lsl) / (3 * sigma);
    return {
      sigma,
      cp: { value: (usl - lsl) / (6 * sigma), reason: null },
      cpu: { value: cpu, reason: null },
      cpl: { value: cpl, reason: null },
      cpk: { value: Math.min(cpu, cpl), reason: null }
    };
  };
  const within = metrics(sigmaWithin, xr.grandMean, "Sigma within");
  const overall = metrics(sigmaOverall, mean, "Sigma overall");
  const outsideCount = data.filter(value => value < lsl || value > usl).length;
  return {
    within, overall, sigmaWithin, sigmaOverall, cpu: within.cpu, cpl: within.cpl,
    ppu: overall.cpu, ppl: overall.cpl, totalObservations: count,
    observedOutOfSpecCount: outsideCount, observedPPM: outsideCount / count * 1_000_000,
    overallMean: mean
  };
}

export function assessment(xr, capability) {
  const stable = xr.outOfControl.xbar.length === 0 && xr.outOfControl.range.length === 0;
  const messages = [
    stable ? "Processo estável pelo critério de pontos fora de 3σ." : "Processo instável: há ponto(s) fora dos limites de controle de 3σ.",
    "Somente o critério de pontos fora de 3σ é avaliado; regras Nelson/Western Electric não estão implementadas.",
    stable ? "Capabilidade pode ser interpretada com cautela após validar as premissas." : "Capabilidade calculada, mas sua interpretação deve aguardar estabilidade do processo.",
    "Premissas: normalidade não testada; MSA não avaliado; subgrupos racionais assumidos.",
    "Este protótipo não certifica conformidade com AIAG, IATF ou ISO.",
    ...xr.warnings
  ];
  if (capability.sigmaWithin === 0) messages.push("Sigma within igual a zero: Cp/Cpk indefinidos.");
  if (capability.sigmaOverall === 0) messages.push("Sigma overall igual a zero: Pp/Ppk indefinidos.");
  return { stable, messages };
}

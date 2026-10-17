function mulberry32(seed) {
  return function random() {
    let value = seed += 0x6D2B79F5;
    value = Math.imul(value ^ value >>> 15, value | 1);
    value ^= value + Math.imul(value ^ value >>> 7, value | 61);
    return ((value ^ value >>> 14) >>> 0) / 4294967296;
  };
}

export function generateSampleData(seed = 20261015) {
  const random = mulberry32(seed);
  const normal = () => {
    const u1 = Math.max(random(), Number.EPSILON);
    const u2 = random();
    return Math.sqrt(-2 * Math.log(u1)) * Math.cos(2 * Math.PI * u2);
  };
  return Array.from({ length: 25 }, (_, index) => {
    const shift = index === 17 ? 0.25 : 0;
    return Array.from({ length: 5 }, () => (10 + shift + normal() * 0.08).toFixed(3));
  });
}

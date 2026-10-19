import '../lib/chartjs/chart.umd.js';

export function dispose(...elements) { for (const e of elements) { const c = Chart.getChart(e); if (c) c.destroy(); } }
export function render(xCanvas, rCanvas, distribution, result) {
    dispose(xCanvas, rCanvas, distribution);
    const r = result;
    const labels = r.points.map(p => p.position);
    const common = {responsive:true, maintainAspectRatio:false, animation:false,
        plugins:{legend:{display:false}}, scales:{x:{title:{display:true,text:'Amostra'},grid:{color:'#eef0f3'}},y:{grid:{color:'#eef0f3'}}}};
    const line = (label, value, color, dash=[]) => ({label,data:labels.map(()=>value),borderColor:color,borderDash:dash,borderWidth:1.5,pointRadius:0,fill:false});
    const measured = (label, values, chart) => ({label,data:values,borderColor:'#1414fa',backgroundColor:'#1414fa',borderWidth:2,pointRadius:3,
        pointBackgroundColor:r.points.map(p=>r.signals.some(s=>s.chart===chart && p.position>=s.from && p.position<=s.to)?'#d92d20':'#1414fa')});
    const xData = [measured('Médias',r.points.map(p=>p.mean),'X̄'),line('Média global',r.mean,'#888',[6,6]),line('LIC',r.xLower,'#ff3030',[8,6]),line('LSC',r.xUpper,'#ff3030',[8,6])];
    if(r.nominal!==null) xData.push(line('Nominal',r.nominal,'#555'));
    if(r.lsl!==null) xData.push(line('LSL',r.lsl,'#ff3030'));
    if(r.usl!==null) xData.push(line('USL',r.usl,'#ff3030'));
    const all = xData.flatMap(s=>s.data);
    let min=Math.min(...all),max=Math.max(...all),margin=(max-min)*0.06 || Math.max(Math.abs(max)*0.01,0.01);
    min-=margin;max+=margin;
    new Chart(xCanvas,{type:'line',data:{labels,datasets:xData},options:{...common,scales:{...common.scales,y:{...common.scales.y,min,max}}}});
    new Chart(rCanvas,{type:'line',data:{labels,datasets:[measured('Amplitude',r.points.map(p=>p.range),'R'),line('R médio',r.averageRange,'#888',[6,6]),line('LIC',r.rLower,'#ff3030',[8,6]),line('LSC',r.rUpper,'#ff3030',[8,6])]},options:{...common,scales:{...common.scales,y:{...common.scales.y,beginAtZero:true}}}});
    const width=(max-min)/6;
    const bins=Array.from({length:6},(_,i)=>({y:min+(i+.5)*width,x:0}));
    for(const p of r.points) bins[Math.min(5,Math.max(0,Math.floor((p.mean-min)/width)))].x++;
    new Chart(distribution,{type:'bar',data:{datasets:[{label:'Distribuição das médias',data:bins,backgroundColor:'#1414fa',barThickness:35}]},options:{responsive:true,maintainAspectRatio:false,animation:false,indexAxis:'y',plugins:{legend:{display:false}},scales:{x:{min:0,ticks:{precision:0},title:{display:true,text:'Frequência'}},y:{type:'linear',min,max,ticks:{display:false},grid:{display:false}}}}});
}

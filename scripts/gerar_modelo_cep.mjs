import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const wb = Workbook.create();
const s = wb.worksheets.add('Coleta CEP');
s.showGridLines = false;
s.getRange('A1:H38').format.font = { name: 'Arial', size: 11, color: '#243746' };
s.getRange('A1:H38').format.rowHeight = 23;
s.getRange('A1:H38').format.columnWidth = 16;
s.getRange('A:A').format.columnWidth = 12;
s.getRange('G:H').format.columnWidth = 24;
s.getRange('A2').values = [['Coleta CEP 25 × 3']];
s.getRange('A2').format.font = { name: 'Arial', size: 16, bold: true, color: '#006BB6' };
s.getRange('A4:H5').values = [
  ['Processo', null, null, null, 'Característica', null, null, null],
  ['Instrumento', null, 'Data / hora', null, 'Turno', null, 'Lote', null],
];
for (const r of ['B4:D4','F4:H4','B5','D5','F5','H5']) s.getRange(r).format.fill = '#FFF2CC';
s.getRange('A7:H7').values = [['Amostra', 'Coleta 1', 'Coleta 2', 'Coleta 3', 'Média', 'Amplitude', 'Observação', 'Responsável']];
s.getRange('A7:H7').format = { fill: '#006BB6', font: { name:'Arial', size:11, bold:true, color:'#FFFFFF' }, horizontalAlignment:'center' };
s.getRange('A8:A32').values = Array.from({length:25}, (_,i)=>[i+1]);
s.getRange('B8:D32').format.fill = '#FFF2CC';
s.getRange('G8:H32').format.fill = '#FFF2CC';
s.getRange('B8:F32').setNumberFormat('0.0000');
s.getRange('E8:F32').formulas = Array.from({length:25}, (_,i)=>{
 const r=i+8; return [`=IF(COUNT(B${r}:D${r})=3,AVERAGE(B${r}:D${r}),"")`, `=IF(COUNT(B${r}:D${r})=3,MAX(B${r}:D${r})-MIN(B${r}:D${r}),"")`];
});
s.getRange('A34').values = [['Uso no sistema']];
s.getRange('A34').format.font.bold = true;
s.getRange('A35').values = [['Preencha as células amarelas. Cada linha representa uma amostra com três leituras na mesma unidade.']];
s.getRange('A36').values = [['Copie somente B8:D32 e cole em CEP > Colar leituras do Excel, sem cabeçalho, média ou amplitude.']];
s.getRange('A37').values = [['Registre instrumento, data, turno e lote no sistema. Esta ficha é para colagem; não envie no importador de base.']];
s.getRange('A38').values = [['Para importar na base, use o template XLSX da característica em Coleta. Média só aparece com três valores numéricos.']];
s.freezePanes.freezeRows(7);
// Check blank, zero, negative and incomplete readings, restoring the empty template.
s.getRange('B8:D8').values = [[0,-3,3]];
wb.recalculate();
if (s.getRange('E8:F8').values[0][0] !== 0 || s.getRange('E8:F8').values[0][1] !== 6) throw new Error('Mean/range check failed');
s.getRange('D8').values = [[null]];
wb.recalculate();
if (s.getRange('E8').values[0][0] !== '') throw new Error('Partial reading must not produce mean');
s.getRange('B8:D8').clear({applyTo:'contents'});
wb.recalculate();
await fs.mkdir('artifacts/modelo-cep', {recursive:true});
console.log((await wb.inspect({kind:'match', searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!', options:{useRegex:true,maxResults:5},maxChars:1000})).ndjson);
const image = await wb.render({sheetName:s.name,range:'A1:H38',scale:1.5,format:'png'});
await fs.writeFile('artifacts/modelo-cep/preview.png',new Uint8Array(await image.arrayBuffer()));
await fs.mkdir('src/Qms.Web/wwwroot/modelos',{recursive:true});
await (await SpreadsheetFile.exportXlsx(wb)).save('src/Qms.Web/wwwroot/modelos/Modelo_CEP_25x3.xlsx');
console.log('Modelo CEP exportado; testes de fórmulas aprovados.');
await fs.rm('src/Qms.Web/wwwroot/modelos/Modelo_CEP_25x3.xlsx.inspect.ndjson', {force:true});
process.exit(0);

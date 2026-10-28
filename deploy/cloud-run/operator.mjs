import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
const root = fileURLToPath(new URL('../..',import.meta.url));
const localDotnet = resolve(root,'.tools/dotnet/dotnet.exe');
const dotnet = process.env.HHH_DOTNET || (existsSync(localDotnet) ? localDotnet : 'dotnet');
const configuration = process.env.HHH_OPERATOR_CONFIGURATION || 'Release';
const binary = resolve(root,`src/Hhh.Operator/bin/${configuration}/net10.0/Hhh.Operator.dll`);
if (!existsSync(binary)) throw new Error('Build Hhh.Operator in the selected configuration before running cloud operations.');
const connections = JSON.parse(readFileSync(resolve(root,'.artifacts/cloud/private/connections.json'),'utf8'));
const result = spawnSync(dotnet, [binary, ...process.argv.slice(2)], {
  cwd:root,
  env: { ...process.env, ...(existsSync(localDotnet) ? {DOTNET_ROOT:resolve(root,'.tools/dotnet')} : {}), HHH_POSTGRES_CONNECTION_STRING:connections.operator },
  stdio:'inherit', windowsHide:true,
});
process.exitCode=result.status ?? 1;
